using UnityEngine;

/// <summary>
/// 可交談 NPC 的頭上「對話泡泡」標示：圓角對話框＋尾巴＋三個點，**純程式畫、零素材**
/// （同 <see cref="InteractMarker"/> 程式畫五角星的路數），上下浮動＋輕微呼吸。
/// 放 InteractOverlay 層由 Overlay 相機重畫 → 暗場景（幽暗/噩夢氛圍）也看得到。
/// 獨立 GameObject 跟著 NPC（不掛作子物件，避免吃到 NPC 的 Scale 而忽大忽小），
/// 目標消失即自毀；NpcAgent 銷毀時也會帶走它。
/// 之後想換成美術圖：改 <see cref="Create"/> 裡的 sprite 來源即可，外部呼叫不變。
/// </summary>
public class NpcTalkMarker : MonoBehaviour
{
    [Header("外觀")]
    public Color bubbleColor = new Color(1f, 1f, 1f, 0.92f);        // 泡泡底色
    public Color dotColor = new Color(0.25f, 0.2f, 0.18f, 0.95f);   // 三個點（墨色）
    public float worldSize = 0.55f;    // 泡泡世界寬
    public float headOffset = 0.32f;   // 距頭頂（碰撞框上緣）多高
    public int sortingOrder = 20;      // 同星星（高於角色）

    [Header("動畫")]
    public float bobAmp = 0.06f;
    public float bobSpeed = 2.2f;
    public float pulseAmp = 0.06f;     // 大小呼吸幅度
    public float pulseSpeed = 2.6f;

    Transform _target;
    Collider2D _targetCol;
    SpriteRenderer _bubble, _dots;
    float _phase;

    static Sprite _bubbleSprite, _dotsSprite;

    /// <summary>幫某個 NPC 建一顆對話泡泡標示（獨立物件、自動跟隨）。</summary>
    public static NpcTalkMarker Create(Transform target)
    {
        var go = new GameObject("NpcTalkMarker");
        var m = go.AddComponent<NpcTalkMarker>();
        m._target = target;
        return m;
    }

    void Start()
    {
        // 暗場景也看得到：放 InteractOverlay 層（找不到就留在 Default、被氛圍壓暗，不報錯——同 InteractMarker）。
        int layer = LayerMask.NameToLayer(OverlayCameraController.LayerName);
        if (layer >= 0) gameObject.layer = layer;

        if (_bubbleSprite == null) BuildSprites();

        _bubble = gameObject.AddComponent<SpriteRenderer>();
        _bubble.sprite = _bubbleSprite;
        _bubble.sortingOrder = sortingOrder;
        _bubble.color = bubbleColor;

        var dotsGo = new GameObject("Dots");
        dotsGo.transform.SetParent(transform, false);
        if (layer >= 0) dotsGo.layer = layer;
        _dots = dotsGo.AddComponent<SpriteRenderer>();
        _dots.sprite = _dotsSprite;
        _dots.sortingOrder = sortingOrder + 1;
        _dots.color = dotColor;

        _phase = Random.value * Mathf.PI * 2f;
        transform.localScale = Vector3.one * worldSize;
    }

    void LateUpdate()
    {
        if (_target == null) { Destroy(gameObject); return; }
        if (_targetCol == null) _targetCol = _target.GetComponent<Collider2D>();

        float top = _targetCol != null ? _targetCol.bounds.max.y : _target.position.y + 1f;
        float bob = Mathf.Sin(Time.time * bobSpeed + _phase) * bobAmp;
        transform.position = new Vector3(_target.position.x, top + headOffset + bob, 0f);

        float s = worldSize * (1f + Mathf.Sin(Time.time * pulseSpeed + _phase) * pulseAmp);
        transform.localScale = new Vector3(s, s, 1f);
    }

    // ───────── 程式畫「圓角對話框＋尾巴」與「三個點」兩張 sprite（白色，靠 color 染色）─────────

    static void BuildSprites()
    {
        const int N = 96;
        var bubblePx = new Color32[N * N];
        var dotsPx = new Color32[N * N];

        // 泡泡本體：圓角矩形 x∈[0.06,0.94]、y∈[0.32,0.94]（歸一化，y 往上），圓角半徑 0.14；
        // 尾巴：從泡泡下緣 (0.32,0.32)-(0.52,0.32) 收斂到左下角尖點 (0.24,0.04)。
        // 三個點：泡泡中線 y=0.63 上等距三顆，半徑 0.052。
        Vector2[] dots = { new Vector2(0.34f, 0.63f), new Vector2(0.50f, 0.63f), new Vector2(0.66f, 0.63f) };

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                int bubbleHit = 0, dotHit = 0;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    float fx = (x + (sx + 0.5f) * 0.5f) / N;
                    float fy = (y + (sy + 0.5f) * 0.5f) / N;
                    if (InBubble(fx, fy)) bubbleHit++;
                    foreach (var d in dots)
                        if ((fx - d.x) * (fx - d.x) + (fy - d.y) * (fy - d.y) <= 0.052f * 0.052f) { dotHit++; break; }
                }
                byte ba = (byte)(bubbleHit * 255 / 4);
                byte da = (byte)(dotHit * 255 / 4);
                bubblePx[y * N + x] = new Color32(255, 255, 255, ba);
                dotsPx[y * N + x] = new Color32(255, 255, 255, da);
            }
        }

        _bubbleSprite = MakeSprite(bubblePx, N);
        _dotsSprite = MakeSprite(dotsPx, N);
    }

    static bool InBubble(float x, float y)
    {
        // 圓角矩形
        const float x0 = 0.06f, x1 = 0.94f, y0 = 0.32f, y1 = 0.94f, r = 0.14f;
        float cx = Mathf.Clamp(x, x0 + r, x1 - r);
        float cy = Mathf.Clamp(y, y0 + r, y1 - r);
        float dx = x - cx, dy = y - cy;
        if (dx * dx + dy * dy <= r * r) return true;

        // 尾巴（三角形：泡泡下緣兩點 → 左下尖點）
        return PointInTriangle(x, y, 0.30f, 0.34f, 0.52f, 0.34f, 0.22f, 0.05f);
    }

    static bool PointInTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float d1 = Sign(px, py, ax, ay, bx, by);
        float d2 = Sign(px, py, bx, by, cx, cy);
        float d3 = Sign(px, py, cx, cy, ax, ay);
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(hasNeg && hasPos);
    }

    static float Sign(float px, float py, float ax, float ay, float bx, float by)
        => (px - bx) * (ay - by) - (ax - bx) * (py - by);

    static Sprite MakeSprite(Color32[] px, int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);   // 世界尺寸 1，localScale 控大小
    }
}
