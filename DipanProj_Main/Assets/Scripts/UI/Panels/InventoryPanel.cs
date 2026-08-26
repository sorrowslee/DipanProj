using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Inventory;
using Dipan.Localization;

namespace Dipan.UI
{
    /// <summary>
    /// 背包面板（呈現層）。背景用 inventoryPanel_Bg.png 整張當底，依量到的像素座標在上面疊互動格子，
    /// 從 InventorySystem 讀資料繪 icon。座標都在「背景原圖像素空間」(1254x1254)，整個 frame 等比縮放塞進畫面。
    ///
    /// 版面：左右各三個裝備欄（左＝武器/手套/鞋子、右＝盔甲/護身符/戒指，順序照背景圖畫的剪影）、
    /// 下方兩個藥水格、中央 5x4 共 20 格的道具區，上方兩個頁籤（裝備 / 消耗品）切換兩個獨立的包，
    /// 底列由左到右是 重整鈕 / 上一頁 / 頁碼 / 下一頁 / 金錢。
    ///
    /// 分包與容量規則在資料層（<see cref="InventorySystem"/>），這裡只負責「顯示第幾包的第幾頁」。
    /// 見 readme/INVENTORY.md。
    /// </summary>
    public class InventoryPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool ShowBackdrop => true;   // 遮罩由 UIManager 統一鋪在所有視窗最底層（一層、不蓋面板）
        public override bool CloseOnEscape => true;

        const string ResDir = "UI/InventoryPanel/";

        // ── 背景原圖尺寸（2026-08-07 新版背景 inventoryPanel_Bg.png，正方形）──
        const float BgW = 1254f, BgH = 1254f;

        // ── 道具格 5x4（量自背景的格線）──
        static readonly float[] GridCx = { 403f, 513.4f, 623.8f, 734.2f, 844.6f };
        static readonly float[] GridCy = { 467f, 577f, 687f, 797f };
        const float CellW = 95f, CellH = 92f;

        // ── icon 與方框的比例（所有格子共用一組）──
        // icon 的實際大小不再逐格寫死：呼叫端只給「內容框 = 格框 × 這兩個比例」，
        // 由 IconFit 依每張圖的**不透明內容**反推要放多大——素材留白多的（例如藥水只佔 41%）
        // 會自動放大、留白少的（例如劍佔 100%）會自動縮小，畫面上大小才一致。見 UI/IconFit.cs。
        const float IconFillX = 0.84f, IconFillY = 0.82f;

        // ── 裝備欄 6 格（左欄由上到下：武器/手套/鞋子；右欄：盔甲/護身符/戒指）──
        //    每格的方框大小不一樣（上排比較大），所以四個值都逐格列出，不用共用常數。
        static readonly EquipSlot[] EquipOrder =
        {
            EquipSlot.Weapon, EquipSlot.Chest,
            EquipSlot.Gloves, EquipSlot.Amulet,
            EquipSlot.Boots,  EquipSlot.Ring,
        };
        static readonly float[] EquipCx = { 233f, 1019f, 199f, 1052f, 199f, 1052f };
        static readonly float[] EquipCy = { 243f, 243f, 535f, 535f, 807f, 807f };
        static readonly float[] EquipBoxW = { 220f, 221f, 173f, 175f, 173f, 175f };
        static readonly float[] EquipBoxH = { 258f, 258f, 230f, 230f, 218f, 218f };

        // ── 藥水格 2 格（面板最下方突出的兩格；左＝鍵1、右＝鍵2）──
        static readonly float[] PotionCx = { 534f, 717f };
        const float PotionCy = 1098f, PotionBoxW = 132f, PotionBoxH = 172f;

        // ── 頁籤（裝備 / 消耗品）──
        static readonly float[] TabCx = { 499f, 749f };
        const float TabCy = 338f, TabContentW = 248f, TabHitW = 248f, TabHitH = 74f, TabIconH = 54f;

        // ── 底列（都在同一條基準線上）──
        const float BarCy = 939f;
        const float RefreshCx = 410f, RefreshContentW = 190f, RefreshHitW = 190f, RefreshHitH = 70f;
        const float PrevCx = 551f, PageNumCx = 627f, NextCx = 703f;
        const float NavContentH = 66f, NavHitW = 58f, NavHitH = 70f;
        // 金錢：**靠左**對齊擺在牌子前段。右界一定要停在 x≈872 之前——背景圖從 x≈878 開始畫錢幣，
        // 壓過去就會疊在錢幣上。長數字靠 resizeTextForBestFit 自動縮字級，不會撐出這個框。
        const float MoneyCx = 812f, MoneyCy = 937f, MoneyW = 108f, MoneyH = 46f;

        // ── 兩種格子提示：一定要一眼分得出來 ──
        // 以前兩者都是「整片鋪滿的黃色」、只差 0.08 的 alpha，玩家根本分不出來，拖曳提示等於白做。
        // 現在：hover ＝ 一圈細金框（靜止、不填滿）；可放這格 ＝ 會呼吸的亮金外框 ＋ 很淡的底光。
        // ⚠ 本專案是 Linear 色彩空間，半透明疊圖比直覺亮很多（同樣 alpha=0.22 疊在近黑底上，
        //   Gamma 是 RGB(73,62,32)、Linear 是 RGB(129,106,41)），所以下面的 alpha 都刻意壓得比「感覺上該有的」低。
        static readonly Color HoverOutline = new Color(1f, 0.88f, 0.55f, 0.85f);   // hover 外框（細線，所以 alpha 要高才看得見）
        const float HoverOutlineWidth = 3.5f;
        // 「可放這格」＝很淡的底光（固定）＋**會呼吸的亮外框**。
        // 刻意讓會動的是外框而不是底光：底光一強，大格子又會變成一片黃色看板（就是原本被抱怨的那個問題）。
        static readonly Color DropHiColor = new Color(1f, 0.85f, 0.40f, 0.07f);
        static readonly Color DropHiOutline = new Color(1f, 0.90f, 0.55f, 1f);
        const float DropPulseMin = 0.40f, DropPulseMax = 1f, DropPulseSpeed = 4.2f;

        static readonly Color NavDisabled = new Color(0.42f, 0.42f, 0.46f, 0.55f);

        [Tooltip("面板顯示高度（CanvasScaler 參考單位，1080 為滿版）。新背景是正方形，這個值同時等於寬度。")]
        public float displayHeight = 900f;

        // 與倉庫/鍛造/傳送門並排時，背包置於右側。
        // 這個值是用「看得見的美術」算的，不是用整張圖：底圖 1254 裡真正不透明的內容是 x 57~1198，
        // 左右各有約 57px 透明留白（顯示時約 41 單位）。倉庫/鍛造的底圖也有類似留白，
        // 所以早期用整張圖寬度去排，兩個面板中間會空出一大塊、背包又被推到快出畫面。
        // 現在的算法：讓「兩邊看得見的美術」中間只留約 40 單位的縫，整組置中。
        const float PairRightX = 400f;
        /// <summary>底圖不透明內容的半寬（原圖像素）：(1198-57)/2。算並排位置與邊界夾制用。</summary>
        const float ArtHalfW = 570.5f;

        RectTransform _frame;
        RectTransform _highlight;   // hover 外框（四條線，不是一整片色塊）——見 UI/SlotOutline.cs
        InventorySlotWidget[] _gridSlots;
        InventorySlotWidget[] _equipSlots;
        PotionSlot[] _potionSlots;
        Text _moneyText, _pageText;
        Image[] _tabOn = new Image[2], _tabOff = new Image[2];
        Image _prevArt, _nextArt;
        Button _prevBtn, _nextBtn;
        int _lastDragId = -1;
        bool _dropHintOn;            // 現在有沒有「可放這格」提示亮著（有才需要跑呼吸）
        readonly List<RectTransform> _dropOutlines = new List<RectTransform>();   // 所有「可放這格」的外框（呼吸用）
        int _lastLockVersion = -1;   // 見 Update()：鍛造台鎖定狀態的輪詢

        BagKind _bag = BagKind.Equip;
        readonly int[] _pageOf = new int[2];   // 每個頁籤各自記住停在第幾頁

        /// <summary>目前顯示中的那一包（給外部查詢／除錯用）。</summary>
        public BagKind CurrentBag => _bag;

        // ── tooltip ──
        const float TooltipWidth = 460f;
        // 語言表：鑲嵌珠對目前武器無效的標記（4016，鍛造介面段）
        const int TxtGemIneffectiveMark = 4016;
        RectTransform _tooltip;
        Text _tipName, _tipStats, _tipLore;

        // ═══════════════ 素材擺放（ArtSpec）═══════════════
        //
        // AI 產的 UI 素材幾乎都是「內容只佔中間一塊、四周整片透明」。uGUI 對齊的是整張圖，
        // 所以直接照原圖擺一定會偏。這裡沿用抽選／鍛造那一套：把每張圖**不透明內容的邊界框**量出來寫成常數，
        // PlaceArt 再反推 Image 應該多大、中心該放哪。換圖務必重量一次（見 readme/PROBLEMS.md E9）。

        readonly struct Art
        {
            public readonly string Res;
            public readonly float TexW, TexH, Bx, By, Bw, Bh;
            public Art(string res, float texW, float texH, float bx, float by, float bw, float bh)
            { Res = res; TexW = texW; TexH = texH; Bx = bx; By = by; Bw = bw; Bh = bh; }
            /// <summary>內容中心相對「整張圖中心」的位移（原圖像素，y 向下）。</summary>
            public float Dx => (Bx + Bw * 0.5f) - TexW * 0.5f;
            public float Dy => (By + Bh * 0.5f) - TexH * 0.5f;
        }

        static readonly Art ArtTabOn = new Art("inventoryPanel_Cell_Selected", 867, 288, 26, 34, 803, 244);
        static readonly Art ArtTabOff = new Art("inventoryPanel_Cell_UnSelected", 822, 278, 24, 36, 776, 227);
        static readonly Art ArtTabIconEquip = new Art("inventoryPanel_Cell_EquipmentIcom", 555, 449, 20, 35, 507, 402);
        static readonly Art ArtTabIconItem = new Art("inventoryPanel_Cell_ItemIcom", 482, 518, 69, 46, 349, 453);
        static readonly Art ArtRefresh = new Art("inventoryPanel_Cell_RefreshButton", 866, 288, 22, 5, 822, 270);
        static readonly Art ArtPageArrow = new Art("inventoryPanel_Cell_PageButton_Right", 500, 500, 80, 32, 350, 435);
        static readonly Art ArtPageNumBg = new Art("inventoryPanel_Cell_PageNum_Bg", 500, 500, 18, 35, 464, 424);

        /// <summary>
        /// 依「內容邊界框」把一張素材擺到背景像素座標 (cx,cy)。contentW / contentH 給一個即可（另一個等比）。
        /// mirror = 水平鏡像（左箭頭直接用右箭頭圖翻過來，不另外出圖）。
        /// </summary>
        Image PlaceArt(in Art a, float cx, float cy, float contentW = 0f, float contentH = 0f, bool mirror = false)
        {
            var img = UIBuilder.Image(_frame, a.Res + (mirror ? "_M" : ""), UIBuilder.LoadSprite(ResDir + a.Res));
            img.raycastTarget = false;
            img.preserveAspect = false;   // 已用邊界框算好比例，不要再讓 preserveAspect 二次縮放
            float s = contentW > 0f ? contentW / a.Bw : contentH / a.Bh;
            float dx = a.Dx * s * (mirror ? -1f : 1f);
            float dy = a.Dy * s;
            Place(img.rectTransform, cx - dx, cy - dy, a.TexW * s, a.TexH * s);
            if (mirror) img.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            return img;
        }

        // ═══════════════ 建構 ═══════════════

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
            var bg = UIBuilder.Image(frameGO.transform, "BG", UIBuilder.LoadSprite(ResDir + "inventoryPanel_Bg"));
            UIBuilder.Stretch(bg.rectTransform);
            bg.raycastTarget = true;   // 吃掉空白處點擊（不穿到遊戲）

            BuildEquipSlots();
            BuildTabs();          // 頁籤圖（底下）
            BuildBottomBar();     // 底列圖（底下）
            BuildGridSlots();     // 20 個道具格

            // hover 外框（重用一個，移入時貼到該格）
            _highlight = SlotOutline.Create(frameGO.transform, "HoverOutline", HoverOutline, HoverOutlineWidth);
            _highlight.gameObject.SetActive(false);

            BuildHitAreas();      // 透明按鈕蓋在圖上（一定要在圖之後建，否則收不到點擊）
            BuildTooltip();
            BuildPotionSlots();
        }

        void BuildEquipSlots()
        {
            _equipSlots = new InventorySlotWidget[EquipOrder.Length];
            for (int i = 0; i < EquipOrder.Length; i++)
            {
                var w = MakeSlot($"Equip_{EquipOrder[i]}", EquipCx[i], EquipCy[i], EquipBoxW[i], EquipBoxH[i],
                                 InventorySlotWidget.Kind.Equip);
                w.equipSlot = EquipOrder[i];
                _equipSlots[i] = w;
            }
        }

        void BuildGridSlots()
        {
            // 只建「一頁」的格子並重複使用：切頁籤/翻頁時只重新綁定索引、不重建物件。
            // 這點很重要——新手教學會鎖定某一格的 GameObject，重建會讓它指到已銷毀的物件。
            _gridSlots = new InventorySlotWidget[InventorySystem.PageSlots];
            for (int i = 0; i < _gridSlots.Length; i++)
            {
                int c = i % GridCx.Length, r = i / GridCx.Length;
                var w = MakeSlot($"Cell_{i}", GridCx[c], GridCy[r], CellW, CellH,
                                 InventorySlotWidget.Kind.Grid);
                w.index = -1;
                _gridSlots[i] = w;
            }
        }

        void BuildTabs()
        {
            for (int i = 0; i < 2; i++)
            {
                _tabOff[i] = PlaceArt(ArtTabOff, TabCx[i], TabCy, contentW: TabContentW);
                _tabOn[i] = PlaceArt(ArtTabOn, TabCx[i], TabCy, contentW: TabContentW);
            }
            PlaceArt(ArtTabIconEquip, TabCx[0], TabCy - 3f, contentH: TabIconH);
            PlaceArt(ArtTabIconItem, TabCx[1], TabCy - 3f, contentH: TabIconH);
        }

        void BuildBottomBar()
        {
            PlaceArt(ArtRefresh, RefreshCx, BarCy, contentW: RefreshContentW);
            _prevArt = PlaceArt(ArtPageArrow, PrevCx, BarCy, contentH: NavContentH, mirror: true);
            PlaceArt(ArtPageNumBg, PageNumCx, BarCy, contentH: NavContentH);
            _nextArt = PlaceArt(ArtPageArrow, NextCx, BarCy, contentH: NavContentH);

            _pageText = UIBuilder.Text(_frame, "PageNum", "1", 30,
                                       new Color(0.93f, 0.90f, 0.82f), TextAnchor.MiddleCenter);
            Place(_pageText.rectTransform, PageNumCx, BarCy + 1f, 66f, 44f);

            // 金錢總額：靠左對齊擺在牌子前段；框的右界停在錢幣圖左側，所以字再長也不會壓到錢幣。
            _moneyText = UIBuilder.Text(_frame, "MoneyTotal", "0", 30,
                                        new Color(1f, 0.88f, 0.55f), TextAnchor.MiddleLeft);
            _moneyText.resizeTextForBestFit = true;   // 錢多到爆框時自動縮小字級（實測九位數在 ~20 級就塞得下）
            _moneyText.resizeTextMinSize = 14;
            _moneyText.resizeTextMaxSize = 30;
            Place(_moneyText.rectTransform, MoneyCx, MoneyCy, MoneyW, MoneyH);
        }

        /// <summary>透明命中區（頁籤、重整、上下頁）。圖是 raycastTarget=false 的，點擊全靠這些方框。</summary>
        void BuildHitAreas()
        {
            for (int i = 0; i < 2; i++)
            {
                var bag = (BagKind)i;
                var b = MakeGhostButton($"TabHit_{bag}", TabCx[i], TabCy, TabHitW, TabHitH, () => SetBag(bag));
                b.name = $"TabHit_{bag}";
            }
            MakeGhostButton("RefreshHit", RefreshCx, BarCy, RefreshHitW, RefreshHitH,
                            () => InventorySystem.Instance.SortBag(_bag));
            _prevBtn = MakeGhostButton("PrevPageHit", PrevCx, BarCy, NavHitW, NavHitH, () => StepPage(-1));
            _nextBtn = MakeGhostButton("NextPageHit", NextCx, BarCy, NavHitW, NavHitH, () => StepPage(+1));
        }

        /// <summary>透明按鈕：美術照樣露出，只用輕微 tint 當 hover / 按下的回饋。</summary>
        Button MakeGhostButton(string name, float cx, float cy, float w, float h, UnityEngine.Events.UnityAction onClick)
        {
            var btn = UIBuilder.Button(_frame, name, "", onClick, new Color(1f, 1f, 1f, 0f));
            var img = btn.GetComponent<Image>();
            btn.targetGraphic = img;   // 程式建的 Button 一定要手動補 targetGraphic，否則沒有任何互動回饋
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0f);
            cb.highlightedColor = new Color(1f, 0.9f, 0.6f, 0.16f);
            cb.pressedColor = new Color(1f, 0.8f, 0.4f, 0.28f);
            cb.selectedColor = new Color(1f, 1f, 1f, 0f);
            cb.disabledColor = new Color(1f, 1f, 1f, 0f);
            cb.colorMultiplier = 1f;
            btn.colors = cb;
            Place((RectTransform)btn.transform, cx, cy, w, h);
            return btn;
        }

        // 兩個藥水格（拖藥劑上來綁定種類、顯示 icon＋剩餘數量；遊戲中按 1/2 使用，見 PotionHotkeys）。
        void BuildPotionSlots()
        {
            _potionSlots = new PotionSlot[PotionCx.Length];
            for (int i = 0; i < PotionCx.Length; i++)
            {
                var go = UIBuilder.Create($"PotionSlot{i}", _frame);
                Place(UIBuilder.Rect(go), PotionCx[i], PotionCy, PotionBoxW, PotionBoxH);

                var hit = go.AddComponent<Image>();
                hit.color = new Color(1f, 1f, 1f, 0f);   // 透明命中區（收拖放）
                hit.raycastTarget = true;

                var dropHi = MakeDropHint(go.transform);

                var icon = MakeIcon(go.transform, PotionBoxW, PotionBoxH);
                var count = MakeCountText(go.transform, PotionBoxW, PotionBoxH);

                var slot = go.AddComponent<PotionSlot>();
                slot.index = i;
                slot.icon = icon;
                slot.count = count;
                slot.dropHi = dropHi;
                slot.Entered = ShowTooltip;
                slot.Exited = HideTooltip;
                _potionSlots[i] = slot;
            }
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
                                     InventorySlotWidget.Kind kind)
        {
            var go = UIBuilder.Create(name, _frame);
            Place(UIBuilder.Rect(go), px, py, w, h);

            var hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);   // 透明命中區
            hit.raycastTarget = true;

            var icon = MakeIcon(go.transform, w, h);
            var count = MakeCountText(go.transform, w, h);

            Image dropHi = null;
            if (kind == InventorySlotWidget.Kind.Equip)
            {
                dropHi = MakeDropHint(go.transform);
                dropHi.transform.SetAsFirstSibling();
            }

            var widget = go.AddComponent<InventorySlotWidget>();
            widget.kind = kind;
            widget.icon = icon;
            widget.count = count;
            widget.dropHi = dropHi;
            widget.Clicked = OnSlotClicked;
            widget.RightClicked = OnSlotRightClicked;
            widget.Entered = OnSlotEnter;
            widget.Exited = OnSlotExit;
            return widget;
        }

        /// <summary>
        /// 「這格可以放」提示：整格底光 ＋ 一圈更亮更實的外框。整包用 SetActive 開關
        /// （**不能只關 Image.enabled**——外框是子物件，關掉父物件的 Image 子物件照樣會畫出來）。
        /// 底光的呼吸閃爍由 <see cref="Update"/> 統一驅動，不必每格各掛一個 Update。
        /// </summary>
        Image MakeDropHint(Transform parent)
        {
            var fill = UIBuilder.Image(parent, "DropHint", null, DropHiColor);
            UIBuilder.Stretch(fill.rectTransform);
            fill.raycastTarget = false;
            var outline = SlotOutline.Create(fill.transform, "Outline", DropHiOutline, HoverOutlineWidth);
            _dropOutlines.Add(outline);   // 呼吸時要一起改色，先登記起來免得每幀去 Find
            fill.gameObject.SetActive(false);
            return fill;
        }

        /// <summary>
        /// 建一個格子裡的 icon。這裡設的 sizeDelta 是**內容框**——實際圖會由 IconFit 依不透明內容反推放多大，
        /// 所以留白多的素材會自動放大、留白少的會自動縮小，看起來大小才一致。見 UI/IconFit.cs。
        /// </summary>
        static Image MakeIcon(Transform parent, float boxW, float boxH)
        {
            var icon = UIBuilder.Image(parent, "Icon", null, Color.white);
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(boxW * IconFillX, boxH * IconFillY);
            icon.preserveAspect = true;   // IconFit 接手後會關掉；沒圖時維持這個比較安全
            icon.raycastTarget = false;
            icon.enabled = false;
            return icon;
        }

        /// <summary>
        /// 格子右下角的數量字。字級跟著格子大小走（不再各處寫死），並加深色陰影，
        /// 壓在 icon 亮處才看得清楚。
        /// </summary>
        static Text MakeCountText(Transform parent, float boxW, float boxH)
        {
            int size = Mathf.RoundToInt(Mathf.Clamp(Mathf.Min(boxW, boxH) * 0.26f, 18f, 30f));
            var t = UIBuilder.Text(parent, "Count", "", size, Color.white, TextAnchor.LowerRight);
            UIBuilder.Stretch(t.rectTransform, 0, Mathf.Max(4f, boxW * 0.06f), 0, Mathf.Max(3f, boxH * 0.04f));
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sh.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        /// <summary>把 RectTransform 放到背景像素座標 (px,py)（左上為原點、y 向下），中心對齊、給定寬高。</summary>
        void Place(RectTransform rt, float px, float py, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // 錨到 frame 左上角
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(px, -py);
            rt.sizeDelta = new Vector2(w, h);
        }

        // ═══════════════ 開關 ═══════════════

        protected override void OnOpen()
        {
            var inv = InventorySystem.Instance;
            inv.OnChanged += Redraw;
            var sm = Dipan.Save.SaveManager.Instance;
            if (sm != null) sm.OnCurrencyChanged += RedrawMoney;   // 金錢是獨立數字，不會觸發背包的 OnChanged
            ShowBagPage(_bag, _pageOf[(int)_bag]);                 // 記住上次停在哪個頁籤/第幾頁
            RedrawMoney();
            _lastDragId = -1;   // 開背包時重算「可放欄位」高亮
            _lastLockVersion = ForgingPanel.LockVersion;
        }

        protected override void OnClose()
        {
            if (InventorySystem.Instance != null)
                InventorySystem.Instance.OnChanged -= Redraw;
            var sm = Dipan.Save.SaveManager.Instance;
            if (sm != null) sm.OnCurrencyChanged -= RedrawMoney;
            ClearHighlight();
            HideTooltip();
        }

        // ═══════════════ 頁籤 / 翻頁 ═══════════════

        /// <summary>切到某個頁籤（回到那個頁籤上次停留的頁數）。</summary>
        public void SetBag(BagKind bag) => ShowBagPage(bag, _pageOf[(int)bag]);

        /// <summary>切到指定的頁籤與頁數（頁數會自動夾在合法範圍內）。</summary>
        public void ShowBagPage(BagKind bag, int page)
        {
            _bag = bag;
            _pageOf[(int)bag] = Mathf.Clamp(page, 0, InventorySystem.PagesOf(bag) - 1);
            UpdateTabVisual();
            Redraw();
        }

        void StepPage(int delta) => ShowBagPage(_bag, _pageOf[(int)_bag] + delta);

        int CurrentPage => _pageOf[(int)_bag];

        void UpdateTabVisual()
        {
            for (int i = 0; i < 2; i++)
            {
                bool on = (int)_bag == i;
                if (_tabOn[i] != null) _tabOn[i].enabled = on;
                if (_tabOff[i] != null) _tabOff[i].enabled = !on;
            }
        }

        void UpdatePageNav()
        {
            int pages = InventorySystem.PagesOf(_bag);
            // 只顯示「現在第幾頁」，不顯示總頁數（作者要求）。總頁數由箭頭能不能按來表達。
            if (_pageText != null) _pageText.text = (CurrentPage + 1).ToString();
            SetNav(_prevBtn, _prevArt, CurrentPage > 0);
            SetNav(_nextBtn, _nextArt, CurrentPage < pages - 1);
        }

        static void SetNav(Button btn, Image art, bool on)
        {
            if (btn != null) btn.interactable = on;
            if (art != null) art.color = on ? Color.white : NavDisabled;
        }

        // ═══════════════ 重繪 ═══════════════

        // 「被別的介面借走」的格子壓黑用的色（見 InventorySlotWidget.locked）。
        static readonly Color LockedTint = new Color(0.26f, 0.26f, 0.28f, 1f);
        static readonly Color LockedTextTint = new Color(0.45f, 0.45f, 0.47f, 1f);

        void Redraw()
        {
            var inv = InventorySystem.Instance;
            int start = InventorySystem.BagStart(_bag);
            int cap = InventorySystem.BagCount(_bag);
            int firstLocal = CurrentPage * InventorySystem.PageSlots;

            for (int i = 0; i < _gridSlots.Length; i++)
            {
                var w = _gridSlots[i];
                if (w == null) continue;
                int local = firstLocal + i;
                bool valid = local < cap;
                // 容量不是一頁的整數倍時，最後一頁會有多的格子——關掉它們，
                // 而不是留著一個 index 越界的格子（那會讓拖放把來源清空、東西憑空消失）。
                if (w.gameObject.activeSelf != valid) w.gameObject.SetActive(valid);
                if (!valid) { w.index = -1; continue; }

                int flat = start + local;
                w.index = flat;
                // 鍛造台把這格的裝備借去放鐵砧了 → 東西還在原位，但鎖起來壓黑（取下才解鎖）。
                w.locked = ForgingPanel.IsGridLocked(flat);
                var st = inv.GetGrid(flat);
                SetSlotVisual(w, st, st.Count);
            }

            for (int i = 0; i < _equipSlots.Length; i++)
            {
                int id = inv.GetEquipped(_equipSlots[i].equipSlot);
                _equipSlots[i].locked = ForgingPanel.IsEquipLocked(_equipSlots[i].equipSlot);
                SetSlotVisual(_equipSlots[i], inv.GetEquippedStack(_equipSlots[i].equipSlot), id > 0 ? 1 : 0);
            }

            if (_potionSlots != null)
                for (int i = 0; i < _potionSlots.Length; i++)
                    if (_potionSlots[i] != null) _potionSlots[i].Refresh();

            UpdatePageNav();
        }

        void SetSlotVisual(InventorySlotWidget w, ItemStack st, int count)
        {
            // 珠子是「珠身（依等級）＋能力符號」兩層，一律走 ItemIcons（見 readme/GEM_SOCKET.md）
            w.icon.color = w.locked ? LockedTint : Color.white;
            ItemIcons.Apply(w.icon, st);
            if (w.count != null)
            {
                w.count.text = (count > 1) ? count.ToString() : "";
                w.count.color = w.locked ? LockedTextTint : Color.white;
            }
        }

        void RedrawMoney()
        {
            if (_moneyText == null) return;
            var sm = Dipan.Save.SaveManager.Instance;
            _moneyText.text = sm != null ? sm.Currency.ToString("N0") : "0";
        }

        // ═══════════════ 互動 ═══════════════

        /// <summary>
        /// **左鍵背包格 = 搬移／裝備／綁定，永遠不會「使用」任何東西。**
        ///
        /// ⚠ 這條界線是刻意的（2026-08-19 作者拍板）：左鍵做的事**一律不消耗道具**，
        /// 所以誤點永遠不會造成損失；真正會消耗的「使用」全部集中在右鍵
        /// （<see cref="OnSlotRightClicked"/> → <see cref="ItemUse"/>）。
        /// 在這裡加分支前先確認它不會消耗東西——原本這裡有一條「左鍵＝喝血統藥劑」，
        /// 而血統是**本世不可逆**的，等於一次誤點就定終身。
        ///
        /// 沒有任何分支命中就**安靜地什麼都不做**（例：左鍵點血統藥劑、能力珠、材料）。
        /// 刻意不 Toast「請按右鍵」——那條規則只需要學一次，每次點都念會很煩。
        /// </summary>
        void OnSlotClicked(InventorySlotWidget w)
        {
            var inv = InventorySystem.Instance;
            if (w.kind == InventorySlotWidget.Kind.Grid)
            {
                if (w.index < 0) return;
                var portal = ScriptsPanel.ActiveGridIfOpen();
                var store = StoragePanel.ActivePageIfOpen();
                int clickedId = inv.GetGrid(w.index).ItemId;
                var clickedData = clickedId > 0 ? inv.GetData(clickedId) : null;
                if (portal != null && clickedData != null && clickedData.IsScript)
                    InventoryActions.QuickMoveGrid(w, portal);  // 傳送門開著：點劇本 → 送進傳送門方框
                else if (ForgingPanel.IsForgeOpen && clickedData != null && clickedData.IsEquippable)
                    ForgingPanel.TryPlaceFromGrid(w.index);     // 鍛造開著：點武器/裝備 → 放上鐵砧（借放，物品留在原位）
                else if (store != null)
                    InventoryActions.QuickMoveGrid(w, store);   // 倉庫開著：點一下送進倉庫當前分頁
                else if (clickedData != null && clickedData.IsPotion)
                    inv.AutoPlacePotion(clickedId);              // 藥水：綁定到快捷格（**不是喝掉**；空位優先＝左格＝鍵1）
                else if (clickedData != null && clickedData.IsEquippable)
                    inv.EquipFromGrid(w.index);                  // 可裝備物品：左鍵自動裝備
            }
            else
            {
                inv.Unequip(w.equipSlot);                        // 裝備格左鍵 = 卸下
            }
        }

        /// <summary>
        /// 找出目前放著某道具的背包格 RectTransform（給新手教學手指/遮罩指向）；找不到回 null。
        /// **會自動切到那件東西所在的頁籤與頁數**——不然教學的手指會指向一個根本沒顯示出來的格子。
        /// </summary>
        public RectTransform FindGridSlotRect(int itemId)
        {
            var inv = InventorySystem.Instance;
            if (inv == null || _gridSlots == null || itemId <= 0) return null;

            // 先看目前這一頁（最常見的情況，順便避免每幀重切頁）
            for (int i = 0; i < _gridSlots.Length; i++)
            {
                var w = _gridSlots[i];
                if (w == null || w.index < 0 || !w.gameObject.activeSelf) continue;
                if (inv.GetGrid(w.index).ItemId == itemId) return (RectTransform)w.transform;
            }

            // 不在這一頁 → 全背包找，找到就切過去
            for (int flat = 0; flat < InventorySystem.GridCount; flat++)
            {
                if (inv.GetGrid(flat).ItemId != itemId) continue;
                var bag = InventorySystem.BagOf(flat);
                int local = flat - InventorySystem.BagStart(bag);
                ShowBagPage(bag, local / InventorySystem.PageSlots);
                int slot = local % InventorySystem.PageSlots;
                return (slot >= 0 && slot < _gridSlots.Length) ? (RectTransform)_gridSlots[slot].transform : null;
            }
            return null;
        }

        Canvas _canvas;

        /// <summary>並排（右移）或單獨（置中）。由 StorageBagCoordinator 每幀呼叫（idempotent）。</summary>
        public void SetPairedLayout(bool paired)
        {
            if (_frame == null) return;
            _frame.anchoredPosition = new Vector2(paired ? PairedX() : 0f, 0f);
        }

        /// <summary>
        /// 並排時的 X。除了固定值，還會**夾住不讓看得見的美術超出畫面右緣**——
        /// CanvasScaler 用 MatchWidthOrHeight=0.5，畫面越窄（非 16:9）可用的參考寬度就越小，
        /// 固定值在窄視窗會把背包右半邊切掉。
        /// </summary>
        float PairedX()
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null) return PairRightX;
            var root = _canvas.rootCanvas != null ? _canvas.rootCanvas : _canvas;
            float halfCanvas = ((RectTransform)root.transform).rect.width * 0.5f;
            float visibleHalf = ArtHalfW * (displayHeight / BgH);
            return Mathf.Min(PairRightX, halfCanvas - visibleHalf - 8f);
        }

        void OnSlotEnter(InventorySlotWidget w)
        {
            // 拖曳中不用 hover 高亮（改用「可放欄位」高亮，見 UpdateDropHighlights）
            if (SlotDragController.DraggingItemId == 0)
            {
                _highlight.SetParent(w.transform, false);
                UIBuilder.Stretch(_highlight);
                _highlight.SetAsFirstSibling();
                _highlight.gameObject.SetActive(true);
            }

            var inv = InventorySystem.Instance;
            var st = (w.kind == InventorySlotWidget.Kind.Grid)
                ? inv.GetGrid(w.index)
                : inv.GetEquippedStack(w.equipSlot);
            ShowTooltip(st);
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
            _highlight.SetParent(_frame, false);
        }

        // ── tooltip ──

        void ShowTooltip(int itemId) => ShowTooltip(new ItemStack { ItemId = itemId, Count = 1, Inst = null });

        void ShowTooltip(ItemStack st)
        {
            var d = (st.ItemId > 0) ? InventorySystem.Instance.GetData(st.ItemId) : null;
            if (d == null) { HideTooltip(); return; }

            // 名稱後面標出「這一件」的資訊——孔數／珠子等級是每一件各自不同的，表格裡查不到。
            string title = d.Name;
            if (st.Inst != null)
            {
                if (st.Inst.HasSockets && st.Inst.UnlockedCount > 0)
                    title += $"（{st.Inst.UnlockedCount} 孔）";
                else if (st.Inst.level > 0)
                    title += $"  Lv{st.Inst.level}";
            }
            _tipName.text = title;

            _tipStats.text = BuildTipStats(d, st.Inst);
            _tipStats.gameObject.SetActive(!string.IsNullOrEmpty(_tipStats.text));
            _tipLore.text = d.TipLore;
            _tipLore.gameObject.SetActive(!string.IsNullOrEmpty(d.TipLore));

            _tooltip.gameObject.SetActive(true);
            _tooltip.SetAsLastSibling();
            PositionTooltip();
        }

        /// <summary>
        /// tooltip 上半：表格寫死的說明 ＋「這一件」的鑲嵌內容。
        /// 能力珠會顯示它這一級實際給多少（直接查 GemTable，不用另外維護一份文案）。
        /// </summary>
        static string BuildTipStats(ItemData d, ItemInstance inst)
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

        void HideTooltip()
        {
            if (_tooltip != null) _tooltip.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_tooltip != null && _tooltip.gameObject.activeSelf) PositionTooltip();
            int drag = SlotDragController.DraggingItemId;
            if (drag != _lastDragId) { _lastDragId = drag; UpdateDropHighlights(drag); }
            if (_dropHintOn) PulseDropHints();

            // 東西放上／取下鐵砧時**不會動到背包資料**（是借放不是搬移），所以不會觸發 OnChanged。
            // 靠鍛造台的版本號輪詢來重畫「哪一格被鎖住壓黑」。
            if (ForgingPanel.LockVersion != _lastLockVersion)
            {
                _lastLockVersion = ForgingPanel.LockVersion;
                Redraw();
            }
        }

        /// <summary>
        /// 「可放這格」底光的呼吸。用 <see cref="Time.unscaledTime"/>——背包會把遊戲暫停（timeScale = 0），
        /// 用一般 Time.time 會整個停住不動。
        /// </summary>
        void PulseDropHints()
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * DropPulseSpeed);
            var c = DropHiOutline;
            c.a = Mathf.Lerp(DropPulseMin, DropPulseMax, t);
            for (int i = 0; i < _dropOutlines.Count; i++)
            {
                var o = _dropOutlines[i];
                if (o != null && o.gameObject.activeInHierarchy) SlotOutline.SetColor(o, c);
            }
        }

        // 拖起某類物品時，把「該類物品能放、且空著的專用欄」亮起提示（裝備欄/藥水格）。放開時 itemId=0 → 全部關掉。
        void UpdateDropHighlights(int itemId)
        {
            _dropHintOn = false;
            if (_equipSlots != null)
                foreach (var w in _equipSlots) if (w != null && w.dropHi != null) w.dropHi.gameObject.SetActive(false);
            if (_potionSlots != null)
                foreach (var s in _potionSlots) if (s != null && s.dropHi != null) s.dropHi.gameObject.SetActive(false);

            var d = itemId > 0 ? InventorySystem.Instance.GetData(itemId) : null;
            if (d == null) return;
            var inv = InventorySystem.Instance;
            if (d.IsEquippable)
            {
                if (_equipSlots != null)
                    foreach (var w in _equipSlots)
                        if (w != null && w.dropHi != null && w.equipSlot == d.EquipSlot && inv.GetEquipped(w.equipSlot) == 0)
                        { w.dropHi.gameObject.SetActive(true); _dropHintOn = true; }
            }
            else if (d.IsPotion)
            {
                if (_potionSlots != null)
                    for (int i = 0; i < _potionSlots.Length; i++)
                        if (_potionSlots[i] != null && _potionSlots[i].dropHi != null && inv.GetPotionSlot(i) == 0)
                        { _potionSlots[i].dropHi.gameObject.SetActive(true); _dropHintOn = true; }
            }
        }

        /// <summary>
        /// **右鍵背包格 = 使用這件道具。這是全遊戲唯一的「使用」入口**
        ///（藥水的快捷格熱鍵 1／2 最後也是走進同一支 <see cref="ItemUse"/>）。
        ///
        /// 本面板刻意**不懂任何道具規則**——「這東西能不能用」「不能用的理由」「要不要先跳確認視窗」
        /// 全部由 <see cref="ItemUse.PlanUse"/> 算好，這裡只負責顯示。加新的可用道具不用回頭動 UI。
        ///
        /// 三種結果：
        /// <list type="bullet">
        /// <item>能用、要確認（血統藥劑不可逆）→ 跳 <see cref="ConfirmPopup"/>，按下去才真的用。</item>
        /// <item>能用、不用確認（回血回魔藥劑）→ 當場喝掉。</item>
        /// <item>不能用 → <c>Reason</c> 有字就 Toast 說明；<b>沒字代表這東西根本沒有使用行為</b>
        ///   （武器、材料、能力珠…），此時**安靜地什麼都不做**，不要跳「無法使用」之類的廢話。</item>
        /// </list>
        ///
        /// ⚠ 不能用的時候是**在按下右鍵的當下就擋掉並說明理由**，不會先跳確認視窗、按完才發現沒反應
        ///   （例：還在第一階卻拿到高階藥劑，會直接告訴玩家要先進階為「毛殭」）。
        /// ⚠ 教學強制階段（<c>TutorialManager.HardLock</c>）不放行使用——教學正在教「左鍵把藥水
        ///   放進快捷格」，玩家若在那一步右鍵把唯一一瓶喝掉，教學會等一個永遠不會發生的條件。
        /// </summary>
        void OnSlotRightClicked(InventorySlotWidget w)
        {
            if (w == null || w.kind != InventorySlotWidget.Kind.Grid || w.index < 0) return;
            if (TutorialManager.HardLock) return;

            var inv = InventorySystem.Instance;
            int id = inv.GetGrid(w.index).ItemId;
            if (id <= 0) return;

            var plan = ItemUse.PlanUse(id);
            if (!plan.Ok)
            {
                if (!string.IsNullOrEmpty(plan.Reason)) AlertPanel.Toast(plan.Reason);
                return;
            }

            if (!string.IsNullOrEmpty(plan.ConfirmText))
            {
                ConfirmPopup.Show(plan.ConfirmText, () => UseItem(id));
                return;
            }
            UseItem(id);
        }

        /// <summary>
        /// 真的用下去並回報結果。
        /// ⚠ 刻意不重用外面算好的 <see cref="ItemUse.Plan"/>——確認視窗開著的期間狀態可能變了
        /// （東西被搬走／被喝掉／血統被別的途徑改掉），<see cref="ItemUse.TryUse"/> 內部會自己重新規劃一次，
        /// 成功與失敗的訊息都由它回傳，有字就直接顯示。
        /// </summary>
        static void UseItem(int itemId)
        {
            ItemUse.TryUse(itemId, out string message);
            if (!string.IsNullOrEmpty(message)) AlertPanel.Toast(message);
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
