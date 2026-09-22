using UnityEngine;

/// <summary>
/// 自爆型（炸彈怪／爆裂蟲／人肉炸彈…）：**走到目標身邊 → 站定點引信 → 炸掉自己與周圍。**
///
/// <para>和 <see cref="ChaseBrain"/> 的差別在最後一步：衝撞型是貼上去「磨」（每 `AttackInterval` 扣一次接觸傷害），
/// 這支是貼上去「**一次結算、同歸於盡**」。所以本 Brain 會**關掉接觸傷害**——
/// 自爆怪擦過你身邊不該扣血，牠的全部威脅就是那一下爆炸。</para>
///
/// 三段（`Phase`）：
///   Chase   走向目標；碰撞框邊緣距離進 <see cref="FuseSlack"/> 就點引信
///   Fuse    **站定**閃爍（越接近爆炸閃越快）＝玩家的逃命窗口
///   Done    已引爆，等這一幀結束被銷毀
///
/// <para>⭐ <b>鐵則（照抄 <see cref="ArcherBrain"/> §8 的「出手就一定要射」）：進了引信就一定會爆。</b>
/// `Fuse` 階段**沒有任何中途取消**。逐項確認過：
/// <b>玩家跑出範圍</b>（不重驗距離）、<b>玩家不見了</b>（引爆完全不讀 target，只用自己的位置）、
/// <b>被擊退</b>（擊退窗口會整段跳過 <c>Think()</c>，但引信走 <c>Time.time</c>、時間照跑，
/// 窗口一結束的第一幀就引爆）、<b>被打</b>（受擊只重置受擊自己的視覺，
/// <c>HitReactionHandler.ResetVisuals</c> 刻意不清充能發光）——全都照爆。
/// 唯一的例外是**被打死**，那是設計上留給玩家的拆彈窗口（見下）。
/// 一隻「嗶嗶叫了兩聲又把引信收回去」的炸彈怪，在畫面上看起來就是壞掉的怪。
/// 玩家的應對是**閃開爆炸範圍**或**在引信燒完前把牠打死**，不是讓牠自己放棄。</para>
///
/// <para><b>被玩家打死不會爆</b>（作者 2026-09-22 拍板）：爆炸只發生在引信燒完的那一刻，
/// 而怪一死 <c>MonsterController.Update</c> 就提前 return、Brain 不再被呼叫 ⇒ 這是天生行為，不必寫特例。
/// 「搶在引信燒完前拆掉它」因此成為一個真正的操作空間。</para>
///
/// <para><b>零新傷害程式</b>：爆炸走 <see cref="ImpactDamageArea"/>（＝跳躍踐踏 §9.4／揮擊命中幀 §10.4 同一支），
/// 誰能傷誰查 <c>FactionRelations</c>（所以自爆也會炸到敵對陣營的怪）、傷害統一進 <c>CombatSystem</c>。
/// 自毀走 <c>TakeDamage</c> 而不是 <c>Destroy</c>，這樣掉落、死亡旗標、RunProgress『已清』全部照既有流程跑。</para>
///
/// `BrainType` 填 `SuicideBomb`。數值走 CSV 表尾三欄 `BombDamage`／`BombRadius`／`BombFuse`（留空有退路值）。
/// 見 readme/BOSS_MODULE.md §11。
/// </summary>
public class SuicideBombBrain : IMonsterBrain
{
    // ── 點引信的距離 ──
    // ⚠⚠ **不能用 `Physics2D.Distance` 的框對框邊緣距離**（2026-09-22 改）：身體框貼合整個可見身體
    //    ⇒ 它的**高度就是身高**，而俯視角的 Y 軸同時是地面深度 ⇒ 玩家從上／下方接近時，
    //    離怪還有一個半身位就已經算「貼到框」了 ⇒ 畫面上是「離很遠就停下來自爆」（作者回報）。
    //    改成**只取水平半徑的圓形距離**：各方向門檻一致，而且不受這隻怪畫得多高影響。
    // ⚠ 半徑取自碰撞框（`bounds.extents.x`，已含 `Scale`）⇒ **體型變大時門檻自動跟著長**，
    //    不會重蹈 F29 那個「寫死世界單位」的覆轍。
    // 0.05 ＝ 幾乎要真的碰到才點火。放大這個值＝更早引爆（更難閃），縮小＝要貼更緊。
    const float FuseSlack = 0.05f;

    // ── 退路值（CSV 留空時用）──
    const float FuseSecondsFallback = 0.6f;   // 引信秒數
    // ⚠ 半徑是「**體型 1 時**的世界單位」，實際會 ×`Scale`（見 MonsterController.ScaledRadius）。
    //    實際殺傷距離 ＝ 縮放後半徑 ＋ 目標碰撞框半徑（玩家約 0.5）。
    const float RadiusFallback = 1.8f;
    const float DamageMulFallback = 3f;       // 傷害＝ContactDamage × 此值

    // 爆炸特效（VfxTable ID）。0 ＝不放特效。
    // ⚠ 特效大小會**依實際半徑等比縮放**（extraScale ＝ 實際半徑 ÷ RadiusFallback）。
    //    實際半徑已經含了體型（ScaledRadius）⇒ **把怪放大時火球也跟著大**，
    //    不會出現「炸得到卻看不出來」或反過來「看起來炸很大卻沒傷害」。
    const int BombVfxId = 42;

    // ── 引信的視覺：**整隻怪逐漸燒紅 ＋ 脈動越來越急**（這是玩家唯一的預告，不要拿掉）──
    // 走 SpriteFlash shader 的 _Charge* 參數（加法發光），**不是** SpriteRenderer.color：
    //   ‧ 這些怪本體幾乎全黑，乘法 tint（黑 × 紅 = 黑）根本看不出來，只有加亮才會「燒起來」；
    //   ‧ 而且 `SpriteRenderer.color` 會被受擊無敵閃爍改掉再還原 ⇒ 引信顏色會被洗掉（見 HitReactionHandler.SetCharge）。
    static readonly Color ChargeColor = new Color(1f, 0.22f, 0.10f);   // 熾紅（加法，會疊到本體上）

    // 底光：隨引信進度線性爬升 0 → ChargeBaseMax（「越燒越紅」）
    const float ChargeBaseMax = 0.55f;
    // 脈動：疊在底光上的一層方波（「滴、滴、滴」的視覺節拍）
    const float ChargePulseAmp = 0.45f;
    const float PulseSlowInterval = 0.22f;    // 引信剛點著時的節拍週期
    const float PulseFastInterval = 0.05f;    // 快爆炸時的節拍週期

    // 自爆怪不該「擦到就痛」：牠的全部威脅是那一下爆炸。
    // 改成 false 就變成「碰到會磨血、而且還會爆」。
    const bool DisableContactDamage = true;

    // 走到目標多近就不必再前進（單純避免抖動；真正的引信門檻是 FuseSlack）
    const float StopDistance = 0.2f;

    /// <summary>除錯：印引信與爆炸的距離、傷害、半徑。</summary>
    public static bool DebugLog = false;

    enum Phase { Chase, Fuse, Done }

    Phase _phase = Phase.Chase;
    float _fuseEnd;              // 引信燒完的時間
    float _fuseLen;              // 這一次的引信總長（算閃爍節奏用）
    float _nextBlinkAt;
    bool _configured;
    Collider2D _selfCol, _targetCol;
    Transform _targetColOwner;
    HitReactionHandler _hit;     // 充能發光的唯一寫入點（見 HitReactionHandler.SetCharge）
    bool _pulseOn;
    VfxManager _vfx;             // 爆炸特效用（快取；同 LeapSlamBrain 的取法）

    /// <summary>套用本 Brain 需要的設定（關接觸傷害、關一般死亡特效）。呼叫幾次都只生效一次。</summary>
    void EnsureConfigured(MonsterController self)
    {
        if (_configured || self == null) return;
        _configured = true;

        // 傷害只來自爆炸（見 DisableContactDamage）
        if (DisableContactDamage)
        {
            var contact = self.GetComponent<EnemyContactDamage>();
            if (contact != null) contact.enabled = false;
        }

        // 自爆時的視覺是那顆火球，不要再疊一次一般怪的死亡煙霧（DeathVfxId 預設 7）。
        // ⚠ 這也影響「被玩家打死」的情況——自爆怪被拆掉時同樣不放骷髏煙霧，
        //   那是刻意的：一隻炸彈被拆掉就是安靜地倒下，有煙霧反而像爆了。
        self.DeathVfxId = 0;

        _hit = self.GetComponent<HitReactionHandler>();
    }

    public void Think(in MonsterContext ctx)
    {
        MonsterActuator act = ctx.Actuator;
        if (act == null) return;

        EnsureConfigured(ctx.Self);

        Vector2 pos = act.transform.position;
        Transform target = Nearest(pos, ctx.Enemy, ctx.Player);

        // 貼身判定要的兩個 collider（快取；目標換人才重取）
        if (_selfCol == null) _selfCol = act.GetComponent<Collider2D>();
        if (target != null && _targetColOwner != target)
        {
            _targetColOwner = target;
            _targetCol = target.GetComponent<Collider2D>();
        }

        switch (_phase)
        {
            case Phase.Chase:
                if (target == null) { act.Stop(); break; }

                if (Reached(FuseSlack)) { BeginFuse(ctx, act); break; }

                if (Vector2.Distance(pos, target.position) > StopDistance) act.MoveTowards(target.position);
                else act.Stop();
                break;

            // ── 引信：站定閃爍。**沒有任何中途取消**（見檔頭鐵則）──
            case Phase.Fuse:
                act.Stop();
                UpdateFuseVisual();
                if (Time.time >= _fuseEnd) Detonate(ctx, act);
                break;

            case Phase.Done:
                act.Stop();
                break;
        }
    }

    void BeginFuse(in MonsterContext ctx, MonsterActuator act)
    {
        var self = ctx.Self;
        _fuseLen = (self != null && self.BombFuse > 0.01f) ? self.BombFuse : FuseSecondsFallback;
        _fuseEnd = Time.time + _fuseLen;
        _nextBlinkAt = 0f;
        _phase = Phase.Fuse;
        act.Stop();

        if (DebugLog) Debug.Log($"[SuicideBomb] 點引信（邊緣距離={EdgeDistance():F2} ≤ {FuseSlack}）　{_fuseLen:F2}s 後引爆");
    }

    /// <summary>
    /// 引爆：開一次傷害圈 ＋ 放爆炸特效 ＋ 自毀。
    ///
    /// <para>⚠ <b>傷害圈的中心用怪的 <c>transform</c>，視覺用腳底</b>——兩者刻意不同，理由見
    /// readme/PROBLEMS.md **F27**：玩家的 <c>CircleCollider2D</c> offset 是 0（圓心＝transform），
    /// 而 route B 怪物的 pivot 在畫布中心、腳底離 transform 有半個身高，**那個落差隨 Scale 線性長大**。
    /// 判定圈若以腳底為心，怪一放大就炸不到人。</para>
    /// </summary>
    void Detonate(in MonsterContext ctx, MonsterActuator act)
    {
        _phase = Phase.Done;
        ClearFuseVisual();

        var self = ctx.Self;
        float baseRadius = (self != null && self.BombRadius > 0.01f) ? self.BombRadius : RadiusFallback;

        // ⭐ **半徑一定要隨體型縮放**（見 MonsterController.ScaledRadius 與 PROBLEMS **F27**／**F29**）：
        //   引信是用「碰撞框的邊緣距離」判定的 ⇒ 觸發距離**會**隨體型長大；
        //   半徑若是寫死的世界單位就**不會** ⇒ 怪一放大就「炸了卻炸不到人」。
        float radius = (self != null) ? self.ScaledRadius(baseRadius) : baseRadius;

        // ⭐⭐ **防呆下限：引信點著了，就一定要炸得到。**
        //   引信觸發時的中心距離 ＝ 自身水平半徑 ＋ 目標水平半徑 ＋ FuseSlack（見 Reached）；
        //   傷害圈打得到的中心距離 ＝ radius ＋ 目標碰撞半徑。兩式相減 ⇒ **radius ≥ 自身半徑 ＋ FuseSlack**。
        //   （目標的半徑在兩邊都有、自動抵銷，所以這條**不必知道玩家多大**。）
        //   沒有這條的話，體型小到某個程度同一個坑會從另一端再出現一次——
        //   因為引信距離裡有一截固定量（FuseSlack）不隨體型縮。
        //   這也是「進了引信就一定會爆」那條鐵則的必然延伸：**爆了卻炸不到，等於沒爆。**
        radius = Mathf.Max(radius, SelfRadius() + FuseSlack);

        float damage = (self != null && self.BombDamage > 0.01f)
                       ? self.BombDamage
                       : (self != null ? self.ContactDamage * DamageMulFallback : 30f);

        Vector2 hitCenter = act.transform.position;                          // 判定：與玩家的碰撞圓同基準
        Vector2 visualPos = (self != null) ? self.BodyCenterWorldPos : hitCenter;  // 視覺：可見身體中心

        if (BombVfxId > 0)
        {
            if (_vfx == null) _vfx = Object.FindObjectOfType<VfxManager>();
            if (_vfx != null) _vfx.Spawn(BombVfxId, visualPos, 0f, radius / RadiusFallback);
        }

        ImpactDamageArea.Spawn(hitCenter, radius, damage, self != null ? self.Faction : MonsterFaction.Enemy);

        if (DebugLog)
            Debug.Log($"[SuicideBomb] 引爆　傷害={damage:F0}　半徑={radius:F2}" +
                      $"（實際殺傷還要加上目標碰撞框半徑）　判定中心={hitCenter}");

        // 自毀走既有死亡流程（死亡特效／掉落／死亡旗標／RunProgress『已清』／召喚回收都照跑），
        // 不要直接 Destroy——那會把上面那些全部跳過。
        // ⚠⚠ 用 Kill() 而**不是** TakeDamage(超大數字)：後者會先問 HitReactionHandler，
        //    怪正在無敵幀內就一滴血都不扣 ⇒ 「爆炸放了、怪沒死」，而這裡已經進了 Done
        //    ⇒ 牠會站在原地不動、不再追人也不再爆。自爆成不成功不該取決於 InvincibleTimeMs（同 PROBLEMS F19）。
        if (self != null && !self.IsDead) self.Kill();
    }

    /// <summary>
    /// 引信的視覺：**底光隨進度爬升（越燒越紅）＋ 一層越來越急的脈動**。玩家唯一的預告。
    /// 走 <see cref="HitReactionHandler.SetCharge"/>（shader 加法發光），理由見 <see cref="ChargeColor"/> 的註解。
    /// </summary>
    void UpdateFuseVisual()
    {
        if (_hit == null) return;

        // 進度 0 → 1（剛點著 → 要爆了）
        float t = (_fuseLen > 0.01f) ? Mathf.Clamp01(1f - (_fuseEnd - Time.time) / _fuseLen) : 1f;

        // 節拍：週期從慢插到快。用方波（不是 sin）才有「滴、滴、滴」的斷點感。
        float interval = Mathf.Lerp(PulseSlowInterval, PulseFastInterval, t);
        if (Time.time >= _nextBlinkAt)
        {
            _pulseOn = !_pulseOn;
            _nextBlinkAt = Time.time + interval;
        }

        float amount = ChargeBaseMax * t + (_pulseOn ? ChargePulseAmp : 0f);
        _hit.SetCharge(amount, ChargeColor);
    }

    /// <summary>引爆／收尾時把發光關掉（怪馬上就會被銷毀，這只是保險）。</summary>
    void ClearFuseVisual()
    {
        if (_hit != null) _hit.ClearCharge();
    }

    /// <summary>
    /// 走到目標身邊了沒：**兩者的水平半徑相加 ＋ slack**，拿中心距離去比。
    ///
    /// <para>⚠ 刻意**不用** <c>Physics2D.Distance</c> 的框對框邊緣距離（那是接觸傷害／近戰貼身在用的）：
    /// 身體框的高度＝整個身高，在俯視角裡等於宣告「這隻怪佔了一個身高的地面深度」
    /// ⇒ 玩家從上下方接近時會**提早一個半身位**觸發。圓形判定讓各方向一致。</para>
    ///
    /// <para>半徑取自碰撞框（已含體型），所以放大縮小怪都不必回來改常數（見 PROBLEMS **F29** 的通則）。</para>
    /// </summary>
    bool Reached(float slack) => EdgeDistance() <= slack;

    /// <summary>中心距離 −（兩者水平半徑和）。負值＝已經重疊。取不到 collider 回很大的數（＝還沒到）。</summary>
    float EdgeDistance()
    {
        if (_selfCol == null || _targetCol == null) return float.MaxValue;
        float r = _selfCol.bounds.extents.x + _targetCol.bounds.extents.x;
        return Vector2.Distance(_selfCol.bounds.center, _targetCol.bounds.center) - r;
    }

    /// <summary>自身的水平半徑（世界單位，已含體型）。給引爆的防呆下限用。</summary>
    float SelfRadius() => _selfCol != null ? _selfCol.bounds.extents.x : 0.5f;

    static Transform Nearest(Vector2 pos, Transform a, Transform b)
    {
        if (a == null) return b;
        if (b == null) return a;
        float da = ((Vector2)a.position - pos).sqrMagnitude;
        float db = ((Vector2)b.position - pos).sqrMagnitude;
        return da <= db ? a : b;
    }
}
