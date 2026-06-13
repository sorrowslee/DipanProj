using UnityEngine;

/// <summary>
/// 單一一次性特效實體：用一個 SpriteRenderer 播 VfxData 的序列圖。
/// Loop = false：**完整播完每一格後才銷毀**（銷毀由動畫進度驅動，與子彈速度/軌跡無關——
///   不管 AnimFPS 多慢，每一格都會顯示滿一個 frameDuration，播到最後一幀再撐滿一輪才消失）。
/// Loop = true：循環播放，撐滿 Duration 秒後自毀（Duration = 0 視為 1 秒；**Duration &lt; 0 = 無限循環**，
///   永不自毀、由外部負責 Destroy，例如火焰噴射器的火焰柱由 PlayerController 持有並在放開時清除）。
/// </summary>
public class VfxInstance : MonoBehaviour
{
    private VfxData _data;
    private SpriteRenderer _renderer;

    private float _animTimer;
    private int _animFrame;
    private float _lifeRemaining;   // 只在 Loop=true 或「靜態單張/沒給 FPS」時用到
    private bool _infiniteLife;     // Loop + Duration<0：永不自毀（外部管理生死）
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

        // Loop=true → 用 Duration 當壽命；靜態單張/沒 FPS → 用 Duration（再退保底）。
        // 一般的一次性序列圖不靠這個壽命，改由動畫進度在 Update 內決定何時銷毀。
        if (_data.Loop)
        {
            _infiniteLife = _data.Duration < 0f;   // Loop + Duration<0 = 無限循環（外部管理生死）
            _lifeRemaining = _data.Duration > 0f ? _data.Duration : 1f;
        }
        else
        {
            _lifeRemaining = _data.Duration > 0f ? _data.Duration : 0.2f;
        }

        _animTimer = 0f;
        _animFrame = 0;
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized) return;

        int last = _data.AnimationSprites.Length - 1;
        float frameDuration = (_data.AnimFPS > 0f) ? 1f / _data.AnimFPS : 0f;

        // 靜態單張或沒給 FPS：無動畫可播，用壽命計時顯示後銷毀
        if (last <= 0 || frameDuration <= 0f)
        {
            _lifeRemaining -= Time.deltaTime;
            if (_lifeRemaining <= 0f) Destroy(gameObject);
            return;
        }

        if (_data.Loop)
        {
            // 循環：繞回播放；撐滿 Duration 秒才銷毀
            _animTimer += Time.deltaTime;
            while (_animTimer >= frameDuration)
            {
                _animTimer -= frameDuration;
                _animFrame = (_animFrame + 1) % _data.AnimationSprites.Length;
                _renderer.sprite = _data.AnimationSprites[_animFrame];
            }
            if (!_infiniteLife)
            {
                _lifeRemaining -= Time.deltaTime;
                if (_lifeRemaining <= 0f) Destroy(gameObject);
            }
            return;
        }

        // 一次性：每一格顯示滿一個 frameDuration 才前進；播到最後一幀、再撐滿一個 frameDuration 後才銷毀。
        // 銷毀完全由「動畫播到哪」決定，與子彈飛行速度無關 → 保證完整播完每一格。
        _animTimer += Time.deltaTime;
        while (_animTimer >= frameDuration)
        {
            _animTimer -= frameDuration;
            if (_animFrame >= last)
            {
                Destroy(gameObject); // 最後一幀已顯示滿一輪 → 整段動畫播完
                return;
            }
            _animFrame++;
            _renderer.sprite = _data.AnimationSprites[_animFrame];
        }
    }
}
