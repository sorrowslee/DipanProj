using UnityEngine;
using DipanMapEditor.Tools;
using DipanMapEditor.UI;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 可走工具啟用時，用 GL 在每個子格畫半透明疊加：綠(可走)/紅(牆)/藍(水)。
    /// 掛在相機上，OnPostRender 繪製（畫在場景之上）。
    ///
    /// <para>另外兩種顯示時機（2026-09-07）：</para>
    /// <list type="bullet">
    /// <item><b>自動生成預覽</b>：`EditorUI.AutoPreview` 有東西時，改畫**套用後的結果**
    ///       （綠＝可走、紅＝牆，語意與平常的筆刷完全一致，只是濃一點）。預覽中筆刷改的是預覽本身，
    ///       所以塗完立刻變色、改完再一起套用。</item>
    /// <item><b>試走模式</b>：`EditorUI.PlaytestShowWalkable` 開著時照樣畫，這樣可以邊走邊看
    ///       是哪一格擋住的（按 Tab 切換）。</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class WalkableOverlay : MonoBehaviour
    {
        public Color wallColor = new Color(0.85f, 0.2f, 0.2f, 0.10f);      // 牆 = 紅
        public Color waterColor = new Color(0.25f, 0.5f, 0.95f, 0.12f);    // 水/坑 = 藍
        public Color walkableColor = new Color(0.2f, 0.85f, 0.35f, 0.055f);// 可走 = 綠

        [Header("自動生成預覽")]
        [Tooltip("預覽時的顏色語意與平常完全一致（綠=可走、紅=牆），只是濃一點表示還沒套用。\n" +
                 "⚠ 不要再做「差異色」：試過用橘色標『現在可走但預覽沒抓到』，結果作者拿綠筆去塗、" +
                 "顏色卻不變（因為那格仍然同時滿足『現在可走』與『預覽沒抓到』），完全誤導。" +
                 "預覽要顯示的是**套用後的結果**，不是差異。")]
        public Color previewWalkColor = new Color(0.2f, 0.9f, 0.4f, 0.16f);
        public Color previewWallColor = new Color(0.9f, 0.25f, 0.25f, 0.20f);

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
            if (_ui == null) return;

            bool inWalkTool = _ui.CurrentTool == EditTool.Walkable;
            bool inPlaytest = _ui.CurrentTool == EditTool.Playtest && _ui.PlaytestShowWalkable;
            if (!inWalkTool && !inPlaytest) return;

            var map = session.Map;
            EnsureMaterial();
            _mat.SetPass(0);

            float fs = MapCoords.FineSize(map);   // 子格世界尺寸
            float ox = map.origin.x;
            float oy = map.origin.y;
            int fw = map.FineWidth;
            int fh = map.FineHeight;

            // 預覽只在可走工具下顯示（試走時要看的是「現在實際會擋哪裡」，不是還沒套用的預覽）
            bool[] preview = inWalkTool ? _ui.AutoPreview : null;
            if (preview != null && preview.Length != fw * fh) preview = null;

            GL.PushMatrix();
            GL.Begin(GL.QUADS);
            for (int fy = 0; fy < fh; fy++)
            {
                for (int fx = 0; fx < fw; fx++)
                {
                    Color col;
                    if (preview != null)
                    {
                        // 預覽＝直接畫「套用後會變成什麼」，語意與平常的綠/紅一致。
                        col = preview[fy * fw + fx] ? previewWalkColor : previewWallColor;
                    }
                    else
                    {
                        char s = WalkableOps.GetState(map, fx, fy);
                        col = s == WalkableOps.Wall ? wallColor
                            : s == WalkableOps.Water ? waterColor
                            : walkableColor;
                    }
                    GL.Color(col);
                    float x0 = ox + fx * fs;
                    float y0 = oy - fy * fs;
                    float x1 = x0 + fs;
                    float y1 = y0 - fs;
                    GL.Vertex3(x0, y0, 0); GL.Vertex3(x1, y0, 0);
                    GL.Vertex3(x1, y1, 0); GL.Vertex3(x0, y1, 0);
                }
            }
            GL.End();

            // 種子點：畫一個十字，讓作者知道自動生成是從哪裡長出來的
            if (inWalkTool && _ui.AutoSeedValid)
            {
                var seed = _ui.AutoSeed;
                float cx = ox + (seed.x + 0.5f) * fs;
                float cy = oy - (seed.y + 0.5f) * fs;
                float len = fs * 6f;
                GL.Begin(GL.LINES);
                GL.Color(new Color(1f, 1f, 0.2f, 0.95f));
                GL.Vertex3(cx - len, cy, 0); GL.Vertex3(cx + len, cy, 0);
                GL.Vertex3(cx, cy - len, 0); GL.Vertex3(cx, cy + len, 0);
                GL.End();
            }

            GL.PopMatrix();
        }
    }
}
