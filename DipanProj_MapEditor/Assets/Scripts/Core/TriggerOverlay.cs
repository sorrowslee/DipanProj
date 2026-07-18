using UnityEngine;
using DipanMapEditor.Data;
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

            // 選取中的傳送點：畫「外型位置」預覽（黃十字＝特效實際落點；藍格＝踩踏功能區）。給對齊門用。
            if (current != null && current.typeId == "teleport" && TryMarkerPos(current, map, out Vector2 mp))
            {
                _mat.SetPass(0);
                GL.PushMatrix();
                GL.Begin(GL.LINES);
                GL.Color(new Color(1f, 0.92f, 0.2f, 0.95f));
                float rr = ts * 0.38f;
                GL.Vertex3(mp.x - rr, mp.y, 0); GL.Vertex3(mp.x + rr, mp.y, 0);
                GL.Vertex3(mp.x, mp.y - rr, 0); GL.Vertex3(mp.x, mp.y + rr, 0);
                GL.Vertex3(mp.x - rr, mp.y, 0); GL.Vertex3(mp.x, mp.y + rr, 0);
                GL.Vertex3(mp.x, mp.y + rr, 0); GL.Vertex3(mp.x + rr, mp.y, 0);
                GL.Vertex3(mp.x + rr, mp.y, 0); GL.Vertex3(mp.x, mp.y - rr, 0);
                GL.Vertex3(mp.x, mp.y - rr, 0); GL.Vertex3(mp.x - rr, mp.y, 0);
                GL.End();
                GL.PopMatrix();
            }
        }

        // 傳送點外型的預覽位置：有錨點 markerX/markerY 用它，否則用格子平均中心（同遊戲端 RegionCenter）。
        static bool TryMarkerPos(TriggerRegion r, MapData map, out Vector2 pos)
        {
            pos = Vector2.zero;
            if (r.Params != null
                && r.Params.TryGetValue("markerX", out var vx) && vx != null && float.TryParse(vx.ToString(), out float mx)
                && r.Params.TryGetValue("markerY", out var vy) && vy != null && float.TryParse(vy.ToString(), out float my))
            { pos = new Vector2(mx, my); return true; }
            if (r.cells == null || r.cells.Count == 0) return false;
            Vector2 sum = Vector2.zero; int n = 0;
            Vector2 origin = MapCoords.Origin(map);
            foreach (var c in r.cells)
            {
                if (c == null || c.Length < 2) continue;
                sum += MapCoords.CellCenter(c[0], c[1], map.tileSize, origin); n++;
            }
            if (n == 0) return false;
            pos = sum / n; return true;
        }
    }
}
