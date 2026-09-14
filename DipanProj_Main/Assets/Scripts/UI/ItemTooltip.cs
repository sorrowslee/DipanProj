using UnityEngine;
using UnityEngine.UI;
using Dipan.Inventory;
using Dipan.Localization;

namespace Dipan.UI
{
    /// <summary>
    /// **物品浮動 tooltip —— 全遊戲唯一的一份**（背包／倉庫／鍛造共用）。
    ///
    /// 2026-09-14 之前這東西在三個面板各抄了一份，於是「加大圖預覽」「一律開在游標上方」
    /// 這類調整只會落在背包，倉庫與鍛造停在舊樣子——一致性靠人記得去改三個地方，遲早會漏。
    /// 現在版面、字級、定位規則、鑲嵌文案全部只有這裡一份，**新的面板要 tooltip 一律用這個**，
    /// 不要再自己 new 一個 VerticalLayoutGroup。（[TODO.md](../../../../readme/TODO.md) 早就記著要抽。）
    ///
    /// 用法（面板端三行）：
    /// <code>
    ///   _tip = ItemTooltip.Create(transform);   // OnBuild 裡，掛在 panel root
    ///   _tip.Show(stack);  /  _tip.Hide();      // hover 進出
    ///   void Update() => _tip.Follow();         // 跟著游標
    /// </code>
    ///
    /// 版面：**大圖預覽** ＋ 名稱（粗體金）＋ `TipStats`（正楷）＋ `TipLore`（斜體）。
    /// 高度由 `VerticalLayoutGroup + ContentSizeFitter` 自動撐開，空的段落自動隱藏。
    /// </summary>
    public class ItemTooltip
    {
        // ══════════════════ 尺寸（要放大縮小改這一區就好）══════════════════

        const float Width = 520f;
        const int NameSize = 30, StatsSize = 26, LoreSize = 24;
        const int PadX = 20, PadY = 16, Spacing = 10;

        /// <summary>tooltip 與游標的間距。</summary>
        const float GapX = 18f, GapY = 18f;

        /// <summary>
        /// 大圖預覽的邊長。物品 icon 素材是 500×500，畫成這個大小是原圖在縮、不會糊。
        /// 存在的理由：背包格子只有 124px，疊圖型的 icon（血統藥劑、能力珠）在格子裡看不清楚細節。
        /// </summary>
        const float PreviewSize = 280f;

        /// <summary>true ＝ 所有物品都畫預覽圖；false ＝ 只有血統藥劑畫，其餘維持純文字。</summary>
        const bool PreviewAllItems = true;

        /// <summary>語言表：鑲嵌珠對目前武器無效的標記（4016，鍛造介面段）。</summary>
        const int TxtGemIneffectiveMark = 4016;

        // ══════════════════ 狀態 ══════════════════

        RectTransform _root;        // tooltip 本體
        RectTransform _panelRect;   // 定位基準與夾制範圍（Popup 層的 Canvas；退路才是面板 root）
        RectTransform _previewBox;  // 預覽圖的版面容器（關的是這個，才不會在 layout 裡留一段空白）
        Image _preview;
        Text _name, _stats, _lore;

        public bool Visible => _root != null && _root.gameObject.activeSelf;

        // ══════════════════ 建構 ══════════════════

        /// <summary>
        /// 建一個 tooltip。**會掛到 `UILayer.Popup` 層**，不是掛在呼叫它的面板底下——
        /// `UILayer` 對 Popup 的定義本來就是「彈窗：確認框、提示（tooltip）。永遠壓在視窗之上」。
        ///
        /// ⚠ **為什麼不能掛在面板底下**（2026-09-14 實機踩到）：背包與倉庫同時開著時，
        /// 兩個面板是 Window 層的兄弟節點，掛在背包底下的 tooltip 再怎麼 `SetAsLastSibling()`
        /// 也只是排到**背包內部**的最後，整個倉庫面板還是畫在它上面 ⇒ tooltip 被切掉一半。
        ///
        /// <paramref name="fallbackParent"/> 是拿不到 UIManager 時的退路（傳面板的 root 即可）。
        /// ⚠ 掛在 Popup 層之後 tooltip **不再跟著面板一起隱藏**，所以面板的 `OnClose` 一定要呼叫 <see cref="Hide"/>
        /// （背包／倉庫／鍛造都已經有）。
        /// </summary>
        public static ItemTooltip Create(Transform fallbackParent)
        {
            var t = new ItemTooltip();

            var mgr = UIManager.Instance;
            Transform parent = mgr != null ? mgr.LayerRoot(UILayer.Popup) : null;
            if (parent == null) parent = fallbackParent;
            t._panelRect = (RectTransform)parent;

            var go = UIBuilder.Create("Tooltip", parent);
            t._root = UIBuilder.Rect(go);
            t._root.anchorMin = t._root.anchorMax = new Vector2(0.5f, 0.5f);
            t._root.pivot = new Vector2(0f, 0f);
            t._root.sizeDelta = new Vector2(Width, 10f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.07f, 0.96f);
            bg.raycastTarget = false;   // tooltip 跟著游標，絕不能擋住 hover 事件

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(PadX, PadX, PadY, PadY);
            vlg.spacing = Spacing;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;   // 寬固定
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;     // 高自動

            // 大圖預覽（**要排在名稱之前**才會出現在最上面）。
            // VerticalLayoutGroup 會把直接子物件的寬度撐滿，所以這裡放一個「只負責占高度」的容器，
            // 真正的圖當它的子物件、用固定 sizeDelta 置中 —— 這樣圖才不會被拉成長方形。
            var boxGO = UIBuilder.Create("PreviewBox", go.transform);
            t._previewBox = UIBuilder.Rect(boxGO);
            var le = boxGO.AddComponent<LayoutElement>();
            le.preferredHeight = PreviewSize;
            le.minHeight = PreviewSize;

            t._preview = UIBuilder.Image(boxGO.transform, "Icon", null, Color.white);
            var prt = t._preview.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero;
            prt.sizeDelta = new Vector2(PreviewSize, PreviewSize);
            t._preview.raycastTarget = false;
            t._preview.preserveAspect = true;

            t._name = UIBuilder.Text(go.transform, "Name", "", NameSize, new Color(1f, 0.85f, 0.45f), TextAnchor.UpperLeft);
            t._name.fontStyle = FontStyle.Bold;
            t._stats = UIBuilder.Text(go.transform, "Stats", "", StatsSize, new Color(0.92f, 0.92f, 0.95f), TextAnchor.UpperLeft);
            t._stats.fontStyle = FontStyle.Normal;   // 正楷
            t._lore = UIBuilder.Text(go.transform, "Lore", "", LoreSize, new Color(0.72f, 0.69f, 0.62f), TextAnchor.UpperLeft);
            t._lore.fontStyle = FontStyle.Italic;    // 斜體

            go.SetActive(false);
            return t;
        }

        // ══════════════════ 開關 ══════════════════

        /// <summary>只知道 itemId 的場合（沒有物品實例，例如鍛造的孔位）。</summary>
        public void Show(int itemId, string extraStats = null)
            => Show(new ItemStack { ItemId = itemId, Count = 1, Inst = null }, extraStats);

        /// <summary>
        /// 顯示某一件東西的說明。
        /// <paramref name="extraStats"/> ＝ 面板自己要補的一段話（鍛造用它講「這顆珠子對這把武器無效」），
        /// 會接在 `TipStats` 後面；一般面板傳 null 就好。
        /// </summary>
        public void Show(ItemStack st, string extraStats = null)
        {
            var inv = InventorySystem.Instance;
            var d = (st.ItemId > 0 && inv != null) ? inv.GetData(st.ItemId) : null;
            if (d == null) { Hide(); return; }

            // 名稱後面標出「這一件」的資訊——孔數／珠子等級是每一件各自不同的，表格裡查不到。
            string title = d.Name;
            if (st.Inst != null)
            {
                if (st.Inst.HasSockets && st.Inst.UnlockedCount > 0)
                    title += $"（{st.Inst.UnlockedCount} 孔）";
                else if (st.Inst.level > 0)
                    title += $"  Lv{st.Inst.level}";
            }
            _name.text = title;

            string stats = BuildStats(d, st.Inst);
            if (!string.IsNullOrEmpty(extraStats))
                stats += (stats.Length > 0 ? "\n" : "") + extraStats;
            _stats.text = stats;
            _stats.gameObject.SetActive(!string.IsNullOrEmpty(stats));

            _lore.text = d.TipLore;
            _lore.gameObject.SetActive(!string.IsNullOrEmpty(d.TipLore));

            // 大圖預覽：走 ItemIcons 這個唯一入口，所以血統藥劑的角標、珠子的能力符號都自動對。
            bool wantPreview = PreviewAllItems || d.IsBloodline;
            _previewBox.gameObject.SetActive(wantPreview);
            if (wantPreview) ItemIcons.Apply(_preview, st);

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            // ContentSizeFitter 要重算過，Follow 才讀得到正確高度（否則第一幀會用上一件的高度去夾制，位置閃一下）。
            LayoutRebuilder.ForceRebuildLayoutImmediate(_root);
            Follow();
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        // ══════════════════ 定位 ══════════════════

        /// <summary>
        /// 跟著游標。**一律開在游標上方**（位置要統一、不要隨游標高低換方向）：
        /// pivot 的 y 固定在下緣，`anchoredPosition.y` 就是 tooltip 的底端，再夾制頂端不超出面板上緣
        /// ——畫面上半 hover 時會整個往下壓，**不翻方向**，因為翻方向正是「tooltip 在游標上下跳」的來源。
        /// 左右則照舊：游標在右半邊就往左開。
        ///
        /// 面板的 `Update()` 每幀呼叫；沒顯示時直接跳出。
        /// </summary>
        public void Follow()
        {
            if (!Visible) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _panelRect, Input.mousePosition, null, out Vector2 local))
                return;

            var r = _panelRect.rect;                  // 以面板中心為原點
            float w = _root.rect.width, h = _root.rect.height;

            bool right = local.x > 0f;
            _root.pivot = new Vector2(right ? 1f : 0f, 0f);   // y=0 ＝ 以下緣定位 ⇒ 一律長在游標上方

            float x = local.x + (right ? -GapX : GapX);
            x = right ? Mathf.Clamp(x, r.xMin + w, r.xMax) : Mathf.Clamp(x, r.xMin, r.xMax - w);

            float y = local.y + GapY;                 // pivot 在下緣，所以 y 就是 tooltip 的底端
            if (y + h > r.yMax) y = r.yMax - h;       // 頂端會超出上緣 → 整個往下壓（維持在游標上方的語意）
            y = Mathf.Max(y, r.yMin);                 // 面板真的不夠高就貼齊下緣

            _root.anchoredPosition = new Vector2(x, y);
        }

        // ══════════════════ 內文 ══════════════════

        /// <summary>
        /// tooltip 上半：表格寫死的說明 ＋「這一件」的鑲嵌內容。
        /// 能力珠會顯示它這一級實際給多少（直接查 GemTable，不用另外維護一份文案）。
        /// </summary>
        static string BuildStats(ItemData d, ItemInstance inst)
        {
            var sb = new System.Text.StringBuilder(d.TipStats ?? "");

            // 能力珠：這一顆這一級給多少
            if (d.IsGem && inst != null)
            {
                var gd = ItemManager.Gems.Get(d.GemID);
                if (gd != null)
                {
                    if (sb.Length > 0) sb.Append('\n');
                    float v = gd.ValueAt(inst.level);
                    string val = gd.IsPercent ? $"{(v >= 0 ? "+" : "")}{(v * 100f):0.#}%" : $"{(v >= 0 ? "+" : "")}{v:0.##}";
                    sb.Append($"Lv{inst.level}：{gd.Name} {val}");
                }
            }

            // 裝備：列出目前鑲了什麼；對參考武器（這件是武器→它自己；防具→目前裝備的武器）沒效果的珠子標出來
            if (inst != null && inst.HasSockets && inst.UnlockedCount > 0)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append($"鑲嵌 {inst.GemCount}/{inst.UnlockedCount}");
                var inv = InventorySystem.Instance;
                var refW = GemEffectiveness.ReferenceWeapon(d.WeaponID > 0 ? d.ID : 0);
                for (int i = 0; i < inst.sockets.Count; i++)
                {
                    var g = inst.GemAt(i);
                    if (g == null) continue;
                    var gemItem = inv != null ? inv.GetData(g.itemId) : null;
                    sb.Append('\n').Append("　・").Append(gemItem != null ? gemItem.Name : $"#{g.itemId}").Append(" Lv").Append(g.level);
                    if (refW != null && !GemEffectiveness.IsEffective(g, refW))
                        sb.Append(Language.GetText(TxtGemIneffectiveMark));
                }
            }
            return sb.ToString();
        }
    }
}
