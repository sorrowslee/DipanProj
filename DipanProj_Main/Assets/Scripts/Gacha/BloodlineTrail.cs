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
/// </summary>
[DisallowMultipleComponent]
public class BloodlineTrail : MonoBehaviour
{
    /// <summary>每走「角色高度 × 此比例」的距離留一個殘影。數字越小殘影越密。</summary>
    public float SpacingRatio = 0.30f;

    /// <summary>單個殘影從生成到完全消失的秒數。</summary>
    public float Life = 0.55f;

    /// <summary>殘影的起始不透明度。⚠ Linear 色彩空間下請填「感覺值的一半」（E11）。</summary>
    public float StartAlpha = 0.40f;

    /// <summary>相對角色的排序偏移。-2 = 沉在角色與背後圓盤之下。</summary>
    public int SortOffset = -2;

    /// <summary>同時存在的殘影上限（防呆：極端速度下不會無限生成）。</summary>
    public int MaxGhosts = 24;

    string _style;
    Color _color = Color.white;

    PlayerController _pc;
    YSortByFeet _ysort;
    SpriteRenderer _src;

    Vector3 _lastPos;
    float _accum;
    int _alive;
    bool _warnedDust;

    void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _ysort = GetComponent<YSortByFeet>();
        _src = GetComponent<SpriteRenderer>();
        if (_src == null) _src = GetComponentInChildren<SpriteRenderer>();
        _lastPos = transform.position;
    }

    /// <summary>設定樣式與顏色；樣式留空 = 關掉這一層。</summary>
    public void SetStyle(string style, Color color)
    {
        _style = string.IsNullOrWhiteSpace(style) ? null : style.Trim().ToLowerInvariant();
        _color = color;
        _accum = 0f;
        _lastPos = transform.position;

        if (_style == "dust" && !_warnedDust)
        {
            _warnedDust = true;
            Debug.LogWarning("[BloodlineTrail] TrailStyle=Dust（腳下揚塵）還沒接上——" +
                             "它要一組塵土序列圖（特效庫的 fx3_dust_trail_brown 可用），" +
                             "複製進 Resources/VfxEffects/ 並加一列 VfxTable 之後再接。目前這一層不會有任何表現。");
        }
    }

    void LateUpdate()
    {
        if (_style != "fade" || _src == null || _src.sprite == null) return;

        Vector3 pos = transform.position;
        float moved = Vector2.Distance(pos, _lastPos);
        _lastPos = pos;

        // 只有真的在移動時才留殘影（站著不動不該一直冒）。
        if (moved <= 0.0001f) return;

        float h = _pc != null ? _pc.ScaledCharacterHeight : 2f;
        float spacing = Mathf.Max(0.05f, h * Mathf.Max(0.01f, SpacingRatio));

        _accum += moved;
        if (_accum < spacing) return;
        _accum = 0f;

        if (_alive >= MaxGhosts) return;
        SpawnGhost();
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
