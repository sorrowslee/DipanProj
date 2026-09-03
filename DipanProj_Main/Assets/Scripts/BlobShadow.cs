using UnityEngine;

/// <summary>
/// 腳下橢圓影子（blob shadow）：在角色腳下放一個半透明深色橢圓，畫在角色之下、地面之上，每幀跟著角色走。
/// 俯視角 2D 常見做法——不靠即時光照（本專案 Built-in 管線、無 2D 燈光），最省也最穩。
///
/// 掛在任何角色上即可（玩家 / 怪物 / 未來 NPC）：`gameObject.AddComponent&lt;BlobShadow&gt;()`。
/// 影子是**獨立物件**（非子物件，避免被角色翻轉/縮放二次影響），角色銷毀時自動一起清掉。
/// 全程式建構、零 prefab——影子圖用程序生成的柔邊橢圓（一張共用快取）。見 readme/SHADOW.md。
///
/// ── 定位（2026-09-03 起兩條路）──
/// ① **錨點路**：角色身上有 <see cref="IShadowAnchorSource"/>（PlayerAnimator／MonsterAnimator）時，每幀拿「目前動作」的
///    影子錨點（像素、畫布座標；來源是 ShadowAnchorTable.csv 或同一條演算法當場算），再用**當前 sprite** 的
///    PPU／pivot／lossyScale／flipX 換成世界位移。idle 與 walk 的腳在畫布裡不在同一個位置，所以一定要逐動作。
/// ② **舊路**（沒有錨點來源，例如舊 Animator 怪）：Start 量一次 idle 幀的不透明區——X 用 transform、Y 用不透明區下緣。
///    這條路的已知缺陷（idle 偏、walk 準）見 readme/PROBLEMS.md **E28**。
/// </summary>
public class BlobShadow : MonoBehaviour
{
    [Header("影子外觀（可調）")]
    public Color ShadowColor = new Color(0f, 0f, 0f, 0.3f);    // 深色半透明（加深 2 倍：0.45 → 0.9）
    public float WidthFactor = 1.0f;    // 影子寬 = 角色 sprite 世界寬 × 此值（放大、貼合角色）
    public float HeightRatio = 0.5f;    // 影子高 / 寬（越小越扁，俯視感越強）
    public float VerticalOffset = 0f;   // 腳底再往下(正)/上(負)微調（世界單位）
    public int SortingOrderBelow = 1;   // 比角色 sortingOrder 低幾階
    [Tooltip("錨點路：換動作時影子位移的平滑時間（秒）。idle 與 walk 的腳在畫布裡差了幾十像素，不平滑會在每次起步/停步時跳一下。0 = 不平滑。")]
    public float AnchorSmoothTime = 0.08f;

    // ── 除錯：true = 把影子強制畫在最上層（會蓋住角色）＋印 log，用來確認影子是否生成。平時 false ──
    const bool DebugDrawOnTop = false;
    const int DebugSortingOrder = 30000;

    static Sprite _sharedSprite;

    SpriteRenderer _charSr;
    GameObject _shadowGo;
    float _footOffsetY;
    IShadowAnchorSource _anchorSrc;   // 錨點路（見檔頭）；null = 舊路
    float _lastAnchorW = -1f;         // 上次套的影子寬（世界單位），變了才改 localScale
    Vector2 _smoothOff;               // 平滑中的本地位移（世界單位、相對 transform；只平滑位移、不平滑世界位置，角色移動時影子不拖尾）
    bool _smoothInit;
    bool _lastFlip;                   // 轉身時位移直接跳（轉身本來就是瞬間的，平滑反而像影子滑過去）

    void Start()
    {
        _charSr = GetComponent<SpriteRenderer>();
        if (_charSr == null) _charSr = GetComponentInChildren<SpriteRenderer>();
        _anchorSrc = GetComponent<IShadowAnchorSource>();

        Measure(out float width, out float height);

        _shadowGo = new GameObject(gameObject.name + "_Shadow");
        var sr = _shadowGo.AddComponent<SpriteRenderer>();
        sr.sprite = GetSharedSprite();
        sr.color = ShadowColor;
        if (_charSr != null)
        {
            sr.sortingLayerID = _charSr.sortingLayerID;
            sr.sortingOrder = DebugDrawOnTop ? DebugSortingOrder : (_charSr.sortingOrder - SortingOrderBelow);
        }
        else
        {
            sr.sortingOrder = DebugDrawOnTop ? DebugSortingOrder : 0;
        }
        // 共用 sprite 的 native 尺寸 = 1 世界單位（PPU=邊長），故 localScale 直接 = 目標世界大小
        _shadowGo.transform.localScale = new Vector3(width, height, 1f);

        UpdateShadowPosition();
    }

    /// <summary>
    /// 依角色「可見（不透明）像素」量出影子的寬高與腳底位移（順便寫進 <c>_footOffsetY</c>）。
    ///
    /// ⚠ 不能用整張 sprite bounds：AutoSprite 等圖角色只佔畫布一小塊（四周透明），整張寬會讓影子過大、
    /// 腳底落在透明區下緣讓影子偏低。先抓不透明像素範圍；texture 不可讀（舊 Animator 圖）則退回整張 bounds。
    /// </summary>
    void Measure(out float width, out float height)
    {
        float charWidth = 1f;
        float footWorldY = transform.position.y - 0.5f;
        if (_charSr != null && _charSr.sprite != null)
        {
            Bounds b = _charSr.bounds;   // 世界 AABB（已含 transform 縮放與翻轉，寬高不受 flip 影響）
            charWidth = b.size.x;
            footWorldY = b.min.y;
            if (TryGetVisibleFraction(_charSr.sprite, out float widthFrac, out float bottomFrac))
            {
                charWidth = b.size.x * widthFrac;              // 只取不透明寬度
                footWorldY = b.min.y + b.size.y * bottomFrac;  // 腳底 = 不透明區下緣（非畫布底）
            }
        }
        width = charWidth * WidthFactor;
        height = width * HeightRatio;
        _footOffsetY = (footWorldY - transform.position.y) - VerticalOffset;
    }

    /// <summary>
    /// 角色的顯示大小變了之後重新量一次（例如血統換外型／改體型倍率）。
    /// 不叫的話影子會停在舊尺寸——換成 1.5 倍體型的血統時，腳下會頂著一塊明顯偏小的影子。
    /// </summary>
    public void Refresh()
    {
        if (_shadowGo == null) return;
        if (_charSr == null)
        {
            _charSr = GetComponent<SpriteRenderer>();
            if (_charSr == null) _charSr = GetComponentInChildren<SpriteRenderer>();
        }
        if (_anchorSrc == null) _anchorSrc = GetComponent<IShadowAnchorSource>();
        Measure(out float width, out float height);
        _shadowGo.transform.localScale = new Vector3(width, height, 1f);
        _lastAnchorW = -1f;   // 錨點路下一幀會重套
        _smoothInit = false;  // 換外型後位移直接跳到新值，不從舊外型平滑過去
        UpdateShadowPosition();
    }

    /// <summary>
    /// 顯示／隱藏影子本體。
    /// ⚠ 影子是**獨立 GameObject、不是子物件**，所以「把角色整個 SetActive(false)」不會讓影子消失——
    /// 反而因為本元件的 LateUpdate 也停了，影子會定格留在原地。要隱藏角色一定要一起呼叫這支。
    /// （劇情演出的「隱藏主角」走 <see cref="Dipan.Cutscene.PlayerVisibility"/>，那裡已經接好。）
    /// </summary>
    public void SetVisible(bool on)
    {
        if (_shadowGo != null) _shadowGo.SetActive(on);
    }

    void LateUpdate()
    {
        UpdateShadowPosition();
    }

    void UpdateShadowPosition()
    {
        if (_shadowGo == null) return;
        Vector3 p = transform.position;

        // ── 錨點路：目前動作的錨點（像素）→ 世界位移 ──
        if (_anchorSrc != null && _charSr != null && _charSr.sprite != null
            && _anchorSrc.TryGetShadowAnchor(out var a) && a.ok)
        {
            var sp = _charSr.sprite;
            float ppu = sp.pixelsPerUnit;
            if (ppu > 0.0001f)
            {
                Rect r = sp.rect;
                Vector2 pivotPx = sp.pivot;   // 以 rect 左下為原點的像素（bodyScale > 1 時 pivot 被 PlayerSpriteLibrary 往下移，這裡自動跟上）
                // 表裡的 X 相對畫布中心、Y 從畫布底往上；換算成相對 pivot（＝transform）的本地位移。
                // 畫布尺寸若與量表時不同（作者換了 512 的圖沒重算），按比例縮放像素值，至少不會整個飛掉。
                float sx = (a.canvasW > 0) ? r.width / a.canvasW : 1f;
                float sy = (a.canvasH > 0) ? r.height / a.canvasH : 1f;
                float localX = (a.xFromCenterPx * sx + r.width * 0.5f - pivotPx.x) / ppu;
                float localY = (a.yFromBottomPx * sy - pivotPx.y) / ppu;

                Vector3 ls = transform.lossyScale;
                float flip = _charSr.flipX ? -1f : 1f;   // 錨點是「未翻面」的來源圖方向，翻面時 X 取負
                var target = new Vector2(localX * ls.x * flip, localY * ls.y - VerticalOffset);
                // 換動作（idle↔walk）錨點會差幾十像素，平滑一下免得起步/停步時影子跳；平滑的是位移不是世界位置。
                bool flipped = _charSr.flipX;
                if (!_smoothInit || AnchorSmoothTime <= 0.0001f || flipped != _lastFlip) { _smoothOff = target; _smoothInit = true; _lastFlip = flipped; }
                else
                {
                    float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / AnchorSmoothTime);   // unscaled：暫停中的演出（D15）角色照走，影子也要跟
                    _smoothOff = Vector2.Lerp(_smoothOff, target, k);
                }
                _shadowGo.transform.position = new Vector3(p.x + _smoothOff.x, p.y + _smoothOff.y, p.z);

                float w = (a.widthPx * sx / ppu) * Mathf.Abs(ls.x) * WidthFactor;
                if (w > 0.0001f && !Mathf.Approximately(w, _lastAnchorW))
                {
                    _lastAnchorW = w;
                    _shadowGo.transform.localScale = new Vector3(w, w * HeightRatio, 1f);
                }
                return;
            }
        }

        // ── 舊路：Start 量一次的位移 ──
        _shadowGo.transform.position = new Vector3(p.x, p.y + _footOffsetY, p.z);
    }

    void OnDestroy()
    {
        if (_shadowGo != null) Destroy(_shadowGo);
    }

    // 回傳此 sprite 不透明像素的「寬度佔整張比例」與「下緣距底部比例」（0~1）。
    // texture 不可讀（舊 Animator 匯入圖未開 Read/Write）或全透明則回 false，呼叫端退回整張 bounds。
    static bool TryGetVisibleFraction(Sprite sprite, out float widthFrac, out float bottomFrac)
    {
        widthFrac = 1f; bottomFrac = 0f;
        var tex = sprite.texture;
        if (tex == null || !tex.isReadable) return false;

        Rect r = sprite.rect;   // 此 sprite 在 texture 內的像素區域（整張或某格）
        int rx = Mathf.RoundToInt(r.x), ry = Mathf.RoundToInt(r.y);
        int rw = Mathf.RoundToInt(r.width), rh = Mathf.RoundToInt(r.height);
        if (rw <= 0 || rh <= 0) return false;

        Color32[] px;
        try { px = tex.GetPixels32(); } catch { return false; }
        int texW = tex.width;

        const byte aThr = 10;
        int minX = rw, maxX = -1, minY = rh, maxY = -1;
        for (int y = 0; y < rh; y++)
        {
            int row = (ry + y) * texW + rx;
            for (int x = 0; x < rw; x++)
            {
                if (px[row + x].a > aThr)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        if (maxX < 0) return false;   // 全透明

        widthFrac = (maxX - minX + 1) / (float)rw;
        bottomFrac = minY / (float)rh;   // texture 原點在左下 → minY = 不透明區最低列 = 腳底
        return true;
    }

    // 程序生成一張「中心實、邊緣柔淡」的圓形 alpha 貼圖（白色，靠 SpriteRenderer.color 染成黑半透明）。
    // PPU = 邊長 → sprite native = 1 世界單位，方便用 localScale 直接控大小。整個遊戲共用一張。
    static Sprite GetSharedSprite()
    {
        if (_sharedSprite != null) return _sharedSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float r = size / 2f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - r) / r;
                float dy = (y + 0.5f - r) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);   // 0=中心 → 1=邊緣
                // 中心大片實心(a=1)、只有外圈 0.72~1 羽化到 0。
                // 注意：別直接寫 Mathf.SmoothStep(0.72f,1f,d)——那是「在 0.72 與 1 之間插值」，會把中心壓到 ~0.28、
                // 害整個影子超淡（先前看不清就是這個雷）。要先用 InverseLerp 把 d 正規化成 0~1 再 SmoothStep。
                float edge = Mathf.InverseLerp(0.72f, 1f, d);
                float a = 1f - Mathf.SmoothStep(0f, 1f, edge);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();

        _sharedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _sharedSprite;
    }
}
