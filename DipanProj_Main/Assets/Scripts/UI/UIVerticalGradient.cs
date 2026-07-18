using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 讓 uGUI Graphic（Text）的頂點做「上→下」垂直漸層著色，模仿燙金字。
    /// 掛在 Text 上、設 top/bottom 兩色即可。淡入淡出由 UIPanel 的 CanvasGroup 處理，
    /// 這裡保留頂點原有 alpha（乘進去），不干擾淡出。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIVerticalGradient : BaseMeshEffect
    {
        public Color top    = new Color(0.98f, 0.90f, 0.58f, 1f);
        public Color bottom = new Color(0.77f, 0.55f, 0.17f, 1f);

        static readonly List<UIVertex> _verts = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            vh.GetUIVertexStream(_verts);

            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < _verts.Count; i++)
            {
                float y = _verts[i].position.y;
                if (y < min) min = y;
                if (y > max) max = y;
            }
            float h = Mathf.Max(0.0001f, max - min);

            for (int i = 0; i < _verts.Count; i++)
            {
                var v = _verts[i];
                float f = (max - v.position.y) / h;          // 頂 →0、底 →1
                Color c = Color.Lerp(top, bottom, f);
                c.a *= (v.color.a / 255f);                    // 保留原本 alpha（給淡出用）
                v.color = c;
                _verts[i] = v;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(_verts);
            _verts.Clear();
        }
    }
}
