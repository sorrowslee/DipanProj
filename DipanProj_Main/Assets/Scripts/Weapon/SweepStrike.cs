using System;
using System.Collections.Generic;
using UnityEngine;
using Sorrows.Ballistics;

/// <summary>
/// **掃射（<see cref="WeaponMode.Sweep"/>）的執行元件**——巨神兵式光束：
/// 施放者頭頂上方一個源點，光束先劈到自己腳底，再沿地面**朝瞄準方向**一路掃出 Range 的長度（Range ≤ 0 才是掃到瞄準點為止）；掃過的線上每隔
/// <see cref="RecipeEntry.ExplodeSpacing"/> 排一顆爆炸，在光束經過後 <see cref="RecipeEntry.ExplodeDelay"/> 秒炸開。
///
/// <para><b>光束本身不傷人</b>，傷害全在爆炸：每顆爆炸對半徑內每個目標各結算一次武器 Damage
/// （同一隻怪被好幾顆炸到就吃好幾次——作者 2026-11-11 拍板）。每條掃描路徑從腳底朝目標檢查牆，撞牆就停在牆前。</para>
///
/// <para><b>擁有者無關</b>：玩家與怪物都呼叫 <see cref="Cast"/>（照 <see cref="WeaponCastService"/> 的原則：
/// 幾何與排程在這裡，瞄準點、耗魔、冷卻留在呼叫端）。回傳值＝這一發從現在到最後一顆爆炸播完要幾秒，
/// 呼叫端把它加進冷卻，就是「整發播完之前不能再發」的發射鎖。</para>
///
/// <para>每次施放生一個 GameObject（掛本元件）自己跑完自己 Destroy；不需要場景裡預先放任何東西。
/// 擁有者中途死亡／被移除 ⇒ 光束立刻淡出、還沒炸的爆炸全部取消（傷害來源不能是一個已經不存在的物件）。</para>
///
/// <para>視覺借用 <see cref="BallisticsEngine.SpawnChainVisual"/> 的靜態折線光束：每幀用新的兩點折線重餵
/// （<c>SetStaticPath</c> 會順便把淡出壽命重置成滿），停止重餵後它自己在 <see cref="BeamFadeSeconds"/> 秒內淡出自毀。
/// 所以不必改 BallisticsSystem。</para>
/// </summary>
public class SweepStrike : MonoBehaviour
{
    /// <summary>光束從源點「劈到腳底」要幾秒（之後才開始沿地面掃）。</summary>
    public const float DropSeconds = 0.1f;
    /// <summary>掃描結束（或擁有者消失）後光束淡出秒數。</summary>
    public const float BeamFadeSeconds = 0.15f;
    /// <summary>爆炸特效查不到長度時，當成播多久（算發射鎖用）。</summary>
    public const float FallbackExplodeSeconds = 0.4f;
    /// <summary>撞牆時停在牆前多遠（免得爆炸中心陷進牆裡）。</summary>
    const float WallBackoff = 0.05f;
    /// <summary>一發最多幾顆爆炸（道數 × 每道顆數）；防止表填錯（間距 0.1、射程 200、10 道）把遊戲卡死。</summary>
    public const int MaxExplosionsPerCast = 256;

    /// <summary>一次施放所需的「誰、從哪、往哪、打得到誰」。</summary>
    public struct Context
    {
        /// <summary>施放者。傷害來源；死掉／被移除時整發中止。</summary>
        public GameObject Owner;
        /// <summary>施放者腳底（世界座標）。掃描路徑的起點，施放當下鎖定。</summary>
        public Vector2 Feet;
        /// <summary>施放者可見身高（世界單位）。源點高度＝腳底＋身高×<see cref="RecipeEntry.SweepOriginHeight"/>。</summary>
        public float BodyHeight;
        /// <summary>每幀取施放者目前腳底（讓光束源點跟著人走）；null＝源點固定在施放當下的位置。</summary>
        public Func<Vector2> LiveFeet;
        /// <summary>瞄準點（玩家＝滑鼠、怪物＝目標位置）。</summary>
        public Vector2 AimPoint;
        /// <summary>擁有者體型，只乘在光束粗細上（爆炸大小看 BulletScale）。</summary>
        public float OwnerScale;
        /// <summary>爆炸打得到的層（玩家放＝怪物層；怪物放＝玩家層）。</summary>
        public LayerMask TargetLayers;
        /// <summary>環境層：擋光束的牆，也是可破壞地上物所在的層（爆炸會一起炸）。</summary>
        public LayerMask EnvLayer;
    }

    struct Path
    {
        public Vector2 Dir;
        public float Length;
        public LaserBeam Beam;
    }

    struct Pending
    {
        public float At;      // 從施放算起第幾秒炸
        public Vector2 Pos;
    }

    WeaponData _weapon;
    Context _ctx;
    Path[] _paths;
    readonly List<Pending> _pending = new List<Pending>();
    readonly List<Vector2> _pts = new List<Vector2>(2) { Vector2.zero, Vector2.zero };
    float _t;
    float _sweepSeconds;   // 最長那一道掃完的時間（不含落下）
    bool _beamsDone;

    static VfxManager _vfx;

    // ── 源點光球（2026-11-11 作者要求：光束像是從一顆紅色能量球射出來）──
    /// <summary>光球直徑＝施放者可見身高 × 這個比例（≈ 一顆頭的大小）。</summary>
    public const float OrbDiameterRatio = 0.16f;
    /// <summary>光球核心（偏白的亮芯）佔光球直徑的比例。</summary>
    const float OrbCoreRatio = 0.5f;
    /// <summary>光球畫在光束之上（LaserBeam 光束本體 22000、砲口光暈 22005）。</summary>
    const int OrbSortingOrder = 22006;
    /// <summary>光球每秒脈動幾次、幅度多少（純視覺）。</summary>
    const float OrbPulseHz = 6f, OrbPulseAmount = 0.08f;
    static Sprite _orbSprite;   // 程式生成的圓形柔光貼圖（全場共用；被銷毀時 == null 會成立而重建，見 PROBLEMS I8）
    Transform _orb;
    SpriteRenderer _orbSr, _orbCoreSr;
    Color _orbColor, _orbCoreColor;
    float _orbDiameter, _orbFadeT = -1f;
    readonly List<Material> _orbMats = new List<Material>(2);   // 每次施放 new 的材質，OnDestroy 收掉（不收會每發漏兩份）

    /// <summary>
    /// 施放一發掃射。回傳**從現在到整發播完的秒數**（最後一顆爆炸的特效也播完；目前只供參考）；施放失敗回 -1。
    /// 冷卻＝配方 FireInterval，**從施放那一刻算**、不等整發播完（2026-11-11 作者改：要讓疾發珠有感、冷卻可以很短），
    /// 所以上一發還在掃時下一發就可以出去，好幾發會疊在一起。
    /// </summary>
    public static float Cast(WeaponData weapon, in Context ctx)
    {
        if (weapon == null || weapon.Recipe == null || ctx.Owner == null) return -1f;
        RecipeEntry r = weapon.Recipe;
        ProjectileData pd = r.Data;

        Vector2 axis = ctx.AimPoint - ctx.Feet;
        float aimDist = axis.magnitude;
        float baseAngle = aimDist > 0.0001f ? Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg : 0f;
        if (aimDist < 0.0001f) aimDist = 0.0001f;
        // 掃描長度：Range > 0 ⇒ 朝瞄準方向**固定掃這麼遠**（不管滑鼠點多近，2026-11-11 作者：只掃到滑鼠太短、不好用；
        // 遠射珠加的就是這一欄）。Range ≤ 0（填 -1）⇒ 舊行為：只掃到瞄準點為止。
        float dist = (pd != null && pd.BeamRange > 0f) ? pd.BeamRange : aimDist;

        int count = Mathf.Max(1, pd != null ? pd.SplitCount : 1);
        float spread = pd != null ? pd.SpreadAngle : 0f;
        float speed = Mathf.Max(0.5f, r.SweepSpeed);
        float spacing = Mathf.Max(0.1f, r.ExplodeSpacing);
        float delay = Mathf.Max(0f, r.ExplodeDelay);

        var go = new GameObject("SweepStrike_" + weapon.Name);
        var s = go.AddComponent<SweepStrike>();
        s._weapon = weapon;
        s._ctx = ctx;
        s._paths = new Path[count];

        int budget = MaxExplosionsPerCast;
        float lastBoom = 0f;
        for (int i = 0; i < count; i++)
        {
            float ang = baseAngle;
            if (count > 1) ang = baseAngle - spread * 0.5f + spread * ((float)i / (count - 1));
            float rad = ang * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            // 牆：從腳底朝目標打一條細射線，撞到就停在牆前。
            // （queriesStartInColliders=false：腳底若已經貼在牆裡會整片被忽略——那種情況等於沒有牆，可接受。）
            float len = dist;
            // 配方 BlockedByEnvironment=0 ⇒ 不檢查牆，直接掃過不可走區域（路西法之怒，2026-11-11 作者：被牆擋住很怪）。
            if (r.BlockedByEnvironment && ctx.EnvLayer.value != 0 && !float.IsInfinity(len))
            {
                RaycastHit2D wall = Physics2D.Raycast(ctx.Feet, dir, len, ctx.EnvLayer);
                if (wall.collider != null) len = Mathf.Max(0f, wall.distance - WallBackoff);
            }

            s._paths[i] = new Path { Dir = dir, Length = len };
            s._sweepSeconds = Mathf.Max(s._sweepSeconds, len / speed);

            // 沿線排爆炸：spacing、2×spacing…；整條比間距短時至少在終點炸一顆。
            if (len < spacing)
            {
                if (budget-- > 0) s.AddExplosion(ctx.Feet + dir * len, DropSeconds + len / speed + delay, ref lastBoom);
            }
            else
            {
                for (float d = spacing; d <= len + 0.001f && budget > 0; d += spacing, budget--)
                    s.AddExplosion(ctx.Feet + dir * d, DropSeconds + d / speed + delay, ref lastBoom);
            }
        }
        if (budget <= 0)
            Debug.LogWarning($"[Sweep] {weapon.Name}：一發超過 {MaxExplosionsPerCast} 顆爆炸，後面的省略（檢查 ExplodeSpacing／Range／SpreadCount）。");

        // 光束視覺：每一道一條靜態折線（源點 → 落點），之後每幀重餵。
        float width = weapon.BeamWidth * (ctx.OwnerScale > 0.0001f ? ctx.OwnerScale : 1f);
        Vector2 src = s.Source();
        s.CreateOrb(src, weapon.BeamColor);
        s._pts[0] = src; s._pts[1] = src;
        for (int i = 0; i < count; i++)
            s._paths[i].Beam = BallisticsEngine.SpawnChainVisual(s._pts, weapon.BeamStyle, weapon.BeamColor,
                width, weapon.BeamMuzzleSprite, weapon.BeamImpactSprite, BeamFadeSeconds);

        float beamEnd = DropSeconds + s._sweepSeconds + BeamFadeSeconds;
        float boomEnd = lastBoom > 0f ? lastBoom + ExplodeSeconds(weapon.HitEffectID) : 0f;
        return Mathf.Max(beamEnd, boomEnd);
    }

    void AddExplosion(Vector2 pos, float at, ref float lastBoom)
    {
        _pending.Add(new Pending { At = at, Pos = pos });
        if (at > lastBoom) lastBoom = at;
    }

    Vector2 Source()
    {
        Vector2 feet = (_ctx.LiveFeet != null && _ctx.Owner != null) ? _ctx.LiveFeet() : _ctx.Feet;
        return feet + Vector2.up * (_ctx.BodyHeight * _weapon.Recipe.SweepOriginHeight);
    }

    void Update()
    {
        // 擁有者沒了（死亡／換場景被清掉）⇒ 光束停止重餵（自己淡出）、剩下的爆炸取消。
        // ⚠ `_ctx.Owner == null` 走 UnityEngine.Object 覆寫的 ==，被 Destroy 的物件才判得出來（見 PROBLEMS F26）。
        if (_ctx.Owner == null)
        {
            Destroy(gameObject);
            return;
        }

        _t += Time.deltaTime;

        if (!_beamsDone)
        {
            Vector2 src = Source();
            float speed = Mathf.Max(0.5f, _weapon.Recipe.SweepSpeed);
            for (int i = 0; i < _paths.Length; i++)
            {
                if (_paths[i].Beam == null) continue;
                Vector2 tip;
                if (_t < DropSeconds)
                    tip = Vector2.Lerp(src, _ctx.Feet, _t / DropSeconds);   // 先從源點劈到腳底
                else
                    tip = _ctx.Feet + _paths[i].Dir * Mathf.Min(_paths[i].Length, (_t - DropSeconds) * speed);
                _pts[0] = src; _pts[1] = tip;
                _paths[i].Beam.SetStaticPath(_pts, BeamFadeSeconds);   // 重設壽命＝持續亮著
            }
            UpdateOrb(src, 1f);
            if (_t >= DropSeconds + _sweepSeconds) { _beamsDone = true; _orbFadeT = 0f; }   // 不再重餵 ⇒ 光束自己淡出自毀
        }

        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].At > _t) continue;
            Explode(_pending[i].Pos);
            _pending.RemoveAt(i);
        }

        // 光球跟光束同一個節奏淡出（BeamFadeSeconds），淡完收掉；整個物件還在等爆炸也沒關係。
        if (_orbFadeT >= 0f && _orb != null)
        {
            _orbFadeT += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(_orbFadeT / BeamFadeSeconds);
            if (k <= 0f) { Destroy(_orb.gameObject); _orb = null; }
            else UpdateOrb(Source(), k);
        }

        if (_beamsDone && _pending.Count == 0) Destroy(gameObject);
    }

    // ── 源點光球 ──
    void CreateOrb(Vector2 at, Color beamColor)
    {
        if (_orbSprite == null) _orbSprite = BuildOrbSprite();
        _orbColor = beamColor;
        _orbCoreColor = Color.Lerp(beamColor, Color.white, 0.6f);
        _orbDiameter = Mathf.Max(0.05f, _ctx.BodyHeight * OrbDiameterRatio);

        var go = new GameObject("SweepOrb");
        go.transform.SetParent(transform, false);
        _orb = go.transform;
        _orbSr = MakeOrbLayer(go, "Glow", 1.6f, OrbSortingOrder);   // 外圈柔光比球本體大，邊緣淡掉後視覺大小≈一顆頭
        _orbCoreSr = MakeOrbLayer(go, "Core", OrbCoreRatio, OrbSortingOrder + 1);
        UpdateOrb(at, 1f);
    }

    SpriteRenderer MakeOrbLayer(GameObject parent, string name, float sizeRatio, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _orbSprite;
        // 加色光暈材質（同 LaserBeam 的砲口光暈）；找不到 shader 就用預設 sprite 材質，至少看得到一顆球。
        Shader sh = Shader.Find("Custom/AdditiveGlow");
        if (sh != null)
        {
            var m = new Material(sh) { hideFlags = HideFlags.DontSave };
            _orbMats.Add(m);
            sr.material = m;
        }
        sr.sortingOrder = order;
        float unit = _orbSprite.bounds.size.x;
        float sc = unit > 0.0001f ? sizeRatio / unit : sizeRatio;
        go.transform.localScale = new Vector3(sc, sc, 1f);
        return sr;
    }

    void UpdateOrb(Vector2 at, float alpha)
    {
        if (_orb == null) return;
        _orb.position = new Vector3(at.x, at.y, 0f);
        float pulse = 1f + OrbPulseAmount * Mathf.Sin(_t * OrbPulseHz * Mathf.PI * 2f);
        float d = _orbDiameter * pulse;
        _orb.localScale = new Vector3(d, d, 1f);
        // 加色混合：顏色往黑收＝變透明，所以淡出同時乘在 rgb 與 a 上。
        if (_orbSr != null) _orbSr.color = _orbColor * alpha;
        if (_orbCoreSr != null) _orbCoreSr.color = _orbCoreColor * alpha;
    }

    void OnDestroy()
    {
        for (int i = 0; i < _orbMats.Count; i++) if (_orbMats[i] != null) Destroy(_orbMats[i]);
        _orbMats.Clear();
    }

    /// <summary>
    /// 64×64 圓形柔光（世界寬 1 單位）：中心最亮、往外連續淡到 0，沒有硬邊。
    /// ⚠ **亮度要寫在 RGB 裡（預乘）**：<c>Custom/AdditiveGlow</c> 只讀貼圖的 rgb、完全不看 alpha
    /// （<c>col = tex.rgb × 頂點色</c>）。第一版 rgb 全白、只在 alpha 做圓 ⇒ 畫出來是一塊方形（作者 2026-11-11 回報）。
    /// alpha 也照寫一份，萬一退回 Sprites/Default（找不到 shader）時一樣是圓的。
    /// </summary>
    static Sprite BuildOrbSprite()
    {
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[N * N];
        float c = (N - 1) * 0.5f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;   // 0 中心、1 邊
                float t = Mathf.Clamp01(1f - r * r);
                float v = t * t;                                                    // (1−r²)²：中心飽滿、邊緣柔、r=1 剛好 0
                byte b = (byte)Mathf.RoundToInt(v * 255f);
                px[y * N + x] = new Color32(b, b, b, b);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        var sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N, 0, SpriteMeshType.FullRect);
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    void Explode(Vector2 at)
    {
        float scale = _weapon.BulletScale;   // 須彌珠：半徑與特效一起放大
        if (_weapon.HitEffectID > 0)
        {
            if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();
            if (_vfx != null) _vfx.Spawn(_weapon.HitEffectID, at, 0f, scale);
        }

        if (_weapon.Damage <= 0f) return;
        float radius = Mathf.Max(0.05f, _weapon.Recipe.AreaRadius) * scale;
        Collider2D[] hits = Physics2D.OverlapCircleAll(at, radius, _ctx.TargetLayers | _ctx.EnvLayer);
        var seen = new HashSet<int>();   // 同一顆爆炸對同一個目標只算一次（一隻怪可能有好幾個碰撞框）
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;
            GameObject target = hits[i].gameObject;
            if (target == _ctx.Owner) continue;
            if (!seen.Add(target.GetInstanceID())) continue;
            Vector2 dir = (Vector2)target.transform.position - at;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
            // 牆沒有 IDamageable，CombatSystem 會自動略過；陣營判斷也交給它（FactionRelations）。
            CombatSystem.Apply(_ctx.Owner, target, GmWeapons.DamageAgainst(_weapon, target), dir);
        }
    }

    /// <summary>爆炸特效播一次要幾秒（序列圖張數 ÷ 幀率；查不到用 <see cref="FallbackExplodeSeconds"/>）。</summary>
    static float ExplodeSeconds(int vfxId)
    {
        if (vfxId <= 0) return 0f;
        if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();
        VfxData v = _vfx != null ? _vfx.GetEffect(vfxId) : null;
        if (v == null) return FallbackExplodeSeconds;
        if (v.AniNumber > 0 && v.AnimFPS > 0f) return v.AniNumber / v.AnimFPS;
        return v.Duration > 0f ? v.Duration : FallbackExplodeSeconds;
    }
}
