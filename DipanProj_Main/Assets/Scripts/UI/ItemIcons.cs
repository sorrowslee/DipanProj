using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// **物品圖示的唯一繪製入口。**
    ///
    /// 一般道具是「一張圖」（<see cref="ItemData.Icon"/>），但**能力珠是兩層疊合**：
    ///   底層＝珠子本體（依等級不同外型，玩家一眼看得出這顆是幾級）
    ///   上層＝能力符號（反彈箭頭之類，一種能力一張）
    /// 這樣 8 種能力 × 3 個等級只要 3 + 8 = 11 張圖，而不是 24 張成品圖；
    /// 之後加新能力珠只要多一張 24KB 的符號圖。
    ///
    /// **所有畫物品圖示的地方都應該呼叫這裡**（背包、倉庫、鍛造、結算、抽選、地上掉落物），
    /// 不要再直接讀 <c>data.Icon</c>——否則那個地方的珠子會只剩一顆空白珠子、看不出是什麼能力，
    /// 而且不會報錯。見 readme/GEM_SOCKET.md。
    ///
    /// 疊圖的位置與大小是量出來的常數（見下方 ArtSpec 區），慣例同 ForgingPanel／GachaPanel 的 ArtSpec 表：
    /// **換素材時要重量一次**。
    /// </summary>
    public static class ItemIcons
    {
        // ══════════════════ ArtSpec：珠子疊圖規格（換素材要重量）══════════════════
        //
        // 量自 2026-08-04 的素材（gemBase 三級 + gemIcon_bounce，皆 500×500）：
        //   ‧ 四張圖的內容都以畫布中心為準，所以左右不需要偏移。
        //   ‧ 但三級的「紅球」中心高度不一樣——lv2 底下多了底座、把球往上推了 24px，
        //     lv3 上下都有金框、往上 14px。符號要對齊**紅球**而不是畫布，否則 lv2 的箭頭會偏低壓在底座上。

        /// <summary>能力符號相對整張圖的大小（0.55 ＝ 符號寬度是圖的 55%）。</summary>
        const float GemIconScale = 0.55f;

        /// <summary>各等級的珠子中心相對畫布中心往上偏多少（比例）。索引 = 等級-1。</summary>
        static readonly float[] GemIconOffsetUp = { 8f / 500f, 24f / 500f, 14f / 500f };

        /// <summary>珠子素材的資料夾。</summary>
        const string GemBaseDir = "UI/Icons/Items/gems/gemBase/gemBase_";
        const string GemIconDir = "UI/Icons/Items/gems/gemIcons/gemIcon_";

        /// <summary>疊在底圖之上的子物件名稱（重複使用、不會每幀新建）。</summary>
        const string OverlayName = "IconOverlay";

        // ══════════════════ 快取 ══════════════════

        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        /// <summary>關掉 Domain Reload 後 static 不會歸零，進 Play 時由 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode() => _cache.Clear();

        static Sprite Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            // 理論上道具 icon 不會是「圖片型文字」，但還是先解析一次語言路徑再進快取——
            // ⚠ 重點是**快取要用解析後的路徑當 key**。用解析前的邏輯路徑當 key 的話，
            //   切語言之後會直接命中上一個語言的那張圖（Sprite 還活著，連重載的機會都沒有）。
            path = Dipan.Localization.LocalizedArt.ResolveExisting(path);
            // 注意：這是「陣列/集合型的 UnityEngine.Object 快取」——停止 Play 後容器不會變 null、
            // 但裡面的 Sprite 會被銷毀。所以一定要在 PlayModeStaticReset 清掉（見該檔的說明）。
            if (_cache.TryGetValue(path, out var sp) && sp != null) return sp;
            sp = Resources.Load<Sprite>(path);
            if (sp == null) Debug.LogWarning($"[ItemIcons] 找不到圖：Resources/{path}");
            _cache[path] = sp;
            return sp;
        }

        // ══════════════════ 取圖 ══════════════════

        /// <summary>這個物品的底圖。珠子＝依等級的珠身；其他＝ItemTable 的 IconPath。</summary>
        public static Sprite BaseOf(int itemId, int gemLevel)
        {
            var inv = InventorySystem.Instance;
            var d = inv != null ? inv.GetData(itemId) : null;
            if (d == null) return null;
            if (!d.IsGem) return d.Icon;

            var gd = ItemManager.Gems.Get(d.GemID);
            string color = gd != null ? gd.BaseColor : "red";
            int lv = Mathf.Clamp(gemLevel, 1, 3);
            return Load($"{GemBaseDir}{color}_lv{lv}");
        }

        /// <summary>這個物品要疊在底圖上的符號；沒有就回 null（一般道具都是 null）。</summary>
        public static Sprite OverlayOf(int itemId)
        {
            var inv = InventorySystem.Instance;
            var d = inv != null ? inv.GetData(itemId) : null;
            if (d == null || !d.IsGem) return null;

            var gd = ItemManager.Gems.Get(d.GemID);
            if (gd == null || string.IsNullOrEmpty(gd.Icon)) return null;
            return Load($"{GemIconDir}{gd.Icon}");
        }

        /// <summary>符號要往上偏多少（相對圖高的比例）。</summary>
        static float OffsetUpOf(int gemLevel)
        {
            int i = Mathf.Clamp(gemLevel, 1, GemIconOffsetUp.Length) - 1;
            return GemIconOffsetUp[i];
        }

        // ══════════════════ uGUI ══════════════════

        /// <summary>把某一格的內容畫到一個 Image 上（含珠子的疊圖）。空的就關掉顯示。</summary>
        public static void Apply(Image target, ItemStack st)
            => Apply(target, st.ItemId, st.Inst != null && st.Inst.level > 0 ? st.Inst.level : 1);

        /// <summary>
        /// 依 itemId 畫圖示。珠子請盡量傳實際等級；只知道 id 的場合（例如抽選轉盤只有獎品清單）傳預設 1。
        /// </summary>
        public static void Apply(Image target, int itemId, int gemLevel = 1)
        {
            if (target == null) return;

            var baseSp = itemId > 0 ? BaseOf(itemId, gemLevel) : null;
            target.sprite = baseSp;
            target.enabled = baseSp != null;

            // 正規化：不同 icon 的透明留白差很多（量過的 30 張裡，內容佔長邊從 41% 到 100%），
            // 不處理的話同一個格子畫出來會差快 2.5 倍。見 IconFit 的說明。
            // ⚠ 一定要在下面算疊圖之前做——疊圖的大小與偏移是以 target.rect 為基準的。
            IconFit.Fit(target);

            var overlay = itemId > 0 ? OverlayOf(itemId) : null;
            var child = FindOverlay(target);

            if (overlay == null)
            {
                if (child != null) child.gameObject.SetActive(false);
                return;
            }

            if (child == null) child = CreateOverlay(target);
            child.gameObject.SetActive(true);
            child.sprite = overlay;
            child.color = target.color;   // 跟著底圖一起壓黑（被鐵砧借走時背包會壓黑那一格）

            // 疊圖**不另外正規化**：符號與珠身的相對比例是量出來的（符號 = 整張圖的 55%），
            // 兩者又都以 target.rect 為基準，所以底圖被 IconFit 放大時符號會同倍率跟著放大，
            // 疊合關係完全不變（單獨正規化符號反而會破壞這個比例）。
            var rt = child.rectTransform;
            var prt = target.rectTransform;
            float w = prt.rect.width, h = prt.rect.height;
            rt.sizeDelta = new Vector2(w * GemIconScale, h * GemIconScale);
            rt.anchoredPosition = new Vector2(0f, h * OffsetUpOf(gemLevel));
        }

        static Image FindOverlay(Image target)
        {
            var t = target.transform.Find(OverlayName);
            return t != null ? t.GetComponent<Image>() : null;
        }

        static Image CreateOverlay(Image target)
        {
            var go = new GameObject(OverlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(target.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;      // 疊圖純裝飾，點擊要穿到底下的格子
            img.preserveAspect = true;
            return img;
        }

        // ══════════════════ 世界端（地上掉落物）══════════════════

        /// <summary>
        /// 把物品畫到世界端的 SpriteRenderer（地上掉落物）。
        /// 疊圖用一個子物件，排序 +1 疊在底圖之上；父物件被縮放時它自然跟著縮。
        /// </summary>
        public static void Apply(SpriteRenderer target, ItemStack st, string sortingLayerName, int sortingOrder)
        {
            if (target == null) return;
            int lv = st.Inst != null && st.Inst.level > 0 ? st.Inst.level : 1;

            target.sprite = BaseOf(st.ItemId, lv);

            var overlay = OverlayOf(st.ItemId);
            var t = target.transform.Find(OverlayName);

            if (overlay == null)
            {
                if (t != null) t.gameObject.SetActive(false);
                return;
            }

            SpriteRenderer sr;
            if (t == null)
            {
                var go = new GameObject(OverlayName);
                go.transform.SetParent(target.transform, false);
                sr = go.AddComponent<SpriteRenderer>();
            }
            else
            {
                t.gameObject.SetActive(true);
                sr = t.GetComponent<SpriteRenderer>();
            }
            if (sr == null) return;

            sr.sprite = overlay;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder + 1;

            // 兩張來源圖同尺寸（500×500），所以只要縮到 GemIconScale、再依比例往上移即可。
            sr.transform.localScale = Vector3.one * GemIconScale;
            float baseH = target.sprite != null ? target.sprite.bounds.size.y : 1f;
            sr.transform.localPosition = new Vector3(0f, baseH * OffsetUpOf(lv), 0f);
        }
    }
}
