using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 倉庫面板：5 分頁（StoragePanelBG 外框＋真素材頁籤/重整鈕）。單獨開啟時置中；背包也開時左移、與背包並排，
    /// 兩邊用點擊（送到對側）與拖放（SlotDragController，跨面板互通）互搬。座標皆為底圖原生像素(1123×1401)，
    /// 外框用 localScale 縮放、子物件跟著縮。見 readme/STORAGE.md。
    /// </summary>
    public class StoragePanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool ShowBackdrop => true;   // 遮罩由 UIManager 統一鋪在所有視窗最底層（一層、不蓋面板）

        const string ResDir = "UI/StoragePanel/";

        // ── 底圖原生座標（量自 StoragePanelBG）──
        const float FrameW = 1123f, FrameH = 1401f;
        const float GridX0 = 167f, GridY0 = 403f;
        const float CellW = 84.8f, CellH = 85f;
        const int StoreCols = 10, StoreRows = 10;
        static readonly float[] TabCx = { 252f, 421f, 591f, 760f, 930f };
        const float TabCy = 350f, TabW = 156f, TabH = 104f;
        // 重整鈕「中心」位置（底圖原生像素，左上為原點，X→右、Y→下）。要往上就調小 RefreshCy、往右就調大 RefreshCx。
        const float RefreshCx = 765f, RefreshCy = 1250f, RefreshSize = 130f;

        // ── 擺位 ──
        const float FrameScale = 0.72f;
        const float SoloX = 0f;          // 單獨開啟：置中
        // 與背包並排：左移。值是用「看得見的美術」算的（底圖 1122 裡不透明內容是 x 52~1070，
        // 左右各約 52px 透明留白），讓兩邊美術中間只留約 40 單位的縫。見 InventoryPanel.PairRightX 的註解。
        const float PairLeftX = -416f;

        Sprite _cellNormal, _cellPressed;
        Sprite[] _cellNum;

        int _page;
        IItemGrid _activeStore;
        public IItemGrid ActiveStore => _activeStore;

        RectTransform _frame;
        RectTransform _storeHolder;
        readonly List<Image> _tabImages = new List<Image>();
        readonly List<ItemSlotWidget> _storeSlots = new List<ItemSlotWidget>();

        // hover 高亮 + tooltip（行為比照背包）
        const float TooltipWidth = 460f;
        RectTransform _highlight;   // hover 外框（與背包同一套，見 UI/SlotOutline.cs）
        RectTransform _tooltip;
        Text _tipName, _tipStats, _tipLore;

        static Sprite L(string n) => Resources.Load<Sprite>(ResDir + n);

        /// <summary>倉庫面板若已開啟，回傳當前分頁（給背包點擊「送到倉庫」用）；否則 null。</summary>
        public static IItemGrid ActivePageIfOpen()
        {
            var ui = UIManager.Instance;
            if (ui == null) return null;
            var p = ui.Get<StoragePanel>();
            return (p != null && p.IsOpen) ? p._activeStore : null;
        }

        protected override void OnBuild()
        {
            _cellNormal = L("CellBG_normal");
            _cellPressed = L("CellBG_pressed");
            _cellNum = new[] { L("Cell_number_1"), L("Cell_number_2"), L("Cell_number_3"), L("Cell_number_4"), L("Cell_number_5") };

            var bg = L("StoragePanelBG");
            var frameImg = UIBuilder.Image(transform, "StorageFrame", bg,
                                           bg != null ? Color.white : new Color(0.1f, 0.1f, 0.13f, 0.97f));
            _frame = frameImg.rectTransform;
            UIBuilder.Center(_frame, FrameW, FrameH, new Vector2(SoloX, 0));
            _frame.localScale = new Vector3(FrameScale, FrameScale, 1f);

            for (int i = 0; i < TabCx.Length; i++)
            {
                int page = i;
                var b = UIBuilder.Button(_frame, $"Tab{i + 1}", "", () => SetPage(page), Color.white, _cellNormal);
                var bimg = b.GetComponent<Image>();
                b.targetGraphic = bimg;
                PlaceTL((RectTransform)b.transform, TabCx[i] - TabW / 2f, TabCy - TabH / 2f, TabW, TabH);
                if (i < _cellNum.Length && _cellNum[i] != null)
                {
                    var num = UIBuilder.Image(b.transform, "Num", _cellNum[i]);
                    num.preserveAspect = true; num.raycastTarget = false;
                    UIBuilder.Stretch(num.rectTransform, 34, 34, 16, 16);
                }
                _tabImages.Add(bimg);
            }

            var rb = UIBuilder.Button(_frame, "Refresh", "", SortActivePage, Color.white, L("RefreshBG_normal"));
            rb.targetGraphic = rb.GetComponent<Image>();
            rb.transition = Selectable.Transition.SpriteSwap;
            var ss = rb.spriteState;
            ss.pressedSprite = L("RefreshBG_pressed");
            ss.highlightedSprite = L("RefreshBG_normal");
            ss.selectedSprite = L("RefreshBG_normal");
            rb.spriteState = ss;
            PlaceTL((RectTransform)rb.transform, RefreshCx - RefreshSize / 2f, RefreshCy - RefreshSize / 2f, RefreshSize, RefreshSize);
            var ic = UIBuilder.Image(rb.transform, "Icon", L("RefreshIcon"));
            ic.preserveAspect = true; ic.raycastTarget = false;
            UIBuilder.Stretch(ic.rectTransform, 30, 30, 30, 30);

            _storeHolder = MakeHolder(_frame, "StoreGrid", GridX0, GridY0);

            // hover 外框（重用一個，移入時貼到該格）。細線而不是整片上色——理由見 UI/SlotOutline.cs。
            _highlight = SlotOutline.Create(_frame, "HoverOutline", new Color(1f, 0.88f, 0.55f, 0.85f), 3.5f);
            _highlight.gameObject.SetActive(false);

            BuildTooltip();
        }

        /// <summary>浮動 tooltip（與背包同款）：掛在 panel root（不受 frame 縮放），上半正楷功能、下半斜體劇情。</summary>
        void BuildTooltip()
        {
            var go = UIBuilder.Create("Tooltip", transform);
            _tooltip = UIBuilder.Rect(go);
            _tooltip.anchorMin = _tooltip.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltip.pivot = new Vector2(0f, 1f);
            _tooltip.sizeDelta = new Vector2(TooltipWidth, 10f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.07f, 0.96f);
            bg.raycastTarget = false;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(18, 18, 14, 14);
            vlg.spacing = 8;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _tipName = UIBuilder.Text(go.transform, "Name", "", 26, new Color(1f, 0.85f, 0.45f), TextAnchor.UpperLeft);
            _tipName.fontStyle = FontStyle.Bold;
            _tipStats = UIBuilder.Text(go.transform, "Stats", "", 22, new Color(0.92f, 0.92f, 0.95f), TextAnchor.UpperLeft);
            _tipLore = UIBuilder.Text(go.transform, "Lore", "", 20, new Color(0.72f, 0.69f, 0.62f), TextAnchor.UpperLeft);
            _tipLore.fontStyle = FontStyle.Italic;

            go.SetActive(false);
        }

        protected override void OnOpen()
        {
            _page = 0;
            _activeStore = StorageSystem.Instance.Page(_page);
            SetPairedLayout(false);   // 預設置中；協調器會視背包是否開啟再調整
            BuildStoreGrid();
            UpdateTabHighlight();
            StorageSystem.Instance.OnChanged += RefreshAll;
            RefreshAll();
        }

        protected override void OnClose()
        {
            if (StorageSystem.Instance != null) StorageSystem.Instance.OnChanged -= RefreshAll;
            ClearHighlight();
            HideTooltip();
        }

        // ── hover：高亮 + tooltip（比照背包）──
        public void HoverEnter(ItemSlotWidget slot)
        {
            _highlight.SetParent(slot.transform, false);
            UIBuilder.Stretch(_highlight);
            _highlight.SetAsFirstSibling();
            _highlight.gameObject.SetActive(true);
            ShowTooltip(slot.Container.GetAt(slot.Index).ItemId);
        }

        public void HoverExit(ItemSlotWidget slot)
        {
            ClearHighlight();
            HideTooltip();
        }

        void ClearHighlight()
        {
            if (_highlight == null) return;
            _highlight.gameObject.SetActive(false);
            _highlight.SetParent(_frame, false);
        }

        void ShowTooltip(int itemId)
        {
            var d = itemId > 0 ? InventorySystem.Instance.GetData(itemId) : null;
            if (d == null) { HideTooltip(); return; }
            _tipName.text = d.Name;
            _tipStats.text = d.TipStats; _tipStats.gameObject.SetActive(!string.IsNullOrEmpty(d.TipStats));
            _tipLore.text = d.TipLore; _tipLore.gameObject.SetActive(!string.IsNullOrEmpty(d.TipLore));
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

        /// <summary>並排（左移）或單獨（置中）。由 StorageBagCoordinator 呼叫。</summary>
        public void SetPairedLayout(bool paired)
        {
            if (_frame != null) _frame.anchoredPosition = new Vector2(paired ? PairLeftX : SoloX, 0);
        }

        // ── 分頁 / 排序 ──
        void SetPage(int page)
        {
            _page = Mathf.Clamp(page, 0, StorageSystem.Instance.Pages - 1);
            _activeStore = StorageSystem.Instance.Page(_page);
            BuildStoreGrid();
            UpdateTabHighlight();
            RefreshAll();
        }

        void SortActivePage()
        {
            if (_activeStore is ItemGridData g) g.Sort();
        }

        void UpdateTabHighlight()
        {
            for (int i = 0; i < _tabImages.Count; i++)
                if (_tabImages[i] != null)
                    _tabImages[i].sprite = (i == _page) ? _cellPressed : _cellNormal;
        }

        // ── 點擊：只有背包也開著時才把整堆送到背包；單開倉庫只供整理/瀏覽，不搬運 ──
        public void HandleClick(ItemSlotWidget slot)
        {
            var ui = UIManager.Instance;
            bool bagOpen = ui != null && ui.IsOpen<InventoryPanel>();
            if (!bagOpen) return;
            InventoryActions.QuickMoveGrid(slot, InventorySystem.Instance);
        }

        // ── 建格 / 刷新 ──
        void BuildStoreGrid()
        {
            ClearHolder(_storeHolder);
            _storeSlots.Clear();
            int capacity = _activeStore.Capacity;
            for (int i = 0; i < capacity; i++)
            {
                int c = i % StoreCols, r = i / StoreCols;
                float size = Mathf.Min(CellW, CellH) - 4f;
                var slot = ItemSlotWidget.Create(_storeHolder, size);
                PlaceTL((RectTransform)slot.transform, c * CellW, r * CellH, size, size);
                slot.Bind(this, _activeStore, i);
                _storeSlots.Add(slot);
            }
        }

        void RefreshAll()
        {
            for (int i = 0; i < _storeSlots.Count; i++) if (_storeSlots[i] != null) _storeSlots[i].Refresh();
        }

        // ── 小工具 ──
        RectTransform MakeHolder(Transform parent, string name, float x, float y)
        {
            var go = UIBuilder.Create(name, parent);
            var rt = UIBuilder.Rect(go);
            PlaceTL(rt, x, y, 10, 10);
            return rt;
        }

        static void ClearHolder(RectTransform holder)
        {
            if (holder == null) return;
            for (int i = holder.childCount - 1; i >= 0; i--) Destroy(holder.GetChild(i).gameObject);
        }

        static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
