using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 由頂端 start + N 個 tileable loop 組成的全高落雷。
/// loop 一路延伸到 impact，不使用會突然收細的 end；每段保持同一寬度，只微調 Y 尺寸讓總長無縫閉合。
/// </summary>
public sealed class SegmentedLightningColumn : MonoBehaviour
{
    const string StartPath = "VfxEffects/SkyLightningColumn/Start/Start";
    const string LoopPath = "VfxEffects/SkyLightningColumn/Loop/Loop";
    const int SortingOrder = 22000;
    const float DefaultScale = 1.5f;
    const float DefaultFps = 20f;
    const float DefaultDuration = 0.45f;

    static Sprite[] _start;
    static Sprite[] _loop;

    readonly List<SpriteRenderer> _loops = new List<SpriteRenderer>();
    SpriteRenderer _startRenderer;
    float _fps;
    float _duration;
    float _elapsed;

    public static void Spawn(Vector2 impact, Camera camera, float scale = DefaultScale,
        float fps = DefaultFps, float duration = DefaultDuration)
    {
        EnsureLoaded();
        // 用 IsStale 而非「陣列 == null」：Load() 永遠回傳非 null 陣列（即使每格 Resources.Load 都失敗），
        // 舊守衛攔不到「陣列在但元素全 null」，會一路走到底下取 _loop[0].bounds 而丟 NullReference。
        if (camera == null || IsStale(_start) || IsStale(_loop))
        {
            Debug.LogWarning("分段雷柱素材未完整載入，略過雷柱視覺。");
            return;
        }

        var go = new GameObject("SegmentedSkyLightning");
        var column = go.AddComponent<SegmentedLightningColumn>();
        column.Build(impact, camera, scale, fps, duration);
    }

    static void EnsureLoaded()
    {
        // ⚠ 陣列型的 static 快取「不能」只判斷 `_x == null`：
        // 關掉 Domain Reload 後，停止 Play 被銷毀的是陣列「裡面的 Sprite」，
        // 陣列本身是純 C# 物件、永遠不會變 null → 舊寫法不會重載，第二次 Play 會拿到一堆已銷毀的 Sprite
        // （症狀：雷柱不見／變白塊）。所以要連「元素是否還活著」一起判。
        // 見 readme/PROBLEMS.md 的 Domain Reload 系列（I3/I5/I7）與 PlayModeStaticReset.cs 的註記。
        if (IsStale(_start)) _start = Load(StartPath, 2);
        if (IsStale(_loop)) _loop = Load(LoopPath, 8);
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

    void Build(Vector2 impact, Camera camera, float scale, float fps, float duration)
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

        for (int i = 0; i < segmentCount; i++)
        {
            bool top = i == segmentCount - 1;
            Sprite sprite = top ? _start[0] : _loop[0];
            var segment = new GameObject(top ? "Start" : $"Loop_{i}");
            segment.transform.SetParent(transform, false);
            segment.transform.position = new Vector3(impact.x, impact.y + (i + 0.5f) * segmentHeight, 0f);
            segment.transform.localScale = new Vector3(scale, scaleY, 1f);

            var renderer = segment.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = SortingOrder;
            if (top) _startRenderer = renderer;
            else _loops.Add(renderer);
        }
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        int loopFrame = Mathf.FloorToInt(_elapsed * _fps) % _loop.Length;
        int capFrame = Mathf.Min(1, Mathf.FloorToInt(_elapsed * _fps * 0.35f));
        if (_startRenderer != null) _startRenderer.sprite = _start[capFrame];
        for (int i = 0; i < _loops.Count; i++)
            if (_loops[i] != null) _loops[i].sprite = _loop[loopFrame];

        if (_elapsed >= _duration) Destroy(gameObject);
    }
}
