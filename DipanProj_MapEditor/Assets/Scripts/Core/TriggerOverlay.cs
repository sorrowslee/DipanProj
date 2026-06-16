using UnityEngine;
using DipanMapEditor.Tools;
using DipanMapEditor.UI;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// Trigger 工具啟用時，用 GL 依各區域的類型顏色畫半透明疊加；
    /// 當前選取的區域用較高透明度加亮。掛在相機上。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class TriggerOverlay : MonoBehaviour
    {
        const float NormalAlpha = 0.3f;
        const float CurrentAlpha = 0.55f;

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
            if (_ui == null || _ui.CurrentTool != EditTool.Trigger) return;

            var map = session.Map;
            var layer = map.TriggerLayer;
            if (layer?.regions == null) return;

            EnsureMaterial();
            _mat.SetPass(0);

            float ts = map.tileSize;
            float ox = map.origin.x;
            float oy = map.origin.y;
            var current = _ui.CurrentRegion;

            GL.PushMatrix();
            GL.Begin(GL.QUADS);
            foreach (var region in layer.regions)
            {
                var def = session.TriggerTypes.Find(region.typeId);
                string hex = def != null ? def.color : "#FFFFFF";
                Color col = TriggerOps.ParseColor(hex, region == current ? CurrentAlpha : NormalAlpha);
                GL.Color(col);
                foreach (var c in region.cells)
                {
                    if (c == null || c.Length < 2) continue;
                    float x0 = ox + c[0] * ts;
                    float y0 = oy - c[1] * ts;
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
