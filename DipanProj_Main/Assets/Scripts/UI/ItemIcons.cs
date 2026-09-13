using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Inventory;
using Dipan.Gacha;

namespace Dipan.UI
{
    /// <summary>
    /// **物品圖示的唯一繪製入口。**
    ///
    /// 一般道具是「一張圖」（<see cref="ItemData.Icon"/>），但有兩種道具是**多層疊合**：
    ///
    ///   ① 能力珠 ＝ 兩層
    ///        底層＝珠子本體（依等級不同外型，玩家一眼看得出這顆是幾級）
    ///        上層＝能力符號（反彈箭頭之類，一種能力一張）
    ///      這樣 8 種能力 × 3 個等級只要 3 + 8 = 11 張圖，而不是 24 張成品圖；
    ///      之後加新能力珠只要多一張 24KB 的符號圖。
    ///
    ///   ② 血統「系列起始藥劑」＝ 三層（2026-09-13 改）
    ///        底層＝共用血瓶 `bloodline_BaseBottle`
    ///        中層＝系列圖騰 `bloodline_Logo_<系列Key>`（白色紋章，疊在瓶身液體上）
    ///        上層＝右下角階級星星 `inventoryPanel_ItemLv<1|2|3>`（銅/銀/金）
    ///      同樣的算術：7 系列 × 3 階 21 瓶，只要 1 + 7 + 3 = 11 張圖。
    ///      **加新系列只要多一張 logo**，不必再畫三張成品瓶。
    ///      ⚠ 血統「進階藥劑」（ItemTable 310/311，`IsBloodlineUpgrade`）**不走這條路**——
    ///        它不屬於任何系列（全系列通用），維持自己的成品圖 `bloodline_lvup_middle/high`。
    ///
    /// **所有畫物品圖示的地方都應該呼叫這裡**（背包、倉庫、鍛造、結算、抽選、地上掉落物），
    /// 不要再直接讀 <c>data.Icon</c>——否則那個地方的珠子會只剩一顆空白珠子、看不出是什麼能力，
    /// 血統藥劑會只剩一個空瓶、看不出是哪個系列第幾階，而且**不會報錯**。
    /// 見 readme/GEM_SOCKET.md、readme/BLOODLINE.md。
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

        // ══════════════════ ArtSpec：血統藥劑疊圖規格（換素材要重量）══════════════════
        //
        // 量自 2026-09-13 的素材（bloodline_BaseBottle 500×500、bloodline_Logo_* 1254×1254
        // 但 SpiritRoot 那張是 500×500、星星 500×500）：
        //   ‧ **三種來源圖的尺寸與留白都不一樣**，所以這裡一律以「不透明內容」為準換算
        //     （<see cref="IconFit.ContentPx"/>），不是以畫布為準——否則 SpiritRoot 的圖騰
        //     會比別的系列小一號（它的內容佔 82%，其他張佔 96%）。
        //   ‧ 圖騰要對齊**方形瓶身**而不是畫布：瓶身方形玻璃區量出來是 y 212~429（500 基準），
        //     中心比畫布中心低約 7.5%。對齊畫布的話圖騰會浮在液體上緣。

        /// <summary>系列圖騰的內容寬度佔整張 icon（rect 長邊）的比例。</summary>
        const float BloodlineLogoScale = 0.38f;

        /// <summary>圖騰中心相對 icon 中心往**下**偏多少（比例）——對齊方形瓶身中心。</summary>
        const float BloodlineLogoDown = 0.075f;

        /// <summary>階級星星的內容寬度佔整張 icon 的比例。</summary>
        const float StarScale = 0.32f;

        /// <summary>星星中心相對 icon 中心的位置（比例；x 右為正、y 上為正）＝右下角。</summary>
        static readonly Vector2 StarPos = new Vector2(0.34f, -0.31f);

        /// <summary>血統素材的資料夾與星星圖。</summary>
        const string BloodlineDir  = "UI/Icons/Items/positions/bloodline/";
        const string BloodlineBase = BloodlineDir + "bloodline_BaseBottle";
        const string BloodlineLogo = BloodlineDir + "bloodline_Logo_";
        const string StarPrefix    = "UI/InventoryPanel/inventoryPanel_ItemLv";

        /// <summary>疊在底圖之上的子物件名稱（重複使用、不會每幀新建）。</summary>
        const string OverlayName = "IconOverlay";

        /// <summary>階級星星的子物件名稱（血統藥劑專用的第三層）。</summary>
        const string StarName = "IconStar";

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

        // ══════════════════ 血統藥劑：查「是哪個系列、第幾階」 ══════════════════

        /// <summary>
        /// 這個物品是不是「畫成血瓶＋圖騰＋星星」的系列起始藥劑？是的話回傳系列代號與階數。
        ///
        /// **系列與階數不另存欄位**，一律由 <see cref="BloodlineSeriesTable"/>（表A）
        /// 反查 ItemTable 的 <c>BloodlineID</c> 得到——表A 是「系列 ↔ 階段 ↔ 血統 Id」的唯一真相，
        /// 在這裡重存一份就會有兩套數值打架。系列的 <c>Key</c>（Jiangshi／Blazeborn…）
        /// 刻意就是 logo 的檔名，所以加新系列時**不必改這支程式**。
        /// </summary>
        static bool TryBloodlineArt(ItemData d, out string seriesKey, out int stage)
        {
            seriesKey = null; stage = 0;
            // 進階藥劑（全系列通用）不屬於任何系列，維持它自己的成品圖。
            if (d == null || !d.IsBloodlineStarter) return false;
            if (!BloodlineSeriesTable.TryLocate(d.BloodlineID, out var series, out stage)) return false;
            if (series == null || string.IsNullOrEmpty(series.Key)) return false;
            seriesKey = series.Key;
            return true;
        }

        // ══════════════════ 取圖 ══════════════════

        /// <summary>這個物品的底圖。珠子＝依等級的珠身；血統起始藥劑＝共用血瓶；其他＝ItemTable 的 IconPath。</summary>
        public static Sprite BaseOf(int itemId, int gemLevel)
        {
            var inv = InventorySystem.Instance;
            var d = inv != null ? inv.GetData(itemId) : null;
            if (d == null) return null;

            if (d.IsGem)
            {
                var gd = ItemManager.Gems.Get(d.GemID);
                string color = gd != null ? gd.BaseColor : "red";
                int lv = Mathf.Clamp(gemLevel, 1, 3);
                return Load($"{GemBaseDir}{color}_lv{lv}");
            }

            // 查得到系列才換血瓶；查不到（表沒填好）就退回 CSV 那張圖，畫面不會開天窗。
            if (TryBloodlineArt(d, out _, out _)) return Load(BloodlineBase);

            return d.Icon;
        }

        /// <summary>這個物品要疊在底圖上的符號（珠子的能力符號／血統的系列圖騰）；沒有就回 null。</summary>
        public static Sprite OverlayOf(int itemId)
        {
            var inv = InventorySystem.Instance;
            var d = inv != null ? inv.GetData(itemId) : null;
            if (d == null) return null;

            if (d.IsGem)
            {
                var gd = ItemManager.Gems.Get(d.GemID);
                if (gd == null || string.IsNullOrEmpty(gd.Icon)) return null;
                return Load($"{GemIconDir}{gd.Icon}");
            }

            if (TryBloodlineArt(d, out string key, out _)) return Load($"{BloodlineLogo}{key}");

            return null;
        }

        /// <summary>
        /// 這個物品右下角要蓋的階級星星（目前只有血統起始藥劑有：一階銅、二階銀、三階金）；
        /// 沒有就回 null。
        /// </summary>
        public static Sprite StarOf(int itemId)
        {
            var inv = InventorySystem.Instance;
            var d = inv != null ? inv.GetData(itemId) : null;
            if (d == null) return null;
            if (!TryBloodlineArt(d, out _, out int stage)) return null;
            if (stage < 1 || stage > BloodlineSeriesTable.MaxStage) return null;
            return Load($"{StarPrefix}{stage}");
        }

        /// <summary>符號要往上偏多少（相對圖高的比例）。</summary>
        static float OffsetUpOf(int gemLevel)
        {
            int i = Mathf.Clamp(gemLevel, 1, GemIconOffsetUp.Length) - 1;
            return GemIconOffsetUp[i];
        }

        /// <summary>這個物品的疊圖是不是「血統圖騰」（用內容正規化那套算式，和珠子不同）。</summary>
        static bool IsBloodlineArt(int itemId)
        {
            var inv = InventorySystem.Instance;
            var d = inv != null ? inv.GetData(itemId) : null;
            return d != null && TryBloodlineArt(d, out _, out _);
        }

        // ══════════════════ uGUI ══════════════════

        /// <summary>把某一格的內容畫到一個 Image 上（含珠子／血統的疊圖）。空的就關掉顯示。</summary>
        public static void Apply(Image target, ItemStack st)
            => Apply(target, st.ItemId, st.Inst != null && st.Inst.level > 0 ? st.Inst.level : 1);

        /// <summary>
        /// 依 itemId 畫圖示。珠子請盡量傳實際等級；只知道 id 的場合（例如抽選轉盤只有獎品清單）傳預設 1。
        /// 血統藥劑的階級不看這個參數——它是從 ItemTable 的 BloodlineID 查表來的，只有 id 也畫得對。
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

            bool bloodline = itemId > 0 && IsBloodlineArt(itemId);
            var overlay = itemId > 0 ? OverlayOf(itemId) : null;
            var child = FindChild(target, OverlayName);

            if (overlay == null)
            {
                if (child != null) child.gameObject.SetActive(false);
            }
            else
            {
                if (child == null) child = CreateChild(target, OverlayName);
                child.gameObject.SetActive(true);
                child.sprite = overlay;
                child.color = target.color;   // 跟著底圖一起壓黑（被鐵砧借走時背包會壓黑那一格）

                var prt = target.rectTransform;
                float w = prt.rect.width, h = prt.rect.height;

                if (bloodline)
                {
                    // 血統圖騰：七張 logo 的尺寸與留白都不同，所以**依不透明內容**換算，
                    // 每個系列的圖騰在瓶身上才會一樣大。
                    PlaceByContent(child, overlay, w, h, BloodlineLogoScale, new Vector2(0f, -BloodlineLogoDown));
                }
                else
                {
                    // 珠子疊圖**不另外正規化**：符號與珠身的相對比例是量出來的（符號 = 整張圖的 55%），
                    // 兩者又都以 target.rect 為基準，所以底圖被 IconFit 放大時符號會同倍率跟著放大，
                    // 疊合關係完全不變（單獨正規化符號反而會破壞這個比例）。
                    var rt = child.rectTransform;
                    rt.sizeDelta = new Vector2(w * GemIconScale, h * GemIconScale);
                    rt.anchoredPosition = new Vector2(0f, h * OffsetUpOf(gemLevel));
                }
            }

            // ── 第三層：階級星星（血統藥劑專用）──
            var star = itemId > 0 ? StarOf(itemId) : null;
            var starChild = FindChild(target, StarName);

            if (star == null)
            {
                if (starChild != null) starChild.gameObject.SetActive(false);
                return;
            }

            if (starChild == null) starChild = CreateChild(target, StarName);
            starChild.gameObject.SetActive(true);
            starChild.sprite = star;
            starChild.color = target.color;

            var trt = target.rectTransform;
            PlaceByContent(starChild, star, trt.rect.width, trt.rect.height, StarScale, StarPos);
        }

        /// <summary>
        /// 把一張疊圖擺好：讓它的**不透明內容**等比縮到「父框長邊 × contentScale」，
        /// 內容中心落在父框中心＋<paramref name="centerRatio"/>（比例，x 右為正、y 上為正）。
        ///
        /// 為什麼要對內容而不是畫布：這幾張疊圖（七張系列 logo、三顆星星）尺寸與留白都不同，
        /// 直接縮畫布的話每張看起來會不一樣大。算式與 <see cref="IconFit"/> 同源，
        /// 但**不能直接呼叫 IconFit.Fit**——它會把第一次的 sizeDelta 記成基準框，
        /// 而這裡的基準框會隨底圖正規化後的 rect 改變。
        /// </summary>
        static void PlaceByContent(Image child, Sprite sp, float w, float h, float contentScale, Vector2 centerRatio)
        {
            var rt = child.rectTransform;
            var c = IconFit.ContentPx(sp);
            float longSide = Mathf.Max(c.width, c.height);
            if (longSide <= 0f) return;

            float k = w * contentScale / longSide;
            rt.sizeDelta = new Vector2(sp.rect.width * k, sp.rect.height * k);

            // 內容中心不一定在整張圖的正中央 → 反向平移，讓看得見的那塊落在要的位置。
            Vector2 off = (c.center - sp.rect.size * 0.5f) * k;
            rt.anchoredPosition = new Vector2(w * centerRatio.x, h * centerRatio.y) - off;
        }

        static Image FindChild(Image target, string name)
        {
            var t = target.transform.Find(name);
            return t != null ? t.GetComponent<Image>() : null;
        }

        static Image CreateChild(Image target, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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
        /// 疊圖用子物件，排序 +1（符號／圖騰）、+2（階級星星）疊在底圖之上；
        /// 父物件被縮放時它們自然跟著縮。
        /// </summary>
        public static void Apply(SpriteRenderer target, ItemStack st, string sortingLayerName, int sortingOrder)
        {
            if (target == null) return;
            int lv = st.Inst != null && st.Inst.level > 0 ? st.Inst.level : 1;

            var baseSp = BaseOf(st.ItemId, lv);
            target.sprite = baseSp;

            bool bloodline = IsBloodlineArt(st.ItemId);
            var overlay = OverlayOf(st.ItemId);
            var sr = EnsureChild(target, OverlayName, overlay != null);

            if (sr != null && overlay != null)
            {
                sr.sprite = overlay;
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = sortingOrder + 1;

                if (bloodline)
                {
                    PlaceByContentWorld(sr, baseSp, overlay, BloodlineLogoScale, new Vector2(0f, -BloodlineLogoDown));
                }
                else
                {
                    // 珠子：兩張來源圖同尺寸（500×500），所以只要縮到 GemIconScale、再依比例往上移即可。
                    sr.transform.localScale = Vector3.one * GemIconScale;
                    float baseH = baseSp != null ? baseSp.bounds.size.y : 1f;
                    sr.transform.localPosition = new Vector3(0f, baseH * OffsetUpOf(lv), 0f);
                }
            }

            var star = StarOf(st.ItemId);
            var starSr = EnsureChild(target, StarName, star != null);
            if (starSr == null || star == null) return;

            starSr.sprite = star;
            starSr.sortingLayerName = sortingLayerName;
            starSr.sortingOrder = sortingOrder + 2;
            PlaceByContentWorld(starSr, baseSp, star, StarScale, StarPos);
        }

        /// <summary>世界端版本的 <see cref="PlaceByContent"/>：基準框＝底圖整張的世界尺寸。</summary>
        static void PlaceByContentWorld(SpriteRenderer sr, Sprite baseSp, Sprite sp, float contentScale, Vector2 centerRatio)
        {
            if (baseSp == null || sp == null) return;
            float w = baseSp.bounds.size.x, h = baseSp.bounds.size.y;

            var c = IconFit.ContentPx(sp);
            float longSide = Mathf.Max(c.width, c.height);
            float ppu = sp.pixelsPerUnit > 0f ? sp.pixelsPerUnit : 100f;
            if (longSide <= 0f) return;

            // k ＝「內容要放大幾倍才等於目標寬度」，單位是世界單位／像素 → 再換回 localScale。
            float k = w * contentScale / (longSide / ppu);
            sr.transform.localScale = Vector3.one * k;

            Vector2 off = (c.center - sp.rect.size * 0.5f) / ppu * k;
            sr.transform.localPosition = new Vector3(w * centerRatio.x - off.x, h * centerRatio.y - off.y, 0f);
        }

        /// <summary>拿（必要時建）世界端的疊圖子物件；<paramref name="want"/>＝false 時關掉並回 null。</summary>
        static SpriteRenderer EnsureChild(SpriteRenderer target, string name, bool want)
        {
            var t = target.transform.Find(name);
            if (!want)
            {
                if (t != null) t.gameObject.SetActive(false);
                return null;
            }

            if (t == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(target.transform, false);
                return go.AddComponent<SpriteRenderer>();
            }

            t.gameObject.SetActive(true);
            return t.GetComponent<SpriteRenderer>();
        }
    }
}
