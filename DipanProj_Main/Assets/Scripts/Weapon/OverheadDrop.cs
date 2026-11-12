using System.Collections.Generic;
using UnityEngine;
using Sorrows.Ballistics;

/// <summary>
/// **頭頂投彈（<see cref="WeaponMode.Overhead"/>）的執行元件**——饕餮印（2026-11-12）：
/// 施放時找出施放者身邊 Range 內的每一個目標（玩家放＝畫面內的敵怪；怪物放＝玩家與玩家的召喚物），
/// 每個目標頭上冒出一個「投彈者」（饕餮幽靈…），從嘴裡吐出炸彈往下掉，掉到目標身上爆炸。
///
/// <para><b>打得到移動中的怪</b>（作者 2026-11-12 擔心表演時間太長怪會走遠）：投彈者整段黏在目標頭頂跟著走；
/// 炸彈掉落時每一幀都對準目標**當下**的身體中心（起點偏移隨落下縮到 0），所以落點一定是牠現在站的地方。
/// 目標中途死掉 ⇒ 炸彈落在牠最後的位置。</para>
///
/// <para><b>擁有者無關</b>：玩家與怪物都呼叫 <see cref="Cast"/>（照 <see cref="SweepStrike"/>／<see cref="WeaponCastService"/> 的原則：
/// 找目標、表演、爆炸在這裡；耗魔、冷卻留在呼叫端）。每次施放生一個 GameObject 自己跑完自己 Destroy。
/// 擁有者中途被移除 ⇒ 整發取消（傷害來源不能是不存在的物件）。</para>
///
/// <para><b>欄位</b>（見 readme/RECIPE_DESCRIBE.md〈Overhead 頭頂投彈〉）：
/// 配方 <c>Range</c>＝索敵半徑（遠射珠）、<c>MaxTargets</c>＝同時上限（空＝不限）、<c>DropStagger</c>＝各目標陸續冒出的時間差、
/// <c>FlightTime</c>＝炸彈掉落秒數（迅捷珠縮短）、<c>AreaRadius</c>＝爆炸半徑、<c>SpreadCount</c>＝每個投彈者吐幾顆（分裂珠）、
/// <c>LandingScatterRadius</c>＝多顆時的落點散開、<c>BurstCount／BurstInterval</c>＝同一批投彈者連吐幾輪（連擊珠）。
/// 武器 <c>DropperStyle</c>（shader 樣式，目前 <c>TaotieInk</c>）或 <c>DropperVfxId</c>（任何 VfxTable 序列特效）決定投彈者外型、<c>DropperSize</c> 大小；
/// 炸彈＝武器自己的子彈圖（WeaponSpritePath／WeaponAniPath）；爆炸＝<c>HitEffectID</c>；<c>BulletScale</c>（須彌珠）＝炸彈、爆炸半徑與特效一起放大。</para>
/// </summary>
public class OverheadDrop : MonoBehaviour
{
    // ── 表演節奏（秒）──
    /// <summary>投彈者凝結成形要多久（墨煙一絲一絲聚起來）。比血統身邊的幽靈（1 秒）短很多——武器要快。</summary>
    public const float FormSeconds = 0.35f;
    /// <summary>最後一顆吐完後再停多久才散去。</summary>
    public const float HoldAfterSpit = 0.15f;
    /// <summary>散去要多久（尾巴先散）。</summary>
    public const float FadeSeconds = 0.5f;
    /// <summary>分裂（一隻吐好幾顆）時，同一輪每顆之間隔多久。</summary>
    public const float SpreadSpitGap = 0.1f;   // 2026-11-12 饕餮印改一次吐 3 顆，0.1 秒一顆有連發感
    /// <summary>一發最多打幾個目標（效能保護；表上的 MaxTargets 再往下限）。</summary>
    public const int HardMaxTargets = 64;
    /// <summary>投彈者底部離目標頭頂多高（× 投彈者大小）。</summary>
    const float GapAboveHead = 0.6f;   // 2026-11-12 作者：0.08 太貼頭 → 0.3 → 再高一點 0.6（太靠畫面上緣的由 ClampToView 壓回畫面內）
    /// <summary>幽靈貼圖裡嘴的高度（uv，由下往上）。炸彈從這裡吐出來。</summary>
    const float MouthV = 0.2f;
    /// <summary>沒有子彈預製物可參考大小時，炸彈圖的縮放。</summary>
    const float FallbackBombScale = 0.1f;

    // ── 投彈者樣式（DropperStyle）：shader 樣式名 → 貼圖。之後加新樣式在這裡加一行 ──
    static readonly Dictionary<string, string> StyleTex = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "TaotieInk", "VfxEffects/BloodlineGhost/taotie_ghost" },   // 水墨饕餮（跟血統身邊的幽靈同一張）
    };
    const string GhostShaderPath = "Shaders/BloodlineGhost";
    const string SmokeTexAPath = "UI/BossIntroPanel/BossIntroPanel_Smoke1";
    const string SmokeTexBPath = "UI/BossIntroPanel/BossIntroPanel_Smoke2";

    /// <summary>一次施放所需的「誰、從哪、打得到誰」。</summary>
    public struct Context
    {
        /// <summary>施放者。傷害來源；被移除時整發中止。</summary>
        public GameObject Owner;
        /// <summary>量索敵半徑的圓心（施放者位置）。</summary>
        public Vector2 Center;
        /// <summary>目標清單（呼叫端用 <see cref="CollectEnemiesOfPlayer"/>／<see cref="CollectFoesOfMonster"/> 填）。</summary>
        public List<Target> Targets;
        /// <summary>爆炸打得到的層（玩家放＝怪物層；怪物放＝玩家層＋友軍所在層）。陣營由 CombatSystem 再擋一次。</summary>
        public LayerMask TargetLayers;
        /// <summary>環境層（可破壞地上物在這層，爆炸會一起炸）。0＝不炸地上物。</summary>
        public LayerMask EnvLayer;
        /// <summary>擁有者體型，乘在炸彈圖上（爆炸大小看 BulletScale）。</summary>
        public float OwnerScale;
        /// <summary>投彈者與落點收進畫面內（玩家放＝true：巨大 boss 頭頂在畫面外時幽靈壓到畫面上緣）。</summary>
        public bool ClampToView;
    }

    /// <summary>一個目標（怪物或玩家）。只用來問「活著嗎、腳在哪、多高」。</summary>
    public struct Target
    {
        public GameObject Go;
        public MonsterController Monster;
        public PlayerController Player;

        public bool Alive
        {
            get
            {
                if (Go == null) return false;
                if (Monster != null) return !Monster.IsDead;
                if (Player != null) return !Player.IsDead;
                return true;
            }
        }
        public Vector2 Feet => Monster != null ? Monster.FeetWorldPos : Player != null ? Player.FeetWorldPos : (Vector2)Go.transform.position;
        public float Height => Monster != null ? Monster.VisibleBodyHeight : Player != null ? Player.VisibleBodyHeight : 1f;
    }

    // ═══════════════════════════ 找目標 ═══════════════════════════

    /// <summary>
    /// 玩家施放：<paramref name="center"/> 半徑 <paramref name="range"/> 內、**畫面內**、活著的敵怪（不含友軍與中立 NPC、被骨牢關住的照打）。
    /// 由近到遠排序（MaxTargets 截斷時留最近的）。
    /// </summary>
    public static void CollectEnemiesOfPlayer(Vector2 center, float range, List<Target> outList)
    {
        outList.Clear();
        float r2 = range * range;
        var cam = Camera.main;
        var all = MonsterController.Active;
        for (int i = 0; i < all.Count; i++)
        {
            var m = all[i];
            // ⚠ 不看 Controllable：那是「能不能被骨牢定身」，boss 填 0 是免疫控制、不是打不到（2026-11-12 新手夢境佛掌就是這樣漏掉的）
            if (m == null || m.IsDead) continue;
            if (m.Faction == MonsterFaction.PlayerAlly || m.Faction == MonsterFaction.Neutral) continue;
            // 距離與畫面判斷都用「身體這一條直線」（腳底到頭頂），不用身體中心：
            // 巨大的 boss（佛掌 Scale 6）中心常在畫面外或很遠，但身體明明有一大段在畫面裡
            Vector2 feet = m.FeetWorldPos;
            float h = m.VisibleBodyHeight;
            Vector2 near = new Vector2(feet.x, Mathf.Clamp(center.y, feet.y, feet.y + h));
            if ((near - center).sqrMagnitude > r2) continue;
            if (!SpanOnScreen(cam, feet, h)) continue;
            outList.Add(new Target { Go = m.gameObject, Monster = m });
        }
        SortByDistance(center, outList);
    }

    /// <summary>
    /// 怪物施放：主要目標（通常是玩家）＋半徑內玩家的召喚物（PlayerAlly）。不限畫面內。由近到遠排序。
    /// </summary>
    public static void CollectFoesOfMonster(Vector2 center, float range, Transform primary, List<Target> outList)
    {
        outList.Clear();
        float r2 = range * range;
        if (primary != null)
        {
            var pc = primary.GetComponentInParent<PlayerController>();
            var mc = pc == null ? primary.GetComponentInParent<MonsterController>() : null;
            var t = new Target { Go = pc != null ? pc.gameObject : mc != null ? mc.gameObject : primary.gameObject, Player = pc, Monster = mc };
            Vector2 c = t.Feet + Vector2.up * (t.Height * 0.5f);
            if (t.Alive && (c - center).sqrMagnitude <= r2) outList.Add(t);
        }
        var all = MonsterController.Active;
        for (int i = 0; i < all.Count; i++)
        {
            var m = all[i];
            if (m == null || m.IsDead || m.Faction != MonsterFaction.PlayerAlly) continue;
            if (primary != null && m.gameObject == primary.gameObject) continue;
            if ((m.BodyCenterWorldPos - center).sqrMagnitude > r2) continue;
            outList.Add(new Target { Go = m.gameObject, Monster = m });
        }
        SortByDistance(center, outList);
    }

    static void SortByDistance(Vector2 center, List<Target> list)
    {
        list.Sort((a, b) => ((a.Feet - center).sqrMagnitude).CompareTo((b.Feet - center).sqrMagnitude));
    }

    /// <summary>點在不在畫面內（四周留 4% 邊，免得挑到只露半個身體的；同骨牢）。拿不到相機一律 true。</summary>
    static bool OnScreen(Camera cam, Vector2 p)
    {
        if (cam == null) return true;
        const float Margin = 0.04f;
        Vector3 vp = cam.WorldToViewportPoint(p);
        if (vp.z < 0f) return false;
        return vp.x >= Margin && vp.x <= 1f - Margin && vp.y >= Margin && vp.y <= 1f - Margin;
    }

    /// <summary>身體（腳底到頭頂這一條）有沒有一段在畫面內：x 在畫面內、且垂直區間與畫面重疊。</summary>
    static bool SpanOnScreen(Camera cam, Vector2 feet, float height)
    {
        if (cam == null) return true;
        const float Margin = 0.04f;
        Vector3 a = cam.WorldToViewportPoint(feet);
        Vector3 b = cam.WorldToViewportPoint(feet + Vector2.up * height);
        if (a.z < 0f) return false;
        if (a.x < Margin || a.x > 1f - Margin) return false;
        return Mathf.Max(a.y, b.y) >= Margin && Mathf.Min(a.y, b.y) <= 1f - Margin;
    }

    // ═══════════════════════════ 施放 ═══════════════════════════

    class Drop
    {
        public Target Tgt;
        public float Delay;          // 從施放算起幾秒後冒出來
        public float Seed;
        public bool Flip;
        public Vector2 LastFeet;     // 目標最後一次活著時的位置（死了就停在這）
        public float LastHeight;
        public float[] SpitAt;       // 每一顆炸彈在本地時間第幾秒吐出
        public int NextSpit;
        public float FadeStart;      // 本地時間第幾秒開始散
        public GameObject Go;
        public SpriteRenderer Sr;
        public VfxInstance Vfx;
        public bool Done;
    }

    class Bomb
    {
        public Drop From;
        public Vector2 Offset;       // 吐出當下「嘴 − 落點」：落下時乘 (1−e) 縮到 0，炸彈就一路追著目標
        public Vector2 Scatter;      // 多顆時各自的落點偏移
        public float T;
        public Transform Tr;
        public SpriteRenderer Sr;
        public float Frame;
    }

    WeaponData _weapon;
    Context _ctx;
    readonly List<Drop> _drops = new List<Drop>();
    readonly List<Bomb> _bombs = new List<Bomb>();
    float _t;
    float _size;
    float _fall;
    Material _ghostMat;
    MaterialPropertyBlock _mpb;
    bool _clampToView;   // 玩家放的才收進畫面（怪物放的目標是玩家，本來就在畫面裡）
    Camera _cam;
    static VfxManager _vfx;
    static Texture2D _smokeA, _smokeB;
    static Shader _ghostShader;

    /// <summary>
    /// 施放一發。回傳打了幾個目標（0＝範圍內沒有目標，呼叫端不該扣魔／進冷卻）；武器或擁有者無效回 -1。
    /// </summary>
    public static int Cast(WeaponData weapon, in Context ctx)
    {
        if (weapon == null || weapon.Recipe == null || ctx.Owner == null) return -1;
        if (ctx.Targets == null || ctx.Targets.Count == 0) return 0;
        RecipeEntry r = weapon.Recipe;
        ProjectileData pd = r.Data;

        int max = r.MaxTargets > 0 ? Mathf.Min(r.MaxTargets, HardMaxTargets) : HardMaxTargets;
        int n = Mathf.Min(ctx.Targets.Count, max);
        int perRound = Mathf.Clamp(pd != null ? pd.SplitCount : 1, 1, 16);
        int rounds = Mathf.Clamp(r.BurstCount, 1, 16);
        float roundGap = Mathf.Max(0.05f, r.BurstInterval);

        var go = new GameObject("OverheadDrop_" + weapon.Name);
        var s = go.AddComponent<OverheadDrop>();
        s._weapon = weapon;
        s._ctx = ctx;
        s._ctx.Targets = null;   // 目標已抄進各個 Drop；不留呼叫端清單的參照（玩家那份每發重用）
        s._size = Mathf.Max(0.1f, weapon.DropperSize);
        s._fall = Mathf.Max(0.05f, pd != null ? pd.FlightTime : 0.35f);
        s._mpb = new MaterialPropertyBlock();
        s._clampToView = ctx.ClampToView;
        s._cam = Camera.main;
        s.BuildGhostMaterial();

        float stagger = Mathf.Max(0f, r.DropStagger);
        for (int i = 0; i < n; i++)
        {
            var d = new Drop
            {
                Tgt = ctx.Targets[i],
                Delay = n > 1 ? Random.Range(0f, stagger) : 0f,
                Seed = Random.Range(0f, 10f),
                Flip = Random.value < 0.5f,
            };
            d.LastFeet = d.Tgt.Feet;
            d.LastHeight = d.Tgt.Height;
            d.SpitAt = new float[perRound * rounds];
            int k = 0;
            for (int rd = 0; rd < rounds; rd++)
                for (int j = 0; j < perRound; j++)
                    d.SpitAt[k++] = FormSeconds + rd * roundGap + j * SpreadSpitGap;
            d.FadeStart = d.SpitAt[d.SpitAt.Length - 1] + HoldAfterSpit;
            s._drops.Add(d);
        }
        return n;
    }

    void BuildGhostMaterial()
    {
        string style = _weapon.DropperStyle;
        if (string.IsNullOrEmpty(style) || !StyleTex.TryGetValue(style, out var texPath)) return;
        if (_ghostShader == null) _ghostShader = Resources.Load<Shader>(GhostShaderPath);
        if (_ghostShader == null) { Debug.LogWarning($"[Overhead] 找不到 Resources/{GhostShaderPath}，投彈者改成不顯示。"); return; }
        var tex = LoadTex(texPath);
        if (tex == null) { Debug.LogWarning($"[Overhead] 找不到投彈者貼圖 Resources/{texPath}（DropperStyle={style}）。"); return; }
        if (_smokeA == null) _smokeA = LoadTex(SmokeTexAPath);
        if (_smokeB == null) _smokeB = LoadTex(SmokeTexBPath);
        _ghostMat = new Material(_ghostShader) { hideFlags = HideFlags.HideAndDontSave };
        _ghostMat.SetTexture("_GhostTex", tex);
        if (_smokeA != null) _ghostMat.SetTexture("_SmokeA", _smokeA);
        if (_smokeB != null) _ghostMat.SetTexture("_SmokeB", _smokeB);
    }

    static Texture2D LoadTex(string path)
    {
        var tex = Resources.Load<Texture2D>(path);
        if (tex != null) return tex;
        var spr = Resources.Load<Sprite>(path);
        return spr != null ? spr.texture : null;
    }

    static Sprite _quad;
    static Sprite Quad()
    {
        if (_quad != null) return _quad;
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, Color.white); t.Apply();
        _quad = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        _quad.hideFlags = HideFlags.HideAndDontSave;
        return _quad;
    }

    void Update()
    {
        // 擁有者沒了 ⇒ 整發取消（⚠ UnityEngine.Object 的 == 才判得出被 Destroy 的物件，PROBLEMS F26）
        if (_ctx.Owner == null) { Destroy(gameObject); return; }

        float dt = Time.deltaTime;
        _t += dt;

        bool anyAlive = false;
        foreach (var d in _drops)
        {
            if (d.Done) continue;
            anyAlive = true;
            float tau = _t - d.Delay;
            if (tau < 0f) continue;

            if (d.Tgt.Alive) { d.LastFeet = d.Tgt.Feet; d.LastHeight = d.Tgt.Height; }
            Vector2 center = GhostCenter(d, tau);

            if (d.Go == null) SpawnDropper(d, center);

            // 吐彈
            while (d.NextSpit < d.SpitAt.Length && tau >= d.SpitAt[d.NextSpit])
            {
                SpitBomb(d, center + Vector2.up * ((MouthV - 0.5f) * _size));
                d.NextSpit++;
            }

            // 投彈者：凝結 0→1、停留 1、散去 1→2
            float gT = tau < FormSeconds ? tau / FormSeconds : tau < d.FadeStart ? 1f : 1f + (tau - d.FadeStart) / FadeSeconds;
            if (gT >= 2f)
            {
                if (d.Go != null) Destroy(d.Go);
                d.Go = null; d.Done = true;
                continue;
            }
            if (d.Go != null)
            {
                d.Go.transform.position = new Vector3(center.x, center.y, 0f);
                int order = MapDepthSort.Order(d.LastFeet.y, 0) + 3;
                if (d.Sr != null)
                {
                    d.Sr.sortingOrder = order;
                    d.Sr.GetPropertyBlock(_mpb);
                    _mpb.SetFloat("_GhostT", gT);
                    _mpb.SetFloat("_Seed", d.Seed);
                    _mpb.SetFloat("_Flip", d.Flip ? 1f : 0f);
                    d.Sr.SetPropertyBlock(_mpb);
                }
            }
        }

        for (int i = _bombs.Count - 1; i >= 0; i--)
        {
            var b = _bombs[i];
            b.T += dt;
            Drop d = b.From;
            if (d.Tgt.Alive) { d.LastFeet = d.Tgt.Feet; d.LastHeight = d.Tgt.Height; }
            Vector2 land = LandPoint(d, b.Scatter, _t - d.Delay);
            float u = Mathf.Clamp01(b.T / _fall);
            float e = u * u;   // 加速落下
            if (u >= 1f)
            {
                Explode(land, d.Tgt);
                if (b.Tr != null) Destroy(b.Tr.gameObject);
                _bombs.RemoveAt(i);
                continue;
            }
            if (b.Tr != null)
            {
                Vector2 p = land + b.Offset * (1f - e);
                b.Tr.position = new Vector3(p.x, p.y, 0f);
                var pd = _weapon.Recipe.Data;
                if (pd != null && pd.RotationSpeed != 0f) b.Tr.Rotate(0f, 0f, pd.RotationSpeed * dt);
                if (b.Sr != null)
                {
                    b.Sr.sortingOrder = MapDepthSort.Order(d.LastFeet.y, 0) + 4;
                    var frames = _weapon.WeaponSprites;
                    if (frames != null && frames.Length > 1 && _weapon.AnimFPS > 0f)
                    {
                        b.Frame += dt * _weapon.AnimFPS;
                        b.Sr.sprite = frames[(int)b.Frame % frames.Length];
                    }
                }
            }
        }

        if (!anyAlive && _bombs.Count == 0) Destroy(gameObject);
    }

    Vector2 GhostCenter(Drop d, float tau)
    {
        float bob = Mathf.Sin((tau + d.Seed) * 5f) * _size * 0.03f;
        var c = new Vector2(d.LastFeet.x, d.LastFeet.y + d.LastHeight + _size * (0.5f + GapAboveHead) + bob);
        // 玩家放的：投彈者一定要看得到。目標太高（巨大 boss 頭頂在畫面外）就壓到畫面上緣內側，左右也收進畫面
        if (_clampToView && _cam != null)
        {
            float top = _cam.ViewportToWorldPoint(new Vector3(0.5f, 1f, 0f)).y - _size * 0.55f;
            float left = _cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, 0f)).x + _size * 0.5f;
            float right = _cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, 0f)).x - _size * 0.5f;
            if (c.y > top) c.y = Mathf.Max(top, d.LastFeet.y + _size * 0.6f);
            if (left < right) c.x = Mathf.Clamp(c.x, left, right);
        }
        return c;
    }

    /// <summary>炸彈落點：身體中心；被壓到畫面內的投彈者要在落點上方，所以落點不高於投彈者底下一點、也不低於腳底。</summary>
    Vector2 LandPoint(Drop d, Vector2 scatter, float tau)
    {
        Vector2 land = d.LastFeet + Vector2.up * (d.LastHeight * 0.5f);
        if (_clampToView && _cam != null)
        {
            Vector2 g = GhostCenter(d, tau);
            land.x = g.x;
            land.y = Mathf.Clamp(land.y, d.LastFeet.y, Mathf.Max(d.LastFeet.y, g.y - _size * 1.1f));
        }
        return land + scatter;
    }

    void SpawnDropper(Drop d, Vector2 at)
    {
        if (_ghostMat != null)
        {
            d.Go = new GameObject("OverheadDropper");
            d.Go.transform.SetParent(transform, false);
            d.Go.transform.position = new Vector3(at.x, at.y, 0f);
            d.Go.transform.localScale = new Vector3(_size, _size, 1f);
            d.Sr = d.Go.AddComponent<SpriteRenderer>();
            d.Sr.sprite = Quad();
            d.Sr.sharedMaterial = _ghostMat;
            return;
        }
        if (_weapon.DropperVfxId > 0)
        {
            if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();
            if (_vfx == null) return;
            // 生死由這裡管：活到散去開始（序列特效沒有溶解，直接收掉）
            d.Vfx = _vfx.SpawnLoopSizedToHeight(_weapon.DropperVfxId, at, _size, -1f);
            if (d.Vfx != null) { d.Go = d.Vfx.gameObject; d.Go.transform.SetParent(transform, true); }
        }
    }

    void SpitBomb(Drop d, Vector2 mouth)
    {
        float scatterR = _weapon.Recipe.Data != null ? _weapon.Recipe.Data.LandingScatterRadius : 0f;
        Vector2 scatter = (d.SpitAt.Length > 1 && scatterR > 0f) ? Random.insideUnitCircle * scatterR : Vector2.zero;
        Vector2 land = LandPoint(d, scatter, _t - d.Delay);
        var b = new Bomb { From = d, Offset = mouth - land, Scatter = scatter };

        Sprite sp = _weapon.WeaponSprite;
        if (sp == null && _weapon.WeaponSprites != null && _weapon.WeaponSprites.Length > 0) sp = _weapon.WeaponSprites[0];
        if (sp != null)
        {
            var go = new GameObject("OverheadBomb");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(mouth.x, mouth.y, 0f);
            float baseScale = _weapon.BulletPrefab != null ? _weapon.BulletPrefab.transform.localScale.x : FallbackBombScale;
            float sc = baseScale * _weapon.BulletScale * (_ctx.OwnerScale > 0.0001f ? _ctx.OwnerScale : 1f);
            go.transform.localScale = new Vector3(sc, sc, 1f);
            b.Tr = go.transform;
            b.Sr = go.AddComponent<SpriteRenderer>();
            b.Sr.sprite = sp;
        }
        _bombs.Add(b);
    }

    void Explode(Vector2 at, Target primary)
    {
        float scale = _weapon.BulletScale;   // 須彌珠：半徑與特效一起放大
        if (_weapon.HitEffectID > 0)
        {
            if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();
            if (_vfx != null) _vfx.Spawn(_weapon.HitEffectID, at, 0f, scale);
        }
        if (_weapon.Damage <= 0f) return;

        float radius = Mathf.Max(0.05f, _weapon.Recipe.AreaRadius) * scale;
        var seen = new HashSet<int>();   // 同一顆炸彈對同一個目標只算一次（一隻怪可能有好幾個碰撞框）
        // 炸彈是追著這個目標掉的 ⇒ 牠一定吃到（OverlapCircle 在 queriesStartInColliders=false 下可能漏抓貼身的，見 PROBLEMS）
        if (primary.Alive) Hit(primary.Go, at, seen);
        Collider2D[] hits = Physics2D.OverlapCircleAll(at, radius, _ctx.TargetLayers | _ctx.EnvLayer);
        for (int i = 0; i < hits.Length; i++)
            if (hits[i] != null) Hit(hits[i].gameObject, at, seen);
    }

    void Hit(GameObject target, Vector2 at, HashSet<int> seen)
    {
        if (target == null || target == _ctx.Owner) return;
        // 去重用「角色本體」：碰撞框可能掛在子物件上，同一隻怪不能因為兩個碰撞框吃兩次
        var mc = target.GetComponentInParent<MonsterController>();
        var pc = mc == null ? target.GetComponentInParent<PlayerController>() : null;
        GameObject body = mc != null ? mc.gameObject : pc != null ? pc.gameObject : target;
        if (body == _ctx.Owner) return;
        if (!seen.Add(body.GetInstanceID())) return;
        Vector2 dir = (Vector2)target.transform.position - at;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
        // 牆沒有 IDamageable，CombatSystem 會自動略過；陣營判斷也交給它（FactionRelations）。
        CombatSystem.Apply(_ctx.Owner, target, GmWeapons.DamageAgainst(_weapon, target), dir);
    }

    void OnDestroy()
    {
        if (_ghostMat != null) Destroy(_ghostMat);
    }
}
