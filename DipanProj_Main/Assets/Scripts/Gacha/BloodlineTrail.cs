using UnityEngine;

/// <summary>
/// 第三階血統的「移動殘影」——走動時在身後留下一串逐漸淡去的自己。
///
/// 專案裡沒有現成的殘影機制（<c>TrailEffectID</c> 那套是**子彈**沿路種特效的，不是角色），
/// 所以這是新寫的一層。作法是最省的那種：**不畫新圖**，每隔一段距離把角色**當下那一格 sprite**
/// 複製成一個獨立的 SpriteRenderer 留在原地，染色後淡出自毀。
///
/// 三個要小心的地方：
/// ① **殘影不能掛在玩家底下。** 它要留在「剛剛那個位置」，成了子物件就會跟著玩家一起走。
/// ② **要連 flipX 與 localScale 一起複製。** 角色圖是執行期 <c>Sprite.Create</c> 出來的
///    （腳底 pivot、逐格動畫），只抄 sprite 會得到面向相反或大小不對的殘影。
/// ③ **alpha 要填「感覺值的一半」。** 專案是 Linear 色彩空間，半透明疊色比直覺重一倍
///    （readme/PROBLEMS.md **E11**）。
///
/// 排序沉在角色**與背後圓盤**之下（SortOffset = -2），所以殘影不會蓋住本體。
///
/// 三種樣式。**樣式決定的是「機制與怎麼對齊」，不是「用什麼素材」**——素材一律由 <c>TrailVfxId</c> 指定，
/// 所以換一張圖不必新增樣式（第一版把樣式命名成 <c>Fire</c> 就是這個錯：泰坦放的是衝擊環卻得填 Fire）：
///
/// ‧ <c>Fade</c>：複製角色當下那格 sprite 當虛影。不吃 <c>TrailVfxId</c>。
/// ‧ <c>Step</c>：腳下放一次性 Vfx，**底部貼齊腳底**——給「立起來」的東西（火焰、塵柱）。芬里爾的火焰腳印。
/// ‧ <c>Ground</c>：腳下放一次性 Vfx，**中心對齊腳底**——給「躺平貼地」的東西（衝擊環、裂痕）。泰坦的踏地衝擊環。
///
/// `Step` 與 `Ground` 的差別**只有 pivot 怎麼對**：立起來的東西要把底部放在腳底，
/// 躺平的環則是圓心就在腳的位置。用錯的話，環會整個往上飄半個高度。
/// （<c>Fire</c>／<c>Dust</c> 保留為 <c>Step</c> 的別名，舊資料不會壞。）
/// </summary>
[DisallowMultipleComponent]
public class BloodlineTrail : MonoBehaviour
{
    /// <summary>
    /// 每走「角色高度 × 此比例」的距離留一個。殘影（<c>Fade</c>）一律用它；
    /// 腳步特效**只在拿不到走路動畫幀時**才退回用它（見 <see cref="StepPhaseA"/>）。
    /// </summary>
    public float SpacingRatio = 0.30f;

    /// <summary>
    /// 一個走路循環裡「腳踏地」的兩個相位（0~1）。腳步特效在跨過這兩點時各放一個，
    /// 所以**節奏跟著動畫走、不隨移動速度改變**——泰坦（WalkSpeed 2）與芬里爾（5）差 2.5 倍，
    /// 用「走了多遠」推算的話一定有一邊對不上（作者回報泰坦「特效播太快」就是這個）。
    ///
    /// 預設 0.25 / 0.75 是「一個循環兩次著地」的通例；某個血統的走路動畫節拍不同就調這兩個值。
    /// </summary>
    public float StepPhaseA = 0.25f;
    public float StepPhaseB = 0.75f;

    /// <summary>單個殘影從生成到完全消失的秒數。</summary>
    public float Life = 0.55f;

    /// <summary>殘影的起始不透明度。⚠ Linear 色彩空間下請填「感覺值的一半」（E11）。</summary>
    public float StartAlpha = 0.40f;

    /// <summary>相對角色的排序偏移。-2 = 沉在角色與背後圓盤之下。</summary>
    public int SortOffset = -2;

    /// <summary>同時存在的殘影上限（防呆：極端速度下不會無限生成）。僅 <c>Fade</c> 用。</summary>
    public int MaxGhosts = 24;

    /// <summary>（<c>Fire</c>／<c>Dust</c>）特效高度 = 角色高度 × 此倍率。腳印要小，不然變成一路燒。</summary>
    public float FootFxSizeRatio = 0.20f;

    /// <summary>（<c>Fire</c>／<c>Dust</c>）左右腳相對行進方向的偏移 = 角色高度 × 此倍率。0 = 全部踩在正中線上。</summary>
    public float FootSpread = 0.05f;

    /// <summary>
    /// pivot 補償的**覆寫值**（單位是「特效自己的高度」）。**留 -1 ＝ 依樣式自動決定**：
    /// <c>Step</c> = 0.5（底部貼齊腳底）、<c>Ground</c> = 0（中心對齊腳底）。
    ///
    /// ⚠ 為什麼需要補償：`VfxManager` 生出來的 sprite 是**中心 pivot**，直接擺在 <c>FeetWorldPos</c>
    /// 的話特效會有一半落在腳底線**以下**，看起來像踩在角色前面的地上（2026-09-10 芬里爾踩過）。
    /// 但那只對「立起來」的特效成立——**躺平的衝擊環反而就是要中心對齊**，抬了會整個往上飄。
    /// </summary>
    public float FootFxYRatioOverride = -1f;

    string _style;
    Color _color = Color.white;
    int _vfxId;
    VfxManager _vfx;
    int _footSide = 1;          // 左右腳交替
    float _lastPhase = -1f;     // 上一幀的走路循環相位（-1 = 沒在走／拿不到幀）

    PlayerController _pc;
    PlayerAnimator _anim;
    YSortByFeet _ysort;
    SpriteRenderer _src;

    Vector3 _lastPos;
    float _accum;
    int _alive;
    bool _warnedNoVfx;

    /// <summary>「腳下放 Vfx」那一類（含舊別名）。與 <see cref="IsGroundStyle"/> 的差別只有 pivot 怎麼對。</summary>
    static bool IsStepStyle(string s) => s == "step" || s == "fire" || s == "dust";

    /// <summary>「躺平貼地」那一類（衝擊環、裂痕）：中心對齊腳底。</summary>
    static bool IsGroundStyle(string s) => s == "ground";

    static bool IsFootFxStyle(string s) => IsStepStyle(s) || IsGroundStyle(s);

    void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _anim = GetComponent<PlayerAnimator>();
        _ysort = GetComponent<YSortByFeet>();
        _src = GetComponent<SpriteRenderer>();
        if (_src == null) _src = GetComponentInChildren<SpriteRenderer>();
        _lastPos = transform.position;
    }

    /// <summary>設定樣式與顏色；樣式留空 = 關掉這一層。</summary>
    public void SetStyle(string style, Color color, int vfxId)
    {
        _style = string.IsNullOrWhiteSpace(style) ? null : style.Trim().ToLowerInvariant();
        _color = color;
        _vfxId = vfxId;
        _accum = 0f;
        _lastPos = transform.position;

        // 「放特效」的樣式沒填 VfxTable id 就什麼都不會發生——講明白，不要靜默沒反應。
        if (IsFootFxStyle(_style) && _vfxId <= 0 && !_warnedNoVfx)
        {
            _warnedNoVfx = true;
            Debug.LogWarning($"[BloodlineTrail] TrailStyle={_style} 需要 TrailVfxId（VfxTable 的 id），" +
                             "但血統表那一格是空的 ⇒ 這一層不會有任何表現。");
        }
    }

    void LateUpdate()
    {
        if (_style == null) return;
        bool footFx = IsFootFxStyle(_style);
        if (!footFx && _style != "fade") return;                       // 不認得的樣式
        if (!footFx && (_src == null || _src.sprite == null)) return;
        if (footFx && _vfxId <= 0) return;                             // 沒填素材（SetStyle 已警告過）

        if (_anim == null) _anim = GetComponent<PlayerAnimator>();

        // 趴著／倒下／爬起時不留（同另外兩層）。已經生出來的殘影／火會自己播完，不必回收。
        if (_anim != null && !_anim.BodyFxVisible) { _lastPos = transform.position; _accum = 0f; return; }

        Vector3 pos = transform.position;
        Vector2 delta = (Vector2)pos - (Vector2)_lastPos;
        float moved = delta.magnitude;
        _lastPos = pos;

        // 只有真的在移動時才留（站著不動不該一直冒）。
        if (moved <= 0.0001f) return;

        float h = _pc != null ? _pc.ScaledCharacterHeight : 2f;
        Vector2 dir = delta / moved;

        // ── 何時放一個 ──
        // 腳步特效跟**走路動畫的幀**同步（＝腳真正踏地的節奏）；殘影仍走距離（它要的是均勻的軌跡）。
        int wf = _anim != null ? _anim.WalkFrame : -1;
        int wn = _anim != null ? _anim.WalkFrameCount : 0;

        if (footFx && wf >= 0 && wn >= 4)
        {
            float phase = (float)wf / wn;
            bool step = _lastPhase >= 0f
                        && (CrossedPhase(_lastPhase, phase, StepPhaseA)
                         || CrossedPhase(_lastPhase, phase, StepPhaseB));
            _lastPhase = phase;
            _accum = 0f;
            if (step) SpawnFootFx(dir, h);
            return;
        }

        _lastPhase = -1f;

        // 退路：拿不到走路幀（沒有 walk 圖），或走殘影那條路 ⇒ 回到「走多遠放一個」。
        float spacing = Mathf.Max(0.05f, h * Mathf.Max(0.01f, SpacingRatio));
        _accum += moved;
        if (_accum < spacing) return;
        _accum = 0f;

        if (footFx) SpawnFootFx(dir, h);
        else if (_alive < MaxGhosts) SpawnGhost();
    }

    /// <summary>
    /// 相位是否在這一幀跨過 <paramref name="target"/>。循環會從 0.9 繞回 0.05，所以要分兩種情況判。
    /// </summary>
    static bool CrossedPhase(float prev, float cur, float target)
    {
        if (cur >= prev) return prev < target && target <= cur;   // 正常前進
        return target > prev || target <= cur;                    // 繞回一圈
    }

    /// <summary>
    /// 在腳下放一個一次性的 Vfx（火焰腳印）。**壽命、幀率、大小全部由 VfxTable 那一列決定**，
    /// 這裡只負責「什麼時候放、放在哪」——所以要改火燒多久、多快熄，改 CSV 就好，不用動程式。
    /// </summary>
    void SpawnFootFx(Vector2 dir, float h)
    {
        if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();
        if (_vfx == null) return;

        // 左右腳交替：往**垂直於行進方向**偏一點。用世界 X 軸偏的話，往上下走時就會排成一直線
        // （俯視角可以往任意方向走，不是只有左右）。
        _footSide = -_footSide;
        Vector2 perp = new Vector2(-dir.y, dir.x);

        // 縮放到「角色高度 × 倍率」，所以體型大的血統腳印也大（芬里爾 BodyScale 1.4）。
        float size = h * Mathf.Max(0.01f, FootFxSizeRatio);

        Vector2 p = _pc != null ? _pc.FeetWorldPos : (Vector2)transform.position;
        p += perp * (_footSide * h * FootSpread);

        // pivot 補償：sprite 是中心 pivot。立起來的東西（Step）要把底部放到腳底；
        // 躺平的環（Ground）圓心本來就該在腳的位置，抬了反而會往上飄。
        // ⚠ 實測後 Step 與 Ground 都用 0.5。本來以為「躺平的環該中心對齊腳底」，
        //   但實機看起來一樣偏下（作者：「太下面了，跟之前芬里爾的狀況差不多」）。
        //   原因是 FeetWorldPos 已經是可見身體的**最底緣**，任何往下延伸的東西都會跑到角色前面去。
        //   樣式的區分先留著（語意上仍有意義、也還有覆寫），但預設值目前相同。
        float yRatio = FootFxYRatioOverride >= 0f ? FootFxYRatioOverride : 0.5f;
        p.y += size * yRatio;

        var inst = _vfx.SpawnSizedToHeight(_vfxId, p, size);

        // TrailColor 留空 = 白 = 用素材原色（火本來就有顏色，多半不該再染）。
        if (inst != null && _color != Color.white)
        {
            var sr = inst.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = _color;
        }
    }

    void SpawnGhost()
    {
        var go = new GameObject("BloodlineTrailGhost");
        go.transform.position = _src.transform.position;      // 留在原地——刻意不 SetParent
        go.transform.rotation = _src.transform.rotation;
        go.transform.localScale = _src.transform.lossyScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _src.sprite;                              // 當下那一格（走路動畫的姿勢會被留下來）
        sr.flipX = _src.flipX;
        sr.flipY = _src.flipY;
        sr.sortingLayerID = _src.sortingLayerID;

        float baseY = transform.position.y + (_ysort != null ? _ysort.FeetYOffset : 0f);
        sr.sortingOrder = MapDepthSort.Order(baseY, 0) + SortOffset;

        var c = _color;
        c.a = Mathf.Clamp01(StartAlpha);
        sr.color = c;

        var ghost = go.AddComponent<BloodlineTrailGhost>();
        ghost.Init(Mathf.Max(0.05f, Life), () => _alive--);
        _alive++;
    }
}

/// <summary>一個殘影的生命週期：原地淡出後自毀。由 <see cref="BloodlineTrail"/> 生成。</summary>
public class BloodlineTrailGhost : MonoBehaviour
{
    SpriteRenderer _sr;
    float _life = 0.5f;
    float _startAlpha = 1f;
    float _t;
    System.Action _onGone;

    public void Init(float life, System.Action onGone)
    {
        _sr = GetComponent<SpriteRenderer>();
        _life = Mathf.Max(0.05f, life);
        _startAlpha = _sr != null ? _sr.color.a : 1f;
        _onGone = onGone;
    }

    void Update()
    {
        // 吃 Time.deltaTime：開背包／面板暫停時殘影跟著凍住，與其他戰鬥特效一致。
        _t += Time.deltaTime;

        if (_sr != null)
        {
            var c = _sr.color;
            c.a = _startAlpha * (1f - Mathf.Clamp01(_t / _life));
            _sr.color = c;
        }

        if (_t >= _life) Destroy(gameObject);
    }

    // 回報「我沒了」統一走 OnDestroy：不管是自然淡完、換場景還是被外力銷毀，計數都不會漏掉。
    void OnDestroy()
    {
        var cb = _onGone;
        _onGone = null;
        cb?.Invoke();
    }
}
