using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// **把物品圖示正規化成「不透明內容剛好塞滿指定方框」。**
    ///
    /// 【為什麼需要】
    /// 這些 icon 的透明留白差非常多——2026-08-07 量過 30 張：內容佔長邊最少只有 41%
    /// （`item_hpPosition_s` 500×500 裡只有 146×206），最多 100%（`weapon_sword` 整張都是圖）。
    /// 於是同一個格子、同一個 sizeDelta 畫出來，藥水看起來只有劍的一半不到。
    /// 光靠 `preserveAspect` 沒有用——它只管長寬比，不管畫面裡有多少是空氣。
    ///
    /// 【做法】
    /// 量出 sprite 的不透明內容範圍，反推 Image 的 rect 要多大、要往哪偏，
    /// 讓「看得見的那塊」正好置中塞滿呼叫端要的方框。等於把 ForgingPanel / GachaPanel / ItemIcons
    /// 那套手動 ArtSpec 自動化——差別是這裡不必為每張圖手寫常數，換圖也不用重量。
    ///
    /// 【內容範圍怎麼來的】
    /// 用 <see cref="Sprite.vertices"/>（sprite 的緊貼網格頂點）。專案的 icon 匯入設定是
    /// **Mesh Type = Tight**（`spriteMeshType: 1`），Unity 會依 alpha 產生貼合外形的網格，
    /// 所以頂點的外接框就是不透明內容的範圍。**這條路不需要貼圖開 Read/Write**
    /// （開了會多一份 CPU 記憶體，而且新圖還得記得勾——正是這專案一再踩的那種坑）。
    /// 萬一哪張圖是 Full Rect，頂點就是整張圖的四角 → 自動退回「不縮放」，行為與以前相同。
    ///
    /// 呼叫端不必自己呼叫這裡：<see cref="ItemIcons.Apply(Image,int,int)"/> 已經內建，
    /// 而它是所有畫物品圖示的唯一入口。
    /// </summary>
    public static class IconFit
    {
        /// <summary>內容要佔方框的比例（1 = 剛好塞滿）。個別呼叫端可另外傳。</summary>
        public const float DefaultFill = 1f;

        // ⚠ 字典型的 UnityEngine.Object 快取：容器永遠不會變 null，停止 Play 後裡面的 Sprite 卻已銷毀。
        //    一定要在 PlayModeStaticReset 清掉（見該檔說明）。
        static readonly Dictionary<Sprite, Rect> _content = new Dictionary<Sprite, Rect>();

        /// <summary>關掉 Domain Reload 後 static 不會歸零，進 Play 時由 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode() => _content.Clear();

        /// <summary>
        /// 這張 sprite 的「不透明內容」範圍（像素，原點 = sprite.rect 左下角）。量不到就回整張。
        /// 結果會快取——每張圖只算一次。
        /// </summary>
        public static Rect ContentPx(Sprite s)
        {
            if (s == null) return new Rect(0f, 0f, 1f, 1f);
            if (_content.TryGetValue(s, out var cached) && cached.width > 0f) return cached;

            var full = new Rect(0f, 0f, s.rect.width, s.rect.height);
            var result = full;

            var v = s.vertices;
            if (v != null && v.Length >= 3)
            {
                float ppu = s.pixelsPerUnit > 0f ? s.pixelsPerUnit : 100f;
                Vector2 mn = v[0], mx = v[0];
                for (int i = 1; i < v.Length; i++) { mn = Vector2.Min(mn, v[i]); mx = Vector2.Max(mx, v[i]); }

                // vertices 以 pivot 為原點、單位是 unit；換回「相對 rect 左下角」的像素。
                Vector2 mnPx = mn * ppu + s.pivot;
                Vector2 mxPx = mx * ppu + s.pivot;
                var tight = Rect.MinMaxRect(mnPx.x, mnPx.y, mxPx.x, mxPx.y);

                // 合理性檢查：要在整張圖之內、而且不能算出一個沒有面積的框。
                // 算壞就退回整張（等於維持舊行為，不會炸）。
                if (tight.width > 1f && tight.height > 1f &&
                    tight.width <= full.width + 1f && tight.height <= full.height + 1f)
                    result = tight;
            }

            _content[s] = result;
            return result;
        }

        /// <summary>
        /// 把 icon 正規化：讓它的不透明內容置中、等比塞滿「呼叫端第一次設定的 sizeDelta」乘上 fill。
        ///
        /// 只處理**固定尺寸**的 icon（anchorMin == anchorMax）。四邊拉伸型的 icon 直接跳過、維持原行為，
        /// 因為那種 sizeDelta 的語意是「相對錨點的內縮」，用同一套算式會算歪。
        /// </summary>
        public static void Fit(Image img, float fill = DefaultFill)
        {
            if (img == null) return;
            var rt = img.rectTransform;
            if (rt.anchorMin != rt.anchorMax) return;   // 拉伸型：不碰

            var box = img.GetComponent<IconFitBox>();
            if (box == null) box = img.gameObject.AddComponent<IconFitBox>();
            if (!box.captured)
            {
                box.baseSize = rt.sizeDelta;
                box.basePos = rt.anchoredPosition;
                box.captured = true;
            }
            if (box.baseSize.x <= 0f || box.baseSize.y <= 0f) return;

            var sp = img.sprite;
            if (sp == null) { Restore(rt, box); return; }

            var c = ContentPx(sp);
            if (c.width <= 0f || c.height <= 0f) { Restore(rt, box); return; }

            // 內容要放大/縮小幾倍，才會剛好塞進方框（等比、取比較嚴的那一邊）
            float k = Mathf.Min(box.baseSize.x * fill / c.width, box.baseSize.y * fill / c.height);

            // Image 的 rect 是「整張圖」，所以要照同樣的倍率放大整張
            rt.sizeDelta = new Vector2(sp.rect.width * k, sp.rect.height * k);

            // 內容中心不一定在整張圖的正中央 → 反向平移，讓看得見的那塊落在方框中心
            Vector2 off = (c.center - sp.rect.size * 0.5f) * k;
            rt.anchoredPosition = box.basePos - off;

            img.preserveAspect = false;   // 已經按內容算好比例，不要再讓 preserveAspect 二次縮放
        }

        static void Restore(RectTransform rt, IconFitBox box)
        {
            rt.sizeDelta = box.baseSize;
            rt.anchoredPosition = box.basePos;
        }
    }
}
