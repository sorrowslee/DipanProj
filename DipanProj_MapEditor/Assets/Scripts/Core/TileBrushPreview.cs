using UnityEngine;
using DipanMapEditor.Tools;
using DipanMapEditor.UI;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 畫地磚工具下，於滑鼠所在格顯示「筆刷footprint」方框（含內部格線），
    /// 大小 = 當前選取的 tile 區塊（例如 3×4），以游標格為左上角往右下展開——
    /// 與實際貼上的範圍一致，讓使用者下手前就知道會鋪到哪。掛在相機上。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class TileBrushPreview : MonoBehaviour
    {
        public Color fillColor = new Color(0.2f, 0.9f, 1f, 0.15f);
        public Color lineColor = new Color(0.2f, 0.9f, 1f, 0.9f);

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
            if (_ui.CurrentTool != EditTool.TilePaint || !_ui.HasTileBrush) return;
            if (_ui.IsPointerOverUI(Input.mousePosition)) return;

            var map = session.Map;
            Vector3 wp = _cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int cell = MapCoords.WorldToCell(wp, map.tileSize, MapCoords.Origin(map));

            int bw = _ui.TileBrushW, bh = _ui.TileBrushH;
            float ts = map.tileSize;
            float x0 = map.origin.x + cell.x * ts;
            float y0 = map.origin.y - cell.y * ts;
            float x1 = x0 + bw * ts;
            float y1 = y0 - bh * ts;

            EnsureMaterial();
            _mat.SetPass(0);
            GL.PushMatrix();

            // 半透明填滿
            GL.Begin(GL.QUADS);
            GL.Color(fillColor);
            GL.Vertex3(x0, y0, 0); GL.Vertex3(x1, y0, 0);
            GL.Vertex3(x1, y1, 0); GL.Vertex3(x0, y1, 0);
            GL.End();

            // 外框 + 內部格線
            GL.Begin(GL.LINES);
            GL.Color(lineColor);
            for (int i = 0; i <= bw; i++)
            {
                float x = x0 + i * ts;
                GL.Vertex3(x, y0, 0); GL.Vertex3(x, y1, 0);
            }
            for (int j = 0; j <= bh; j++)
            {
                float y = y0 - j * ts;
                GL.Vertex3(x0, y, 0); GL.Vertex3(x1, y, 0);
            }
            GL.End();

            GL.PopMatrix();
        }
    }
}
