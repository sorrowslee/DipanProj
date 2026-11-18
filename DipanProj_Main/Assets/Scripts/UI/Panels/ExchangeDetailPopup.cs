using UnityEngine;
using UnityEngine.UI;
using Dipan.Exchange;
using Dipan.Localization;

namespace Dipan.UI
{
    /// <summary>
    /// 兌換所的「商品詳情＋兌換確認」彈窗（點兌換所的任一品項開啟）。
    /// 版面：上方標題帶寫物品名（稀有度色）；左邊大圖＋稀有度／分類／持有數；右邊說明、單價、數量、合計、兌換後剩餘、按鈕。
    /// 底圖沿用兌換所的 ExchangePanel_Bg（縮小顯示），座標一樣是底圖原生像素（1483×955，左上為原點）。
    /// </summary>
    public class ExchangeDetailPopup : UIPanel
    {
        public override UILayer Layer => UILayer.Popup;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool ShowBackdrop => false;   // Popup 層自己畫暗底（同 ConfirmPopup）
        public override bool CloseOnEscape => true;

        const float FrameW = 1483f, FrameH = 955f, FrameScale = 0.74f;
        const float HeadY = 72f, HeadH = 110f;

        static readonly Color Gold = new Color(1f, 0.85f, 0.45f);
        static readonly Color TextLight = new Color(0.92f, 0.9f, 0.85f);
        static readonly Color TextDim = new Color(0.62f, 0.6f, 0.55f);
        static readonly Color TextLore = new Color(0.72f, 0.69f, 0.62f);
        static readonly Color TextBad = new Color(0.88f, 0.38f, 0.32f);

        // 語言表（兌換系統段）
        const int TxtExchange = 10035, TxtCancel = 10036, TxtUnit = 10037, TxtQty = 10038, TxtTotal = 10039,
                  TxtAfter = 10040, TxtOwned = 10041, TxtMax = 10043, TxtFavAdd = 10044, TxtFavDel = 10045, TxtNotOpen = 10049;

        ExchangeEntry _entry;
        int _qty = 1;

        RectTransform _frame;
        Text _name, _sub, _owned, _stats, _lore, _unit, _qtyText, _total, _after, _reason;
        Image _icon, _rarityBar;
        RectTransform _rarityOutline;
        Button _buyBtn;
        Image _favImg;

        /// <summary>開某個品項的詳情。</summary>
        public static void Show(ExchangeEntry e)
        {
            if (e == null || UIManager.Instance == null) return;
            var p = UIManager.Instance.Open<ExchangeDetailPopup>();
            if (p != null) p.Bind(e);
        }

        protected override void OnBuild()
        {
            var dim = UIBuilder.SolidPanel(transform, "Dim", new Color(0f, 0f, 0f, 0.6f));
            dim.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>();   // 點彈窗外面＝取消
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Cancel);

            var tile = Resources.Load<Sprite>("UI/ExchangePanel/ExchangePanel_Tile");
            var field = Resources.Load<Sprite>("UI/ExchangePanel/ExchangePanel_Field");

            var bg = Resources.Load<Sprite>("UI/ExchangePanel/ExchangePanel_Bg");
            var frameImg = UIBuilder.Image(transform, "Frame", bg, bg != null ? Color.white : new Color(0.09f, 0.075f, 0.05f, 1f));
            frameImg.raycastTarget = true;
            _frame = frameImg.rectTransform;
            UIBuilder.Center(_frame, FrameW, FrameH);
            _frame.localScale = new Vector3(FrameScale, FrameScale, 1f);

            // 標題帶：物品名
            _name = ExchangePanel.Txt(_frame, "Name", "", 60, Gold, TextAnchor.MiddleCenter);
            _name.fontStyle = FontStyle.Bold;
            _name.resizeTextForBestFit = true; _name.resizeTextMinSize = 36; _name.resizeTextMaxSize = 60;
            _name.verticalOverflow = VerticalWrapMode.Truncate;
            ExchangePanel.PlaceTL(_name.rectTransform, 160f, HeadY + 6f, FrameW - 320f, HeadH - 12f);

            // 左：大圖
            var box = ExchangePanel.Sliced(_frame, "IconBox", tile);
            box.raycastTarget = false;
            ExchangePanel.PlaceTL(box.rectTransform, 90f, 230f, 420f, 420f);
            _rarityBar = UIBuilder.Image(box.transform, "RarityBar", null, Color.white);
            _rarityBar.raycastTarget = false;
            ExchangePanel.PlaceTL(_rarityBar.rectTransform, 18f, 14f, 384f, 6f);
            _rarityOutline = SlotOutline.Create(box.transform, "Rarity", Color.white, 3f);
            UIBuilder.Stretch(_rarityOutline, 5f, 5f, 5f, 5f);
            _icon = UIBuilder.Image(box.transform, "Icon", null, Color.white);
            _icon.raycastTarget = false;
            ExchangePanel.PlaceCenter(_icon.rectTransform, 40f, 40f, 340f, 340f);   // IconFit 需要置中 pivot

            _sub = ExchangePanel.Txt(_frame, "Sub", "", 30, TextLight, TextAnchor.MiddleCenter);
            ExchangePanel.PlaceTL(_sub.rectTransform, 90f, 668f, 420f, 44f);
            _owned = ExchangePanel.Txt(_frame, "Owned", "", 26, TextDim, TextAnchor.MiddleCenter);
            ExchangePanel.PlaceTL(_owned.rectTransform, 90f, 714f, 420f, 38f);

            // 收藏（左下）
            var fav = UIBuilder.Button(_frame, "Fav", "", ToggleFav, Color.white, Resources.Load<Sprite>("UI/ExchangePanel/ExchangePanel_Icon_Star"));
            _favImg = fav.GetComponent<Image>();
            _favImg.preserveAspect = true;
            fav.targetGraphic = _favImg;
            ExchangePanel.PlaceTL((RectTransform)fav.transform, 270f, 772f, 60f, 60f);
            HoverTip.Attach(fav.gameObject, () => Language.GetText(ExchangeService.IsFavorite(_entry) ? TxtFavDel : TxtFavAdd));

            // 右：說明
            _stats = ExchangePanel.Txt(_frame, "Stats", "", 30, TextLight, TextAnchor.UpperLeft);
            _stats.resizeTextForBestFit = true; _stats.resizeTextMinSize = 20; _stats.resizeTextMaxSize = 30;
            _stats.verticalOverflow = VerticalWrapMode.Truncate;
            ExchangePanel.PlaceTL(_stats.rectTransform, 570f, 228f, 820f, 190f);
            _lore = ExchangePanel.Txt(_frame, "Lore", "", 26, TextLore, TextAnchor.UpperLeft);
            _lore.fontStyle = FontStyle.Italic;
            _lore.resizeTextForBestFit = true; _lore.resizeTextMinSize = 18; _lore.resizeTextMaxSize = 26;
            _lore.verticalOverflow = VerticalWrapMode.Truncate;
            ExchangePanel.PlaceTL(_lore.rectTransform, 570f, 428f, 820f, 120f);

            // 右下：價格與數量
            Label(TxtUnit, 570f, 570f);
            _unit = ExchangePanel.Txt(_frame, "Unit", "", 32, Gold, TextAnchor.MiddleLeft);
            ExchangePanel.PlaceTL(_unit.rectTransform, 740f, 570f, 650f, 50f);

            Label(TxtQty, 570f, 630f);
            SmallButton("Minus", "－", 740f, 630f, 60f, field, () => SetQty(_qty - 1));
            _qtyText = ExchangePanel.Txt(_frame, "Qty", "1", 34, TextLight, TextAnchor.MiddleCenter);
            ExchangePanel.PlaceTL(_qtyText.rectTransform, 806f, 630f, 100f, 50f);
            SmallButton("Plus", "＋", 912f, 630f, 60f, field, () => SetQty(_qty + 1));
            SmallButton("Max", Language.GetText(TxtMax), 990f, 630f, 110f, field, () => SetQty(ExchangeService.MaxAffordable(_entry)));

            Label(TxtTotal, 570f, 690f);
            _total = ExchangePanel.Txt(_frame, "Total", "", 34, Gold, TextAnchor.MiddleLeft);
            ExchangePanel.PlaceTL(_total.rectTransform, 740f, 690f, 650f, 50f);

            Label(TxtAfter, 570f, 740f);
            _after = ExchangePanel.Txt(_frame, "After", "", 26, TextDim, TextAnchor.MiddleLeft);
            ExchangePanel.PlaceTL(_after.rectTransform, 740f, 740f, 650f, 44f);

            // 按鈕：兌換（深緋紅漆）／取消（深石）
            _buyBtn = TextButton("Buy", Language.GetText(TxtExchange), 760f, 800f, 300f, 80f, "UI/InventoryPanel/inventoryPanel_Cell_Selected", Buy);
            TextButton("Cancel", Language.GetText(TxtCancel), 1090f, 800f, 270f, 80f, "UI/InventoryPanel/inventoryPanel_Cell_UnSelected", Cancel);

            _reason = ExchangePanel.Txt(_frame, "Reason", "", 24, TextBad, TextAnchor.MiddleCenter);
            ExchangePanel.PlaceTL(_reason.rectTransform, 760f, 880f, 600f, 30f);
        }

        void Label(int langId, float x, float y)
        {
            var t = ExchangePanel.Txt(_frame, "L" + langId, Language.GetText(langId), 28, TextDim, TextAnchor.MiddleLeft);
            ExchangePanel.PlaceTL(t.rectTransform, x, y, 160f, 50f);
        }

        void SmallButton(string name, string label, float x, float y, float w, Sprite field, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIBuilder.Button(_frame, name, "", onClick, Color.white, field);
            var img = b.GetComponent<Image>();
            if (field != null) img.type = Image.Type.Sliced; else img.color = new Color(0.12f, 0.1f, 0.07f, 1f);
            b.targetGraphic = img;
            ExchangePanel.PlaceTL((RectTransform)b.transform, x, y, w, 50f);
            var t = ExchangePanel.Txt(b.transform, "Label", label, 28, Gold, TextAnchor.MiddleCenter);
            UIBuilder.Stretch(t.rectTransform);
        }

        Button TextButton(string name, string label, float x, float y, float w, float h, string sprite, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIBuilder.Button(_frame, name, "", onClick, Color.white, UIBuilder.LoadSprite(sprite));
            var img = b.GetComponent<Image>();
            img.preserveAspect = false;
            b.targetGraphic = img;
            ExchangePanel.PlaceTL((RectTransform)b.transform, x, y, w, h);
            var t = ExchangePanel.Txt(b.transform, "Label", label, 34, new Color(0.96f, 0.92f, 0.82f), TextAnchor.MiddleCenter);
            t.fontStyle = FontStyle.Bold;
            UIBuilder.Stretch(t.rectTransform);
            return b;
        }

        // ══════════════════ 資料 ══════════════════

        void Bind(ExchangeEntry e)
        {
            _entry = e;
            var rc = RarityInfo.ColorOf(e.Rarity);
            _name.text = e.Name;
            _name.color = rc;
            _rarityBar.color = rc;
            SlotOutline.SetColor(_rarityOutline, rc);
            ItemIcons.Apply(_icon, e.ItemId);
            _sub.text = $"<color=#{ColorUtility.ToHtmlStringRGB(rc)}>{e.RarityLabel}</color>　{ExchangeCategoryInfo.Name(e.Category)}";

            string stats = e.Item.TipStats;
            if (string.IsNullOrEmpty(stats)) stats = e.Item.Description;
            _stats.text = stats ?? "";
            _lore.text = e.Item.TipLore ?? "";

            _unit.text = e.CurrenciesAvailable ? ExchangeService.CostText(e.Cost) : Language.GetText(TxtNotOpen);
            _qty = 1;
            RefreshNumbers();
        }

        void SetQty(int q)
        {
            _qty = Mathf.Clamp(q, 1, ExchangeService.MaxQuantity);
            RefreshNumbers();
        }

        void RefreshNumbers()
        {
            if (_entry == null) return;
            var inv = Dipan.Inventory.InventorySystem.Instance;
            int owned = inv != null ? inv.CountOf(_entry.ItemId) : 0;
            _owned.text = string.Format(Language.GetText(TxtOwned), owned);
            _favImg.color = ExchangeService.IsFavorite(_entry) ? Color.white : new Color(0.35f, 0.32f, 0.28f, 0.85f);
            _qtyText.text = _qty.ToString();

            if (!_entry.CurrenciesAvailable)
            {
                _total.text = "—"; _after.text = "";
                _reason.text = Language.GetText(TxtNotOpen);
                _buyBtn.interactable = false;
                return;
            }

            var total = ExchangeService.TotalCost(_entry, _qty);
            bool ok = ExchangeService.CanAfford(_entry, _qty, out string why);
            _total.text = ExchangeService.CostText(total);
            _total.color = ok ? Gold : TextBad;

            var after = new System.Text.StringBuilder();
            for (int i = 0; i < total.Count; i++)
            {
                var c = total[i].Currency;
                if (i > 0) after.Append("＋");
                after.Append((c.HeldNow - total[i].Amount).ToString("N0")).Append(' ').Append(c.Name);
            }
            _after.text = after.ToString();
            _after.color = ok ? TextDim : TextBad;
            _reason.text = ok ? "" : why;
            _buyBtn.interactable = ok;
        }

        // ══════════════════ 操作 ══════════════════

        void Buy()
        {
            if (_entry == null) return;
            if (!ExchangeService.Buy(_entry, _qty, out string why))
            {
                _reason.text = why;
                return;
            }
            var panel = UIManager.Instance != null ? UIManager.Instance.Get<ExchangePanel>() : null;
            UIManager.Instance.Close(this);
            if (panel != null && panel.IsOpen) panel.NotifyChanged();
        }

        void ToggleFav()
        {
            if (_entry == null) return;
            ExchangeService.ToggleFavorite(_entry);
            RefreshNumbers();
            var panel = UIManager.Instance != null ? UIManager.Instance.Get<ExchangePanel>() : null;
            if (panel != null && panel.IsOpen) panel.NotifyChanged();
        }

        void Cancel()
        {
            if (UIManager.Instance != null) UIManager.Instance.Close(this);
        }
    }
}
