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

            // 自動生成預覽中一動筆刷 → **先把預覽套用進地圖資料**，再照常塗。
            //
            // ⚠ 這裡的設計換過一次，換的原因是一起資料遺失事故（2026-09-07）：
            //   原本是「筆刷改預覽本身、按套用才寫入」。作者自動生成後直接用筆刷修、然後存檔，
            //   結果**塗的東西全在那張沒套用的遮罩裡**，存進檔案的還是全牆，整張圖白做。
            //   現在改成「一動筆刷就先落地」——所見即所得，而且畫面上看到的一定就是資料裡的，
            //   不存在「看得到但還沒生效」的中間狀態。套用進 Undo，反悔按 Cmd/Ctrl+Z 即可。
            if (_ui.AutoPreview != null)
            {
                UndoManager.Push();
                int n = WalkableOps.ApplyMask(map, _ui.AutoPreview);
                _ui.ClearAutoPreview();
                _ui.SetStatus(n >= 0
                    ? $"已自動套用生成結果（{n} 子格），接著就是一般塗改——反悔按 Cmd/Ctrl+Z"
                    : "預覽與地圖尺寸對不上，已丟棄預覽");
                _strokePushed = true;   // 上面已經 Push 過，這一筆不要再 Push 第二次
            }

            if (!_strokePushed) { UndoManager.Push(); _strokePushed = true; }

            // 依當前筆刷大小塗 N×N 子格方塊（以游標子格為中心）。狀態：可走 '0' / 牆 '1' / 水 '2'。
            for (int dy = 0; dy < size; dy++)
                for (int dx = 0; dx < size; dx++)
                    WalkableOps.SetState(map, x0 + dx, y0 + dy, state);
        }
    }
}
