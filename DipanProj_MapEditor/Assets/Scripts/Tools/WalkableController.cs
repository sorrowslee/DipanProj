using UnityEngine;
using DipanMapEditor.Core;
using DipanMapEditor.UI;

namespace DipanMapEditor.Tools
{
    /// <summary>
    /// 可走/不可走筆刷：在 Walkable 工具下左鍵拖曳塗格。
    /// 塗「可走」或「不可走」由 EditorUI.WalkPaintWalkable 決定。
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
            Vector2Int cell = MapCoords.WorldToCell(world, map.tileSize, MapCoords.Origin(map));
            if (!MapCoords.InBounds(cell.x, cell.y, map)) return;
            if (cell == _lastCell) return;
            _lastCell = cell;

            if (!_strokePushed) { UndoManager.Push(); _strokePushed = true; }

            // 筆刷塗「可走」→ blocked=false；塗「不可走」→ blocked=true
            WalkableOps.SetBlocked(map, cell.x, cell.y, !_ui.WalkPaintWalkable);
        }
    }
}
