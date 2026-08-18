using UnityEngine;
using Dipan.MapRuntime;

/// <summary>
/// 地圖相機控制（由 <see cref="MapManager"/> 在每次載圖後驅動，仿 TeleportWatcher 由 MapManager AddComponent）。
///
/// 兩種模式（來源：MapsTable.csv 的 MapMode 欄）：
/// 1. 整張地圖（MapMode=1）：相機置中地圖、orthographicSize = 地圖高 / 2，把整張地圖塞進畫面，
///    角色／怪物隨之變小（＝原本 MapLoader.FitCamera 的行為）。
/// 2. 鏡頭跟隨（MapMode=2，預設）：orthographicSize 固定成「標準房間」大小（角色維持正常大小），
///    鏡頭每幀跟著玩家移動，並夾在地圖邊界內（不露出地圖外的黑邊）。
///
/// 重要：MapMode=2 不代表一定跟隨——只有地圖「夠大」(寬或高超過門檻) 才跟隨；不夠大的地圖
/// 即使填 2 也走整張地圖模式。這讓現有的小／適中地圖完全不受預設值 2 影響。
/// 見 readme/MAP_SYSTEM.md。
/// </summary>
public class MapCameraController : MonoBehaviour
{
    [Header("鏡頭跟隨：縮放基準")]
    [Tooltip("跟隨模式下，畫面要顯示的『高度（格數）』。等於標準房間的高度，讓角色維持正常大小。" +
             "orthographicSize = 此值 × tileSize ÷ 2。")]
    public float followViewHeightTiles = 10f;

    [Header("『地圖夠大才跟隨』門檻（寬或高任一超過就跟隨）")]
    [Tooltip("地圖寬度（格）超過此值就算夠大。預設 18 = 現有適中房間的寬度（含 18 寬不跟隨，>18 才跟隨）。")]
    public float maxWholeWidthTiles = 18f;
    [Tooltip("地圖高度（格）超過此值就算夠大。預設 10 = 現有適中房間的高度（含 10 高不跟隨，>10 才跟隨）。")]
    public float maxWholeHeightTiles = 10f;

    [Header("跟隨平滑（0 = 立即跟上）")]
    [Tooltip("鏡頭追上玩家的平滑時間（秒），用 SmoothDamp。0 = 完全不平滑、立即對齊。")]
    public float followSmoothTime = 0.12f;

    // ---- runtime ----
    Camera _cam;
    Transform _target;          // 跟隨目標（玩家）
    bool _following;
    Rect _bounds;               // 當前地圖世界包圍盒
    float _orthoSize;           // 跟隨模式下固定的 orthographicSize
    float _tileSize = 1f;
    Vector3 _vel;               // SmoothDamp 速度暫存

    // ---- 鏡頭區覆蓋（camZone trigger：踩上去拉遠/位移，離開還原）----
    [Header("鏡頭區平滑（拉遠/恢復的過渡秒數）")]
    public float zoneTransitionTime = 0.4f;
    bool _applied;
    float _baseOrtho;           // 當前模式的基準 orthographicSize（zoom=1 時的值）
    Vector2 _baseCenter;        // 整張地圖模式的中心
    bool _zoneActive;
    float _zoneZoomMul = 1f;    // 目標縮放倍率（>1 拉遠、<1 拉近）
    Vector2 _zoneOffset;        // 目標位移（世界單位）
    float _zoomCur = 1f;        // 目前縮放（平滑趨近）
    Vector2 _offsetCur;         // 目前位移（平滑趨近）

    /// <summary>
    /// 由 MapManager 在載圖 + 放好玩家後呼叫。依 mode 與地圖大小決定相機行為。
    /// </summary>
    public void Apply(MapData map, int mode, Transform player)
    {
        if (map == null) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) { Debug.LogWarning("[MapCameraController] 找不到 Camera.main，略過相機設定。"); return; }

        // 場景外（地圖未覆蓋到、露出相機底色處）一律純黑：強制相機 Solid Color 黑底。
        // 之所以放這裡而非只改場景：保證每張地圖、每種相機模式都黑，不依賴場景相機設定。
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = Color.black;

        _target = player;
        _tileSize = map.tileSize;
        _bounds = MapCoords.WorldBounds(map);

        bool bigEnough = map.width > maxWholeWidthTiles || map.height > maxWholeHeightTiles;
        bool follow = (mode == 2) && bigEnough && _target != null;

        if (follow)
        {
            _following = true;
            _orthoSize = followViewHeightTiles * _tileSize * 0.5f;
            if (_cam.orthographic) _cam.orthographicSize = _orthoSize;
            // 立刻對齊（夾在邊界內），避免從上一張地圖位置慢慢滑過來。
            _vel = Vector3.zero;
            Vector3 desired = ClampToBounds(_target.position);
            _cam.transform.position = new Vector3(desired.x, desired.y, _cam.transform.position.z);
            Debug.Log($"[MapCameraController] 鏡頭跟隨模式（地圖 {map.width}×{map.height}，orthoSize={_orthoSize:0.##}）。");
        }
        else
        {
            _following = false;
            FitWholeMap(map);
            if (mode == 2 && !bigEnough)
                Debug.Log($"[MapCameraController] MapMode=2 但地圖（{map.width}×{map.height}）未超過門檻，採整張地圖模式。");
            else
                Debug.Log($"[MapCameraController] 整張地圖模式（地圖 {map.width}×{map.height}）。");
        }

        // 記錄基準（給鏡頭區縮放/位移用），並重置鏡頭區覆蓋（換地圖不殘留）。
        _baseCenter = MapCoords.WorldCenter(map);
        _baseOrtho = _cam.orthographic ? _cam.orthographicSize : (map.height * map.tileSize * 0.5f);
        _zoneActive = false; _zoneZoomMul = 1f; _zoneOffset = Vector2.zero;
        _zoomCur = 1f; _offsetCur = Vector2.zero;
        _applied = true;
    }

    // ---- 鏡頭區覆蓋 API（給 CameraZoneWatcher 呼叫）----

    /// <summary>踩進鏡頭區：套用縮放倍率（>1 拉遠）與位移（世界單位）。平滑過渡。</summary>
    public void SetCameraZone(float zoomMul, Vector2 offset)
    {
        _zoneActive = true;
        _zoneZoomMul = Mathf.Max(0.05f, zoomMul);
        _zoneOffset = offset;
    }

    /// <summary>離開鏡頭區：還原到本地圖的正常縮放/位置。</summary>
    public void ClearCameraZone() => _zoneActive = false;

    /// <summary>
    /// 鏡頭區的縮放/位移是否已趨近目標（=拉伸表演完成）。給 CameraZoneWatcher 判斷
    /// 「鏡頭到位後才觸發 camZone 的 next 觸發鏈」用（見 TriggerChain / readme/TRIGGER_CHAIN.md）。
    /// 指數趨近永遠到不了 100%，取 2% 容差；zoom=1 且無位移的區域（純鏈觸發用）會立即成立。
    /// </summary>
    public bool ZoneSettled
    {
        get
        {
            float targetZoom = _zoneActive ? _zoneZoomMul : 1f;
            Vector2 targetOff = _zoneActive ? _zoneOffset : Vector2.zero;
            return Mathf.Abs(_zoomCur - targetZoom) <= 0.02f * Mathf.Max(0.1f, targetZoom)
                && (_offsetCur - targetOff).sqrMagnitude <= 0.01f;
        }
    }

    /// <summary>整張地圖模式：置中地圖、用高度撐滿畫面（與原 MapLoader.FitCamera 一致）。</summary>
    void FitWholeMap(MapData map)
    {
        Vector2 c = MapCoords.WorldCenter(map);
        _cam.transform.position = new Vector3(c.x, c.y, _cam.transform.position.z);
        if (_cam.orthographic)
            _cam.orthographicSize = map.height * map.tileSize * 0.5f;
    }

    void LateUpdate()
    {
        if (!_applied || _cam == null) return;

        // 1) 平滑趨近鏡頭區的縮放/位移目標（沒踩區域時目標 = 1 / 0，即還原）。
        // 新手教學「對準點」期間，忽略鏡頭區的縮放/位移（否則會被邪佛全貌那種 offset 推歪），純對準傳送門。
        bool focusing = _focus.HasValue;
        float targetZoom = focusing ? 1f : (_zoneActive ? _zoneZoomMul : 1f);
        Vector2 targetOff = focusing ? Vector2.zero : (_zoneActive ? _zoneOffset : Vector2.zero);
        if (zoneTransitionTime <= 0f) { _zoomCur = targetZoom; _offsetCur = targetOff; }
        else
        {
            float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, zoneTransitionTime * 0.5f));
            _zoomCur = Mathf.Lerp(_zoomCur, targetZoom, k);
            _offsetCur = Vector2.Lerp(_offsetCur, targetOff, k);
        }

        // 2) 套用縮放（在基準 orthoSize 上乘倍率）。
        if (_cam.orthographic) _cam.orthographicSize = Mathf.Max(0.1f, _baseOrtho * _zoomCur);

        // 3) 基準位置：有「對準點覆寫」（新手教學拉去看傳送門）就對準它；否則跟隨模式＝玩家、整張圖＝中心。再加鏡頭區位移。
        Vector3 basePos = _focus.HasValue
            ? new Vector3(_focus.Value.x, _focus.Value.y, 0f)   // 對準點不夾邊界，確保目標（傳送門）正好在畫面中央
            : (_following && _target != null)
                ? ClampToBounds(_target.position)
                : new Vector3(_baseCenter.x, _baseCenter.y, 0f);

        Vector3 cur = _cam.transform.position;
        Vector3 desired = new Vector3(basePos.x + _offsetCur.x, basePos.y + _offsetCur.y, cur.z);

        // 對準點期間也要平滑移動過去（即使整張圖模式），所以一律用 SmoothDamp。
        if ((_following || _focus.HasValue) && followSmoothTime > 0f)
            _cam.transform.position = Vector3.SmoothDamp(cur, desired, ref _vel, followSmoothTime);
        else
            _cam.transform.position = desired;

        // 4) 震動：最後才疊上去。
        // ⚠ 順序很重要——必須在 SmoothDamp 之後。若把偏移加進 desired 再 SmoothDamp，
        //   平滑會把高頻抖動整個吃掉，看起來只是鏡頭「軟軟地飄一下」而不是震。
        //   也因為不寫回 _vel / desired，震完自然回到正確位置，不會累積漂移。
        Vector3 shake = ConsumeShakeOffset();
        if (shake != Vector3.zero) _cam.transform.position += shake;
    }

    // ---- 震動（擊中衝擊感）----
    // ⚠ 震動一定要做在這支元件裡，不能另外掛一個「震屏元件」：
    //    相機位置每幀由本元件的 LateUpdate 獨佔寫入，另一個元件也寫 transform.position 會互相蓋掉
    //    （誰先跑取決於 Script Execution Order，是個抓不到的隨機 bug）。
    //    這裡的做法是：算完基準位置後，最後再疊一個衰減中的隨機偏移上去。
    float _shakeTimeLeft;
    float _shakeDuration;
    float _shakeAmplitude;   // 世界單位

    /// <summary>
    /// 讓鏡頭震動一下（擊中、爆炸之類的衝擊感）。振幅線性衰減到 0。
    /// 重複呼叫取「較強的那一次」，不累加——連續呼叫時不會震到飛出去。
    /// </summary>
    /// <param name="duration">秒數（用 unscaled 計時，暫停中也會衰減完）。</param>
    /// <param name="amplitude">最大偏移（世界單位）。0.15 約是一格的 1/7，已經很明顯。</param>
    public void AddShake(float duration, float amplitude)
    {
        duration = Mathf.Max(0f, duration);
        amplitude = Mathf.Max(0f, amplitude);
        if (duration <= 0f || amplitude <= 0f) return;
        // 只有「比目前更強」的震動才取代，較弱的直接忽略。
        // 刻意不做「延長時間但保留舊振幅」那種合併——衰減係數是 timeLeft/duration，
        // 中途把 timeLeft 拉長會讓已經衰減掉的震動整個彈回滿強度（看起來像抖到一半又暴衝一次）。
        if (amplitude < _shakeAmplitude) return;
        _shakeAmplitude = amplitude;
        _shakeDuration = duration;
        _shakeTimeLeft = duration;
    }

    /// <summary>立刻停止震動（演出被打斷時用，避免鏡頭停在偏移位置）。</summary>
    public void StopShake() { _shakeTimeLeft = 0f; _shakeAmplitude = 0f; }

    /// <summary>算出這一幀要疊加的震動偏移，並推進衰減。沒在震時回 zero。</summary>
    Vector3 ConsumeShakeOffset()
    {
        if (_shakeTimeLeft <= 0f || _shakeAmplitude <= 0f) return Vector3.zero;
        // 用 unscaledDeltaTime：演出可能在暫停狀態下播（見 readme/PROBLEMS.md 的過場慣例）。
        _shakeTimeLeft -= Time.unscaledDeltaTime;
        if (_shakeTimeLeft <= 0f) { _shakeTimeLeft = 0f; _shakeAmplitude = 0f; return Vector3.zero; }
        float k = _shakeDuration > 0.0001f ? (_shakeTimeLeft / _shakeDuration) : 0f;   // 線性衰減
        float a = _shakeAmplitude * k;
        return new Vector3(Random.Range(-a, a), Random.Range(-a, a), 0f);
    }

    // ---- 把鏡頭「對準某個世界座標」，設 null 還原成跟隨玩家 ----
    Vector2? _focus;
    /// <summary>對準某個世界座標（拉鏡頭看傳送門/邪佛用）。傳 null 還原。</summary>
    public void SetFocusPoint(Vector2? worldPoint) => _focus = worldPoint;
    /// <summary>鏡頭是否已大致移到對準點（給教學判斷「飄到位了」）。</summary>
    public bool FocusReached(float tol = 0.3f)
        => _focus.HasValue && _cam != null && ((Vector2)_cam.transform.position - (_focus.Value + _offsetCur)).sqrMagnitude <= tol * tol;

    // ---- 鏡頭聚焦一段表演（給「鏡頭聚焦」trigger 由觸發鏈驅動）----
    // 流程：onStart（黑幕＋定住玩家）→ 平滑移到 center → 停留 holdSeconds 秒 → 拉回 → onEnd（收黑幕＋放開＋接 next）。
    Coroutine _focusCo;
    /// <summary>播放一段鏡頭聚焦：移到 center、停留、再拉回。onStart 在開始、onEnd 在結束時呼叫（黑幕/定住/接鏈交給外部）。</summary>
    public void PlayFocus(Vector2 center, float holdSeconds, System.Action onStart, System.Action onEnd)
    {
        if (_focusCo != null) StopCoroutine(_focusCo);
        _focusCo = StartCoroutine(FocusRoutine(center, holdSeconds, onStart, onEnd));
    }

    System.Collections.IEnumerator FocusRoutine(Vector2 center, float holdSeconds, System.Action onStart, System.Action onEnd)
    {
        onStart?.Invoke();
        SetFocusPoint(center);
        // 等鏡頭大致飄到位（最多等 2.5 秒，避免地圖太大追不到卡住）。
        float t = 0f;
        while (!FocusReached(0.3f) && t < 2.5f) { t += Time.unscaledDeltaTime; yield return null; }
        // 停留讓玩家看清楚目標。
        float h = 0f;
        while (h < Mathf.Max(0f, holdSeconds)) { h += Time.unscaledDeltaTime; yield return null; }
        // 放開對準點 → 鏡頭開始拉回玩家；稍等一下再收黑幕，過渡比較順。
        SetFocusPoint(null);
        float back = 0f;
        while (back < 0.45f) { back += Time.unscaledDeltaTime; yield return null; }
        _focusCo = null;
        onEnd?.Invoke();
    }

    /// <summary>把目標點夾進地圖邊界，使視窗不超出地圖（地圖該軸比視窗小時則置中該軸）。</summary>
    Vector3 ClampToBounds(Vector3 target)
    {
        float halfH = _cam.orthographic ? _cam.orthographicSize : _orthoSize;
        float halfW = halfH * _cam.aspect;

        float x, y;
        if (_bounds.width <= halfW * 2f) x = _bounds.center.x;
        else x = Mathf.Clamp(target.x, _bounds.xMin + halfW, _bounds.xMax - halfW);

        if (_bounds.height <= halfH * 2f) y = _bounds.center.y;
        else y = Mathf.Clamp(target.y, _bounds.yMin + halfH, _bounds.yMax - halfH);

        return new Vector3(x, y, target.z);
    }
}
