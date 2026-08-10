using UnityEngine;
using DipanMapEditor.Tools;
using DipanMapEditor.UI;
using DipanMapEditor.Data;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 把每盞燈畫成光圈：外圈＝照得到的範圍、內圈＝全亮範圍（＝邊緣柔和度）。圈的顏色就是該盞燈的光色。
    /// 兩種光源都畫：
    ///   ① **獨立光源**（`map.lights`，不綁地上物）—— 在「照明」工具下顯示，另加中心十字方便對準火焰。
    ///   ② **地上物自帶的燈**（`ObjectInstance.lightRadius`）—— 在「地上物」工具下顯示。
    /// 在「照明」工具下也會把地上物的燈畫成暗一點的圈，這樣排一整個房間的照明時看得到全貌。
    ///
    /// 掛在相機上，OnPostRender 用 GL 畫線（同 ObjectSelectionOverlay / SceneFxOverlay 範式）。
    ///
    /// 為什麼需要：編輯器不跑氛圍後處理，光的實際效果只有進遊戲才看得到。
    /// 沒有這圈線的話擺燈等於盲填數字，半徑要來回試好幾次。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class LightOverlay : MonoBehaviour
    {
        const int Segments = 48;                        // 圓的邊數（夠圓又不吃效能）
        static readonly Color FallbackWarm = new Color(1.00f, 0.78f, 0.52f, 1f);

        Material _mat;
        EditorUI _ui;
        ObjectController _objCtl;
        LightController _lightCtl;

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
            if (_objCtl == null) _objCtl = FindObjectOfType<ObjectController>();
            if (_lightCtl == null) _lightCtl = FindObjectOfType<LightController>();
        }

        void OnPostRender()
        {
            if (MapSession.Instance == null || MapSession.Instance.Map == null) return;
            EnsureRefs();
            if (_ui == null) return;

            bool onLightTool = _ui.CurrentTool == EditTool.Light;
            bool onObjectTool = _ui.CurrentTool == EditTool.Object;
            if (!onLightTool && !onObjectTool) return;   // 其他工具下不畫，免得擋到編輯

            var map = MapSession.Instance.Map;

            _mat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);

            // ── ① 獨立光源（照明工具下才畫；地上物工具下不畫，免得兩種燈混在一起分不清）──
            if (onLightTool && map.lights != null)
            {
                // 把手大小＝可抓取半徑，必須與 LightController.PickNearest 的 pickR 一致，
                // 否則會出現「看得到把手卻抓不到」或「抓得到卻沒畫」的錯位。
                float handle = Mathf.Max(0.4f, map.tileSize * 0.5f);
                bool dragging = _lightCtl != null && _lightCtl.Dragging;

                foreach (var l in map.lights)
                {
                    if (l == null) continue;
                    Color c = ParseHex(l.color);
                    bool isSel = _lightCtl != null && _lightCtl.Selected == l;
                    var center = new Vector3(l.x, l.y, 0f);

                    DrawRings(center, l.radius, l.softness, c, isSel);

                    // 把手：光源本身沒有圖，沒有這個就不知道該抓哪裡。
                    // 拖曳中的那盞多畫兩圈做成「實心」感，一眼看出正在被拖的是誰。
                    Color hc = isSel ? new Color(1f, 1f, 0.4f, 1f) : new Color(c.r, c.g, c.b, 0.9f);
                    Circle(center, handle, hc);
                    if (isSel && dragging)
                    {
                        Circle(center, handle * 0.72f, hc);
                        Circle(center, handle * 0.44f, hc);
                    }
                    GL.Color(hc);
                    GL.Vertex(center + new Vector3(-handle, 0f, 0f)); GL.Vertex(center + new Vector3(handle, 0f, 0f));
                    GL.Vertex(center + new Vector3(0f, -handle, 0f)); GL.Vertex(center + new Vector3(0f, handle, 0f));
                }
            }

            // ── ② 地上物自帶的燈 ──
            var game = map.GameLayer;
            if (game != null && game.objects != null)
            {
                foreach (var o in game.objects)
                {
                    if (o == null || o.lightRadius <= 0f) continue;
                    Color c = ParseHex(o.lightColor);
                    bool isSel = onObjectTool && _objCtl != null && _objCtl.Selected == o;
                    DrawRings(new Vector3(o.x, o.y, 0f), o.lightRadius, o.lightSoftness, c, isSel);
                }
            }

            GL.End();
            GL.PopMatrix();
        }

        // 外圈（照得到的範圍）＋內圈（全亮範圍）＋選取時的黃描邊。
        static void DrawRings(Vector3 center, float radius, float softness, Color c, bool isSel)
        {
            if (radius <= 0f) return;
            Circle(center, radius, new Color(c.r, c.g, c.b, isSel ? 1f : 0.55f));
            float inner = radius * Mathf.Clamp01(softness > 0f ? softness : 0.46f);
            Circle(center, inner, new Color(c.r, c.g, c.b, isSel ? 0.7f : 0.3f));
            if (isSel) Circle(center, radius * 1.02f, new Color(1f, 1f, 0.4f, 0.9f));
        }

        // 用 GL.LINES 畫一個圓（呼叫端已 GL.Begin(GL.LINES)）。
        static void Circle(Vector3 center, float radius, Color color)
        {
            if (radius <= 0.001f) return;
            GL.Color(color);
            float step = Mathf.PI * 2f / Segments;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= Segments; i++)
            {
                float a = step * i;
                Vector3 cur = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                GL.Vertex(prev);
                GL.Vertex(cur);
                prev = cur;
            }
        }

        /// <summary>6 碼 16 進位 RRGGBB（可含 #）→ Color；空/無效回預設暖橘。</summary>
        static Color ParseHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return FallbackWarm;
            string s = hex.Trim().TrimStart('#');
            return ColorUtility.TryParseHtmlString("#" + s, out var c) ? c : FallbackWarm;
        }
    }
}
