using UnityEngine;

/// <summary>
/// 第三階血統的「身邊幽靈」——兩隻只有頭、拖著墨煙尾巴的水墨幽靈，一左一右在角色身邊若隱若現：
/// 有時只出左邊、有時只出右邊、有時兩隻一起。目前只有暴食系列第三階 饕餮（表B `GhostStyle` = <c>TaotieInk</c>）。
/// 見 readme/BLOODLINE.md §5b-2〈`GhostStyle`：身邊幽靈〉。
///
/// ── 是哪幾條既有結論拼起來的（同 <see cref="BloodlineMist"/>）──
/// ① **溶解與尾巴擺動用美術畫的煙霧密度圖**（Resources/UI/BossIntroPanel/BossIntroPanel_Smoke1/2），不用純程序 noise。
/// ② **獨立物件＋位置大小用 BodyCenterWorldPos／ScaledCharacterHeight**（PROBLEMS E13、E14），體型變了每幀自然跟上，
///    不必掛進 <c>PlayerController.RefreshBodyScaledVisuals()</c>。
/// ③ **趴著／倒下時整層關掉**（<c>PlayerAnimator.BodyFxVisible</c>）。
/// ④ 全部用 Time.deltaTime，開面板暫停時跟著凍住（shader 的 _Time 也跟 timeScale 走）。
///
/// ── 一輪 ──
/// 等待（隨機）→ 抽這一輪出哪邊（左／右／兩隻，<see cref="BothChance"/>）→ 每隻各自：
/// 凝結（煙霧圖當溶解門檻，墨一絲一絲聚成形）→ 停留（隨機長短，上下浮動、尾巴像煙一樣擺）→ 尾巴先散、整隻往上飄 → 全部散完再等下一輪。
///
/// ⚠ 顏色在 shader 的 Properties（Linear 色彩空間，E11），不在表格——目前只有一個樣式。
/// </summary>
[DisallowMultipleComponent]
public class BloodlineGhost : MonoBehaviour
{
    const string ShaderPath = "Shaders/BloodlineGhost";
    const string SmokeTexAPath = "UI/BossIntroPanel/BossIntroPanel_Smoke1";   // 厚重霧體
    const string SmokeTexBPath = "UI/BossIntroPanel/BossIntroPanel_Smoke2";   // 細絮煙流
    const string GhostTexPath = "VfxEffects/BloodlineGhost/taotie_ghost";

    [Header("大小與位置（× 角色高度）")]
    /// <summary>單隻幽靈 quad 的邊長 = 角色高度 × 此值（正方形）。</summary>
    public float GhostSize = 0.6f;
    /// <summary>幽靈中心離身體中心的水平距離。</summary>
    public float SideOffset = 0.56f;
    /// <summary>幽靈中心比身體中心高多少。</summary>
    public float HeightAboveBody = 0.22f;
    /// <summary>上下浮動幅度與速度。</summary>
    public float BobAmount = 0.035f;
    public float BobSpeed = 1.1f;
    /// <summary>左右晃蕩幅度（× 角色高度）與速度（每秒幾趟）。兩個頻率疊加，看起來不像鐘擺；兩隻各自用自己的相位。</summary>
    public float SwayAmount = 0.08f;
    public float SwaySpeed = 0.28f;
    /// <summary>散去時整隻往上飄多少。</summary>
    public float FadeDrift = 0.10f;
    /// <summary>貼圖裡的尾巴往哪邊飄：true＝往左。taotie_ghost 的墨煙是往右上飄 ⇒ false，左邊那隻會鏡射，讓兩隻的尾巴都往外飄。</summary>
    public bool TailToLeftInTexture = false;

    [Header("出現節奏（秒）")]
    public float WaitMin = 1.5f;
    public float WaitMax = 3.5f;
    public float FormTime = 1.0f;
    public float HoldMin = 1.6f;
    public float HoldMax = 3.2f;
    public float FadeTime = 1.3f;
    /// <summary>每一輪「兩隻一起出」的機率；其餘左右各半。</summary>
    [Range(0f, 1f)] public float BothChance = 0.3f;
    /// <summary>兩隻一起時，第二隻晚多久出現（看起來不像同步複製）。</summary>
    public float BothStagger = 0.4f;

    /// <summary>相對角色的排序偏移。-1 = 畫在角色之下（幽靈與身體重疊時不蓋住角色）。</summary>
    public int SortOffset = -1;

    enum Phase { Off, Delay, Form, Hold, Fade }

    class Ghost
    {
        public GameObject go;
        public SpriteRenderer sr;
        public Phase phase = Phase.Off;
        public float t;
        public float delay;
        public float hold;
        public float seed;
        public bool right;
    }

    string _style;
    PlayerController _pc;
    PlayerAnimator _anim;
    YSortByFeet _ysort;

    readonly Ghost[] _ghosts = new Ghost[2];
    Sprite _sprite;
    Texture2D _quadTex;
    Material _mat;
    MaterialPropertyBlock _mpb;
    bool _built;
    float _wait;

    void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _anim = GetComponent<PlayerAnimator>();
        _ysort = GetComponent<YSortByFeet>();
        _mpb = new MaterialPropertyBlock();
    }

    /// <summary>設定樣式；留空 = 關掉這一層。重複傳同樣的值不會重建。</summary>
    public void SetStyle(string style)
    {
        style = string.IsNullOrWhiteSpace(style) ? null : style.Trim();
        if (style == null) { _style = null; Clear(); return; }
        if (style == _style && _built) return;
        _style = style;
        _wait = Random.Range(0.6f, 1.2f);   // 第一輪來得快一點，變身完馬上看得到
        Build();
    }

    void Build()
    {
        if (_built) return;
        _built = true;

        // 1×1 白點、PPU 1 ⇒ 世界尺寸 1×1、uv 0~1；大小全由 localScale 決定（同 BloodlineMist）。
        _quadTex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        _quadTex.SetPixel(0, 0, Color.white);
        _quadTex.Apply();
        _sprite = Sprite.Create(_quadTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        _sprite.name = "BloodlineGhostQuad";
        _sprite.hideFlags = HideFlags.HideAndDontSave;

        // 走 Resources.Load（專案慣例）——Shader.Find 打包後容易是 null。
        var sh = Resources.Load<Shader>(ShaderPath);
        if (sh == null) Debug.LogWarning($"[BloodlineGhost] 找不到 Resources/{ShaderPath}，幽靈不顯示。");
        else
        {
            _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            var texA = LoadTex(SmokeTexAPath);
            var texB = LoadTex(SmokeTexBPath);
            if (texA == null || texB == null)
                Debug.LogWarning($"[BloodlineGhost] 找不到煙霧密度圖 Resources/{SmokeTexAPath} 或 {SmokeTexBPath}。");
            else { _mat.SetTexture("_SmokeA", texA); _mat.SetTexture("_SmokeB", texB); }
            var ghost = LoadTex(GhostTexPath);
            if (ghost != null) _mat.SetTexture("_GhostTex", ghost);
            else Debug.LogWarning($"[BloodlineGhost] 找不到幽靈貼圖 Resources/{GhostTexPath}，幽靈不顯示。");
        }

        for (int i = 0; i < 2; i++)
        {
            var g = new Ghost { right = i == 1, seed = Random.Range(0f, 10f) };
            g.go = new GameObject(i == 0 ? "BloodlineGhost_L" : "BloodlineGhost_R");
            // ⚠ 獨立物件、不是玩家子物件（同 BloodlineHalo／BloodlineMist：子物件會被角色 localScale 二次影響）。
            g.go.transform.SetParent(transform.parent, false);
            g.sr = g.go.AddComponent<SpriteRenderer>();
            g.sr.sprite = _sprite;
            if (_mat != null) g.sr.sharedMaterial = _mat;
            g.sr.enabled = false;
            _ghosts[i] = g;
        }
    }

    // 圖若以 Sprite 匯入，Texture2D 載不到就從 Sprite 拿（同 BloodlineMist.LoadTex）
    static Texture2D LoadTex(string path)
    {
        var tex = Resources.Load<Texture2D>(path);
        if (tex != null) return tex;
        var spr = Resources.Load<Sprite>(path);
        return spr != null ? spr.texture : null;
    }

    void LateUpdate()
    {
        if (_style == null) return;
        if (!_built) { Build(); return; }
        if (_mat == null) return;

        if (_anim == null) _anim = GetComponent<PlayerAnimator>();
        bool visible = _anim == null || _anim.BodyFxVisible;
        if (!visible)
        {
            // 趴下／倒地：整層收掉，爬起來後重新排一輪
            foreach (var g in _ghosts) { g.phase = Phase.Off; g.sr.enabled = false; }
            _wait = Random.Range(0.6f, 1.2f);
            return;
        }

        float dt = Time.deltaTime;
        TickSchedule(dt);

        float h = _pc != null ? _pc.ScaledCharacterHeight : 2f;
        Vector2 c = _pc != null ? _pc.BodyCenterWorldPos : (Vector2)transform.position;
        float size = Mathf.Max(0.01f, h * GhostSize);
        float baseY = transform.position.y + (_ysort != null ? _ysort.FeetYOffset : 0f);
        int order = MapDepthSort.Order(baseY, 0) + SortOffset;

        foreach (var g in _ghosts)
        {
            float ghostT = TickGhost(g, dt, out float drift);
            bool on = ghostT >= 0f;
            if (g.sr.enabled != on) g.sr.enabled = on;
            if (!on) continue;

            float side = g.right ? 1f : -1f;
            float bob = Mathf.Sin((Time.time + g.seed * 3.1f) * BobSpeed * Mathf.PI * 2f * 0.5f) * BobAmount;
            // 左右晃蕩：主頻＋一個不成整數比的副頻（0.7 : 0.3），飄得不規則；左右兩隻相位不同，不會同步擺
            float ph = Time.time * SwaySpeed * Mathf.PI * 2f;
            float sway = (Mathf.Sin(ph + g.seed * 1.9f) * 0.7f + Mathf.Sin(ph * 1.73f + g.seed * 4.3f) * 0.3f) * SwayAmount;
            g.go.transform.position = new Vector3(
                c.x + side * h * SideOffset + h * sway,
                c.y + h * (HeightAboveBody + bob + drift), 0f);
            g.go.transform.localScale = new Vector3(size, size, 1f);
            g.sr.sortingOrder = order;

            // 鏡射：讓兩隻的尾巴都往外飄（左邊那隻尾巴往左、右邊往右）
            bool flip = g.right == TailToLeftInTexture;
            g.sr.GetPropertyBlock(_mpb);
            _mpb.SetFloat("_GhostT", ghostT);
            _mpb.SetFloat("_Seed", g.seed);
            _mpb.SetFloat("_Flip", flip ? 1f : 0f);
            g.sr.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>兩隻都散完了才倒數下一輪；倒數到了抽「左／右／兩隻」。</summary>
    void TickSchedule(float dt)
    {
        if (_ghosts[0].phase != Phase.Off || _ghosts[1].phase != Phase.Off) return;
        _wait -= dt;
        if (_wait > 0f) return;
        _wait = Random.Range(WaitMin, Mathf.Max(WaitMin, WaitMax));

        if (Random.value < BothChance)
        {
            int first = Random.value < 0.5f ? 0 : 1;
            Begin(_ghosts[first], 0f);
            Begin(_ghosts[1 - first], BothStagger);
        }
        else Begin(_ghosts[Random.value < 0.5f ? 0 : 1], 0f);
    }

    void Begin(Ghost g, float delay)   // ⚠ 不要叫 Start：Unity 會把帶參數的 Start 當訊息方法報錯
    {
        g.phase = delay > 0f ? Phase.Delay : Phase.Form;
        g.t = 0f;
        g.delay = delay;
        g.hold = Random.Range(HoldMin, Mathf.Max(HoldMin, HoldMax));
        g.seed = Random.Range(0f, 10f);   // 每次出現溶解紋路都不一樣
    }

    /// <summary>回傳給 shader 的進度：0→1 凝結、1 停留、1→2 散去；&lt;0 = 不顯示。</summary>
    float TickGhost(Ghost g, float dt, out float drift)
    {
        drift = 0f;
        g.t += dt;
        switch (g.phase)
        {
            case Phase.Off: return -1f;
            case Phase.Delay:
                if (g.t >= g.delay) { g.phase = Phase.Form; g.t = 0f; }
                return -1f;
            case Phase.Form:
                if (g.t >= FormTime) { g.phase = Phase.Hold; g.t = 0f; return 1f; }
                return g.t / Mathf.Max(0.01f, FormTime);
            case Phase.Hold:
                if (g.t >= g.hold) { g.phase = Phase.Fade; g.t = 0f; }
                return 1f;
            case Phase.Fade:
            {
                if (g.t >= FadeTime) { g.phase = Phase.Off; g.t = 0f; return -1f; }
                float f = g.t / Mathf.Max(0.01f, FadeTime);
                drift = FadeDrift * f;
                return 1f + f;
            }
        }
        return -1f;
    }

    void Clear()
    {
        for (int i = 0; i < _ghosts.Length; i++)
        {
            var g = _ghosts[i];
            if (g == null) continue;
            if (g.go != null) Destroy(g.go);
            _ghosts[i] = null;
        }
        if (_sprite != null) { Destroy(_sprite); _sprite = null; }
        if (_quadTex != null) { Destroy(_quadTex); _quadTex = null; }
        if (_mat != null) { Destroy(_mat); _mat = null; }
        _built = false;
    }

    void OnDisable() { Clear(); }
    void OnDestroy() { Clear(); }
}
