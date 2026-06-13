using UnityEngine;

/// <summary>
/// 單一一次性特效實體：用一個 SpriteRenderer 播 VfxData 的序列圖。
/// Loop = false：播完一輪（AniNumber / AnimFPS 秒）後自毀。
/// Loop = true：循環播放，撐滿 Duration 秒後自毀（Duration &le; 0 視為 1 秒，避免永生）。
/// </summary>
public class VfxInstance : MonoBehaviour
{
    private VfxData _data;
    private SpriteRenderer _renderer;

    private float _animTimer;
    private int _animFrame;
    private float _lifeRemaining;
    private bool _initialized;

    public void Initialize(VfxData data, SpriteRenderer renderer)
    {
        _data = data;
        _renderer = renderer;

        if (_data == null || _renderer == null
            || _data.AnimationSprites == null || _data.AnimationSprites.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        transform.localScale = Vector3.one * _data.Scale;
        _renderer.sprite = _data.AnimationSprites[0];

        _lifeRemaining = ResolveLifetime();
        _animTimer = 0f;
        _animFrame = 0;
        _initialized = true;
    }

    /// <summary>非循環 = 一輪動畫長度；循環 = Duration（無效值退回 1 秒）。</summary>
    private float ResolveLifetime()
    {
        if (_data.Loop)
            return _data.Duration > 0f ? _data.Duration : 1f;

        int frames = _data.AnimationSprites.Length;
        if (_data.AnimFPS > 0f && frames > 0)
            return frames / _data.AnimFPS;

        // 靜態單張或沒給 FPS：退回 Duration，再退回一個保底秒數讓它看得見。
        return _data.Duration > 0f ? _data.Duration : 0.2f;
    }

    private void Update()
    {
        if (!_initialized) return;

        TickAnimation();

        _lifeRemaining -= Time.deltaTime;
        if (_lifeRemaining <= 0f)
            Destroy(gameObject);
    }

    private void TickAnimation()
    {
        if (_data.AnimationSprites.Length <= 1 || _data.AnimFPS <= 0f) return;

        _animTimer += Time.deltaTime;
        float frameDuration = 1f / _data.AnimFPS;
        while (_animTimer >= frameDuration)
        {
            _animTimer -= frameDuration;
            _animFrame = (_animFrame + 1) % _data.AnimationSprites.Length;
            _renderer.sprite = _data.AnimationSprites[_animFrame];
        }
    }
}
