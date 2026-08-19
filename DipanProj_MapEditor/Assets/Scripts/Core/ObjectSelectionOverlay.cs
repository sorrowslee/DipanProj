using UnityEngine;
using DipanMapEditor.Tools;
using DipanMapEditor.UI;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 物件工具下，於「所有選取」的地上物四周畫外框（＝整張原圖邊界，含透明邊），
    /// 並在拖曳框選時畫出框選矩形。掛在相機上，OnPostRender 用 GL 畫線。
    ///
    /// <para>另外會把**「層」不是 0 的地上物**直接標在場景上（橘＝層 +1、藍＝層 -1，角落有 ＋／－ 記號）。
    /// 「層 +1」的實際語意是「**永遠畫在角色前面、完全不參與 Y 排序**」，設在大型落地家具上會造成
    /// 「玩家站在它前面、頭還是被蓋住」——而這件事**不點選那個物件就完全看不出來**，
    /// 所以做成常駐標示。見 readme/PROBLEMS.md **E16**。</para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ObjectSelectionOverlay : MonoBehaviour
    {
        public Color frameColor = new Color(0.2f, 0.9f, 1f, 0.95f);   // 選取框（青）
        public Color boxColor = new Color(1f, 0.95f, 0.3f, 0.9f);      // 框選矩形（黃）
        public Color layerUpColor = new Color(1f, 0.58f, 0.15f, 0.9f);    // 層 +1：永遠畫在角色前面
        public Color layerDownColor = new Color(0.45f, 0.68f, 1f, 0.9f);  // 層 -1：永遠被其他地上物蓋住

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
            if (_ui.CurrentTool != EditTool.Object) return;

            _mat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);

            // 「層」不是 0 的地上物：常駐標示（不必點選就看得到）。畫在選取框之前，選取框才蓋得過去。
            var objs = MapSession.Instance.Map.GameLayer?.objects;
            if (objs != null)
            {
                for (int i = 0; i < objs.Count; i++)
                {
                    var o = objs[i];
                    if (o == null || o.zOrder == 0) continue;
                    if (!_view.TryGetSpriteCorners(o, out var lbl, out var lbr, out var ltr, out var ltl)) continue;

                    GL.Color(o.zOrder > 0 ? layerUpColor : layerDownColor);
                    Line(lbl, lbr); Line(lbr, ltr); Line(ltr, ltl); Line(ltl, lbl);

                    // 左上角畫 ＋（層往前）或 －（層往後），一眼分辨是哪一種。
                    Vector3 up = (ltl - lbl).normalized, right = (lbr - lbl).normalized;
                    float k = Mathf.Clamp(Mathf.Min((ltl - lbl).magnitude, (lbr - lbl).magnitude) * 0.12f, 0.08f, 0.3f);
                    Vector3 c = ltl + right * (k * 1.6f) - up * (k * 1.6f);
                    Line(c - right * k, c + right * k);
                    if (o.zOrder > 0) Line(c - up * k, c + up * k);
                }
            }

            // 每個選取物件的外框
            var sel = _ctl.Selection;
            if (sel != null)
            {
                GL.Color(frameColor);
                for (int i = 0; i < sel.Count; i++)
                {
                    if (_view.TryGetSpriteCorners(sel[i], out var bl, out var br, out var tr, out var tl))
                    {
                        Line(bl, br); Line(br, tr); Line(tr, tl); Line(tl, bl);
                    }
                }
            }

            // 框選中的矩形（世界座標；與物件外框同一個 GL 空間）
            if (_ctl.BoxSelecting)
            {
                GL.Color(boxColor);
                Vector2 a = _ctl.BoxStartWorld, b = _ctl.BoxCurWorld;
                float minx = Mathf.Min(a.x, b.x), maxx = Mathf.Max(a.x, b.x);
                float miny = Mathf.Min(a.y, b.y), maxy = Mathf.Max(a.y, b.y);
                Vector3 p0 = new Vector3(minx, miny, 0), p1 = new Vector3(maxx, miny, 0);
                Vector3 p2 = new Vector3(maxx, maxy, 0), p3 = new Vector3(minx, maxy, 0);
                Line(p0, p1); Line(p1, p2); Line(p2, p3); Line(p3, p0);
            }

            GL.End();
            GL.PopMatrix();
        }

        static void Line(Vector3 a, Vector3 b)
        {
            GL.Vertex(a); GL.Vertex(b);
        }
    }
}
