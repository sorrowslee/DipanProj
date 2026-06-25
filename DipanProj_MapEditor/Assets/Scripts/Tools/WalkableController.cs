using UnityEngine;
using DipanMapEditor.Core;
using DipanMapEditor.UI;

namespace DipanMapEditor.Tools
{
    /// <summary>
    /// 可走/牆/水筆刷：在 Walkable 工具下左鍵拖曳塗子格。
    /// 塗哪一種（可走/牆/水）由 EditorUI.WalkBrushState 決定。
    /// </summary>
    public class WalkableController : MonoBehaviour
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
            if (_ui == null || _ui.CurrentTool != EditTool.Walkable) return;

            if (!Input.GetMouseButton(0)) { _lastCell = new Vector2Int(int.MinValue, int.MinValue); _strokePushed = false; return; }
            if (_ui.IsPointerOverUI(Input.mousePosition)) return;

            var map = session.Map;
            Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int cell = MapCoords.WorldToFineCell(world, map);
            if (!MapCoords.InBoundsFine(cell.x, cell.y, map)) return;
            if (cell == _lastCell) return;
            _lastCell = cell;

            if (!_strokePushed) { UndoManager.Push(); _strokePushed = true; }

            // 依當前筆刷大小塗 N×N 子格方塊（以游標子格為中心）。狀態：可走 '0' / 牆 '1' / 水 '2'。
            int size = _ui.WalkBrushSize;
            int x0 = cell.x - size / 2;
            int y0 = cell.y - size / 2;
            char state = _ui.WalkBrushState;
            for (int dy = 0; dy < size; dy++)
                for (int dx = 0; dx < size; dx++)
                    WalkableOps.SetState(map, x0 + dx, y0 + dy, state);
        }
    }
}
