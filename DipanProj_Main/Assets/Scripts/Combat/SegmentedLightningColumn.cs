using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 由頂端 start + N 個 tileable loop 組成的全高落雷。
/// loop 一路延伸到 impact，不使用會突然收細的 end；每段保持同一寬度，只微調 Y 尺寸讓總長無縫閉合。
///
/// 素材路徑可指定（見 <see cref="Spawn(Vector2, Camera, Style, float, float, float)"/>），
/// 所以同一套「拼接雷柱」邏輯可以給不同外觀共用：
///   ・九霄雷獄（武器）：SkyLightningColumn（預設）
///   ・血統變身：TransformLightning（見 Dipan.Gacha.BloodlineTransformFx）
/// </summary>
public sealed class SegmentedLightningColumn : MonoBehaviour
{
    // ── 預設素材（九霄雷獄）──
    const string StartPath = "VfxEffects/SkyLightningColumn/Start/Start";
    const string LoopPath = "VfxEffects/SkyLightningColumn/Loop/Loop";
    const int StartCount = 2;
    const int LoopCount = 8;

    const int DefaultSortingOrder = 22000;
    const float DefaultScale = 1.5f;
    const float DefaultFps = 20f;
    const float DefaultDuration = 0.45f;

    /// <summary>一組雷柱外觀（素材路徑 + 張數 + 排序層）。加新外觀＝多做一個 static readonly。</summary>
    public sealed class Style
    {
        /// <summary>頂端「雷首」的素材前綴。**留空 = 不用雷首，整根都用 loop**（見 <see cref="HasCap"/>）。</summary>
        public readonly string StartPrefix;
        public readonly string LoopPrefix;
        public readonly int StartFrames;
        public readonly int LoopFrames;
        public readonly int SortingOrder;

        /// <summary>
        /// 這組外觀要不要頂端雷首。
        ///
        /// ⚠ **雷首與 loop 的粗細差太多就別用。** 這一套素材的雷首是「尚未成形的細電光」，
        /// 邊緣只有 1~2px 寬，而 loop 的邊緣是 5~17px——接在一起是一根髮絲頂著一根粗電柱，
        /// 接縫非常明顯。而且雷首只有 2 張、`capFrame` 的算式會讓它在 0.15 秒左右就播完定格，
        /// 底下的 loop 卻還在跑 8 幀循環，變成「靜止的髮絲蓋在閃爍的柱子上」。
        /// 血統變身用的那組因此**不接雷首**；九霄雷獄維持原樣（它的節奏短、雷首多半在畫面外）。
        /// </summary>
        public bool HasCap => !string.IsNullOrEmpty(StartPrefix) && StartFrames > 0;

        /// <param name="startPrefix">頂端雷首素材；**留空 = 整根都用 loop**。</param>
        public Style(string startPrefix, string loopPrefix, int startFrames, int loopFrames,
                     int sortingOrder = DefaultSortingOrder)
        {
            StartPrefix = startPrefix;
            LoopPrefix = loopPrefix;
            StartFrames = Mathf.Max(0, startFrames);
            LoopFrames = Mathf.Max(1, loopFrames);
            // ⚠ sortingOrder 實為 16-bit，超過 32767 會溢位變負數整個看不到（見 readme/PROBLEMS.md E4）。
            SortingOrder = Mathf.Clamp(sortingOrder, -32768, 32767);
        }
    }

    /// <summary>九霄雷獄（武器 24）用的預設外觀。</summary>
    public static readonly Style SkyStrike = new Style(StartPath, LoopPath, StartCount, LoopCount);

    // 以「路徑前綴」為鍵的 Sprite 快取，讓多種外觀各自快取、互不干擾。
    static readonly Dictionary<string, Sprite[]> _cache = new Dictionary<string, Sprite[]>();

    /// <summary>
    /// 用不受 <c>Time.timeScale</c> 影響的時間推進動畫與壽命。
    /// 給「遊戲暫停期間仍然要播」的演出用（血統變身整段 timeScale=0）。
    /// **預設 false**＝跟著遊戲時間，九霄雷獄維持原本行為。Spawn 會回傳實體，設完即可。
    /// </summary>
    public bool Unscaled;

    readonly List<SpriteRenderer> _loops = new List<SpriteRenderer>();
    SpriteRenderer _startRenderer;
    Sprite[] _start;
    Sprite[] _loop;
    float _fps;
    float _duration;
    float _elapsed;

    /// <summary>進 Play 時清 Sprite 快取（Domain Reload 已關）。由 PlayModeStaticReset 呼叫。</summary>
    public static void ResetForPlayMode() => _cache.Clear();

    /// <summary>舊簽章：九霄雷獄用。行為與外觀完全不變。</summary>
    public static void Spawn(Vector2 impact, Camera camera, float scale = DefaultScale,
        float fps = DefaultFps, float duration = DefaultDuration)
        => Spawn(impact, camera, SkyStrike, scale, fps, duration);

    /// <summary>指定外觀的版本。回傳建出來的物件（可提前 Destroy）；素材缺失時回 null。</summary>
    public static SegmentedLightningColumn Spawn(Vector2 impact, Camera camera, Style style,
        float scale = DefaultScale, float fps = DefaultFps, float duration = DefaultDuration)
    {
        if (style == null) style = SkyStrike;
        var start = style.HasCap ? EnsureLoaded(style.StartPrefix, style.StartFrames) : null;
        var loop = EnsureLoaded(style.LoopPrefix, style.LoopFrames);

        // 用 IsStale 而非「陣列 == null」：Load() 永遠回傳非 null 陣列（即使每格 Resources.Load 都失敗），
        // 舊守衛攔不到「陣列在但元素全 null」，會一路走到底下取 _loop[0].bounds 而丟 NullReference。
        if (camera == null || IsStale(loop) || (style.HasCap && IsStale(start)))
        {
            Debug.LogWarning($"分段雷柱素材未完整載入（{style.LoopPrefix}），略過雷柱視覺。");
            return null;
        }

        var go = new GameObject("SegmentedLightning");
        var column = go.AddComponent<SegmentedLightningColumn>();
        column._start = start;
        column._loop = loop;
        column.Build(impact, camera, scale, fps, duration, style.SortingOrder);
        return column;
    }

    static Sprite[] EnsureLoaded(string prefix, int count)
    {
        // ⚠ 陣列型的 static 快取「不能」只判斷 `_x == null`：
        // 關掉 Domain Reload 後，停止 Play 被銷毀的是陣列「裡面的 Sprite」，
        // 陣列本身是純 C# 物件、永遠不會變 null → 舊寫法不會重載，第二次 Play 會拿到一堆已銷毀的 Sprite
        // （症狀：雷柱不見／變白塊）。所以要連「元素是否還活著」一起判。
        // 見 readme/PROBLEMS.md 的 Domain Reload 系列（I3/I5/I7/I8）與 PlayModeStaticReset.cs 的註記。
        if (_cache.TryGetValue(prefix, out var cached) && !IsStale(cached)) return cached;
        var loaded = Load(prefix, count);
        _cache[prefix] = loaded;
        return loaded;
    }

    /// <summary>陣列為 null／空／首元素已被銷毀（Unity 的 == null 對已銷毀物件回 true）＝需要重載。</summary>
    static bool IsStale(Sprite[] arr) => arr == null || arr.Length == 0 || arr[0] == null;

    static Sprite[] Load(string prefix, int count)
    {
        var sprites = new Sprite[count];
        for (int i = 0; i < count; i++)
            sprites[i] = Resources.Load<Sprite>($"{prefix}_{i + 1:D2}");
        return sprites;
    }

    void Build(Vector2 impact, Camera camera, float scale, float fps, float duration, int sortingOrder)
    {
        _fps = Mathf.Max(1f, fps);
        _duration = Mathf.Max(0.1f, duration);

        float topY = camera.ViewportToWorldPoint(new Vector3(0.5f, 1.12f, 0f)).y;
        float length = Mathf.Max(1f, topY - impact.y);
        float nativeHeight = Mathf.Max(0.01f, _loop[0].bounds.size.y);
        float desiredHeight = nativeHeight * Mathf.Max(0.1f, scale);
        int segmentCount = Mathf.Max(2, Mathf.CeilToInt(length / desiredHeight));
        float segmentHeight = length / segmentCount;
        float scaleY = segmentHeight / nativeHeight;

        bool hasCap = _start != null && _start.Length > 0;
        for (int i = 0; i < segmentCount; i++)
        {
            // 沒有雷首時整根都用 loop——loop 本身上下貫穿、可平鋪，所以純 loop 疊起來完全無接縫，
            // 而且柱子頂端本來就延伸到畫面外（topY 取視窗上緣再往上 12%），不會看到「斷頭」。
            bool top = hasCap && i == segmentCount - 1;
            Sprite sprite = top ? _start[0] : _loop[0];
            var segment = new GameObject(top ? "Start" : $"Loop_{i}");
            segment.transform.SetParent(transform, false);
            segment.transform.position = new Vector3(impact.x, impact.y + (i + 0.5f) * segmentHeight, 0f);
            segment.transform.localScale = new Vector3(scale, scaleY, 1f);

            var renderer = segment.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            if (top) _startRenderer = renderer;
            else _loops.Add(renderer);
        }
    }

    void Update()
    {
        _elapsed += Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
        int loopFrame = Mathf.FloorToInt(_elapsed * _fps) % _loop.Length;
        if (_startRenderer != null && _start != null && _start.Length > 0)
        {
            int capFrame = Mathf.Min(_start.Length - 1, Mathf.FloorToInt(_elapsed * _fps * 0.35f));
            _startRenderer.sprite = _start[capFrame];
        }
        for (int i = 0; i < _loops.Count; i++)
            if (_loops[i] != null) _loops[i].sprite = _loop[loopFrame];

        if (_elapsed >= _duration) Destroy(gameObject);
    }
}
