using UnityEngine;
using DipanMapEditor.Core;
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

            if (!Input.GetMouseButton(0)) { _lastCell = new Vector2Int(int.MinValue, int.MinValue); _strokePushed = false; return; }
            if (_ui.IsPointerOverUI(Input.mousePosition)) return;

            var map = session.Map;
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
    }
}
