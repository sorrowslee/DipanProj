using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Dipan.Exchange;
using Dipan.Localization;

namespace Dipan.UI
{
    /// <summary>
    /// **兌換所面板**（主神空間式兌換系統，2026-11-15）。版面與功能規格見 readme/EXCHANGE.md。
    ///
    /// 功能：分類下拉、名稱搜尋、稀有度篩選（白藍黃紫，可複選）、只看買得起、只看收藏、
    /// 排序（推薦／價格／稀有度／名稱）、三種檢視（列表／縮圖／大圖）。點品項開 <see cref="ExchangeDetailPopup"/> 兌換。
    /// - 列表、縮圖：滑鼠移上去跳共用 <see cref="ItemTooltip"/>（大圖預覽＋說明＋稀有度與價格）。
    /// - 大圖：說明直接寫在卡片下半，不跳 tooltip。
    ///
    /// 座標一律是底圖 ExchangePanel_Bg.png 的原生像素（1483×955，左上為原點），外框用 localScale 縮放。
    /// 資料層在 Dipan.Exchange（ExchangeTable／ExchangeService），本面板只負責呈現。
    /// </summary>
    public class ExchangePanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool ShowBackdrop => true;

        const string Res = "UI/ExchangePanel/";

        // ══════════════════ 底圖座標（量自 ExchangePanel_Bg.png 1483×955；換底圖要重量）══════════════════
        const float FrameW = 1483f, FrameH = 955f;
        const float FrameScale = 1.08f;                       // 1483×1.08 ≈ 1600、955×1.08 ≈ 1031（1920×1080 參考解析度）
        const float HeadY = 72f, HeadH = 110f;                // 上方標題帶（金線分隔之上）
        // 搜尋框寬與錢包起點（2026-11-16 錢包從一組變四組：搜尋框 540→450，錢包 x 890～1334，關閉鈕在 1340）
        const float SearchW = 450f, WalletX0 = 890f, WalletGap = 6f;
        const float BodyX = 62f, BodyR = 1422f;               // 內容區左右（內框 46~1437 各留 16）
        const float ToolY = 200f, ToolH = 56f;                // 工具列
        const float ResultsY = 268f, ResultsH = 590f;         // 結果捲動區
        const float FootY = 864f, FootH = 36f;                // 底列（件數／重設）
        const float ScrollbarW = 14f;

        // 每種檢視的格子尺寸（原生像素）
        const float ListRowH = 72f, ListGap = 6f;
        const float ThumbW = 120f, ThumbH = 152f, ThumbGap = 12f;
        const float CardW = 316f, CardH = 440f, CardGap = 14f;
        const float GridPad = 8f;

        // ══════════════════ 顏色 ══════════════════
        static readonly Color Gold = new Color(1f, 0.85f, 0.45f);
        static readonly Color TextLight = new Color(0.92f, 0.9f, 0.85f);
        static readonly Color TextDim = new Color(0.62f, 0.6f, 0.55f);
        static readonly Color TextBad = new Color(0.88f, 0.38f, 0.32f);
        static readonly Color OutlineOn = new Color(0.85f, 0.68f, 0.42f);
        static readonly Color HoverLine = new Color(1f, 0.9f, 0.62f);
        static readonly Color Fallback = new Color(0.09f, 0.075f, 0.05f, 1f);   // 素材缺圖時的底色

        // ══════════════════ 語言表（兌換系統段 10001–10099）══════════════════
        const int TxtTitle = 10001, TxtSearch = 10002, TxtAll = 10003;
        const int TxtSortFirst = 10021;                                    // 10021–10026 排序選項
        const int TxtAffordable = 10027, TxtFavOnly = 10028, TxtCount = 10029, TxtEmpty = 10030, TxtReset = 10031;
        const int TxtViewList = 10032, TxtViewThumb = 10033, TxtViewCard = 10034;
        const int TxtOwned = 10041, TxtNotOpen = 10049, TxtRarity = 10051, TxtPrice = 10052;
        const int TxtRolledOnExchange = 10056;
        const int TxtRightFavAdd = 10053, TxtRightFavDel = 10054;           // 「右鍵：加入收藏／取消收藏」

        enum ViewMode { List, Thumb, Card }
        enum SortMode { Recommended, PriceAsc, PriceDesc, RarityDesc, RarityAsc, Name }

        // ══════════════════ 篩選狀態（面板實例會被快取，所以關掉再開會保留上次的條件）══════════════════
        ExchangeCategory _cat = ExchangeCategory.All;
        readonly HashSet<ItemRarity> _rarities = new HashSet<ItemRarity>();   // 空 ＝ 全部
        string _query = "";
        bool _affordOnly, _favOnly;
        SortMode _sort = SortMode.Recommended;
        ViewMode _view = ViewMode.Thumb;

        // ══════════════════ 元件 ══════════════════
        Sprite _tile, _field;
        RectTransform _frame;
        readonly Dictionary<string, Text> _walletTexts = new Dictionary<string, Text>();   // 貨幣代號 → 數字
        InputField _search;
        DropMenu _catDrop, _sortDrop;
        RectTransform _rarityAllOutline;
        readonly Dictionary<ItemRarity, RectTransform> _rarityOutlines = new Dictionary<ItemRarity, RectTransform>();
        RectTransform _affordOutline, _favOutline;
        readonly RectTransform[] _viewOutlines = new RectTransform[3];

        ScrollRect _scroll;
        RectTransform _content;
        GridLayoutGroup _grid;
        Text _countText, _emptyText;
        ItemTooltip _tip;

        ExchangeEntry _hovered;
        ExchangeEntry _tipFor;   // 目前 ItemTooltip 顯示的是哪一件（右鍵切收藏後要重畫 tooltip 的提示行）
        RectTransform _hoverOutline;
        readonly List<ExchangeEntry> _shown = new List<ExchangeEntry>();
        bool _dirty;

        static Sprite L(string n) => Resources.Load<Sprite>(Res + n);

        /// <summary>
        /// 開兌換所。<paramref name="arg"/>（openPanel 的參數）可填分類英文名，開啟時先切到那個分類；留空＝維持上次的篩選。
        /// </summary>
        public static ExchangePanel OpenWith(string arg = null)
        {
            var ui = UIManager.Instance;
            if (ui == null) return null;
            var p = ui.Open<ExchangePanel>();
            if (p != null && !string.IsNullOrWhiteSpace(arg)
                && Enum.TryParse(arg.Trim(), true, out ExchangeCategory c))
            {
                p._cat = c;
                p._catDrop?.SetValue(Array.IndexOf(ExchangeCategoryInfo.MenuOrder, c));
                p.Refresh();
            }
            return p;
        }

        // ══════════════════ 建構（只跑一次）══════════════════

        protected override void OnBuild()
        {
            _tile = L("ExchangePanel_Tile");
            _field = L("ExchangePanel_Field");

            var bg = L("ExchangePanel_Bg");
            var frameImg = UIBuilder.Image(transform, "Frame", bg, bg != null ? Color.white : Fallback);
            frameImg.raycastTarget = true;   // 擋住點擊不穿到遊戲
            _frame = frameImg.rectTransform;
            UIBuilder.Center(_frame, FrameW, FrameH);
            _frame.localScale = new Vector3(FrameScale, FrameScale, 1f);

            BuildHeader();
            BuildToolbar();
            BuildResults();
            BuildFooter();

            _tip = ItemTooltip.Create(transform);
        }

        void BuildHeader()
        {
            var title = Txt(_frame, "Title", Language.GetText(TxtTitle), 56, Gold, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            PlaceTL(title.rectTransform, 80f, HeadY + 10f, 320f, HeadH - 20f);

            // 搜尋框：輸入條底圖＋放大鏡＋InputField（打字即篩）
            var box = Sliced(_frame, "SearchBox", _field);
            PlaceTL(box.rectTransform, 420f, HeadY + 27f, SearchW, 56f);
            var mag = UIBuilder.Image(box.transform, "Icon", L("ExchangePanel_Icon_Search"));
            mag.preserveAspect = true; mag.raycastTarget = false;
            PlaceTL(mag.rectTransform, 22f, 8f, 40f, 40f);

            _search = UIBuilder.InputField(box.transform, "Input", Language.GetText(TxtSearch), 26, 20, new Color(0f, 0f, 0f, 0f));
            UIBuilder.Stretch((RectTransform)_search.transform, 70f, 26f, 4f, 4f);
            _search.textComponent.color = TextLight;
            _search.onValueChanged.AddListener(s => { _query = s ?? ""; Refresh(); });

            // 錢包（2026-11-16）：功德值＋B／A／S 證明四組並排（順序 ExchangeCurrencies.WalletOrder）。
            // 功德值數字可能很長、給寬一點並自動縮字；證明一輩子最多幾十張，給窄格。
            float x = WalletX0;
            foreach (var key in ExchangeCurrencies.WalletOrder)
            {
                var cur = ExchangeCurrencies.Get(key);
                if (cur == null) continue;
                bool merit = key == ExchangeCurrencies.Merit;
                float icon = merit ? 48f : 38f, textW = merit ? 130f : 40f;
                var ic = UIBuilder.Image(_frame, "WalletIcon_" + key, null, Color.white);
                ic.raycastTarget = false;
                PlaceCenter(ic.rectTransform, x, HeadY + 55f - icon * 0.5f, icon, icon);
                if (cur.IconItemId > 0) ItemIcons.Apply(ic, cur.IconItemId);

                var t = Txt(_frame, "Wallet_" + key, "0", merit ? 36 : 32, Gold, TextAnchor.MiddleLeft);
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 16; t.resizeTextMaxSize = merit ? 36 : 32;
                PlaceTL(t.rectTransform, x + icon + 4f, HeadY + 22f, textW, 66f);
                _walletTexts[key] = t;

                float groupW = icon + 4f + textW;
                var hover = UIBuilder.Image(_frame, "WalletHover_" + key, null, new Color(0f, 0f, 0f, 0f));
                hover.raycastTarget = true;
                PlaceTL(hover.rectTransform, x, HeadY + 22f, groupW, 66f);
                var c = cur;
                HoverTip.Attach(hover.gameObject, () => c.Name);
                x += groupW + WalletGap;
            }

            // 關閉鈕：標題帶右端（原本放在外框右上角外面、壓在角飾上，作者 2026-11-16 要求下移）
            var cb = UIBuilder.Button(_frame, "Close", "", () => UIManager.Instance.Close(this), Color.white,
                                      UIBuilder.LoadSprite("UI/Common/CloseBtn_normal"));
            var cbImg = cb.GetComponent<Image>();
            cbImg.preserveAspect = true;
            cb.targetGraphic = cbImg;   // 程式建按鈕要手動指（PROBLEMS D4）
            cb.transition = Selectable.Transition.SpriteSwap;
            var ss = cb.spriteState;
            ss.pressedSprite = UIBuilder.LoadSprite("UI/Common/CloseBtn_pressed");
            cb.spriteState = ss;
            PlaceTL((RectTransform)cb.transform, 1340f, HeadY + 14f, 82f, 82f);
        }

        void BuildToolbar()
        {
            float x = BodyX;

            // 分類下拉
            var catOpts = new List<Func<string>>();
            foreach (var c in ExchangeCategoryInfo.MenuOrder) { var cc = c; catOpts.Add(() => ExchangeCategoryInfo.Name(cc)); }
            _catDrop = new DropMenu(this, "Category", x, ToolY, 230f, ToolH, catOpts, i =>
            {
                _cat = ExchangeCategoryInfo.MenuOrder[Mathf.Clamp(i, 0, ExchangeCategoryInfo.MenuOrder.Length - 1)];
                Refresh();
            });
            x += 230f + 16f;

            // 稀有度：「全部」＋四色（可複選；全部不選＝全部）
            ChipButton("RarityAll", x, 84f, null, Language.GetText(TxtAll), () => { _rarities.Clear(); Refresh(); }, out _rarityAllOutline);
            x += 84f + 6f;
            foreach (var r in RarityInfo.All)
            {
                var rr = r;
                var b = ChipButton("Rarity" + r, x, 70f, RarityInfo.ColorOf(r), RarityInfo.ColorName(r), () =>
                {
                    if (!_rarities.Remove(rr)) _rarities.Add(rr);
                    if (_rarities.Count == RarityInfo.All.Length) _rarities.Clear();   // 四個全選 ＝ 全部
                    Refresh();
                }, out var ol);
                _rarityOutlines[r] = ol;
                HoverTip.Attach(b.gameObject, () => RarityInfo.Label(rr));
                x += 70f + 6f;
            }
            x += 14f;

            // 只看買得起
            ChipButton("Affordable", x, 150f, null, Language.GetText(TxtAffordable), () => { _affordOnly = !_affordOnly; Refresh(); }, out _affordOutline);
            x += 150f + 10f;

            // 只看收藏（星星）
            var fav = IconButton("FavOnly", x, ToolY, ToolH, L("ExchangePanel_Icon_Star"), () => { _favOnly = !_favOnly; Refresh(); }, out _favOutline);
            HoverTip.Attach(fav.gameObject, TxtFavOnly);
            x += ToolH + 16f;

            // 排序下拉
            var sortOpts = new List<Func<string>>();
            for (int i = 0; i < 6; i++) { int id = TxtSortFirst + i; sortOpts.Add(() => Language.GetText(id)); }
            _sortDrop = new DropMenu(this, "Sort", x, ToolY, 260f, ToolH, sortOpts, i => { _sort = (SortMode)i; Refresh(); }, L("ExchangePanel_Icon_Sort"));

            // 三種檢視（靠右）
            float vx = BodyR - ToolH * 3f - 8f * 2f;
            string[] icons = { "ExchangePanel_Icon_List", "ExchangePanel_Icon_Grid", "ExchangePanel_Icon_Card" };
            int[] tips = { TxtViewList, TxtViewThumb, TxtViewCard };
            for (int i = 0; i < 3; i++)
            {
                var mode = (ViewMode)i;
                var b = IconButton("View" + mode, vx + i * (ToolH + 8f), ToolY, ToolH, L(icons[i]), () => { _view = mode; Refresh(); }, out _viewOutlines[i]);
                HoverTip.Attach(b.gameObject, tips[i]);
            }
        }

        void BuildResults()
        {
            var go = UIBuilder.Create("Results", _frame);
            var rt = UIBuilder.Rect(go);
            PlaceTL(rt, BodyX, ResultsY, BodyR - BodyX, ResultsH);

            // 視窗（遮罩＋接滾輪的透明底）
            var vp = UIBuilder.Image(go.transform, "Viewport", null, new Color(0f, 0f, 0f, 0f));
            vp.raycastTarget = true;
            vp.gameObject.AddComponent<RectMask2D>();
            UIBuilder.Stretch(vp.rectTransform, 0f, ScrollbarW + 6f, 0f, 0f);

            var content = UIBuilder.Create("Content", vp.transform);
            _content = UIBuilder.Rect(content);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;
            _grid = content.AddComponent<GridLayoutGroup>();
            _grid.padding = new RectOffset((int)GridPad, (int)GridPad, (int)GridPad, (int)GridPad);
            _grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            _grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            _grid.childAlignment = TextAnchor.UpperLeft;     // 物品少時從左邊排起（左右對稱靠 Rebuild 算 padding，不靠置中）
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            var fit = content.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 捲軸（細銅條）
            var sbGO = UIBuilder.Create("Scrollbar", go.transform);
            var sbRt = UIBuilder.Rect(sbGO);
            sbRt.anchorMin = new Vector2(1f, 0f); sbRt.anchorMax = new Vector2(1f, 1f); sbRt.pivot = new Vector2(1f, 0.5f);
            sbRt.anchoredPosition = Vector2.zero; sbRt.sizeDelta = new Vector2(ScrollbarW, 0f);
            var track = sbGO.AddComponent<Image>();
            track.color = new Color(0.05f, 0.04f, 0.03f, 0.9f);
            var slide = UIBuilder.Create("Sliding", sbGO.transform);
            UIBuilder.Stretch(UIBuilder.Rect(slide));
            var handle = UIBuilder.Image(slide.transform, "Handle", null, new Color(0.55f, 0.4f, 0.24f, 1f));
            UIBuilder.Stretch(handle.rectTransform);
            var sb = sbGO.AddComponent<Scrollbar>();
            sb.handleRect = handle.rectTransform;
            sb.targetGraphic = handle;
            sb.direction = Scrollbar.Direction.BottomToTop;

            _scroll = go.AddComponent<ScrollRect>();
            _scroll.viewport = vp.rectTransform;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;
            _scroll.verticalScrollbar = sb;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            _emptyText = Txt(go.transform, "Empty", Language.GetText(TxtEmpty), 32, TextDim, TextAnchor.MiddleCenter);
            UIBuilder.Stretch(_emptyText.rectTransform);
            _emptyText.gameObject.SetActive(false);
        }

        void BuildFooter()
        {
            _countText = Txt(_frame, "Count", "", 26, TextDim, TextAnchor.MiddleLeft);
            PlaceTL(_countText.rectTransform, BodyX + 4f, FootY, 500f, FootH);

            var reset = UIBuilder.Button(_frame, "Reset", "", ResetFilters, Color.white, _field);
            var img = reset.GetComponent<Image>();
            if (_field != null) img.type = Image.Type.Sliced; else img.color = Fallback;
            reset.targetGraphic = img;
            PlaceTL((RectTransform)reset.transform, BodyR - 200f, FootY - 4f, 200f, FootH + 8f);
            var t = Txt(reset.transform, "Label", Language.GetText(TxtReset), 22, TextLight, TextAnchor.MiddleCenter);
            UIBuilder.Stretch(t.rectTransform);
        }

        // ══════════════════ 開關 ══════════════════

        protected override void OnOpen()
        {
            ExchangeCurrencies.Subscribe(OnCurrencyChanged);
            UpdateWallet();
            Refresh();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        protected override void OnClose()
        {
            ExchangeCurrencies.Unsubscribe(OnCurrencyChanged);
            _catDrop?.Close();
            _sortDrop?.Close();
            SetHovered(null, null);
            _tip?.Hide();
        }

        void OnCurrencyChanged()
        {
            UpdateWallet();
            Refresh();   // 「買得起」與價格紅字要跟著變
        }

        void UpdateWallet()
        {
            foreach (var kv in _walletTexts)
            {
                var cur = ExchangeCurrencies.Get(kv.Key);
                if (kv.Value != null) kv.Value.text = cur != null ? cur.HeldNow.ToString("N0") : "0";
            }
        }

        void Update()
        {
            if (_tip != null) _tip.Follow();
            if (_dirty) { _dirty = false; Rebuild(); }
        }

        void ResetFilters()
        {
            _cat = ExchangeCategory.All;
            _rarities.Clear();
            _affordOnly = _favOnly = false;
            _sort = SortMode.Recommended;
            _catDrop?.SetValue(0);
            _sortDrop?.SetValue(0);
            if (_search != null) _search.text = "";   // 會觸發 onValueChanged → _query 清空
            _query = "";
            Refresh();
        }

        /// <summary>條件變了：先標記，下一幀重建（同一幀內連續改好幾個條件只重建一次）。</summary>
        public void Refresh()
        {
            _dirty = true;
            UpdateToolbarState();
        }

        void UpdateToolbarState()
        {
            if (_rarityAllOutline != null) _rarityAllOutline.gameObject.SetActive(_rarities.Count == 0);
            foreach (var kv in _rarityOutlines) kv.Value.gameObject.SetActive(_rarities.Contains(kv.Key));
            if (_affordOutline != null) _affordOutline.gameObject.SetActive(_affordOnly);
            if (_favOutline != null) _favOutline.gameObject.SetActive(_favOnly);
            for (int i = 0; i < _viewOutlines.Length; i++)
                if (_viewOutlines[i] != null) _viewOutlines[i].gameObject.SetActive((int)_view == i);
        }

        // ══════════════════ 篩選＋排序 ══════════════════

        void Collect()
        {
            _shown.Clear();
            string q = (_query ?? "").Trim();
            foreach (var e in ExchangeTable.All)
            {
                if (_cat != ExchangeCategory.All && e.Category != _cat) continue;
                if (_rarities.Count > 0 && !_rarities.Contains(e.Rarity)) continue;
                if (q.Length > 0 && e.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (_affordOnly && !ExchangeService.CanAfford(e)) continue;
                if (_favOnly && !ExchangeService.IsFavorite(e)) continue;
                _shown.Add(e);
            }

            Comparison<ExchangeEntry> byOrder = (a, b) => a.SortOrder != b.SortOrder ? a.SortOrder.CompareTo(b.SortOrder) : a.ExchangeId.CompareTo(b.ExchangeId);
            switch (_sort)
            {
                case SortMode.PriceAsc:
                    _shown.Sort((a, b) => { int c = a.PriceSortKey.CompareTo(b.PriceSortKey); return c != 0 ? c : byOrder(a, b); }); break;
                case SortMode.PriceDesc:
                    _shown.Sort((a, b) => { int c = b.PriceSortKey.CompareTo(a.PriceSortKey); return c != 0 ? c : byOrder(a, b); }); break;
                case SortMode.RarityDesc:
                    _shown.Sort((a, b) => { int c = b.Rarity.CompareTo(a.Rarity); return c != 0 ? c : byOrder(a, b); }); break;
                case SortMode.RarityAsc:
                    _shown.Sort((a, b) => { int c = a.Rarity.CompareTo(b.Rarity); return c != 0 ? c : byOrder(a, b); }); break;
                case SortMode.Name:
                    _shown.Sort((a, b) => { int c = string.Compare(a.Name, b.Name, StringComparison.CurrentCulture); return c != 0 ? c : byOrder(a, b); }); break;
                default:
                    _shown.Sort(byOrder); break;
            }
        }

        // ══════════════════ 重建結果區 ══════════════════

        void Rebuild()
        {
            Collect();
            SetHovered(null, null);
            _tip?.Hide();

            // 舊的先關掉再刪：Destroy 要到這一幀結尾才生效，關掉才不會被這一幀的版面算進去
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var c = _content.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }

            float innerW = ((RectTransform)_scroll.viewport).rect.width - GridPad * 2f;
            switch (_view)
            {
                case ViewMode.List:
                    _grid.cellSize = new Vector2(Mathf.Max(200f, innerW), ListRowH);
                    _grid.spacing = new Vector2(0f, ListGap);
                    _grid.constraintCount = 1;
                    break;
                case ViewMode.Thumb:
                    _grid.cellSize = new Vector2(ThumbW, ThumbH);
                    _grid.spacing = new Vector2(ThumbGap, ThumbGap);
                    _grid.constraintCount = Mathf.Max(1, Mathf.FloorToInt((innerW + ThumbGap) / (ThumbW + ThumbGap)));
                    break;
                default:
                    _grid.cellSize = new Vector2(CardW, CardH);
                    _grid.spacing = new Vector2(CardGap, CardGap);
                    _grid.constraintCount = Mathf.Max(1, Mathf.FloorToInt((innerW + CardGap) / (CardW + CardGap)));
                    break;
            }

            // 左右留白對稱：一列「排得下的寬度」算出來後，剩下的空間平分到兩側當 padding。
            // ⚠ 不要用 childAlignment=UpperCenter 達成對稱——那會讓物品少的時候整排擠到中間（2026-11-16 作者截圖：護身符／戒指／道具）。
            {
                float vpW = ((RectTransform)_scroll.viewport).rect.width;
                int cols = _grid.constraintCount;
                float usedW = cols * _grid.cellSize.x + (cols - 1) * _grid.spacing.x;
                int side = Mathf.Max(0, Mathf.FloorToInt((vpW - usedW) * 0.5f));
                _grid.padding = new RectOffset(side, side, (int)GridPad, (int)GridPad);
            }

            for (int i = 0; i < _shown.Count; i++)
            {
                switch (_view)
                {
                    case ViewMode.List: BuildListRow(_shown[i]); break;
                    case ViewMode.Thumb: BuildThumb(_shown[i]); break;
                    default: BuildCard(_shown[i]); break;
                }
            }

            _countText.text = string.Format(Language.GetText(TxtCount), _shown.Count);
            _emptyText.gameObject.SetActive(_shown.Count == 0);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        // ── 列表：一列一件 ──
        void BuildListRow(ExchangeEntry e)
        {
            var root = EntryRoot(e, "Row", out var view);
            var rc = RarityInfo.ColorOf(e.Rarity);

            var bar = UIBuilder.Image(root, "RarityBar", null, rc);
            bar.raycastTarget = false;
            PlaceTL(bar.rectTransform, 6f, 8f, 6f, ListRowH - 16f);

            Icon(root, e, 22f, 6f, 60f);

            var name = Txt(root, "Name", e.Name, 30, rc, TextAnchor.MiddleLeft);
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 18; name.resizeTextMaxSize = 30;   // 長名字縮字，不擠到右邊欄位
            name.verticalOverflow = VerticalWrapMode.Truncate;
            PlaceTL(name.rectTransform, 100f, 4f, 520f, ListRowH - 8f);

            var cat = Txt(root, "Cat", ExchangeCategoryInfo.Name(e.Category) + "・" + e.GradeLabel, 22, TextDim, TextAnchor.MiddleLeft);
            PlaceTL(cat.rectTransform, 640f, 0f, 220f, ListRowH);

            int owned = OwnedCount(e);
            var own = Txt(root, "Owned", owned > 0 ? string.Format(Language.GetText(TxtOwned), owned) : "", 22, TextDim, TextAnchor.MiddleLeft);
            PlaceTL(own.rectTransform, 870f, 0f, 150f, ListRowH);

            float w = _grid.cellSize.x;
            PriceRow(root, e, w - 380f, 14f, 290f, 44f, 30, TextAnchor.MiddleRight);
            HookFavorite(view, e, FavMark(root, e, w - 64f, 14f, 44f, true), true);
        }

        // ── 縮圖：方格＋價格 ──
        void BuildThumb(ExchangeEntry e)
        {
            var root = EntryRoot(e, "Thumb", out var view, ThumbW);   // 方框只畫上面 120×120，價格寫在框下
            Icon(root, e, 12f, 12f, ThumbW - 24f);
            HookFavorite(view, e, FavMark(root, e, ThumbW - 34f, 6f, 28f, false), false);   // 縮圖格子小：沒收藏就不畫星
            PriceRow(root, e, 0f, ThumbW + 2f, ThumbW, ThumbH - ThumbW - 2f, 22, TextAnchor.MiddleCenter);
        }

        // ── 大圖：卡片，說明直接寫在下面 ──
        void BuildCard(ExchangeEntry e)
        {
            var root = EntryRoot(e, "Card", out var view, 0f, false);   // 大圖不跳 tooltip（說明已經在卡上）
            var rc = RarityInfo.ColorOf(e.Rarity);

            var bar = UIBuilder.Image(root, "RarityBar", null, rc);
            bar.raycastTarget = false;
            PlaceTL(bar.rectTransform, 14f, 12f, CardW - 28f, 5f);

            Icon(root, e, (CardW - 190f) / 2f, 26f, 190f);
            HookFavorite(view, e, FavMark(root, e, CardW - 58f, 22f, 40f, true), true);

            var name = Txt(root, "Name", e.Name, 28, rc, TextAnchor.MiddleCenter);
            name.fontStyle = FontStyle.Bold;
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 18; name.resizeTextMaxSize = 28;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            PlaceTL(name.rectTransform, 14f, 222f, CardW - 28f, 40f);

            var sub = Txt(root, "Sub", e.RarityLabel + "　" + ExchangeCategoryInfo.Name(e.Category), 20, TextDim, TextAnchor.MiddleCenter);
            PlaceTL(sub.rectTransform, 14f, 262f, CardW - 28f, 26f);

            string desc = e.Item.TipStats;
            if (!string.IsNullOrEmpty(e.Item.TipLore)) desc = (string.IsNullOrEmpty(desc) ? "" : desc + "\n") + e.Item.TipLore;
            if (string.IsNullOrEmpty(desc)) desc = e.Item.Description;
            var d = Txt(root, "Desc", desc ?? "", 19, TextLight, TextAnchor.UpperLeft);
            d.resizeTextForBestFit = true; d.resizeTextMinSize = 13; d.resizeTextMaxSize = 19;
            d.verticalOverflow = VerticalWrapMode.Truncate;
            d.lineSpacing = 1.05f;
            PlaceTL(d.rectTransform, 22f, 294f, CardW - 44f, 100f);

            PriceRow(root, e, 14f, CardH - 46f, CardW - 28f, 36f, 26, TextAnchor.MiddleCenter);
        }

        // ══════════════════ 品項的共用零件 ══════════════════

        /// <summary>一個品項的根：格框底圖＋稀有度細框＋hover 高亮＋事件。<paramref name="frameH"/>＝0 表示框貼滿整格。</summary>
        RectTransform EntryRoot(ExchangeEntry e, string name, out ExchangeEntryView view, float frameH = 0f, bool tooltip = true)
        {
            var go = UIBuilder.Create(name + e.ExchangeId, _content);
            var root = UIBuilder.Rect(go);

            // 透明底接滑鼠（縮圖的價格區也要能點）
            var hit = go.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var frame = Sliced(root, "Frame", _tile);
            frame.raycastTarget = false;
            var frt = frame.rectTransform;
            if (frameH > 0f)
            {
                frt.anchorMin = new Vector2(0f, 1f); frt.anchorMax = new Vector2(1f, 1f); frt.pivot = new Vector2(0.5f, 1f);
                frt.anchoredPosition = Vector2.zero; frt.sizeDelta = new Vector2(0f, frameH);
            }
            else UIBuilder.Stretch(frt);

            var rarity = SlotOutline.Create(frame.transform, "Rarity", RarityInfo.ColorOf(e.Rarity), 2f);
            UIBuilder.Stretch(rarity, 3f, 3f, 3f, 3f);

            var hl = SlotOutline.Create(frame.transform, "Hover", HoverLine, 3f);
            hl.gameObject.SetActive(false);

            view = go.AddComponent<ExchangeEntryView>();
            view.OnEnter = () => SetHovered(e, hl, tooltip);
            view.OnExit = () => { if (_hovered == e) SetHovered(null, null); };
            view.OnClick = () => ExchangeDetailPopup.Show(e);
            // 大圖不跳物品 tooltip，右鍵收藏的說明改用一行小提示
            if (!tooltip) HoverTip.Attach(go, () => RightClickHint(e));
            return root;
        }

        void SetHovered(ExchangeEntry e, RectTransform outline, bool tooltip = true)
        {
            if (_hoverOutline != null) _hoverOutline.gameObject.SetActive(false);
            _hovered = e;
            _hoverOutline = outline;
            if (_hoverOutline != null) _hoverOutline.gameObject.SetActive(true);

            _tipFor = (e != null && tooltip) ? e : null;
            if (_tip == null) return;
            if (_tipFor == null) { _tip.Hide(); return; }
            _tip.Show(e.ItemId, TipExtra(e), e.Name, RarityInfo.ColorOf(e.Rarity));   // 名稱用兌換表的品質（東西還沒產生，沒有實例）
        }

        /// <summary>tooltip 在物品說明後面補的三行：稀有度＋分類、價格（買不起標紅）、右鍵收藏的操作提示。</summary>
        static string TipExtra(ExchangeEntry e)
        {
            string price = e.CurrenciesAvailable ? ExchangeService.CostText(e.Cost) : Language.GetText(TxtNotOpen);
            if (!ExchangeService.CanAfford(e)) price = $"<color=#E0614F>{price}</color>";
            string rolled = (e.Item != null && Dipan.Inventory.ItemManager.NeedsInstance(e.Item))
                ? $"<color=#9E998C>{Language.GetText(TxtRolledOnExchange)}</color>\n" : "";   // 裝備的效果／孔數、珠子的等級兌換時才骰
            return rolled + $"{Language.GetText(TxtRarity)}：<color=#{ColorUtility.ToHtmlStringRGB(RarityInfo.ColorOf(e.Rarity))}>{e.RarityLabel}</color>　{ExchangeCategoryInfo.Name(e.Category)}\n"
                 + $"{Language.GetText(TxtPrice)}：{price}\n"
                 + $"<color=#9E998C>{RightClickHint(e)}</color>";
        }

        /// <summary>依目前狀態：「右鍵：加入收藏」或「右鍵：取消收藏」。</summary>
        static string RightClickHint(ExchangeEntry e)
            => Language.GetText(ExchangeService.IsFavorite(e) ? TxtRightFavDel : TxtRightFavAdd);

        void Icon(RectTransform parent, ExchangeEntry e, float x, float y, float size)
        {
            var img = UIBuilder.Image(parent, "Icon", null, Color.white);
            img.raycastTarget = false;
            PlaceCenter(img.rectTransform, x, y, size, size);   // ⚠ 一定要置中 pivot，見 PlaceCenter 的說明
            ItemIcons.Apply(img, e.ItemId);   // 唯一入口：血統藥劑的疊圖、IconFit 正規化都在裡面
        }

        /// <summary>價格：每種貨幣一組「圖示＋數字」橫排；買不起的數字標紅；貨幣未開放顯示「尚未開放」。</summary>
        void PriceRow(RectTransform parent, ExchangeEntry e, float x, float y, float w, float h, int fontSize, TextAnchor align)
        {
            var go = UIBuilder.Create("Price", parent);
            var rt = UIBuilder.Rect(go);
            PlaceTL(rt, x, y, w, h);
            var hl = go.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = align;
            hl.spacing = 4f;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;

            if (!e.CurrenciesAvailable)
            {
                var t = Txt(go.transform, "NA", Language.GetText(TxtNotOpen), fontSize, TextDim, TextAnchor.MiddleCenter);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                return;
            }

            bool afford = ExchangeService.CanAfford(e);
            float iconSize = fontSize * 1.25f;
            for (int i = 0; i < e.Cost.Count; i++)
            {
                var c = e.Cost[i].Currency;
                if (c.IconItemId > 0)
                {
                    // 版面群組會改子物件的大小，而 IconFit 也要改圖的大小——所以圖包一層容器：
                    // 容器給版面群組排、圖在容器裡由 IconFit 正規化，兩邊互不打架。
                    var holder = UIBuilder.Create("Cur", go.transform);
                    var hrt = UIBuilder.Rect(holder);
                    hrt.sizeDelta = new Vector2(iconSize, iconSize);
                    var le = holder.AddComponent<LayoutElement>();
                    le.preferredWidth = le.minWidth = iconSize;
                    le.preferredHeight = le.minHeight = iconSize;
                    var ic = UIBuilder.Image(holder.transform, "Icon", null, Color.white);
                    ic.raycastTarget = false;
                    var irt = ic.rectTransform;
                    irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
                    irt.anchoredPosition = Vector2.zero;
                    irt.sizeDelta = new Vector2(iconSize, iconSize);
                    ItemIcons.Apply(ic, c.IconItemId);
                }
                var t = Txt(go.transform, "Amt", e.Cost[i].Amount.ToString("N0") + (c.IconItemId > 0 ? "" : " " + c.Name),
                            fontSize, afford ? Gold : TextBad, TextAnchor.MiddleLeft);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
        }

        static readonly Color FavOffColor = new Color(0.35f, 0.32f, 0.28f, 0.85f);

        /// <summary>
        /// 收藏標記（**只是標記、不能點**——收藏一律用右鍵，2026-11-16 作者定）：亮＝已收藏；
        /// 沒收藏時 <paramref name="showWhenOff"/> 為 true 畫暗星（列表、大圖），false 不畫（縮圖格子小）。
        /// </summary>
        Image FavMark(RectTransform parent, ExchangeEntry e, float x, float y, float size, bool showWhenOff)
        {
            var img = UIBuilder.Image(parent, "Fav", L("ExchangePanel_Icon_Star"));
            img.preserveAspect = true;
            img.raycastTarget = false;
            PlaceTL(img.rectTransform, x, y, size, size);
            PaintFav(img, e, showWhenOff);
            return img;
        }

        static void PaintFav(Image img, ExchangeEntry e, bool showWhenOff)
        {
            if (img == null) return;
            bool on = ExchangeService.IsFavorite(e);
            img.enabled = on || showWhenOff;
            img.color = on ? Color.white : FavOffColor;
        }

        /// <summary>右鍵＝切換收藏（再按一次取消）。所有檢視模式共用。</summary>
        void HookFavorite(ExchangeEntryView view, ExchangeEntry e, Image mark, bool showWhenOff)
        {
            view.OnRightClick = () =>
            {
                ExchangeService.ToggleFavorite(e);
                if (_favOnly) { Refresh(); return; }               // 「只看收藏」開著：取消的要從清單消失
                PaintFav(mark, e, showWhenOff);
                if (_tipFor == e && _tip != null) _tip.Show(e.ItemId, TipExtra(e), e.Name, RarityInfo.ColorOf(e.Rarity));   // 提示行改成另一句
            };
        }

        static int OwnedCount(ExchangeEntry e)
        {
            var inv = Dipan.Inventory.InventorySystem.Instance;
            return inv != null ? inv.CountOf(e.ItemId) : 0;
        }

        /// <summary>給詳情彈窗兌換成功後呼叫：持有數、買得起與否都要重畫。</summary>
        public void NotifyChanged() => Refresh();

        // ══════════════════ 小工具 ══════════════════

        /// <summary>錨在父物件左上角、以左上為 pivot：(x,y) 就是底圖像素座標的左上角。</summary>
        internal static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>
        /// 同 <see cref="PlaceTL"/> 的座標（左上角＋寬高），但 **pivot 在中心**。
        /// **要交給 <c>ItemIcons.Apply</c>（IconFit）的圖一律用這個**：IconFit 會把 rect 放大、再用 anchoredPosition
        /// 把不透明內容拉回方框中心——這套算式假設 pivot 在中心。pivot 在左上的話，放大的部分全部往右下長，
        /// 透明留白越多的圖（能力珠、藥瓶）偏越多、還會溢出格子（2026-11-15 作者截圖）。
        /// </summary>
        internal static void PlaceCenter(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + w * 0.5f, -(y + h * 0.5f));
            rt.sizeDelta = new Vector2(w, h);
        }

        internal static Image Sliced(Transform parent, string name, Sprite s)
        {
            var img = UIBuilder.Image(parent, name, s, s != null ? Color.white : Fallback);
            if (s != null) img.type = Image.Type.Sliced;
            return img;
        }

        internal static Text Txt(Transform parent, string name, string content, int size, Color c, TextAnchor a)
        {
            var t = UIBuilder.Text(parent, name, content, size, c, a);
            t.supportRichText = true;
            return t;
        }

        /// <summary>工具列上的文字／色塊開關鈕（格框底＋選中時金框）。</summary>
        Button ChipButton(string name, float x, float w, Color? diamond, string label, UnityAction onClick, out RectTransform outline)
        {
            var b = UIBuilder.Button(_frame, name, "", onClick, Color.white, _tile);
            var img = b.GetComponent<Image>();
            if (_tile != null) img.type = Image.Type.Sliced; else img.color = Fallback;
            b.targetGraphic = img;
            var rt = (RectTransform)b.transform;
            PlaceTL(rt, x, ToolY, w, ToolH);

            float textX = 10f;
            if (diamond.HasValue)
            {
                var dm = UIBuilder.Image(b.transform, "Diamond", null, diamond.Value);
                dm.raycastTarget = false;
                var drt = dm.rectTransform;
                drt.anchorMin = drt.anchorMax = new Vector2(0f, 0.5f);
                drt.pivot = new Vector2(0.5f, 0.5f);
                drt.anchoredPosition = new Vector2(22f, 0f);
                drt.sizeDelta = new Vector2(14f, 14f);
                drt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                textX = 32f;
            }
            var t = Txt(b.transform, "Label", label, 24, TextLight, diamond.HasValue ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            UIBuilder.Stretch(t.rectTransform, textX, 6f, 0f, 0f);

            outline = SlotOutline.Create(b.transform, "On", OutlineOn, 3f);
            outline.gameObject.SetActive(false);
            return b;
        }

        /// <summary>方形圖示鈕（格框底＋圖示＋選中時金框）。</summary>
        Button IconButton(string name, float x, float y, float size, Sprite icon, UnityAction onClick, out RectTransform outline)
        {
            var b = UIBuilder.Button(_frame, name, "", onClick, Color.white, _tile);
            var img = b.GetComponent<Image>();
            if (_tile != null) img.type = Image.Type.Sliced; else img.color = Fallback;
            b.targetGraphic = img;
            PlaceTL((RectTransform)b.transform, x, y, size, size);

            var ic = UIBuilder.Image(b.transform, "Icon", icon, Color.white);
            ic.preserveAspect = true; ic.raycastTarget = false;
            UIBuilder.Stretch(ic.rectTransform, size * 0.2f, size * 0.2f, size * 0.2f, size * 0.2f);

            outline = SlotOutline.Create(b.transform, "On", OutlineOn, 3f);
            outline.gameObject.SetActive(false);
            return b;
        }

        // ══════════════════ 下拉選單（程式建；uGUI 內建 Dropdown 要 template，程式建很囉嗦）══════════════════

        sealed class DropMenu
        {
            const float RowH = 50f;

            readonly ExchangePanel _owner;
            readonly List<Func<string>> _options;
            readonly Action<int> _onChange;
            readonly Text _label;
            readonly float _x, _y, _w, _h;
            RectTransform _popup;   // 開著時才存在：全螢幕擋板＋選項清單
            int _value;

            public DropMenu(ExchangePanel owner, string name, float x, float y, float w, float h,
                            List<Func<string>> options, Action<int> onChange, Sprite leadIcon = null)
            {
                _owner = owner; _options = options; _onChange = onChange;
                _x = x; _y = y; _w = w; _h = h;

                var b = UIBuilder.Button(owner._frame, name, "", Toggle, Color.white, owner._field);
                var img = b.GetComponent<Image>();
                if (owner._field != null) img.type = Image.Type.Sliced; else img.color = Fallback;
                b.targetGraphic = img;
                PlaceTL((RectTransform)b.transform, x, y, w, h);

                float lx = 36f;   // 輸入條左端有尖角（九宮格邊界 24），文字要讓開
                if (leadIcon != null)
                {
                    var ic = UIBuilder.Image(b.transform, "Icon", leadIcon);
                    ic.preserveAspect = true; ic.raycastTarget = false;
                    PlaceTL(ic.rectTransform, 36f, (h - 30f) / 2f, 30f, 30f);   // 36：讓開左端尖角
                    lx = 74f;
                }
                _label = Txt(b.transform, "Label", "", 24, TextLight, TextAnchor.MiddleLeft);
                UIBuilder.Stretch(_label.rectTransform, lx, 40f, 0f, 0f);
                var arrow = Txt(b.transform, "Arrow", "▾", 26, Gold, TextAnchor.MiddleCenter);
                arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
                arrow.rectTransform.anchorMax = new Vector2(1f, 1f);
                arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
                arrow.rectTransform.anchoredPosition = new Vector2(-16f, 0f);
                arrow.rectTransform.sizeDelta = new Vector2(26f, 0f);
                SetValue(0);
            }

            public void SetValue(int i)
            {
                _value = Mathf.Clamp(i, 0, _options.Count - 1);
                if (_label != null) _label.text = _options[_value]();
            }

            void Toggle()
            {
                if (_popup != null) { Close(); return; }
                Open();
            }

            void Open()
            {
                // 擋板：大到蓋住整個畫面（外框有縮放，用很大的負邊距最省事），點清單以外就收起來
                var blocker = UIBuilder.Button(_owner._frame, "DropBlocker", "", Close, new Color(0f, 0f, 0f, 0.001f));
                _popup = (RectTransform)blocker.transform;
                UIBuilder.Stretch(_popup, -3000f, -3000f, -3000f, -3000f);
                _popup.SetAsLastSibling();

                // 清單：貼在下拉鈕正下方（座標轉回外框座標：擋板比外框大 3000）
                var list = UIBuilder.Image(_popup, "List", null, new Color(0.06f, 0.05f, 0.035f, 0.98f));
                list.raycastTarget = true;
                PlaceTL(list.rectTransform, _x + 3000f, _y + _h + 4f + 3000f, _w, RowH * _options.Count + 8f);
                SlotOutline.Create(list.transform, "Border", new Color(0.55f, 0.4f, 0.24f), 2f);

                for (int i = 0; i < _options.Count; i++)
                {
                    int idx = i;
                    var row = UIBuilder.Button(list.transform, "Opt" + i, "", () => { SetValue(idx); Close(); _onChange?.Invoke(idx); },
                                               new Color(1f, 1f, 1f, 0f));
                    var rimg = row.GetComponent<Image>();
                    row.targetGraphic = rimg;
                    var cb = row.colors;
                    cb.normalColor = new Color(1f, 1f, 1f, 0f);
                    cb.highlightedColor = new Color(0.55f, 0.4f, 0.24f, 0.45f);
                    cb.pressedColor = new Color(0.55f, 0.4f, 0.24f, 0.7f);
                    cb.selectedColor = cb.normalColor;
                    row.colors = cb;
                    PlaceTL((RectTransform)row.transform, 4f, 4f + i * RowH, _w - 8f, RowH);
                    var t = Txt(row.transform, "Label", _options[i](), 24, i == _value ? Gold : TextLight, TextAnchor.MiddleLeft);
                    UIBuilder.Stretch(t.rectTransform, 22f, 10f, 0f, 0f);
                }
            }

            public void Close()
            {
                if (_popup == null) return;
                UnityEngine.Object.Destroy(_popup.gameObject);
                _popup = null;
            }
        }
    }
}
