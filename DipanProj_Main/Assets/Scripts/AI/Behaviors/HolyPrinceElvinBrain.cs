using System.Collections.Generic;
using UnityEngine;
using Sorrows.Ballistics;

/// <summary>
/// 狂族頭目「17 聖子・艾爾文」（17th Holy Prince – Elvin）。`BrainType=HolyPrinceElvin`。設計見 readme/BOSS_MODULE.md §16。
///
/// <para><b>一般狀態</b>（優先序：大絕 ＞ 跳劈 ＞ 近戰追擊；閃避隨時插隊，見下）：</para>
/// <list type="bullet">
///   <item><b>近戰追擊</b>：整段**委派 <see cref="MeleeChaseBrain"/>**（站定揮完、命中幀結算、起手／停止兩個門檻），
///         同菁英血衛士的做法——那套踩過 F22／F27／F29 才定版，不要重寫。</item>
///   <item><b>跳劈衝擊波</b>：每 <see cref="SlashInterval"/> 秒一次，**不限距離**（作者：就算很近也會施展）。
///         slash 跳起來往下劈，劈到地上那一幀（<see cref="SlashReleaseFrame"/>）朝目標放出**三向**聖光衝擊波——
///         三向是**配方**做的（RecipeTable 90：`SpreadCount 3`／`SpreadAngle 50`＝±25°），Brain 只管什麼時候放、從哪裡放。</item>
/// </list>
///
/// <para><b>大絕「血色劍域」</b>：血量第一次 ≤ 50% 時施放，之後每降 10%（40/30/20/10）再一次（門檻規則同夏綠蒂公爵：
/// 一口氣跨好幾格只放一次、大絕進行中跨過的門檻作廢）。連續 slash <see cref="UltWaves"/> 波、每波播放加速
/// <see cref="UltSlashFpsMul"/> 倍，每一波固定 <see cref="UltCount"/> 道、相鄰間隔 <see cref="UltSpacingDeg"/>°，
/// **每一波都以「放波那一刻」的玩家位置為中軸**展開紅色衝擊波（武器 76，扇形由 <c>MonsterWeaponUser.TryUseAt</c> 展開）。
/// （2026-10-27 作者：第一版每波隨機抽組合不 OK，改固定 6 道。）</para>
///
/// <para><b>閃避（dodge）</b>：冷卻 <see cref="DodgeCooldown"/> 秒（從開始閃算起）。只有「手上沒動作」時能閃——
/// 揮劍、跳劈、大絕中都不能閃（出手不反悔，那是玩家的反擊窗口）。觸發有兩條路：</para>
/// <list type="number">
///   <item><b>子彈提前預判</b>：每幀掃 <c>BulletInstance.Active</c>，找「打得到我、預計 <see cref="PredictWindow"/> 秒內會撞上我」的子彈
///         （或已經貼到身邊、正朝我飛的）⇒ 先閃。閃避一開始就是虛體，子彈直接穿過去。</item>
///   <item><b>受擊攔截</b>（雷射、近戰、地面 AOE、落雷…沒有「飛過來」的過程）：第一下打到的瞬間改成閃避、這一下作廢
///         （<c>MonsterController.DamageInterceptor</c>）。持續傷害（DOT）不觸發。</item>
/// </list>
/// <para>無敵＝dodge 第 <see cref="DodgeInvulnFrom"/>~<see cref="DodgeInvulnTo"/> 幀，走 <c>SetUntargetable</c>
/// （換到 Mist 層 ⇒ **飛行道具直接穿過去**，不是撞上來被擋掉）。第一版是 4~17 幀，作者實測：閃避中子彈還是撞上、噴擊中特效，
/// 要的是「穿透」⇒ 改成從第 1 幀起。受擊攔截觸發的那一下，子彈也會穿過去（<c>BulletInstance.PassThroughCurrentHit</c>）。
/// 原地消失、原地出現。
/// 第 <see cref="ShadowHideFrom"/>~<see cref="ShadowHideTo"/> 幀身形幾乎全空，影子一起收掉。</para>
///
/// <para>素材（2026-10-27 量，256 畫布、全部動作同尺寸）：</para>
/// <list type="bullet">
///   <item>attack 25：1–5 起手、6–9 舉劍、<b>10–11 劍掃到最前</b>（bbox 右緣 202→252）、12–19 停在揮完、20–25 收招 ⇒ CSV `AttackHitFrame 11`。</item>
///   <item>slash 25：1–6 站、7–9 舉劍過頭、<b>10–13 騰空</b>（腳離地約 50px）、14 下劈、<b>15–17 砍地</b>（17 劍光最寬）、18–25 收招。</item>
///   <item>dodge 22：1–3 站、4–10 旋風包住、<b>11–13 幾乎全空</b>、14–17 風中現身、18–22 回站姿。</item>
/// </list>
/// <para>⚠ slash／dodge 載圖用「只量第一幀」對齊腳底（MonsterAnimator，PROBLEMS **G17**），否則跳不起來、風會亂跳。</para>
///
/// <para>不可中斷：跳劈、大絕、閃避期間開 <c>SuppressKnockbackInterrupt</c>（PROBLEMS **F19**：擊退會整段跳過 Think，
/// 幀事件會被吃掉）。</para>
/// </summary>
public class HolyPrinceElvinBrain : IMonsterBrain
{
    // ───────────────────────── 跳劈（一般） ─────────────────────────
    const float SlashInterval   = 5f;     // 每幾秒跳劈一次（從上一次「開始」算）。作者：每隔 5 秒
    const float FirstSlashDelay = 5f;     // 開戰（第一次看到目標）後多久第一次跳劈
    const int   SlashReleaseFrame = 17;   // slash 第幾幀放衝擊波（1-based＝檔名編號；17＝劍砍進地面、劍光最寬）
    const float SlashFpsMul     = 1f;     // 一般跳劈的播放倍率（相對 CSV AnimFPS）
    const float SlashRecoverSeconds = 0.15f;   // 劈完站多久才回到追擊

    // 衝擊波的出手點：腳底往前推「身體框半寬 + 這麼多」，再抬高這麼多。
    //   抬 1.0 有兩個理由剛好重疊：① 衝擊波圖高約 2.56，中心抬 1.0 ⇒ 下緣略埋進地面＝貼地衝；
    //   ② 判定圓（配方 Radius 0.6）跟著子彈中心走，而玩家的碰撞圓在**胸口**（腳底往上約 1.0，PROBLEMS B13）
    //      ⇒ 判定圓在「行進線往上 1.0」＝玩家的腳踩在行進線上時兩個圓剛好對齊。
    //   ⚠ 判定半徑不要照圖的高度給：圖的高度是「往上竄多高」，不是「在地上佔多寬」。給 1.25 時玩家要離行進線 1.75 才安全，
    //     差不多一整個身高，作者實測「以為躲開了還是被打到」（2026-10-27 縮成 0.6）。
    const float ShockForward = 0.3f;
    const float ShockLift    = 1.0f;
    const int   ImpactDustVfxId = 41;     // 砍地那一下的揚塵（VfxTable 41，同跳躍踐踏）；0＝不放
    const float ImpactDustScale = 1.3f;

    // ───────────────────────── 大絕：血色劍域 ─────────────────────────
    static readonly float[] UltThresholds = { 0.5f, 0.4f, 0.3f, 0.2f, 0.1f };   // `≤` 該值就觸發（同夏綠蒂公爵）
    const int   UltWaves       = 5;       // 一共幾波（作者：射出 5 波才停止）
    const float UltSlashFpsMul = 1.5f;    // 大絕時 slash 加速（25 幀 @14fps 一般 1.8 秒 ⇒ 1.5 倍約 1.2 秒一波）
    const float UltRecoverSeconds = 1.0f; // 收招後站著不動多久（玩家的輸出窗口）
    // 大絕用的武器（WeaponTable ID）：紅色衝擊波，配方 91 不分裂、方向由本 Brain 展開。
    // 一隻怪的 CSV `Weapon` 欄只能填一把（＝一般跳劈的 75），所以這把由 Brain 自己再掛一個 MonsterWeaponUser（同 DukeCharlotteBrain）。
    const int   UltWeaponId    = 76;
    // 每一波固定 6 道、相鄰間隔 30°（總角度 150°，±75°），以「放波那一刻的玩家位置」為中軸對稱（作者 2026-10-27）。
    // 為什麼是 30°（衝擊波判定半徑 0.6 ＋ 玩家碰撞圓 0.5 ＝ 離行進線 1.1 以內就中）：
    //   ‧ 6 是偶數 ⇒ 中軸上沒有一道，玩家站在正中間兩道（±15°）的夾縫裡；夾縫在距離 d 處的半寬＝d·tan15° ≈ 0.27d，
    //     d < 4.1 時小於 1.1 ⇒ **離他約 4 單位以內站著不動一定吃到**（不會因為偶數道而「站著不動最安全」）。
    //   ‧ 相鄰兩道之間要有「離兩邊都 > 1.1」的位置 ⇒ 2d·sin15° > 2.2 ⇒ d ≳ 4.3 就有縫可鑽，越遠縫越寬。
    //   ⇒ 貼近他時躲不掉、拉開一點就有縫——壓迫感夠、但躲得掉。
    //   （2026-10-27 判定半徑從 1.25 縮到 0.6 之前，上面兩個數字是 6.5／6.8，作者實測「以為躲開了還是被打到」。）
    const int   UltCount      = 6;
    const float UltSpacingDeg = 30f;
    // ⭐ 每一波都要有一道**正對玩家**（作者 2026-10-27 實測：躲過第一波後站著不動，之後每一波都剛好站在正中間兩道的縫裡，全部躲掉）。
    //   6 是偶數 ⇒ 對稱展開時正中是縫 ⇒ 整個扇形轉半個間隔（±15°），讓其中一道落在中軸上；
    //   **單數波往一邊轉、雙數波往另一邊轉**：站在上一波的縫裡不動，下一波一定有一道正中，非移動不可。
    //   （奇數道數時中軸本來就有一道，不必轉，見 UltRotateFor。）

    // ───────────────────────── 閃避 ─────────────────────────
    const float DodgeCooldown  = 3f;      // 冷卻（從開始閃算起）。作者：每 3 秒可以躲一次
    const int   DodgeInvulnFrom = 1;      // 無敵（虛體、飛行道具穿過）幀（1-based，含兩端）。2026-10-27 第一版 4~17，作者實測要「一閃就穿透」⇒ 1~17
    const int   DodgeInvulnTo   = 17;
    const int   ShadowHideFrom  = 10;     // 身形幾乎全空的那幾幀，影子一起收（第 11~13 幀幾乎沒有像素）
    const int   ShadowHideTo    = 14;
    const float DodgeFpsMul    = 1f;
    // 子彈預判：預計這麼多秒內會撞上就閃（閃避第一幀就是虛體，所以沒有下限——再近都來得及穿過去）。
    // 追蹤彈會轉彎，直線外插不準 ⇒ 另外加一條「已經貼到身邊、而且正朝我飛」也算（PredictNearDistance）。
    const float PredictWindow  = 0.3f;
    const float PredictNearDistance = 1.2f;   // 子彈中心離我的身體邊緣這麼近、且速度朝我 ⇒ 直接閃
    const float PredictBodyShrink = 0.85f;   // 預判用的身體半徑＝碰撞框半寬高的較大者 × 此（略縮，擦邊的不閃）

    /// <summary>除錯：印出每次出招、閃避觸發原因、大絕門檻。</summary>
    public static bool DebugLog = false;

    enum Phase { Chase, Slash, Ult, Dodge, Recover }

    Phase _phase = Phase.Chase;
    bool _init;
    readonly MeleeChaseBrain _melee = new MeleeChaseBrain();
    MonsterController _self;
    MonsterAnimator _anim;
    MonsterWeaponUser _ultWeapon;
    Collider2D _selfCol;
    BlobShadow _shadow;
    VfxManager _vfx;
    bool _vfxLooked;

    float _phaseUntil;
    float _nextSlashAt = -1f;     // -1＝還沒看到目標（第一次看到時才開始倒數）
    bool _fired;                  // 這一刀的衝擊波放過了沒
    Transform _slashTarget;

    // 大絕
    int _ultIdx;
    int _ultWave;

    // 閃避
    float _nextDodgeAt;
    bool _invuln, _shadowHidden;

    public void Think(in MonsterContext ctx)
    {
        var self = ctx.Self;
        MonsterActuator act = ctx.Actuator;
        if (self == null || act == null) return;

        if (!_init)
        {
            _init = true;
            _self = self;
            _anim = self.Anim;
            _selfCol = act.GetComponent<Collider2D>();
            _shadow = act.GetComponent<BlobShadow>();
            _melee.EnsureConfigured(self);          // 接管攻擊姿勢、關掉「碰到就痛」（傷害只來自揮擊）
            SkipCrossedThresholds(self.HealthFraction, includeCurrent: false);   // 半血登場不會一出來就連放大絕
            _ultWeapon = SetupUltWeapon(self, act);
            self.DamageInterceptor = OnIncomingDamage;   // 受擊攔截（閃避的第二條路）
        }

        Transform target = LeapSlamBrain.Nearest(act.transform.position, ctx.Enemy, ctx.Player);
        if (target != null && _nextSlashAt < 0f) _nextSlashAt = Time.time + FirstSlashDelay;

        switch (_phase)
        {
            case Phase.Chase:   TickChase(in ctx, act, target); break;
            case Phase.Slash:   TickSlash(in ctx, act);         break;
            case Phase.Ult:     TickUlt(in ctx, act, target);   break;
            case Phase.Dodge:   TickDodge(act);                 break;
            case Phase.Recover:
                act.Stop();
                if (TryPredictDodge()) break;        // 收招的小硬直裡也能閃（手上已經沒有動作）
                if (Time.time >= _phaseUntil) _phase = Phase.Chase;
                break;
        }
    }

    // ═════════════════════════ 一般：決策 ═════════════════════════
    void TickChase(in MonsterContext ctx, MonsterActuator act, Transform target)
    {
        var self = ctx.Self;
        if (_ultWeapon != null) _ = _ultWeapon.Ready;   // 先解析大絕武器（起手緩衝在解析時才寫入，見 MonsterWeaponUser.Ready）
        _ = self.WeaponUser != null && self.WeaponUser.Ready;

        bool busy = _melee.IsBusy;   // 揮劍／收招中：不能插別的招、也不能閃（出手不反悔）
        if (!busy)
        {
            if (TryPredictDodge()) return;
            if (target != null && TryBeginUlt(in ctx, act, target)) return;
            if (target != null && _nextSlashAt >= 0f && Time.time >= _nextSlashAt && BeginSlash(in ctx, act, target, false)) return;
        }
        _melee.Think(in ctx);
    }

    // ═════════════════════════ 跳劈 ═════════════════════════
    bool BeginSlash(in MonsterContext ctx, MonsterActuator act, Transform target, bool ult)
    {
        if (_anim == null) return false;
        float mul = ult ? UltSlashFpsMul : SlashFpsMul;
        if (!_anim.PlayOneShot(MonsterAnimator.State.Slash, 1, 0, mul)) return false;

        var self = ctx.Self;
        act.Stop();
        _fired = false;
        _slashTarget = target;
        self.SuppressKnockbackInterrupt = true;   // F19：跳起來了就一定要劈下去

        float fps = Mathf.Max(1f, self.AnimFPS * mul);
        int frames = _anim.FrameCount(MonsterAnimator.State.Slash);
        _phaseUntil = Time.time + (frames > 0 ? frames / fps : 2f) + 0.5f;   // 保險：動畫沒正常結束也不卡死

        if (!ult)
        {
            _nextSlashAt = Time.time + SlashInterval;
            _phase = Phase.Slash;
            if (DebugLog) Debug.Log($"[Elvin] 跳劈：{frames} 幀 @{fps:F0}fps");
        }
        return true;
    }

    void TickSlash(in MonsterContext ctx, MonsterActuator act)
    {
        var self = ctx.Self;
        act.Stop();

        if (!_fired && _anim != null && _anim.OneShotFrame >= SlashReleaseFrame)
        {
            var w = self.WeaponUser;
            if (w != null && _slashTarget != null && w.TryUseAt(_slashTarget, ShockOrigin(_slashTarget)))
            {
                _fired = true;
                SpawnImpactDust();
                if (DebugLog) Debug.Log("[Elvin] 衝擊波（三向）");
            }
            // 冷卻沒好／目標消失：這一刀就不放（下一幀再試，直到動作播完）
        }

        if (SlashEnded())
        {
            if (_anim != null) _anim.CancelOneShot();
            self.SuppressKnockbackInterrupt = false;
            _phase = Phase.Recover;
            _phaseUntil = Time.time + SlashRecoverSeconds;
        }
    }

    bool SlashEnded() => _anim == null || !_anim.OneShotPlaying || _anim.OneShotFinished || Time.time >= _phaseUntil;

    /// <summary>衝擊波出手點：腳底往目標那一側推出身體框，再抬到「衝擊波下緣貼地」的高度（見 ShockForward／ShockLift）。</summary>
    Vector2 ShockOrigin(Transform target)
    {
        Vector2 feet = _self.FeetWorldPos;
        float side = (target != null && target.position.x < feet.x) ? -1f : 1f;
        float halfW = _selfCol != null ? _selfCol.bounds.extents.x : 0.5f;
        return feet + new Vector2(side * (halfW + ShockForward), ShockLift);
    }

    void SpawnImpactDust()
    {
        if (ImpactDustVfxId <= 0) return;
        if (!_vfxLooked) { _vfxLooked = true; _vfx = Object.FindObjectOfType<VfxManager>(); }
        if (_vfx == null) return;
        Vector2 feet = _self.FeetWorldPos;
        float side = (_slashTarget != null && _slashTarget.position.x < feet.x) ? -1f : 1f;
        float halfW = _selfCol != null ? _selfCol.bounds.extents.x : 0.5f;
        _vfx.Spawn(ImpactDustVfxId, feet + new Vector2(side * halfW, 0.3f), 0f, ImpactDustScale);
    }

    // ═════════════════════════ 大絕 ═════════════════════════
    /// <summary>把「已經跨過」的門檻全部標記用掉（同 DukeCharlotteBrain）。開招時呼叫 ⇒ 一次跨好幾格只放一次；收招時呼叫 ⇒ 大絕中跨過的作廢。</summary>
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
        if (!BeginSlash(in ctx, act, target, true)) return false;

        if (DebugLog) Debug.Log($"[Elvin] 大絕：血量 {hp:P0}（門檻 {UltThresholds[_ultIdx]:P0}）");
        SkipCrossedThresholds(hp);
        _ultWave = 0;
        _phase = Phase.Ult;
        return true;
    }

    void TickUlt(in MonsterContext ctx, MonsterActuator act, Transform target)
    {
        var self = ctx.Self;
        act.Stop();
        if (target != null) _slashTarget = target;   // 每一波都朝「當下」的目標（玩家跑位，下一波會追著他）

        if (!_fired && _anim != null && _anim.OneShotFrame >= SlashReleaseFrame)
        {
            var uw = _ultWeapon != null ? _ultWeapon : self.WeaponUser;
            if (uw != null && _slashTarget != null)
            {
                // 方向在放波這一刻才算（TryUseAt 用目標當下的位置）⇒ 每一波都追著玩家現在的位置打
                float rot = UltRotateFor(_ultWave);
                if (uw.TryUseAt(_slashTarget, ShockOrigin(_slashTarget), UltCount, UltSpacingDeg, rot))
                {
                    _fired = true;
                    SpawnImpactDust();
                    if (DebugLog) Debug.Log($"[Elvin] 大絕第 {_ultWave + 1} 波：{UltCount} 道、間隔 {UltSpacingDeg}°、整體轉 {rot:+0;-0}°（有一道正對玩家）");
                }
            }
        }

        if (!SlashEnded()) return;

        _ultWave++;
        if (_ultWave < UltWaves && self.HealthFraction > 0f && _slashTarget != null &&
            BeginSlash(in ctx, act, _slashTarget, true))
            return;   // 下一波（不停頓，連續劈）

        EndUlt(in ctx, act);
    }

    /// <summary>
    /// 第 <paramref name="wave"/> 波（0 起算）整個扇形要轉幾度，才會有一道正對目標：
    /// 偶數道 ⇒ ±半個間隔、單雙波交替；奇數道 ⇒ 0（中軸本來就有一道）。
    /// </summary>
    static float UltRotateFor(int wave)
    {
        if (UltCount % 2 == 1) return 0f;
        float half = UltSpacingDeg * 0.5f;
        return (wave % 2 == 0) ? half : -half;
    }

    void EndUlt(in MonsterContext ctx, MonsterActuator act)
    {
        var self = ctx.Self;
        if (_anim != null) _anim.CancelOneShot();
        self.SuppressKnockbackInterrupt = false;
        act.Stop();
        SkipCrossedThresholds(self.HealthFraction);   // ⭐ 大絕進行中跨過的門檻作廢（同夏綠蒂公爵）
        if (DebugLog)
            Debug.Log($"[Elvin] 大絕結束：血量 {self.HealthFraction:P0}，下一個門檻 " +
                      (_ultIdx < UltThresholds.Length ? UltThresholds[_ultIdx].ToString("P0") : "（沒有了）"));
        _phase = Phase.Recover;
        _phaseUntil = Time.time + UltRecoverSeconds;
        _nextSlashAt = Mathf.Max(_nextSlashAt, Time.time + UltRecoverSeconds + 2f);
    }

    // ═════════════════════════ 閃避 ═════════════════════════
    /// <summary>現在能不能閃：冷卻好了、手上沒有動作、有 dodge 圖、沒被關、還活著。</summary>
    bool CanDodge()
    {
        if (_self == null || _self.IsDead || _self.Caged || _self.IsUntargetable) return false;
        if (Time.time < _nextDodgeAt) return false;
        if (_anim == null || !_anim.Has(MonsterAnimator.State.Dodge)) return false;
        if (_phase == Phase.Chase) return !_melee.IsBusy;
        return _phase == Phase.Recover;
    }

    /// <summary>受擊攔截（雷射、近戰、AOE…第一下打到時）：能閃就閃、這一下作廢。DOT 不觸發。</summary>
    bool OnIncomingDamage(float amount, Vector2 hitDirection)
    {
        if (CombatSystem.CurrentHitIsDot) return false;
        if (!CanDodge()) return false;
        if (DebugLog) Debug.Log($"[Elvin] 閃避（受擊攔截，作廢 {amount:F0} 傷害）");
        BeginDodge();
        return true;
    }

    /// <summary>子彈提前預判：有子彈預計在無敵那段時間抵達 ⇒ 現在就閃。</summary>
    bool TryPredictDodge()
    {
        if (!CanDodge() || _selfCol == null) return false;

        int myLayerBit = 1 << _self.gameObject.layer;
        Vector2 c = _selfCol.bounds.center;
        Vector2 ext = _selfCol.bounds.extents;
        float body = Mathf.Max(ext.x, ext.y) * PredictBodyShrink;

        float tMin = (DodgeInvulnFrom - 1) / Mathf.Max(1f, _self.AnimFPS * DodgeFpsMul);   // 無敵從第 1 幀起 ⇒ 0
        float tMax = tMin + PredictWindow;

        List<BulletInstance> list = BulletInstance.Active;
        for (int i = 0; i < list.Count; i++)
        {
            var b = list[i];
            if (b == null) continue;
            if ((b.CollisionMask & myLayerBit) == 0) continue;   // 打不到我的（怪物自己的子彈、打玩家的）
            Vector2 v = b.Velocity;
            float v2 = v.sqrMagnitude;
            if (v2 < 0.01f) continue;
            Vector2 rel = c - (Vector2)b.transform.position;
            float t = Vector2.Dot(rel, v) / v2;                  // 最接近的時間
            if (t < 0f) continue;                                // 已經飛離我了
            // 已經貼到身邊、正朝我飛（追蹤彈轉彎時直線外插不準，近身這條兜底）
            if (rel.magnitude <= body + b.Radius + PredictNearDistance)
            {
                if (DebugLog) Debug.Log($"[Elvin] 閃避（子彈已貼身，距離 {rel.magnitude:F2}）");
                BeginDodge();
                return true;
            }
            if (t < tMin || t > tMax) continue;
            float miss = (rel - v * t).magnitude;                // 最接近時的距離
            if (miss > body + b.Radius) continue;
            if (DebugLog) Debug.Log($"[Elvin] 閃避（預判子彈，{t:F2}s 後抵達、偏差 {miss:F2}）");
            BeginDodge();
            return true;
        }
        return false;
    }

    void BeginDodge()
    {
        if (_anim == null || !_anim.PlayOneShot(MonsterAnimator.State.Dodge, 1, 0, DodgeFpsMul)) return;
        _nextDodgeAt = Time.time + DodgeCooldown;
        _self.SuppressKnockbackInterrupt = true;
        var act = _self.GetComponent<MonsterActuator>();
        if (act != null) act.Stop();
        float fps = Mathf.Max(1f, _self.AnimFPS * DodgeFpsMul);
        int frames = _anim.FrameCount(MonsterAnimator.State.Dodge);
        _phaseUntil = Time.time + (frames > 0 ? frames / fps : 1.6f) + 0.5f;
        _phase = Phase.Dodge;
        // ⚠ 當下就切成虛體，不能等下一次 Think：受擊攔截是在 TakeDamage 裡觸發的，
        //   緊接著 CombatSystem／PlayerController.HandleBulletHit 就會問「目標閃開了沒」決定子彈要不要穿過去、要不要噴擊中特效。
        if (DodgeInvulnFrom <= 1) SetInvuln(true);
    }

    void TickDodge(MonsterActuator act)
    {
        act.Stop();
        int f = _anim != null ? _anim.OneShotFrame : 0;

        SetInvuln(f >= DodgeInvulnFrom && f <= DodgeInvulnTo);
        SetShadowHidden(f >= ShadowHideFrom && f <= ShadowHideTo);

        if (SlashEnded())   // 同一個判斷：one-shot 播完／被收掉／保險時間到
        {
            if (_anim != null) _anim.CancelOneShot();
            SetInvuln(false);
            SetShadowHidden(false);
            _self.SuppressKnockbackInterrupt = false;
            _phase = Phase.Chase;
        }
    }

    void SetInvuln(bool on)
    {
        if (_invuln == on) return;
        _invuln = on;
        _self.SetUntargetable(on);
    }

    void SetShadowHidden(bool hide)
    {
        if (_shadowHidden == hide) return;
        _shadowHidden = hide;
        if (_shadow != null) _shadow.SetVisible(!hide);
    }

    // ═════════════════════════ 小工具 ═════════════════════════
    /// <summary>掛上大絕專用的第二個 MonsterWeaponUser（同 DukeCharlotteBrain.SetupUltWeapon：重用「不是主武器」的那一個，沒有才新增）。</summary>
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
}
