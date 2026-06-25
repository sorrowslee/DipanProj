using UnityEngine;

/// <summary>
/// 腳下橢圓影子（blob shadow）：在角色腳下放一個半透明深色橢圓，畫在角色之下、地面之上，每幀跟著角色走。
/// 俯視角 2D 常見做法——不靠即時光照（本專案 Built-in 管線、無 2D 燈光），最省也最穩。
///
/// 掛在任何角色上即可（玩家 / 怪物 / 未來 NPC）：`gameObject.AddComponent&lt;BlobShadow&gt;()`。
/// 影子是**獨立物件**（非子物件，避免被角色翻轉/縮放二次影響），角色銷毀時自動一起清掉。
/// 全程式建構、零 prefab——影子圖用程序生成的柔邊橢圓（一張共用快取）。見 readme/SHADOW.md。
/// </summary>
public class BlobShadow : MonoBehaviour
{
    [Header("影子外觀（可調）")]
    public Color ShadowColor = new Color(0f, 0f, 0f, 0.45f);   // 深色半透明
    public float WidthFactor = 0.85f;   // 影子寬 = 角色 sprite 世界寬 × 此值
    public float HeightRatio = 0.4f;    // 影子高 / 寬（越小越扁，俯視感越強）
    public float VerticalOffset = 0f;   // 腳底再往下(正)/上(負)微調（世界單位）
    public int SortingOrderBelow = 1;   // 比角色 sortingOrder 低幾階

    static Sprite _sharedSprite;

    SpriteRenderer _charSr;
    GameObject _shadowGo;
    float _footOffsetY;

    void Start()
    {
        _charSr = GetComponent<SpriteRenderer>();
        if (_charSr == null) _charSr = GetComponentInChildren<SpriteRenderer>();

        // 依角色目前 sprite 的世界寬度決定影子大小；腳底相對 pivot 的偏移算一次
        float charWidth = (_charSr != null && _charSr.sprite != null) ? _charSr.bounds.size.x : 1f;
        float width = charWidth * WidthFactor;
        float height = width * HeightRatio;
        _footOffsetY = ((_charSr != null) ? (_charSr.bounds.min.y - transform.position.y) : -0.5f) - VerticalOffset;

        _shadowGo = new GameObject(gameObject.name + "_Shadow");
        var sr = _shadowGo.AddComponent<SpriteRenderer>();
        sr.sprite = GetSharedSprite();
        sr.color = ShadowColor;
        if (_charSr != null)
        {
            sr.sortingLayerID = _charSr.sortingLayerID;
            sr.sortingOrder = _charSr.sortingOrder - SortingOrderBelow;
        }
        // 共用 sprite 的 native 尺寸 = 1 世界單位（PPU=邊長），故 localScale 直接 = 目標世界大小
        _shadowGo.transform.localScale = new Vector3(width, height, 1f);

        UpdateShadowPosition();
    }

    void LateUpdate()
    {
        UpdateShadowPosition();
    }

    void UpdateShadowPosition()
    {
        if (_shadowGo == null) return;
        Vector3 p = transform.position;
        _shadowGo.transform.position = new Vector3(p.x, p.y + _footOffsetY, p.z);
    }

    void OnDestroy()
    {
        if (_shadowGo != null) Destroy(_shadowGo);
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
                float a = Mathf.Clamp01(1f - d);
                a *= a;                                     // 柔化：邊緣更快淡掉
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();

        _sharedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _sharedSprite;
    }
}
