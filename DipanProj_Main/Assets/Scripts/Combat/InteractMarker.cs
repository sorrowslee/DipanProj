using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 互動點（道具拾取點 / 劇情觸發點）的標示特效：數顆星星在空中閃爍（純程式生成，無需任何貼圖）。
///
/// 星星 sprite 由程式畫出（五角星，反鋸齒），同一張共用快取；每顆星各有獨立相位，
/// 做「亮度閃爍 + 大小脈動 + 上下浮動 + 緩慢自轉」。由 <see cref="InteractionManager"/> 在每個互動點生成，
/// 觸發掉（撿取 / 看完劇情）時連同本物件一起銷毀。風格對齊專案：全程式建構、零素材、零接線。
/// </summary>
public class InteractMarker : MonoBehaviour
{
    [Header("外觀")]
    public int starCount = 5;
    public Color color = new Color(1f, 0.92f, 0.45f, 1f);
    public string sortingLayerName = "Default";
    public int sortingOrder = 20;

    [Header("分布（相對互動點中心，世界單位）")]
    public float spreadRadius = 0.5f;     // 星星散布的水平半徑
    public float floatHeight = 0.45f;     // 星群整體浮在中心上方多高
    public float starWorldSize = 0.22f;   // 單顆星的世界大小

    [Header("動畫")]
    public float twinkleSpeed = 3.2f;     // 閃爍速度
    public float baseAlpha = 0.35f;       // 最暗時的透明度
    public float alphaAmp = 0.65f;        // 閃爍幅度（baseAlpha + amp 為最亮）
    public float bobAmp = 0.09f;          // 上下浮動幅度
    public float bobSpeed = 2.0f;
    public float spinSpeed = 35f;         // 自轉（度/秒）

    class Star
    {
        public Transform tr;
        public SpriteRenderer sr;
        public Vector3 basePos;
        public float phase;     // 閃爍/脈動相位
        public float bobPhase;
        public float sizeFactor;
        public float spinDir;
    }

    readonly List<Star> _stars = new List<Star>();
    bool _built;

    static Sprite _starSprite;

    /// <summary>由 InteractionManager 在 AddComponent 後立即呼叫，設定顆數/顏色/排序。</summary>
    public void Configure(int count, Color c, string sortLayer, int sortOrder)
    {
        starCount = Mathf.Max(1, count);
        color = c;
        sortingLayerName = sortLayer;
        sortingOrder = sortOrder;
    }

    void Start() => Build();

    void Build()
    {
        if (_built) return;
        _built = true;

        // 讓星星改由「疊在氛圍後處理之上」的 OverlayCamera 畫 → 暗場景也永遠可見。
        // 找不到該 Layer（沒在 Tags and Layers 加）就退回原本行為（被氛圍壓暗）。
        int _ovLayer = LayerMask.NameToLayer(OverlayCameraController.LayerName);
        if (_ovLayer >= 0) gameObject.layer = _ovLayer;

        if (_starSprite == null) _starSprite = BuildStarSprite();

        for (int i = 0; i < starCount; i++)
        {
            var go = new GameObject($"Star_{i}");
            go.transform.SetParent(transform, false);
            if (_ovLayer >= 0) go.layer = _ovLayer;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _starSprite;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;
            sr.color = color;

            // 在中心上方一個小範圍內散開（圓盤分布），各星相位隨機。
            Vector2 off = Random.insideUnitCircle * spreadRadius;
            var star = new Star
            {
                tr = go.transform,
                sr = sr,
                basePos = new Vector3(off.x, floatHeight + off.y * 0.5f, 0f),
                phase = Random.value * Mathf.PI * 2f,
                bobPhase = Random.value * Mathf.PI * 2f,
                sizeFactor = Random.Range(0.7f, 1.3f),
                spinDir = Random.value < 0.5f ? 1f : -1f,
            };
            _stars.Add(star);
        }
    }

    void Update()
    {
        float t = Time.time;
        for (int i = 0; i < _stars.Count; i++)
        {
            var s = _stars[i];
            if (s.sr == null) continue;

            float tw = 0.5f + 0.5f * Mathf.Sin(t * twinkleSpeed + s.phase);   // 0..1
            var c = color;
            c.a = Mathf.Clamp01(baseAlpha + alphaAmp * tw);
            s.sr.color = c;

            float size = starWorldSize * s.sizeFactor * (0.7f + 0.3f * tw);
            s.tr.localScale = new Vector3(size, size, 1f);

            float bob = Mathf.Sin(t * bobSpeed + s.bobPhase) * bobAmp;
            s.tr.localPosition = s.basePos + Vector3.up * bob;

            s.tr.localRotation = Quaternion.Euler(0f, 0f, t * spinSpeed * s.spinDir + s.phase * 30f);
        }
    }

    // ───────────── 程式畫一顆五角星 sprite（反鋸齒，白色，靠 SpriteRenderer.color 染色）─────────────

    static Sprite BuildStarSprite()
    {
        const int N = 64;                       // 紋理邊長（像素）
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color32[N * N];

        // 五角星 10 個頂點（外/內交替），歸一化到 [-1,1]，留邊。
        Vector2[] verts = new Vector2[10];
        const float outer = 0.95f, inner = 0.45f;
        for (int k = 0; k < 10; k++)
        {
            float ang = Mathf.Deg2Rad * (90f - k * 36f);   // 從正上方開始，順時針
            float rad = (k % 2 == 0) ? outer : inner;
            verts[k] = new Vector2(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad);
        }

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                // 每像素 2×2 超取樣做反鋸齒。
                int hit = 0;
                for (int sy = 0; sy < 2; sy++)
                {
                    for (int sx = 0; sx < 2; sx++)
                    {
                        float fx = (x + (sx + 0.5f) * 0.5f) / N * 2f - 1f;
                        float fy = (y + (sy + 0.5f) * 0.5f) / N * 2f - 1f;
                        if (PointInPolygon(verts, fx, fy)) hit++;
                    }
                }
                byte a = (byte)(hit * 255 / 4);
                px[y * N + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        // PPU = N → sprite 世界尺寸 = 1 單位；實際大小再由 transform.localScale 控制。
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
    }

    static bool PointInPolygon(Vector2[] poly, float x, float y)
    {
        bool inside = false;
        int n = poly.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if (((poly[i].y > y) != (poly[j].y > y)) &&
                (x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                inside = !inside;
        }
        return inside;
    }
}
