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
        float targetZoom = _zoneActive ? _zoneZoomMul : 1f;
        Vector2 targetOff = _zoneActive ? _zoneOffset : Vector2.zero;
        if (zoneTransitionTime <= 0f) { _zoomCur = targetZoom; _offsetCur = targetOff; }
        else
        {
            float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, zoneTransitionTime * 0.5f));
            _zoomCur = Mathf.Lerp(_zoomCur, targetZoom, k);
            _offsetCur = Vector2.Lerp(_offsetCur, targetOff, k);
        }

        // 2) 套用縮放（在基準 orthoSize 上乘倍率）。
        if (_cam.orthographic) _cam.orthographicSize = Mathf.Max(0.1f, _baseOrtho * _zoomCur);

        // 3) 基準位置：跟隨模式＝夾在邊界內的玩家位置；整張地圖模式＝地圖中心。再加上鏡頭區位移。
        Vector3 basePos = (_following && _target != null)
            ? ClampToBounds(_target.position)
            : new Vector3(_baseCenter.x, _baseCenter.y, 0f);

        Vector3 cur = _cam.transform.position;
        Vector3 desired = new Vector3(basePos.x + _offsetCur.x, basePos.y + _offsetCur.y, cur.z);

        if (_following && followSmoothTime > 0f)
            _cam.transform.position = Vector3.SmoothDamp(cur, desired, ref _vel, followSmoothTime);
        else
            _cam.transform.position = desired;
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
