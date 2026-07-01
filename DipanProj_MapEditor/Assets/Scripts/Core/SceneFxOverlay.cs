using UnityEngine;
using DipanMapEditor.Tools;
using DipanMapEditor.UI;
using DipanMapEditor.Data;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 場景特效工具下，畫每個特效的起點（綠）、終點（紅）與之間的弧線；選取中的加亮。
    /// 掛在相機上，OnPostRender 用 GL 畫線（同 ObjectSelectionOverlay 範式）。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SceneFxOverlay : MonoBehaviour
    {
        static readonly Color StartColor = new Color(0.2f, 1f, 0.35f, 0.95f);   // 綠：起點
        static readonly Color EndColor = new Color(1f, 0.3f, 0.3f, 0.95f);      // 紅：終點
        static readonly Color LineColor = new Color(1f, 1f, 1f, 0.55f);
        static readonly Color SelTint = new Color(1f, 1f, 0.3f, 1f);            // 選取加亮（黃）

        Material _mat;
        EditorUI _ui;
        SceneFxController _ctl;

        void EnsureRefs()
        {
            if (_mat == null)
            {
                _mat = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _mat.SetInt("_ZWrite", 0);
            }
            if (_ui == null) _ui = FindObjectOfType<EditorUI>();
            if (_ctl == null) _ctl = FindObjectOfType<SceneFxController>();
        }

        void OnPostRender()
        {
            if (MapSession.Instance == null || MapSession.Instance.Map == null) return;
            EnsureRefs();
            if (_ui == null || _ctl == null) return;
            if (_ui.CurrentTool != EditTool.SceneFx) return;

            var map = MapSession.Instance.Map;
            float r = Mathf.Max(0.15f, map.tileSize * 0.3f);   // 標記大小

            _mat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);

            foreach (var fx in map.sceneFx)
            {
                bool sel = fx == _ctl.Selected;
                Vector3 s = new Vector3(fx.startX, fx.startY, 0f);
                Vector3 e = new Vector3(fx.endX, fx.endY, 0f);

                // 弧線（有終點才畫）
                if (fx.hasEnd)
                {
                    Vector3 dir = (e - s);
                    float len = dir.magnitude;
                    Vector3 perp = (len > 1e-4f) ? new Vector3(-dir.y, dir.x, 0f).normalized : Vector3.right;
                    Vector3 ctrl = (s + e) * 0.5f + perp * fx.bulge;
                    GL.Color(sel ? SelTint : LineColor);
                    Vector3 prev = s;
                    for (int i = 1; i <= 16; i++)
                    {
                        float t = i / 16f;
                        Vector3 p = Bezier(s, ctrl, e, t);
                        GL.Vertex(prev); GL.Vertex(p);
                        prev = p;
                    }
                }

                // 起點：永遠綠；選取時另加一圈黃色外框當標示（不改內框顏色）。
                GL.Color(StartColor);
                Box(s, r);
                if (sel) { GL.Color(SelTint); Box(s, r * 1.6f); }

                // 終點：永遠紅；選取時另加黃色外框。
                if (fx.hasEnd)
                {
                    GL.Color(EndColor);
                    Box(e, r);
                    if (sel) { GL.Color(SelTint); Box(e, r * 1.6f); }
                }
            }

            GL.End();
            GL.PopMatrix();
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        static void Box(Vector3 c, float r)
        {
            Vector3 bl = c + new Vector3(-r, -r), br = c + new Vector3(r, -r);
            Vector3 tr = c + new Vector3(r, r), tl = c + new Vector3(-r, r);
            GL.Vertex(bl); GL.Vertex(br);
            GL.Vertex(br); GL.Vertex(tr);
            GL.Vertex(tr); GL.Vertex(tl);
            GL.Vertex(tl); GL.Vertex(bl);
        }
    }
}
