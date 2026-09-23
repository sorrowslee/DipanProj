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
    public enum State { Idle, Walk, Attack, Pant, Jump }

    [Tooltip("基準播放幀率（幀/秒）；由 CSV 的 AnimFPS 帶入，留空＝8")]
    public float BaseFps = 8f;

    [Tooltip("走路速度連動的『正常移動速度』。走路 fps = BaseFps × clamp(實際速度/此值, MinMul, MaxMul)")]
    public float ReferenceSpeed = 3f;

    [Tooltip("走路放慢時的最低倍率（避免太慢變超卡）")]
    public float MinMul = 0.6f;

    // ⭐ 2026-09-17 加。以前這裡的上限**寫死 1**，意思是「跑得再快，腳步頻率最多就是 BaseFps」。
    // 對一般怪沒差（牠們的實際速度恆等於 CSV 的 Speed ⇒ 倍率恆為 1），但對**會短暫加速的怪**是硬傷：
    // 撲擊型的狼衝刺時位移是平常的 3 倍，腳步卻還是散步的節奏 ⇒ 視覺上是「滑過去」不是「衝過去」，
    // 作者實機回報「衝過去的速度跟平常移動時一樣」——其實位移早就 3 倍了，**沒有突襲感是動畫給的，不是數字給的**。
    // 預設 2.5 對所有既有怪是零變化（跑不過 ReferenceSpeed 就吃不到上限）；不想要就設回 1。
    [Tooltip("走路加快時的最高倍率。1 = 舊行為（再快也不加速）。撲擊型的怪衝刺時靠它做出狂奔的腳步節奏")]
    public float MaxMul = 2.5f;

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
    Sprite[] _idle, _walk, _attack, _pant, _jump;
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

    // ── 骨牢錨點（2026-09-23）：「罩住整個身體」的東西該對準哪一點 ──
    // 演算法與理由都在 BoneCageVisual.ComputeIdleAnchor（玩家端 PlayerAnimator 共用同一份）。這裡只負責快取。
    bool _cageCached, _cageOk;
    Vector2 _cageLocal;
    float _cageTorsoW;

    /// <summary>idle 軀幹寬（本地單位，未乘 lossyScale）；取不到回 0。見 BoneCageVisual.ComputeIdleAnchor。</summary>
    public float CageTorsoWidthLocal
    {
        get { if (!_cageCached) TryGetCageAnchorLocal(out _); return _cageOk ? _cageTorsoW : 0f; }
    }

    /// <summary>
    /// 骨牢中心：X＝idle 軀幹中心、Y＝idle 地面線，相對 transform 的本地位移（**未乘 lossyScale、未翻面**）。
    /// 第一次呼叫時算、之後快取（換外型 Setup 時清掉）。見 BoneCageVisual.ComputeIdleAnchor。
    /// </summary>
    public bool TryGetCageAnchorLocal(out Vector2 local)
    {
        if (!_cageCached)
        {
            _cageCached = true;
            _shadow.TryGetValue(State.Idle, out var a);
            _cageOk = BoneCageVisual.ComputeIdleAnchor(_idle ?? _walk, a, out _cageLocal, out _cageTorsoW);
        }
        local = _cageLocal;
        return _cageOk;
    }
    float _timer;
    float _currentSpeed;   // 由 MonsterController 每幀餵入，用於走路 fps 連動

    // ── one-shot（播一段幀區間一次就停，2026-09-18 為跳躍踐踏加）──
    // 為什麼需要：既有播放一律是「循環」，而「跳躍」這種**有頭有尾的動作**不能循環——
    // 而且 MonsterController.HandleVisuals 每幀都會依「距離/位移」呼叫 SetState 覆寫狀態，
    // Brain 光是 SetState(Jump) 撐不過下一幀。所以 one-shot 期間**直接壓過 SetState**（見 SetState）。
    // 另一半的用意是「幀號即事件」：Brain 用 OneShotFrame 對齊起跳/落地那一幀，
    // 這樣改 CSV 的 AnimFPS 時，整段跳躍的節奏與時機**自動跟著對**（同 ArcherBrain 的放箭幀，見 BOSS_MODULE §8.2b）。
    bool  _osActive;      // one-shot 進行中（含播完停在最後一幀、等 Brain 來收）
    bool  _osDone;        // 已播到結束幀
    int   _osStart, _osEnd;   // 0-based 幀索引（含兩端）
    float _osFpsMul = 1f;

    /// <summary>one-shot 進行中（播完停在結束幀也算，直到 <see cref="CancelOneShot"/>）。</summary>
    public bool OneShotPlaying => _osActive;
    /// <summary>one-shot 已播到結束幀。</summary>
    public bool OneShotFinished => _osActive && _osDone;
    /// <summary>目前播到第幾幀（**1-based**，與資料夾裡的檔名編號一致）。沒在播 one-shot 時回 0。</summary>
    public int OneShotFrame => _osActive ? _idx + 1 : 0;

    /// <summary>
    /// 播一段幀區間**一次**就停（停在結束幀，直到 <see cref="CancelOneShot"/>）。
    /// <paramref name="startFrame"/>／<paramref name="endFrame"/> 是 **1-based**（＝資料夾裡的檔名編號，肉眼對得起來）。
    /// endFrame ≤ 0 或超出張數＝播到最後一張。沒有這個動作的圖時回 false（**呼叫端要有 plan B**，
    /// 不像 SetState 會自動退回——退回去循環播走路只會讓跳躍變成滑行）。
    /// </summary>
    public bool PlayOneShot(State s, int startFrame = 1, int endFrame = 0, float fpsMul = 1f)
    {
        var frames = FramesFor(s);
        if (frames == null || frames.Length == 0) return false;

        _osActive = true;
        _osDone = false;
        _osStart = Mathf.Clamp(startFrame - 1, 0, frames.Length - 1);
        _osEnd   = (endFrame <= 0) ? frames.Length - 1 : Mathf.Clamp(endFrame - 1, _osStart, frames.Length - 1);
        _osFpsMul = fpsMul > 0.01f ? fpsMul : 1f;

        _state = s;
        _idx = _osStart;
        _dir = 1;
        _timer = 0f;
        ApplyFrame();
        return true;
    }

    /// <summary>
    /// 把「正在循環播的 attack」轉成 one-shot：**從目前這一幀播到最後一幀就停**（之後由呼叫端 <see cref="CancelOneShot"/> 收）。
    /// 給骨牢用（2026-09-23 作者：「讓怪物把動作播完，然後就變回 idle 等待骨牢破碎」）——
    /// 直接切 idle 會把揮到一半的刀硬生生砍斷，讓它循環又會一直揮。已經在 one-shot 或不是 attack 時什麼都不做。
    /// </summary>
    public void FinishAttackCycle()
    {
        if (_osActive || _state != State.Attack) return;
        var frames = FramesFor(_state);
        if (frames == null || frames.Length == 0) return;
        _osActive = true;
        _osDone = false;
        _osStart = Mathf.Clamp(_idx, 0, frames.Length - 1);
        _osEnd = frames.Length - 1;
        _osFpsMul = 1f;
        _dir = 1;
    }

    /// <summary>結束 one-shot，把控制權交還給每幀的 SetState。</summary>
    public void CancelOneShot()
    {
        if (!_osActive) return;
        _osActive = false;
        _osDone = false;
        _timer = 0f;
    }

    /// <summary>
    /// 依怪名載入各動作的幀。fps≤0 用 8、referenceSpeed≤0 用 3。
    /// <paramref name="tileSize"/> 決定顯示大小（PPU=256/tileSize），由 MonsterController 依 idle 可見高度自動換算後傳入
    /// → 與主角同一套：同一張圖在主角/怪物資料夾顯示一樣大。
    /// </summary>
    public void Setup(string monsterName, float fps, float referenceSpeed, float tileSize = 1f,
                      float idleScale = 0f, float walkScale = 0f, float attackScale = 0f, float jumpScale = 0f)
    {
        _sr = GetComponent<SpriteRenderer>();
        BaseFps = fps > 0f ? fps : 8f;
        ReferenceSpeed = referenceSpeed > 0f ? referenceSpeed : 3f;

        var lib = MonsterSpriteLibrary.Instance;
        // 逐動作縮放：**留空＝自動依可見高對齊 idle；有填＝在那個自動結果上「再乘」**（2026-09-22 改，見 Tile()）。
        //   ⚠ 舊版手填是「覆寫自動」⇒ 填 1.1 常常反而變小（自動倍率本來就 > 1）。
        // ⚠ 自動那套量的是可見高度，對「同一視角、只是畫粗一圈」很準，但對**四足獸**會適得其反——
        //   idle 是 3/4 正面站姿、walk 是側面奔跑**壓低身體**，高度矮就被放大：戰狼實測 walk ×1.288、
        //   等效寬 221→285px（idle 才 181），作者實機回報「walk 明顯比 idle 大很多」。
        //   這種「視角本身換了」的素材演算法永遠猜不準，所以開放 MonsterData 的 IdleScale/WalkScale/AttackScale 手填。
        float idleVis = StateVisH(lib, monsterName, "idle");
        float idleTile   = Tile(lib, monsterName, "idle",   tileSize, idleVis, idleScale);
        float walkTile   = Tile(lib, monsterName, "walk",   tileSize, idleVis, walkScale);
        float attackTile = Tile(lib, monsterName, "attack", tileSize, idleVis, attackScale);
        float pantTile   = Tile(lib, monsterName, "pant",   tileSize, idleVis, idleScale);   // pant 是站著喘 → 沿用 idle 的倍率
        // jump 的倍率語義**與其他動作不同**（2026-09-18 加 JumpScale 欄時保留這個設計）：
        //   ‧ 有填 → 照填的走（× tileSize），和其他動作一樣。
        //   ‧ **留空 → 沿用 idle 的 tileSize，不走自動高度對齊**（其他動作留空是走自動）。
        // ⚠ 為什麼 jump 不能用自動：自動那套是「把這個動作的可見高度拉成跟 idle 一樣」，
        //   而跳躍的可見高度**本來就是動作的內容**（蹲下蓄力時矮、騰空伸展時又不同）。
        //   對它做正規化＝把跳躍最重要的那段身體變化整個抵銷掉，而且越蜷縮的幀被放得越大
        //   ⇒ 騰空時怪會忽然膨脹一圈（同 PROBLEMS G12 的機制，但這裡發生在幀與幀之間）。
        //   狂族皇家衛士實測：jump 的可見高在 152~190px 之間跳動、idle 是 199 ⇒ 自動對齊會逐幀放大 1.05~1.31 倍。
        // jump 有填 → **以 idle 的最終大小為基準再乘**（與其他動作同語意：「比平常大幾倍」）；
        // 留空 → 直接沿用 idleTile。兩種情況都不走自動高度對齊（理由見上）。
        // ⚠ 基準是 idleTile 不是 tileSize：IdleScale 有填時，jump 也該跟著那個大小走。
        float jumpTile   = jumpScale > 0.0001f ? Mathf.Clamp(idleTile * jumpScale, 0.1f, 30f) : idleTile;

        // ⚠ 第 4 個參數是 **idle 的 tileSize**：腳底對齊（pivot 補償）要知道「基準動作被放大多少」才算得對，
        //   各動作的 tileSize 不一樣時，只比像素會錯。見 MonsterSpriteLibrary.GetFrames。
        _idle   = lib.GetFrames(monsterName, "idle",   idleTile,   idleTile);
        _walk   = lib.GetFrames(monsterName, "walk",   walkTile,   idleTile);
        _attack = lib.GetFrames(monsterName, "attack", attackTile, idleTile);
        _pant   = lib.GetFrames(monsterName, "pant",   pantTile,   idleTile);
        _jump   = lib.GetFrames(monsterName, "jump",   jumpTile,   idleTile);

        // 【過渡期】角色取樣密度對齊背景（mipMapBias），見 CharacterMipBias 檔頭；背景解析度提上來後可拿掉這三行。
        CharacterMipBias.Register(_idle, transform);
        CharacterMipBias.Register(_walk, transform);
        CharacterMipBias.Register(_attack, transform);
        CharacterMipBias.Register(_pant, transform);
        CharacterMipBias.Register(_jump, transform);

        _shadow.Clear();
        _cageCached = false;   // 換外型後骨牢錨點要重算（見 TryGetCageAnchorLocal）
        _shadow[State.Idle]   = lib.GetShadowAnchor(monsterName, "idle");
        _shadow[State.Walk]   = lib.GetShadowAnchor(monsterName, "walk");
        _shadow[State.Attack] = lib.GetShadowAnchor(monsterName, "attack");
        _shadow[State.Pant]   = lib.GetShadowAnchor(monsterName, "pant");
        _shadow[State.Jump]   = lib.GetShadowAnchor(monsterName, "jump");

        // idle 是必備；萬一只給了 walk 沒給 idle，就用 walk 當待機後備（不至於沒圖）
        if (_idle == null && _walk != null) { _idle = _walk; if (!_shadow[State.Idle].ok) _shadow[State.Idle] = _shadow[State.Walk]; }

        _hasAny = _idle != null || _walk != null || _attack != null || _pant != null || _jump != null;
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

    /// <summary>
    /// 某動作最終的 tileSize ＝ **自動高度對齊的結果 × <paramref name="manualScale"/>**
    /// （留空／0 ＝ 只做自動對齊，行為與 2026-09-17 以來完全相同）。
    ///
    /// <para>⭐ <b>手填是「在正常顯示大小之上再乘」，不是「覆寫」</b>（2026-09-22 改，作者拍板）：
    /// CSV 的 `Scale` 是這隻怪的基準，`IdleScale`／`WalkScale`／`AttackScale` 是**在那之上**的逐動作微調
    /// ⇒ 填 1.1 就該是「這個動作比平常大 10%」，填 1.0 ＝ 跟留空一樣。</para>
    ///
    /// <para>⚠ <b>舊版是 `baseTile × manualScale`，完全跳過自動對齊 ⇒ 填 1.1 常常反而變小。</b>
    /// 因為自動對齊的倍率本來就不是 1：它要把這個動作的可見高拉到與 idle 一致，
    /// 而攻擊／奔跑姿勢通常**比站姿矮**（身體前傾、壓低）⇒ 自動倍率 &gt; 1。
    /// 實測 ZhaYu_HugeSword 的 attack 自動倍率是 **1.252**，作者填 1.1 想放大，結果反而縮小 12%
    /// （作者回報「設 1.1 反而變更小」）。狂族皇家衛士的 jump 自動倍率 1.138、戰狼的 walk 1.336，
    /// **手填的數字只要小於自動倍率就會縮**——這個陷阱沒有任何錯誤訊息。
    /// 見 readme/PROBLEMS.md **G14**。</para>
    ///
    /// <para>⚠ 改語意時**既有的手填值要一起換算**（新值 ＝ 舊值 ÷ 自動倍率），否則那些怪的顯示會跟著變。
    /// `IdleScale` 不必換算（idle 對齊自己，自動倍率恆為 1）；`jump` 見 <see cref="Setup"/> 的說明。</para>
    /// </summary>
    static float Tile(MonsterSpriteLibrary lib, string name, string state, float baseTile, float idleVis, float manualScale)
    {
        float auto = StateTile(lib, name, state, baseTile, idleVis);
        return manualScale > 0.0001f ? Mathf.Clamp(auto * manualScale, 0.1f, 30f) : auto;
    }

    public bool Has(State s) => FramesFor(s) != null;

    /// <summary>這個動作有幾幀（沒圖回 0）。Brain 要把「動作播完」換算成秒時用：<c>張數 ÷ (AnimFPS × 倍率)</c>。</summary>
    public int FrameCount(State s) { var f = FramesFor(s); return f != null ? f.Length : 0; }

    /// <summary>
    /// 設定當前狀態並餵入當前速度（給走路 fps 連動）。沒有對應圖時自動退回 Attack→Walk→Idle。
    /// </summary>
    public void SetState(State s, float currentSpeed)
    {
        _currentSpeed = currentSpeed;
        // one-shot 期間**完全不理會**外部狀態指令：HandleVisuals 每幀都會依距離/位移喊 Walk 或 Attack，
        // 不擋的話跳到一半就被切回走路。要提前中止得明確呼叫 CancelOneShot（＝所有權清楚，不會互搶）。
        if (_osActive) return;
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
        if (frames.Length == 1) { ApplyFrame(); if (_osActive) _osDone = true; return; }   // 靜態姿勢（one-shot 視同播完）

        float fps = BaseFps;
        if (_osActive)
        {
            // one-shot 走固定幀率（BaseFps × 倍率）：跳躍的節奏不該被「當下移動得多快」左右——
            // 那條連動是為了走路不腳滑而存在的，套到跳躍上會讓騰空那幾幀忽快忽慢。
            fps = BaseFps * _osFpsMul;
            if (_osDone) { ApplyFrame(); return; }   // 播完就停在結束幀，等 Brain 收
        }
        else if (_state == State.Walk && ReferenceSpeed > 0.01f)
        {
            float mul = Mathf.Clamp(_currentSpeed / ReferenceSpeed, MinMul, Mathf.Max(1f, MaxMul));
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

        if (_osActive)
        {
            if (_idx >= _osEnd) { _idx = _osEnd; _osDone = true; return; }   // 到結束幀就停住（不回頭、不循環）
            _idx++;
            if (_idx >= _osEnd) { _idx = _osEnd; _osDone = true; }
            return;
        }

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
        // jump 沒圖就退回 walk（不退 idle）——跳躍型 Brain 在「跳」的那段本來就在位移，退成發呆會變成滑過去。
        if (s == State.Jump) return Has(State.Walk) ? State.Walk : State.Idle;
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
            case State.Jump: return _jump;
            default: return _idle;
        }
    }
}
