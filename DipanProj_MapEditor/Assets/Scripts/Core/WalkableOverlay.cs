using UnityEngine;
using DipanMapEditor.Tools;
using DipanMapEditor.UI;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 可走工具啟用時，用 GL 在每格畫紅(不可走)/綠(可走)半透明疊加。
    /// 掛在相機上，OnPostRender 繪製（畫在場景之上）。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class WalkableOverlay : MonoBehaviour
    {
        public Color blockedColor = new Color(0.85f, 0.2f, 0.2f, 0.08f);   // 再淡一半（原始的 1/4）
        public Color walkableColor = new Color(0.2f, 0.85f, 0.35f, 0.055f);

        Material _mat;
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
            if (_ui == null) _ui = FindObjectOfType<EditorUI>();
            if (_ui == null || _ui.CurrentTool != EditTool.Walkable) return;

            var map = session.Map;
            EnsureMaterial();
            _mat.SetPass(0);

            float ts = map.tileSize;
            float ox = map.origin.x;
            float oy = map.origin.y;

            GL.PushMatrix();
            GL.Begin(GL.QUADS);
            for (int gy = 0; gy < map.height; gy++)
            {
                for (int gx = 0; gx < map.width; gx++)
                {
                    GL.Color(WalkableOps.IsBlocked(map, gx, gy) ? blockedColor : walkableColor);
                    float x0 = ox + gx * ts;
                    float y0 = oy - gy * ts;
                    float x1 = x0 + ts;
                    float y1 = y0 - ts;
                    GL.Vertex3(x0, y0, 0); GL.Vertex3(x1, y0, 0);
                    GL.Vertex3(x1, y1, 0); GL.Vertex3(x0, y1, 0);
                }
            }
            GL.End();
            GL.PopMatrix();
        }
    }
}
