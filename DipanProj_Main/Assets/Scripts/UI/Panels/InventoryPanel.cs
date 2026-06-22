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
        public override bool ShowBackdrop => true;
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

        RectTransform _frame;
        Image _highlight;
        Text _nameLabel;
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

            // 底部名稱列
            _nameLabel = UIBuilder.Text(frameGO.transform, "NameLabel", "", 28,
                                        new Color(0.95f, 0.9f, 0.78f), TextAnchor.MiddleCenter);
            Place(_nameLabel.rectTransform, NameBarX, NameBarY, NameBarW, NameBarH);

            BuildTooltip();
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
                inv.EquipFromGrid(w.index);    // 可裝備才會生效，否則 no-op
            else
                inv.Unequip(w.equipSlot);
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
            var d = (id > 0) ? inv.GetData(id) : null;
            _nameLabel.text = (d != null) ? d.Name : "";

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
            if (_nameLabel != null) _nameLabel.text = "";
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
