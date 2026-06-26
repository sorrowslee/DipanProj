using UnityEngine;

/// <summary>
/// 玩家逐格動畫播放器（路線 B：純程式、不用 Unity Animator）。與 <see cref="MonsterAnimator"/> 同模式，
/// 多了「一次性動作（dead）播完定格」的支援，並用「血統(bloodline)」決定外型。
///
/// 狀態：Idle / Walk（循環）、Dead（一次性，播完停在最後一幀）。Attack 預留（一次性），有圖才會用。
/// 防呆：只有「載得到圖」的狀態才存在（<see cref="Has"/>）；要播沒圖的狀態時自動退回
/// Dead/Attack→Idle、Walk→Idle；一張圖都沒有就整個不動。
///
/// 走路 fps 跟實際移動速度連動（防腳滑），作法同 AnimatorSpeedByVelocity 但內建於此。
/// 左右翻面仍由 PlayerController 控 SpriteRenderer.flipX，與本元件無關。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimator : MonoBehaviour
{
    public enum State { Idle, Walk, Dead, Attack }

    public float BaseFps = 12f;
    public float ReferenceSpeed = 5f;   // 走路速度連動的「正常移動速度」
    public float MinMul = 0.6f;

    SpriteRenderer _sr;
    Sprite[] _idle, _walk, _dead, _attack;
    bool _hasAny;

    State _state = State.Idle;
    int _idx;
    float _timer;
    float _currentSpeed;
    bool _oneShotDone;   // 一次性動作（dead/attack）是否已播到最後一幀定格

    /// <summary>
    /// 依血統載入各動作的幀。fps≤0 用 12、referenceSpeed≤0 用 5。
    /// <paramref name="targetHeight"/> = 角色站立顯示高度（世界單位，≤0 用 1.95）：依 idle（取不到改 walk）
    /// 的可見像素高度自動換算每張的縮放(tileSize)，與來源解析度/留白無關，所有動作同一縮放（比例一致，
    /// dead 仍維持較矮的躺姿）。
    /// </summary>
    public void Setup(string bloodline, float fps, float referenceSpeed, float targetHeight = 1.95f)
    {
        _sr = GetComponent<SpriteRenderer>();
        BaseFps = fps > 0f ? fps : 12f;
        ReferenceSpeed = referenceSpeed > 0f ? referenceSpeed : 5f;
        if (targetHeight <= 0f) targetHeight = 1.95f;

        var lib = PlayerSpriteLibrary.Instance;

        // 自動換算顯示縮放：用 idle（取不到改 walk）的「可見高度」把幀放大到 targetHeight 世界高。
        // box.size.y 是世界單位 @ tileSize 1（= 可見像素 / 256）；tileSize = targetHeight / box.size.y。
        float tileSize = 1f;
        if (lib.TryGetVisibleBox(bloodline, "idle", out var vbox, out _) && vbox.y > 0.0001f)
            tileSize = targetHeight / vbox.y;
        else if (lib.TryGetVisibleBox(bloodline, "walk", out var wbox, out _) && wbox.y > 0.0001f)
            tileSize = targetHeight / wbox.y;
        tileSize = Mathf.Clamp(tileSize, 0.1f, 30f);

        _idle = lib.GetFrames(bloodline, "idle", tileSize);
        _walk = lib.GetFrames(bloodline, "walk", tileSize);
        _dead = lib.GetFrames(bloodline, "dead", tileSize);
        _attack = lib.GetFrames(bloodline, "attack", tileSize);

        if (_idle == null && _walk != null) _idle = _walk;   // 沒給 idle 就用 walk 當待機後備

        _hasAny = _idle != null || _walk != null || _dead != null || _attack != null;
        if (!_hasAny)
        {
            Debug.LogWarning($"[PlayerAnimator] 血統「{bloodline}」找不到任何外型圖。" +
                "確認圖放在 GameAssets/Main/Characters/SequenceImage/<血統>/<idle|walk|dead>/ 下，" +
                "且已執行 Project Tools → Sync Map Assets。");
            return;
        }

        _state = State.Idle;
        _idx = 0; _timer = 0f; _oneShotDone = false;
        ApplyFrame();
    }

    public bool Has(State s) => FramesFor(s) != null;

    /// <summary>設定當前狀態並餵入當前速度（走路 fps 連動）。沒有對應圖時自動退回。</summary>
    public void SetState(State s, float currentSpeed)
    {
        _currentSpeed = currentSpeed;
        s = Resolve(s);
        if (s != _state)
        {
            _state = s;
            _idx = 0; _timer = 0f; _oneShotDone = false;
            ApplyFrame();
        }
    }

    void Update()
    {
        if (!_hasAny) return;
        var frames = FramesFor(_state);
        if (frames == null || frames.Length == 0) return;
        if (frames.Length == 1) { ApplyFrame(); return; }

        bool loop = IsLooping(_state);
        if (!loop && _oneShotDone) return;   // 一次性已定格在最後一幀

        float fps = BaseFps;
        if (_state == State.Walk && ReferenceSpeed > 0.01f)
            fps = BaseFps * Mathf.Clamp(_currentSpeed / ReferenceSpeed, MinMul, 1f);
        if (fps <= 0.01f) return;

        float frameDur = 1f / fps;
        _timer += Time.deltaTime;
        while (_timer >= frameDur)
        {
            _timer -= frameDur;
            if (loop)
            {
                _idx = (_idx + 1) % frames.Length;
            }
            else
            {
                if (_idx < frames.Length - 1) _idx++;
                else { _oneShotDone = true; break; }   // 停在最後一幀
            }
        }
        ApplyFrame();
    }

    void ApplyFrame()
    {
        if (_sr == null) return;
        var frames = FramesFor(_state);
        if (frames == null || frames.Length == 0) return;
        if (_idx >= frames.Length) _idx = frames.Length - 1;
        _sr.sprite = frames[_idx];
    }

    static bool IsLooping(State s) => s == State.Idle || s == State.Walk;

    State Resolve(State s)
    {
        if (Has(s)) return s;
        // 一次性動作沒圖 → 退回 Idle；走路沒圖 → 退回 Idle
        return State.Idle;
    }

    Sprite[] FramesFor(State s)
    {
        switch (s)
        {
            case State.Walk: return _walk;
            case State.Dead: return _dead;
            case State.Attack: return _attack;
            default: return _idle;
        }
    }
}
