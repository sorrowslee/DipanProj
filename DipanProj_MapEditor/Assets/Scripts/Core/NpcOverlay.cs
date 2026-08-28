using UnityEngine;
using DipanMapEditor.Data;
using DipanMapEditor.Tools;
using DipanMapEditor.UI;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// NPC 工具的畫布疊加層（GL，掛相機，同 LightOverlay 範式）：
    ///   ‧ 每個 NPC 站位畫一個圓把手（選取＝黃、未選＝青）；
    ///   ‧ 巡邏路徑畫折線 站位→wp1→wp2…，路徑點畫小方塊（乒乓來回，不畫回程線）；
    ///   ‧ 放置/加點模式時把手加亮提示。
    /// 只在 NPC 工具下畫（角色本體的預覽由 <see cref="NpcView"/> 負責、所有工具都看得到）。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class NpcOverlay : MonoBehaviour
    {
        static readonly Color CNpc = new Color(0.35f, 0.9f, 1f, 0.9f);
        static readonly Color CSel = new Color(1f, 1f, 0.4f, 1f);
        static readonly Color CPath = new Color(0.5f, 1f, 0.6f, 0.8f);
        static readonly Color CWp = new Color(0.5f, 1f, 0.6f, 1f);

        Material _mat;
        EditorUI _ui;
        NpcController _ctl;

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
            if (_ctl == null) _ctl = FindObjectOfType<NpcController>();
        }

        void OnPostRender()
        {
            if (MapSession.Instance == null || MapSession.Instance.Map == null) return;
            EnsureRefs();
            if (_ui == null || _ui.CurrentTool != EditTool.Npc) return;

            var map = MapSession.Instance.Map;
            if (map.npcs == null || map.npcs.Count == 0) return;

            float handle = NpcController.NpcPickR(map);
            float wpHalf = NpcController.WaypointPickR(map);

            _mat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);

            foreach (var n in map.npcs)
            {
                if (n == null) continue;
                bool isSel = _ctl != null && _ctl.Selected == n;
                var c = isSel ? CSel : CNpc;
                var pos = new Vector3(n.x, n.y, 0f);

                // 站位把手：圓＋十字
                Circle(pos, handle, c);
                if (isSel && (_ctl.Placing || _ctl.AddingWaypoints)) Circle(pos, handle * 0.7f, c);
                GL.Color(c);
                GL.Vertex(pos + new Vector3(-handle, 0f)); GL.Vertex(pos + new Vector3(handle, 0f));
                GL.Vertex(pos + new Vector3(0f, -handle)); GL.Vertex(pos + new Vector3(0f, handle));

                // 巡邏路徑（乒乓：畫單程折線即可）
                if (n.waypoints != null && n.waypoints.Count > 0)
                {
                    Vector3 prev = pos;
                    for (int i = 0; i < n.waypoints.Count; i++)
                    {
                        var w = n.waypoints[i];
                        var wp = new Vector3(w.x, w.y, 0f);
                        GL.Color(isSel ? CPath : new Color(CPath.r, CPath.g, CPath.b, 0.35f));
                        GL.Vertex(prev); GL.Vertex(wp);
                        Square(wp, wpHalf, isSel ? CWp : new Color(CWp.r, CWp.g, CWp.b, 0.4f));
                        if (isSel && _ctl.DraggingWaypoint == i) Square(wp, wpHalf * 0.6f, CSel);
                        prev = wp;
                    }
                }
            }

            GL.End();
            GL.PopMatrix();
        }

        static void Circle(Vector3 center, float radius, Color color)
        {
            const int Seg = 36;
            if (radius <= 0.001f) return;
            GL.Color(color);
            float step = Mathf.PI * 2f / Seg;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= Seg; i++)
            {
                float a = step * i;
                Vector3 cur = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                GL.Vertex(prev); GL.Vertex(cur);
                prev = cur;
            }
        }

        static void Square(Vector3 c, float half, Color color)
        {
            GL.Color(color);
            var a = c + new Vector3(-half, -half); var b = c + new Vector3(half, -half);
            var d = c + new Vector3(half, half); var e = c + new Vector3(-half, half);
            GL.Vertex(a); GL.Vertex(b);
            GL.Vertex(b); GL.Vertex(d);
            GL.Vertex(d); GL.Vertex(e);
            GL.Vertex(e); GL.Vertex(a);
        }
    }
}
