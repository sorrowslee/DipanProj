using UnityEngine;

/// <summary>
/// 跳躍踐踏型（狂族皇家衛士這種「開場一記下馬威」的近戰）：
/// **一見到目標就躍向他、落地踩出一圈裂地 AOE，之後永遠改用一般追擊**。
///
/// 三段（`Phase`）：
///   Approach  太遠 → 正常速度走近，進到 LeapRangeMax 就起跳
///   Leap      **一生只跳這一次**：蓄力 → 騰空（程式位移＋拋物線抬升）→ 落地（裂痕＋傷害）→ 收招
///   Chase     之後每一幀都委派給 <see cref="ChaseBrain"/>，本 Brain 不再介入
///
/// <para><b>落點鎖定「起跳當下」目標的位置</b>，刻意不追蹤（作者 2026-09-18 拍板）。
/// 蓄力那 4 幀就是給玩家看的預告窗口，看到牠蹲下去就該側身閃開；
/// 改成像 <see cref="PounceBrain"/> 的衝刺那樣全程追蹤，等於「出手必中」，走位就沒有意義了。
/// 撲擊型可以追蹤，是因為牠一輪一輪不停地撲；這一記是一次性的，閃掉就該真的閃掉。</para>
///
/// <para><b>傷害不在這裡算</b>：落地時丟一個 <see cref="ImpactDamageArea"/>（短命圓形 trigger ＋ 既有的
/// <c>EnemyContactDamage</c>），陣營判定、玩家無敵幀、中央結算全部沿用既有那套。
/// 視覺是 <see cref="GroundCrackFx"/>（程序化 shader 裂痕），與傷害各自獨立、半徑可以不一樣。</para>
///
/// <para><b>動畫走 one-shot</b>（<see cref="MonsterAnimator.PlayOneShot"/>）而不是 SetState：
/// `HandleVisuals` 每幀都會依距離/位移覆寫狀態，SetState 撐不過下一幀。
/// 起跳與落地的時機寫成**幀號**（`TakeoffFrame`／`LandFrame`）而不是秒數，所以改 CSV 的 `AnimFPS`
/// 時整段節奏會自動對上（同 ArcherBrain 的放箭幀，見 readme/BOSS_MODULE.md §8.2b）。</para>
///
/// ⚠ 素材前提：`jump/` 資料夾的第 1~13 幀是**一次完整跳躍**。狂族皇家衛士那 25 張其實是「跳兩次」
///   （14~25 是第二輪），所以這裡只播前 13 張。換一隻怪時務必先量一次幀（見 §9 的 SOP）。
/// </summary>
public class LeapSlamBrain : IMonsterBrain
{
    // ── 觸發 ──
    // 起跳的最遠距離。⚠ **這個數字就是跳躍的速度上限**：騰空時間由動畫幀固定（(11-5)/AnimFPS，13fps 時 0.46 秒），
    // 距離越遠＝飛越快。5.0 ÷ 0.46 ≈ 10.9 單位/秒，剛好和戰狼的衝刺速度（Speed 3.5 × 3）同級；
    // 放到 7 就變成 15 單位/秒，畫面上讀起來像瞬移而不是跳躍。要跳更遠就得同時把 AnimFPS 調慢（騰空變久）。
    const float LeapRangeMax = 5.0f;
    const float MinLeapDist  = 1.2f;   // 落點至少離起跳點這麼遠（目標貼臉時也要往前踏一步，不要原地跳）

    // ── 幀事件（**1-based**，直接對應 jump/ 資料夾的檔名編號，肉眼對得起來）──
    // 狂族皇家衛士實測（量每一幀不透明像素的底邊高度）：
    //   1~4 蓄力下蹲（在地）｜5 離地｜6~9 騰空（最高在 8）｜10 下墜｜11 觸地｜12~13 落地緩衝
    const int TakeoffFrame = 5;
    const int LandFrame    = 11;
    const int EndFrame     = 13;
    const int TotalFrames  = 13;       // 只播第一輪（見類別註解）

    // ── 騰空 ──
    // 素材自帶的騰空只有約 63px ≈ 0.25 世界單位，畫面上幾乎看不出離地，所以程式再疊一段弧線。
    // 疊加的量就是這個常數（世界單位）；影子與 Y 排序會自動把它扣回地面（見 IAirborneVisual）。
    // ⭐ **以「角色身高」為單位，不是寫死的世界單位**：作者要的是「跳到主角頭上三個怪物的高度」，
    //    那是相對身高的描述；寫死的話換一隻體型不同的怪就得重調。
    //    身高取 `MonsterController.CharacterWorldHeight`(1.95) × 這隻怪的 Scale，所以大怪跳得更高、小怪更低。
    //    演進：0.55（第一版，「跳得不夠高」）→ 1.5（第二版，還是太矮）→ 3 個身高 ≈ 5.85 單位。
    const float HopHeightInBodies = 3.0f;
    const float FallbackBodyHeight = 1.95f;   // 取不到角色高時的退路（＝MonsterController 的預設值）

    // 素材裡**最高的那一幀**（量不透明像素底邊量出來的：幀 7~8 底邊 163~164 是最高點）。
    // 弧線的頂點對齊它，程式加的高度才會跟畫出來的姿勢同步；不對齊的話會出現
    // 「圖已經在下墜、程式還在把牠往上抬」的橡皮筋感。
    const int PeakFrame = 8;

    // 直線落點被擋住時，往左右試這些角度找替代落點（度）。順序＝由正前方往外散。
    static readonly float[] LandingAngles = { 0f, 18f, -18f, 36f, -36f, 54f, -54f, 72f, -72f };
    // ⭐ **到頂點時水平已經走完的比例**：0.85 ⇒ 下降那半段只剩 15% 的水平位移，看起來就是「躍到高處、垂直砸下來」。
    //    0.5（＝等速）的話是一條對稱的拋物線，讀起來是「飛過去」而不是「砸下來」。
    const float HorizAtPeak = 0.85f;

    // 跳躍動畫相對 AnimFPS 的倍率。**跳這麼高需要時間演**：1.0 之下騰空只有 0.46 秒，
    // 要在裡面升降 5.85 單位 ⇒ 垂直速度 25 單位/秒（玩家才 5），畫面上會像被彈射。
    // 0.65 ⇒ 蓄力 0.47s／騰空 0.71s／整段 1.42s，蓄力變長也讓預告窗口更清楚。
    // ⚠ 這是**整段**的倍率：想要「蓄力不變、只有騰空變慢」得做分段 fps（PlayOneShot 目前只吃一個倍率），還沒做。
    const float JumpFpsMul = 0.65f;

    // ── 落地 ──
    // 裂痕視覺半徑 = 殺傷半徑 × 此。**打不到的邊緣也該裂開**，踐踏才有份量。
    // 演進：1.35（太小）→ 2.2（太大）→ **1.47**（＝2.2 的 2/3，作者 2026-09-18 定）。
    // LeapRadius 1.6 之下 ⇒ 視覺半徑 2.35、直徑 4.7 世界單位（角色高約 2.5），壓扁後畫面上是 4.7 × 2.35 的橢圓。
    // ⚠ 這只放大**視覺**，殺傷仍嚴格走 LeapRadius（同 GroundEffect 的 visualScale / radiusScale 之分）。
    //   要連傷害一起縮，改的是 MonsterData.csv 的 `LeapRadius` 欄，不是這裡。
    const float CrackRadiusMul = 1.47f;
    const float DefaultRadius  = 1.6f;   // CSV LeapRadius 留空時的退路
    const float DefaultDamageMul = 2.0f; // CSV LeapDamage 留空時：接觸傷害 × 此

    // ── 魄力（2026-09-18 加，作者回報「感受不到跳躍踐踏的魄力」）──
    // ⭐ **落地定格**：砸到地的那一瞬間把時間壓到幾乎停住再彈回來。
    //    同樣的動畫，加了這 0.06 秒就會從「碰到地面」變成「砸到地面」——動作遊戲重量感的核心。
    //    實作與那些坑（不能用協程、還原時不能覆蓋面板暫停）見 HitStop。
    const float HitStopSeconds = 0.06f;

    // ⭐ **下墜殘影**：下降段每隔一小段留一個半透明剪影，快速淡出 ⇒ 讀得出「掉得非常快」。
    //    作者本來問的是速度線；殘影傳達同一件事但不會與陰暗寫實的美術打架（理由見 Afterimage 的類別註解）。
    //    只在**下降段**留（上升是「躍起」、下降才是「砸落」，全程都留會變成一條沒有重點的長尾巴）。
    const float AfterimageFromT    = 0.42f;   // 騰空進度 ≥ 此才開始留（頂點在 0.5，所以是從接近頂點就開始）
    const float AfterimageInterval = 0.035f;  // 兩個殘影之間隔多久（秒）
    const float AfterimageLife     = 0.22f;   // 每個殘影活多久
    const float AfterimageAlpha    = 0.40f;   // 起始透明度

    // ⭐ **落地揚塵**：VfxTable 41「踐踏揚塵」（Super Pixel Effects Pack 1 的 fx1_impact_dust，brown，13 幀）。
    //    這個素材很適合踐踏：畫布 104×24 的**極寬扁**橫幅、塵土從中央往左右推開、
    //    而且**前 8 幀底邊都貼齊畫布底** ⇒ 它是「沿著地面擴散」而不是「往上噴的雲團」，正是落地該有的樣子。
    //    `Loop=0` ＝播一輪自毀（作者要「播一次就好」）；`SortingOrder` 留空 ⇒ 用 VfxManager 全域 22000
    //    ⇒ **蓋在角色前面**，塵土會遮住腳，衝擊感比畫在腳下更強。
    //    0 ＝關掉。
    const int LandDustVfxId = 41;
    const float LandDustLiftY = 0.38f;   // 把素材往上抬，讓它坐在地面上（理由見落地那一段的註解）

    // ⭐ 落地後**停在最後一幀不動**多久才交棒給追擊（作者 2026-09-18：「沒有那種壓迫感」）。
    // 這一秒是整套動作的重量所在：砸下來 → 定格 → 地裂慢慢竄開 → 才緩緩起身追人。
    // one-shot 天生會停在結束幀，所以「定格」不必另外做，只要延後交棒即可。
    // 這同時也是**玩家的輸出窗口**（落地那一刻就把擊退與內插還給系統了，見 TickLeap）。
    const float LandHoldSeconds = 1.0f;

    // 整段跳躍的保險上限（秒）。動畫或幀率出意外時不至於卡在空中。
    const float SafetySeconds = 4.0f;

    /// <summary>除錯：印出起跳距離、落點、實際落地時間。抓手感時打開。</summary>
    public static bool DebugLog = false;

    enum Phase { Approach, Leap, Chase }

    Phase _phase = Phase.Approach;
    // 跳完之後的近戰追擊：用 MeleeChaseBrain 而不是 ChaseBrain——後者會讓怪**舉著劍追人**
    // （攻擊動畫與移動互不相干，見 MeleeChaseBrain 的類別註解）。
    readonly MeleeChaseBrain _chase = new MeleeChaseBrain();

    // 跳躍狀態
    float _leapStart;
    Vector2 _from, _to;
    float _tTakeoff, _tLand, _tEnd;
    bool _impacted;
    float _radius, _damage;
    float _hopHeight;          // 這一次跳躍的抬升上限（＝身高 × HopHeightInBodies，起跳時算好）
    float _nextAfterimageAt;   // 下一個殘影什麼時候留
    VfxManager _vfx;           // 落地揚塵用（快取；同 BossSpike 的取法）
    Rigidbody2D _rb;
    RigidbodyInterpolation2D _savedInterp;
    MonsterAnimator _anim;

    public void Think(in MonsterContext ctx)
    {
        MonsterActuator act = ctx.Actuator;
        if (act == null) return;

        // 攻擊姿勢全程由 Brain 控制（跳躍是 one-shot、之後的揮劍由 MeleeChaseBrain 的 one-shot）。
        // 不設的話，光是「走近的路上距離 ≤ AttackRange」就會讓 HandleVisuals 自動舉劍（見 BrainControlsAttackPose）。
        // （含關掉「碰到就痛」——這隻怪的傷害一律走踐踏與揮擊的命中判定，見 MeleeChaseBrain）
        _chase.EnsureConfigured(ctx.Self);

        // ⚠ 收尾檢查放在最前面、不放在 Leap 分支裡：怪被打時的擊退窗口會**整段跳過 Think()**
        //   （見 readme/PROBLEMS.md F19）。跳到一半被打斷時，窗口一結束要第一時間把落地結算掉，
        //   否則牠會停在半空、裂痕與傷害也不會出現。
        //   位置是「由經過時間算出的絕對插值」而不是逐幀累加，所以被跳過幾幀也不會偏——回來就自動對位。
        if (_phase == Phase.Leap && Time.time - _leapStart >= SafetySeconds + LandHoldSeconds)
        {
            FinishLeap(ctx, act);
        }

        Transform target = Nearest(act.transform.position, ctx.Enemy, ctx.Player);

        switch (_phase)
        {
            case Phase.Approach:
                if (target == null) { act.Stop(); break; }
                if (Vector2.Distance(act.transform.position, target.position) <= LeapRangeMax)
                    BeginLeap(ctx, act, target);
                else
                    act.MoveTowards(target.position);
                break;

            case Phase.Leap:
                TickLeap(ctx, act);
                break;

            case Phase.Chase:
                _chase.Think(in ctx);
                break;
        }
    }

    // ── 起跳 ────────────────────────────────────────────────────────────────
    void BeginLeap(in MonsterContext ctx, MonsterActuator act, Transform target)
    {
        _anim = ctx.Self != null ? ctx.Self.Anim : null;

        // 沒有 jump 圖（或是舊 prefab 怪）→ **不硬演**，直接變成一般追擊。
        // PlayOneShot 找不到幀時會回 false，這比 SetState 的「自動退回走路」好：
        // 退回走路只會變成「滑過去然後莫名其妙炸一下」，不如乾脆不跳。
        if (_anim == null || !_anim.PlayOneShot(MonsterAnimator.State.Jump, 1, TotalFrames, JumpFpsMul))
        {
            _phase = Phase.Chase;
            return;
        }

        Vector2 pos = act.transform.position;
        _from = pos;

        // ── 落點：鎖定起跳當下目標的位置 ──
        // ⭐⭐ **鐵則（作者 2026-09-18 拍板，同 §8 射手的「動作播了就一定要射」）：蹲下去了就一定要跳出去。**
        //   這裡**不會因為「太遠」或「被牆擋」而放棄**，只會退而求其次選一個
        //   「射程內離目標最近、而且跳得到」的點。理由同射手：玩家看到怪蹲下去蓄力、結果牠站在原地不動，
        //   讀起來就是 bug，而不是「牠判斷了一下決定不跳」。
        Vector2 want = target.position;
        Vector2 d = want - pos;
        float tdist = d.magnitude;
        Vector2 tdir = tdist < 1e-4f ? Vector2.right : d / tdist;
        if (tdist > LeapRangeMax)      want = pos + tdir * LeapRangeMax;   // 玩家跑遠了 → 跳到射程邊緣＝射程內最靠近他的點
        else if (tdist < MinLeapDist)  want = pos + tdir * MinLeapDist;    // 貼臉 → 往前踏一步，不要原地跳
        _to = ResolveLanding(pos, want, target.position);

        // 跳躍高度＝這隻怪的身高 × 倍率（見 HopHeightInBodies）。localScale 一起乘，體型倍率才吃得到。
        float bodyH = (ctx.Self != null && ctx.Self.CharacterWorldHeight > 0.01f)
                      ? ctx.Self.CharacterWorldHeight : FallbackBodyHeight;
        bodyH *= Mathf.Abs(act.transform.localScale.y);
        _hopHeight = bodyH * HopHeightInBodies;

        // 幀號換算成秒：與動畫同一個幀率，所以改 CSV 的 AnimFPS 兩邊會一起變
        float fps = Mathf.Max(1f, (ctx.Self != null ? ctx.Self.AnimFPS : 8f) * JumpFpsMul);
        _tTakeoff = (TakeoffFrame - 1) / fps;
        _tLand    = (LandFrame - 1) / fps;
        _tEnd     = EndFrame / fps;

        // 踐踏數值：CSV 留空就給退路（見 MonsterData 的欄位註解）
        var self = ctx.Self;
        _radius = (self != null && self.LeapRadius > 0.01f) ? self.LeapRadius : DefaultRadius;
        _damage = (self != null && self.LeapDamage > 0.01f)
                  ? self.LeapDamage
                  : ((self != null ? self.ContactDamage : 10f) * DefaultDamageMul);

        // 騰空期間位置由本 Brain 直接寫 transform，所以先讓 actuator 停手。
        act.Stop();
        // ⚠ 內插要暫時關掉：開著 Interpolate 時 Rigidbody2D 會用「上一個物理步的位置」往回補畫面，
        //   而我們每幀直接改 transform ⇒ 兩邊互相拉扯，騰空看起來會抖。落地後還原。
        _rb = act.GetComponent<Rigidbody2D>();
        if (_rb != null) { _savedInterp = _rb.interpolation; _rb.interpolation = RigidbodyInterpolation2D.None; }

        // ⭐ 跳躍期間不准被擊退打斷決策（見 MonsterController.SuppressKnockbackInterrupt）。
        //   不開這個的話，玩家連射時擊退窗口首尾相連 ⇒ Think() 整段被跳過 ⇒ 騰空那 0.46 秒完全沒有位移，
        //   畫面上就是「蹲下、動畫演完、人還在原地」——作者 2026-09-18 實機回報的「常常準備跳但沒跳出去」，
        //   主因就是這條（另一半是落點被夾回原地，見 ResolveLanding）。
        if (ctx.Self != null) ctx.Self.SuppressKnockbackInterrupt = true;

        _impacted = false;
        _leapStart = Time.time;
        _phase = Phase.Leap;

        if (DebugLog)
            Debug.Log($"[LeapSlam] 起跳：距離={Vector2.Distance(_from, _to):F2}　落點={_to}　" +
                      $"起跳@{_tTakeoff:F2}s　落地@{_tLand:F2}s　結束@{_tEnd:F2}s　半徑={_radius:F2}　傷害={_damage:F0}");
    }

    // ── 跳躍中 ──────────────────────────────────────────────────────────────
    void TickLeap(in MonsterContext ctx, MonsterActuator act)
    {
        float e = Time.time - _leapStart;
        var self = ctx.Self;

        // 全程不讓 actuator 出力：這一段的位置完全由下面的插值決定
        if (_rb != null) _rb.velocity = Vector2.zero;

        if (e < _tTakeoff)
        {
            // 蓄力：站著不動＝玩家的預告窗口
            return;
        }

        if (e < _tLand)
        {
            float t = Mathf.Clamp01((e - _tTakeoff) / Mathf.Max(0.0001f, _tLand - _tTakeoff));
            Vector2 ground = Vector2.Lerp(_from, _to, HorizCurve(t));   // 水平：上升段走完大半，下降段幾乎不動
            float hop = _hopHeight * HopCurve(t);
            act.transform.position = new Vector3(ground.x, ground.y + hop, act.transform.position.z);
            if (self != null) self.AirborneVisualHeight = hop;   // 影子留地面、Y 排序不誤判（見 IAirborneVisual）

            // 下墜殘影（只在下降段留，見 AfterimageFromT）
            if (t >= AfterimageFromT && Time.time >= _nextAfterimageAt)
            {
                _nextAfterimageAt = Time.time + AfterimageInterval;
                Afterimage.Spawn(act.GetComponent<SpriteRenderer>(), AfterimageLife, AfterimageAlpha);
            }
            return;
        }

        // 觸地：位置歸位、裂痕＋傷害各放一次
        if (!_impacted)
        {
            _impacted = true;
            act.transform.position = new Vector3(_to.x, _to.y, act.transform.position.z);

            // ⭐ 砸到地的那一瞬間定格（見 HitStopSeconds）。放在這裡而不是動畫某一幀：
            //    要的就是「接觸地面」的那一刻，而那一刻就是這一格。
            HitStop.Play(HitStopSeconds);

            // ⭐ 一落地就把「不可中斷」與內插還回去，不要等停留那一秒結束——
            //    那一秒是玩家的輸出窗口，牠該會被打退、該有受擊回饋。
            //    （位置也不再被壓制：下面不再寫 transform，rb.velocity 也不再每幀歸零。）
            if (self != null)
            {
                self.AirborneVisualHeight = 0f;
                self.SuppressKnockbackInterrupt = false;
            }
            if (_rb != null) { _rb.interpolation = _savedInterp; _rb = null; }

            // 落地揚塵（播一次，見 LandDustVfxId）。與裂痕、傷害圈同樣對準腳底。
            // ⚠ 素材錨點在**畫布中心**，而它的內容是貼著畫布底的橫幅 ⇒ 直接放在腳底的話塵土會有一半沉到地下。
            //   往上補半個畫布高（24px ÷ 2 ÷ PPU 100 × Scale 3.2 ≈ 0.38 世界單位）讓它坐在地面上。
            //   （這個數字是「素材怎麼畫」決定的，不是手感，所以寫在這裡而不是開成 CSV 欄。）

            // ⚠⚠ **視覺對準腳底，傷害對準 transform——這兩個刻意不一樣**（2026-09-18 二修，見 PROBLEMS F27）：
            //   ‧ **裂痕／揚塵是視覺** ⇒ 畫在畫面上「腳踩的位置」＝ FeetWorldPos。
            //     route B 怪物的 pivot 在畫布中心，用 transform 會讓它出現在半身高（作者回報「龜裂在胸口」）。
            //   ‧ **傷害圈是判定** ⇒ 必須與「玩家的碰撞圓」同一個基準。玩家的 CircleCollider2D **offset 是 0**，
            //     圓心就在牠的 transform ⇒ 傷害圈也要以**怪的 transform** 為心。
            //   第一版兩個都用腳底，於是傷害圈比玩家的圓低了半個身高，**而那個落差隨 Scale 線性長大**
            //   ⇒ Scale 一調大就打不到（作者調到 1.5 後回報「砍不到的問題又出現」）。
            //   ⚠ 所以畫面上「裂痕的圈」與「打得到的圈」會差一個半身高的深度——**那不是 bug，
            //     是 pivot 不一致的後果**（玩家 pivot 在腳底、怪在畫布中心），玩家的圓半徑 0.5 會吸收一部分。
            Vector2 foot = (self != null) ? self.FeetWorldPos : _to;   // 視覺用
            Vector2 hitCenter = _to;                                   // 判定用（＝怪的 transform，與玩家圓同基準）
            var sr = act.GetComponent<SpriteRenderer>();
            GroundCrackFx.Spawn(foot, _radius * CrackRadiusMul, sr);

            if (LandDustVfxId > 0)
            {
                if (_vfx == null) _vfx = Object.FindObjectOfType<VfxManager>();
                if (_vfx != null) _vfx.Spawn(LandDustVfxId, foot + new Vector2(0f, LandDustLiftY));
            }
            ImpactDamageArea.Spawn(hitCenter, _radius, _damage, self != null ? self.Faction : MonsterFaction.Enemy);

            if (DebugLog) Debug.Log($"[LeapSlam] 落地 @{e:F2}s　判定中心={hitCenter}　視覺(腳底)={foot}" +
                                    $"（深度差 {(_to.y - foot.y):F2}＝pivot 不一致，見 PROBLEMS F27）");
        }

        // 落地緩衝播完 **再定格 LandHoldSeconds** → 才交棒給追擊。
        // 動畫早就停在最後一幀（one-shot 的天生行為），所以這一秒畫面上是「維持落地姿勢不動」。
        bool animDone = _anim == null || _anim.OneShotFinished || e >= _tEnd;
        if (animDone && e >= _tLand + LandHoldSeconds) FinishLeap(ctx, act);
    }

    /// <summary>
    /// 收掉跳躍狀態（動畫、內插、騰空高度），並確保「裂痕與傷害至少放過一次」——
    /// 被擊退打斷而提前走到這裡時，玩家還是應該吃到那一記踐踏。
    /// </summary>
    void FinishLeap(in MonsterContext ctx, MonsterActuator act)
    {
        var self = ctx.Self;

        if (!_impacted)
        {
            _impacted = true;
            Vector2 here = (act != null) ? (Vector2)act.transform.position : _to;
            Vector2 atVisual = (self != null) ? self.FeetWorldPos : here;   // 視覺＝腳底、判定＝transform（見落地那段）
            var sr = act != null ? act.GetComponent<SpriteRenderer>() : null;
            GroundCrackFx.Spawn(atVisual, _radius * CrackRadiusMul, sr);
            ImpactDamageArea.Spawn(here, _radius, _damage, self != null ? self.Faction : MonsterFaction.Enemy);
        }

        if (self != null)
        {
            self.AirborneVisualHeight = 0f;
            self.SuppressKnockbackInterrupt = false;   // 交還擊退的中斷權（一定要還，否則牠一輩子不會被打斷）
        }
        if (_anim != null) _anim.CancelOneShot();
        if (_rb != null) { _rb.interpolation = _savedInterp; _rb = null; }
        _phase = Phase.Chase;
    }

    /// <summary>
    /// 騰空的高度曲線（輸入 0~1、輸出 0~1）：**上升慢到頂點、下降加速砸下來**。
    ///
    /// <para>第一版用對稱拋物線 <c>4t(1−t)</c>，上升與下降一樣快 ⇒ 是「飄過去」不是「跳過去」。
    /// 改成分段：頂點前走 sin 的前四分之一（越接近頂點越慢＝滯空），頂點後走 <c>1−v²</c>（越掉越快＝重量感）。
    /// 頂點的位置 <see cref="PeakFrame"/> 對齊素材裡最高的那一幀，不然會出現「圖已經在下墜、程式還在往上抬」。</para>
    /// </summary>
    /// <summary>頂點在整段騰空裡的位置（0~1），由素材最高的那一幀決定。</summary>
    static float PeakT => Mathf.Clamp01((float)(PeakFrame - TakeoffFrame) / Mathf.Max(1, LandFrame - TakeoffFrame));

    /// <summary>
    /// 水平位移曲線：**到頂點時就走完 <see cref="HorizAtPeak"/>（85%），剩下的 15% 留給整個下降段**
    /// ⇒ 落下那一半幾乎是垂直的，讀起來是「躍到高處、垂直砸下來」而不是「斜斜飛過去」。
    /// <para>頂點處水平速度會有一個明顯的落差（上升段比下降段快 5.7 倍），那正是「滯空一下再砸下來」的感覺，
    /// 不是 bug。</para>
    /// </summary>
    static float HorizCurve(float t)
    {
        float peak = PeakT;
        if (t <= peak) return HorizAtPeak * (t / Mathf.Max(0.0001f, peak));
        return HorizAtPeak + (1f - HorizAtPeak) * ((t - peak) / Mathf.Max(0.0001f, 1f - peak));
    }

    static float HopCurve(float t)
    {
        float peak = PeakT;
        if (t <= peak)
        {
            float u = t / Mathf.Max(0.0001f, peak);
            return Mathf.Sin(u * Mathf.PI * 0.5f);        // 0 → 1，接近頂點趨緩（滯空）
        }
        float v = (t - peak) / Mathf.Max(0.0001f, 1f - peak);
        return 1f - v * v;                                 // 1 → 0，越掉越快（墜落感）
    }

    /// <summary>
    /// 決定真正的落點。**這支永遠會回傳一個跳得出去的點**（除非整個人被封在牆縫裡）——
    /// 見 <see cref="BeginLeap"/> 的鐵則。
    ///
    /// <para>先試想要的落點；被擋住就以「起跳點→想要的落點」為中線往兩側散開試
    /// （<see cref="LandingAngles"/>），每個角度由遠而近找第一個跳得到的距離，
    /// 最後在所有候選裡挑**離目標最近**的那個。</para>
    ///
    /// <para>可達性走 <c>MapNavGrid.HasLineOfSight</c>——和 A* 同一份障礙圖（含家具膨脹），
    /// 與 actuator 的判定一致。**牆的另一側不該因為會跳就跳得過去**，那會破壞地圖的動線設計。</para>
    ///
    /// <para>⚠ 第一版只沿直線往回退，退到 0.2 都不通就回傳起跳點 ⇒ **原地跳**。
    /// 玩家站的格子對尋徑格來說常常是不可走的（家具膨脹了一圈），所以這個「退回原地」比想像中常發生，
    /// 是作者回報「準備跳但沒跳出去」的第二個來源。</para>
    /// </summary>
    static Vector2 ResolveLanding(Vector2 from, Vector2 want, Vector2 goal)
    {
        var nav = MapNavGrid.Instance;
        if (nav == null || !nav.Ready) return want;          // 沒有尋徑格（單場景測試）→ 直接用
        if (nav.HasLineOfSight(from, want)) return want;

        Vector2 toWant = want - from;
        float maxDist = toWant.magnitude;
        if (maxDist < 0.01f) return want;
        Vector2 baseDir = toWant / maxDist;

        Vector2 best = want;
        float bestSq = float.MaxValue;
        bool found = false;

        for (int i = 0; i < LandingAngles.Length; i++)
        {
            Vector2 dir = Rotate(baseDir, LandingAngles[i]);
            for (float f = 1f; f >= 0.35f; f -= 0.15f)       // 由遠而近：同一個角度取跳得到的最遠處就好
            {
                float len = maxDist * f;
                if (len < MinLeapDist) break;
                Vector2 p = from + dir * len;
                if (!nav.HasLineOfSight(from, p)) continue;
                float sq = ((Vector2)goal - p).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = p; found = true; }
                break;
            }
        }
        return found ? best : want;   // 一個候選都沒有＝被封在牆縫裡；仍然回想要的點（寧可跳出去，也不要蹲著不動）
    }

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    static Transform Nearest(Vector2 pos, Transform a, Transform b)
    {
        if (a == null) return b;
        if (b == null) return a;
        float da = ((Vector2)a.position - pos).sqrMagnitude;
        float db = ((Vector2)b.position - pos).sqrMagnitude;
        return da <= db ? a : b;
    }
}
