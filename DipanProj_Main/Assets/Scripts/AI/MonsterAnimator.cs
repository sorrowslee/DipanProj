using UnityEngine;

/// <summary>
/// 怪物逐格動畫播放器（路線 B：純程式、不用 Unity Animator / AnimationClip）。
///
/// 開場由 <see cref="MonsterController"/> 呼叫 <see cref="Setup"/>，依怪名從 <see cref="MonsterSpriteLibrary"/>
/// 載入 idle / walk / attack / pant 的幀序列，之後每幀換 <see cref="SpriteRenderer"/>.sprite 播放當前狀態。
/// （<b>pant ＝ 喘息</b>：放完大絕後站著喘的破綻姿勢，目前只有紅嫁衣用，見 readme/BOSS_MODULE.md §2。
/// 動作名就是資料夾名，<see cref="MonsterSpriteLibrary"/> 與 Sync 工具都是通用字串／掃葉資料夾，加動作不必改它們。）
///
/// 防呆：只有「載得到圖」的狀態才算存在（<see cref="Has"/>）。要求播一個沒有圖的狀態時，
/// 自動退回 Attack→Walk→Idle、Pant→Idle；一張圖都沒有就整個不動（不會把 sprite 清成 null）。
/// 所以「有攻擊圖才會演攻擊、沒有就只走路/發呆」是天生行為，量產新怪不必改程式。
///
/// 走路播放速度跟實際移動速度連動（避免腳滑），作法同 <see cref="AnimatorSpeedByVelocity"/>
/// 但內建在這裡（不依賴 Unity Animator）。左右翻面仍由 MonsterController 控 SpriteRenderer.flipX，與本元件無關。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class MonsterAnimator : MonoBehaviour, IShadowAnchorSource
{
    public enum State { Idle, Walk, Attack, Pant }

    [Tooltip("基準播放幀率（幀/秒）；由 CSV 的 AnimFPS 帶入，留空＝8")]
    public float BaseFps = 8f;

    [Tooltip("走路速度連動的『正常移動速度』。走路 fps = BaseFps × clamp(實際速度/此值, MinMul, 1)")]
    public float ReferenceSpeed = 3f;

    [Tooltip("走路放慢時的最低倍率（避免太慢變超卡）")]
    public float MinMul = 0.6f;

    // ⭐ 要調喘息快慢就改這一行（不是 CSV 的 AnimFPS——那是 idle/walk/attack 共用的，改它會整隻怪一起變）。
    [Tooltip("pant（喘息）的播放倍率：相對 BaseFps 的倍數。1 = 與 idle/walk 同速；數字越小越慢。" +
             "實際幀率 = CSV 的 AnimFPS × 本倍率；一輪秒數 = 張數 ÷ 實際幀率。" +
             "紅嫁衣 AnimFPS=25、pant 50 張 ⇒ 1.0=2 秒一輪、0.5=4 秒、0.25=8 秒。" +
             "喘氣照原速播會太急促，放慢才像真的累了。")]
    public float PantFpsMul = 0.25f;

    [Tooltip("pant 乒乓輪播：播到最後一幀改成倒著播回第一幀，如此往復（0→N→0→N…）。" +
             "喘氣是吸↔吐的往復動作，首尾本來就接不起來，一般循環每播完一輪就會跳接一次、看得很清楚；" +
             "倒著播回去接縫自然消失，也不必為了對接去修素材。" +
             "⚠ 若哪天的 pant 素材是**不對稱**的動作（例如身體逐漸下沉），倒放會像倒帶 —— 那就把這個關掉。")]
    public bool PantPingPong = true;

    SpriteRenderer _sr;
    Sprite[] _idle, _walk, _attack, _pant;
    bool _hasAny;

    State _state = State.Idle;
    int _idx;
    int _dir = 1;   // 幀推進方向：一般循環恆為 +1；pant 乒乓時會在兩端翻成 -1（見 AdvanceFrame）

    // ── 影子錨點（每個動作一組，Setup 時從 ShadowAnchorTable／自動計算取好；見 ShadowAnchor.cs）──
    readonly System.Collections.Generic.Dictionary<State, ShadowAnchorPx> _shadow = new System.Collections.Generic.Dictionary<State, ShadowAnchorPx>();

    public bool TryGetShadowAnchor(out ShadowAnchorPx anchor)
    {
        if (_shadow.TryGetValue(_state, out anchor) && anchor.ok) return true;
        if (_shadow.TryGetValue(State.Idle, out anchor) && anchor.ok) return true;
        anchor = default;
        return false;
    }
    float _timer;
    float _currentSpeed;   // 由 MonsterController 每幀餵入，用於走路 fps 連動

    /// <summary>
    /// 依怪名載入各動作的幀。fps≤0 用 8、referenceSpeed≤0 用 3。
    /// <paramref name="tileSize"/> 決定顯示大小（PPU=256/tileSize），由 MonsterController 依 idle 可見高度自動換算後傳入
    /// → 與主角同一套：同一張圖在主角/怪物資料夾顯示一樣大。
    /// </summary>
    public void Setup(string monsterName, float fps, float referenceSpeed, float tileSize = 1f)
    {
        _sr = GetComponent<SpriteRenderer>();
        BaseFps = fps > 0f ? fps : 8f;
        ReferenceSpeed = referenceSpeed > 0f ? referenceSpeed : 3f;

        var lib = MonsterSpriteLibrary.Instance;
        // 逐動作高度正規化：walk/attack 對齊 idle 的可見高度，消除 AI 各動作大小落差（走路變大、停下變小）。
        float idleVis = StateVisH(lib, monsterName, "idle");
        _idle = lib.GetFrames(monsterName, "idle", tileSize);
        _walk = lib.GetFrames(monsterName, "walk", StateTile(lib, monsterName, "walk", tileSize, idleVis));
        _attack = lib.GetFrames(monsterName, "attack", StateTile(lib, monsterName, "attack", tileSize, idleVis));
        _pant = lib.GetFrames(monsterName, "pant", StateTile(lib, monsterName, "pant", tileSize, idleVis));

        // 【過渡期】角色取樣密度對齊背景（mipMapBias），見 CharacterMipBias 檔頭；背景解析度提上來後可拿掉這三行。
        CharacterMipBias.Register(_idle, transform);
        CharacterMipBias.Register(_walk, transform);
        CharacterMipBias.Register(_attack, transform);
        CharacterMipBias.Register(_pant, transform);

        _shadow.Clear();
        _shadow[State.Idle]   = lib.GetShadowAnchor(monsterName, "idle");
        _shadow[State.Walk]   = lib.GetShadowAnchor(monsterName, "walk");
        _shadow[State.Attack] = lib.GetShadowAnchor(monsterName, "attack");
        _shadow[State.Pant]   = lib.GetShadowAnchor(monsterName, "pant");

        // idle 是必備；萬一只給了 walk 沒給 idle，就用 walk 當待機後備（不至於沒圖）
        if (_idle == null && _walk != null) { _idle = _walk; if (!_shadow[State.Idle].ok) _shadow[State.Idle] = _shadow[State.Walk]; }

        _hasAny = _idle != null || _walk != null || _attack != null || _pant != null;
        if (!_hasAny)
        {
            Debug.LogWarning($"[MonsterAnimator] 怪物「{monsterName}」找不到任何動作圖。" +
                "確認圖放在 GameAssets/Modules/<關卡>/Monsters/SequenceImage/<怪名>/<idle|walk|attack|pant>/ 下，" +
                "且已執行 Project Tools → Sync Map Assets。");
            return;
        }

        _state = State.Idle;
        _idx = 0;
        _timer = 0f;
        ApplyFrame();   // 先把第 0 幀指上去，讓 MonsterController 的 AutoAdjustCollider 量得到尺寸
    }

    /// <summary>這隻怪有沒有這個動作的圖。</summary>
    // 逐動作高度正規化助手：讓某動作的顯示縮放(tileSize)使其「可見高度」= idle 的可見高度。
    static float StateVisH(MonsterSpriteLibrary lib, string name, string state)
        => (lib.TryGetVisibleBox(name, state, 1f, out var sz, out _) && sz.y > 0.0001f) ? sz.y : 0f;
    static float StateTile(MonsterSpriteLibrary lib, string name, string state, float baseTile, float idleVis)
    {
        float v = StateVisH(lib, name, state);
        return (idleVis > 0.0001f && v > 0.0001f) ? Mathf.Clamp(baseTile * (idleVis / v), 0.1f, 30f) : baseTile;
    }

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
            _dir = 1;      // 每次進入新狀態都從第一幀正向起算（乒乓不會延續上一個狀態的方向）
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
        else if (_state == State.Pant)
        {
            // 喘息刻意放慢（見 PantFpsMul）。與走路那條互斥：pant 不跟移動速度連動——她喘的時候站著不動，
            // 連動的話速度≈0 會被壓到 MinMul 而變成另一個數字，節奏就不是這裡設定的了。
            fps = BaseFps * Mathf.Max(0.01f, PantFpsMul);
        }
        if (fps <= 0.01f) return;

        float frameDur = 1f / fps;
        _timer += Time.deltaTime;
        while (_timer >= frameDur)
        {
            _timer -= frameDur;
            AdvanceFrame(frames.Length);
        }
        ApplyFrame();
    }

    /// <summary>推進一幀。一般狀態是循環（播完回第一幀）；pant 開了 <see cref="PantPingPong"/> 則來回播
    /// （0→N-1→0→…）。兩端**不重播同一幀**：碰到端點當下就翻方向，下一幀是倒數第二張，看起來才是平順的折返。</summary>
    void AdvanceFrame(int count)
    {
        if (count <= 1) return;

        if (_state == State.Pant && PantPingPong)
        {
            _idx += _dir;
            if (_idx >= count - 1) { _idx = count - 1; _dir = -1; }
            else if (_idx <= 0)    { _idx = 0;         _dir = 1; }
            return;
        }
        _idx = (_idx + 1) % count;
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
        // pant 沒圖就退回 idle（**不退 walk**）——喘息時本來就站著不動，退成走路會變原地踏步。
        if (s == State.Pant) return State.Idle;
        if (s == State.Walk) return State.Idle;
        return State.Idle;
    }

    Sprite[] FramesFor(State s)
    {
        switch (s)
        {
            case State.Walk: return _walk;
            case State.Attack: return _attack;
            case State.Pant: return _pant;
            default: return _idle;
        }
    }
}
