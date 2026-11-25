using UnityEngine;

/// <summary>
/// **固定砲台**（BrainType=<c>Turret</c>，2026-11-24，墨子的機關塔）：玩家召喚出來的友軍，**不會移動**。
///  ‧ 出生：從土裡升起（<see cref="GroundEmerge"/>），升起完成才開始工作。
///  ‧ 工作：射程（CSV `ShootRange`，留空＝7）內有敵怪、武器冷卻好了 → 起播 attack 動畫，
///    到放彈幀（CSV `ReleaseFrame`，砲管縮回那一幀）才用 <see cref="MonsterWeaponUser.TryUse"/> 開火。
///    射什麼、幾發、幾個方向全看 MonsterData.Weapon 指的那把武器（機關塔＝8 方向放射）。
///  ‧ **無敵、不會被鎖定**（2026-11-25 作者：怪多時塔還沒開火就被拆掉）：一出生就 <see cref="MonsterController.SetUntargetable"/>
///    ——搬到 Mist 層（敵方子彈／近戰都穿過去）＋ TakeDamage 擋掉；敵怪找目標的 <see cref="MonsterController.FindNearestEnemy"/>
///    也跳過虛體，不會圍著塔打。
///  ‧ 收工：存在 `LifeSeconds` 秒（CSV 表尾欄，留空＝10）→ 沉回土裡 → 銷毀（不掉寶、不播死亡特效）。
/// 放彈的時機算法照抄 <see cref="ArcherBrain"/>（幀號即事件，改 AnimFPS 會自動跟著對）。
///
/// ⚠ MonsterSpawner 對 PlayerAlly 陣營一律換成 AllyBrain（跟班），BrainType=Turret 例外（見 MonsterSpawner.SpawnMonster）。
/// </summary>
public class TurretBrain : IMonsterBrain
{
    const float DefaultRange = 7f;
    const float DefaultLife = 10f;
    const int ReleaseFrameFallback = 7;
    const float FallbackAnimFps = 14f;
    const float FollowThroughSeconds = 0.6f;

    enum Phase { Spawn, Idle, Draw, Sink }
    Phase _phase = Phase.Spawn;
    float _bornAt = -1f;
    float _releaseAt;
    float _attackAnimEnd;
    GroundEmerge _emerge;
    Transform _target;

    public void Think(in MonsterContext ctx)
    {
        var self = ctx.Self;
        var act = ctx.Actuator;
        if (self == null || act == null) return;
        act.Stop();

        if (_bornAt < 0f)
        {
            _bornAt = Time.time;
            self.SetUntargetable(true);   // 無敵＋不被鎖定（見檔頭）
            _emerge = self.GetComponent<GroundEmerge>();
            if (_emerge == null) _emerge = self.gameObject.AddComponent<GroundEmerge>();
            _emerge.BeginRise();
        }

        float life = self.LifeSeconds > 0.01f ? self.LifeSeconds : DefaultLife;
        if (_phase != Phase.Sink && Time.time - _bornAt >= life)
        {
            _phase = Phase.Sink;
            var go = self.gameObject;
            _emerge.BeginSink(() => { if (go != null) Object.Destroy(go); });
            return;
        }

        switch (_phase)
        {
            case Phase.Spawn:
                if (_emerge.IsUp) _phase = Phase.Idle;
                return;

            case Phase.Idle:
            {
                KeepAttackPose(self);
                var weapon = self.WeaponUser;
                if (weapon == null || !weapon.Ready) return;
                Transform t = ctx.Enemy;
                if (t == null) return;
                float range = self.ShootRange > 0.01f ? self.ShootRange : DefaultRange;
                if (Vector2.Distance(self.BodyCenterWorldPos, t.position) > range) return;
                _target = t;
                BeginDraw(self);
                return;
            }

            case Phase.Draw:
                KeepAttackPose(self);
                if (Time.time < _releaseAt) return;
                // 放彈那一刻目標可能已經死了 → 換最近的；都沒有就這一發作罷（冷卻不扣）
                Transform target = (_target != null && _target.gameObject.activeInHierarchy) ? _target : ctx.Enemy;
                if (target != null && self.WeaponUser != null) self.WeaponUser.TryUse(target);
                _target = null;
                _phase = Phase.Idle;
                return;

            case Phase.Sink:
                return;
        }
    }

    void BeginDraw(MonsterController self)
    {
        float fps = self.AnimFPS > 0.01f ? self.AnimFPS : FallbackAnimFps;
        int releaseFrame = self.ReleaseFrame > 0 ? self.ReleaseFrame : ReleaseFrameFallback;
        int frames = self.Anim != null ? self.Anim.FrameCount(MonsterAnimator.State.Attack) : 0;
        if (frames > 0) releaseFrame = Mathf.Clamp(releaseFrame, 1, frames);
        _releaseAt = Time.time + Mathf.Max(0f, (releaseFrame - 1) / fps);
        float animLen = frames > 0 ? frames / fps : 0f;
        _attackAnimEnd = Mathf.Max(_releaseAt + FollowThroughSeconds, Time.time + animLen);
        _phase = Phase.Draw;
        self.NotifySkillCast();   // 讓 attack 動畫現在就開始播
    }

    /// <summary>讓 attack 動畫撐到整套播完（同 ArcherBrain.KeepAttackPose：只在再續一次不會超過結束時間時才續）。</summary>
    void KeepAttackPose(MonsterController self)
    {
        if (_attackAnimEnd <= 0f) return;
        if (Time.time >= _attackAnimEnd) { _attackAnimEnd = 0f; return; }
        if (Time.time + self.SkillCastAnimSeconds <= _attackAnimEnd) self.NotifySkillCast();
    }
}
