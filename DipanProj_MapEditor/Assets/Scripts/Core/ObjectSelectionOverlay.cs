using UnityEngine;
using DipanMapEditor.Tools;
using DipanMapEditor.UI;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 物件工具下，於選取的地上物四周畫外框（＝整張原圖邊界，含透明邊）。
    /// 掛在相機上，OnPostRender 用 GL 畫線。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ObjectSelectionOverlay : MonoBehaviour
    {
        public Color frameColor = new Color(0.2f, 0.9f, 1f, 0.95f);

        Material _mat;
        EditorUI _ui;
        ObjectController _ctl;
        ObjectView _view;

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
            if (_ctl == null) _ctl = FindObjectOfType<ObjectController>();
            if (_view == null) _view = FindObjectOfType<ObjectView>();
        }

        void OnPostRender()
        {
            if (MapSession.Instance == null || MapSession.Instance.Map == null) return;
            EnsureRefs();
            if (_ui == null || _ctl == null || _view == null) return;
            if (_ui.CurrentTool != EditTool.Object || _ctl.Selected == null) return;
            if (!_view.TryGetSpriteCorners(_ctl.Selected, out var bl, out var br, out var tr, out var tl)) return;

            _mat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            GL.Color(frameColor);
            Line(bl, br); Line(br, tr); Line(tr, tl); Line(tl, bl);
            GL.End();
            GL.PopMatrix();
        }

        static void Line(Vector3 a, Vector3 b)
        {
            GL.Vertex(a); GL.Vertex(b);
        }
    }
}
