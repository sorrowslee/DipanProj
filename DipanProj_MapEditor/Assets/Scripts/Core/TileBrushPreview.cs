using UnityEngine;
using DipanMapEditor.Data;
using DipanMapEditor.Tools;
using DipanMapEditor.UI;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 所有筆刷工具的「落點預覽」：在滑鼠所在格畫出會塗到的範圍框（含格線）。
    /// 地磚 = 多格 block（依選取大小）；擦/可走/Trigger = 單格；物件用自己的幻影預覽。
    /// 顏色依工具區分（可走綠/不可走紅、擦紅、其餘青）。掛在相機上。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class TileBrushPreview : MonoBehaviour
    {
        Material _mat;
        Camera _cam;
        EditorUI _ui;

        void EnsureMaterial()
        {
            if (_mat != null) return;
            _mat = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
            _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _mat.SetInt("_ZWrite", 0);
        }

        void OnPostRender()
        {
            var session = MapSession.Instance;
            if (session == null || session.Map == null) return;
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_ui == null) _ui = FindObjectOfType<EditorUI>();
            if (_ui == null) return;
            if (_ui.IsPointerOverUI(Input.mousePosition)) return;

            var map = session.Map;
            Vector3 wp = _cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int cell = MapCoords.WorldToCell(wp, map.tileSize, MapCoords.Origin(map));

            int bw = 1, bh = 1;
            Color line, fill;

            switch (_ui.CurrentTool)
            {
                case EditTool.TilePaint:
                    if (!_ui.HasTileBrush) return;
                    bw = _ui.TileBrushW; bh = _ui.TileBrushH;
                    line = new Color(0.2f, 0.9f, 1f, 0.9f); fill = new Color(0.2f, 0.9f, 1f, 0.15f);
                    break;

                case EditTool.Erase:
                    if (!MapCoords.InBounds(cell.x, cell.y, map)) return;
                    line = new Color(1f, 0.45f, 0.45f, 0.9f); fill = new Color(1f, 0.35f, 0.35f, 0.15f);
                    break;

                case EditTool.Walkable:
                    if (!MapCoords.InBounds(cell.x, cell.y, map)) return;
                    if (_ui.WalkPaintWalkable) { line = new Color(0.3f, 1f, 0.45f, 0.95f); fill = new Color(0.3f, 1f, 0.45f, 0.2f); }
                    else { line = new Color(1f, 0.4f, 0.4f, 0.95f); fill = new Color(1f, 0.4f, 0.4f, 0.2f); }
                    break;

                case EditTool.Trigger:
                    if (!MapCoords.InBounds(cell.x, cell.y, map)) return;
                    line = new Color(0.2f, 0.9f, 1f, 0.9f); fill = new Color(0.2f, 0.9f, 1f, 0.15f);
                    break;

                default:
                    return;   // 物件工具用自己的幻影預覽
            }

            DrawFootprint(map, cell, bw, bh, fill, line);
        }

        void DrawFootprint(MapData map, Vector2Int cell, int bw, int bh, Color fill, Color line)
        {
            float ts = map.tileSize;
            float x0 = map.origin.x + cell.x * ts;
            float y0 = map.origin.y - cell.y * ts;
            float x1 = x0 + bw * ts;
            float y1 = y0 - bh * ts;

            EnsureMaterial();
            _mat.SetPass(0);
            GL.PushMatrix();

            GL.Begin(GL.QUADS);
            GL.Color(fill);
            GL.Vertex3(x0, y0, 0); GL.Vertex3(x1, y0, 0);
            GL.Vertex3(x1, y1, 0); GL.Vertex3(x0, y1, 0);
            GL.End();

            GL.Begin(GL.LINES);
            GL.Color(line);
            for (int i = 0; i <= bw; i++) { float x = x0 + i * ts; GL.Vertex3(x, y0, 0); GL.Vertex3(x, y1, 0); }
            for (int j = 0; j <= bh; j++) { float y = y0 - j * ts; GL.Vertex3(x0, y, 0); GL.Vertex3(x1, y, 0); }
            GL.End();

            GL.PopMatrix();
        }
    }
}
