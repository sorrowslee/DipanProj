using UnityEngine;

/// <summary>
/// 玩家逐格動畫播放器（路線 B：純程式、不用 Unity Animator）。與 <see cref="MonsterAnimator"/> 同模式，
/// 多了「一次性動作（dead）播完定格」的支援，並用「血統(bloodline)」決定外型。
///
/// 狀態：Idle / Walk / Attack（循環；Attack 攻擊按住時反覆播）、Dead（一次性，播完停在最後一幀）。有圖才會用。
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

    // ── 甦醒表演（趴地 → 倒播 dead 爬起）：進場「睜眼醒來」用，見 MapManager.FireEnterTriggersRoutine ──
    // 表演期間 SetState 全部忽略（HandleVisuals 每幀塞 Idle/Walk 也蓋不掉趴姿）；Dead 例外（真死打斷表演）。
    bool _lyingHold;            // 趴地定格中（顯示 dead 最後一幀，不動）
    bool _wakePlaying;          // 倒播爬起中
    int _wakeIdx;               // 倒播索引（dead 最後一幀 → 第 0 幀）
    System.Action _wakeDone;    // 爬起播完的回呼

    /// <summary>表演中（趴地定格或爬起倒播）。</summary>
    public bool IsWakeUpBusy => _lyingHold || _wakePlaying;

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

        // 逐動作高度正規化：walk/attack 對齊 idle 的可見高度（消除 AI 各動作大小落差）；dead 維持躺姿用 idle 縮放、不正規化。
        float idleVis = StateVisH(lib, bloodline, "idle");
        _idle = lib.GetFrames(bloodline, "idle", tileSize);
        _walk = lib.GetFrames(bloodline, "walk", StateTile(lib, bloodline, "walk", tileSize, idleVis));
        _dead = lib.GetFrames(bloodline, "dead", tileSize);
        _attack = lib.GetFrames(bloodline, "attack", StateTile(lib, bloodline, "attack", tileSize, idleVis));

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

    // 逐動作高度正規化助手：讓某動作的顯示縮放(tileSize)使其「可見高度」= idle 的可見高度。
    static float StateVisH(PlayerSpriteLibrary lib, string b, string state)
        => (lib.TryGetVisibleBox(b, state, out var sz, out _) && sz.y > 0.0001f) ? sz.y : 0f;
    static float StateTile(PlayerSpriteLibrary lib, string b, string state, float baseTile, float idleVis)
    {
        float v = StateVisH(lib, b, state);
        return (idleVis > 0.0001f && v > 0.0001f) ? Mathf.Clamp(baseTile * (idleVis / v), 0.1f, 30f) : baseTile;
    }

    public bool Has(State s) => FramesFor(s) != null;

    /// <summary>設定當前狀態並餵入當前速度（走路 fps 連動）。沒有對應圖時自動退回。</summary>
    public void SetState(State s, float currentSpeed)
    {
        _currentSpeed = currentSpeed;
        // 甦醒表演中：忽略一般狀態切換（趴姿/爬起不被 Idle/Walk 蓋掉）；真死（Dead）例外，打斷表演。
        if (IsWakeUpBusy)
        {
            if (s != State.Dead) return;
            CancelWakeUp(invokeDone: false);
        }
        s = Resolve(s);
        if (s != _state)
        {
            _state = s;
            _idx = 0; _timer = 0f; _oneShotDone = false;
            ApplyFrame();
        }
    }

    // ───────────────────────── 甦醒表演（睜眼醒來：趴地 → 倒播 dead 爬起） ─────────────────────────

    /// <summary>
    /// 立即趴地定格（顯示 dead 的最後一幀＝完全倒地），維持到 <see cref="PlayWakeUp"/> 被呼叫。
    /// 沒有 dead 圖（該血統沒給倒地素材）回 false＝不表演，呼叫端直接跳過。
    /// </summary>
    public bool HoldLyingPose()
    {
        if (_dead == null || _dead.Length == 0 || _sr == null) return false;
        _lyingHold = true; _wakePlaying = false; _wakeDone = null;
        _sr.sprite = _dead[_dead.Length - 1];
        return true;
    }

    /// <summary>
    /// 倒播 dead（最後一幀 → 第 0 幀）＝爬起動畫，播完自動回 Idle 並回呼 onDone。
    /// 沒有 dead 圖時直接回 Idle＋立即回呼（防呆）。速率用 BaseFps（與死亡同節奏）。
    /// </summary>
    public void PlayWakeUp(System.Action onDone)
    {
        if (_dead == null || _dead.Length == 0 || _sr == null)
        {
            CancelWakeUp(invokeDone: false);
            onDone?.Invoke();
            return;
        }
        _lyingHold = false;
        _wakePlaying = true;
        _wakeIdx = _dead.Length - 1;
        _timer = 0f;
        _wakeDone = onDone;
        _sr.sprite = _dead[_wakeIdx];
    }

    // 結束/打斷表演：回 Idle 第 0 幀。invokeDone=true 時觸發播完回呼。
    void CancelWakeUp(bool invokeDone)
    {
        _lyingHold = false; _wakePlaying = false;
        var done = _wakeDone; _wakeDone = null;
        _state = State.Idle; _idx = 0; _timer = 0f; _oneShotDone = false;
        ApplyFrame();
        if (invokeDone) done?.Invoke();
    }

    void Update()
    {
        if (!_hasAny) return;

        // 甦醒表演：趴地定格＝什麼都不做（sprite 已定在倒地幀）；爬起＝依 BaseFps 倒播 dead。
        if (_lyingHold) return;
        if (_wakePlaying)
        {
            if (BaseFps <= 0.01f) { CancelWakeUp(invokeDone: true); return; }
            _timer += Time.deltaTime;
            float dur = 1f / BaseFps;
            while (_timer >= dur)
            {
                _timer -= dur;
                if (_wakeIdx > 0) _wakeIdx--;
                else { CancelWakeUp(invokeDone: true); return; }   // 倒播到第 0 幀 → 起身完成，回 Idle
            }
            _sr.sprite = _dead[Mathf.Clamp(_wakeIdx, 0, _dead.Length - 1)];
            return;
        }

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

    // Idle/Walk/Attack 循環播（攻擊按住時 cast 反覆播）；Dead 一次性（播完定格最後一幀）。
    static bool IsLooping(State s) => s == State.Idle || s == State.Walk || s == State.Attack;

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
