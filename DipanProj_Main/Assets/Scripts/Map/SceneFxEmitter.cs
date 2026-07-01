using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用場景特效發射器（程式化粒子，零 prefab）。沿一條弧線（起點 → 控制點 → 終點）持續冒出柔邊粒子，
/// 一路放大、邊飄邊自轉、先淡入再淡出後回收。外觀（顏色/密度/大小/壽命/濃度…）由 <see cref="SceneFxTable"/> 的 fxId 決定；
/// 放置（起/終點/弧度/長寬/循環/間歇）由編輯器的 SceneFxInstance 決定。
///
/// 「滾滾澎湃」的濃煙感靠三招：① 粒子貼圖是雜訊擾動的花椰菜狀煙塊（非乾淨圓點）；② 每顆邊飄邊自轉＋亮度隨機（翻騰/體積層次）；
/// ③ 高密度、大顆、長壽 → 大量重疊堆成一團（密度/大小/壽命在 SceneFxTable 調）。
/// 由 <see cref="MapLoader"/> 在載圖時依 map.sceneFx 逐一生成。純表演，不碰戰鬥系統。
/// </summary>
[DisallowMultipleComponent]
public class SceneFxEmitter : MonoBehaviour
{
    /// <summary>外觀/行為模板（來自 SceneFxTable 的一列）。</summary>
    public struct Look
    {
        public Color color;
        public float emitPerSecond;
        public float lifeMin, lifeMax;
        public float sizeStart, sizeEnd;
        public float peakAlpha;
        public float turbulence;
        public int sortingOrder;
    }

    Look _look;
    Vector3 _start, _ctrl, _end;
    float _wScale = 1f, _hScale = 1f;
    bool _loop = true, _intermittent = false;
    float _interval = 2f;
    bool _ready;

    bool _on = true, _doneForever = false;
    float _phaseTimer;

    float _accum;
    Sprite _puff;
    readonly List<P> _pool = new List<P>();

    class P
    {
        public Transform tr; public SpriteRenderer sr; public bool active;
        public float age, life, seed, turbPhase;
        public float angle, rotSpeed, sizeMul, bright;   // 自轉 + 大小/亮度隨機
        public Vector3 s, c, e;
    }

    public void Configure(Look look, Vector3 start, Vector3 ctrl, Vector3 end,
                          float wScale, float hScale, bool loop, bool intermittent, float interval)
    {
        _look = look;
        _start = start; _ctrl = ctrl; _end = end;
        _wScale = Mathf.Max(0.01f, wScale); _hScale = Mathf.Max(0.01f, hScale);
        _loop = loop; _intermittent = intermittent; _interval = Mathf.Max(0.05f, interval);
        _on = true; _doneForever = false; _phaseTimer = 0f; _accum = 0f;
        if (_puff == null) _puff = PuffSprite();
        _ready = true;
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    void Update()
    {
        if (!_ready) return;
        float dt = Time.deltaTime;

        if (_intermittent && !_doneForever)
        {
            _phaseTimer += dt;
            if (_phaseTimer >= _interval)
            {
                _phaseTimer -= _interval;
                if (_on && !_loop) { _on = false; _doneForever = true; }
                else _on = !_on;
            }
        }

        bool emitting = !_doneForever && (_intermittent ? _on : true);
        if (emitting)
        {
            _accum += dt * _look.emitPerSecond;
            while (_accum >= 1f) { _accum -= 1f; Emit(); }
        }

        for (int i = 0; i < _pool.Count; i++)
        {
            var p = _pool[i];
            if (!p.active) continue;
            p.age += dt;
            float t = p.age / p.life;
            if (t >= 1f) { p.active = false; p.tr.gameObject.SetActive(false); continue; }

            Vector3 pos = Bezier(p.s, p.c, p.e, t);
            pos.x += Mathf.Sin(p.seed + t * 6.28318f * p.turbPhase) * _look.turbulence * (1f - t * 0.5f);
            pos.y += Mathf.Cos(p.seed * 1.7f + t * 6.28318f * p.turbPhase * 0.7f) * _look.turbulence * 0.4f; // 縱向也擾動＝翻騰
            p.tr.position = pos;
            p.tr.rotation = Quaternion.Euler(0f, 0f, p.angle + p.rotSpeed * p.age);   // 邊飄邊自轉

            float baseSize = Mathf.Lerp(_look.sizeStart, _look.sizeEnd, t) * p.sizeMul;
            p.tr.localScale = new Vector3(baseSize * _wScale, baseSize * _hScale, 1f);

            float fadeIn = Mathf.Clamp01(t / 0.14f);
            float fadeOut = Mathf.Clamp01((1f - t) / 0.5f);
            var col = _look.color;
            col.r *= p.bright; col.g *= p.bright; col.b *= p.bright;   // 亮度隨機＝體積層次
            col.a = _look.peakAlpha * fadeIn * fadeOut;
            p.sr.color = col;
        }
    }

    void Emit()
    {
        var p = GetOrCreate();
        if (p == null) return;
        p.s = _start; p.c = _ctrl; p.e = _end;
        p.age = 0f;
        p.life = Random.Range(_look.lifeMin, _look.lifeMax);
        p.seed = Random.Range(0f, 6.283f);
        p.turbPhase = Random.Range(0.6f, 1.6f);
        p.angle = Random.Range(0f, 360f);
        p.rotSpeed = Random.Range(-55f, 55f);          // 每顆自轉方向/速度不同 → 翻騰
        p.sizeMul = Random.Range(0.75f, 1.45f);        // 大小混雜 → 蓬鬆
        p.bright = Random.Range(0.65f, 1.2f);          // 明暗混雜 → 有體積
        p.active = true;

        p.tr.position = _start;
        p.tr.localScale = Vector3.one * _look.sizeStart * p.sizeMul;
        p.sr.sortingOrder = _look.sortingOrder;
        var c0 = _look.color; c0.a = 0f; p.sr.color = c0;
        p.tr.gameObject.SetActive(true);
    }

    P GetOrCreate()
    {
        for (int i = 0; i < _pool.Count; i++) if (!_pool[i].active) return _pool[i];
        if (_pool.Count >= 200) return null;
        var go = new GameObject("Puff");
        go.transform.SetParent(transform, true);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _puff;
        var p = new P { tr = go.transform, sr = sr, active = false };
        go.SetActive(false);
        _pool.Add(p);
        return p;
    }

    // ── 花椰菜狀煙塊：radial 柔淡 × 分形雜訊（fbm）擾動 → 邊緣不規則、內部有濃淡，比乾淨圓點像煙 ──
    static Sprite _shared;
    static Sprite PuffSprite()
    {
        if (_shared != null) return _shared;
        const int n = 96;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = n * 0.5f;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float nx = (x + 0.5f) / n, ny = (y + 0.5f) / n;
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / r);
                float baseA = 1f - d; baseA *= baseA;                 // 中心實、邊緣柔
                float fb = Fbm(nx * 5.3f, ny * 5.3f);                 // 0..1 雲狀擾動
                float a = baseA * (0.30f + 0.85f * fb);               // 內部濃淡 + 破碎邊緣
                a = Mathf.Clamp01(a);
                px[y * n + x] = new Color(1f, 1f, 1f, a);             // 顏色由 SpriteRenderer.color 染
            }
        tex.SetPixels32(px); tex.Apply();
        _shared = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 96f);
        return _shared;
    }

    // 簡易 value noise + 3 octave fbm（只在建貼圖時算一次）。
    static float Hash(int x, int y) { int h = x * 374761393 + y * 668265263; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF; }
    static float ValueNoise(float x, float y)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float xf = x - xi, yf = y - yi;
        float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
        float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), e = Hash(xi + 1, yi + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, e, u), v);
    }
    static float Fbm(float x, float y)
    {
        float f = 0.5f * ValueNoise(x, y) + 0.3f * ValueNoise(x * 2.1f, y * 2.1f) + 0.2f * ValueNoise(x * 4.3f, y * 4.3f);
        return Mathf.Clamp01(f);
    }
}
