using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 劇本同伴（<see cref="CompanionService"/>）的通用行為：**象徵性地幫忙打**（作者 2026-10-26）。
///
/// <para><b>設計目標</b>：同伴的戰鬥力要「很弱、但不是零」。敵人本來就不會攻擊同伴（<c>IsCompanion</c>），
/// 所以同伴的表現無足輕重——重點是**看起來在並肩作戰**：偶爾上去打一兩下、打一陣子就回到玩家身邊或發呆一下。</para>
///
/// <para><b>「排班」輪流</b>（作者 2026-10-26 二修：<b>發呆 4：作戰 6，不跟隨玩家</b>——同伴與玩家在同一張地圖裡，專注作戰就好）：
/// 每一段做完就按權重抽下一段（<see cref="PickNext"/>）；附近沒有敵人時全部發呆。跟隨段與牽繩的程式保留但關掉（權重 0／LeashRange 0）。</para>
/// <list type="bullet">
///   <item><b>發呆（Idle）</b>：原地站 <see cref="IdleMin"/>~<see cref="IdleMax"/> 秒。<b>玩家走開也不會打斷</b>（除非超過牽繩距離）。</item>
///   <item><b>跟隨（Follow）</b>：往玩家走 <see cref="FollowMin"/>~<see cref="FollowMax"/> 秒，走到 <see cref="FollowNear"/> 內就站在旁邊等。</item>
///   <item><b>作戰（Engage）</b>：走向最近的敵人、用自己的攻擊方式打 <see cref="EngageMin"/>~<see cref="EngageMax"/> 秒。</item>
///   <item><b>牽繩</b>：離玩家超過 <see cref="LeashRange"/> ⇒ 不管在做什麼都先追回來（強制跟隨，不算在比例裡）。</item>
/// </list>
/// <para>⚠ 第一版是「發呆時玩家一走開就跟上、發呆時間到才檢查要不要打」⇒ 玩家一直在移動時同伴幾乎都在跟隨，
/// 作者回報「比較像寵物，不像一起作戰的夥伴」。所以改成排班：比例由權重決定，不被玩家的移動牽著走。</para>
///
/// <para><b>傷害規則統一</b>：每次命中 <see cref="HitDamage"/>(1)、兩次之間至少 <see cref="HitInterval"/>(2) 秒。
/// 走中央 <c>CombatSystem.Apply</c>（陣營倍率、受擊反應都照既有規則），同伴那一列的 <c>ContactDamage</c> 維持 0（不靠接觸傷害）。</para>
///
/// <para><b>攻擊方式可替換（<see cref="ICompanionAttack"/>）</b>：節奏與傷害規則是共用的，「怎麼打」依 MonsterData 表尾欄
/// <c>CompanionStyle</c> 選：留空＝<see cref="MeleeCompanionAttack"/>（播一次攻擊動作、命中幀打單一目標）；
/// <c>Spin</c>＝<see cref="SpinCompanionAttack"/>（夏綠蒂：原地循環旋轉、每 2 秒對身邊所有敵人各 1 傷害）。
/// 之後有特殊的同伴（例：狂族首領的招式）就新寫一個 ICompanionAttack，在 <see cref="CreateAttack"/> 加一個名字。</para>
///
/// <para>只取代**劇本同伴**的腦；玩家召喚物（御靈水晶等）照舊是 <see cref="AllyBrain"/>。</para>
/// </summary>
public class CompanionBrain : IMonsterBrain
{
    // ── 傷害（所有同伴共用；作者：「每 2 秒才會跳 1 滴血」）──
    public const float HitDamage   = 1f;
    public const float HitInterval = 2f;

    // ── 節奏 ──
    // ⭐ 比例（作者 2026-10-26 二修）：**發呆 4：作戰 6，不跟隨玩家**。
    //   「協同的 NPC 跟玩家都在同一張地圖裡，他應該專注作戰就好」。一修是 3:3:4（含跟隨），像在跟著玩家跑。
    //   跟隨段的程式保留著（權重 0＝不會被抽到），哪天要找回「跟上玩家」只要把 WeightFollow 改回非 0。
    //   附近沒有敵人時作戰權重是 0 ⇒ 全部發呆（站在原地，不會走回玩家身邊）。
    const float WeightIdle = 4f, WeightFollow = 0f, WeightEngage = 6f;
    const float IdleMin   = 2f,   IdleMax   = 3.5f;
    const float FollowMin = 2f,   FollowMax = 3.5f;
    const float EngageMin = 2.5f, EngageMax = 4f;

    // 敵人在這距離內才算「可以去作戰」。不跟隨玩家之後要靠自己找仗打，所以放大到 20
    // （再遠也找不到：ctx.Enemy 的上限是感知半徑 DetectionRange，同伴列填 25）。
    const float AggroRange = 20f;
    const float FollowNear = 2.2f;    // 跟隨：走到離玩家這麼近就站著等（目前不會被抽到）
    // 牽繩：離玩家超過這距離 → 不管在做什麼都先追回來。⭐ 2026-10-26 作者決定同伴不找玩家 ⇒ 關掉（0＝關）。
    //   換圖時 MapManager 會把 PlayerAlly 移到新落點（RepositionPlayerAllies），所以不跟隨也不會被留在上一張圖。
    const float LeashRange = 0f;
    const float FaceDeadzone = 0.15f;  // 與看的對象水平差在這以內就不轉身（正上／正下方時不會左右抖）

    /// <summary>除錯：印出狀態切換與每次造成傷害。</summary>
    public static bool DebugLog = false;

    enum Mode { Idle, Follow, Engage }

    readonly ICompanionAttack _attack;
    Mode _mode = Mode.Idle;
    float _until;
    bool _init;
    Transform _target;

    public CompanionBrain(ICompanionAttack attack) { _attack = attack ?? new MeleeCompanionAttack(); }

    /// <summary>依 CSV <c>CompanionStyle</c> 建攻擊方式。不認得的名字＝近戰（並印一次警告）。</summary>
    public static ICompanionAttack CreateAttack(string style)
    {
        string s = (style ?? "").Trim().ToLowerInvariant();
        switch (s)
        {
            case "":
            case "melee": return new MeleeCompanionAttack();
            case "spin":  return new SpinCompanionAttack();
            default:
                Debug.LogWarning($"[CompanionBrain] 不認得的 CompanionStyle「{style}」，改用近戰。可用值：（空）／Melee／Spin");
                return new MeleeCompanionAttack();
        }
    }

    public void Think(in MonsterContext ctx)
    {
        var self = ctx.Self;
        MonsterActuator act = ctx.Actuator;
        if (self == null || act == null) return;

        if (!_init)
        {
            _init = true;
            // 攻擊姿勢一律由攻擊方式自己控制：不設的話 HandleVisuals 會在「離敵人夠近」時自動循環播攻擊動畫
            // ＝以前「只會轉圈、沒有傷害」的那個樣子。
            self.BrainControlsAttackPose = true;
            Enter(Mode.Idle, Random.Range(IdleMin, IdleMax));
        }

        float now = Time.time;
        Vector2 pos = act.transform.position;
        Transform player = ctx.Player;
        float dp = player != null ? Vector2.Distance(pos, player.position) : 0f;

        ThinkMode(in ctx, self, act, now, pos, player, dp);

        // ⭐ 朝向由同伴自己決定（2026-10-26 作者回報：「跟著玩家走時頭一直看著怪物，像想打又在跟人」）。
        //   MonsterController 預設「有敵人就面向敵人、沒有才面向玩家」——那是給一直在打的怪用的；
        //   同伴大部分時間在跟隨／發呆，照那條規則就會倒退著走回玩家身邊。
        //   交戰＝看目標；跟隨／發呆＝看玩家（走回去的方向就是玩家的方向）。
        Transform look = (_mode == Mode.Engage && _target != null) ? _target : player;
        if (look != null)
        {
            float dx = look.position.x - pos.x;
            if (Mathf.Abs(dx) > FaceDeadzone) self.BrainFacing = dx > 0f ? 1 : -1;
        }
    }

    bool _leashed;   // 目前的跟隨是被牽繩拉回來的（走到玩家身邊才結束，不看時間）

    void ThinkMode(in MonsterContext ctx, MonsterController self, MonsterActuator act, float now, Vector2 pos, Transform player, float dp)
    {
        // ── 牽繩：最高優先（作戰中就先收手；揮到一半也不等）──
        if (LeashRange > 0f && player != null && dp > LeashRange && !_leashed)
        {
            if (_mode == Mode.Engage) EndEngage(self, act);
            _leashed = true;
            Enter(Mode.Follow, 0f);
        }

        switch (_mode)
        {
            case Mode.Engage:
            {
                if (now >= _until)
                {
                    if (_attack.Busy) { act.Stop(); _attack.Tick(self, act, _target, true, now); break; }   // 揮到一半就讓它揮完
                    EndEngage(self, act);
                    PickNext(in ctx, self, act, now, pos);
                    break;
                }

                if (!Alive(_target)) _target = (ctx.Enemy != null && Vector2.Distance(pos, ctx.Enemy.position) <= AggroRange + 2f) ? ctx.Enemy : null;
                if (_target == null && !_attack.Busy)
                {
                    // 打的對象沒了、附近也沒有別的：提早結束這一段，換下一段（不是硬發呆到時間）
                    EndEngage(self, act);
                    PickNext(in ctx, self, act, now, pos);
                    break;
                }

                float d = _target != null ? Vector2.Distance(pos, _target.position) : 0f;
                bool inRange = _target != null && d <= _attack.EngageRange;
                if (_attack.HoldsPosition || inRange) act.Stop();
                else act.MoveTowards(_target.position);
                _attack.Tick(self, act, _target, inRange, now);
                break;
            }

            case Mode.Follow:
                if (player != null && dp > FollowNear) act.MoveTowards(player.position);
                else act.Stop();   // 到玩家身邊了：站著等這一段的時間用完
                if (_leashed) { if (player == null || dp <= FollowNear) { _leashed = false; PickNext(in ctx, self, act, now, pos); } }
                else if (now >= _until) PickNext(in ctx, self, act, now, pos);
                break;

            default: // Idle：玩家走開也不打斷（牽繩在最上面處理）
                act.Stop();
                if (now >= _until) PickNext(in ctx, self, act, now, pos);
                break;
        }
    }

    /// <summary>按權重抽下一段（發呆 3：跟隨 3：作戰 4；附近沒敵人時作戰權重 0）。</summary>
    void PickNext(in MonsterContext ctx, MonsterController self, MonsterActuator act, float now, Vector2 pos)
    {
        Transform e = ctx.Enemy;
        bool canFight = Alive(e) && Vector2.Distance(pos, e.position) <= AggroRange;
        float wE = canFight ? WeightEngage : 0f;
        float r = Random.value * (WeightIdle + WeightFollow + wE);

        if (r < wE)
        {
            _target = e;
            _attack.Begin(self, act, now);
            Enter(Mode.Engage, Random.Range(EngageMin, EngageMax));
        }
        else if (r < wE + WeightFollow) Enter(Mode.Follow, Random.Range(FollowMin, FollowMax));
        else Enter(Mode.Idle, Random.Range(IdleMin, IdleMax));
    }

    void Enter(Mode m, float seconds)
    {
        if (DebugLog && m != _mode) Debug.Log($"[CompanionBrain] {_mode} → {m}（{seconds:F1}s）");
        _mode = m;
        _until = Time.time + seconds;
    }

    void EndEngage(MonsterController self, MonsterActuator act)
    {
        _attack.End(self, act);
        _target = null;
    }

    static bool Alive(Transform t)
    {
        if (t == null) return false;
        var mc = t.GetComponent<MonsterController>();
        return mc != null && !mc.IsDead && !mc.IsUntargetable;
    }

    // ═════════════════════ 傷害（給攻擊方式共用）═════════════════════

    /// <summary>對單一目標造成一次同伴傷害（<see cref="HitDamage"/>）。</summary>
    public static void HitOne(MonsterController self, Transform target)
    {
        if (self == null || !Alive(target)) return;
        Vector2 dir = ((Vector2)target.position - (Vector2)self.transform.position);
        dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.right;
        CombatSystem.Apply(self.gameObject, target.gameObject, HitDamage, dir);
        if (DebugLog) Debug.Log($"[CompanionBrain] {self.MonsterName} 打 {target.name} {HitDamage}");
    }

    /// <summary>
    /// 對 <paramref name="center"/> 半徑 <paramref name="radius"/> 內**所有**敵對的怪各造成一次同伴傷害，回傳打到幾隻。
    /// 距離用「中心到對方碰撞框最近點」（邊緣距離），大怪的身體邊緣進圈就算——與 EnemyContactDamage 同一把尺（F27）。
    /// 「誰是敵人」與 <see cref="MonsterController.FindNearestEnemy"/> 同一套：查 FactionRelations、跳過同伴與虛體。
    /// </summary>
    public static int HitAllAround(MonsterController self, Vector2 center, float radius)
    {
        if (self == null) return 0;
        // 複製一份：傷害可能把怪打死、讓 Active 清單在迭代中被改
        var list = new List<MonsterController>(MonsterController.Active);
        int n = 0;
        foreach (var mc in list)
        {
            if (mc == null || mc == self || mc.IsDead || mc.IsCompanion || mc.IsUntargetable) continue;
            if (!FactionRelations.Hostile(self.Faction, mc.Faction)) continue;
            var col = mc.GetComponent<Collider2D>();
            float d = col != null ? Vector2.Distance(center, col.ClosestPoint(center))
                                  : Vector2.Distance(center, mc.transform.position);
            if (d > radius) continue;
            Vector2 dir = (Vector2)mc.transform.position - center;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.right;
            CombatSystem.Apply(self.gameObject, mc.gameObject, HitDamage, dir);
            n++;
        }
        if (DebugLog && n > 0) Debug.Log($"[CompanionBrain] {self.MonsterName} 範圍 {radius:F1} 打到 {n} 隻，各 {HitDamage}");
        return n;
    }
}

/// <summary>
/// 同伴的「怎麼打」。節奏（何時交戰、打多久）與傷害規則由 <see cref="CompanionBrain"/> 管，這裡只管動作與命中。
/// 每次命中請用 <see cref="CompanionBrain.HitOne"/>／<see cref="CompanionBrain.HitAllAround"/>，
/// 兩次命中至少隔 <see cref="CompanionBrain.HitInterval"/> 秒——同伴的傷害規則只有一份。
/// </summary>
public interface ICompanionAttack
{
    /// <summary>離目標多近才開始打（世界單位，transform 中心距離）。</summary>
    float EngageRange { get; }
    /// <summary>正在演一個不能中斷的動作（例：揮到一半）——這段期間 Brain 不結束交戰（時間到也等它演完）。</summary>
    bool Busy { get; }
    /// <summary>這一刻要站定不動（例：揮刀中、原地旋轉中）。與 Busy 分開：旋轉可以隨時收，但轉的時候不該邊走。</summary>
    bool HoldsPosition { get; }
    void Begin(MonsterController self, MonsterActuator act, float now);
    /// <param name="target">目前的目標（可能為 null：剛被打死、正在找下一個）。</param>
    /// <param name="inRange">是否已經在 <see cref="EngageRange"/> 內。</param>
    void Tick(MonsterController self, MonsterActuator act, Transform target, bool inRange, float now);
    void End(MonsterController self, MonsterActuator act);
}

/// <summary>
/// 近戰（預設）：在攻擊距離內每 <see cref="CompanionBrain.HitInterval"/> 秒揮一下（attack 播一次 one-shot），
/// 命中幀（CSV <c>AttackHitFrame</c>，沒填＝張數 × 0.7）時目標還在附近就打 1。揮的期間站定把動作做完。
/// 沒有 attack 圖的同伴：不演動作、直接結算（傷害一樣，只是看不出揮）。
/// </summary>
public class MeleeCompanionAttack : ICompanionAttack
{
    const float HitFrameFallbackRatio = 0.7f;
    const float ReachSlack = 0.6f;     // 命中幀時目標離開這麼遠以內仍算打到（同伴的傷害無關緊要，寬鬆一點才看得出在打）

    float _reach = 1.4f;
    float _nextSwingAt;
    bool _swinging, _hitDone;
    int _hitFrame;
    float _swingUntil;

    public float EngageRange => _reach;
    public bool Busy => _swinging;
    public bool HoldsPosition => _swinging;

    public void Begin(MonsterController self, MonsterActuator act, float now)
    {
        // 攻擊距離跟著體型：大隻的同伴站遠一點就揮得到（可見身高的一半，至少 1.4）
        _reach = Mathf.Max(1.4f, self.VisibleBodyHeight * 0.5f);
        _nextSwingAt = now + 0.3f;
        _swinging = false;
    }

    public void Tick(MonsterController self, MonsterActuator act, Transform target, bool inRange, float now)
    {
        var anim = self.Anim;
        if (_swinging)
        {
            bool done = anim == null || anim.OneShotFinished || now >= _swingUntil;
            if (!_hitDone && (done || (anim != null && anim.OneShotFrame >= _hitFrame)))
            {
                _hitDone = true;
                if (target != null && Vector2.Distance(self.transform.position, target.position) <= _reach + ReachSlack)
                    CompanionBrain.HitOne(self, target);
            }
            if (done) { if (anim != null) anim.CancelOneShot(); _swinging = false; }
            return;
        }

        if (!inRange || target == null || now < _nextSwingAt) return;
        _nextSwingAt = now + CompanionBrain.HitInterval;

        if (anim != null && anim.PlayOneShot(MonsterAnimator.State.Attack, 1, 0, 1f))
        {
            int total = Mathf.Max(1, anim.FrameCount(MonsterAnimator.State.Attack));
            _hitFrame = self.AttackHitFrame > 0 ? Mathf.Clamp(self.AttackHitFrame, 1, total)
                                                : Mathf.Clamp(Mathf.RoundToInt(total * HitFrameFallbackRatio), 1, total);
            _swingUntil = now + total / Mathf.Max(1f, self.AnimFPS) + 0.5f;
            _swinging = true;
            _hitDone = false;
        }
        else CompanionBrain.HitOne(self, target);   // 沒有 attack 圖：直接結算
    }

    public void End(MonsterController self, MonsterActuator act)
    {
        if (_swinging && self.Anim != null) self.Anim.CancelOneShot();
        _swinging = false;
    }
}

/// <summary>
/// 旋轉（夏綠蒂）：靠近敵人後**原地循環旋轉**（attack 動畫），每 <see cref="CompanionBrain.HitInterval"/> 秒
/// 對身邊半徑內**所有**敵人各打 1。目標跑出範圍就停轉、走過去再轉。
/// 半徑＝<see cref="BaseRadius"/> × 體型（F29：範圍技的半徑不能是裸的世界單位）。
/// </summary>
public class SpinCompanionAttack : ICompanionAttack
{
    const float BaseRadius = 1.2f;     // 體型 1 時的半徑；夏綠蒂 Scale 2 ⇒ 2.4（裙擺轉開來大概就這麼寬）
    const float FirstTickDelay = 0.6f; // 開始轉之後多久第一次結算（轉起來了才痛，不是一碰就痛）

    float _radius = BaseRadius;
    float _nextTickAt;
    bool _spinning;

    // 走到目標在半徑的六成內才開始轉：轉起來之後目標稍微移動也還在圈內
    public float EngageRange => _radius * 0.6f;
    public bool Busy => false;                 // 旋轉隨時可以收（交戰時間到就停）
    public bool HoldsPosition => _spinning;    // 但轉的時候站定，不要邊轉邊走（那是大絕的樣子）

    public void Begin(MonsterController self, MonsterActuator act, float now)
    {
        _radius = self.ScaledRadius(BaseRadius);
        _spinning = false;
    }

    public void Tick(MonsterController self, MonsterActuator act, Transform target, bool inRange, float now)
    {
        if (inRange && !_spinning)
        {
            _spinning = true;
            self.BrainPoseActive = true;                    // 旋轉＝attack 循環播（HandleVisuals 照 BrainPose 播）
            self.BrainPose = MonsterAnimator.State.Attack;
            _nextTickAt = Mathf.Max(_nextTickAt, now + FirstTickDelay);
        }
        else if (!inRange && _spinning && target != null &&
                 Vector2.Distance(self.transform.position, target.position) > _radius)
        {
            // 目標跑出整個圈才停轉去追（在「開始轉的距離」與「整圈」之間就繼續轉，免得一直開開關關）
            _spinning = false;
            self.BrainPoseActive = false;
        }

        if (_spinning && now >= _nextTickAt)
        {
            _nextTickAt = now + CompanionBrain.HitInterval;
            CompanionBrain.HitAllAround(self, self.transform.position, _radius);
        }
    }

    public void End(MonsterController self, MonsterActuator act)
    {
        _spinning = false;
        self.BrainPoseActive = false;
    }
}
