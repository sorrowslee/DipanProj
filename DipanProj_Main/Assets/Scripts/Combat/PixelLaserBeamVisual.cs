using System.Collections.Generic;
using UnityEngine;
using Sorrows.Ballistics;

/// <summary>
/// 將 Super Pixel Projectiles Pack 4 的 origin / center loop / impact loop
/// 沿 LaserBeam 已算好的反射折線渲染。只負責外觀，不參與碰撞或傷害。
/// </summary>
public sealed class PixelLaserBeamVisual : MonoBehaviour
{
    const int FrameCount = 8;
    const float Fps = 20f;
    const float SourceVisibleThickness = 0.18f; // A 組在 PPU100 時白熱光柱的近似可見厚度
    const float SegmentOverlap = 0.08f;
    const int SortingOrder = 22000;

    LaserBeam _beam;
    Sprite[] _originFrames;
    Sprite[] _centerFrames;
    Sprite[] _impactFrames;
    SpriteRenderer _origin;
    SpriteRenderer _impact;
    readonly List<SpriteRenderer> _segments = new List<SpriteRenderer>();
    readonly List<SpriteRenderer> _bounceFlares = new List<SpriteRenderer>();
    float _visualScale = 1f;
    int _frame = -1;

    public void Initialize(LaserBeam beam, string setName, float beamWidth)
    {
        _beam = beam;
        string root = ResolveRoot(setName);
        _originFrames = LoadFrames(root + "/Origin/Origin");
        _centerFrames = LoadFrames(root + "/Center/Center");
        _impactFrames = LoadFrames(root + "/Impact/Impact");
        _visualScale = Mathf.Max(0.25f, beamWidth / SourceVisibleThickness);

        if (!FramesValid(_originFrames) || !FramesValid(_centerFrames) || !FramesValid(_impactFrames))
        {
            Debug.LogWarning($"像素雷射素材組 '{setName}' 未完整載入，略過像素雷射視覺。");
            enabled = false;
            return;
        }

        _origin = CreateRenderer("PixelBeamOrigin", false, SortingOrder + 2);
        _impact = CreateRenderer("PixelBeamImpact", false, SortingOrder + 3);
        RefreshFrame(0);
    }

    static string ResolveRoot(string setName)
    {
        switch (setName)
        {
            case "A_Blue": return "VfxEffects/PixelLaserA_Blue";
            default: return "VfxEffects/PixelLaserA_Blue";
        }
    }

    static Sprite[] LoadFrames(string prefix)
    {
        var frames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
            frames[i] = Resources.Load<Sprite>($"{prefix}_{i + 1:D2}");
        return frames;
    }

    static bool FramesValid(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0) return false;
        for (int i = 0; i < frames.Length; i++)
            if (frames[i] == null) return false;
        return true;
    }

    SpriteRenderer CreateRenderer(string objectName, bool tiled, int sortingOrder)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;
        if (tiled) sr.drawMode = SpriteDrawMode.Tiled;
        return sr;
    }

    void LateUpdate()
    {
        if (_beam == null || _centerFrames == null) return;
        IReadOnlyList<Vector2> points = _beam.Points;
        if (points == null || points.Count < 2) return;

        int frame = Mathf.FloorToInt(Time.time * Fps) % FrameCount;
        if (frame != _frame) RefreshFrame(frame);

        EnsureCount(_segments, points.Count - 1, "PixelBeamSegment", true, SortingOrder);
        EnsureCount(_bounceFlares, Mathf.Max(0, points.Count - 2), "PixelBeamBounce", false, SortingOrder + 4);

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[i + 1];
            Vector2 delta = b - a;
            float length = Mathf.Max(0.01f, delta.magnitude);
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            SpriteRenderer sr = _segments[i];
            sr.gameObject.SetActive(true);
            sr.transform.position = (a + b) * 0.5f;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            sr.transform.localScale = new Vector3(_visualScale, _visualScale, 1f);
            // 中心圖沿本地 X 軸平鋪；少量重疊可蓋住反射折角的像素縫。
            sr.size = new Vector2(length / _visualScale + SegmentOverlap,
                _centerFrames[frame].bounds.size.y);
        }

        SetEndpoint(_origin, points[0], points[1] - points[0], _originFrames[frame], _visualScale);
        SetEndpoint(_impact, points[points.Count - 1], points[points.Count - 1] - points[points.Count - 2],
            _impactFrames[frame], _visualScale);

        for (int i = 1; i < points.Count - 1; i++)
        {
            SpriteRenderer flare = _bounceFlares[i - 1];
            Vector2 incoming = points[i] - points[i - 1];
            SetEndpoint(flare, points[i], incoming, _impactFrames[frame], _visualScale * 0.72f);
        }
    }

    void RefreshFrame(int frame)
    {
        _frame = frame;
        if (_origin != null) _origin.sprite = _originFrames[frame];
        if (_impact != null) _impact.sprite = _impactFrames[frame];
        for (int i = 0; i < _segments.Count; i++) _segments[i].sprite = _centerFrames[frame];
        for (int i = 0; i < _bounceFlares.Count; i++) _bounceFlares[i].sprite = _impactFrames[frame];
    }

    void EnsureCount(List<SpriteRenderer> list, int count, string prefix, bool tiled, int sortingOrder)
    {
        while (list.Count < count)
        {
            SpriteRenderer sr = CreateRenderer(prefix + "_" + list.Count, tiled, sortingOrder);
            sr.sprite = tiled ? _centerFrames[Mathf.Max(0, _frame)] : _impactFrames[Mathf.Max(0, _frame)];
            list.Add(sr);
        }
        for (int i = 0; i < list.Count; i++)
            list[i].gameObject.SetActive(i < count);
    }

    static void SetEndpoint(SpriteRenderer sr, Vector2 position, Vector2 direction, Sprite sprite, float scale)
    {
        if (sr == null) return;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        sr.gameObject.SetActive(true);
        sr.sprite = sprite;
        sr.transform.position = position;
        sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        sr.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
