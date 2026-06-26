using UnityEngine;

/// <summary>
/// 怪物逐格動畫播放器（路線 B：純程式、不用 Unity Animator / AnimationClip）。
///
/// 開場由 <see cref="MonsterController"/> 呼叫 <see cref="Setup"/>，依怪名從 <see cref="MonsterSpriteLibrary"/>
/// 載入 idle / walk / attack 的幀序列，之後每幀換 <see cref="SpriteRenderer"/>.sprite 播放當前狀態。
///
/// 防呆：只有「載得到圖」的狀態才算存在（<see cref="Has"/>）。要求播一個沒有圖的狀態時，
/// 自動退回 Attack→Walk→Idle；一張圖都沒有就整個不動（不會把 sprite 清成 null）。
/// 所以「有攻擊圖才會演攻擊、沒有就只走路/發呆」是天生行為，量產新怪不必改程式。
///
/// 走路播放速度跟實際移動速度連動（避免腳滑），作法同 <see cref="AnimatorSpeedByVelocity"/>
/// 但內建在這裡（不依賴 Unity Animator）。左右翻面仍由 MonsterController 控 SpriteRenderer.flipX，與本元件無關。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class MonsterAnimator : MonoBehaviour
{
    public enum State { Idle, Walk, Attack }

    [Tooltip("基準播放幀率（幀/秒）；由 CSV 的 AnimFPS 帶入，留空＝8")]
    public float BaseFps = 8f;

    [Tooltip("走路速度連動的『正常移動速度』。走路 fps = BaseFps × clamp(實際速度/此值, MinMul, 1)")]
    public float ReferenceSpeed = 3f;

    [Tooltip("走路放慢時的最低倍率（避免太慢變超卡）")]
    public float MinMul = 0.6f;

    SpriteRenderer _sr;
    Sprite[] _idle, _walk, _attack;
    bool _hasAny;

    State _state = State.Idle;
    int _idx;
    float _timer;
    float _currentSpeed;   // 由 MonsterController 每幀餵入，用於走路 fps 連動

    /// <summary>依怪名載入各動作的幀。fps≤0 用 8、referenceSpeed≤0 用 3。</summary>
    public void Setup(string monsterName, float fps, float referenceSpeed)
    {
        _sr = GetComponent<SpriteRenderer>();
        BaseFps = fps > 0f ? fps : 8f;
        ReferenceSpeed = referenceSpeed > 0f ? referenceSpeed : 3f;

        var lib = MonsterSpriteLibrary.Instance;
        _idle = lib.GetFrames(monsterName, "idle");
        _walk = lib.GetFrames(monsterName, "walk");
        _attack = lib.GetFrames(monsterName, "attack");

        // idle 是必備；萬一只給了 walk 沒給 idle，就用 walk 當待機後備（不至於沒圖）
        if (_idle == null && _walk != null) _idle = _walk;

        _hasAny = _idle != null || _walk != null || _attack != null;
        if (!_hasAny)
        {
            Debug.LogWarning($"[MonsterAnimator] 怪物「{monsterName}」找不到任何動作圖。" +
                "確認圖放在 GameAssets/Modules/<關卡>/Monsters/SequenceImage/<怪名>/<idle|walk|attack>/ 下，" +
                "且已執行 Project Tools → Sync Map Assets。");
            return;
        }

        _state = State.Idle;
        _idx = 0;
        _timer = 0f;
        ApplyFrame();   // 先把第 0 幀指上去，讓 MonsterController 的 AutoAdjustCollider 量得到尺寸
    }

    /// <summary>這隻怪有沒有這個動作的圖。</summary>
    public bool Has(State s) => FramesFor(s) != null;

    /// <summary>
    /// 設定當前狀態並餵入當前速度（給走路 fps 連動）。沒有對應圖時自動退回 Attack→Walk→Idle。
    /// </summary>
    public void SetState(State s, float currentSpeed)
    {
        _currentSpeed = currentSpeed;
        s = Resolve(s);
        if (s != _state)
        {
            _state = s;
            _idx = 0;
            _timer = 0f;
            ApplyFrame();
        }
    }

    void Update()
    {
        if (!_hasAny) return;
        var frames = FramesFor(_state);
        if (frames == null || frames.Length == 0) return;
        if (frames.Length == 1) { ApplyFrame(); return; }   // 靜態姿勢

        float fps = BaseFps;
        if (_state == State.Walk && ReferenceSpeed > 0.01f)
        {
            float mul = Mathf.Clamp(_currentSpeed / ReferenceSpeed, MinMul, 1f);
            fps = BaseFps * mul;
        }
        if (fps <= 0.01f) return;

        float frameDur = 1f / fps;
        _timer += Time.deltaTime;
        while (_timer >= frameDur)
        {
            _timer -= frameDur;
            _idx = (_idx + 1) % frames.Length;
        }
        ApplyFrame();
    }

    void ApplyFrame()
    {
        if (_sr == null) return;
        var frames = FramesFor(_state);
        if (frames == null || frames.Length == 0) return;
        if (_idx >= frames.Length) _idx = 0;
        _sr.sprite = frames[_idx];
    }

    // 退回規則：要的狀態沒圖就往「一定有」的方向退（Attack→Walk→Idle）
    State Resolve(State s)
    {
        if (Has(s)) return s;
        if (s == State.Attack) return Has(State.Walk) ? State.Walk : State.Idle;
        if (s == State.Walk) return State.Idle;
        return State.Idle;
    }

    Sprite[] FramesFor(State s)
    {
        switch (s)
        {
            case State.Walk: return _walk;
            case State.Attack: return _attack;
            default: return _idle;
        }
    }
}
