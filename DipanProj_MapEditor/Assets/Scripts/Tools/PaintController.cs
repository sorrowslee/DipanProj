using UnityEngine;
using DipanMapEditor.Core;
using DipanMapEditor.Data;
using DipanMapEditor.UI;

namespace DipanMapEditor.Tools
{
    /// <summary>
    /// 筆刷輸入：左鍵在畫布上畫 / 擦 tile，拖曳連續作畫。
    /// 讀 EditorUI 的當前工具與選取 tile；避開 UI 面板區域。
    /// </summary>
    public class PaintController : MonoBehaviour
    {
        Camera _cam;
        EditorUI _ui;
        TilemapView _view;
        Vector2Int _lastCell = new Vector2Int(int.MinValue, int.MinValue);
        bool _strokePushed;

        void Start()
        {
            _cam = Camera.main;
            _ui = FindObjectOfType<EditorUI>();
            _view = FindObjectOfType<TilemapView>();
        }

        void Update()
        {
            var session = MapSession.Instance;
            if (session == null || session.Map == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_ui == null) _ui = FindObjectOfType<EditorUI>();
            if (_view == null) _view = FindObjectOfType<TilemapView>();

            // ESC：在地磚「畫」工具下退出筆刷（清掉選取的地磚塊，不再有預覽/誤畫）
            if (_ui != null && _ui.CurrentTool == EditTool.TilePaint && Input.GetKeyDown(KeyCode.Escape))
                _ui.ClearTileBrush();

            if (!Input.GetMouseButton(0)) { _lastCell = new Vector2Int(int.MinValue, int.MinValue); _strokePushed = false; return; }
            if (_ui != null && _ui.IsPointerOverUI(Input.mousePosition)) return;

            var tool = _ui != null ? _ui.CurrentTool : EditTool.TilePaint;
            if (tool != EditTool.TilePaint && tool != EditTool.Erase) return;

            var map = session.Map;
            Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int cell = MapCoords.WorldToCell(world, map.tileSize, MapCoords.Origin(map));
            if (!MapCoords.InBounds(cell.x, cell.y, map)) return;
            if (cell == _lastCell) return;     // 同一格不重複處理
            _lastCell = cell;

            switch (tool)
            {
                case EditTool.TilePaint:
                    if (_ui != null && _ui.HasTileBrush)
                    {
                        PushOnce();
                        int bw = _ui.TileBrushW, bh = _ui.TileBrushH;
                        for (int dy = 0; dy < bh; dy++)
                            for (int dx = 0; dx < bw; dx++)
                            {
                                int tx = cell.x + dx, ty = cell.y + dy;
                                if (!MapCoords.InBounds(tx, ty, map)) continue;
                                string tid = _ui.TileBrushAt(dx, dy);
                                SetTile(map, tx, ty, tid);
                                _view?.SetCellVisual(tx, ty, tid, map);
                            }
                    }
                    break;

                case EditTool.Erase:
                    PushOnce();
                    EraseTile(map, cell.x, cell.y);
                    _view?.ClearCellVisual(cell.x, cell.y, map);
                    break;
            }
        }

        void PushOnce()
        {
            if (_strokePushed) return;
            UndoManager.Push();
            _strokePushed = true;
        }

        // ---- 資料寫入（遊戲層 tiles） ----

        static void SetTile(MapData map, int x, int y, string tileId)
        {
            var tiles = map.GameLayer.tiles;
            var existing = tiles.Find(t => t.x == x && t.y == y);
            if (existing != null) existing.tileId = tileId;
            else tiles.Add(new TilePlacement { x = x, y = y, tileId = tileId });
        }

        static void EraseTile(MapData map, int x, int y)
        {
            map.GameLayer.tiles.RemoveAll(t => t.x == x && t.y == y);
        }
    }
}
