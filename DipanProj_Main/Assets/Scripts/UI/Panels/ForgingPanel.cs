using UnityEngine;
using UnityEngine.UI;
using Dipan.Inventory;
using Dipan.Localization;

namespace Dipan.UI
{
    /// <summary>
    /// 鍛造面板（Y 鍵開關；將來改由鐵匠 NPC 互動開啟）。
    ///
    /// 版面＝一張整圖底 ForgingPanel_Bg（1536×1024）＋在上面疊互動格子，座標全部在「底圖原生像素空間」，
    /// 整個 frame 等比縮放塞進畫面——與 InventoryPanel 同一套作法。
    ///
    /// 內容：
    /// - 鐵砧中央一格：玩家從背包拖武器／裝備上來。**這是「借放」不是「搬移」**——物品一直留在背包原位，
    ///   鐵砧只記住它在哪一格，背包那一格會被鎖起來壓黑；左鍵點鐵砧即可取下解鎖（見 ForgeAnvilSlot）。
    /// - 左三右三共 6 個鑲嵌孔（ForgeSocketGrid）：依台面上那件裝備的孔位數開啟，其餘蓋鎖鏈。
    ///   現在裝備還沒有孔位屬性（ForgeSockets.Of 一律回 0）→ 六孔全鎖，功能鏈路已通、等資料接上。
    /// - 左下「移除鑲嵌」、右下「拆除裝備」：**尚未接功能**，按了只跳提示。
    ///
    /// 開啟時強制把背包一起開（並排，方便拖曳），關閉時把台上與孔上的東西退回背包再關背包——
    /// 這一套與傳送門 ScriptsPanel 完全同源。見 readme/FORGING.md。
    /// </summary>
    public class ForgingPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool ShowBackdrop => true;
        public override bool CloseOnEscape => true;

        const string Dir = "UI/ForgingPanel/";
        const string CommonDir = "UI/Common/";
        const string TitleFontPath = "Fonts/Bakudai/Bakudai-Bold";   // 毛筆字（同 GachaPanel / BossIntroPanel）

        // ── 語言表 id（LanguageTable.csv 的 4001–4099「鍛造介面」段）──
        const int TxtTitle = 4001, TxtRemoveGem = 4002, TxtDismantle = 4003;
        const int TxtNotYet = 4004, TxtOnlyEquip = 4005, TxtNeedInBag = 4006;

        // ───────── 底圖原生座標（量自示意圖，1536×1024；左上為原點、y 向下）─────────
        const float BgW = 1536f, BgH = 1024f;

        // 六個鑲嵌孔：左右各一欄、三排。方框大小＝框圖「本體」大小（側邊尖角會自然凸出去）。
        const float CellW = 153f, CellH = 156f;
        const float LeftCx = 307f, RightCx = 1224f;
        static readonly float[] RowCy = { 280f, 491f, 701f };

        // 鐵砧中央那一格
        const float AnvilCx = 768f, AnvilCy = 502f, AnvilW = 232f, AnvilH = 240f;

        // 兩顆長按鈕（左：移除鑲嵌／右：拆除裝備）
        const float BtnCy = 893f, BtnW = 386f;
        const float BtnLeftCx = 465f, BtnRightCx = 1068f;

        // 關閉鈕與標題
        const float CloseCx = 1355f, CloseCy = 103f, CloseW = 88f;
        const float TitleCx = 766f, TitleCy = 100f;

        // 物品圖佔格子的比例。
        // （鎖鏈不再是疊上去的獨立圖層——外框圖本身就有「無鎖鏈／有鎖鏈」兩個版本，見 ArtFrame / ArtFrameLocked。）
        const float IconRatio = 0.70f;

        // ───────── 擺位 ─────────
        [Tooltip("面板顯示高度（CanvasScaler 參考單位，1080 為滿版）。1536×1024 的圖，636 高 ≈ 954 寬。")]
        public float displayHeight = 636f;

        const float PairLeftX = -483f;   // 與背包並排：靠左（背包 SetPairedLayout(true) 會靠右）
        const float SoloX = 0f;

        // ───────── 資料 ─────────
        // ⚠ 鐵砧是**借放不是搬移**：它只記住東西在背包的哪一格，物品原地不動、來源那一格被鎖住壓黑，
        //   從鐵砧取下才解鎖。詳見 ForgeAnvilSlot 的註解與 readme/FORGING.md §4。
        readonly ForgeAnvilSlot _anvil = new ForgeAnvilSlot();
        readonly ForgeSocketGrid _sockets = new ForgeSocketGrid();

        RectTransform _frame;
        ForgeSlotWidget _anvilSlot;
        readonly ForgeSlotWidget[] _socketSlots = new ForgeSlotWidget[ForgeSockets.MaxSockets];
        int _lastDragId = -1;

        // tooltip（與背包／倉庫同款）
        const float TooltipWidth = 460f;
        RectTransform _tooltip;
        Text _tipName, _tipStats, _tipLore;

        // ───────────────────────── 給背包查詢的靜態入口 ─────────────────────────
        //
        // 背包不認識鍛造台的內部結構，只問這三個問題：這格鎖了嗎？這個裝備欄鎖了嗎？鎖的狀態變了嗎？
        // （同 StoragePanel.ActivePageIfOpen / ScriptsPanel.ActiveGridIfOpen 的既有慣例。）

        /// <summary>鎖定狀態的版本號：每次鐵砧上的東西變了就 +1，背包靠它知道要重畫（放上鐵砧不會動到背包資料、不會觸發 OnChanged）。</summary>
        public static int LockVersion { get; private set; }

        static ForgingPanel OpenInstance()
        {
            var ui = UIManager.Instance;
            var p = ui != null ? ui.Get<ForgingPanel>() : null;
            return (p != null && p.IsOpen) ? p : null;
        }

        /// <summary>某個背包道具格是不是正被鐵砧借走（借走＝壓黑、不能拖也不能點）。</summary>
        public static bool IsGridLocked(int index)
        {
            var p = OpenInstance();
            return p != null && p._anvil.HoldsGrid(index);
        }

        /// <summary>某個裝備欄是不是正被鐵砧借走。</summary>
        public static bool IsEquipLocked(EquipSlot slot)
        {
            var p = OpenInstance();
            return p != null && p._anvil.HoldsEquip(slot);
        }

        /// <summary>鍛造開著時，把背包某一格的武器／裝備放上鐵砧（給背包「左鍵點一下」用）。回傳有沒有放成功。</summary>
        public static bool TryPlaceFromGrid(int gridIndex)
        {
            var p = OpenInstance();
            return p != null && p._anvil.PlaceFromGrid(gridIndex);
        }

        /// <summary>鍛造是否開著（背包用它決定點擊要走「送上鐵砧」還是原本的裝備/使用）。</summary>
        public static bool IsForgeOpen => OpenInstance() != null;

        /// <summary>Play 模式 static 殘留保險（本專案關掉了 Domain Reload，見 PROBLEMS I3）。</summary>
        public static void ResetForPlayMode() => LockVersion = 0;

        // ───────────────────────── 建版面 ─────────────────────────

        protected override void OnBuild()
        {
            var frameGO = UIBuilder.Create("Frame", transform);
            _frame = UIBuilder.Rect(frameGO);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.anchoredPosition = new Vector2(SoloX, 0f);
            _frame.sizeDelta = new Vector2(BgW, BgH);
            float scale = displayHeight / BgH;
            _frame.localScale = new Vector3(scale, scale, 1f);

            // 底圖（整張＝面板畫布，所以直接拉伸貼齊 frame）
            var bg = UIBuilder.Image(frameGO.transform, "BG", LoadArt(ArtBg));
            UIBuilder.Stretch(bg.rectTransform);
            bg.raycastTarget = true;   // 吃掉空白處點擊，不穿到遊戲

            // 七個方框（先畫框，格子的物品圖才會疊在框上面）。
            // 六個鑲嵌孔各建「無鎖鏈」與「有鎖鏈」兩張圖疊在同一個位置，切換上鎖只是換哪一張 enabled——
            // 兩張圖的畫布尺寸與內容邊界框不同（見 ArtFrame / ArtFrameLocked），所以各自用自己的 ArtSpec 擺，
            // 但都指定同一組 CellW×CellH，確保切換時方框不會跳動。
            var socketFrames = new Image[ForgeSockets.MaxSockets, 2];
            for (int i = 0; i < ForgeSockets.MaxSockets; i++)
            {
                var c = SocketCenter(i);
                var open = MakeArt(_frame, $"SocketFrame{i}", ArtFrame);
                PlaceArt(open, ArtFrame, CellW, c, CellH);
                var shut = MakeArt(_frame, $"SocketFrameLocked{i}", ArtFrameLocked);
                PlaceArt(shut, ArtFrameLocked, CellW, c, CellH);
                socketFrames[i, 0] = open;
                socketFrames[i, 1] = shut;
            }
            var anvilFrame = MakeArt(_frame, "AnvilFrame", ArtFrame);
            PlaceArt(anvilFrame, ArtFrame, AnvilW, new Vector2(AnvilCx, AnvilCy), AnvilH);

            // 鑲嵌孔（預設全鎖）
            for (int i = 0; i < ForgeSockets.MaxSockets; i++)
            {
                var w = ForgeSlotWidget.Create(_frame, $"Socket{i}", CellW, CellW * IconRatio);
                Place(w.Rt, SocketCenter(i), CellW, CellH);
                w.SetFrameArt(socketFrames[i, 0], socketFrames[i, 1]);
                w.Accepts = IsGem;
                w.Entered = ShowTooltip; w.Exited = _ => HideTooltip();
                w.Bind(_sockets, i);
                w.Locked = true;
                _socketSlots[i] = w;
            }

            // 鐵砧中央那一格（永遠不會上鎖，所以只掛「無鎖鏈」那張）
            _anvilSlot = ForgeSlotWidget.Create(_frame, "AnvilSlot", AnvilW, AnvilW * IconRatio);
            Place(_anvilSlot.Rt, new Vector2(AnvilCx, AnvilCy), AnvilW, AnvilH);
            _anvilSlot.SetFrameArt(anvilFrame, null);
            _anvilSlot.Accepts = d => d != null && d.IsEquippable;
            _anvilSlot.Rejected = () => AlertPanel.Toast(Language.GetText(TxtOnlyEquip));
            _anvilSlot.Entered = ShowTooltip; _anvilSlot.Exited = _ => HideTooltip();
            // 借放而非搬移：收下時只記來源（CustomDrop），也不能反過來把東西從鐵砧拖出去（CanDragOut=false，
            // 否則共用搬運會把它當「搬出去」而在背包憑空多一件）。取下改成左鍵點鐵砧。
            _anvilSlot.CustomDrop = OnAnvilDrop;
            _anvilSlot.CanDragOut = false;
            _anvilSlot.Clicked = TakeOffAnvil;
            _anvilSlot.Bind(_anvil, 0);

            // 兩顆按鈕（功能未接，先跳提示）
            MakeLongButton("RemoveGemBtn", Language.GetText(TxtRemoveGem), BtnLeftCx, OnRemoveGemPressed);
            MakeLongButton("DismantleBtn", Language.GetText(TxtDismantle), BtnRightCx, OnDismantlePressed);

            BuildCloseButton();
            BuildTitle();
            BuildTooltip();
        }

        static Vector2 SocketCenter(int i) =>
            new Vector2(i < 3 ? LeftCx : RightCx, RowCy[i % 3]);

        void BuildTitle()
        {
            var t = UIBuilder.Text(_frame, "Title", Language.GetText(TxtTitle), 68, TextGold);
            t.font = UIBuilder.LoadFont(TitleFontPath);
            Place(t.rectTransform, new Vector2(TitleCx, TitleCy), 460f, 110f);
        }

        void BuildCloseButton()
        {
            var b = UIBuilder.Button(_frame, "CloseBtn", "", () => UIManager.Instance?.Close(this),
                                     Color.white, LoadArt(ArtClose));
            var img = b.GetComponent<Image>();
            b.targetGraphic = img;                          // 程式建鈕必須手動指（見 PROBLEMS D4）
            // CloseBtn_2 只有一張圖（沒有按下版），所以用 ColorTint 做回饋：移入微亮、按下壓暗。
            // 之後若補了 CloseBtn_2_pressed，再改回 SpriteSwap 即可（作法同 StoragePanel 的重整鈕）。
            b.transition = Selectable.Transition.ColorTint;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 0.94f, 0.80f);
            cb.pressedColor = new Color(0.66f, 0.62f, 0.56f);
            cb.selectedColor = Color.white;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            PlaceArt(img, ArtClose, CloseW, new Vector2(CloseCx, CloseCy));
        }

        /// <summary>一顆「長條底板 ＋ 文字」的按鈕。命中區是一張全透明 Image（尺寸＝底板可見範圍），底板是它的子物件。</summary>
        Button MakeLongButton(string name, string label, float cx, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIBuilder.Button(_frame, name, "", onClick, new Color(1f, 1f, 1f, 0f));
            var hit = b.GetComponent<Image>();
            hit.raycastTarget = true;                        // 全透明仍接得到點擊（uGUI 不看 alpha）
            b.targetGraphic = hit;
            b.transition = Selectable.Transition.ColorTint;
            var cb = b.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0f);
            cb.highlightedColor = new Color(1f, 0.85f, 0.5f, 0.16f);
            cb.pressedColor = new Color(1f, 0.75f, 0.35f, 0.28f);
            cb.selectedColor = new Color(1f, 1f, 1f, 0f);
            cb.colorMultiplier = 1f;
            b.colors = cb;

            float btnH = BtnW / ArtBtn.Aspect;
            Place((RectTransform)b.transform, new Vector2(cx, BtnCy), BtnW, btnH);

            var plate = MakeArt(b.transform, "Plate", ArtBtn);
            PlaceArt(plate, ArtBtn, BtnW, Vector2.zero);      // 子物件：以按鈕自身中心為原點
            plate.transform.SetAsFirstSibling();

            var t = UIBuilder.Text(b.transform, "Label", label, 46, TextGold);
            t.font = UIBuilder.LoadFont(TitleFontPath);
            UIBuilder.Stretch(t.rectTransform);
            return b;
        }

        /// <summary>浮動 tooltip（與背包／倉庫同款）：掛在 panel root（不受 frame 縮放）。</summary>
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

        // ───────────────────────── 開關 ─────────────────────────

        protected override void OnOpen()
        {
            // 鍛造與背包是綁定的一組（要能從背包把裝備拖上鐵砧）→ 強制一起開，並排由 StorageBagCoordinator 擺。
            UIManager.Instance?.Open<InventoryPanel>();
            SetPairedLayout(true);

            _anvil.OnChanged += OnAnvilChanged;
            _sockets.OnChanged += RefreshSockets;
            // 背包內容一變就檢查鐵砧指的來源還在不在（被排序搬走、被消耗、換裝了都會自動取下）。
            if (InventorySystem.Instance != null) InventorySystem.Instance.OnChanged += _anvil.Validate;
            // 把「這一格被鐵砧借走了」告訴共用拖放層。
            // ⚠ 鎖一定要擋在 SlotDragController，光在格子元件的 OnBeginDrag 裡 return 是擋不住的——
            //   EventSystem 在按下時就填好 e.pointerDrag 了，Drop 讀的是它，結果會變成
            //   「沒有懸浮圖示、但放到別格照樣把東西搬走」。
            SlotDragController.IsSlotLocked = IsSlotBorrowed;
            _lastDragId = -1;
            OnAnvilChanged();
        }

        protected override void OnClose()
        {
            _anvil.OnChanged -= OnAnvilChanged;
            _sockets.OnChanged -= RefreshSockets;
            if (InventorySystem.Instance != null) InventorySystem.Instance.OnChanged -= _anvil.Validate;
            if (SlotDragController.IsSlotLocked == (System.Func<ISlotView, bool>)IsSlotBorrowed)
                SlotDragController.IsSlotLocked = null;   // 只拆自己掛上去的那一個
            HideTooltip();

            // 鐵砧只是借放（物品一直在背包原位），所以關面板只要取下＝解鎖那一格，沒有東西要退回。
            _anvil.Clear();
            // 鑲嵌孔則是真的收著東西 → 一律退回背包，別弄丟。
            ReturnSocketsToBag();

            UIManager.Instance?.Close<InventoryPanel>();
        }

        void ReturnSocketsToBag()
        {
            var inv = InventorySystem.Instance;
            if (inv == null) return;
            foreach (var st in _sockets.TakeAll())
                if (!st.IsEmpty) inv.AddItem(st.ItemId, st.Count);
        }

        // ───────────────────────── 鐵砧：放上／取下 ─────────────────────────

        /// <summary>從背包（道具格或裝備欄）拖上鐵砧：只記來源，物品留在原位不動。</summary>
        void OnAnvilDrop(ISlotView src)
        {
            if (src == null) return;
            if (src.IsEquip) { _anvil.PlaceFromEquip(src.Equip); return; }
            if (src.Grid != null && ReferenceEquals(src.Grid, InventorySystem.Instance))
            {
                _anvil.PlaceFromGrid(src.GridIndex);
                return;
            }
            // 倉庫等其他容器：鐵砧只認得背包裡的東西（鎖定/解鎖是對背包那一格做的）。
            AlertPanel.Toast(Language.GetText(TxtNeedInBag));
        }

        /// <summary>左鍵點鐵砧＝取下（來源那一格解鎖）。</summary>
        void TakeOffAnvil() => _anvil.Clear();

        /// <summary>這一格是不是正被鐵砧借走（掛給 SlotDragController 當鎖定判斷）。</summary>
        bool IsSlotBorrowed(ISlotView v)
        {
            if (v == null || _anvil.IsEmpty) return false;
            if (v.IsEquip) return _anvil.HoldsEquip(v.Equip);
            return v.Grid != null && ReferenceEquals(v.Grid, InventorySystem.Instance)
                                  && _anvil.HoldsGrid(v.GridIndex);
        }

        void Update()
        {
            if (!IsOpen) return;

            // 背包被單獨關掉（例如按 B 或 ESC）就一起收掉——這兩個面板是一組的。
            var ui = UIManager.Instance;
            if (ui != null && !ui.IsOpen<InventoryPanel>()) { ui.Close(this); return; }

            if (_tooltip != null && _tooltip.gameObject.activeSelf) PositionTooltip();

            int drag = SlotDragController.DraggingItemId;
            if (drag != _lastDragId) { _lastDragId = drag; UpdateDropHighlights(drag); }
        }

        /// <summary>並排（靠左）或單獨（置中）。由 StorageBagCoordinator 呼叫。</summary>
        public void SetPairedLayout(bool paired)
        {
            if (_frame != null) _frame.anchoredPosition = new Vector2(paired ? PairLeftX : SoloX, 0f);
        }

        // ───────────────────────── 鑲嵌孔解鎖 ─────────────────────────

        void OnAnvilChanged()
        {
            if (_anvilSlot != null) _anvilSlot.Refresh();
            LockVersion++;   // 讓背包知道要重畫「哪一格被鎖住壓黑」（放上鐵砧不會動到背包資料）

            int want = _anvil.SocketCount;                  // 現在 ForgeSockets.Of 一律回 0 ＝ 全鎖
            if (want < _sockets.UnlockedCount) ReturnClosingSockets(want);
            _sockets.UnlockedCount = want;
            RefreshSockets();
        }

        /// <summary>孔位變少（換了孔比較少的裝備／把裝備拿下來）時，把被關掉那幾孔的東西退回背包。</summary>
        void ReturnClosingSockets(int newUnlocked)
        {
            var inv = InventorySystem.Instance;
            for (int i = newUnlocked; i < ForgeSockets.MaxSockets; i++)
            {
                var st = _sockets.GetAt(i);
                if (st.IsEmpty) continue;
                _sockets.RemoveAt(i, st.Count);
                if (inv != null) inv.AddItem(st.ItemId, st.Count);
            }
        }

        void RefreshSockets()
        {
            for (int i = 0; i < _socketSlots.Length; i++)
            {
                var w = _socketSlots[i];
                if (w == null) continue;
                w.Locked = _sockets.IsLocked(i);   // Locked 的 setter 會順便 Refresh
                if (!w.Locked) w.Refresh();
            }
        }

        void UpdateDropHighlights(int itemId)
        {
            if (_anvilSlot != null) _anvilSlot.SetDropHighlight(itemId);
            foreach (var w in _socketSlots) if (w != null) w.SetDropHighlight(itemId);
        }

        /// <summary>
        /// 什麼算「寶石」——目前遊戲裡還沒有這種道具，所以一律回 false（孔位就算開了也放不進東西）。
        /// 將來做出寶石時，把判斷換成 <c>d.Category == "Gem"</c> 之類即可，其餘完全不用動。
        /// </summary>
        static bool IsGem(ItemData d) => false;

        // ───────────────────────── 兩顆按鈕（功能未接）─────────────────────────

        void OnRemoveGemPressed() => AlertPanel.Toast(Language.GetText(TxtNotYet));
        void OnDismantlePressed() => AlertPanel.Toast(Language.GetText(TxtNotYet));

        // ───────────────────────── tooltip ─────────────────────────

        void ShowTooltip(int itemId)
        {
            var d = itemId > 0 && InventorySystem.Instance != null ? InventorySystem.Instance.GetData(itemId) : null;
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

        void PositionTooltip()
        {
            var panelRect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    panelRect, Input.mousePosition, null, out Vector2 local))
                return;
            bool right = local.x > 0f;
            _tooltip.pivot = new Vector2(right ? 1f : 0f, 1f);
            _tooltip.anchoredPosition = local + new Vector2(right ? -18f : 18f, -18f);
        }

        // ───────────────────────── 版面小工具 ─────────────────────────

        static readonly Color TextGold = new Color(0.95f, 0.89f, 0.76f);

        /// <summary>把 RectTransform 放到底圖像素座標（左上為原點、y 向下），中心對齊、給定寬高。</summary>
        void Place(RectTransform rt, Vector2 centerPx, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // 錨到 frame 左上角
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(centerPx.x, -centerPx.y);
            rt.sizeDelta = new Vector2(w, h);
        }

        // ── 美術圖的「透明邊補償」（與 GachaPanel 同一套 ArtSpec 作法）──
        //
        // AI 產的圖多半是整張畫布輸出、真正的內容只佔中間一塊。若直接把整張塞進方框，結果會又小又偏。
        // 下表記下每張圖「內容」在畫布中的邊界框，由 PlaceArt 反推方框該多大、要偏移多少，
        // 讓**內容**剛好落在指定位置與尺寸。
        //
        // 【重新輸出圖檔之後】數字會對不上，但不會靜默出錯——LoadArt 會比對畫布尺寸並印警告。
        // 重量方法：把圖丟進任何影像工具，看不透明像素的邊界框（左, 上, 寬, 高）。
        struct ArtSpec
        {
            public string path;
            public float fullW, fullH;    // 圖檔完整尺寸
            public float bx, by, bw, bh;  // 內容邊界框（左上為原點）
            public ArtSpec(string path, float fullW, float fullH, float bx, float by, float bw, float bh)
            { this.path = path; this.fullW = fullW; this.fullH = fullH; this.bx = bx; this.by = by; this.bw = bw; this.bh = bh; }
            public float Aspect => bw / bh;
        }

        // 底圖是「整張都是內容」的那種（雖然四周有透明邊，但那本來就是面板外緣的一部分）→ 邊界框＝整張畫布。
        static readonly ArtSpec ArtBg = new ArtSpec(Dir + "ForgingPanel_Bg", 1536, 1024, 0, 0, 1536, 1024);
        // 方框有兩個版本：無鎖鏈（可用的孔／鐵砧）與有鎖鏈（上鎖的孔）。
        // 兩張圖的內容都含兩側尖角，但這裡記的一律是**方形本體**的邊界框（尖角自然凸到方框外，與示意圖一致）；
        // 兩張的畫布尺寸與本體位置都不同，所以各記各的，靠 PlaceArt 對齊到同一個 CellW×CellH。
        static readonly ArtSpec ArtFrame = new ArtSpec(Dir + "ForgingPanel_ItemFrame", 1448, 1296, 145, 60, 1153, 1176);
        static readonly ArtSpec ArtFrameLocked = new ArtSpec(Dir + "ForgingPanel_ItemFrameWithChain", 1412, 1302, 109, 65, 1171, 1185);
        static readonly ArtSpec ArtBtn = new ArtSpec(CommonDir + "ForgingPanel_Btn", 2416, 676, 7, 66, 2388, 559);
        static readonly ArtSpec ArtClose = new ArtSpec(CommonDir + "CloseBtn_2", 519, 481, 17, 14, 475, 459);

        /// <summary>建一張美術圖（不擋點擊）。載不到就留一個透明的殼，版面不會塌。</summary>
        static Image MakeArt(Transform parent, string name, ArtSpec spec)
        {
            var img = UIBuilder.Image(parent, name, LoadArt(spec), Color.white);
            img.raycastTarget = false;
            img.preserveAspect = false;   // 尺寸由 PlaceArt 精算，不要讓 preserveAspect 再插手
            return img;
        }

        /// <summary>
        /// 把圖擺好，讓「內容」剛好是 contentW 寬、中心落在 centerPx（底圖像素座標，左上原點）。
        /// contentH &gt; 0 時連高度也指定（否則依原圖比例算）——用在「兩張比例略有差異的圖要能無縫互換」的場合，
        /// 例如無鎖鏈／有鎖鏈兩張外框（本體比例差約 3%，不鎖死高度切換時方框會跳一下）。
        /// </summary>
        void PlaceArt(Image img, ArtSpec spec, float contentW, Vector2 centerPx, float contentHOverride = 0f)
        {
            float contentH = contentHOverride > 0f ? contentHOverride : contentW / spec.Aspect;
            float rectW = contentW * (spec.fullW / spec.bw);
            float rectH = contentH * (spec.fullH / spec.bh);

            // 內容中心相對「畫布中心」的偏移（圖檔座標左上原點 → UI 座標 +y 向上）
            float ox = (spec.bx + spec.bw * 0.5f) - spec.fullW * 0.5f;
            float oy = spec.fullH * 0.5f - (spec.by + spec.bh * 0.5f);
            float dx = ox * (rectW / spec.fullW);
            float dy = oy * (rectH / spec.fullH);

            var rt = img.rectTransform;
            if (rt.parent == _frame)
                Place(rt, centerPx - new Vector2(dx, -dy), rectW, rectH);
            else
                UIBuilder.Center(rt, rectW, rectH, centerPx - new Vector2(dx, dy));
        }

        /// <summary>
        /// 載圖並檢查它跟 ArtSpec 記的還對不對得上（重新輸出圖檔時把靜默偏移變成明確警告）。
        ///
        /// ⚠ 比的是**畫布比例**而不是像素數。原因：Unity 匯入設定的 `Max Size`（本專案預設 2048）
        ///   會把超過的圖**等比縮小**，`sprite.rect` 拿到的是縮小後的尺寸——例如 2416×676 的按鈕底板
        ///   進到遊戲裡是 2048×573。而 PlaceArt 的算式全是比值（fullW/bw、rectW/fullW…），
        ///   等比縮放完全不影響結果，那種情況報警只是雜訊。
        ///   真正會出事、也是這裡要抓的，是「重新輸出時畫布比例變了」＝內容在畫布裡的相對位置跑掉。
        /// </summary>
        static Sprite LoadArt(ArtSpec spec)
        {
            var sp = LoadSprite(spec.path);
            if (sp != null)
            {
                float w = sp.rect.width, h = sp.rect.height;
                if (w > 0f && h > 0f && spec.fullH > 0f)
                {
                    float got = w / h, want = spec.fullW / spec.fullH;
                    if (Mathf.Abs(got - want) > want * 0.01f)   // 差 1% 以上才算真的換圖了
                        Debug.LogWarning($"[ForgingPanel]「{spec.path}」的畫布比例對不上：實際 {w}x{h}（{got:F3}），" +
                                         $"版面表記的是 {spec.fullW}x{spec.fullH}（{want:F3}）。" +
                                         "圖重新輸出過了嗎？請重新量它的不透明內容邊界框並更新 ForgingPanel 的 ArtSpec，否則位置與大小會偏掉。");
                }
            }
            return sp;
        }

        static Sprite LoadSprite(string path)
        {
            var sp = Resources.Load<Sprite>(path);
            if (sp != null) return sp;
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null) return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            Debug.LogWarning($"[ForgingPanel] 載不到美術 Resources/{path}（沒放圖，或匯入型別不是 Sprite/Texture？）。");
            return null;
        }
    }
}
