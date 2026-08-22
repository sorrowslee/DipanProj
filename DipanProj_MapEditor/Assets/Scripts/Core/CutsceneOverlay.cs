using UnityEngine;
using DipanMapEditor.Tools;
using DipanMapEditor.UI;
using DipanMapEditor.Data;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 劇情工具下，畫每個演員起點（npc 藍 / player 金）、朝向短線、走位折線（起點→依序 move 目標），
    /// 以及有座標的步驟標記（紫十字）；選取者加亮（黃）。掛在相機上，OnPostRender 用 GL 畫（同 SceneFxOverlay 範式）。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CutsceneOverlay : MonoBehaviour
    {
        static readonly Color NpcColor = new Color(0.3f, 0.9f, 1f, 0.95f);
        static readonly Color PlayerColor = new Color(1f, 0.8f, 0.2f, 0.95f);
        static readonly Color StepColor = new Color(0.8f, 0.5f, 1f, 0.95f);
        static readonly Color PathColor = new Color(1f, 1f, 1f, 0.5f);
        static readonly Color SelTint = new Color(1f, 1f, 0.3f, 1f);

        Material _mat;
        EditorUI _ui;
        CutsceneController _ctl;

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
            if (_ctl == null) _ctl = FindObjectOfType<CutsceneController>();
        }

        void OnPostRender()
        {
            if (MapSession.Instance == null || MapSession.Instance.Map == null) return;
            EnsureRefs();
            if (_ui == null || _ctl == null) return;
            if (_ui.CurrentTool != EditTool.Cutscene) return;
            var cs = MapSession.Instance.Map.MainCutscene;
            if (cs == null) return;

            float r = Mathf.Max(0.15f, MapSession.Instance.Map.tileSize * 0.35f);
            _mat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);

            // 每個演員的走位折線：起點 → 依序 move 步驟目標
            foreach (var a in cs.actors)
            {
                GL.Color(PathColor);
                Vector3 prev = new Vector3(a.x, a.y, 0f);
                foreach (var s in cs.steps)
                {
                    if (s.type != "move" || s.actorId != a.id || !s.hasPos) continue;
                    Vector3 p = new Vector3(s.x, s.y, 0f);
                    GL.Vertex(prev); GL.Vertex(p);
                    prev = p;
                }
            }

            // 有座標的步驟標記（move/camera）
            foreach (var s in cs.steps)
            {
                if (!s.hasPos) continue;
                bool sel = s == _ctl.SelectedStep;
                GL.Color(sel ? SelTint : StepColor);
                Cross(new Vector3(s.x, s.y, 0f), r * (sel ? 1.4f : 1f));
            }

            // 演員起點 + 朝向短線
            foreach (var a in cs.actors)
            {
                bool sel = a == _ctl.SelectedActor;
                GL.Color(a.kind == "player" ? PlayerColor : NpcColor);
                Vector3 c = new Vector3(a.x, a.y, 0f);
                Box(c, r);
                float dx = a.facing == "right" ? r : -r;
                GL.Vertex(c); GL.Vertex(c + new Vector3(dx, 0f, 0f));
                if (sel) { GL.Color(SelTint); Box(c, r * 1.6f); }
            }

            GL.End();
            GL.PopMatrix();
        }

        static void Box(Vector3 c, float r)
        {
            Vector3 bl = c + new Vector3(-r, -r), br = c + new Vector3(r, -r);
            Vector3 tr = c + new Vector3(r, r), tl = c + new Vector3(-r, r);
            GL.Vertex(bl); GL.Vertex(br); GL.Vertex(br); GL.Vertex(tr);
            GL.Vertex(tr); GL.Vertex(tl); GL.Vertex(tl); GL.Vertex(bl);
        }
        static void Cross(Vector3 c, float r)
        {
            GL.Vertex(c + new Vector3(-r, 0, 0)); GL.Vertex(c + new Vector3(r, 0, 0));
            GL.Vertex(c + new Vector3(0, -r, 0)); GL.Vertex(c + new Vector3(0, r, 0));
        }
    }
}
