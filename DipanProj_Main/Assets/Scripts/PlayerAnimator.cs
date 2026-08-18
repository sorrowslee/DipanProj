using System.Collections.Generic;
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
    bool _fallPlaying;          // 正播倒下中（血統變身用；播完轉成趴地定格，不是回 Idle）
    int _wakeIdx;               // 倒播索引（dead 最後一幀 → 第 0 幀）
    int _fallIdx;               // 正播索引（第 0 幀 → dead 最後一幀）
    System.Action _wakeDone;    // 爬起播完的回呼
    System.Action _fallDone;    // 倒下播完的回呼
    float _poseFpsMul = 1f;     // 表演速率倍率（>1 = 更快）。倒下/爬起共用，由呼叫端指定

    /// <summary>表演中（倒下正播、趴地定格或爬起倒播）。</summary>
    public bool IsWakeUpBusy => _lyingHold || _wakePlaying || _fallPlaying;

    // ── 可見身體的幾何（Setup 時一次算好，不用每幀量貼圖）──
    // ⚠ 為什麼不直接讀 SpriteRenderer.bounds：那個含不含四周的透明留白，取決於 sprite 的 mesh 型別
    //   （Tight/FullRect），在這條「執行期 Sprite.Create」的管線上並不保證。
    //   這裡改成從 Setup 已知的縮放參數解析算出來，跟 Unity 怎麼算 bounds 無關，永遠精確。
    //   給「要蓋住玩家 / 要對準腳下」的特效用（佛光光環、血統變身的雷擊點…）。
    readonly Dictionary<State, float> _visH = new Dictionary<State, float>();      // 可見高（世界單位）
    readonly Dictionary<State, float> _footRel = new Dictionary<State, float>();   // 可見腳底相對 transform 的 Y 位移（負＝在下方）

    State GeomState => (_lyingHold || _wakePlaying || _fallPlaying) ? State.Dead : _state;

    /// <summary>目前姿勢下「可見身體」的高度（世界單位）。取不到回 0。</summary>
    public float VisibleHeight => _visH.TryGetValue(GeomState, out var v) ? v : 0f;

    /// <summary>目前姿勢下「可見腳底」相對 transform 的 Y 位移（負＝在 transform 下方）。</summary>
    public float FeetOffsetY => _footRel.TryGetValue(GeomState, out var v) ? v : 0f;

    /// <summary>目前姿勢下「可見身體中心」相對 transform 的 Y 位移。</summary>
    public float BodyCenterOffsetY => FeetOffsetY + VisibleHeight * 0.5f;

    /// <summary>
    /// 依血統載入各動作的幀。fps≤0 用 12、referenceSpeed≤0 用 5。
    /// <paramref name="targetHeight"/> = 角色站立顯示高度（世界單位，≤0 用 1.95）：依 idle（取不到改 walk）
    /// 的可見像素高度自動換算每張的縮放(tileSize)，與來源解析度/留白無關，所有動作同一縮放（比例一致，
    /// dead 仍維持較矮的躺姿）。
    /// </summary>
    /// <param name="bodyScale">
    /// 體型倍率（1 = 原樣）。只用來決定 sprite 的 pivot——放大時讓**可見腳底留在原位、只往上長**，
    /// 而不是置中 pivot 那樣上下同時長（1.5 倍會讓腳往下沉快半格）。
    /// 顯示大小本身是靠 <paramref name="targetHeight"/> 控制（呼叫端已經把倍率乘進去了）。
    /// </param>
    public void Setup(string bloodline, float fps, float referenceSpeed, float targetHeight = 1.95f,
                      float bodyScale = 1f)
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
        if (bodyScale <= 0.01f) bodyScale = 1f;
        _idle = lib.GetFrames(bloodline, "idle", tileSize, bodyScale);
        _walk = lib.GetFrames(bloodline, "walk", StateTile(lib, bloodline, "walk", tileSize, idleVis), bodyScale);
        _dead = lib.GetFrames(bloodline, "dead", tileSize, bodyScale);
        _attack = lib.GetFrames(bloodline, "attack", StateTile(lib, bloodline, "attack", tileSize, idleVis), bodyScale);

        // 各動作的可見幾何（給特效對位用）。與 GetFrames 用同一組 tileSize，算出來才對得上。
        _visH.Clear(); _footRel.Clear();
        CacheGeometry(lib, bloodline, "idle", State.Idle, tileSize, bodyScale);
        CacheGeometry(lib, bloodline, "walk", State.Walk, StateTile(lib, bloodline, "walk", tileSize, idleVis), bodyScale);
        CacheGeometry(lib, bloodline, "dead", State.Dead, tileSize, bodyScale);
        CacheGeometry(lib, bloodline, "attack", State.Attack, StateTile(lib, bloodline, "attack", tileSize, idleVis), bodyScale);

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

    // 算某動作的可見幾何：可見高 = 可見框高 × tileSize；腳底位移 = (可見底緣比例 − pivot) × tileSize。
    void CacheGeometry(PlayerSpriteLibrary lib, string bloodline, string state, State key,
                       float stateTile, float bodyScale)
    {
        if (!lib.TryGetVisibleBox(bloodline, state, out var size, out var offset, out var canvas)) return;
        float fy = PlayerSpriteLibrary.VisibleBottomFraction(size, offset, canvas);
        float pivotY = PlayerSpriteLibrary.FootPivotY(fy, bodyScale);
        _visH[key] = size.y * stateTile;
        _footRel[key] = (fy - pivotY) * stateTile;
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
        _lyingHold = true;
        _wakePlaying = false; _wakeDone = null;
        _fallPlaying = false; _fallDone = null;
        _sr.sprite = _dead[_dead.Length - 1];
        return true;
    }

    /// <summary>
    /// 中止目前的表演（倒下／趴地／爬起），角色回 Idle。表演的回呼**不會**被觸發。
    /// 給演出被外力打斷時收尾用——不叫的話 <c>_lyingHold</c> 會一直是 true、
    /// <c>IsWakeUpBusy</c> 恆真、SetState 全被忽略，角色會永遠定格在趴姿。
    /// 沒在表演中則什麼都不做。
    /// </summary>
    public void CancelPose()
    {
        if (!IsWakeUpBusy) return;
        CancelWakeUp(invokeDone: false);
    }

    /// <summary>
    /// 正播 dead（第 0 幀 → 最後一幀）＝倒下動畫，**播完轉成趴地定格**（不是回 Idle），
    /// 接著回呼 onDone。這是 <see cref="PlayWakeUp"/> 的鏡像，給「血統變身」這種
    /// 「倒下 → 演出 → 爬起」的表演用。
    ///
    /// ⚠ 與 <c>SetState(State.Dead)</c> 的差別：那個會被 PlayerController 每幀的 HandleVisuals
    /// 塞回 Idle/Walk 蓋掉（只有真死 <c>_isDead</c> 才會 return 在它之前）；
    /// 這裡走 <see cref="IsWakeUpBusy"/>，SetState 期間全部忽略，所以不會被蓋。
    /// 也不會觸發任何死亡流程（真死是 PlayerController.Die → EndLevel）。
    ///
    /// 沒有 dead 圖時立即回呼（防呆，呼叫端的流程照走、只是沒有倒下動畫）。
    /// </summary>
    /// <param name="fpsMul">速率倍率（>1 更快）。≤0 視為 1。</param>
    public void PlayFallDown(System.Action onDone, float fpsMul = 1f)
    {
        _poseFpsMul = fpsMul > 0.01f ? fpsMul : 1f;
        if (_dead == null || _dead.Length == 0 || _sr == null)
        {
            onDone?.Invoke();
            return;
        }
        _lyingHold = false;
        _wakePlaying = false; _wakeDone = null;   // 對稱清除：免得殘留的爬起回呼之後被誤觸發
        _fallPlaying = true;
        _fallIdx = 0;
        _timer = 0f;
        _fallDone = onDone;
        _sr.sprite = _dead[0];
    }

    /// <summary>
    /// 趴地定格中，把 sprite 重新定到「目前這組 dead 幀」的最後一幀。
    ///
    /// **換血統之後一定要呼叫。** 因為 <c>PlayerController.SetBloodline</c> 內部會重跑
    /// <see cref="Setup"/>，把 sprite 換成新血統的 idle 第 0 幀（站姿）；但 <c>_lyingHold</c>
    /// 還是 true，Update 直接 return 不再更新 sprite → 角色會**站著定格不動**。
    /// 不在趴地定格中則什麼都不做。
    /// </summary>
    public void RefreshLyingPose()
    {
        if (!_lyingHold || _dead == null || _dead.Length == 0 || _sr == null) return;
        _sr.sprite = _dead[_dead.Length - 1];
    }

    /// <summary>
    /// 倒播 dead（最後一幀 → 第 0 幀）＝爬起動畫，播完自動回 Idle 並回呼 onDone。
    /// 沒有 dead 圖時直接回 Idle＋立即回呼（防呆）。速率用 BaseFps（與死亡同節奏）。
    /// </summary>
    /// <param name="fpsMul">速率倍率（>1 更快）。≤0 視為 1。</param>
    public void PlayWakeUp(System.Action onDone, float fpsMul = 1f)
    {
        _poseFpsMul = fpsMul > 0.01f ? fpsMul : 1f;
        if (_dead == null || _dead.Length == 0 || _sr == null)
        {
            CancelWakeUp(invokeDone: false);
            onDone?.Invoke();
            return;
        }
        _lyingHold = false;
        _fallPlaying = false; _fallDone = null;   // 對稱清除（見 PlayFallDown）
        _wakePlaying = true;
        _wakeIdx = _dead.Length - 1;
        _timer = 0f;
        _wakeDone = onDone;
        _sr.sprite = _dead[_wakeIdx];
    }

    /// <summary>這段表演每幀的秒數（0 = BaseFps 無效，呼叫端要當成立即結束）。</summary>
    float PoseFrameDuration => (BaseFps * _poseFpsMul) <= 0.01f ? 0f : 1f / (BaseFps * _poseFpsMul);

    // 倒下播完 → 轉成趴地定格（維持 IsWakeUpBusy=true，SetState 仍被忽略），再回呼。
    void FinishFallDown()
    {
        _fallPlaying = false;
        _lyingHold = true;
        _timer = 0f;
        if (_dead != null && _dead.Length > 0 && _sr != null) _sr.sprite = _dead[_dead.Length - 1];
        var done = _fallDone; _fallDone = null;
        done?.Invoke();
    }

    // 結束/打斷表演：回 Idle 第 0 幀。invokeDone=true 時觸發播完回呼（倒下與爬起的回呼都會被叫到，
    // 因為「被打斷」對呼叫端而言也是「這段結束了」——不叫的話等在旗標上的協程會永遠卡住）。
    void CancelWakeUp(bool invokeDone)
    {
        _lyingHold = false; _wakePlaying = false; _fallPlaying = false;
        var done = _wakeDone; _wakeDone = null;
        var fallDone = _fallDone; _fallDone = null;
        _poseFpsMul = 1f;
        _state = State.Idle; _idx = 0; _timer = 0f; _oneShotDone = false;
        ApplyFrame();
        // 兩個回呼最多只有一個非 null（三個表演旗標互斥，且 FinishFallDown 交棒時已把 _fallDone 清成 null），
        // 所以這裡不會「同一次取消觸發兩段流程」。寫成兩個都叫是為了不用先判斷現在是哪一段。
        if (invokeDone) { done?.Invoke(); fallDone?.Invoke(); }
    }

    void Update()
    {
        if (!_hasAny) return;

        // 甦醒表演：倒下＝正播 dead（播完轉趴地定格）；趴地定格＝什麼都不做（sprite 已定在倒地幀）；
        // 爬起＝倒播 dead。三者都用 Time.deltaTime，所以演出期間 timeScale 必須維持 1。
        if (_fallPlaying)
        {
            if (_dead == null || _dead.Length == 0) { FinishFallDown(); return; }   // 該血統沒有倒地素材
            float fallDur = PoseFrameDuration;
            if (fallDur <= 0f) { FinishFallDown(); return; }
            _timer += Time.deltaTime;
            while (_timer >= fallDur)
            {
                _timer -= fallDur;
                if (_fallIdx < _dead.Length - 1) _fallIdx++;
                else { FinishFallDown(); return; }   // 正播到最後一幀 → 轉趴地定格
            }
            _sr.sprite = _dead[Mathf.Clamp(_fallIdx, 0, _dead.Length - 1)];
            return;
        }
        if (_lyingHold) return;
        if (_wakePlaying)
        {
            float dur = PoseFrameDuration;
            if (dur <= 0f) { CancelWakeUp(invokeDone: true); return; }
            _timer += Time.deltaTime;
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
