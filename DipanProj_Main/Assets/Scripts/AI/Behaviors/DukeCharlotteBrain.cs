using UnityEngine;

/// <summary>
/// 血族頭目「夏綠蒂公爵」（Duke Charlotte）。`BrainType=DukeCharlotte`。設計見 readme/BOSS_MODULE.md §14。
///
/// <para><b>一般狀態</b>（三招輪流，誰先就緒誰出）：</para>
/// <list type="bullet">
///   <item><b>慢慢走近</b>：CSV `Speed` 本來就給得慢；走到 <see cref="StopDistance"/> 內就站著。</item>
///   <item><b>原地旋轉射 8 方向血球</b>：attack（旋轉）播一輪 one-shot，在 CSV `ReleaseFrame` 那一幀用
///         `MonsterWeaponUser.TryUse` 射一發——8 方向是**配方**做的（`SpreadCount 8`／`SpreadAngle 315`），
///         Brain 只管「什麼時候射」。</item>
///   <item><b>跳躍砸地</b>：jumpStart（蓄力→離地）→ 程式抬到高空 → jumpLanding（下墜→觸地→起身）。
///         落地沿用狂族皇家衛士那一整套：裂地 shader、踐踏傷害圈、落地定格、下墜殘影、揚塵。
///         與皇家衛士不同的是會**一直重複**（冷卻 <see cref="LeapCooldownMin"/>~<see cref="LeapCooldownMax"/> 秒）。</item>
/// </list>
///
/// <para><b>大絕「血舞迴旋」</b>：血量第一次 ≤ 50% 時施放，之後每降 10%（40/30/20/10）再一次。
/// 旋轉著以 3 倍走路速度朝目標衝過去，碰到牆（尋徑格不可走）就**鏡面反彈**繼續轉（＝彈道系統的反彈路徑），
/// 每 <see cref="UltFireInterval"/> 秒射一輪 8 方向，持續 <see cref="UltDuration"/> 秒後停下、站一下、回到一般狀態。</para>
/// <para>⭐ <b>跨門檻只放一次</b>（作者 2026-10-26 拍板）：一口氣從 50% 打到 20% ⇒ 只放 20% 那一次，不補 40/30；
/// <b>大絕進行中跨過的門檻作廢</b>（50% 那次大絕中被打到 38% ⇒ 40% 那次不放，下一次是 30%）。
/// 兩條規則是同一行程式：<see cref="SkipCrossedThresholds"/> 在「開始時」與「結束時」各跑一次。</para>
///
/// <para><b>⚠ jumpLanding 第 1~4 幀頭頂超出畫布（被切掉）</b>。作者的處理（2026-10-26）：照播，
/// 但把她跳得夠高，讓那條切邊**一直待在鏡頭上緣以外**——玩家只看得到從畫面頂端垂下來的下半身。
/// 實作：下墜那四幀每一幀都用「那一幀 Sprite 的畫布上緣」算出最低高度（<see cref="CutFloorHeight"/>），
/// 程式高度不准低於它。用即時的鏡頭位置算，所以鏡頭在哪、她站在畫面哪裡都不會穿幫。</para>
///
/// <para>不可中斷：跳躍與大絕期間開 <c>SuppressKnockbackInterrupt</c>（PROBLEMS **F19**：擊退會整段跳過 Think，
/// 每幀自己算位移的演出會被吃掉）。位置都是「由經過時間算出的絕對值」，被跳過幾幀回來也會自動對位。</para>
/// </summary>
public class DukeCharlotteBrain : IMonsterBrain
{
    // ───────────────────────── 一般：走近 ─────────────────────────
    // 走到這麼近就停。她不是貼身型（傷害來自血球、跳躍與大絕），貼臉只會讓 8 方向從玩家兩側擦過。
    const float StopDistance = 2.5f;

    // ───────────────────────── 旋轉射擊 ─────────────────────────
    const float DefaultSpinRange = 8f;        // CSV `ShootRange` 留空時：目標在這距離內才原地旋轉射擊
    const float SpinCooldownMin = 2.5f;       // 兩次旋轉射擊的間隔（秒，從上一次「轉完」起算）
    const float SpinCooldownMax = 4.0f;
    const float FirstSpinDelay  = 1.5f;       // 開戰後多久才第一次旋轉（別一冒出來就射）
    const int   DefaultReleaseFrame = 13;     // CSV `ReleaseFrame` 留空時：attack 25 幀轉一圈，轉到一半射
    const float SpinFpsMul = 1f;              // 旋轉動畫相對 AnimFPS 的倍率

    // ───────────────────────── 跳躍砸地 ─────────────────────────
    const float LeapCooldownMin = 8f;         // 「偶爾」＝兩次跳躍之間隨機 8~12 秒
    const float LeapCooldownMax = 12f;
    const float FirstLeapDelay  = 5f;         // 開戰後至少這麼久才可能第一次跳
    const float LeapRangeMin = 2.5f;          // 目標在這個距離區間才跳（太近用旋轉、太遠先走近）
    const float LeapRangeMax = 6.5f;
    const float MinLeapDist  = 1.5f;          // 落點至少離起跳點這麼遠（同 LeapSlamBrain）
    // ⭐ 落點在**離地那一刻**鎖定（作者 2026-10-26 二修）：
    //   v1 一開始蹲下就鎖 ⇒「我已經離開該點很久了，他還是跳那個位置」（蓄力 0.93 秒都白預告了）；
    //   v2 一路追到開始下墜才鎖 ⇒「太難躲」（剩 0.4 秒）；
    //   v3（現行）蓄力期間持續看目標、**jumpStart 第 14 幀離地時記下目標位置**，之後上升／下墜都不再改 ⇒ 起跳後約 1.3 秒的閃避窗口。
    //   記錄時落點離起跳點最遠這麼遠。
    const float LeapTrackRangeMax = 9f;

    // 幀事件（**1-based**＝資料夾檔名編號）。2026-10-26 量每一幀不透明像素的上下緣得到：
    //   jumpStart   1~4 站立｜5~13 下蹲蓄力（底邊 433→440、頂邊 91→139）｜14 起身離地｜15~25 向上伸展
    //   jumpLanding 1~4 從畫面外掉進來（頂邊＝0 ⇒ 頭被畫布切掉）｜5 將觸地｜6 觸地｜7 壓到最扁｜8~25 回彈、站起
    const int TakeoffFrame   = 14;            // jumpStart：這一幀開始離地
    const int CutLastFrame   = 4;             // jumpLanding：1~這一幀的頭頂被切掉 ⇒ 畫布上緣必須在鏡頭外
    const int SlamFrame      = 5;             // jumpLanding：最後一段砸落時顯示的那一幀（已經看得到全身）
    const int TouchFrame     = 6;             // jumpLanding：觸地（落地結算在這一幀開始時）

    const float JumpFpsMul   = 1f;            // 起跳與落地起身的播放倍率（相對 AnimFPS）
    const float FallFpsMul   = 1f;            // 下墜那 4 幀（被切頭那段）的播放倍率
    const float SlamSeconds  = 0.12f;         // 從「切頭的最後一幀」砸到地面的時間（顯示 SlamFrame）。越小越像砸
    const float HopHeightInBodies  = 3.0f;    // 頂點高度＝身高 × 此（同 LeapSlamBrain；大多會直接跳出畫面）
    const float FallbackBodyHeight = 1.95f;
    const float HorizAtPeak = 0.9f;           // 到頂點時水平已走完的比例 ⇒ 下墜幾乎垂直
    const float CutMargin   = 0.5f;           // 切邊至少在鏡頭上緣外這麼遠（世界單位），吃掉一幀的時序誤差
    const float LandHoldSeconds = 0.4f;       // 起身動畫播完後再停多久才回到一般狀態（玩家的輸出窗口）
    const float LeapSafetySeconds = 6f;       // 整段跳躍的保險上限

    // 落地（與 LeapSlamBrain 同值同理由，見該檔註解與 BOSS_MODULE §9）
    const float CrackRadiusMul   = 1.47f;
    const float DefaultRadius    = 1.6f;      // CSV LeapRadius 留空時（體型 1 的世界單位，實際 × Scale，見 F29）
    const float DefaultDamageMul = 2.0f;      // CSV LeapDamage 留空時：接觸傷害 × 此
    const float HitStopSeconds   = 0.06f;
    const float AfterimageInterval = 0.035f;
    const float AfterimageLife     = 0.22f;
    const float AfterimageAlpha    = 0.40f;
    const int   LandDustVfxId  = 41;
    const float LandDustLiftY  = 0.38f;

    // ───────────────────────── 大絕：血舞迴旋 ─────────────────────────
    // 血量比例門檻（由高到低）。`≤` 該值就觸發。
    static readonly float[] UltThresholds = { 0.5f, 0.4f, 0.3f, 0.2f, 0.1f };
    const float UltDuration      = 10f;       // 整招持續秒數
    const float UltSpeedMul      = 3f;        // 相對 CSV `Speed`（走路）的倍率
    const float UltFireInterval  = 3f;        // 每隔幾秒射一輪 8 方向
    const float UltFirstShot     = 0.5f;      // 開招後多久射第一輪
    const float UltRecoverSeconds = 1.0f;     // 收招後站著不動多久（玩家的輸出窗口）
    // 反彈探測距離：中心往前這麼遠是不可走格就反彈。與尋徑格的淨空同一個量級（MapNavGrid.AgentRadius 0.3）。
    const float UltProbe = 0.35f;
    // 大絕用的武器（WeaponTable ID）：與一般旋轉的 73 同一種血球，**多了撞牆反彈 2 次**（配方 89，作者 2026-10-26）。
    // 一隻怪的 CSV `Weapon` 欄只能填一把，所以大絕這把由 Brain 自己再掛一個 MonsterWeaponUser。0＝沿用 CSV 那把。
    const int UltWeaponId = 74;

    /// <summary>除錯：印出每次出招、跳躍時間軸、大絕門檻。</summary>
    public static bool DebugLog = false;

    enum Phase { Approach, Spin, Leap, Ult, Recover }
    enum LeapStage { Crouch, Rise, Fall, Slam, Land }

    Phase _phase = Phase.Approach;
    bool _init;
    MonsterAnimator _anim;
    MonsterWeaponUser _ultWeapon;   // 大絕專用的第二把武器（見 UltWeaponId）
    SpriteRenderer _sr;
    Rigidbody2D _rb;
    RigidbodyInterpolation2D _savedInterp;
    bool _interpSaved;
    VfxManager _vfx;
    Camera _cam;

    float _phaseStart, _phaseUntil;
    float _nextSpinAt, _nextLeapAt;

    // 旋轉射擊
    bool _spinFired;
    int _releaseFrame;
    Transform _spinTarget;

    // 跳躍
    LeapStage _stage;
    Vector2 _from, _to;
    Transform _leapTarget;        // 起跳時鎖定的對象（下墜前持續追他的位置）
    float _peakH;                 // 頂點高度（起跳時定）
    float _stageStart;            // 目前這一段的起始時間
    float _tTakeoff, _tApex;      // 相對 _phaseStart
    float _tFall;                 // 下墜（切頭四幀）的長度
    float _hStageFrom;            // 這一段開始時的高度
    float _curH;                  // 目前的抬升量
    bool _impacted;
    float _radius, _damage;
    float _nextAfterimageAt;

    // 大絕
    int _ultIdx;                  // 下一個還沒用掉的門檻
    Vector2 _ultDir;
    float _ultSpeed;
    float _ultNextShot;

    public void Think(in MonsterContext ctx)
    {
        var self = ctx.Self;
        MonsterActuator act = ctx.Actuator;
        if (self == null || act == null) return;

        if (!_init)
        {
            _init = true;
            _anim = self.Anim;
            _sr = act.GetComponent<SpriteRenderer>();
            _rb = act.GetComponent<Rigidbody2D>();
            // 攻擊（旋轉）姿勢一律由本 Brain 控制：不設的話，光是「目標在 AttackRange 內」就會被 HandleVisuals
            // 自動循環播旋轉（＝邊走邊轉），那是大絕的樣子，平常不該出現。見 MonsterController.BrainControlsAttackPose。
            self.BrainControlsAttackPose = true;
            _nextSpinAt = Time.time + FirstSpinDelay;
            _nextLeapAt = Time.time + FirstLeapDelay;
            // 生成時血量就已經低於某些門檻（例：劇情用半血登場）⇒ 那些門檻視同用掉，下一個才算。
            // 不這樣做的話一登場就會連放大絕。
            SkipCrossedThresholds(self.HealthFraction, includeCurrent: false);
            _ultWeapon = SetupUltWeapon(self, act);
        }

        Transform target = LeapSlamBrain.Nearest(act.transform.position, ctx.Enemy, ctx.Player);

        switch (_phase)
        {
            case Phase.Approach: TickApproach(in ctx, act, target); break;
            case Phase.Spin:     TickSpin(in ctx, act, target);     break;
            case Phase.Leap:     TickLeap(in ctx, act, target);     break;
            case Phase.Ult:      TickUlt(in ctx, act, target);      break;
            case Phase.Recover:
                act.Stop();
                if (Time.time >= _phaseUntil) _phase = Phase.Approach;
                break;
        }
    }

    // ═════════════════════════ 一般狀態：決策 ═════════════════════════
    void TickApproach(in MonsterContext ctx, MonsterActuator act, Transform target)
    {
        var self = ctx.Self;
        // 先把大絕武器解析好：解析時才會寫入「起手緩衝」，等開大絕才解析的話第一輪會晚 1 秒（見 MonsterWeaponUser.Ready）
        if (_ultWeapon != null) _ = _ultWeapon.Ready;
        if (target == null) { act.Stop(); return; }

        // 大絕優先（只在「手上沒有動作」時檢查：跳躍蹲下去了就一定要跳完、旋轉播了就一定要射完）
        if (TryBeginUlt(in ctx, act, target)) return;

        float dist = Vector2.Distance(act.transform.position, target.position);

        if (Time.time >= _nextLeapAt && dist >= LeapRangeMin && dist <= LeapRangeMax && BeginLeap(in ctx, act, target))
            return;

        float spinRange = self.ShootRange > 0.01f ? self.ShootRange : DefaultSpinRange;
        var wu = self.WeaponUser;
        // ⚠ 先問 Ready 再開轉：動作播了就一定要射（BOSS_MODULE §8 鐵則），冷卻沒好就別開始演。
        if (Time.time >= _nextSpinAt && dist <= spinRange && wu != null && wu.Ready && BeginSpin(in ctx, act, target))
            return;

        if (dist > StopDistance) act.MoveTowards(target.position);
        else act.Stop();
    }

    // ═════════════════════════ 旋轉射擊 ═════════════════════════
    bool BeginSpin(in MonsterContext ctx, MonsterActuator act, Transform target)
    {
        if (_anim == null || !_anim.PlayOneShot(MonsterAnimator.State.Attack, 1, 0, SpinFpsMul)) return false;

        var self = ctx.Self;
        int total = Mathf.Max(1, _anim.FrameCount(MonsterAnimator.State.Attack));
        _releaseFrame = self.ReleaseFrame > 0 ? Mathf.Clamp(self.ReleaseFrame, 1, total) : Mathf.Min(DefaultReleaseFrame, total);
        float fps = Mathf.Max(1f, self.AnimFPS * SpinFpsMul);
        _phaseStart = Time.time;
        _phaseUntil = Time.time + total / fps + 0.5f;   // 保險上限：動畫出意外也不會卡住
        _spinFired = false;
        _spinTarget = target;
        act.Stop();
        _phase = Phase.Spin;
        if (DebugLog) Debug.Log($"[Duke] 旋轉射擊：放彈幀 {_releaseFrame}/{total}");
        return true;
    }

    void TickSpin(in MonsterContext ctx, MonsterActuator act, Transform target)
    {
        act.Stop();
        bool timeout = Time.time >= _phaseUntil;
        bool done = _anim == null || _anim.OneShotFinished || timeout;

        // 到放彈幀就射。用 `>=` 不用 `==`：被擊退跳過幾幀時（F19）回來仍會補射——動作播了就一定要射。
        if (!_spinFired && (done || (_anim != null && _anim.OneShotFrame >= _releaseFrame)))
        {
            _spinFired = true;
            Transform t = _spinTarget != null ? _spinTarget : target;
            var wu = ctx.Self.WeaponUser;
            if (t != null && wu != null) wu.TryUse(t);
        }

        if (done)
        {
            if (_anim != null) _anim.CancelOneShot();
            _nextSpinAt = Time.time + Random.Range(SpinCooldownMin, SpinCooldownMax);
            _phase = Phase.Approach;
        }
    }

    // ═════════════════════════ 跳躍砸地 ═════════════════════════
    bool BeginLeap(in MonsterContext ctx, MonsterActuator act, Transform target)
    {
        var self = ctx.Self;
        // 兩組圖缺一就**永遠不跳**（不硬演，同 LeapSlamBrain 的原則）
        if (_anim == null || !_anim.Has(MonsterAnimator.State.JumpStart) || !_anim.Has(MonsterAnimator.State.JumpLanding))
        {
            _nextLeapAt = float.MaxValue;
            return false;
        }
        if (!_anim.PlayOneShot(MonsterAnimator.State.JumpStart, 1, 0, JumpFpsMul)) return false;

        Vector2 pos = act.transform.position;
        _from = pos;
        _leapTarget = target;

        // 落點：先算一次（給起跳時的頂點高度用），之後蓄力與上升期間每幀重算（TrackLeapTarget）。
        _peakH = 0f;
        TrackLeapTarget(target);

        float bodyH = self.CharacterWorldHeight > 0.01f ? self.CharacterWorldHeight : FallbackBodyHeight;
        bodyH *= Mathf.Abs(act.transform.localScale.y);
        // 頂點：至少「三個身高」，而且至少高到落地第 1 幀的切邊在鏡頭外（見 CutFloorHeight）——
        // 換到切頭那一幀的瞬間切邊就已經在畫面外，不會閃一下。落點在上升期間還會變，所以 TrackLeapTarget 只會把它往上加。
        _peakH = Mathf.Max(_peakH, bodyH * HopHeightInBodies);

        float fps = Mathf.Max(1f, self.AnimFPS * JumpFpsMul);
        int startFrames = Mathf.Max(TakeoffFrame, _anim.FrameCount(MonsterAnimator.State.JumpStart));
        _tTakeoff = (TakeoffFrame - 1) / fps;
        _tApex    = startFrames / fps;
        _tFall    = CutLastFrame / Mathf.Max(1f, self.AnimFPS * FallFpsMul);

        float baseR = self.LeapRadius > 0.01f ? self.LeapRadius : DefaultRadius;
        _radius = self.ScaledRadius(baseR);                                    // F29：半徑隨體型
        _damage = self.LeapDamage > 0.01f ? self.LeapDamage : self.ContactDamage * DefaultDamageMul;

        act.Stop();
        DisableInterp();
        self.SuppressKnockbackInterrupt = true;   // F19：蹲下去了就一定要跳出去（同 LeapSlamBrain §9.3）

        _impacted = false;
        _curH = 0f;
        _phaseStart = Time.time;
        _stage = LeapStage.Crouch;
        _stageStart = Time.time;
        _phase = Phase.Leap;

        if (DebugLog)
            Debug.Log($"[Duke] 起跳：距離={Vector2.Distance(_from, _to):F2} 落點={_to} 頂點高={_peakH:F2} " +
                      $"離地@{_tTakeoff:F2}s 頂點@{_tApex:F2}s 下墜{_tFall:F2}s＋砸落{SlamSeconds:F2}s 半徑={_radius:F2} 傷害={_damage:F0}");
        return true;
    }

    void TickLeap(in MonsterContext ctx, MonsterActuator act, Transform target)
    {
        var self = ctx.Self;
        float e = Time.time - _phaseStart;
        if (_rb != null && _stage != LeapStage.Land) _rb.velocity = Vector2.zero;   // 騰空期間位置全由下面決定

        if (e >= LeapSafetySeconds) { FinishLeap(in ctx, act); return; }

        switch (_stage)
        {
            case LeapStage.Crouch:
                // 蓄力：站著不動＝玩家的預告窗口。蓄力期間持續看目標，離地那一刻（離開這一段）就是最後一次＝鎖定
                TrackLeapTarget(_leapTarget != null ? _leapTarget : target);
                if (e >= _tTakeoff) { _stage = LeapStage.Rise; _stageStart = Time.time; }
                break;

            case LeapStage.Rise:
            {
                float t = Mathf.Clamp01((e - _tTakeoff) / Mathf.Max(0.0001f, _tApex - _tTakeoff));
                _curH = _peakH * Mathf.Sin(t * Mathf.PI * 0.5f);                  // 升得快、接近頂點趨緩
                Place(act, self, Vector2.Lerp(_from, _to, HorizAtPeak * Mathf.Sin(t * Mathf.PI * 0.5f)), _curH);
                if (e >= _tApex)
                {
                    // 換成落地動作的前 4 幀（切頭的那段），單獨一段 one-shot 才能控制它的長度
                    _anim.PlayOneShot(MonsterAnimator.State.JumpLanding, 1, CutLastFrame, FallFpsMul);
                    _stage = LeapStage.Fall;
                    _stageStart = Time.time;
                    _hStageFrom = _curH;
                }
                break;
            }

            case LeapStage.Fall:
            {
                // 下墜（切頭四幀）：從頂點往下掉到「第 4 幀的切邊剛好還在鏡頭外」的高度，越掉越快。
                float u = Mathf.Clamp01((Time.time - _stageStart) / Mathf.Max(0.0001f, _tFall));
                float hEnd = Mathf.Max(0f, CutFloorHeight(CutLastFrame, _to.y));
                float h = Mathf.Lerp(_hStageFrom, Mathf.Min(_hStageFrom, hEnd), u * u);
                // ⭐ 硬性保護：目前顯示的那一幀如果是切頭幀，高度不准低於它的切邊需求（鏡頭在動也成立）
                int f = _anim != null ? _anim.OneShotFrame : 0;
                if (f >= 1 && f <= CutLastFrame) h = Mathf.Max(h, CutFloorHeight(f, _to.y));
                _curH = h;
                float horizT = HorizAtPeak + (1f - HorizAtPeak) * 0.5f * u;
                Place(act, self, Vector2.Lerp(_from, _to, horizT), _curH);
                SpawnAfterimage();
                if (u >= 1f && (_anim == null || _anim.OneShotFinished))
                {
                    _anim.PlayOneShot(MonsterAnimator.State.JumpLanding, SlamFrame, SlamFrame, 1f);
                    _stage = LeapStage.Slam;
                    _stageStart = Time.time;
                    _hStageFrom = _curH;
                }
                break;
            }

            case LeapStage.Slam:
            {
                // 砸落：已經看得到全身（第 5 幀），用很短的時間垂直砸到地面
                float v = Mathf.Clamp01((Time.time - _stageStart) / Mathf.Max(0.0001f, SlamSeconds));
                _curH = _hStageFrom * (1f - v * v);
                float horizT = HorizAtPeak + (1f - HorizAtPeak) * (0.5f + 0.5f * v);
                Place(act, self, Vector2.Lerp(_from, _to, horizT), _curH);
                SpawnAfterimage();
                if (v >= 1f)
                {
                    Impact(in ctx, act);
                    _anim.PlayOneShot(MonsterAnimator.State.JumpLanding, TouchFrame, 0, JumpFpsMul);
                    _stage = LeapStage.Land;
                    _stageStart = Time.time;
                }
                break;
            }

            case LeapStage.Land:
            {
                // 起身：動畫播完再停 LandHoldSeconds 才交回一般狀態。這段已經可以被擊退（Impact 裡還回去了）。
                bool animDone = _anim == null || _anim.OneShotFinished;
                if (animDone && _phaseUntil <= 0f) _phaseUntil = Time.time + LandHoldSeconds;
                if (animDone && Time.time >= _phaseUntil) FinishLeap(in ctx, act);
                break;
            }
        }
    }

    /// <summary>
    /// 依目標**現在**的位置重算落點（只在蓄力期間每幀呼叫；離地之後就不再呼叫＝鎖定在離地那一刻的位置）。
    /// 規則與皇家衛士共用 <see cref="LeapSlamBrain.ResolveLanding"/>：太遠取射程邊緣、太近往前踏一步、被牆擋就往兩側找替代點。
    /// </summary>
    void TrackLeapTarget(Transform t)
    {
        if (t == null) return;
        Vector2 want = t.position;
        Vector2 d = want - _from;
        float len = d.magnitude;
        Vector2 dir = len < 1e-4f ? Vector2.right : d / len;
        if (len > LeapTrackRangeMax) want = _from + dir * LeapTrackRangeMax;
        else if (len < MinLeapDist)  want = _from + dir * MinLeapDist;
        _to = LeapSlamBrain.ResolveLanding(_from, want, t.position, MinLeapDist);
        // 落點往畫面下方移時，切邊需要的高度會變高 ⇒ 頂點只增不減（上升曲線會自然跟上）
        _peakH = Mathf.Max(_peakH, CutFloorHeight(1, _to.y) + 0.3f);
    }

    /// <summary>把怪放到「地面點 ground ＋ 抬升 h」。影子與 Y 排序由 AirborneVisualHeight 扣回地面（IAirborneVisual）。</summary>
    void Place(MonsterActuator act, MonsterController self, Vector2 ground, float h)
    {
        act.transform.position = new Vector3(ground.x, ground.y + h, act.transform.position.z);
        self.AirborneVisualHeight = h;
    }

    void SpawnAfterimage()
    {
        if (Time.time < _nextAfterimageAt || _sr == null) return;
        _nextAfterimageAt = Time.time + AfterimageInterval;
        Afterimage.Spawn(_sr, AfterimageLife, AfterimageAlpha);
    }

    /// <summary>
    /// 落地 jumpLanding 第 <paramref name="frame"/> 幀時，抬升量至少要多少，畫布上緣（＝頭被切掉的那條線）
    /// 才會在鏡頭上緣外 <see cref="CutMargin"/>。
    /// <para>畫布上緣相對 transform 的本地高度＝(rect 高 − pivot.y) ÷ PPU。每一幀的 pivot 都不一樣
    /// （腳底對齊是逐幀補 pivot 的），所以一定要拿那一幀真正的 Sprite 來算。</para>
    /// <para>取不到鏡頭（單場景測試）或取不到圖時回 0＝不加限制。</para>
    /// </summary>
    float CutFloorHeight(int frame, float groundY)
    {
        if (_cam == null) { _cam = Camera.main; if (_cam == null) _cam = Object.FindObjectOfType<Camera>(); }
        if (_cam == null || !_cam.orthographic || _anim == null || _sr == null) return 0f;
        Sprite s = _anim.FrameSprite(MonsterAnimator.State.JumpLanding, frame);
        if (s == null || s.pixelsPerUnit <= 0f) return 0f;
        float topLocal = (s.rect.height - s.pivot.y) / s.pixelsPerUnit;
        float topAboveTransform = topLocal * Mathf.Abs(_sr.transform.lossyScale.y);
        float camTop = _cam.transform.position.y + _cam.orthographicSize;
        return camTop + CutMargin - groundY - topAboveTransform;
    }

    void Impact(in MonsterContext ctx, MonsterActuator act)
    {
        if (_impacted) return;
        _impacted = true;
        var self = ctx.Self;

        act.transform.position = new Vector3(_to.x, _to.y, act.transform.position.z);
        _curH = 0f;
        self.AirborneVisualHeight = 0f;
        HitStop.Play(HitStopSeconds);

        // 一落地就把擊退中斷權與內插還回去：起身那段是玩家的輸出窗口（同 LeapSlamBrain）
        self.SuppressKnockbackInterrupt = false;
        RestoreInterp();

        // 視覺對準腳底、判定對準 transform（理由見 LeapSlamBrain 落地段與 PROBLEMS F27／G13）
        Vector2 foot = self.FeetWorldPos;
        GroundCrackFx.Spawn(foot, _radius * CrackRadiusMul, _sr);
        if (LandDustVfxId > 0)
        {
            if (_vfx == null) _vfx = Object.FindObjectOfType<VfxManager>();
            if (_vfx != null) _vfx.Spawn(LandDustVfxId, foot + new Vector2(0f, LandDustLiftY));
        }
        ImpactDamageArea.Spawn(_to, _radius, _damage, self.Faction);
        _phaseUntil = 0f;   // Land 段用來記「起身播完後停到幾點」

        if (DebugLog) Debug.Log($"[Duke] 落地 @{Time.time - _phaseStart:F2}s 腳底={foot}");
    }

    /// <summary>收掉跳躍（任何原因走到這裡都保證「至少砸過一次」，同 LeapSlamBrain.FinishLeap）。</summary>
    void FinishLeap(in MonsterContext ctx, MonsterActuator act)
    {
        var self = ctx.Self;
        if (!_impacted) Impact(in ctx, act);
        self.AirborneVisualHeight = 0f;
        self.SuppressKnockbackInterrupt = false;
        RestoreInterp();
        if (_anim != null) _anim.CancelOneShot();
        _nextLeapAt = Time.time + Random.Range(LeapCooldownMin, LeapCooldownMax);
        _nextSpinAt = Mathf.Max(_nextSpinAt, Time.time + 1.0f);
        _phase = Phase.Approach;
    }

    // ═════════════════════════ 大絕：血舞迴旋 ═════════════════════════

    /// <summary>
    /// 把「已經跨過」的門檻全部標記用掉。<paramref name="includeCurrent"/>：true＝血量剛好等於門檻也算跨過。
    /// 開招時呼叫 ⇒ 一次跨好幾格只放一次；收招時呼叫 ⇒ 大絕中跨過的門檻作廢。
    /// </summary>
    void SkipCrossedThresholds(float hp, bool includeCurrent = true)
    {
        while (_ultIdx < UltThresholds.Length &&
               (includeCurrent ? hp <= UltThresholds[_ultIdx] : hp < UltThresholds[_ultIdx]))
            _ultIdx++;
    }

    bool TryBeginUlt(in MonsterContext ctx, MonsterActuator act, Transform target)
    {
        var self = ctx.Self;
        if (_ultIdx >= UltThresholds.Length) return false;
        float hp = self.HealthFraction;
        if (hp > UltThresholds[_ultIdx] || hp <= 0f) return false;

        if (DebugLog) Debug.Log($"[Duke] 大絕：血量 {hp:P0}（門檻 {UltThresholds[_ultIdx]:P0}）");
        SkipCrossedThresholds(hp);

        Vector2 pos = act.transform.position;
        Vector2 d = (Vector2)target.position - pos;
        _ultDir = d.sqrMagnitude > 1e-4f ? d.normalized : Random.insideUnitCircle.normalized;
        if (_ultDir.sqrMagnitude < 1e-4f) _ultDir = Vector2.right;
        _ultSpeed = act.MoveSpeed * UltSpeedMul;
        _ultNextShot = Time.time + UltFirstShot;

        act.Stop();
        if (_anim != null) _anim.CancelOneShot();
        // 旋轉姿勢交給 HandleVisuals 循環播（BrainPose），移動由本 Brain 每幀直接給速度
        self.BrainPoseActive = true;
        self.BrainPose = MonsterAnimator.State.Attack;
        self.SuppressKnockbackInterrupt = true;   // 霸體：被打不會停下來（F19）

        _phaseStart = Time.time;
        _phase = Phase.Ult;
        return true;
    }

    void TickUlt(in MonsterContext ctx, MonsterActuator act, Transform target)
    {
        var self = ctx.Self;
        if (Time.time - _phaseStart >= UltDuration) { EndUlt(in ctx, act); return; }

        // ── 移動＋撞牆反彈（鏡面：撞到哪個軸就翻哪個軸，＝彈道系統打在牆上的反射）──
        Vector2 pos = act.transform.position;
        float step = _ultSpeed * Mathf.Max(ctx.DeltaTime, 0.0001f);
        var nav = MapNavGrid.Instance;
        if (nav != null && nav.Ready)
        {
            if (!nav.IsWalkableWorld(pos))
            {
                // 人已經在不可走格上（被擠進家具淨空、或出生點貼牆）：探測會四面都擋 ⇒ 原地抖動。
                // 改成朝目標走，先離開這一格再說（目標一定站在可走的地方）。
                if (target != null)
                {
                    Vector2 toT = (Vector2)target.position - pos;
                    if (toT.sqrMagnitude > 1e-4f) _ultDir = toT.normalized;
                }
            }
            else
            {
                float sx = _ultDir.x >= 0f ? 1f : -1f;
                float sy = _ultDir.y >= 0f ? 1f : -1f;
                bool blockX = Mathf.Abs(_ultDir.x) > 1e-3f &&
                              !nav.IsWalkableWorld(pos + new Vector2(_ultDir.x * step + sx * UltProbe, 0f));
                bool blockY = Mathf.Abs(_ultDir.y) > 1e-3f &&
                              !nav.IsWalkableWorld(pos + new Vector2(0f, _ultDir.y * step + sy * UltProbe));
                // 兩軸單獨都通、斜著卻撞上＝正好打在凸角上 ⇒ 兩軸都翻（原路彈回）
                if (!blockX && !blockY && !nav.IsWalkableWorld(pos + _ultDir * (step + UltProbe)))
                    blockX = blockY = true;
                if (blockX) _ultDir.x = -_ultDir.x;
                if (blockY) _ultDir.y = -_ultDir.y;
                if ((blockX || blockY) && DebugLog) Debug.Log($"[Duke] 反彈 → {_ultDir}");
            }
        }

        if (_rb != null) _rb.velocity = _ultDir * _ultSpeed;   // 每幀覆寫 ⇒ 擊退的位移也一併壓掉（霸體）
        else act.transform.position = pos + _ultDir * step;

        // ── 每 UltFireInterval 秒射一輪 8 方向 ──
        var uw = _ultWeapon != null ? _ultWeapon : self.WeaponUser;   // 大絕用會反彈的那把（見 UltWeaponId）
        if (Time.time >= _ultNextShot && target != null && uw != null)
        {
            // 冷卻沒好就下一幀再試（不把排程往後推），所以實際間隔是 max(UltFireInterval, 配方 FireInterval)
            if (uw.TryUse(target)) _ultNextShot = Time.time + UltFireInterval;
        }
    }

    void EndUlt(in MonsterContext ctx, MonsterActuator act)
    {
        var self = ctx.Self;
        self.BrainPoseActive = false;
        self.SuppressKnockbackInterrupt = false;
        act.Stop();
        // ⭐ 大絕進行中跨過的門檻作廢（作者 2026-10-26）
        SkipCrossedThresholds(self.HealthFraction);
        if (DebugLog)
            Debug.Log($"[Duke] 大絕結束：血量 {self.HealthFraction:P0}，下一個門檻 " +
                      (_ultIdx < UltThresholds.Length ? UltThresholds[_ultIdx].ToString("P0") : "（沒有了）"));

        _phase = Phase.Recover;
        _phaseUntil = Time.time + UltRecoverSeconds;
        _nextSpinAt = Mathf.Max(_nextSpinAt, Time.time + UltRecoverSeconds + 1.0f);
        _nextLeapAt = Mathf.Max(_nextLeapAt, Time.time + UltRecoverSeconds + 3.0f);
    }

    // ═════════════════════════ 小工具 ═════════════════════════

    /// <summary>
    /// 掛上大絕專用的第二個 <see cref="MonsterWeaponUser"/>。CSV 那把已經由 MonsterController 掛好、存在 <c>self.WeaponUser</c>；
    /// 這裡找「不是那一個」的元件重用（怪被重新 Initialize 時不會越掛越多），沒有才新增。
    /// ⚠ MonsterController 取自己那把用的是 <c>GetComponent</c>（＝第一個），而它在 Initialize 時就先掛好了，
    ///   所以第二個永遠排在後面，不會被誤認成主武器。
    /// </summary>
    static MonsterWeaponUser SetupUltWeapon(MonsterController self, MonsterActuator act)
    {
        if (UltWeaponId <= 0 || UltWeaponId == self.WeaponId) return null;
        MonsterWeaponUser extra = null;
        foreach (var w in act.GetComponents<MonsterWeaponUser>())
            if (w != self.WeaponUser) { extra = w; break; }
        if (extra == null) extra = act.gameObject.AddComponent<MonsterWeaponUser>();
        extra.Configure(self, UltWeaponId);
        return extra;
    }
    // ⚠ 騰空期間每幀直接寫 transform，Rigidbody2D 的內插會用「上一個物理步的位置」往回補 ⇒ 兩邊拉扯、看起來抖。
    //   起跳時關掉、落地還原（同 LeapSlamBrain）。
    void DisableInterp()
    {
        if (_rb == null || _interpSaved) return;
        _savedInterp = _rb.interpolation;
        _interpSaved = true;
        _rb.interpolation = RigidbodyInterpolation2D.None;
    }

    void RestoreInterp()
    {
        if (_rb == null || !_interpSaved) return;
        _rb.interpolation = _savedInterp;
        _interpSaved = false;
    }
}
