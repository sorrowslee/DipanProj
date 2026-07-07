using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 傳送門特效（場景特效的 portal kind）：一片發光的能量光幕。
///
/// ⚠️ 為什麼用「UI 覆蓋層」而不是世界 SpriteRenderer：本專案的氛圍（Atmosphere）是**全螢幕後處理**，
/// 會把主相機畫面裡的所有世界物件重新調色/壓暗（提燈光圈），世界端的綠幕不管多亮都會被壓成一小片暗綠。
/// 而 Screen Space Overlay 的 UI 是在後處理**之後**才合成的，不受氛圍影響（見 AtmosphereController 註解）。
/// 所以這裡改成：一個 overlay 的 UI Image，每幀把「門洞世界矩形」投影到螢幕、貼上去 → 綠幕永遠明亮、不被吃掉。
///
/// 顏色/濃淡/排序來自 SceneFxTable（fxId 對應列 Kind=portal）。由 MapLoader.BuildSceneFx 掛上並 Configure。
/// </summary>
[DisallowMultipleComponent]
public class PortalFx : MonoBehaviour
{
    SceneFxEmitter.Look _look;
    Vector3 _cornerA, _cornerB;
    Canvas _canvas;
    Image _img;
    Camera _cam;
    float _t;

    public void Configure(SceneFxEmitter.Look look, Vector3 cornerA, Vector3 cornerB)
    {
        _look = look;
        _cornerA = cornerA;
        _cornerB = cornerB;

        // 自己一張 overlay canvas（在氛圍後處理之後合成）。sortingOrder 壓在面板層(100+)之下、遊戲畫面之上。
        var canvasGo = new GameObject("PortalCurtainCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 50;

        var imgGo = new GameObject("PortalCurtain");
        imgGo.transform.SetParent(canvasGo.transform, false);
        _img = imgGo.AddComponent<Image>();
        _img.sprite = FillSprite();
        _img.raycastTarget = false;
        _img.color = WithAlpha(_look.peakAlpha);

        _cam = Camera.main;
        Debug.Log($"[PortalFx] 綠幕(UI覆蓋層版)：color={_look.color} peakAlpha={_look.peakAlpha}");
    }

    void LateUpdate()
    {
        if (_img == null) return;
        if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

        // 門洞世界矩形四角 → 螢幕座標，取包圍框，貼到 overlay 上。
        Vector3 a = _cam.WorldToScreenPoint(_cornerA);
        Vector3 b = _cam.WorldToScreenPoint(_cornerB);
        Vector3 c = _cam.WorldToScreenPoint(new Vector3(_cornerA.x, _cornerB.y, 0f));
        Vector3 d = _cam.WorldToScreenPoint(new Vector3(_cornerB.x, _cornerA.y, 0f));
        float minX = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
        float maxX = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
        float minY = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
        float maxY = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));

        var rt = _img.rectTransform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;   // 左下為原點，1 單位=1 螢幕像素（overlay 無 scaler）
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        rt.sizeDelta = new Vector2(maxX - minX, maxY - minY);

        // 極緩慢細微的亮度呼吸（保持平靜）。
        _t += Time.deltaTime;
        float breathe = 0.9f + 0.1f * Mathf.Sin(_t * 0.8f);
        _img.color = WithAlpha(_look.peakAlpha * breathe);
    }

    Color WithAlpha(float a) { var c = _look.color; c.a = a; return c; }

    // ── 光幕圖樣：柔邊圓角矩形＋縱向漸層（白色，顏色由 Image.color 染）──
    const float EdgeFeather = 0.16f;
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
                float nx = (x + 0.5f) / n * 2f - 1f;
                float ny = (y + 0.5f) / n * 2f - 1f;
                float ax = 1f - Mathf.Abs(nx);
                float ay = 1f - Mathf.Abs(ny);
                float fx = Mathf.SmoothStep(0f, EdgeFeather, ax);
                float fy = Mathf.SmoothStep(0f, EdgeFeather, ay);
                float alpha = fx * fy;
                float bright = 0.8f + 0.2f * (1f - ny * ny);
                px[y * n + x] = new Color(bright, bright, bright, alpha);
            }
        tex.SetPixels32(px); tex.Apply();
        _fillShared = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return _fillShared;
    }
}
