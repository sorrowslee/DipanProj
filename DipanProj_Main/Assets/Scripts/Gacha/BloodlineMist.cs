using UnityEngine;

/// <summary>
/// 第三階血統的「身後黑霧」——身上不斷往上冒的黑霧，霧裡會週期性凝結出骷髏頭、停一下再散回霧裡。
/// 目前只有萬魂系列第三階 酆都大帝（表B `MistStyle` = <c>SoulSkull</c>）。見 readme/BLOODLINE.md §5b。
///
/// ── 是哪幾條既有結論拼起來的 ──
/// ① **shader 骨架抄 <c>Custom/BloodMist</c>（血霧衛士）**：兩張美術畫的煙霧密度圖
///    （Resources/UI/BossIntroPanel/BossIntroPanel_Smoke1/2）＋ domain warp。⚠ 不要改成純程序 noise
///    （BossAura 檔頭結論：fbm 生不出煙的絲與捲）。新開一支 <c>Custom/BloodlineMist</c>、原 BloodMist 不動。
/// ② **獨立物件＋畫在角色之下＋位置大小用 BodyCenterWorldPos／ScaledCharacterHeight**：完全照
///    <see cref="BloodlineHalo"/>（PROBLEMS E13、E14）。畫在下面 ⇒ 中央被身體擋住，霧只從輪廓冒出來。
/// ③ **趴著／倒下時整層關掉**（<c>PlayerAnimator.BodyFxVisible</c>），同 Halo。
/// ④ 不需要掛進 <c>PlayerController.RefreshBodyScaledVisuals()</c>：本元件每幀讀 ScaledCharacterHeight，體型變了自然會跟。
///
/// ── 骷髏 ──
/// 貼圖 Resources/VfxEffects/BloodlineMist/soul_skull（256×256，RGB＝灰階亮度、A＝形狀，ChatGPT 產＋去背）。
/// 一輪：等待（隨機）→ 凝結（shader 用煙霧密度圖當溶解門檻，霧一絲一絲聚成形）→ 停留 → 上方先散、整顆往上飄。
/// 左右肩後輪流出現。全部用 Time.deltaTime，開面板暫停時跟著凍住（shader 的 _Time 也跟 timeScale 走）。
///
/// ⚠ 顏色在 shader 的 Properties（Linear 色彩空間，E11），不在表格——目前只有一個樣式，等第二個血統要用再拉出欄位。
/// </summary>
[DisallowMultipleComponent]
public class BloodlineMist : MonoBehaviour
{
    const string ShaderPath = "Shaders/BloodlineMist";
    const string SmokeTexAPath = "UI/BossIntroPanel/BossIntroPanel_Smoke1";   // 厚重霧體
    const string SmokeTexBPath = "UI/BossIntroPanel/BossIntroPanel_Smoke2";   // 細絮煙流
    const string SkullTexPath = "VfxEffects/BloodlineMist/soul_skull";

    [Header("霧團大小與位置（× 角色高度）")]
    /// <summary>霧的 quad 寬 = 角色高度 × 此值。2026-11-09 用該隱 idle 在 numpy 預覽裡定的。</summary>
    public float WidthFactor = 1.7f;
    /// <summary>霧的 quad 高 = 角色高度 × 此值。2026-11-09 實機：2.4 時霧頂約 1.5 個酆都高，作者要約 1.2 ⇒ 底部不動、頂端往下收成 1.55。</summary>
    public float HeightFactor = 1.55f;
    /// <summary>quad 中心相對可見身體中心往上偏移「角色高度 × 此值」（負＝往下）。與 HeightFactor 一起改，維持 quad 底部在原位。</summary>
    public float CenterAboveBody = -0.125f;
    /// <summary>煙紋跟著世界位置走的比例（移動時霧往後流）。0 = 霧黏在身上。同 BloodMistVisual。</summary>
    public float WorldFlowFollow = 0.35f;

    [Header("出現")]
    /// <summary>變身／換血統後霧由下往上長出來的秒數。</summary>
    public float FadeInTime = 0.8f;

    [Header("骷髏（MistStyle = SoulSkull 才有）")]
    public float SkullWaitMin = 2.5f;
    public float SkullWaitMax = 4.5f;
    public float SkullFormTime = 1.2f;
    public float SkullHoldTime = 1.0f;
    public float SkullFadeTime = 1.5f;
    /// <summary>骷髏中心（霧 quad 的 uv）。左肩後；另一側自動鏡射成 1 - x。</summary>
    public Vector2 SkullUv = new Vector2(0.37f, 0.74f);   // 2026-11-09 霧收矮後換算：約在肩膀高度
    /// <summary>骷髏大小（佔 quad 寬的比例；shader 用 _SkullPos.w＝寬/高 修正，世界裡是正方形）。</summary>
    public float SkullSize = 0.24f;
    /// <summary>從停留到散光，整顆往上飄多少（uv）。</summary>
    public float SkullDrift = 0.14f;   // 霧收矮後換算，世界裡的飄移量不變

    /// <summary>相對角色的排序偏移。-1 = 畫在角色之下。⚠ 改成正值霧會蓋住角色（E13 同類問題）。</summary>
    public int SortOffset = -1;

    string _style;
    bool _skulls;

    PlayerController _pc;
    PlayerAnimator _anim;
    YSortByFeet _ysort;

    GameObject _go;
    SpriteRenderer _sr;
    Sprite _sprite;
    Texture2D _quadTex;
    Material _mat;
    MaterialPropertyBlock _mpb;

    float _fade;
    float _seed;

    // 骷髏狀態機
    enum SkullPhase { Wait, Form, Hold, Fade }
    SkullPhase _phase = SkullPhase.Wait;
    float _phaseT;
    float _waitFor;
    bool _rightSide;

    void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _anim = GetComponent<PlayerAnimator>();
        _ysort = GetComponent<YSortByFeet>();
        _mpb = new MaterialPropertyBlock();
        _seed = Random.Range(0f, 10f);
    }

    /// <summary>設定樣式；留空 = 關掉這一層。重複傳同樣的值不會重建。</summary>
    public void SetStyle(string style)
    {
        style = string.IsNullOrWhiteSpace(style) ? null : style.Trim();
        if (style == null) { _style = null; Clear(); return; }
        if (style == _style && _go != null) return;
        _style = style;
        _skulls = style.ToLowerInvariant() == "soulskull";
        _fade = 0f;
        ResetSkull();
        Build();
    }

    void ResetSkull()
    {
        _phase = SkullPhase.Wait;
        _phaseT = 0f;
        _waitFor = Random.Range(0.8f, 1.6f);   // 第一顆來得快一點，變身完馬上看得到
    }

    void Build()
    {
        if (_go != null) return;
        _go = new GameObject("BloodlineMist");
        // ⚠ 獨立物件、不是玩家子物件（同 BloodlineHalo／BlobShadow：子物件會被角色 localScale 二次影響）。
        _go.transform.SetParent(transform.parent, false);
        _sr = _go.AddComponent<SpriteRenderer>();

        // 1×1 白點、PPU 1 ⇒ 世界尺寸 1×1、uv 0~1；大小全由 localScale 決定（同 BloodMistVisual／GroundCrackFx）。
        _quadTex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        _quadTex.SetPixel(0, 0, Color.white);
        _quadTex.Apply();
        _sprite = Sprite.Create(_quadTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        _sprite.name = "BloodlineMistQuad";
        _sprite.hideFlags = HideFlags.HideAndDontSave;
        _sr.sprite = _sprite;

        // 走 Resources.Load（專案慣例）——Shader.Find 打包後容易是 null。
        var sh = Resources.Load<Shader>(ShaderPath);
        if (sh == null)
        {
            Debug.LogWarning($"[BloodlineMist] 找不到 Resources/{ShaderPath}，黑霧不顯示。");
            _sr.enabled = false;
            return;
        }
        _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        var texA = LoadTex(SmokeTexAPath);
        var texB = LoadTex(SmokeTexBPath);
        if (texA == null || texB == null)
            Debug.LogWarning($"[BloodlineMist] 找不到煙霧密度圖 Resources/{SmokeTexAPath} 或 {SmokeTexBPath}。");
        else { _mat.SetTexture("_SmokeA", texA); _mat.SetTexture("_SmokeB", texB); }
        var skull = LoadTex(SkullTexPath);
        if (skull != null) _mat.SetTexture("_SkullTex", skull);
        else if (_skulls) Debug.LogWarning($"[BloodlineMist] 找不到骷髏貼圖 Resources/{SkullTexPath}，只會有黑霧。");
        _sr.material = _mat;
    }

    // 圖若以 Sprite 匯入，Texture2D 載不到就從 Sprite 拿（同 BossIntroPanel／BloodMistVisual.LoadSmokeTex）
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
        if (_go == null) { Build(); return; }
        if (_mat == null) return;

        if (_anim == null) _anim = GetComponent<PlayerAnimator>();
        bool visible = _anim == null || _anim.BodyFxVisible;
        if (_sr.enabled != visible) _sr.enabled = visible;
        if (!visible) { _fade = 0f; return; }   // 爬起來後重新由下往上長出來

        float dt = Time.deltaTime;
        _fade = Mathf.MoveTowards(_fade, 1f, dt / Mathf.Max(0.01f, FadeInTime));

        // ── 位置與大小 ──
        float h = _pc != null ? _pc.ScaledCharacterHeight : 2f;
        Vector2 c = _pc != null ? _pc.BodyCenterWorldPos : (Vector2)transform.position;
        float qw = Mathf.Max(0.01f, h * WidthFactor), qh = Mathf.Max(0.01f, h * HeightFactor);
        _go.transform.position = new Vector3(c.x, c.y + h * CenterAboveBody, 0f);
        _go.transform.localScale = new Vector3(qw, qh, 1f);

        // ── 排序：與角色同一條算式，-1 沉到角色之下 ──
        float baseY = transform.position.y + (_ysort != null ? _ysort.FeetYOffset : 0f);
        _sr.sortingOrder = MapDepthSort.Order(baseY, 0) + SortOffset;

        // ── 骷髏 ──
        float skullT = -1f;
        Vector4 skullPos = Vector4.zero;
        if (_skulls) TickSkull(dt, out skullT, out skullPos);

        Vector2 worldUv = new Vector2(c.x / qw, c.y / qh) * WorldFlowFollow;
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetFloat("_Fade", _fade);
        _mpb.SetFloat("_Seed", _seed);
        _mpb.SetVector("_WorldOffset", new Vector4(worldUv.x, worldUv.y, 0f, 0f));
        _mpb.SetFloat("_SkullT", skullT);
        _mpb.SetVector("_SkullPos", skullPos);
        _sr.SetPropertyBlock(_mpb);
    }

    void TickSkull(float dt, out float skullT, out Vector4 pos)
    {
        _phaseT += dt;
        switch (_phase)
        {
            case SkullPhase.Wait:
                if (_phaseT >= _waitFor) { _phase = SkullPhase.Form; _phaseT = 0f; }
                break;
            case SkullPhase.Form:
                if (_phaseT >= SkullFormTime) { _phase = SkullPhase.Hold; _phaseT = 0f; }
                break;
            case SkullPhase.Hold:
                if (_phaseT >= SkullHoldTime) { _phase = SkullPhase.Fade; _phaseT = 0f; }
                break;
            case SkullPhase.Fade:
                if (_phaseT >= SkullFadeTime)
                {
                    _phase = SkullPhase.Wait; _phaseT = 0f;
                    _waitFor = Random.Range(SkullWaitMin, Mathf.Max(SkullWaitMin, SkullWaitMax));
                    _rightSide = !_rightSide;   // 左右肩輪流
                }
                break;
        }

        float drift = 0f;
        switch (_phase)
        {
            case SkullPhase.Form: skullT = _phaseT / Mathf.Max(0.01f, SkullFormTime); break;
            case SkullPhase.Hold:
                skullT = 1f;
                drift = SkullDrift * 0.3f * (_phaseT / Mathf.Max(0.01f, SkullHoldTime));
                break;
            case SkullPhase.Fade:
            {
                float f = _phaseT / Mathf.Max(0.01f, SkullFadeTime);
                skullT = 1f + f;
                drift = SkullDrift * (0.3f + 0.7f * f);
                break;
            }
            default: skullT = -1f; break;
        }

        float x = _rightSide ? 1f - SkullUv.x : SkullUv.x;
        pos = new Vector4(x, SkullUv.y + drift, SkullSize, WidthFactor / Mathf.Max(0.01f, HeightFactor));
    }

    void Clear()
    {
        if (_sr != null) _sr.sprite = null;
        if (_sprite != null) { Destroy(_sprite); _sprite = null; }
        if (_quadTex != null) { Destroy(_quadTex); _quadTex = null; }
        if (_mat != null) { Destroy(_mat); _mat = null; }
        if (_go != null) { Destroy(_go); _go = null; }
        _sr = null;
    }

    void OnDisable() { Clear(); }
    void OnDestroy() { Clear(); }
}
