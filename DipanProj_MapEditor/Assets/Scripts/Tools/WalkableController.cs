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

            // 「選種子」模式（自動生成用）：左鍵點一下＝指定種子點，**不塗格子**。
            // 借用可走工具的輸入而不是另開一個 EditTool，是因為它只是這個工具裡的一個瞬間狀態，
            // 點完就自動關掉（SetAutoSeed 內），不需要作者再切回來。
            if (_ui.AutoSeedPickMode)
            {
                if (Input.GetMouseButtonDown(0) && !_ui.IsPointerOverUI(Input.mousePosition))
                {
                    Vector3 w0 = _cam.ScreenToWorldPoint(Input.mousePosition);
                    Vector2Int c0 = MapCoords.WorldToFineCell(w0, session.Map);
                    if (MapCoords.InBoundsFine(c0.x, c0.y, session.Map)) _ui.SetAutoSeed(c0);
                }
                return;
            }

            if (!Input.GetMouseButton(0)) { _lastCell = new Vector2Int(int.MinValue, int.MinValue); _strokePushed = false; return; }
            if (_ui.IsPointerOverUI(Input.mousePosition)) return;

            var map = session.Map;
            Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int cell = MapCoords.WorldToFineCell(world, map);
            if (!MapCoords.InBoundsFine(cell.x, cell.y, map)) return;
            if (cell == _lastCell) return;
            _lastCell = cell;

            int size = _ui.WalkBrushSize;
            int x0 = cell.x - size / 2;
            int y0 = cell.y - size / 2;
            char state = _ui.WalkBrushState;

            // 自動生成預覽中：筆刷改的是**預覽**，不是地圖資料（所見即所得，改完一起套用）。
            // 自動生成一定有猜錯的地方（例如可破壞的桌子擋在路中間、破壞後才該能走，影像上分不出來），
            // 讓預覽階段就能修，就不必「套用→再修→再看」跑兩趟。
            if (_ui.AutoPreview != null)
            {
                // 預覽遮罩只有可走/牆兩態，表達不了水；要塗水請先套用。
                if (state == WalkableOps.Water)
                {
                    _ui.SetStatus("預覽中不能塗水／坑——請先按「③ 套用」，再用藍色筆刷塗");
                    return;
                }
                bool walkable = state == WalkableOps.Walk;
                for (int dy = 0; dy < size; dy++)
                    for (int dx = 0; dx < size; dx++)
                        _ui.SetAutoPreviewCell(x0 + dx, y0 + dy, walkable);
                return;   // 不動地圖資料，所以也不進 Undo（預覽本來就是暫態）
            }

            if (!_strokePushed) { UndoManager.Push(); _strokePushed = true; }

            // 依當前筆刷大小塗 N×N 子格方塊（以游標子格為中心）。狀態：可走 '0' / 牆 '1' / 水 '2'。
            for (int dy = 0; dy < size; dy++)
                for (int dx = 0; dx < size; dx++)
                    WalkableOps.SetState(map, x0 + dx, y0 + dy, state);
        }
    }
}
