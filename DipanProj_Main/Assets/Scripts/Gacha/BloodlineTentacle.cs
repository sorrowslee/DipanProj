using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 第三階血統的「虛空觸手」——每隔 20～30 秒（隨機），背後浮現六條半透明章魚觸手（左右各三條：肩、背中、腰），
/// 像被抓起來想掙脫的活章魚一樣**大力亂甩 3 秒**，然後從尖端溶解消失。只是外觀、不打怪。
/// 目前只有古神系列第三階 克蘇魯（表B `TentacleStyle` = <c>EldritchVoid</c>）。見 readme/BLOODLINE.md §5b〈`TentacleStyle`：虛空觸手〉。
///
/// ── 是哪幾條既有結論拼起來的 ──
/// ① **網格帶子沿中心線鋪、貼一張左右無縫的皮**＝靈獸電龍的身體（DIPAN_VFX §4e）：長條軟體適合「靜態圖貼在程式彎的網格上」。
/// ② **虛化**＝BloodlineGhost／BloodlineMist 的煙霧密度圖手法（凝結門檻、斑駁、外緣煙絲、輪廓光）。
/// ③ **獨立物件**（parent＝玩家的父節點）＋位置大小用 <c>FeetWorldPos／ScaledCharacterHeight</c>（PROBLEMS E13、E14），體型變了自然跟上。
/// ④ **趴著／倒下時整層關掉**（<c>PlayerAnimator.BodyFxVisible</c>），爬起來重新倒數。
/// ⑤ 全部用自己累加的時間（Time.deltaTime），開面板暫停時跟著凍住。
///
/// ── 動作（2026-11-25 作者看過 v1～v9 模擬後定稿，見 tools/art/bloodline_Eldritchborn/fx/sim3.py）──
/// 每條觸手＝一串「甩鞭」：根部方向在這條的角度範圍內**猛力甩到一個新方向**（跟現在差 ≥ <see cref="MinSwing"/> 度、
/// 0.16～0.28 秒、ease-out-back 甩過頭再彈回）→ 停 0.04～0.44 秒 → 下一甩；全部亂數，六條不同步。
/// 每一節的角度＝根部「s×lag 秒前」的角度（鞭子從根傳到尖），甩得越快尾段越往反方向拖；再疊上慢慢捲曲、沿身體傳的扭動、些微伸縮。
/// 角度座標：0＝水平往外、90＝往上（左右兩邊各自「往外」），面向改變時整組鏡射。
/// </summary>
[DisallowMultipleComponent]
public class BloodlineTentacle : MonoBehaviour
{
    const string ShaderPath = "Shaders/BloodlineTentacle";
    const string SmokeTexAPath = "UI/BossIntroPanel/BossIntroPanel_Smoke1";
    const string SmokeTexBPath = "UI/BossIntroPanel/BossIntroPanel_Smoke2";
    const string SkinTexPath = "VfxEffects/BloodlineTentacle/tentacle_skin";

    [Header("出現節奏（秒）")]
    public float IntervalMin = 20f;
    public float IntervalMax = 30f;
    /// <summary>變身完第一次出現的等待（短一點，看得到）。</summary>
    public float FirstDelayMin = 2f;
    public float FirstDelayMax = 4f;
    public float FormTime = 0.3f;
    public float ShowTime = 3f;
    public float FadeTime = 0.5f;
    /// <summary>六條觸手凝結的先後差（0～此值隨機）。</summary>
    public float FormStagger = 0.08f;

    [Header("甩鞭")]
    public float SwingTimeMin = 0.16f;
    public float SwingTimeMax = 0.28f;
    public float HoldMin = 0.04f;
    public float HoldMax = 0.44f;
    /// <summary>每一甩至少轉多少度。</summary>
    public float MinSwing = 100f;
    /// <summary>甩過頭的量（ease-out-back 係數）。</summary>
    public float Overshoot = 1.9f;
    /// <summary>鞭子從根傳到尖的延遲（每條觸手出生時在這範圍內抽）。</summary>
    public float LagMin = 0.18f;
    public float LagMax = 0.26f;
    /// <summary>甩動時尾段往反方向拖的量（每「度/秒」幾度）。</summary>
    public float Drag = 0.09f;
    public float DragVelClamp = 1250f;

    [Header("形狀（× 角色高度；角度 0＝水平往外、90＝往上）")]
    public Layer[] Layers =
    {
        new Layer { RootY = 0.70f, Length = 1.55f, Width = 0.095f, AngleLo = -15f, AngleHi = 118f, BackX = -0.06f, FrontX = 0.03f },   // 肩
        new Layer { RootY = 0.58f, Length = 1.45f, Width = 0.088f, AngleLo = -50f, AngleHi = 100f, BackX = -0.09f, FrontX = 0.05f },   // 背中
        new Layer { RootY = 0.46f, Length = 1.30f, Width = 0.080f, AngleLo = -70f, AngleHi = 62f,  BackX = -0.12f, FrontX = 0.07f },   // 腰
    };
    /// <summary>尖端寬度 = 根寬 × (1 − 此值)。</summary>
    public float TipTaper = 0.84f;
    public int Segments = 40;

    [Header("透明度")]
    /// <summary>平常的不透明度；甩得越快越實（加到 +<see cref="OpacityBoost"/>）。</summary>
    public float OpacityBase = 0.42f;
    public float OpacityBoost = 0.33f;
    public float OpacityFullSpeed = 350f;
    /// <summary>甩得快時多畫一層「前一刻」的殘影（像動態模糊）。</summary>
    public float EchoSpeed = 175f;
    public float EchoDelay = 0.07f;
    public float EchoOpacity = 0.18f;

    /// <summary>相對角色的排序偏移。-1 = 畫在角色之下（觸手從背後長出來）。</summary>
    public int SortOffset = -1;
    /// <summary>shader 的網格半寬／可見半寬（多出來的是輪廓光與煙絲）；要跟 shader 的 _GlowWidth 一致。</summary>
    const float GlowWidth = 1.6f;

    [System.Serializable]
    public class Layer
    {
        public float RootY, Length, Width, AngleLo, AngleHi;
        /// <summary>根部的水平位置（× 角色高，正＝面向那邊）：背側那條、面前那側那條。都在身體輪廓內，被角色蓋住。</summary>
        public float BackX, FrontX;
    }

    struct Swing { public float t0, dur, from, to; }

    class Tentacle
    {
        public Layer layer;
        public float side;            // −1＝背側、+1＝面前那側（以面向為準）
        public float seed, lag, formDelay;
        public float wf, wl, wf2;     // 扭動頻率、波長
        public float nCurlA, nCurlB, nLen;   // Perlin 取樣列
        public readonly List<Swing> swings = new List<Swing>();
    }

    string _style;
    PlayerController _pc;
    PlayerAnimator _anim;
    YSortByFeet _ysort;
    SpriteRenderer _hostSr;

    readonly List<Tentacle> _tents = new List<Tentacle>();
    GameObject _go;
    MeshRenderer _mr;
    Mesh _mesh;
    Material _mat;
    bool _built;

    float _wait;        // 距離下一次出現
    float _t = -1f;     // 這次出現的時間（<0＝沒在演）

    readonly List<Vector3> _v = new List<Vector3>();
    readonly List<Vector2> _uv0 = new List<Vector2>(), _uv1 = new List<Vector2>(), _uv2 = new List<Vector2>();
    readonly List<Color> _c = new List<Color>();
    readonly List<int> _tri = new List<int>();
    Vector2[] _pts;
    Matrix4x4 _w2l = Matrix4x4.identity;
    float[] _ang;

    void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _anim = GetComponent<PlayerAnimator>();
        _ysort = GetComponent<YSortByFeet>();
        _hostSr = GetComponent<SpriteRenderer>();
        if (_hostSr == null) _hostSr = GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>設定樣式；留空 = 關掉這一層。重複傳同樣的值不會重建。</summary>
    public void SetStyle(string style)
    {
        style = string.IsNullOrWhiteSpace(style) ? null : style.Trim();
        if (style == null) { _style = null; Clear(); return; }
        if (style == _style && _built) return;
        _style = style;
        _wait = Random.Range(FirstDelayMin, Mathf.Max(FirstDelayMin, FirstDelayMax));
        _t = -1f;
        Build();
    }

    void Build()
    {
        if (_built) return;
        _built = true;

        // 走 Resources.Load（專案慣例）——Shader.Find 打包後容易是 null。
        var sh = Resources.Load<Shader>(ShaderPath);
        if (sh == null) { Debug.LogWarning($"[BloodlineTentacle] 找不到 Resources/{ShaderPath}，觸手不顯示。"); }
        else
        {
            _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            var texA = LoadTex(SmokeTexAPath);
            var texB = LoadTex(SmokeTexBPath);
            if (texA == null || texB == null)
                Debug.LogWarning($"[BloodlineTentacle] 找不到煙霧密度圖 Resources/{SmokeTexAPath} 或 {SmokeTexBPath}。");
            else { _mat.SetTexture("_SmokeA", texA); _mat.SetTexture("_SmokeB", texB); }
            var skin = LoadTex(SkinTexPath);
            if (skin != null) _mat.SetTexture("_MainTex", skin);
            else Debug.LogWarning($"[BloodlineTentacle] 找不到觸手皮 Resources/{SkinTexPath}。");
            _skinAspect = skin != null ? (float)skin.width / Mathf.Max(1, skin.height) : 1.42f;
        }

        _mesh = new Mesh { name = "BloodlineTentacle", hideFlags = HideFlags.DontSave };
        _mesh.MarkDynamic();
        _go = new GameObject("BloodlineTentacle");
        // ⚠ 獨立物件、不是玩家子物件（同 BloodlineGhost：子物件會被角色 localScale／flipX 二次影響）。
        _go.transform.SetParent(transform.parent, false);
        _go.transform.position = Vector3.zero;
        _go.AddComponent<MeshFilter>().sharedMesh = _mesh;
        _mr = _go.AddComponent<MeshRenderer>();
        if (_mat != null) _mr.sharedMaterial = _mat;
        _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _mr.receiveShadows = false;
        _mr.enabled = false;

        int n = Mathf.Max(8, Segments);
        _pts = new Vector2[n];
        _ang = new float[n];
    }

    float _skinAspect = 1.42f;

    static Texture2D LoadTex(string path)
    {
        var tex = Resources.Load<Texture2D>(path);
        if (tex != null) return tex;
        var spr = Resources.Load<Sprite>(path);
        return spr != null ? spr.texture : null;
    }

    // ───────────────────────── 每幀 ─────────────────────────

    void LateUpdate()
    {
        if (_style == null) return;
        if (!_built) { Build(); return; }
        if (_mat == null) return;

        if (_anim == null) _anim = GetComponent<PlayerAnimator>();
        bool visible = _anim == null || _anim.BodyFxVisible;
        if (!visible)
        {
            // 趴下／倒地：收掉，爬起來重新倒數（短一點）
            _t = -1f; _mr.enabled = false;
            _wait = Random.Range(FirstDelayMin, Mathf.Max(FirstDelayMin, FirstDelayMax));
            return;
        }

        float dt = Time.deltaTime;
        if (_t < 0f)
        {
            _wait -= dt;
            if (_wait > 0f) { if (_mr.enabled) _mr.enabled = false; return; }
            BeginShow();
        }
        else
        {
            _t += dt;
            if (_t > FormTime + ShowTime + FadeTime + FormStagger)
            {
                _t = -1f; _mr.enabled = false;
                _wait = Random.Range(IntervalMin, Mathf.Max(IntervalMin, IntervalMax));
                return;
            }
        }

        BuildMesh();
    }

    void BeginShow()
    {
        _t = 0f;
        _tents.Clear();
        float total = FormTime + ShowTime + FadeTime + FormStagger + 0.6f;
        foreach (float side in new[] { -1f, 1f })
        {
            foreach (var L in Layers)
            {
                var tt = new Tentacle
                {
                    layer = L, side = side,
                    seed = Random.Range(0f, 10f),
                    lag = Random.Range(LagMin, LagMax),
                    formDelay = Random.Range(0f, FormStagger),
                    wf = Random.Range(2.2f, 3.4f), wl = Random.Range(5.5f, 8.5f), wf2 = Random.Range(4.5f, 6.5f),
                    nCurlA = Random.Range(0f, 100f), nCurlB = Random.Range(100f, 200f), nLen = Random.Range(200f, 300f),
                };
                // 一串甩鞭（一出場就在甩，不醞釀）
                float cur = Random.Range(L.AngleLo, L.AngleHi);
                float tc = Random.Range(-0.3f, 0.2f);
                float minSwing = Mathf.Min(MinSwing, (L.AngleHi - L.AngleLo) * 0.6f);
                while (tc < total)
                {
                    float nxt = cur;
                    for (int k = 0; k < 20; k++)
                    {
                        nxt = Random.Range(L.AngleLo, L.AngleHi);
                        if (Mathf.Abs(nxt - cur) >= minSwing) break;
                    }
                    float dur = Random.Range(SwingTimeMin, SwingTimeMax);
                    tt.swings.Add(new Swing { t0 = tc, dur = dur, from = cur, to = nxt });
                    cur = nxt;
                    tc += dur + Random.Range(HoldMin, HoldMax);
                }
                _tents.Add(tt);
            }
        }
    }

    float EaseOutBack(float u)
    {
        u = Mathf.Clamp01(u); float k = Overshoot;
        return 1f + (k + 1f) * Mathf.Pow(u - 1f, 3f) + k * (u - 1f) * (u - 1f);
    }

    float RootAngle(Tentacle tt, float x)
    {
        var sw = tt.swings;
        float v = sw.Count > 0 ? sw[0].from : 0f;
        for (int i = 0; i < sw.Count; i++)
        {
            var s = sw[i];
            if (x < s.t0) break;
            float u = (x - s.t0) / s.dur;
            v = u >= 1f ? s.to : s.from + (s.to - s.from) * EaseOutBack(u);
        }
        return v;
    }

    static float Noise(float row, float x) => Mathf.PerlinNoise(row, x) * 2f - 1f;

    /// <summary>這條觸手在時間 t 的每節角度（往外座標）與伸縮。</summary>
    float Pose(Tentacle tt, float t)
    {
        int n = _ang.Length;
        float ph = t * 1.8f;
        float curl = 240f * Noise(tt.nCurlA, ph * 0.45f) + 96f * Noise(tt.nCurlB, ph * 0.8f);
        for (int i = 0; i < n; i++)
        {
            float s = i / (float)(n - 1);
            float td = t - s * tt.lag;
            float a = RootAngle(tt, td);
            float vel = (a - RootAngle(tt, td - 0.03f)) / 0.03f;
            float drag = -Mathf.Clamp(vel, -DragVelClamp, DragVelClamp) * Drag * Mathf.Pow(s, 1.5f);
            float wave = 26f * Mathf.Sin(ph * tt.wf * Mathf.PI * 2f * 0.35f + tt.seed - s * tt.wl) * Mathf.Pow(s, 0.8f)
                       + 10f * Mathf.Sin(ph * tt.wf2 + tt.seed * 2f - s * 3.5f) * s * s;
            _ang[i] = a + drag + curl * Mathf.Pow(s, 1.6f) + wave * 0.6f;
        }
        return 0.86f + 0.14f * (0.5f + 0.5f * Noise(tt.nLen, ph * 0.5f));
    }

    void BuildMesh()
    {
        float H = _pc != null ? _pc.ScaledCharacterHeight : 2f;
        Vector2 feet = _pc != null ? _pc.FeetWorldPos : (Vector2)transform.position;
        bool faceRight = true;
        if (_hostSr != null && _pc != null) faceRight = _hostSr.flipX != _pc.SpriteSourceFacesRight;
        float facing = faceRight ? 1f : -1f;

        _v.Clear(); _uv0.Clear(); _uv1.Clear(); _uv2.Clear(); _c.Clear(); _tri.Clear();
        _w2l = _go.transform.worldToLocalMatrix;   // 頂點算成世界座標再換回物件座標（父節點有縮放也不會歪）

        // 畫的順序：殘影先、腰的觸手後（較靠前）
        for (int pass = 0; pass < 2; pass++)
        {
            for (int li = 0; li < Layers.Length; li++)
            {
                foreach (var tt in _tents)
                {
                    if (tt.layer != Layers[li]) continue;
                    float ta = _t - tt.formDelay;
                    float form = Mathf.Clamp01(ta / Mathf.Max(0.01f, FormTime));
                    float gone = Mathf.Clamp01((ta - FormTime - ShowTime) / Mathf.Max(0.01f, FadeTime));
                    if (form <= 0f || gone >= 1f) continue;
                    float speed = Mathf.Abs(RootAngle(tt, _t) - RootAngle(tt, _t - 0.05f)) / 0.05f;
                    if (pass == 0)
                    {
                        if (speed <= EchoSpeed) continue;
                        float op = EchoOpacity * Mathf.Clamp01((speed - EchoSpeed) / 300f);
                        AddRibbon(tt, _t - EchoDelay, op, form, gone, H, feet, facing);
                    }
                    else
                    {
                        float op = OpacityBase + OpacityBoost * Mathf.Clamp01(speed / OpacityFullSpeed);
                        AddRibbon(tt, _t, op, form, gone, H, feet, facing);
                    }
                }
            }
        }

        _mesh.Clear();
        _mesh.SetVertices(_v);
        _mesh.SetUVs(0, _uv0);
        _mesh.SetUVs(1, _uv1);
        _mesh.SetUVs(2, _uv2);
        _mesh.SetColors(_c);
        _mesh.SetTriangles(_tri, 0, false);
        _mesh.RecalculateBounds();

        float baseY = transform.position.y + (_ysort != null ? _ysort.FeetYOffset : 0f);
        if (_hostSr != null) _mr.sortingLayerID = _hostSr.sortingLayerID;
        _mr.sortingOrder = MapDepthSort.Order(baseY, 0) + SortOffset;
        _mr.enabled = _v.Count > 0;
    }

    void AddRibbon(Tentacle tt, float t, float opacity, float form, float gone, float H, Vector2 feet, float facing)
    {
        var L = tt.layer;
        int n = _pts.Length;
        float lk = Pose(tt, t);
        float ds = L.Length * lk / (n - 1);

        // 正向運動學（往外座標，角色高＝1，腳底 y＝0）
        _pts[0] = new Vector2(0f, L.RootY);
        for (int i = 1; i < n; i++)
        {
            float th = _ang[i - 1] * Mathf.Deg2Rad;
            _pts[i] = _pts[i - 1] + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * ds;
        }

        float rootX = tt.side < 0f ? L.BackX : L.FrontX;
        float mx = facing * tt.side;              // 往外座標的 x → 世界 x 的倍率（含鏡射）
        int baseIdx = _v.Count;
        float u = 0f, sl = 0f;
        for (int i = 0; i < n; i++)
        {
            float s = i / (float)(n - 1);
            Vector2 tan = i < n - 1 ? _pts[i + 1] - _pts[i] : _pts[i] - _pts[i - 1];
            tan.Normalize();
            Vector2 back = new Vector2(-tan.y, tan.x);   // 往外座標的「背」（逆時針法線）
            float half = L.Width * (1f - TipTaper * Mathf.Pow(s, 1.1f));
            if (i > 0)
            {
                float seg = (_pts[i] - _pts[i - 1]).magnitude;
                float halfPrev = L.Width * (1f - TipTaper * Mathf.Pow((i - 1) / (float)(n - 1), 1.1f));
                u += seg / Mathf.Max(1e-4f, (half + halfPrev) * _skinAspect);   // ∫ds ÷ (皮厚×寬高比)；皮厚＝2×半寬
                sl += seg;
            }
            Vector2 p = _pts[i];
            Vector2 wp = feet + new Vector2((rootX * facing + mx * p.x) * H, p.y * H);
            Vector2 wn = new Vector2(mx * back.x, back.y) * (half * GlowWidth * H);
            var col = new Color(s, opacity, 0f, 1f);
            _v.Add(_w2l.MultiplyPoint3x4(wp - wn)); _uv0.Add(new Vector2(u, -1f)); _uv1.Add(new Vector2(sl, form)); _uv2.Add(new Vector2(gone, tt.seed)); _c.Add(col);
            _v.Add(_w2l.MultiplyPoint3x4(wp + wn)); _uv0.Add(new Vector2(u, 1f));  _uv1.Add(new Vector2(sl, form)); _uv2.Add(new Vector2(gone, tt.seed)); _c.Add(col);
            if (i > 0)
            {
                int k = baseIdx + (i - 1) * 2;
                _tri.Add(k); _tri.Add(k + 1); _tri.Add(k + 3);
                _tri.Add(k); _tri.Add(k + 3); _tri.Add(k + 2);
            }
        }
    }

    void Clear()
    {
        if (_go != null) Destroy(_go);
        _go = null; _mr = null;
        if (_mesh != null) { Destroy(_mesh); _mesh = null; }
        if (_mat != null) { Destroy(_mat); _mat = null; }
        _tents.Clear();
        _t = -1f;
        _built = false;
    }

    void OnDisable() { Clear(); }
    void OnDestroy() { Clear(); }
}
