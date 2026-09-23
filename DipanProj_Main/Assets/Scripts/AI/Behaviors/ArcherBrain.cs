using UnityEngine;

/// <summary>
/// 射手型（弓／弩／火槍…）的戰鬥模組：**先評估「站在這裡射得到嗎」，射不到才移動**。
///
/// 與 <see cref="ChaseBrain"/>（一路貼上去磨）和 <see cref="PounceBrain"/>（拉距離再撲）都不同，
/// 射手的決策核心是一個判斷句：<see cref="CanShootFrom"/> —— 距離在射程內 **且** 中間沒有障礙物擋住飛行物。
///   ‧ 成立 → **原地拉弓、放箭**，一步都不移動；
///   ‧ 不成立 → 移動一小段（太遠就靠近／太近就後退／視線被擋就側移換角度），**走完重新評估**，直到成立。
/// 每射完一發會站定 idle 一段（<see cref="IdleAfterShotMin"/>~<see cref="IdleAfterShotMax"/>），
/// 這段是玩家的輸出窗口，也是「牠在上箭」的節奏感來源。
///
/// 四段狀態機（`Phase`）：
///   Observe     站定評估（剛發現目標、移動完一段、射完一發之後都會回到這裡）
///   Reposition  往算好的落點移動一小段就停（綁距離不綁時間）
///   Draw        站定拉弓 ＝ 玩家看得見的預告窗口（attack 動畫在這段持續播）
///   ↑ Draw 結束就放箭，然後回 Observe（用較長的 idle 秒數＝射擊間隔）
///
/// **射擊本身不在這支裡**：委派給 <see cref="MonsterWeaponUser.TryUse"/>（武器 ID 由 `MonsterData.Weapon` 指定），
/// 它會走 <see cref="WeaponCastService"/>——與玩家的 `ShootNormal` 同一份彈道程式。
/// 所以「射什麼、幾發、會不會分裂／追蹤／反彈」全部由 WeaponTable/RecipeTable 決定，本 Brain 一行都不管。
/// 傷害同理（走 `CombatSystem`），attack 動畫也是既有系統白送的。
///
/// ⚠ **移動一律綁「距離」不綁「時間」**（秒數只是被牆卡住時的保險上限）：綁時間的話 CSV 的 `Speed`
/// 一調小，每次挪動的距離就等比縮水，變成原地抽動——同 readme/PROBLEMS.md **F20** 踩過的坑。
/// </summary>
public class ArcherBrain : IMonsterBrain
{
    // ── 射程與站位（世界單位）──
    // ⚠ 這裡的「射程」是**行為射程**，不是子彈真正能飛多遠：配方 44 是 Speed 15 × LifeTime 3 ＝ 飛 45 單位，
    //    而畫面高才 10 ⇒ 直接拿子彈壽命當射程的話，牠會從畫面外看不見的地方開始射你。
    //    7 單位 ≈ 畫面寬的一半，玩家看得到牠、也還有走位空間。
    // 2026-09-17：作者實測「7 太遠」⇒ 砍 1/4 成 5.25（≈ 畫面寬的 1/3，玩家看得很清楚是誰在射他）。
    const float ShootRange = 5.25f;
    const float MinRange = 2.0f;         // 比這還近就後退（射手被貼身該拉開，不是站著硬射）
    // 想站的距離。維持「約 ShootRange 的 0.79」這個比例：射程內留一點餘裕，
    // 玩家走兩步不會立刻脫離射程、害牠又要重新跑位。改 ShootRange 時記得一起按比例調。
    const float PreferredRange = 4.1f;
    // 視線檢查的「飛行物粗細」。比子彈的碰撞半徑（配方 Radius，預設 0.1）寬一點：
    // 貼著牆角擦過去在判定上算通過、實際上常常會撞到，寬一點寧可多繞一步也不要射在柱子上。
    const float LineOfSightRadius = 0.18f;

    // ── 節奏（秒）──
    const float ObserveMin = 0.4f;       // 站定評估：剛發現目標、或移動完一段之後
    const float ObserveMax = 0.8f;
    //    ↑ 這是「CSV `ObserveTime` 留空」時的預設。逐怪可覆寫（2026-09-23）：填 0 ＝ 不觀察，填正數 ＝ 固定秒數。
    //      見 ObserveSeconds。射完一發後的 IdleAfterShot 是射擊節奏，不歸這欄管。
    // ⭐ **放箭時機＝attack 序列圖「弩已經舉到定位」的那一幀**，不是一個拍腦袋的秒數。
    //   2026-09-17 作者實測回報「武器還沒提起來，箭就射出去了」——根因見下方 Draw 階段的註解。
    //   Wolf Archers 的 attack 25 張：1~3 預備、4~9 往前推、**10~12 完全水平前伸到位**、13~19 維持、20~25 收弩。
    //   實際秒數 = (ReleaseFrame - 1) ÷ 這隻怪的 AnimFPS ⇒ **CSV 改 AnimFPS 不會讓時機跑掉**。
    //   ⚠ 第一版取 11（弩剛舉定的那一幀），作者實測仍覺得「太早飛出去」⇒ **2026-09-17 改 14**，
    //     落在「維持瞄準」那一段的開頭，視覺上是「舉定、穩住、才放」。@14fps ＝ 0.93 秒。
    //   ⚠ **2026-09-22 改成逐怪可設**：這個值原本是全域常數，但一隻怪的放彈幀是**牠那套 attack 序列圖的性質**，
    //     不是所有射手共用的手感——ZhaYu_Gun 的 attack 只有 22 張、幀 6 就完全水平舉定，用 14 會晚半秒才吐炮彈。
    //     現在改成優先讀 `MonsterData.ReleaseFrame`（CSV 表尾欄），**留空＝沿用下面這個 14** ⇒ 狂族弩手零變化。
    const int ReleaseFrameFallback = 14;
    const float FallbackAnimFps = 14f;   // 怪物資料拿不到 AnimFPS 時的退路（＝目前 CSV 的值）
    // 放箭之後再維持 attack 動畫多久（收弩的後半段）。這段與射擊間隔的 idle 重疊，不額外拉長節奏。
    const float FollowThroughSeconds = 0.8f;
    // 射完站定多久才進下一輪（＝射擊間隔，作者指定先寫死在模組裡）。
    // ⚠ 這是「行為上的間隔」，與武器配方的 FireInterval 是兩層節流：配方那層在 MonsterWeaponUser 裡，
    //    兩者取較嚴格的一個。想讓牠射快一點，先改這裡。
    const float IdleAfterShotMin = 1.2f;
    const float IdleAfterShotMax = 1.8f;
    // 「射得到，但武器冷卻還沒好」時原地再等這麼久就重新評估。
    // ⚠ 這種情況**不能跑去移動**——牠明明站在射得到的位置，跑開只會看起來很蠢（而且回來又要重走）。
    const float WeaponWaitSeconds = 0.15f;

    // ── 移動（綁距離）──
    const float StepDistMin = 1.2f;      // 一次挪動的距離
    const float StepDistMax = 2.2f;
    const float StepMaxSeconds = 2.0f;   // 保險上限（被牆卡住時才會用到）
    const float ArriveDistSq = 0.3f * 0.3f;
    const float SideAngleMin = 35f;      // 視線被擋時繞多少度找角度
    const float SideAngleMax = 65f;
    const float SideFlipChance = 0.35f;  // 偶爾換邊繞，免得一直往同一邊卡死在同一根柱子後面

    /// <summary>除錯：把每次決策（能不能射／為什麼不能／挪去哪）印出來。抓站位手感時打開。</summary>
    public static bool DebugLog = false;

    enum Phase { Observe, Reposition, Draw }

    Phase _phase = Phase.Observe;
    float _phaseUntil;
    Vector2 _stepTarget;
    float _stepStart;
    int _side = 1;
    int _envMask = -1;
    Collider2D _selfCol;
    bool _inited;
    // 這一套 attack 動畫要演到什麼時候（Time.time）。0 ＝ 現在沒有在演。
    // Draw 與 Observe 兩個階段都靠它決定要不要繼續替動畫「續命」（見 KeepAttackPose）。
    float _attackAnimEnd;
    float _releaseAt;        // 這一發的放箭時刻（Time.time）

    public void Think(in MonsterContext ctx)
    {
        MonsterActuator act = ctx.Actuator;
        if (act == null) return;
        Vector2 pos = act.transform.position;

        // 身體框快取：出手點與視線起點都要從框外緣算（見 MuzzleOffset），沒有它會退回一個寫死的估值。
        if (_selfCol == null) _selfCol = act.GetComponent<Collider2D>();

        if (!_inited)
        {
            _inited = true;
            _side = Random.value < 0.5f ? 1 : -1;   // 一群弓手才不會整排往同一邊挪
            _phaseUntil = Time.time + ObserveSeconds(ctx);
        }

        // 目標取「最近的敵對目標」（同 PounceBrain / WarBrain）：對 Enemy 陣營與三方陣營都成立，不必寫特例。
        Transform target = Nearest(pos, ctx.Enemy, ctx.Player);
        if (target == null)
        {
            act.Stop();
            _attackAnimEnd = 0f;
            _phase = Phase.Observe;
            _phaseUntil = Time.time + ObserveSeconds(ctx);
            return;
        }

        Vector2 tp = target.position;
        float dist = Vector2.Distance(pos, tp);

        switch (_phase)
        {
            // ── 站定評估：這一段就是作者說的「先觀察」──
            case Phase.Observe:
                act.Stop();
                KeepAttackPose(ctx);   // 上一發的收弩動作還沒演完就繼續維持（與 idle 秒數重疊，不額外拉長節奏）
                if (Time.time < _phaseUntil) break;

                // ⭐ **所有檢查都在這裡做完**——射程、視線、武器冷卻。
                //    一旦進了 Draw 就保證會放箭，見 Draw 階段的「鐵則」註解。
                if (!CanShootFrom(pos, tp, dist))
                {
                    BeginStep(pos, tp, dist);   // 射不到 ⇒ 挪位
                }
                else if (!WeaponReady(ctx))
                {
                    // 射得到、只是武器還在冷卻（含剛生成時的起手緩衝）⇒ **站在原地等**，不要跑開。
                    _phaseUntil = Time.time + WeaponWaitSeconds;
                    if (DebugLog) Debug.Log($"[Archer] 射得到但武器冷卻中 → 原地等 {WeaponWaitSeconds:F2}s");
                }
                else
                {
                    BeginDraw(ctx, dist);   // 站在這裡就射得到 ⇒ 一步都不用移動，直接拉弓（作者指定的行為）
                }
                break;

            // ── 移動一小段就停，停下來重新評估（不是一路衝到目標身上）──
            case Phase.Reposition:
            {
                bool arrived = (_stepTarget - pos).sqrMagnitude <= ArriveDistSq;
                bool timeout = Time.time - _stepStart >= StepMaxSeconds;   // 被牆卡住的保險
                // 移動途中若已經滿足射擊條件就直接停下來射——不必把這一步走完，
                // 否則會出現「明明已經走到射得到的位置，卻還要再走兩步才肯開火」。
                if (arrived || timeout || CanShootFrom(pos, tp, dist))
                {
                    act.Stop();
                    _phase = Phase.Observe;
                    _phaseUntil = Time.time + ObserveSeconds(ctx);
                    break;
                }
                act.MoveTowards(_stepTarget);
                break;
            }

            // ── 拉弓：站定不動＝預告窗口。**attack 動畫在進入這個階段時就已經起播**（見 BeginDraw）──
            //
            // ⚠ 2026-09-17 修：原本是「先 TryUse 射出去，射成功後 MonsterWeaponUser 才呼叫 NotifySkillCast」，
            //   也就是**箭先飛出去、動畫才開始播** ⇒ 作者實機回報「武器都還沒提起來，弓箭就射出來了」。
            //   順序必須反過來：動畫先起播，等它演到「弩已經舉到定位」那一幀（ReleaseFrame）才放箭。
            //
            // ⭐⭐ **射手型模組的鐵則（作者 2026-09-17 拍板）：只要 attack 動作播了出去，就一定要射出箭。**
            //   這一段**刻意不做任何中途取消**——不重驗射程、不重驗視線、不看武器冷卻。
            //   原因：拉弓要 0.7 秒，玩家在這段時間很容易走出射程或閃到柱子後面，
            //   第一版會因此取消放箭 ⇒ 作者實機回報「**十字弓已經提起來了卻不射箭，看起來很像 bug**」。
            //   **弓箭滿場飛是可接受、甚至有趣的**；「舉了弩卻沒箭」不是。
            //   所有該擋的檢查（射程／視線／武器冷卻）都在 Observe 決定拉弓的那一刻做完了。
            //   ⚠ **之後做任何射手型 Brain 都照這條**；真的需要「可取消的瞄準」再另開一支 Brain，不要改這裡。
            case Phase.Draw:
            {
                act.Stop();
                KeepAttackPose(ctx);

                if (Time.time < _releaseAt) break;   // 還沒到放箭那一幀（這裡沒有任何取消條件，見上方鐵則）

                MonsterWeaponUser weapon = (ctx.Self != null) ? ctx.Self.WeaponUser : null;
                if (weapon != null)
                {
                    // 射向＝目標**當下**的位置（TryUse 內部用 target.position 算），所以玩家走位時箭會往他現在的地方飛，
                    // 但**不做預判攔截**——躲不躲得掉是玩家的事，不是演算法的聰明程度決定的（同 PounceBrain 的取捨）。
                    bool fired = weapon.TryUse(target);
                    if (!fired)
                    {
                        // 走到這裡代表「動作播了卻沒射出去」＝上面那個鐵則被破壞了。
                        // Observe 已經檢查過 Ready，所以剩下的可能是：武器 ID 沒填／配方不是 Normal／BulletPrefab 沒設。
                        // 這是設定問題，不是手感問題，所以無條件印出來（不藏在 DebugLog 後面）。
                        string who = (ctx.Self != null) ? ctx.Self.name : "(未知)";
                        Debug.LogWarning($"[Archer] {who} 播了 attack 卻沒射出東西——" +
                                         "檢查 MonsterData 的 Weapon 欄是不是 WeaponTable 的武器 ID、該武器的 Mode 是不是 Normal、" +
                                         "以及場景 WeaponManager 的 Bullet Prefab 有沒有設。");
                    }
                }
                else
                {
                    Debug.LogWarning("[Archer] 這隻怪沒有 MonsterWeaponUser（MonsterData 的 Weapon 欄要填 WeaponTable 的武器 ID，不是 Contact）");
                }

                _phase = Phase.Observe;
                _phaseUntil = Time.time + Random.Range(IdleAfterShotMin, IdleAfterShotMax);
                break;
            }
        }
    }

    /// <summary>
    /// 這一次站定評估要停多久：CSV `ObserveTime` 留空（-1）＝ <see cref="ObserveMin"/>~<see cref="ObserveMax"/> 隨機；
    /// 填 0 ＝ 不停（下一次 Think 就評估，一進射程就舉槍）；填正數 ＝ 固定秒數。
    /// <para>2026-09-23 作者：新手夢境教學裡玩家火力太強，射手「觀察」完幾乎沒機會出手 ⇒ 該場的 ZhaYu_Gun（32 號）填 0，
    /// 一般關卡的 21 號與狂族弩手留空維持原樣。</para>
    /// </summary>
    static float ObserveSeconds(in MonsterContext ctx)
    {
        float t = (ctx.Self != null) ? ctx.Self.ObserveTime : -1f;
        if (t < 0f) return Random.Range(ObserveMin, ObserveMax);
        return t;
    }

    /// <summary>
    /// 武器冷卻好了沒（配方的 `FireInterval`）。
    ///
    /// ⭐ 這個檢查刻意放在**決定要不要拉弓之前**，而不是放箭的時候：
    /// 射手型模組的鐵則是「動作播了就一定要射出箭」，所以冷卻沒好就**根本不要開始拉弓**，
    /// 而不是拉完弓才發現射不出來。沒有武器元件時回 true，讓下游的 warning 去提示設定問題。
    ///
    /// ⚠ 這裡讀 <c>MonsterWeaponUser.Ready</c> 還有一個**必要的副作用**：它會順便把武器解析完
    /// （起手緩衝就是解析時寫進冷卻的）。少了這一步，怪剛生成的第一發會拿到「冷卻是 0」的假答案、
    /// 拉完弓才撞上冷卻 ⇒ 每隻新怪的第一發都是空砲。見 readme/PROBLEMS.md **F25**。
    /// </summary>
    bool WeaponReady(in MonsterContext ctx)
    {
        MonsterWeaponUser w = (ctx.Self != null) ? ctx.Self.WeaponUser : null;
        return w == null || w.Ready;
    }

    /// <summary>
    /// 開始拉弓：**先讓 attack 動畫起播**，再依放彈幀（CSV `ReleaseFrame`，留空＝<see cref="ReleaseFrameFallback"/>）算出這一發什麼時候離手。
    ///
    /// ⭐ 放箭時機是從**序列圖的幀**換算的，不是一個固定秒數：
    /// `MonsterAnimator.SetState` 切到 Attack 時會把幀索引歸零、以 CSV 的 `AnimFPS` 起播，
    /// 所以第 N 幀出現在 `(N-1) ÷ AnimFPS` 秒。這樣**改 CSV 的 AnimFPS，放箭時機會自動跟著對**，
    /// 不必回來改這支程式；換一隻拉弓節奏不同的射手，只要改 CSV 的 `ReleaseFrame` 欄（留空＝14）。
    /// </summary>
    void BeginDraw(in MonsterContext ctx, float dist)
    {
        float fps = (ctx.Self != null && ctx.Self.AnimFPS > 0.01f) ? ctx.Self.AnimFPS : FallbackAnimFps;

        // 放彈幀：CSV 的 ReleaseFrame 優先，留空／0 就用退路值。
        // ⚠ 一律夾進這隻怪 attack 的實際張數——填太大的話那一幀永遠不會到，
        //   而 Draw 階段**沒有中途取消**（見檔頭鐵則）⇒ 會變成「舉著槍站在那裡一輩子不射」。
        int releaseFrame = (ctx.Self != null && ctx.Self.ReleaseFrame > 0) ? ctx.Self.ReleaseFrame : ReleaseFrameFallback;
        int attackFrames = (ctx.Self != null && ctx.Self.Anim != null)
                           ? ctx.Self.Anim.FrameCount(MonsterAnimator.State.Attack) : 0;
        if (attackFrames > 0) releaseFrame = Mathf.Clamp(releaseFrame, 1, attackFrames);

        float releaseDelay = Mathf.Max(0f, (releaseFrame - 1) / fps);

        _releaseAt = Time.time + releaseDelay;
        _attackAnimEnd = _releaseAt + FollowThroughSeconds;
        _phase = Phase.Draw;

        // 關鍵：這一行讓 attack 動畫**現在**就開始播（MonsterController.HandleVisuals 看到 casting 就切 Attack）。
        if (ctx.Self != null) ctx.Self.NotifySkillCast();

        if (DebugLog) Debug.Log($"[Archer] 原地可射（距離={dist:F2}、視線通）→ 拉弓，{releaseDelay:F2}s 後放箭（第 {releaseFrame} 幀 @ {fps:F0}fps）");
    }

    /// <summary>
    /// 讓 attack 動畫撐到 <see cref="_attackAnimEnd"/>。
    ///
    /// ⚠ 為什麼需要「續命」：`NotifySkillCast()` 只把 attack 姿勢延到 `Time.time + SkillCastAnimSeconds`，
    /// 而那個欄位預設只有 **0.6 秒**——比拉弓到放箭（Wolf Archers 是 0.71 秒）還短 ⇒ 只呼叫一次的話，
    /// 動畫會在放箭前就切回 idle。
    /// ⚠ 但也**不能無腦每幀續**：最後一次呼叫會把姿勢多撐 0.6 秒、拖過整套動作的結尾，
    /// 動畫就接著演第二輪的前段（看起來像「射完又舉一次弩」）。
    /// 所以只在「再續一次也不會超過結束時間」時才續。
    /// </summary>
    void KeepAttackPose(in MonsterContext ctx)
    {
        if (_attackAnimEnd <= 0f || ctx.Self == null) return;
        if (Time.time >= _attackAnimEnd) { _attackAnimEnd = 0f; return; }
        if (Time.time + ctx.Self.SkillCastAnimSeconds <= _attackAnimEnd) ctx.Self.NotifySkillCast();
    }

    /// <summary>
    /// **這支 Brain 的核心判斷**：站在 <paramref name="from"/> 射得到 <paramref name="targetPos"/> 嗎？
    /// 兩個條件都要成立：① 距離落在 <see cref="MinRange"/>~<see cref="ShootRange"/> 之間；
    /// ② 中間沒有障礙物會擋住飛行物（<see cref="HasLineOfFire"/>）。
    /// </summary>
    bool CanShootFrom(Vector2 from, Vector2 targetPos, float dist)
    {
        if (dist > ShootRange || dist < MinRange) return false;
        return HasLineOfFire(from, targetPos);
    }

    /// <summary>
    /// 飛行物飛得過去嗎——從出手點往目標掃一條有粗細的線（<c>CircleCast</c>），撞到障礙層就是被擋。
    /// ⚠ 用 <c>CircleCast</c> 而不是 <c>Linecast</c>：箭有體積，貼著牆角的那條「數學上通過」的細線，
    ///   實際射出去會撞在柱子邊上。障礙層與 <c>MonsterActuator</c>／<c>MapNavGrid</c> 同一份定義
    ///   （Environment + Water），所以「牠覺得射不過去」與「箭真的會被擋下」是同一個真相。
    /// </summary>
    bool HasLineOfFire(Vector2 from, Vector2 targetPos)
    {
        if (_envMask == -1) _envMask = LayerMask.GetMask("Environment", "Water");

        Vector2 d = targetPos - from;
        float len = d.magnitude;
        if (len < 0.0001f) return true;
        d /= len;

        // 起點推出自己的身體框：否則 CircleCast 的起始圓可能重疊到「怪自己正貼著的那面牆」，
        // 變成站在牆邊就永遠判定射不出去。
        float skip = MuzzleOffset();
        if (len <= skip) return true;

        RaycastHit2D hit = Physics2D.CircleCast(from + d * skip, LineOfSightRadius, d, len - skip, _envMask);
        return hit.collider == null;
    }

    float MuzzleOffset()
    {
        if (_selfCol == null) return 0.4f;
        return _selfCol.bounds.extents.magnitude + 0.1f;
    }

    /// <summary>
    /// 算出這一次要挪去哪，然後進 <see cref="Phase.Reposition"/>。三種情況：
    ///   ‧ 太遠 → 朝目標靠近到 <see cref="PreferredRange"/>；
    ///   ‧ 太近 → 往反方向退到 PreferredRange；
    ///   ‧ 距離剛好但視線被擋 → 保持距離繞一個角度，換個能射穿的角度。
    /// 三種都只走 <see cref="StepDistMin"/>~<see cref="StepDistMax"/> 一小段就停下來重新評估——
    /// 一次走到底的話，途中障礙物變了也不會發現。
    /// </summary>
    void BeginStep(Vector2 pos, Vector2 targetPos, float dist)
    {
        Vector2 away = pos - targetPos;
        if (away.sqrMagnitude < 1e-4f) away = Vector2.right;
        away.Normalize();

        Vector2 desired;
        string why;

        if (dist > ShootRange)
        {
            desired = targetPos + away * PreferredRange;   // 太遠：靠近（沿著兩點連線走，最短）
            why = "太遠";
        }
        else if (dist < MinRange)
        {
            desired = targetPos + away * PreferredRange;   // 太近：同一條線但往外 ⇒ 等於後退
            why = "太近";
        }
        else
        {
            // 距離沒問題，是被擋住 ⇒ 保持大致距離、繞一個角度找射線
            if (Random.value < SideFlipChance) _side = -_side;
            float keep = Mathf.Clamp(dist, MinRange, ShootRange);
            Vector2 dir = Rotate(away, Random.Range(SideAngleMin, SideAngleMax) * _side);
            desired = targetPos + dir * keep;
            why = "視線被擋";
        }

        Vector2 delta = desired - pos;
        float step = Random.Range(StepDistMin, StepDistMax);
        _stepTarget = delta.sqrMagnitude < 1e-4f ? pos : pos + delta.normalized * Mathf.Min(delta.magnitude, step);

        _stepStart = Time.time;
        _phase = Phase.Reposition;
        if (DebugLog) Debug.Log($"[Archer] 射不到（{why}，距離={dist:F2}）→ 挪到 {_stepTarget}");
    }

    static Transform Nearest(Vector2 pos, Transform a, Transform b)
    {
        if (a == null) return b;
        if (b == null) return a;
        float da = ((Vector2)a.position - pos).sqrMagnitude;
        float db = ((Vector2)b.position - pos).sqrMagnitude;
        return da <= db ? a : b;
    }

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
