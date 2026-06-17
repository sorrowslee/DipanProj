using UnityEngine;
using DipanMapEditor.Core;
using DipanMapEditor.Data;
using DipanMapEditor.UI;

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
