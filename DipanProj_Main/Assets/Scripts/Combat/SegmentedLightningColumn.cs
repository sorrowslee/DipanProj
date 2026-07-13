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
        if (camera == null || _start == null || _start.Length == 0
            || _loop == null || _loop.Length == 0)
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
        if (_start == null) _start = Load(StartPath, 2);
        if (_loop == null) _loop = Load(LoopPath, 8);
    }

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
