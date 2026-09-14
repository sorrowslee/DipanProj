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

        // ── 底圖原生座標（**量自 StoragePanelBG.png，1122×1402**；換底圖要重量）──
        //
        // ⚠ 2026-09-14 之前這一區的座標是憑印象填的（`GridX0=167`、`CellW=84.8`、10×10），
        //   與底圖實際畫的格線對不上 ⇒ 格子與頁籤整片往右下溢出到框外（作者實機截圖）。
        //   現在的值是掃描底圖亮度量出來的：**垂直線中心 196…921（節距 72.6，10 欄）、
        //   水平線中心 408…1096（節距 76.5，9 列）**。
        //
        // 格線本身跟背包一樣**改由程式鋪**（底圖那張 10×9 用 `StoragePanel_GridBlank` 蓋掉、
        // 再用從底圖裁下來的格單元 `StoragePanel_CellFrame` 依欄列數平鋪），
        // 所以欄列數改 `StorageSystem.DefaultCols/Rows` 就換版面，素材不用動。見 readme/STORAGE.md。
        const float FrameW = 1122f, FrameH = 1402f;
        const float GridX0 = 196f, GridY0 = 408f, GridX1 = 921f, GridY1 = 1096f;   // 格線區（最外圈線的中心）
        const float GridPad = 5f;                    // 格子與邊界之間留的縫
        const float CellInnerRatio = 0.90f;          // 格單元圖裡「格內容」佔的比例（量自 CellFrame 73×77）
        const float BlankX = 190f, BlankY = 400f, BlankW = 740f, BlankH = 702f;    // 蓋舊格線的底板

        // 頁籤：**對齊每一欄的中心**（剛好 5 欄 5 頁），不再寫死 x 座標
        const float TabCy = 350f;                    // 頁籤中心線（在格線區上緣之上的木板區）
        const float TabWidthRatio = 0.92f;           // 頁籤寬 ＝ 一欄的 92%
        const float TabAspect = 408f / 612f;         // 頁籤圖 CellBG_normal 是 612×408
        // 重整鈕「中心」位置（底圖原生像素，左上為原點，X→右、Y→下）。要往上就調小 RefreshCy、往右就調大 RefreshCx。
        const float RefreshCx = 765f, RefreshCy = 1250f, RefreshSize = 130f;

        /// <summary>一個格單元的邊長（含縫）。由格線區與欄列數算出來 ⇒ 改欄列數不必重量座標。</summary>
        static float CellPitch => Mathf.Min((GridX1 - GridX0 - GridPad * 2f) / StorageSystem.DefaultCols,
                                            (GridY1 - GridY0 - GridPad * 2f) / StorageSystem.DefaultRows);

        /// <summary>第 (col,row) 格的**左上角**（底圖像素座標）。整組在格線區裡置中。</summary>
        static Vector2 CellTopLeft(int col, int row)
        {
            float p = CellPitch;
            float ox = GridX0 + GridPad + ((GridX1 - GridX0 - GridPad * 2f) - p * StorageSystem.DefaultCols) * 0.5f;
            float oy = GridY0 + GridPad + ((GridY1 - GridY0 - GridPad * 2f) - p * StorageSystem.DefaultRows) * 0.5f;
            return new Vector2(ox + col * p, oy + row * p);
        }

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
        RectTransform _highlight;   // hover 外框（與背包同一套，見 UI/SlotOutline.cs）
        ItemTooltip _tip;           // tooltip 的版面與定位都在共用元件裡（見 UI/ItemTooltip.cs）

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

            float tabW = CellPitch * TabWidthRatio, tabH = tabW * TabAspect;
            for (int i = 0; i < StorageSystem.PageCount; i++)
            {
                int page = i;
                var b = UIBuilder.Button(_frame, $"Tab{i + 1}", "", () => SetPage(page), Color.white, _cellNormal);
                var bimg = b.GetComponent<Image>();
                b.targetGraphic = bimg;
                // 頁籤中心對齊第 i 欄的中心（5 欄 5 頁剛好一對一）
                float tabCx = CellTopLeft(i, 0).x + CellPitch * 0.5f;
                PlaceTL((RectTransform)b.transform, tabCx - tabW / 2f, TabCy - tabH / 2f, tabW, tabH);
                if (i < _cellNum.Length && _cellNum[i] != null)
                {
                    var num = UIBuilder.Image(b.transform, "Num", _cellNum[i]);
                    num.preserveAspect = true; num.raycastTarget = false;
                    UIBuilder.Stretch(num.rectTransform, tabW * 0.22f, tabW * 0.22f, tabH * 0.15f, tabH * 0.15f);
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

            BuildGridArt();   // 蓋掉底圖畫死的 10×9 格線 ＋ 依欄列數鋪新格線（**一定要在格子之前**）

            _storeHolder = MakeHolder(_frame, "StoreGrid", 0f, 0f);

            // hover 外框（重用一個，移入時貼到該格）。細線而不是整片上色——理由見 UI/SlotOutline.cs。
            _highlight = SlotOutline.Create(_frame, "HoverOutline", new Color(1f, 0.88f, 0.55f, 0.85f), 3.5f);
            _highlight.gameObject.SetActive(false);

            _tip = ItemTooltip.Create(transform);   // 共用元件（與背包／鍛造同一份）
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
            ShowTooltip(slot.Container.GetAt(slot.Index));
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

        /// <summary>hover 進出時開關 tooltip。倉庫格子拿得到整個 <see cref="ItemStack"/>，
        /// 所以**連鑲嵌內容與珠子等級一起顯示**（以前只傳 itemId，那些資訊看不到）。</summary>
        void ShowTooltip(ItemStack st) => _tip.Show(st);
        void HideTooltip() => _tip.Hide();

        void Update()
        {
            if (_tip != null) _tip.Follow();
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
            int cols = StorageSystem.DefaultCols;
            float pitch = CellPitch, inner = pitch * CellInnerRatio;
            for (int i = 0; i < capacity; i++)
            {
                var tl = CellTopLeft(i % cols, i / cols);
                var slot = ItemSlotWidget.Create(_storeHolder, inner);
                // 命中區置中在格單元裡（CellTopLeft 給的是單元左上角）
                PlaceTL((RectTransform)slot.transform, tl.x + (pitch - inner) * 0.5f, tl.y + (pitch - inner) * 0.5f, inner, inner);
                slot.Bind(this, _activeStore, i);
                _storeSlots.Add(slot);
            }
        }

        /// <summary>
        /// 鋪格線美術：先用底板蓋掉底圖畫死的 10×9 格線，再依欄列數鋪格單元。
        /// 與背包同一套做法（見 readme/INVENTORY.md〈道具區的格線是程式鋪的〉），
        /// **一定要在 <see cref="BuildStoreGrid"/> 之前呼叫**——命中區要蓋在圖上面才收得到點擊。
        /// </summary>
        void BuildGridArt()
        {
            var blank = UIBuilder.Image(_frame, "GridBlank", L("StoragePanel_GridBlank"));
            blank.raycastTarget = false;
            blank.preserveAspect = false;
            PlaceTL(blank.rectTransform, BlankX, BlankY, BlankW, BlankH);

            var cellSprite = L("StoragePanel_CellFrame");
            float p = CellPitch;
            int n = StorageSystem.DefaultCols * StorageSystem.DefaultRows;
            for (int i = 0; i < n; i++)
            {
                var img = UIBuilder.Image(_frame, $"CellFrame_{i}", cellSprite);
                img.raycastTarget = false;
                img.preserveAspect = false;
                var tl = CellTopLeft(i % StorageSystem.DefaultCols, i / StorageSystem.DefaultCols);
                PlaceTL(img.rectTransform, tl.x, tl.y, p, p);
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
