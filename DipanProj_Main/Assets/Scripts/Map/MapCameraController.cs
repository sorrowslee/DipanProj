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

    /// <summary>
    /// 由 MapManager 在載圖 + 放好玩家後呼叫。依 mode 與地圖大小決定相機行為。
    /// </summary>
    public void Apply(MapData map, int mode, Transform player)
    {
        if (map == null) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) { Debug.LogWarning("[MapCameraController] 找不到 Camera.main，略過相機設定。"); return; }

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
        if (!_following || _target == null || _cam == null) return;

        Vector3 desired = ClampToBounds(_target.position);
        Vector3 cur = _cam.transform.position;
        desired.z = cur.z;

        if (followSmoothTime <= 0f)
            _cam.transform.position = desired;
        else
            _cam.transform.position = Vector3.SmoothDamp(cur, desired, ref _vel, followSmoothTime);
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
