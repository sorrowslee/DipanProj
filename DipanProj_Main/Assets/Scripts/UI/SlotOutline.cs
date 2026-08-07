using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 格子的「外框」高亮：用四條細線圍一圈，**不填滿格子內部**。
    ///
    /// 【為什麼不用整片上色】
    /// 原本 hover 是把一整片半透明黃色鋪滿格子。格子小的時候看起來只是微微發亮，
    /// 但新背景的裝備欄是 221×258（舊版的 3.4 倍、一個道具格的 6.5 倍），同一片顏色就變成一大塊黃色看板。
    /// 而且本專案是 **Linear 色彩空間**，半透明疊圖比直覺亮很多——同樣 alpha=0.22 疊在近黑底上，
    /// Gamma 空間是 RGB(73,62,32)、Linear 空間是 RGB(129,106,41)，看起來大概亮一倍。
    /// 所以「憑感覺調的 alpha」在這裡一律會偏重。改成描邊之後，格子再大也只是一圈線，
    /// 順便把「滑鼠在這裡」跟「這格可以放」兩種提示從視覺上徹底分開。
    ///
    /// 四條線的錨點各自貼一邊，所以把 root 拉伸貼滿任何大小的格子都成立，線粗維持不變。
    /// 左右兩條刻意上下各內縮一個線粗，避免四角疊兩層（疊起來會出現四個比較亮的角）。
    /// </summary>
    public static class SlotOutline
    {
        /// <summary>
        /// 建一個外框（回傳 root，呼叫端自己決定要掛在哪、要不要顯示）。
        /// thickness 的單位是**呼叫端的座標系**（背包是底圖像素），所以整個面板縮放時線粗會跟著縮。
        /// </summary>
        public static RectTransform Create(Transform parent, string name, Color color, float thickness = 3.5f)
        {
            var root = UIBuilder.Rect(UIBuilder.Create(name, parent));
            UIBuilder.Stretch(root);

            Edge(root, "T", color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                 new Vector2(0f, thickness));
            Edge(root, "B", color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                 new Vector2(0f, thickness));
            Edge(root, "L", color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                 new Vector2(thickness, -thickness * 2f));
            Edge(root, "R", color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                 new Vector2(thickness, -thickness * 2f));
            return root;
        }

        /// <summary>整條框改色（拖曳提示的呼吸閃爍會用）。</summary>
        public static void SetColor(RectTransform outline, Color c)
        {
            if (outline == null) return;
            for (int i = 0; i < outline.childCount; i++)
            {
                var img = outline.GetChild(i).GetComponent<Image>();
                if (img != null) img.color = c;
            }
        }

        static void Edge(Transform parent, string name, Color color,
                         Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 sizeDelta)
        {
            var img = UIBuilder.Image(parent, name, null, color);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;
        }
    }
}
