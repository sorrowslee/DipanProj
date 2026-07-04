using UnityEngine;

/// <summary>
/// 傳送門特效（場景特效的 portal kind）。起點/終點＝矩形的兩個對角，畫一片「平穩發光的能量光幕」：
/// 柔邊、縱向漸層的發光矩形（像鏡面/湖水），只做極緩慢細微的呼吸，不做流動/閃爍（保持平靜）。
/// 純表演、程式生成佔位素材，之後可換真貼圖/序列圖。
///
/// 由 <see cref="MapLoader.BuildSceneFx"/> 在 fxId 對應 SceneFxTable 列 Kind=portal 時掛上並 Configure。
/// 顏色/濃淡/排序來自 SceneFxTable（複用 <see cref="SceneFxEmitter.Look"/>）。
/// </summary>
[DisallowMultipleComponent]
public class PortalFx : MonoBehaviour
{
    SceneFxEmitter.Look _look;
    Vector3 _center;
    Vector2 _size;
    SpriteRenderer _fill;
    float _t;

    public void Configure(SceneFxEmitter.Look look, Vector3 cornerA, Vector3 cornerB)
    {
        _look = look;
        _center = (cornerA + cornerB) * 0.5f;
        _size = new Vector2(Mathf.Max(0.2f, Mathf.Abs(cornerB.x - cornerA.x)),
                            Mathf.Max(0.2f, Mathf.Abs(cornerB.y - cornerA.y)));

        var fillGo = new GameObject("PortalFill");
        fillGo.transform.SetParent(transform, false);
        fillGo.transform.position = _center;
        _fill = fillGo.AddComponent<SpriteRenderer>();
        _fill.sprite = FillSprite();
        _fill.sortingOrder = _look.sortingOrder;
        _fill.transform.localScale = new Vector3(_size.x, _size.y, 1f);
        _fill.color = WithAlpha(_look.peakAlpha);
    }

    void Update()
    {
        // 只做極緩慢、細微的亮度呼吸（週期約 8 秒、幅度很小），保持平靜如湖水；不流動、不縮放、不搖擺。
        _t += Time.deltaTime;
        float breathe = 0.95f + 0.05f * Mathf.Sin(_t * 0.8f);
        _fill.color = WithAlpha(_look.peakAlpha * breathe);
    }

    Color WithAlpha(float a) { var c = _look.color; c.a = a; return c; }

    // ── 光幕：柔邊圓角矩形漸層（solid 內部、羽化邊；縱向中央略亮）。白色圖樣，顏色由 SpriteRenderer.color 染 ──
    // ⚠️ 之前用 edge=min(ax,ay) 羽化 → 兩線性距離取 min 會在「對角線」形成脊線（帳篷函數的稜線），
    //    烘進貼圖放大後就是明顯的 X。改用「左右羽化 × 上下羽化」相乘（可分離）＝平滑圓角矩形、無對角脊線；
    //    縱向漸層改拋物線（無中央折線）。解析度也提高，放大後更平滑。（與遊戲端 Map/PortalFx.cs 同步）
    const float EdgeFeather = 0.18f;
    static Sprite _fillShared;
    static Sprite FillSprite()
    {
        if (_fillShared != null) return _fillShared;
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float nx = (x + 0.5f) / n * 2f - 1f;   // -1..1
                float ny = (y + 0.5f) / n * 2f - 1f;
                float ax = 1f - Mathf.Abs(nx);          // 到左右邊的距離（0 邊、1 中）
                float ay = 1f - Mathf.Abs(ny);          // 到上下邊的距離
                float fx = Mathf.SmoothStep(0f, EdgeFeather, ax);         // 左右羽化
                float fy = Mathf.SmoothStep(0f, EdgeFeather, ay);         // 上下羽化
                float alpha = fx * fy;                                    // 相乘＝圓角矩形柔邊，無對角脊線
                float bright = 0.72f + 0.28f * (1f - ny * ny);           // 縱向漸層（拋物線，平滑無折線）
                px[y * n + x] = new Color(bright, bright, bright, alpha);
            }
        tex.SetPixels32(px); tex.Apply();
        _fillShared = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f);
        return _fillShared;
    }
}
