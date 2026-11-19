using System.Collections.Generic;
using UnityEngine;
using Dipan.Vfx;
using Sorrows.Ballistics;

/// <summary>
/// **靈獸（<see cref="WeaponMode.Spirit"/>）的遊戲端**（2026-11-19；說明見 readme/RECIPE_DESCRIBE.md〈Spirit 靈獸〉、外觀見 readme/DIPAN_VFX.md §4e）。
/// 一次施放放出 1＋SplitLv 隻靈獸（扇形散開出發，朝瞄準方向），每隻在畫面裡亂游 LifeTime 秒、不碰牆。
///
/// <para>游法與外觀在 DipanVfx 的 <see cref="SpiritBeast"/>；這裡掛在同一個物件上，只管：
/// ① 有追蹤（HomingLv&gt;0）時每 0.25 秒挑一次畫面裡最近的活怪當目標；
/// ② **整條身體的傷害判定**：每 0.1 秒沿身體每隔約 0.5 取一個點、以該處身體半寬做圓形判定，
///    碰到的怪「同一隻每 DotInterval 秒最多被咬一次」，每次吃武器 Damage（＋HitEffectID 特效）。
/// 施放者消失（死亡／換場景）⇒ 靈獸淡出、不再傷人。</para>
///
/// <para>目前只有玩家放（PlayerController.Shoot）；要給怪物用再把目標層與畫面範圍改成參數即可。</para>
/// </summary>
[RequireComponent(typeof(SpiritBeast))]
public class SpiritStrike : MonoBehaviour
{
    const float HitTick = 0.1f;          // 多久判定一次
    const float SampleSpacing = 0.5f;    // 沿身體每隔多遠取一個判定點（×大小）
    const float RetargetInterval = 0.25f;

    public struct Context
    {
        /// <summary>施放者（傷害來源）；被 Destroy 時靈獸淡出。</summary>
        public GameObject Owner;
        /// <summary>出生點（玩家身體中心）。</summary>
        public Vector2 Origin;
        /// <summary>瞄準方向（多隻時以它為中心扇形散開）。</summary>
        public Vector2 AimDir;
        /// <summary>咬得到的層（玩家放＝怪物層）。</summary>
        public LayerMask TargetLayers;
    }

    WeaponData _weapon;
    Context _ctx;
    SpiritBeast _beast;
    float _tick, _retarget;
    Transform _target;
    readonly Dictionary<int, float> _lastHit = new Dictionary<int, float>();
    readonly HashSet<int> _seen = new HashSet<int>();
    static readonly Collider2D[] _buf = new Collider2D[32];
    static VfxManager _vfx;

    /// <summary>施放；回傳放出幾隻（0＝失敗：沒有外觀、表裡沒這列）。</summary>
    public static int Cast(WeaponData weapon, in Context ctx)
    {
        if (weapon == null || weapon.Recipe == null || ctx.Owner == null) return 0;
        if (weapon.SpiritFxID <= 0)
        {
            Debug.LogWarning($"[Spirit] {weapon.Name}：Mode=Spirit 但武器表 SpiritFxID 沒填。");
            return 0;
        }
        ProjectileData pd = weapon.Recipe.Data;
        int count = Mathf.Max(1, pd != null ? pd.SplitCount : 1);
        float spread = pd != null ? pd.SpreadAngle : 0f;
        float speed = pd != null && pd.Speed > 0f ? pd.Speed : RecipeEntry.SpiritDefaultSpeed;
        float life = pd != null && pd.LifeTime > 0f ? pd.LifeTime : RecipeEntry.SpiritDefaultLifeTime;
        float turn = pd != null && pd.HasHoming ? pd.HomingTurnSpeed : 0f;
        float scale = Mathf.Max(0.05f, weapon.BulletScale);
        Vector2 aim = ctx.AimDir.sqrMagnitude > 1e-6f ? ctx.AimDir.normalized : Vector2.right;
        float baseAng = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

        int made = 0;
        for (int i = 0; i < count; i++)
        {
            float ang = baseAng;
            if (count > 1) ang += -spread * 0.5f + spread * i / (count - 1);
            var beast = SpiritBeast.Spawn(weapon.SpiritFxID, ctx.Origin, ang * Mathf.Deg2Rad, scale, speed, life, Mathf.Max(1f, turn));
            if (beast == null) continue;
            var s = beast.gameObject.AddComponent<SpiritStrike>();
            s._weapon = weapon;
            s._ctx = ctx;
            s._beast = beast;
            s._retarget = Random.value * RetargetInterval;   // 多隻別擠在同一幀找目標
            if (turn > 0f) beast.Target = s.TargetPos;
            made++;
        }
        return made;
    }

    Vector2? TargetPos(Vector2 head)
    {
        if (_target == null) return null;
        var mc = _target.GetComponentInParent<MonsterController>();
        if (mc != null && mc.IsDead) { _target = null; return null; }
        return (Vector2)_target.position;
    }

    void Update()
    {
        if (_beast == null) { Destroy(this); return; }
        if (_ctx.Owner == null) { _beast.Stop(); return; }   // 施放者沒了：淡出、不再傷人
        if (!_beast.IsActive) return;

        float dt = Time.deltaTime;
        if (_beast.Target != null)
        {
            _retarget -= dt;
            if (_retarget <= 0f) { _retarget = RetargetInterval; _target = FindTarget(_beast.Motion.Head); }
        }

        _tick += dt;
        if (_tick < HitTick) return;
        _tick = 0f;
        HitAlongBody();
    }

    /// <summary>畫面裡（主相機可見範圍）離 <paramref name="from"/> 最近的活怪。</summary>
    Transform FindTarget(Vector2 from)
    {
        var cam = Camera.main;
        float r = cam != null && cam.orthographic ? cam.orthographicSize * Mathf.Max(1f, cam.aspect) * 1.2f : 15f;
        Rect view = default;
        if (cam != null && cam.orthographic)
        {
            float h = cam.orthographicSize * 2f, w = h * cam.aspect;
            view = new Rect(cam.transform.position.x - w * 0.5f, cam.transform.position.y - h * 0.5f, w, h);
        }
        Collider2D[] cols = Physics2D.OverlapCircleAll(from, r, _ctx.TargetLayers);
        float best = float.MaxValue; Transform bestT = null;
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null) continue;
            var mc = c.GetComponentInParent<MonsterController>();
            if (mc == null || mc.IsDead) continue;
            Vector2 p = mc.transform.position;
            if (view.width > 0f && !view.Contains(p)) continue;
            float d = (p - from).sqrMagnitude;
            if (d < best) { best = d; bestT = mc.transform; }
        }
        return bestT;
    }

    void HitAlongBody()
    {
        var pts = _beast.BodyPoints;
        var half = _beast.HalfWidths;
        if (pts.Count < 2 || half.Count != pts.Count) return;
        float now = Time.time;
        float interval = _weapon.Recipe != null && _weapon.Recipe.Data != null ? Mathf.Max(0.05f, _weapon.Recipe.Data.DotInterval) : 0.5f;
        float spacing = SampleSpacing * _beast.Scale;
        _seen.Clear();

        float acc = spacing;   // 頭（第 0 點）一定判定
        for (int i = 0; i < pts.Count; i++)
        {
            if (i > 0) acc += (pts[i] - pts[i - 1]).magnitude;
            if (acc < spacing && i != pts.Count - 1) continue;
            acc = 0f;
            float r = Mathf.Max(0.05f, half[i] * (i == 0 ? 1.3f : 1f));   // 頭部判定稍大（頭圖比脖子寬）
            int n = Physics2D.OverlapCircleNonAlloc(pts[i], r, _buf, _ctx.TargetLayers);
            for (int k = 0; k < n; k++)
            {
                var col = _buf[k];
                if (col == null) continue;
                GameObject target = col.gameObject;
                if (target == _ctx.Owner) continue;
                int id = target.GetInstanceID();
                if (!_seen.Add(id)) continue;   // 同一次判定同一隻只算一次
                if (_lastHit.TryGetValue(id, out float last) && now - last < interval) continue;
                _lastHit[id] = now;
                Vector2 dir = (Vector2)target.transform.position - pts[i];
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.up;
                CombatSystem.Apply(_ctx.Owner, target, GmWeapons.DamageAgainst(_weapon, target), dir);
                if (_weapon.HitEffectID > 0)
                {
                    if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();
                    if (_vfx != null) _vfx.Spawn(_weapon.HitEffectID, target.transform.position, 0f, 1f);
                }
            }
        }
    }
}
