using UnityEngine;
using UnityEngine.UI;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 背包面板（呈現層）。背景用 inventoryPanelBG.png 整張當底，依量到的像素座標在上面疊互動格子，
    /// 從 InventorySystem 讀資料繪 icon。座標都在「背景原圖像素空間」(1122x1402)，整個 frame 等比縮放塞進畫面。
    ///
    /// v1 互動：點道具格中的可裝備物品 → 裝備；點裝備欄 → 卸下；移入顯示高亮 + 底部欄顯示名稱 + 浮動 tooltip
    /// （上半 TipStats 正楷、下半 TipLore 斜體）。拖放重排等屬後續，見 readme/INVENTORY.md。
    /// </summary>
    public class InventoryPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool ShowBackdrop => true;   // 遮罩由 UIManager 統一鋪在所有視窗最底層（一層、不蓋面板）
        public override bool CloseOnEscape => true;

        // ── 背景原圖尺寸 ──
        const float BgW = 1122f, BgH = 1402f;

        // ── 道具格（量自背景）──
        const float GridLeft = 466f, GridPitchX = 80.857f, GridTop = 280f, GridPitchY = 87.111f;
        const float ItemIconSize = 70f;

        // ── 裝備欄中心（量自背景）──
        static readonly float[] EquipColX = { 152f, 308.5f };   // 左欄 / 右欄
        static readonly float[] EquipRowY = { 410f, 705f, 1000f };
        const float EquipBoxW = 128f, EquipBoxH = 195f, EquipIconSize = 120f;

        // ── 底部名稱列 ──
        const float NameBarX = 660f, NameBarY = 1272f, NameBarW = 360f, NameBarH = 54f;

        [Tooltip("面板顯示高度（CanvasScaler 參考單位，1080 為滿版）。")]
        public float displayHeight = 1040f;

        const float PairRightX = 420f;   // 與倉庫並排時，背包置於右側（與 StoragePanel.PairLeftX 對稱）

        RectTransform _frame;
        Image _highlight;
        InventorySlotWidget[] _gridSlots;
        InventorySlotWidget[] _equipSlots;

        // ── tooltip ──
        const float TooltipWidth = 460f;
        RectTransform _tooltip;
        Text _tipName, _tipStats, _tipLore;

        // 裝備欄順序（對齊背景版面）：左上→右上→左中→右中→左下→右下
        static readonly EquipSlot[] EquipOrder =
        {
            EquipSlot.Weapon, EquipSlot.Chest,
            EquipSlot.Boots,  EquipSlot.Gloves,
            EquipSlot.Amulet, EquipSlot.Ring,
        };

        protected override void OnBuild()
        {
            // frame：原圖尺寸、置中、等比縮放塞進畫面
            var frameGO = UIBuilder.Create("Frame", transform);
            _frame = UIBuilder.Rect(frameGO);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.anchoredPosition = Vector2.zero;
            _frame.sizeDelta = new Vector2(BgW, BgH);
            float scale = displayHeight / BgH;
            _frame.localScale = new Vector3(scale, scale, 1f);

            // 背景
            var bg = UIBuilder.Image(frameGO.transform, "BG", UIBuilder.LoadSprite("UI/InventoryPanel/inventoryPanelBG"));
            UIBuilder.Stretch(bg.rectTransform);
            bg.raycastTarget = true;   // 吃掉空白處點擊（不穿到遊戲）

            // 裝備欄
            _equipSlots = new InventorySlotWidget[EquipOrder.Length];
            for (int i = 0; i < EquipOrder.Length; i++)
            {
                float cx = EquipColX[i % 2];
                float cy = EquipRowY[i / 2];
                var w = MakeSlot($"Equip_{EquipOrder[i]}", cx, cy, EquipBoxW, EquipBoxH, EquipIconSize,
                                 InventorySlotWidget.Kind.Equip);
                w.equipSlot = EquipOrder[i];
                _equipSlots[i] = w;
            }

            // 道具格 7x9
            _gridSlots = new InventorySlotWidget[InventorySystem.GridCount];
            for (int r = 0; r < InventorySystem.Rows; r++)
            {
                for (int c = 0; c < InventorySystem.Columns; c++)
                {
                    int idx = r * InventorySystem.Columns + c;
                    float cx = GridLeft + (c + 0.5f) * GridPitchX;
                    float cy = GridTop + (r + 0.5f) * GridPitchY;
                    var w = MakeSlot($"Cell_{idx}", cx, cy, GridPitchX, GridPitchY, ItemIconSize,
                                     InventorySlotWidget.Kind.Grid);
                    w.index = idx;
                    _gridSlots[idx] = w;
                }
            }

            // 高亮（重用一個，移入時貼到該格後方）
            _highlight = UIBuilder.Image(frameGO.transform, "Highlight", null, new Color(1f, 0.82f, 0.3f, 0.22f));
            _highlight.raycastTarget = false;
            _highlight.gameObject.SetActive(false);

            BuildRefreshButton();
            BuildTooltip();
        }

        // ── 重整（整理道具格）按鈕：中心位置（底圖原生像素，左上為原點，X→右、Y→下）。往上調小 RefreshCy、往右調大 RefreshCx。
        const float RefreshCx = 870f, RefreshCy = 1240f, RefreshSize = 120f;
        const string RefreshResDir = "UI/StoragePanel/";   // 沿用倉庫那組按鈕素材

        void BuildRefreshButton()
        {
            Sprite R(string n) => Resources.Load<Sprite>(RefreshResDir + n);
            var rb = UIBuilder.Button(_frame, "Refresh", "",
                                      () => InventorySystem.Instance.SortGrid(), Color.white, R("RefreshBG_normal"));
            rb.targetGraphic = rb.GetComponent<Image>();
            rb.transition = Selectable.Transition.SpriteSwap;
            var ss = rb.spriteState;
            ss.pressedSprite = R("RefreshBG_pressed");
            ss.highlightedSprite = R("RefreshBG_normal");
            ss.selectedSprite = R("RefreshBG_normal");
            rb.spriteState = ss;
            Place((RectTransform)rb.transform, RefreshCx, RefreshCy, RefreshSize, RefreshSize);

            var ic = UIBuilder.Image(rb.transform, "Icon", R("RefreshIcon"));
            ic.preserveAspect = true; ic.raycastTarget = false;
            UIBuilder.Stretch(ic.rectTransform, 26, 26, 26, 26);
        }

        /// <summary>建浮動 tooltip：掛在 panel root（不受 frame 縮放），上半正楷功能、下半斜體劇情，高度自動。</summary>
        void BuildTooltip()
        {
            var go = UIBuilder.Create("Tooltip", transform);   // panel root，不在 frame 底下
            _tooltip = UIBuilder.Rect(go);
            _tooltip.anchorMin = _tooltip.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltip.pivot = new Vector2(0f, 1f);
            _tooltip.sizeDelta = new Vector2(TooltipWidth, 10f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.07f, 0.96f);
            bg.raycastTarget = false;   // tooltip 跟著游標，絕不能擋住 hover 事件

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(18, 18, 14, 14);
            vlg.spacing = 8;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;   // 寬固定
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;     // 高自動

            _tipName = UIBuilder.Text(go.transform, "Name", "", 26, new Color(1f, 0.85f, 0.45f), TextAnchor.UpperLeft);
            _tipName.fontStyle = FontStyle.Bold;
            _tipStats = UIBuilder.Text(go.transform, "Stats", "", 22, new Color(0.92f, 0.92f, 0.95f), TextAnchor.UpperLeft);
            _tipStats.fontStyle = FontStyle.Normal;   // 正楷
            _tipLore = UIBuilder.Text(go.transform, "Lore", "", 20, new Color(0.72f, 0.69f, 0.62f), TextAnchor.UpperLeft);
            _tipLore.fontStyle = FontStyle.Italic;    // 斜體

            go.SetActive(false);
        }

        InventorySlotWidget MakeSlot(string name, float px, float py, float w, float h,
                                     float iconSize, InventorySlotWidget.Kind kind)
        {
            var go = UIBuilder.Create(name, _frame);
            Place(UIBuilder.Rect(go), px, py, w, h);

            var hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);   // 透明命中區
            hit.raycastTarget = true;

            var icon = UIBuilder.Image(go.transform, "Icon", null, Color.white);
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(iconSize, iconSize);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;

            var count = UIBuilder.Text(go.transform, "Count", "", 20, Color.white, TextAnchor.LowerRight);
            UIBuilder.Stretch(count.rectTransform, 0, 6, 0, 4);

            var widget = go.AddComponent<InventorySlotWidget>();
            widget.kind = kind;
            widget.icon = icon;
            widget.count = count;
            widget.Clicked = OnSlotClicked;
            widget.Entered = OnSlotEnter;
            widget.Exited = OnSlotExit;
            return widget;
        }

        /// <summary>把 RectTransform 放到背景像素座標 (px,py)（左上為原點、y 向下），中心對齊、給定寬高。</summary>
        void Place(RectTransform rt, float px, float py, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // 錨到 frame 左上角
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(px, -py);
            rt.sizeDelta = new Vector2(w, h);
        }

        protected override void OnOpen()
        {
            var inv = InventorySystem.Instance;
            inv.OnChanged += Redraw;
            Redraw();
        }

        protected override void OnClose()
        {
            if (InventorySystem.Instance != null)
                InventorySystem.Instance.OnChanged -= Redraw;
            ClearHighlight();
            HideTooltip();
        }

        void Redraw()
        {
            var inv = InventorySystem.Instance;

            for (int i = 0; i < _gridSlots.Length; i++)
            {
                var st = inv.GetGrid(i);
                SetSlotVisual(_gridSlots[i], st.ItemId, st.Count);
            }
            for (int i = 0; i < _equipSlots.Length; i++)
            {
                int id = inv.GetEquipped(_equipSlots[i].equipSlot);
                SetSlotVisual(_equipSlots[i], id, id > 0 ? 1 : 0);
            }
        }

        void SetSlotVisual(InventorySlotWidget w, int itemId, int count)
        {
            var d = (itemId > 0) ? InventorySystem.Instance.GetData(itemId) : null;
            if (d != null && d.Icon != null)
            {
                w.icon.sprite = d.Icon;
                w.icon.enabled = true;
            }
            else
            {
                w.icon.sprite = null;
                w.icon.enabled = false;
            }
            if (w.count != null) w.count.text = (count > 1) ? count.ToString() : "";
        }

        // ── 互動 ──

        void OnSlotClicked(InventorySlotWidget w)
        {
            var inv = InventorySystem.Instance;
            if (w.kind == InventorySlotWidget.Kind.Grid)
            {
                var portal = ScriptsPanel.ActiveGridIfOpen();
                var store = StoragePanel.ActivePageIfOpen();
                int clickedId = inv.GetGrid(w.index).ItemId;
                var clickedData = clickedId > 0 ? inv.GetData(clickedId) : null;
                if (portal != null && clickedData != null && clickedData.IsScript)
                    InventoryActions.QuickMoveGrid(w, portal);  // 傳送門開著：點劇本 → 送進傳送門方框
                else if (store != null)
                    InventoryActions.QuickMoveGrid(w, store);   // 倉庫開著：點一下送進倉庫當前分頁
                else
                    inv.EquipFromGrid(w.index);                  // 否則維持原本：點可裝備物品 → 裝備
            }
            else
            {
                inv.Unequip(w.equipSlot);
            }
        }

        /// <summary>找出目前放著某道具的背包格 RectTransform（給新手教學手指/遮罩指向）；找不到回 null。</summary>
        public RectTransform FindGridSlotRect(int itemId)
        {
            var inv = InventorySystem.Instance;
            if (inv == null || _gridSlots == null) return null;
            for (int i = 0; i < _gridSlots.Length; i++)
                if (_gridSlots[i] != null && inv.GetGrid(i).ItemId == itemId)
                    return (RectTransform)_gridSlots[i].transform;
            return null;
        }

        /// <summary>並排（右移）或單獨（置中）。由 StorageBagCoordinator 呼叫。</summary>
        public void SetPairedLayout(bool paired)
        {
            if (_frame != null) _frame.anchoredPosition = new Vector2(paired ? PairRightX : 0f, 0f);
        }

        void OnSlotEnter(InventorySlotWidget w)
        {
            // 高亮貼到該格、壓在 icon 之後
            _highlight.transform.SetParent(w.transform, false);
            UIBuilder.Stretch(_highlight.rectTransform);
            _highlight.transform.SetAsFirstSibling();
            _highlight.gameObject.SetActive(true);

            var inv = InventorySystem.Instance;
            int id = (w.kind == InventorySlotWidget.Kind.Grid)
                ? inv.GetGrid(w.index).ItemId
                : inv.GetEquipped(w.equipSlot);
            ShowTooltip(id);
        }

        void OnSlotExit(InventorySlotWidget w)
        {
            ClearHighlight();
            HideTooltip();
        }

        void ClearHighlight()
        {
            if (_highlight == null) return;
            _highlight.gameObject.SetActive(false);
            _highlight.transform.SetParent(_frame, false);
        }

        // ── tooltip ──

        void ShowTooltip(int itemId)
        {
            var d = (itemId > 0) ? InventorySystem.Instance.GetData(itemId) : null;
            if (d == null) { HideTooltip(); return; }

            _tipName.text = d.Name;
            _tipStats.text = d.TipStats;
            _tipStats.gameObject.SetActive(!string.IsNullOrEmpty(d.TipStats));
            _tipLore.text = d.TipLore;
            _tipLore.gameObject.SetActive(!string.IsNullOrEmpty(d.TipLore));

            _tooltip.gameObject.SetActive(true);
            _tooltip.SetAsLastSibling();
            PositionTooltip();
        }

        void HideTooltip()
        {
            if (_tooltip != null) _tooltip.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_tooltip != null && _tooltip.gameObject.activeSelf) PositionTooltip();
        }

        /// <summary>tooltip 跟著游標；游標在右半邊就翻到左側顯示，避免超出畫面。</summary>
        void PositionTooltip()
        {
            var panelRect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    panelRect, Input.mousePosition, null, out Vector2 local))
                return;

            bool right = local.x > 0f;
            _tooltip.pivot = new Vector2(right ? 1f : 0f, 1f);
            float ox = right ? -18f : 18f;
            _tooltip.anchoredPosition = local + new Vector2(ox, -18f);
        }
    }
}
