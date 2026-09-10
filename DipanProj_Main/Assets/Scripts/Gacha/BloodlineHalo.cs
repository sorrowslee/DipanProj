using UnityEngine;

/// <summary>
/// 第三階血統的「背後圓盤」——神格感的那一圈背光（佛像身光／破碎日輪／殘月／岩輪）。
///
/// **圖是程式畫的，不是素材。** 這裡刻意與 readme/ART_DIRECTION.md 紀律四、以及 BossAura 那支
/// 「純程序 noise 生不出煙的絲與捲、形狀要美術畫」的結論**不衝突**：那條講的是**有機形狀**（煙、火、雲），
/// 而圓盤是**幾何形狀**（同心環、放射線、月牙），程序化畫得又準又好調，也不必再開一輪產圖與去背。
/// 之後若真的要換成手繪圖，只要把 <see cref="BuildSprite"/> 換成 Resources.Load 即可，其餘不動。
///
/// 三個要遵守的既有結論：
/// ① **加色（Blend One One）**：用 <c>Custom/AdditiveGlow</c>。所以貼圖是 premultiplied 白
///    （RGB = alpha），透明區 RGB 必為 0——否則會像佛光那樣疊出一個方塊（readme/PROBLEMS.md **E12**）。
/// ② **畫在角色之下**（SortOffset = -1）。圓盤在背後、中央被角色不透明的身體擋住、只露外圈，
///    所以不會跟身上的加色電光互相洗掉（那個零和陷阱見 **E13**）。⚠ 反過來把圓盤疊到角色**之上**
///    就會踩到 E13，改 SortOffset 之前先讀那一則。
/// ③ **位置用 <c>BodyCenterWorldPos</c>、大小用 <c>ScaledCharacterHeight</c>**，不要用
///    transform.position 或 SpriteRenderer.bounds（readme/PROBLEMS.md **E14**）。
///
/// ⚠ 顏色請填「感覺值的一半」：專案是 Linear 色彩空間，亮色疊在暗底上比直覺重一倍（**E11**）。
///
/// ── 與 <see cref="CharacterGlow"/> 的關係（兩者長得像，但不是同一件事）──
/// <c>CharacterGlow</c> 是**怪物**的體光：一圈柔和的徑向漸層，目的是「讓輪廓在暗地板上浮出來」，
/// 由 MonsterSpawner 統一掛，玩家身上沒有。本元件是**血統的身分表徵**：有形狀（環、放射線、月牙、岩塊）、
/// 會自轉、只給填了 HaloStyle 的血統。兩者都走「程序生成貼圖 ＋ 加色 ＋ 畫在角色之下 ＋ 獨立物件」
/// 這條被驗證過的路子，那些結論是從 CharacterGlow／BlobShadow 沿用過來的，不是各做各的。
/// 差別在 shader：CharacterGlow 用 <c>AuraGlow</c>（會再乘一次貼圖 alpha，配它那張非 premultiplied 的圖），
/// 這裡的貼圖是 premultiplied（透明區 RGB = 0）所以用 <c>AdditiveGlow</c>，不會疊出方塊（**E12**）。
/// </summary>
[DisallowMultipleComponent]
public class BloodlineHalo : MonoBehaviour
{
    public const int TextureSize = 512;

    /// <summary>
    /// 勾選 = **尺寸與位置依樣式自動決定**（見 <see cref="StyleDefaults"/>）。
    /// 因為「身光」（罩住整個身體的大圓，如該隱）與「頭光」（只比頭大一點、貼在腦後，如旱魃）
    /// 本來就是兩種東西，尺寸與位置是樣式的一部分，不該由一組全域數字決定。
    /// 取消勾選才改用下面兩個欄位（微調時用）。
    /// </summary>
    public bool UseStyleDefaults = true;

    /// <summary>圓盤直徑 = 角色高度 × 此倍率。<see cref="UseStyleDefaults"/> 取消勾選時才生效。</summary>
    public float DiameterRatio = 1.15f;

    /// <summary>圓心相對可見身體中心往上偏移「角色高度 × 此比例」。<see cref="UseStyleDefaults"/> 取消勾選時才生效。</summary>
    public float CenterYRatio = 0.18f;

    /// <summary>自轉速度（度/秒，正 = 逆時針）。沿用 SigilPath 那層的 32 度/秒，一圈約 11 秒。</summary>
    public float RotateSpeed = 18f;

    /// <summary>呼吸幅度與速度（刻意慢、幅度小——這是常駐的法，不是搖曳的火）。</summary>
    public float Breathe = 0.03f;
    public float BreatheHz = 0.35f;

    /// <summary>
    /// 環的粗細。0.10 ≈ 細線、0.20 = 目前預設、0.30 以上是厚重的光帶。
    /// 主環的半徑會隨粗細自動往內縮，所以加粗不會把外緣切平。
    /// ⚠ 改這個值會**重畫貼圖**（512×512 逐像素），Play 中拉著調沒問題，但別放進每幀變動的東西裡。
    /// </summary>
    [Range(0.04f, 0.40f)]
    public float RingWidth = 0.20f;

    /// <summary>
    /// 圓盤內部的填充濃度：0 = 只有環（乾淨）、1 = 中間補成一片實心的面。
    /// 對 Cracked 的日面、Crescent 的月面、Disc 的中央柔暈統一生效。
    /// 中央大部分會被角色身體擋住，所以這個值主要影響「身體兩側露出多少面」。
    /// </summary>
    [Range(0f, 1f)]
    public float FillAmount = 0.35f;

    /// <summary>相對角色的排序偏移。-1 = 畫在角色之下（在背後）。⚠ 改成正值會踩到 E13。</summary>
    public int SortOffset = -1;

    /// <summary>加色亮度倍率。⚠ 這個數字騙人——實際亮度是「貼圖 alpha × 顏色 a × 本值」（**E12**）。</summary>
    public float Intensity = 1f;

    string _style;
    string _builtStyle;   // 目前這張貼圖是用哪個樣式畫的（換樣式要重畫）
    float _builtRingWidth = -1f;
    float _builtFill = -1f;
    Color _color = Color.white;

    PlayerController _pc;
    PlayerAnimator _anim;
    YSortByFeet _ysort;

    GameObject _go;
    SpriteRenderer _sr;
    Sprite _sprite;
    Texture2D _tex;
    Material _mat;
    float _spin;

    void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _anim = GetComponent<PlayerAnimator>();
        _ysort = GetComponent<YSortByFeet>();
    }

    /// <summary>設定樣式與顏色；樣式留空 = 關掉這一層。重複傳同樣的值不會重建。</summary>
    public void SetStyle(string style, Color color)
    {
        style = string.IsNullOrWhiteSpace(style) ? null : style.Trim();
        if (style == null) { _style = null; Clear(); return; }
        if (style == _style && _go != null) { _color = color; ApplyColor(); return; }
        _style = style;
        _color = color;
        Rebuild();
    }

    /// <summary>體型改變後重新對齊大小。由 <c>PlayerController.RefreshBodyScaledVisuals()</c> 呼叫。</summary>
    public void Rebuild()
    {
        if (_style == null || !isActiveAndEnabled) { Clear(); return; }

        if (_go == null)
        {
            _go = new GameObject("BloodlineHalo");
            // ⚠ 獨立物件、**不是**玩家的子物件——同 BlobShadow / CharacterGlow 的既有結論：
            //   子物件會被角色的 localScale 二次影響。掛在玩家的父節點下，換圖拆場景時一起被清掉。
            _go.transform.SetParent(transform.parent, false);
            _sr = _go.AddComponent<SpriteRenderer>();

            // 走 Resources.Load 而不是 Shader.Find —— 全專案慣例（見 MapFx/MemoryFxController 的註解）：
            // Shader.Find 只找得到已載入或 Always Included 的 shader，打包後很容易變成 null。
            var sh = Resources.Load<Shader>("Shaders/AdditiveGlow");
            if (sh != null)
            {
                _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                _sr.material = _mat;
            }
            // 找不到 shader 就退回 Sprites-Default（alpha 混合）：亮度感會少一點，但不會整個不見。
        }

        // 貼圖只在樣式變更時重畫（換血統才會發生），體型變更只改 localScale。
        if (_tex == null || _sprite == null || _builtStyle != _style
            || !Mathf.Approximately(_builtRingWidth, RingWidth)
            || !Mathf.Approximately(_builtFill, FillAmount))
        {
            Clear(keepObject: true);
            _sprite = BuildSprite(_style, RingWidth, FillAmount, out _tex);
            _sr.sprite = _sprite;
            _builtStyle = _style;
            _builtRingWidth = RingWidth;
            _builtFill = FillAmount;
        }

        ApplyColor();
    }

    void ApplyColor()
    {
        if (_sr == null) return;
        _sr.color = _color;
        if (_mat != null)
        {
            _mat.SetColor("_TintColor", _color);
            _mat.SetFloat("_Intensity", Mathf.Max(0f, Intensity));
        }
    }

    void LateUpdate()
    {
        if (_style == null) return;
        if (_go == null) { Rebuild(); return; }

        // Play 模式中拉 RingWidth / FillAmount 就即時重畫（只有值真的變了才會進去）。
        if (!Mathf.Approximately(_builtRingWidth, RingWidth)
            || !Mathf.Approximately(_builtFill, FillAmount)) Rebuild();

        if (_anim == null) _anim = GetComponent<PlayerAnimator>();

        // 趴著／倒下／爬起時整層關掉：那時「頭」在水平方向的某一端，
        // 而這裡的定位是「從身體中心往上偏移」——對不到，而且不是調參數能修的。
        // （血統變身整段都在演 dead：人倒下了、頭光還留在原地。）見 PlayerAnimator.BodyFxVisible。
        if (_anim != null && _sr != null) _sr.enabled = _anim.BodyFxVisible;
        if (_anim != null && !_anim.BodyFxVisible) return;

        float h = _pc != null ? _pc.ScaledCharacterHeight : 2f;

        float diaRatio = DiameterRatio, cyRatio = CenterYRatio;
        if (UseStyleDefaults) StyleDefaults(_style, out diaRatio, out cyRatio);

        // 大小：直徑 = 角色高度 × 倍率，再疊上呼吸。貼圖的 PPU = TextureSize，所以 sprite 原生 = 1 世界單位。
        float breathe = 1f + Breathe * Mathf.Sin(Time.time * BreatheHz * Mathf.PI * 2f);
        float d = h * Mathf.Max(0.01f, diaRatio) * breathe;
        _go.transform.localScale = new Vector3(d, d, 1f);

        // 位置：從可見身體中心往上偏移，不是 transform.position（那是畫布中心）。
        Vector2 c = _pc != null ? _pc.BodyCenterWorldPos : (Vector2)transform.position;
        _go.transform.position = new Vector3(c.x, c.y + h * cyRatio, 0f);

        // 自轉。用 Time.deltaTime：開面板暫停時圓盤跟著停（與其他戰鬥特效一致）。
        _spin += RotateSpeed * Time.deltaTime;
        _go.transform.rotation = Quaternion.Euler(0f, 0f, _spin);

        // 排序：與角色同一條算式，-1 沉到角色之下。
        if (_sr != null)
        {
            float baseY = transform.position.y + (_ysort != null ? _ysort.FeetYOffset : 0f);
            _sr.sortingOrder = MapDepthSort.Order(baseY, 0) + SortOffset;
        }
    }

    /// <summary>
    /// 每種樣式的預設尺寸與位置。**身光與頭光是兩種不同的東西**，所以這是樣式的一部分：
    ///
    /// ‧ <c>Disk</c>（頭光）：只比頭大一點、貼在腦後 —— 佛像頭光那種。旱魃用它象徵旱災之神。
    /// ‧ 其餘（身光）：罩住整個身體的大圓 —— 該隱的血色圓盤那種。
    ///
    /// 數字是拿實機截圖量出來的（角色可見高 216px、頭部約落在上緣 75px 內），
    /// 直徑 0.36 ≈ 77px ＝「比他的頭大一圈」，圓心 0.34 ≈ 頭部中心。
    /// </summary>
    static void StyleDefaults(string style, out float diameterRatio, out float centerYRatio)
    {
        switch ((style ?? "").ToLowerInvariant())
        {
            case "disk":
                diameterRatio = 0.36f;   // 比頭大一點
                centerYRatio  = 0.34f;   // 頭部中心（自可見身體中心往上）
                break;
            default:
                diameterRatio = 1.15f;   // 罩住整個身體
                centerYRatio  = 0.18f;   // 胸口偏上
                break;
        }
    }

    // ───────────────────────── 程序化貼圖 ─────────────────────────

    /// <summary>高斯環：r 落在 c 附近時亮、往兩側平滑衰減。用來做「柔暈」那種沒有明確邊界的層。</summary>
    static float Ring(float r, float c, float w)
    {
        float d = (r - c) / Mathf.Max(1e-4f, w);
        return Mathf.Exp(-d * d * 4f);
    }

    /// <summary>
    /// 平頂環帶：以 c 為中心、總寬 w，中間實心、只有邊緣柔化。
    ///
    /// ⚠ **環一定要用這個、不要用 <see cref="Ring"/>。** 高斯環在寬度小的時候看起來像一條線，
    /// 但一加粗就整條糊成一團暈——作者要的是「粗的線」不是「暈開的光」（2026-09-10 實機回饋）。
    /// </summary>
    static float Band(float r, float c, float w, float soft = 0.45f)
    {
        float half = Mathf.Max(1e-4f, w * 0.5f);
        float d = Mathf.Abs(r - c) / half;          // 0 = 正中央、1 = 邊界
        return 1f - SStep(1f - soft, 1f, d);
    }

    /// <summary>
    /// GLSL 語意的 smoothstep：x 由 e0 升到 e1 的過程中平滑地從 0 變成 1。
    ///
    /// ⚠ **不要用 <c>Mathf.SmoothStep</c> 代替。** Unity 那支的語意完全不同——
    /// 它是「用平滑過的 t 在 from 與 to 之間插值」（`SmoothStep(from, to, t)`），
    /// 不是「以 from/to 當門檻對 x 取值」。寫成 `Mathf.SmoothStep(0f, 1f, 已Clamp01的值)`
    /// 剛好等價，但只要門檻不是 0/1 就會靜默算錯。
    /// </summary>
    static float SStep(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / Mathf.Max(1e-6f, e1 - e0));
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// 依樣式畫一張 premultiplied 白（RGB = alpha）的圓盤貼圖。
    /// 染色交給 SpriteRenderer.color 與材質的 _TintColor，所以這裡只決定「形狀與亮度分布」。
    ///
    /// ⚠ **刻意不做 static 快取**（CharacterGlow 有做，因為它一張圖要餵全場的怪；玩家只有一個，不划算）。
    ///   陣列／集合型的 static UnityEngine.Object 快取在「關掉 Domain Reload」的本專案會踩到
    ///   readme/PROBLEMS.md **I8**：容器本身永遠不是 null，`if (x == null) 重建` 的慣例對它不成立，
    ///   第二次 Play 就拿到一整包已銷毀的物件。這裡貼圖跟著元件生滅、OnDestroy 就釋放，天生沒這個問題。
    /// </summary>
    static Sprite BuildSprite(string style, float ringWidth, float fill, out Texture2D tex)
    {
        int n = TextureSize;
        tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,     // 柔和漸層，不是像素圖
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };

        string s = (style ?? "Disc").ToLowerInvariant();
        float w = Mathf.Clamp(ringWidth, 0.04f, 0.40f);
        float fillAmt = Mathf.Clamp01(fill);
        var px = new Color32[n * n];
        float inv = 2f / n;

        for (int y = 0; y < n; y++)
        {
            float ny = (y + 0.5f) * inv - 1f;
            for (int x = 0; x < n; x++)
            {
                float nx = (x + 0.5f) * inv - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                float a;

                if (r >= 1f)
                {
                    a = 0f;
                }
                else
                {
                    float theta = Mathf.Atan2(ny, nx);
                    a = Shape(s, r, theta, nx, ny, w, fillAmt);
                    a *= SStep(0f, 0.06f, 1f - r);        // 外緣柔化，不留硬圓邊
                    a = Mathf.Clamp01(a);
                }

                byte v = (byte)(a * 255f + 0.5f);
                px[y * n + x] = new Color32(v, v, v, v);  // premultiplied：透明區 RGB = 0，加色下不會疊出方塊（E12）
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);

        // PPU = n ⇒ sprite 原生大小剛好 1×1 世界單位，直徑由 localScale 決定。
        var sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
        sp.name = "BloodlineHalo_" + s;
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    static float Shape(string s, float r, float theta, float nx, float ny, float w, float fill)
    {
        // 主環半徑隨粗細往內縮：不縮的話加粗會讓環的外半部撞上 r=1 的外緣柔化而被切平。
        float mainR = 0.86f - (w - 0.10f) * 0.55f;

        // 共用底：主環（實心粗線）＋ 一圈內細環 ＋ 中央柔暈（柔暈吃 FillAmount）。
        float a = 0.90f * Band(r, mainR, w)
                + 0.30f * Band(r, mainR - 0.24f, w * 0.40f)
                + 0.26f * fill * Ring(r, 0.38f, 0.26f);

        switch (s)
        {
            case "cracked":   // 龜裂赤日輪（旱魃）：一片日面被六道放射裂縫切開
            {
                a += 0.68f * fill * Ring(r, 0.52f, 0.36f);          // 日面

                // 裂縫是**乘法**（挖掉），不是加亮——連主環一起切斷，才像整個日輪裂開。
                float c1 = Mathf.Abs(Mathf.Sin(theta * 3f + 0.6f));  // |sin| 週期 π ⇒ 繞一圈 6 條，且接縫處連續
                a *= 0.06f + 0.94f * SStep(0.10f, 0.40f, c1);
                break;
            }

            case "crescent":  // 殘月（芬里爾）：挖掉一塊偏移的圓，留下月牙
            {
                a += 0.60f * fill * Ring(r, 0.58f, 0.28f);
                float dx = nx - 0.45f;
                float rr = Mathf.Sqrt(dx * dx + ny * ny);
                a *= SStep(0.70f, 0.78f, rr);
                break;
            }

            case "stone":     // 岩輪（泰坦）：厚、鈍、分成 12 塊，幾乎不發散
            {
                float blocks = 0.5f + 0.5f * Mathf.Cos(theta * 12f);
                float hard = SStep(0.30f, 0.58f, blocks);            // 硬邊分塊
                a = 1.00f * Band(r, mainR, w * 1.35f, 0.30f) * (0.12f + 0.88f * hard)
                  + 0.30f * Band(r, mainR - 0.30f, w * 0.5f);
                break;
            }

            case "disk":      // 實心圓（旱魃的頭光）：整片飽滿、只有外緣柔化，沒有環
            {
                // 這個樣式裡 RingWidth 改當「外緣柔化的寬度」、FillAmount 當「外緣要不要再亮一圈」。
                a = 1f - SStep(1f - w, 1f, r);
                a *= 0.78f + 0.22f * (1f - r);                       // 中心略亮，才不像一張貼紙
                a += 0.30f * fill * Band(r, 1f - w * 0.7f, w * 0.9f); // 外緣亮輪
                break;
            }

            default:          // Disc（該隱）：乾淨的環，只帶極淡的放射紋
            {
                float rays = 0.5f + 0.5f * Mathf.Cos(theta * 24f);
                a += 0.09f * rays * Band(r, mainR, w * 1.5f);
                break;
            }
        }

        return a;
    }

    // ───────────────────────── 清理 ─────────────────────────

    void Clear(bool keepObject = false)
    {
        if (_sr != null) _sr.sprite = null;
        if (_sprite != null) { Destroy(_sprite); _sprite = null; }
        if (_tex != null) { Destroy(_tex); _tex = null; }
        _builtStyle = null;
        _builtRingWidth = -1f;
        _builtFill = -1f;

        if (!keepObject)
        {
            if (_mat != null) { Destroy(_mat); _mat = null; }
            if (_go != null) { Destroy(_go); _go = null; }
            _sr = null;
        }
    }

    void OnDisable() { Clear(); }
    void OnDestroy() { Clear(); }
}
