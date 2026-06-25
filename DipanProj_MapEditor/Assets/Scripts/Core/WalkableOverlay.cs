using UnityEngine;
using DipanMapEditor.Tools;
using DipanMapEditor.UI;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 可走工具啟用時，用 GL 在每個子格畫半透明疊加：綠(可走)/紅(牆)/藍(水)。
    /// 掛在相機上，OnPostRender 繪製（畫在場景之上）。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class WalkableOverlay : MonoBehaviour
    {
        public Color wallColor = new Color(0.85f, 0.2f, 0.2f, 0.10f);      // 牆 = 紅
        public Color waterColor = new Color(0.25f, 0.5f, 0.95f, 0.12f);    // 水/坑 = 藍
        public Color walkableColor = new Color(0.2f, 0.85f, 0.35f, 0.055f);// 可走 = 綠

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

            float fs = MapCoords.FineSize(map);   // 子格世界尺寸
            float ox = map.origin.x;
            float oy = map.origin.y;
            int fw = map.FineWidth;
            int fh = map.FineHeight;

            GL.PushMatrix();
            GL.Begin(GL.QUADS);
            for (int fy = 0; fy < fh; fy++)
            {
                for (int fx = 0; fx < fw; fx++)
                {
                    char s = WalkableOps.GetState(map, fx, fy);
                    GL.Color(s == WalkableOps.Wall ? wallColor
                           : s == WalkableOps.Water ? waterColor
                           : walkableColor);
                    float x0 = ox + fx * fs;
                    float y0 = oy - fy * fs;
                    float x1 = x0 + fs;
                    float y1 = y0 - fs;
                    GL.Vertex3(x0, y0, 0); GL.Vertex3(x1, y0, 0);
                    GL.Vertex3(x1, y1, 0); GL.Vertex3(x0, y1, 0);
                }
            }
            GL.End();
            GL.PopMatrix();
        }
    }
}
