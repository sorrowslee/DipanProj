using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家逐格動畫播放器（路線 B：純程式、不用 Unity Animator）。與 <see cref="MonsterAnimator"/> 同模式，
/// 多了「一次性動作（dead）播完定格」的支援，並用「血統(bloodline)」決定外型。
///
/// 狀態：Idle / Walk（循環）、Attack / Dead（一次性，播完停在最後一幀）。有圖才會用。
/// Attack 另外從 <see cref="AttackStartFrame"/>（自動算出的「起手結束幀」）開始播，見 PlayerSpriteLibrary。
/// 防呆：只有「載得到圖」的狀態才存在（<see cref="Has"/>）；要播沒圖的狀態時自動退回
/// Dead/Attack→Idle、Walk→Idle；一張圖都沒有就整個不動。
///
/// 走路 fps 跟實際移動速度連動（防腳滑），作法同 AnimatorSpeedByVelocity 但內建於此。
/// 左右翻面仍由 PlayerController 控 SpriteRenderer.flipX，與本元件無關。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimator : MonoBehaviour, IShadowAnchorSource
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
    int _attackStart;    // 攻擊的起播幀索引（跳過起手；由 PlayerSpriteLibrary 從圖自動算出）
    int _attackEnd = -1;   // 攻擊結束幀（含）＝最大幀＋尾巴；-1＝最後一幀（同上自動算）
    int _autoAttackStart, _autoAttackEnd = -1;              // 自動算出來的那組（覆寫清掉時還原用）
    int _overrideAttackStart = -1, _overrideAttackEnd = -1; // 血統表 AttackFrameRange（0 起算、含）；-1＝不覆寫

    /// <summary>
    /// 手填攻擊播放範圍（2026-11-09，表B <c>AttackFrameRange</c>）：蓋過自動判斷的起播幀／結束幀。
    /// 傳 -1 ＝ 清掉覆寫、還原自動值。由 <c>BloodlineSystem.ApplyTo</c> 每次套用血統時寫入；Setup 換外型後會自動重套。
    /// ⚠ 為什麼要有手填：自動判斷假設「輪廓跟站姿差最多的那格＝出手到底」，**由上往下劈**的動作（鍾馗）
    /// 刀舉最高時輪廓最大、真正劈下去反而比較小，會被截在刀舉到半空（PROBLEMS G20）。
    /// </summary>
    public void SetAttackRangeOverride(int start0, int end0)
    {
        _overrideAttackStart = start0;
        _overrideAttackEnd = end0;
        if (start0 < 0) { _attackStart = _autoAttackStart; _attackEnd = _autoAttackEnd; return; }
        ApplyAttackRangeOverride();
    }

    void ApplyAttackRangeOverride()
    {
        if (_overrideAttackStart < 0 || _attack == null || _attack.Length == 0) return;
        int last = _attack.Length - 1;
        _attackStart = Mathf.Clamp(_overrideAttackStart, 0, last);
        _attackEnd = _overrideAttackEnd < 0 ? last : Mathf.Clamp(Mathf.Max(_overrideAttackEnd, _attackStart), 0, last);
    }

    /// <summary>
    /// 攻擊動畫是否要循環播（<b>舊行為</b>）。預設 false＝一次性：從起播幀播到最後一幀後定格，
    /// 由呼叫端決定何時重播。設 true 會回到 2026-08-22 之前的行為（從第 0 幀無限循環），
    /// 給 A/B 對比用——由 <c>PlayerController.AttackAnimLegacyMode</c> 設定。
    /// </summary>
    [System.NonSerialized] public bool AttackLoops = false;

    /// <summary>
    /// 攻擊動畫的播放倍率（&lt;1 = 慢）。由血統表B 的 <c>AttackAnimSpeed</c> 經 BloodlineSystem 每次套用時寫入（2026-11-09 加）。
    /// 只影響**動畫**，不影響武器的實際攻速（那在武器表）。例：鍾馗是全力由上往下劈的大動作，12fps 看起來太急。
    /// </summary>
    [System.NonSerialized] public float AttackFpsMul = 1f;

    /// <summary>攻擊從第幾幀開始播（0 起算）。循環模式（舊行為）恆為 0。</summary>
    public int AttackStartFrame => AttackLoops ? 0 : _attackStart;

    /// <summary>攻擊播到第幾幀為止（0 起算、含）＝自動算出的「最大幀＋尾巴」；算不出來或循環模式＝最後一幀。後面的幀（AutoSprite 多出來的第二拳等）不播。</summary>
    public int AttackEndFrame => (AttackLoops || _attack == null || _attack.Length == 0 || _attackEnd < 0) ? Mathf.Max(0, (_attack?.Length ?? 1) - 1) : Mathf.Clamp(_attackEnd, 0, _attack.Length - 1);

    /// <summary>攻擊動畫正在播（還沒定格在最後一幀）。呼叫端用它判斷「這一次還沒播完」。</summary>
    public bool IsAttackPlaying => _state == State.Attack && !_oneShotDone;

    /// <summary>
    /// 攻擊播到最後一幀時要不要**從起播幀再來一次**（而不是定格在最後一幀）。
    ///
    /// 由 <c>PlayerController.HandleVisuals</c> **每幀**設定＝「玩家還按著 且 站著不動 且 開了這個選項」。
    /// 因為是每幀重設，放開的那一刻它就變 false，當次循環播完就定格 → `IsAttackPlaying` 轉 false
    /// → 呼叫端把畫面交還給 Idle/Walk。**所以「放開後照樣播完這一次」在兩種模式下都成立。**
    ///
    /// ⚠ 循環的是「起播幀 → 最後一幀」，不是整段繞回第 0 幀——起手（第 0 幀到起播幀之間）是一次性的，
    /// 每輪都重播會變成「施法到一半又把手放下」。
    /// </summary>
    [System.NonSerialized] public bool AttackRepeats = false;

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

    // ── 動作預覽（作弊面板「動作」分頁，2026-11-11）──
    // 測素材用：指定一個動作讓玩家一直輪播，不管玩家在不在走、有沒有開火。
    // 刻意做成 static：玩家物件換圖／重建、換血統（Setup 會把狀態重設成 Idle）都不必重新指定，
    // 下一幀自動重新進入。不存檔、進 Play 會清掉（見 ResetPreviewOnPlay）。
    //
    // 規則：
    //  ‧ 預覽中 SetState（PlayerController 每幀塞的 Idle/Walk/Attack）全部忽略；**真死（Dead）例外**——會關掉預覽、照常死亡。
    //  ‧ 姿勢表演（倒下／趴地／爬起）優先於預覽，演完自動回到預覽。
    //  ‧ idle／walk 照常循環；**dead／attack 本來是一次性的，預覽時播完最後一幀停 PreviewEndHold 秒再從第 0 幀重播**，
    //    不停的話倒地那一幀一閃就跳回站姿、看不清楚。
    //  ‧ attack 預覽播**全部幀**（0 → 最後一幀），不是實戰的「起播幀 → 結束幀」——目的是逐張檢查素材。
    //  ‧ 幾何（GeomState）跟著預覽動作走：預覽 dead 時影子用 dead 的錨點、身上的血統特效會關——跟真的倒地一樣，正好拿來驗。

    /// <summary>目前預覽中的動作；null＝沒在預覽（正常遊戲）。由 CheatPanel 設定。</summary>
    public static State? PreviewState;

    /// <summary>一次性動作（dead／attack）預覽時，播到最後一幀後停多久（秒）再重播。</summary>
    public const float PreviewEndHold = 0.6f;

    bool _inPreview;          // 這個 instance 目前是否已進入預覽（用來偵測「剛開／剛關」）
    float _previewHold;       // 一次性動作停在最後一幀已經多久

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetPreviewOnPlay() => PreviewState = null;   // 關掉 Domain Reload 時 static 不會自己清

    /// <summary>
    /// 玩家自己動了（WASD 移動／真的攻擊出去）→ 結束預覽，**當場**把畫面交還給呼叫端接下來的 SetState／StartAttack。
    /// 由 PlayerController 呼叫（作者 2026-11-11：一操作就自動停，再開作弊面板按鈕也是關的）。
    /// ⚠ 不能只把 PreviewState 設成 null：那樣 Update 的「剛關掉預覽」分支會在同一幀之後把狀態重設成 Idle，
    ///   蓋掉呼叫端剛設的 Walk／剛起手的 Attack（閃一幀 idle、攻擊動作被吃掉）。這裡同時清 _inPreview 就不會走那條。
    /// 沒在預覽時什麼都不做。
    /// </summary>
    public void EndPreview()
    {
        if (!PreviewState.HasValue && !_inPreview) return;
        PreviewState = null;
        _inPreview = false;
        _oneShotDone = false;   // 預覽可能停在 dead／attack 的定格上；交還後讓一般流程從乾淨狀態接手
        _previewHold = 0f;
    }

    /// <summary>某個動作有幾幀（0＝這個血統沒有這個動作的圖）。給作弊面板顯示用。</summary>
    public int FrameCount(State s) { var f = FramesFor(s); return f != null ? f.Length : 0; }

    // ── 可見身體的幾何（Setup 時一次算好，不用每幀量貼圖）──
    // ⚠ 為什麼不直接讀 SpriteRenderer.bounds：那個含不含四周的透明留白，取決於 sprite 的 mesh 型別
    //   （Tight/FullRect），在這條「執行期 Sprite.Create」的管線上並不保證。
    //   這裡改成從 Setup 已知的縮放參數解析算出來，跟 Unity 怎麼算 bounds 無關，永遠精確。
    //   給「要蓋住玩家 / 要對準腳下」的特效用（佛光光環、血統變身的雷擊點…）。
    readonly Dictionary<State, float> _visH = new Dictionary<State, float>();      // 可見高（世界單位）
    readonly Dictionary<State, float> _footRel = new Dictionary<State, float>();   // 可見腳底相對 transform 的 Y 位移（負＝在下方）

    // ── 影子錨點（每個動作一組，Setup 時從 ShadowAnchorTable／自動計算取好）──
    // BlobShadow 每幀問「目前動作」的錨點；換算成世界座標是它的事（要看當前 sprite 的 PPU/pivot、lossyScale、flipX）。
    // 沒有該動作的錨點（例如沒給 dead 圖）→ 退回 idle 的；連 idle 都沒有 → 回 false，BlobShadow 走舊的自動量測。
    readonly Dictionary<State, ShadowAnchorPx> _shadow = new Dictionary<State, ShadowAnchorPx>();

    public bool TryGetShadowAnchor(out ShadowAnchorPx anchor)
    {
        if (_shadow.TryGetValue(GeomState, out anchor) && anchor.ok) return true;
        if (_shadow.TryGetValue(State.Idle, out anchor) && anchor.ok) return true;
        anchor = default;
        return false;
    }

    // ── 骨牢錨點（2026-09-23，與 MonsterAnimator 同一份演算法：BoneCageVisual.ComputeIdleAnchor）──
    // 夢境教學的 bindPlayer 骨牢用。籠子罩的是身體，X 對 idle 軀幹、Y 對 idle 地面線，一次算好——
    // 不每幀問影子：影子錨點逐動作不同，玩家被關時一直在原地 idle↔attack 切換，籠子會跟著左右滑（PROBLEMS G15）。
    bool _cageCached, _cageOk;
    Vector2 _cageLocal;
    float _cageTorsoW;

    /// <summary>idle 軀幹寬（本地單位，未乘 lossyScale）；取不到回 0。</summary>
    public float CageTorsoWidthLocal
    {
        get { if (!_cageCached) TryGetCageAnchorLocal(out _); return _cageOk ? _cageTorsoW : 0f; }
    }

    /// <summary>骨牢中心：相對 transform 的本地位移（**未乘 lossyScale、未翻面**）。換血統（Setup）時重算。</summary>
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

    State GeomState => (_lyingHold || _wakePlaying || _fallPlaying) ? State.Dead : _state;

    /// <summary>
    /// 目前「**幾何上**」算哪個動作。倒下／趴地／爬起期間一律回 <see cref="State.Dead"/>——
    /// 因為 <see cref="VisibleHeight"/> / <see cref="FeetOffsetY"/> / <see cref="BodyCenterOffsetY"/>
    /// 這三個幾何屬性都是查它，所以它回 Dead 就代表「身體幾何已經是趴姿」。
    ///
    /// 給**掛在角色身上的持續型特效**判斷「我現在還對得到位嗎」用。
    /// 趴著的時候「頭」在水平方向的某一端，任何「從身體中心往上偏移」的定位都對不到
    /// （血統變身那段就是踩這個：旱魃的頭光留在原地、人已經倒下）——那不是調參數能修的，
    /// 所以這類特效在 Dead 期間應該直接關掉。見 [BLOODLINE.md](../../readme/BLOODLINE.md) §5b。
    /// </summary>
    public State CurrentGeomState => GeomState;

    /// <summary>
    /// 掛在身上的持續型特效（血統神格特效那三層）現在該不該顯示。
    ///
    /// **白名單**：只有 idle / walk / attack 這三種「站著的動作」才顯示。
    /// 刻意**不寫成「不是 Dead 就顯示」**——之後若加新動作（受傷、施法、被抓住…），
    /// 白名單會讓它預設關閉、不會冒出「特效浮在半空」的意外；確認那個姿勢對得到位再加進來。
    /// </summary>
    /// <summary>
    /// 走路動畫目前播到第幾幀（0-based）；**不是 Walk 狀態時回 -1**。
    /// 給「要跟腳步節奏同步」的效果用（血統的腳步特效）——用「走了多遠」推算會隨移動速度跑掉，
    /// 動畫幀才是腳實際踏地的節奏。
    /// </summary>
    public int WalkFrame => (_state == State.Walk && !IsWakeUpBusy && _walk != null && _walk.Length > 0)
        ? Mathf.Clamp(_idx, 0, _walk.Length - 1) : -1;

    /// <summary>走路動畫的總幀數（一個完整循環）。0 = 這個血統沒有 walk 圖。</summary>
    public int WalkFrameCount => _walk != null ? _walk.Length : 0;

    /// <summary>
    /// 「掛在身上的血統特效」（頭光 BloodlineHalo／光環 Aura／繞行 Orbit／拖尾 Trail／攻擊特效 AttackFx）
    /// 現在該不該顯示。它們**全部讀這一顆**，所以要一起關就只改這裡。
    ///
    /// 兩個條件：
    /// ① 姿勢正常（Idle/Walk/Attack）——躺下/死亡/變身倒地時人都倒了，頭光還浮在原位很怪。
    /// ② 主角沒有被藏起來（<see cref="Dipan.Cutscene.PlayerVisibility"/>）。
    ///    ⚠ 這幾個特效的視覺是**獨立 GameObject**（刻意不做子物件，免得被角色的 localScale/flipX 二次影響），
    ///    所以 PlayerVisibility 的 GetComponentsInChildren 抓不到它們、關不掉 → 主角藏起來了、地上還浮著
    ///    一圈沒有主人的光。劇情 hidePlayer 與卍字進場（LevelEnterManjiController）都會遇到，在這裡一次擋掉。
    ///    （同一家族的前例：AtmosphereController 也是靠 PlayerVisibility.IsHidden 跳過玩家的提燈光圈。）
    /// </summary>
    public bool BodyFxVisible
    {
        get
        {
            if (Dipan.Cutscene.PlayerVisibility.IsHidden) return false;
            var s = GeomState;
            return s == State.Idle || s == State.Walk || s == State.Attack;
        }
    }

    /// <summary>目前姿勢下「可見身體」的高度（世界單位）。取不到回 0。</summary>
    public float VisibleHeight => _visH.TryGetValue(GeomState, out var v) ? v : 0f;

    /// <summary>目前姿勢下「可見腳底」相對 transform 的 Y 位移（負＝在 transform 下方）。</summary>
    public float FeetOffsetY => _footRel.TryGetValue(GeomState, out var v) ? v : 0f;

    /// <summary>目前姿勢下「可見身體中心」相對 transform 的 Y 位移。</summary>
    public float BodyCenterOffsetY => FeetOffsetY + VisibleHeight * 0.5f;

    /// <summary>
    /// 依血統載入各動作的幀。fps≤0 用 12、referenceSpeed≤0 用 5。
    /// <paramref name="targetHeight"/> = 角色站立顯示高度（世界單位，≤0 用 1.95）：依 idle（取不到改 walk）
    /// 的可見像素高度自動換算 idle 的縮放(tileSize)；walk/attack 再各自依「體積尺度」對齊 idle
    /// （<see cref="PlayerSpriteLibrary.GetActionSize"/>），消除 AutoSprite 各動作大小落差；dead 用 idle 的縮放、不正規化（躺姿本來就矮）。
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

        // 顯示縮放（idle 可見高度換算＋walk/attack 體積正規化）抽到 ResolveTiles，變身預熱也用同一支（PROBLEMS E47）。
        ResolveTiles(lib, bloodline, targetHeight, out float tileSize, out float walkTile, out float attackTile);
        if (bodyScale <= 0.01f) bodyScale = 1f;
        _idle = lib.GetFrames(bloodline, "idle", tileSize, bodyScale);
        _walk = lib.GetFrames(bloodline, "walk", walkTile, bodyScale);
        _dead = lib.GetFrames(bloodline, "dead", tileSize, bodyScale);
        _attack = lib.GetFrames(bloodline, "attack", attackTile, bodyScale);

        // 【過渡期】角色取樣密度對齊背景（mipMapBias），見 CharacterMipBias 檔頭；背景解析度提上來後可拿掉這四行。
        CharacterMipBias.Register(_idle, transform);
        CharacterMipBias.Register(_walk, transform);
        CharacterMipBias.Register(_dead, transform);
        CharacterMipBias.Register(_attack, transform);

        // 各動作的可見幾何（給特效對位用）。與 GetFrames 用同一組 tileSize，算出來才對得上。
        _visH.Clear(); _footRel.Clear();
        _shadow.Clear();
        _cageCached = false;   // 換血統外型後骨牢錨點要重算（見 TryGetCageAnchorLocal）
        _shadow[State.Idle]   = lib.GetShadowAnchor(bloodline, "idle");
        _shadow[State.Walk]   = lib.GetShadowAnchor(bloodline, "walk");
        _shadow[State.Dead]   = lib.GetShadowAnchor(bloodline, "dead");
        _shadow[State.Attack] = lib.GetShadowAnchor(bloodline, "attack");
        CacheGeometry(lib, bloodline, "idle", State.Idle, tileSize, bodyScale);
        CacheGeometry(lib, bloodline, "walk", State.Walk, walkTile, bodyScale);
        CacheGeometry(lib, bloodline, "dead", State.Dead, tileSize, bodyScale);
        CacheGeometry(lib, bloodline, "attack", State.Attack, attackTile, bodyScale);

        // 攻擊起播幀：跳過「跟站著沒兩樣」的起手。整條規則與門檻見 PlayerSpriteLibrary.ActionStartPeakRatio。
        _attackStart = (_attack != null && _attack.Length > 0)
                     ? Mathf.Clamp(lib.GetActionStartFrame(bloodline, "attack"), 0, _attack.Length - 1)
                     : 0;
        // 攻擊結束幀：播到「動作最大幀＋尾巴」就當播完（PlayerSpriteLibrary.ActionEndPeakRatio），-1＝播到最後一幀。
        _attackEnd = (_attack != null && _attack.Length > 0) ? lib.GetActionEndFrame(bloodline, "attack") : -1;
        _autoAttackStart = _attackStart; _autoAttackEnd = _attackEnd;
        ApplyAttackRangeOverride();   // 血統表手填的播放範圍（AttackFrameRange）優先

        if (_idle == null && _walk != null) { _idle = _walk; if (!_shadow[State.Idle].ok) _shadow[State.Idle] = _shadow[State.Walk]; }   // 沒給 idle 就用 walk 當待機後備

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
        float pivotY = PlayerSpriteLibrary.FootPivotY(fy, bodyScale);   // sprite 的 pivot 是用第一幀算的（GetFrames 同一套），這裡不能變

        // Dead（倒下／趴著／爬起期間的幾何）改量**最後一幀＝趴姿**（2026-11-09）：
        // dead 的第一幀還站著，用它算的「身體中心」是站姿的半身高——人已經趴在地上，
        // 喝藥變身的煙塵／電弧（BodyCenterWorldPos）就會飄在趴著的身體上方（作者回報「爆炸比玩家高一些」）。
        if (key == State.Dead && lib.TryGetVisibleBoxLastFrame(bloodline, state, out var lsize, out var loffset, out var lcanvas))
        {
            size = lsize;
            fy = PlayerSpriteLibrary.VisibleBottomFraction(lsize, loffset, lcanvas);
        }
        _visH[key] = size.y * stateTile;
        _footRel[key] = (fy - pivotY) * stateTile;
    }

    // 逐動作尺度正規化助手：讓某動作的顯示縮放(tileSize)使其「體積尺度」= idle 的體積尺度。
    // 尺度算不出來（沒圖／全透明）就用 idle 的縮放，行為同改動前的退路。
    /// <summary>
    /// 各動作的顯示縮放（tileSize）。<see cref="Setup"/> 與變身預熱（<see cref="PlayerSpriteLibrary.PrewarmRoutine"/>）共用，
    /// 兩邊算出來的值一定相同 ⇒ 預熱時建好的 Sprite 會被 Setup 的快取命中（PROBLEMS E47）。
    /// </summary>
    public static void ResolveTiles(PlayerSpriteLibrary lib, string bloodline, float targetHeight,
                                    out float tileSize, out float walkTile, out float attackTile)
    {
        // 自動換算顯示縮放：用 idle（取不到改 walk）的「可見高度」把幀放大到 targetHeight 世界高。
        // box.size.y 是世界單位 @ tileSize 1（= 可見像素 / 256）；tileSize = targetHeight / box.size.y。
        if (targetHeight <= 0f) targetHeight = 1.95f;
        tileSize = 1f;
        if (lib.TryGetVisibleBox(bloodline, "idle", out var vbox, out _) && vbox.y > 0.0001f)
            tileSize = targetHeight / vbox.y;
        else if (lib.TryGetVisibleBox(bloodline, "walk", out var wbox, out _) && wbox.y > 0.0001f)
            tileSize = targetHeight / wbox.y;
        tileSize = Mathf.Clamp(tileSize, 0.1f, 30f);

        // 逐動作尺度正規化：walk/attack 對齊 idle 的「體積尺度」（掃全幀取中位數的 √(高×√面積)，見
        // PlayerSpriteLibrary.GetActionSize），消除 AutoSprite 各動作大小落差；dead 維持躺姿用 idle 縮放、不正規化。
        // ⚠ 以前只對齊「第一幀的可見高度」——Base 的 attack 高度只多 4%、整個人卻粗了 14%，玩家一出手角色就長大
        //   （PROBLEMS G7）。
        float idleSize = StateSize(lib, bloodline, "idle");
        walkTile = StateTile(lib, bloodline, "walk", tileSize, idleSize);
        attackTile = StateTile(lib, bloodline, "attack", tileSize, idleSize);
    }

    static float StateSize(PlayerSpriteLibrary lib, string b, string state)
        => lib.GetActionSize(b, state).Scale;
    static float StateTile(PlayerSpriteLibrary lib, string b, string state, float baseTile, float idleSize)
    {
        float v = StateSize(lib, b, state);
        return (idleSize > 0.0001f && v > 0.0001f) ? Mathf.Clamp(baseTile * (idleSize / v), 0.1f, 30f) : baseTile;
    }

    public bool Has(State s) => FramesFor(s) != null;

    /// <summary>設定當前狀態並餵入當前速度（走路 fps 連動）。沒有對應圖時自動退回。</summary>
    public void SetState(State s, float currentSpeed)
    {
        _currentSpeed = currentSpeed;
        // 動作預覽中：忽略一般狀態切換；真死（Dead）關掉預覽、照常往下走。
        if (PreviewState.HasValue)
        {
            if (s != State.Dead) return;
            PreviewState = null;
            _inPreview = false;
        }
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
            _idx = EntryIndex(s); _timer = 0f; _oneShotDone = false;
            ApplyFrame();
        }
    }

    /// <summary>進入某狀態時從第幾幀開始。只有 Attack 不是 0（跳過起手），其餘都從頭。</summary>
    int EntryIndex(State s)
    {
        if (s != State.Attack || _attack == null || _attack.Length == 0) return 0;
        return Mathf.Clamp(AttackStartFrame, 0, _attack.Length - 1);
    }

    /// <summary>
    /// 重新播一次攻擊動作（從起播幀開始）。**與 <see cref="SetState"/> 的差別**：SetState 在
    /// 「已經是 Attack」時什麼都不做（讓動畫繼續往下播），這支則是無條件從頭來——
    /// 所以「按住不放」不會重播（呼叫端不叫它），「放開再按」才會。
    ///
    /// 姿勢表演（倒下／趴地／爬起）進行中不受理，回 false；沒有 attack 圖也回 false。
    /// </summary>
    public bool StartAttack()
    {
        if (IsWakeUpBusy) return false;
        if (PreviewState.HasValue) return false;   // 動作預覽中：畫面由預覽接管
        if (_attack == null || _attack.Length == 0 || _sr == null) return false;
        _state = State.Attack;
        _idx = EntryIndex(State.Attack);
        _timer = 0f;
        _oneShotDone = false;
        ApplyFrame();
        return true;
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
    /// 「姿勢表演」（倒下／趴地／爬起）改用不受 <c>Time.timeScale</c> 影響的時間推進。
    ///
    /// 給**暫停播放**的演出用：血統變身全程 <c>timeScale=0</c>，不切成 unscaled 的話
    /// 角色會倒到一半凍住、整段演出停擺（見 <c>Dipan.Gacha.BloodlineTransformFxRunner</c>）。
    ///
    /// ⚠ 只影響姿勢表演這三段。一般走路/待機/攻擊動畫**仍然吃 <c>Time.deltaTime</c>**——
    /// 那些在遊戲暫停時本來就該停（背包開著角色還在原地踏步很怪）。
    /// 由演出的持有者負責開關（開頭設 true、收尾 finally 設回 false）。
    /// </summary>
    [System.NonSerialized] public bool UnscaledPose;

    /// <summary>姿勢表演本幀要推進多少秒（依 <see cref="UnscaledPose"/> 二選一）。</summary>
    float PoseDt => UnscaledPose ? Time.unscaledDeltaTime : Time.deltaTime;

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
        // 爬起＝倒播 dead。三者走 PoseDt——預設等同 Time.deltaTime，但演出方把 UnscaledPose 設 true
        // 之後就改吃 unscaled，讓「遊戲暫停播的演出」（血統變身）不會凍住。
        if (_fallPlaying)
        {
            if (_dead == null || _dead.Length == 0) { FinishFallDown(); return; }   // 該血統沒有倒地素材
            float fallDur = PoseFrameDuration;
            if (fallDur <= 0f) { FinishFallDown(); return; }
            _timer += PoseDt;
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
            _timer += PoseDt;
            while (_timer >= dur)
            {
                _timer -= dur;
                if (_wakeIdx > 0) _wakeIdx--;
                else { CancelWakeUp(invokeDone: true); return; }   // 倒播到第 0 幀 → 起身完成，回 Idle
            }
            _sr.sprite = _dead[Mathf.Clamp(_wakeIdx, 0, _dead.Length - 1)];
            return;
        }

        if (PreviewState.HasValue) { UpdatePreview(PreviewState.Value); return; }
        if (_inPreview)
        {
            // 剛關掉預覽：回 Idle，下一幀起交還 PlayerController.HandleVisuals。
            _inPreview = false;
            _state = State.Idle; _idx = 0; _timer = 0f; _oneShotDone = false;
            ApplyFrame();
        }

        var frames = FramesFor(_state);
        if (frames == null || frames.Length == 0) return;
        if (frames.Length == 1) { ApplyFrame(); return; }

        bool loop = IsLooping(_state);
        if (!loop && _oneShotDone) return;   // 一次性已定格在最後一幀

        float fps = BaseFps;
        if (_state == State.Walk && ReferenceSpeed > 0.01f)
            fps = BaseFps * Mathf.Clamp(_currentSpeed / ReferenceSpeed, MinMul, 1f);
        else if (_state == State.Attack)
            fps = BaseFps * Mathf.Max(0.05f, AttackFpsMul);
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
                int last = (_state == State.Attack) ? AttackEndFrame : frames.Length - 1;   // Attack 只播到結束幀（最大幀＋尾巴）
                if (_idx < last) _idx++;
                else if (_state == State.Attack && AttackRepeats) _idx = EntryIndex(State.Attack);   // 按住 → 從起播幀再來一次
                else { _oneShotDone = true; break; }   // 停在結束幀
            }
        }
        ApplyFrame();
    }

    /// <summary>動作預覽的每幀推進（規則見 <see cref="PreviewState"/> 上方註解）。</summary>
    void UpdatePreview(State s)
    {
        var frames = FramesFor(s);
        if (frames == null || frames.Length == 0) return;   // 這個血統沒有這個動作：維持原畫面（面板會提示）

        // 剛開預覽、換了預覽動作、或 Setup（換血統）把狀態重設過 → 從第 0 幀重新開始
        if (!_inPreview || _state != s)
        {
            _inPreview = true;
            _state = s; _idx = 0; _timer = 0f; _previewHold = 0f; _oneShotDone = false;
            ApplyFrame();
            return;
        }
        if (frames.Length == 1) { ApplyFrame(); return; }

        // 一次性動作停在最後一幀中：停滿 PreviewEndHold 再從頭
        if (_oneShotDone)
        {
            _previewHold += Time.deltaTime;
            if (_previewHold < PreviewEndHold) return;
            _oneShotDone = false; _previewHold = 0f; _idx = 0; _timer = 0f;
            ApplyFrame();
            return;
        }

        // 速度：walk 用滿速（預覽時多半站著不動，照實戰會被速度連動壓到 MinMul）；attack 照實戰倍率
        float fps = BaseFps;
        if (s == State.Attack) fps = BaseFps * Mathf.Max(0.05f, AttackFpsMul);
        if (fps <= 0.01f) return;

        bool loop = s == State.Idle || s == State.Walk;
        float frameDur = 1f / fps;
        _timer += Time.deltaTime;
        while (_timer >= frameDur)
        {
            _timer -= frameDur;
            if (loop) _idx = (_idx + 1) % frames.Length;
            else if (_idx < frames.Length - 1) _idx++;
            else { _oneShotDone = true; _previewHold = 0f; _timer = 0f; break; }
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

    // Idle/Walk 循環播；Dead 與 Attack 一次性（播完定格在最後一幀）。
    // ⚠ Attack 曾經是循環的，那是錯的——實測七組素材的「最後一幀 → 第 1 幀」接縫差異達 23~52%，
    // 而相鄰幀平均只有 3~20%（只有 Base 是無縫的），也就是說這些攻擊動畫天生有起有收、不是循環動畫，
    // 硬循環會每播完一輪跳接一次。idle 則全部都是無縫的（接縫 0.4~3%），循環正確。
    // AttackLoops = true 只保留給 A/B 對比舊行為。
    bool IsLooping(State s) => s == State.Idle || s == State.Walk || (s == State.Attack && AttackLoops);

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
