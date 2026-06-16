using UnityEngine;
using DipanMapEditor.Data;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 用 GL 即時畫出 tile 格線與畫布外框（不需任何貼圖/prefab）。
    /// 掛在相機上，於 OnPostRender 繪製。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class GridRenderer : MonoBehaviour
    {
        public Color gridColor = new Color(1f, 1f, 1f, 0.22f);   // 黑底上要看得見格線（編輯用）
        public Color borderColor = new Color(0.3f, 0.8f, 1f, 0.9f);
        public bool show = true;

        Material _mat;

        void EnsureMaterial()
        {
            if (_mat != null) return;
            // Unity 內建的純色 shader，適合 GL 畫線
            var shader = Shader.Find("Hidden/Internal-Colored");
            _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _mat.SetInt("_ZWrite", 0);
        }

        void OnPostRender()
        {
            if (!show) return;
            var session = MapSession.Instance;
            if (session == null || session.Map == null) return;

            var map = session.Map;
            EnsureMaterial();
            _mat.SetPass(0);

            float ts = map.tileSize;
            float ox = map.origin.x;
            float oy = map.origin.y;
            float right = ox + map.width * ts;
            float bottom = oy - map.height * ts;

            GL.PushMatrix();
            GL.Begin(GL.LINES);

            // 內部格線
            GL.Color(gridColor);
            for (int gx = 1; gx < map.width; gx++)
            {
                float x = ox + gx * ts;
                GL.Vertex3(x, oy, 0); GL.Vertex3(x, bottom, 0);
            }
            for (int gy = 1; gy < map.height; gy++)
            {
                float y = oy - gy * ts;
                GL.Vertex3(ox, y, 0); GL.Vertex3(right, y, 0);
            }

            // 外框
            GL.Color(borderColor);
            GL.Vertex3(ox, oy, 0);     GL.Vertex3(right, oy, 0);
            GL.Vertex3(right, oy, 0);  GL.Vertex3(right, bottom, 0);
            GL.Vertex3(right, bottom, 0); GL.Vertex3(ox, bottom, 0);
            GL.Vertex3(ox, bottom, 0); GL.Vertex3(ox, oy, 0);

            GL.End();
            GL.PopMatrix();
        }
    }
}
