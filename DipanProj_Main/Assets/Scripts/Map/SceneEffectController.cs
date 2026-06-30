using System.Collections.Generic;
using UnityEngine;
using Dipan.MapRuntime;

/// <summary>
/// 場景特效（地圖級「世界端」環境表演）。設計仿 <see cref="AtmosphereController"/>：自動生成、跨地圖常駐，
/// 由 <c>MapsTable.csv</c> 的 <c>SceneEffect</c> 欄驅動，<see cref="MapManager"/> 載圖時呼叫
/// <see cref="ApplyMapSceneEffect"/>（換圖即時切換、自動清掉上一張的殘留）。
///
///   0 / 空 = 無；
///   1 = 火雨（FireRain）：火球**像「火焰拋擲彈」一樣，從畫面外上方以拋物線弧線丟進場內**、落地有火光。
///       純表演不傷人；範圍跟著相機可視畫面（所以鏡頭走到哪、火球就丟到哪，密度看得到）。
///
/// 真實素材之後可放 Resources（或接 VfxTable），目前火球/火光用程式生成的佔位圖（見 <see cref="SceneEffectSprites"/>）。
/// 全程式建構、零 prefab/Inspector 接線，風格對齊專案。
/// </summary>
[DisallowMultipleComponent]
public class SceneEffectController : MonoBehaviour
{
    public static SceneEffectController Instance { get; private set; }

    FireRain _fireRain;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("[SceneEffect]");
        go.AddComponent<SceneEffectController>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>由 MapManager 在載圖時呼叫。type = MapsTable 的 SceneEffect 欄；map = 當前地圖（用於落點夾在地圖內）。</summary>
    public static void ApplyMapSceneEffect(int type, MapData map)
    {
        if (Instance == null) AutoSpawn();
        if (Instance != null) Instance.Apply(type, map);
    }

    void Apply(int type, MapData map)
    {
        if (_fireRain != null) _fireRain.StopAndClear();   // 先清上一張地圖的殘留

        if (type == 1)   // 火雨
        {
            if (_fireRain == null)
            {
                var go = new GameObject("FireRain");
                go.transform.SetParent(transform, false);
                _fireRain = go.AddComponent<FireRain>();
            }
            _fireRain.gameObject.SetActive(true);
            _fireRain.Begin(map);
        }
        else if (_fireRain != null)
        {
            _fireRain.gameObject.SetActive(false);
        }
    }
}

/// <summary>
/// 火雨：以穩定密度持續從「畫面外上方」往相機可視範圍內拋火球（拋物線弧線，仿火焰拋擲彈），落地播一次火光。
/// 純表演（不碰戰鬥系統、不傷害）。範圍跟著相機，落點夾在地圖範圍內。參數可在 Inspector 微調。
/// </summary>
[DisallowMultipleComponent]
public class FireRain : MonoBehaviour
{
    [Header("密度 / 數量")]
    [Tooltip("每秒丟幾顆火球")] public float spawnPerSecond = 1.5f;
    [Tooltip("同時存在上限（保護效能）")] public int maxAlive = 40;

    [Header("拋擲弧線")]
    [Tooltip("飛行時間範圍（秒）：起點→落點固定時間抵達；越小飛越快")] public Vector2 flightTime = new Vector2(0.7f, 1.1f);
    [Tooltip("拋物線弧高範圍（世界單位）：越小越接近直線")] public Vector2 arcHeight = new Vector2(0.5f, 1.5f);
    [Tooltip("起點在畫面上緣外多高開始丟")] public float launchAboveTop = 3f;
    [Tooltip("斜向丟入的水平偏移範圍（越大越斜）")] public Vector2 throwSideRange = new Vector2(3f, 9f);
    [Tooltip("火球大小範圍（世界單位）")] public Vector2 sizeRange = new Vector2(0.6f, 1.2f);

    // 注意：Unity sortingOrder 實為 16-bit（-32768~32767），給超大值會溢位繞回負數 → 被背景蓋住看不到。
    // 地上物繞回後約落在 ~17000~22000，這裡用 30000（仍在合法範圍內、且高於地上物），確保火球畫在最前面。
    [Tooltip("火球繪製排序（合法範圍 ≤32767；要高於地上物）")] public int sortingOrder = 30000;

    MapData _map;
    Rect _mapBounds;
    Camera _cam;
    float _accum;
    readonly List<TossedFireball> _alive = new List<TossedFireball>();
    Sprite _ballSprite, _impactSprite;

    public void Begin(MapData map)
    {
        _map = map;
        _mapBounds = (map != null) ? MapCoords.WorldBounds(map) : new Rect(-9999, -9999, 19998, 19998);
        _accum = 0f;
        if (_ballSprite == null) _ballSprite = SceneEffectSprites.Comet();
        if (_impactSprite == null) _impactSprite = SceneEffectSprites.Glow();
    }

    /// <summary>清掉場上所有火球（換圖或關閉效果時）。</summary>
    public void StopAndClear()
    {
        for (int i = 0; i < _alive.Count; i++)
            if (_alive[i] != null) Destroy(_alive[i].gameObject);
        _alive.Clear();
        _accum = 0f;
    }

    void Update()
    {
        _alive.RemoveAll(f => f == null);   // 落地自毀的會在這裡清出清單

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        _accum += Time.deltaTime * spawnPerSecond;
        while (_accum >= 1f)
        {
            _accum -= 1f;
            if (_alive.Count < maxAlive) SpawnOne();
        }
    }

    void SpawnOne()
    {
        // 相機可視範圍（正交）：火球丟進玩家看得到的畫面內。
        float halfH = _cam.orthographic ? _cam.orthographicSize : 8f;
        float halfW = halfH * _cam.aspect;
        Vector3 cc = _cam.transform.position;
        float viewMinX = cc.x - halfW, viewMaxX = cc.x + halfW;
        float viewMinY = cc.y - halfH, viewMaxY = cc.y + halfH;

        // 落點：可視範圍內隨機，並夾在地圖範圍內（避免砸到地圖外）。
        float tx = Mathf.Clamp(Random.Range(viewMinX, viewMaxX), _mapBounds.xMin, _mapBounds.xMax);
        float ty = Mathf.Clamp(Random.Range(viewMinY, viewMaxY), _mapBounds.yMin, _mapBounds.yMax);
        Vector2 target = new Vector2(tx, ty);

        // 起點：畫面上緣外、左右隨機偏一邊 → 斜斜地丟進來（仿拋擲彈從畫面外進場）。
        float side = Random.Range(throwSideRange.x, throwSideRange.y) * (Random.value < 0.5f ? -1f : 1f);
        Vector2 start = new Vector2(tx + side, viewMaxY + launchAboveTop);

        var go = new GameObject("Fireball");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(start.x, start.y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _ballSprite;
        sr.sortingOrder = sortingOrder;

        float size = Random.Range(sizeRange.x, sizeRange.y);
        float dur = Random.Range(flightTime.x, flightTime.y);
        float arc = Random.Range(arcHeight.x, arcHeight.y);

        var f = go.AddComponent<TossedFireball>();
        f.Init(sr, _impactSprite, start, target, arc, dur, size, sortingOrder);
        _alive.Add(f);
    }
}

/// <summary>
/// 單顆拋擲火球：地面位置 start→target 線性插值（固定飛行時間），視覺再加「假高度」拋物線 Y 偏移，
/// 模擬丟炸彈的弧線（與 Sorrows.Ballistics 的 ParabolicBehavior 同套做法）。朝飛行方向轉、落地播火光後自毀。
/// </summary>
[DisallowMultipleComponent]
public class TossedFireball : MonoBehaviour
{
    SpriteRenderer _sr;
    Sprite _impact;
    Vector2 _start, _target;
    float _arc, _dur, _size, _elapsed;
    int _sorting;
    Vector3 _prevPos;

    public void Init(SpriteRenderer sr, Sprite impact, Vector2 start, Vector2 target,
                     float arcHeight, float flightDuration, float size, int sorting)
    {
        _sr = sr; _impact = impact;
        _start = start; _target = target;
        _arc = arcHeight; _dur = Mathf.Max(0.05f, flightDuration); _size = size; _sorting = sorting;
        _elapsed = 0f;
        _prevPos = new Vector3(start.x, start.y, 0f);
        transform.position = _prevPos;
        // 縱向拉長一點像帶尾火球（會被旋轉對齊飛行方向）。放大 2×（原 0.55/1.25 → 1.1/2.5）；
        // 落地火光不受影響（SpawnImpact 仍依 _size 計算）。
        transform.localScale = new Vector3(_size * 1.1f, _size * 2.5f, 1f);
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        float p = Mathf.Clamp01(_elapsed / _dur);

        Vector2 ground = Vector2.Lerp(_start, _target, p);
        float height = 4f * _arc * p * (1f - p);            // 拋物線假高度（apex 在 p=0.5）
        Vector3 pos = new Vector3(ground.x, ground.y + height, 0f);
        transform.position = pos;

        // 朝飛行方向轉（sprite 頭部 = +Y，所以對齊速度方向時頭部領先、尾巴拖後）。
        Vector3 vel = pos - _prevPos;
        if (vel.sqrMagnitude > 1e-6f)
        {
            float ang = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, ang);
        }
        _prevPos = pos;

        if (p >= 1f) { SpawnImpact(_target); Destroy(gameObject); }
    }

    void SpawnImpact(Vector2 at)
    {
        if (_impact == null) return;
        var go = new GameObject("FireImpact");
        go.transform.position = new Vector3(at.x, at.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _impact;
        sr.color = new Color(1f, 0.6f, 0.2f, 0.9f);
        sr.sortingOrder = _sorting - 1;
        go.AddComponent<FireImpactFx>().Init(sr, _size * 2.7f);   // 落地火光放大到 1.5×（原 1.8 → 2.7）
    }
}

/// <summary>落地火光：擴散放大 + 淡出後自毀。</summary>
[DisallowMultipleComponent]
public class FireImpactFx : MonoBehaviour
{
    SpriteRenderer _sr;
    float _maxSize, _life = 0.35f, _t;

    public void Init(SpriteRenderer sr, float maxSize)
    {
        _sr = sr;
        _maxSize = Mathf.Max(0.1f, maxSize);
        transform.localScale = Vector3.one * (_maxSize * 0.3f);
    }

    void Update()
    {
        _t += Time.deltaTime;
        float k = _t / _life;
        if (k >= 1f) { Destroy(gameObject); return; }
        transform.localScale = Vector3.one * Mathf.Lerp(_maxSize * 0.3f, _maxSize, k);
        if (_sr != null) { var c = _sr.color; c.a = Mathf.Lerp(0.9f, 0f, k); _sr.color = c; }
    }
}

/// <summary>場景特效用的程式生成佔位素材（零美術，整個遊戲共用 static 快取）。</summary>
public static class SceneEffectSprites
{
    static Sprite _comet, _glow;

    /// <summary>彗星狀火球：上方亮黃頭 + 往下漸淡的橘紅尾（頭 = +Y，會被旋轉對齊飛行方向）。</summary>
    public static Sprite Comet()
    {
        if (_comet != null) return _comet;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        Vector2 head = new Vector2(0.5f, 0.72f);
        float headR = 0.26f;
        Color coreC = new Color(1f, 0.95f, 0.6f, 1f);    // 亮黃頭
        Color tailC = new Color(1f, 0.35f, 0.06f, 1f);   // 橘紅尾
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float nx = (x + 0.5f) / n, ny = (y + 0.5f) / n;
                float hd = Vector2.Distance(new Vector2(nx, ny), head) / headR;
                float headA = Mathf.Clamp01(1f - hd); headA *= headA;
                float tailA = 0f;
                if (ny < head.y)
                {
                    float down = (head.y - ny) / head.y;                 // 0 在頭、1 在底
                    float width = Mathf.Lerp(0.11f, 0.015f, down);       // 往下收窄
                    float dx = Mathf.Abs(nx - 0.5f);
                    float band = Mathf.Clamp01(1f - dx / Mathf.Max(0.001f, width));
                    tailA = band * Mathf.Clamp01(1f - down) * 0.8f;
                }
                float a = Mathf.Clamp01(Mathf.Max(headA, tailA));
                Color c = Color.Lerp(tailC, coreC, headA);
                c.a *= a;
                px[y * n + x] = c;
            }
        tex.SetPixels32(px); tex.Apply();
        _comet = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f);
        return _comet;
    }

    /// <summary>落地火光：橘黃柔光圓。</summary>
    public static Sprite Glow()
    {
        if (_glow != null) return _glow;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = n * 0.5f;
        var px = new Color32[n * n];
        Color core = new Color(1f, 0.8f, 0.4f, 1f), edge = new Color(1f, 0.4f, 0.1f, 0f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / r);
                float a = 1f - d; a *= a;
                Color c = Color.Lerp(core, edge, d);
                c.a *= a;
                px[y * n + x] = c;
            }
        tex.SetPixels32(px); tex.Apply();
        _glow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f);
        return _glow;
    }
}
