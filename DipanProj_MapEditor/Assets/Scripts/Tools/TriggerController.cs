using UnityEngine;
using DipanMapEditor.Core;
using DipanMapEditor.Data;
using DipanMapEditor.UI;
using DipanMapEditor.Preview;

namespace DipanMapEditor.Tools
{
    /// <summary>
    /// Trigger 塗刷：在當前選取的區域上左鍵拖曳加格/減格。
    /// 當前區域與加/減模式由 EditorUI 提供。
    /// </summary>
    public class TriggerController : MonoBehaviour
    {
        Camera _cam;
        EditorUI _ui;
        Vector2Int _lastCell = new Vector2Int(int.MinValue, int.MinValue);
        bool _strokePushed;

        // 傳送點對位模式：正在被拖曳的傳送點（null = 沒在拖）。
        TeleportMarkerPreview _tpPrev;
        TriggerRegion _dragMarker;      // 拖曳本體（移動錨點）
        TriggerRegion _sizeMarker;      // 拖曳右下角把手（改踩踏矩形大小）

        void Start()
        {
            _cam = Camera.main;
            _ui = FindObjectOfType<EditorUI>();
        }

        void Update()
        {
            var session = MapSession.Instance;
            if (session == null || session.Map == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_ui == null) _ui = FindObjectOfType<EditorUI>();
            if (_ui == null || _ui.CurrentTool != EditTool.Trigger) return;

            var map = session.Map;

            // 傳送點「外型位置」點放：面板按下「設定外型位置」後，下一次點畫布就把該傳送點的 markerX/markerY 設成點擊處。
            if (_ui.MarkerPlaceActive)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { _ui.EndMarkerPlace(); return; }
                if (Input.GetMouseButtonDown(0) && !_ui.IsPointerOverUI(Input.mousePosition))
                {
                    var reg = _ui.CurrentRegion;
                    if (reg != null && reg.typeId == "teleport" && reg.Params != null)
                    {
                        Vector3 w = _cam.ScreenToWorldPoint(Input.mousePosition);
                        UndoManager.Push();
                        reg.Params["markerX"] = w.x;
                        reg.Params["markerY"] = w.y;
                    }
                    _ui.EndMarkerPlace();
                }
                return;   // 放置模式中吃掉輸入，不做塗刷/選取
            }

            // 傳送點對位模式（頂部列「傳送點對位」鈕）：直接拖曳畫在畫布上的傳送點外型。
            // 命中就吃掉這次輸入，不塗刷 —— 所見即所得，免去「猜座標→進遊戲看→回來改」的循環。
            if (HandleMarkerDrag()) return;

            // ESC：進入檢視模式（停止筆刷，改成點區域檢查）
            if (Input.GetKeyDown(KeyCode.Escape)) { _ui.EnterTriggerInspect(); return; }

            // 檢視模式：左鍵點畫布 → 選取該格所屬的 trigger 區域（不塗）
            if (!_ui.TriggerPaintMode)
            {
                if (Input.GetMouseButtonDown(0) && !_ui.IsPointerOverUI(Input.mousePosition))
                {
                    Vector3 w = _cam.ScreenToWorldPoint(Input.mousePosition);
                    Vector2Int c = MapCoords.WorldToCell(w, map.tileSize, MapCoords.Origin(map));
                    _ui.SelectRegion(FindRegionAt(map, c.x, c.y));
                }
                return;
            }

            // 筆刷模式
            // 一筆的開始（按下左鍵）：拍快照；若「每筆一個新區域」就先建一個新區域
            if (Input.GetMouseButtonDown(0) && !_ui.IsPointerOverUI(Input.mousePosition))
            {
                UndoManager.Push();
                _strokePushed = true;
                _lastCell = new Vector2Int(int.MinValue, int.MinValue);
                if (_ui.TriggerNewRegionPerStroke && _ui.TriggerAddCells) _ui.BeginNewRegion();
            }

            if (!Input.GetMouseButton(0)) { _lastCell = new Vector2Int(int.MinValue, int.MinValue); _strokePushed = false; return; }
            if (_ui.IsPointerOverUI(Input.mousePosition)) return;

            Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int cell = MapCoords.WorldToCell(world, map.tileSize, MapCoords.Origin(map));
            if (!MapCoords.InBounds(cell.x, cell.y, map)) return;
            if (cell == _lastCell) return;
            _lastCell = cell;

            if (!_strokePushed) { UndoManager.Push(); _strokePushed = true; }

            // 加格時若還沒有當前區域，依選取的類型自動建一個
            var region = _ui.CurrentRegion;
            if (region == null)
            {
                if (!_ui.TriggerAddCells) return;     // 減格但沒區域 → 無事可做
                region = _ui.EnsureCurrentRegion();
                if (region == null) return;
            }

            if (_ui.TriggerAddCells) TriggerOps.AddCell(region, cell.x, cell.y);
            else TriggerOps.RemoveCell(region, cell.x, cell.y);
        }

        /// <summary>
        /// 傳送點對位模式下的拖曳：按住外型拖到門正中央即可。回傳 true＝這一幀的輸入已被吃掉。
        /// Undo 只在按下那一幀 Push 一次（整段拖曳算一步）。
        /// </summary>
        bool HandleMarkerDrag()
        {
            if (_tpPrev == null) _tpPrev = FindObjectOfType<TeleportMarkerPreview>();
            if (_tpPrev == null || !_tpPrev.Enabled) { _dragMarker = null; _sizeMarker = null; return false; }

            var map = MapSession.Instance?.Map;

            // 改大小（拖右下角把手）：以錨點為中心，寬高 = |滑鼠−錨點| × 2。
            if (_sizeMarker != null)
            {
                if (Input.GetMouseButton(0))
                {
                    Vector3 w = _cam.ScreenToWorldPoint(Input.mousePosition);
                    if (TeleportMarkerPreview.TryMarkerPos(_sizeMarker, map, out Vector2 c))
                        TeleportMarkerPreview.SetSize(_sizeMarker,
                            Mathf.Abs(w.x - c.x) * 2f, Mathf.Abs(w.y - c.y) * 2f);
                    return true;
                }
                _sizeMarker = null;
                return true;
            }

            // 移動（拖本體）
            if (_dragMarker != null)
            {
                if (Input.GetMouseButton(0))
                {
                    Vector3 w = _cam.ScreenToWorldPoint(Input.mousePosition);
                    TeleportMarkerPreview.SetMarker(_dragMarker, w);
                    return true;
                }
                _dragMarker = null;
                return true;   // 放開的那一幀也吃掉，免得同一次點擊又被當成塗刷
            }

            if (Input.GetMouseButtonDown(0) && !_ui.IsPointerOverUI(Input.mousePosition))
            {
                Vector3 w = _cam.ScreenToWorldPoint(Input.mousePosition);

                // ⚠ 把手要比本體先判，否則角落永遠被本體吃掉、大小改不了。
                if (_tpPrev.TryPickHandle(w, out var handleHit))
                {
                    UndoManager.Push();
                    _sizeMarker = handleHit;
                    _ui.SelectRegion(handleHit);
                    return true;
                }

                if (_tpPrev.TryPick(w, out var hit))
                {
                    UndoManager.Push();
                    _dragMarker = hit;
                    _ui.SelectRegion(hit);   // 順便選起來，右側面板才看得到是哪一顆
                    TeleportMarkerPreview.SetMarker(hit, w);
                    return true;
                }
            }
            return false;
        }

        /// <summary>找出覆蓋 (x,y) 的 trigger 區域；重疊時回傳畫在最上層的（清單中最後一個）。</summary>
        static TriggerRegion FindRegionAt(MapData map, int x, int y)
        {
            var regions = map.TriggerLayer?.regions;
            if (regions == null) return null;
            TriggerRegion found = null;
            foreach (var r in regions)
                if (TriggerOps.HasCell(r, x, y)) found = r;
            return found;
        }
    }
}
