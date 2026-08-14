using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 作弊面板（測試用，呈現層）。預設按 <b>L</b> 開/關（見 <see cref="CheatLauncher"/>）。
    ///
    /// 版面刻意做大、且採「左側分頁導覽 + 右側內容區」結構，之後要加更多作弊功能只要新增一個分頁即可
    /// （見 <see cref="OnBuild"/> 裡「＝＝＝ 如何新增一個作弊分頁 ＝＝＝」的說明）。
    ///
    /// 目前分頁：
    ///   1.「給道具」：填入物品 ID + 數量 → 按確認 → 走 RunProgress.GiveItem(toRealBag:true)（**一律直接進真背包**，
    ///      給 101 銅錢會轉成金錢數字）。另有一鍵快捷：給錢、**取得所有武器（每種一把）**。
    ///   2.「鑲嵌」：改裝備中武器的孔數、每種能力珠各給一顆 Lv3、給測試防具。
    ///
    /// 風格對齊專案：全程式建構、零 prefab / Inspector 接線（同 SettingsPanel / InventoryPanel）。
    /// 開啟時暫停遊戲 + 擋輸入（方便打字），ESC 或右上 X 關閉。
    /// </summary>
    public class CheatPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;          // 開著時暫停，方便安心打字/操作
        public override bool BlocksGameplayInput => true;
        public override bool ShowBackdrop => true;
        public override bool CloseOnEscape => true;

        // ── 版面尺寸（CanvasScaler 參考 1920x1080，這裡是參考單位）──
        const float FrameW = 1400f, FrameH = 860f;
        const float TitleH = 74f;                 // 頂部標題列高
        const float NavW = 320f;                  // 左側分頁欄寬
        const float Pad = 24f;                    // 內距
        const float NavBtnH = 60f, NavBtnGap = 10f;

        // ── 配色 ──
        static readonly Color ColFrame   = new Color(0.08f, 0.08f, 0.10f, 0.97f);
        static readonly Color ColTitle   = new Color(0.16f, 0.13f, 0.20f, 1f);
        static readonly Color ColNavBg   = new Color(0.11f, 0.11f, 0.14f, 1f);
        static readonly Color ColContent = new Color(0.13f, 0.13f, 0.16f, 1f);
        static readonly Color ColAccent  = new Color(0.86f, 0.72f, 0.36f, 1f);   // 金
        static readonly Color ColNavIdle = new Color(0.18f, 0.18f, 0.22f, 1f);
        static readonly Color ColNavOn   = new Color(0.34f, 0.28f, 0.15f, 1f);
        static readonly Color ColBtn     = new Color(0.55f, 0.42f, 0.16f, 1f);
        static readonly Color ColInputBg = new Color(1f, 1f, 1f, 0.10f);
        static readonly Color ColOk      = new Color(0.55f, 0.9f, 0.55f, 1f);
        static readonly Color ColErr     = new Color(0.95f, 0.5f, 0.5f, 1f);
        // 功能分組的底板色：不同功能給不同色塊，一眼看得出「這幾個東西是一組的」。
        static readonly Color ColGroupA  = new Color(1f, 1f, 1f, 0.05f);            // 中性（依 ID 給道具）
        static readonly Color ColGroupB  = new Color(0.86f, 0.72f, 0.36f, 0.09f);   // 金調（快捷鈕）
        static readonly Color ColDivider = new Color(1f, 1f, 1f, 0.12f);

        RectTransform _frame;
        RectTransform _navCol;     // 左側分頁欄
        RectTransform _contentCol; // 右側內容區

        // 分頁：一個導覽鈕 + 一塊內容根物件。切換時只切內容的顯示。
        readonly List<Button> _navButtons = new List<Button>();
        readonly List<GameObject> _contents = new List<GameObject>();

        // 「給道具」分頁的欄位
        InputField _idInput, _countInput;
        Text _giveStatus;

        protected override void OnBuild()
        {
            // ── 主框（置中、固定大小）──
            var frameGO = UIBuilder.Create("Frame", transform);
            _frame = UIBuilder.Rect(frameGO);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.anchoredPosition = Vector2.zero;
            _frame.sizeDelta = new Vector2(FrameW, FrameH);
            var frameBg = frameGO.AddComponent<Image>();
            frameBg.color = ColFrame;
            frameBg.raycastTarget = true;   // 吃掉空白處點擊，不穿到遊戲

            // ── 標題列（貼齊框頂，寬 = 框寬）──
            var titleBar = UIBuilder.Create("TitleBar", _frame);
            titleBar.AddComponent<Image>().color = ColTitle;
            Place(UIBuilder.Rect(titleBar), 0f, 0f, FrameW, TitleH);

            var title = UIBuilder.Text(titleBar.transform, "Title", "作弊面板 · Cheat Panel", 30, ColAccent, TextAnchor.MiddleLeft);
            Place(title.rectTransform, Pad + 6f, 0f, FrameW - 200f, TitleH);

            // 右上關閉鈕
            var closeBtn = UIBuilder.Button(titleBar.transform, "CloseBtn", "X", () => UIManager.Instance.Close(this),
                                            new Color(0.5f, 0.2f, 0.2f, 1f));
            var crt = (RectTransform)closeBtn.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-12f, -12f);
            crt.sizeDelta = new Vector2(50f, 50f);

            // ── 左側分頁欄（框頂之下，上下拉伸、寬固定）──
            var navGO = UIBuilder.Create("NavColumn", _frame);
            navGO.AddComponent<Image>().color = ColNavBg;
            _navCol = UIBuilder.Rect(navGO);
            _navCol.anchorMin = new Vector2(0f, 0f);
            _navCol.anchorMax = new Vector2(0f, 1f);
            _navCol.pivot = new Vector2(0f, 1f);
            _navCol.offsetMin = new Vector2(0f, 0f);         // 左=0、下=0
            _navCol.offsetMax = new Vector2(NavW, -TitleH);  // 右=NavW（欄寬）、上=框頂內縮標題列高

            // ── 右側內容區（框頂之下、左欄之右，四邊拉伸）──
            var contentGO = UIBuilder.Create("ContentColumn", _frame);
            contentGO.AddComponent<Image>().color = ColContent;
            _contentCol = UIBuilder.Rect(contentGO);
            _contentCol.anchorMin = new Vector2(0f, 0f);
            _contentCol.anchorMax = new Vector2(1f, 1f);
            _contentCol.pivot = new Vector2(0f, 1f);
            _contentCol.offsetMin = new Vector2(NavW, 0f);   // 左=NavW（讓開左欄）、下=0
            _contentCol.offsetMax = new Vector2(0f, -TitleH); // 右=0（貼框右）、上=框頂內縮標題列高

            // ═══ 如何新增一個作弊分頁 ═══
            // 呼叫 AddSection("分頁名稱", root => { 用 UIBuilder 在 root 裡拼這個分頁的內容 });
            // root 是右側內容區裡一塊「已四邊拉伸並留內距」的容器（左上為原點，用 Place() 擺元件）。
            // 每呼叫一次就多一個左側導覽鈕；第一個分頁預設顯示。
            // 之後要加「加錢」「回滿血」「無敵」「解鎖關卡」等都照這樣加一行 + 一個 Build 方法。
            AddSection("給道具", BuildGiveItemSection);
            AddSection("鑲嵌", BuildSocketSection);

            ShowSection(0);
        }

        protected override void OnOpen()
        {
            if (_giveStatus != null) _giveStatus.text = "";   // 每次開啟清掉上次的結果提示
        }

        // ───────────────────────── 分頁框架 ─────────────────────────

        void AddSection(string label, Action<RectTransform> buildContent)
        {
            int index = _navButtons.Count;

            // 導覽鈕（掛在左欄，依序往下排）
            var btn = UIBuilder.Button(_navCol, $"Nav_{label}", label, () => ShowSection(index), ColNavIdle);
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 1f);
            brt.anchoredPosition = new Vector2(Pad * 0.5f, -(Pad * 0.5f) - index * (NavBtnH + NavBtnGap));
            brt.sizeDelta = new Vector2(NavW - Pad, NavBtnH);
            var lbl = btn.GetComponentInChildren<Text>();
            if (lbl != null)
            {
                lbl.alignment = TextAnchor.MiddleLeft;
                lbl.fontSize = 24;
                UIBuilder.Stretch(lbl.rectTransform, 20, 8, 0, 0);   // 文字內縮不貼左緣
            }
            _navButtons.Add(btn);

            // 內容根（四邊拉伸貼齊內容區、留內距）
            var root = UIBuilder.Create($"Section_{label}", _contentCol);
            UIBuilder.Stretch(UIBuilder.Rect(root), Pad, Pad, Pad, Pad);
            buildContent(UIBuilder.Rect(root));
            root.SetActive(false);
            _contents.Add(root);
        }

        void ShowSection(int index)
        {
            if (index < 0 || index >= _contents.Count) return;
            for (int i = 0; i < _contents.Count; i++)
            {
                if (_contents[i] != null) _contents[i].SetActive(i == index);
                var img = _navButtons[i] != null ? _navButtons[i].GetComponent<Image>() : null;
                if (img != null) img.color = (i == index) ? ColNavOn : ColNavIdle;
            }
        }

        // ───────────────────────── 分頁 1：給道具 ─────────────────────────

        void BuildGiveItemSection(RectTransform root)
        {
            var head = UIBuilder.Text(root, "Head", "給道具", 28, ColAccent, TextAnchor.UpperLeft);
            Place(head.rectTransform, 0f, 0f, 600f, 40f);

            var hint = UIBuilder.Text(root, "Hint",
                "物品 ID 見 Assets/Data/ItemTable.csv（例：201 回血藥）。下方一鍵快捷不用填欄位。",
                18, new Color(1f, 1f, 1f, 0.6f), TextAnchor.UpperLeft);
            Place(hint.rectTransform, 0f, 46f, GroupW, 30f);

            // ── 第一組：依 ID 給道具 ──
            //    ID、數量、確認是同一件事的三個步驟，排成一列才好操作（填、填、按）。
            const float G1Y = 92f, G1H = 124f, Row1Y = 136f, RowH = 48f;
            AddGroup(root, "依 ID 給道具", G1Y, G1H, ColGroupA);

            var idLabel = UIBuilder.Text(root, "IdLabel", "物品 ID", 22, Color.white, TextAnchor.MiddleLeft);
            Place(idLabel.rectTransform, 14f, Row1Y, 110f, RowH);
            _idInput = UIBuilder.InputField(root, "IdInput", "例：201", 24, 9, ColInputBg);
            _idInput.contentType = InputField.ContentType.IntegerNumber;   // 只准整數
            Place((RectTransform)_idInput.transform, 129f, Row1Y, 180f, RowH);

            var countLabel = UIBuilder.Text(root, "CountLabel", "數量", 22, Color.white, TextAnchor.MiddleLeft);
            Place(countLabel.rectTransform, 329f, Row1Y, 80f, RowH);
            _countInput = UIBuilder.InputField(root, "CountInput", "例：1", 24, 9, ColInputBg);
            _countInput.contentType = InputField.ContentType.IntegerNumber;
            _countInput.text = "1";
            Place((RectTransform)_countInput.transform, 414f, Row1Y, 140f, RowH);

            var giveBtn = UIBuilder.Button(root, "GiveBtn", "確認給予", OnClickGive, ColBtn);
            Place((RectTransform)giveBtn.transform, 579f, Row1Y, 230f, RowH);

            AddDivider(root, 232f);

            // ── 第二組：一鍵快捷（不用填任何欄位）──
            const float G2Y = 246f, G2H = 118f;
            AddGroup(root, "一鍵快捷", G2Y, G2H, ColGroupB);

            var moneyBtn = UIBuilder.Button(root, "MoneyBtn", $"獲得 {CheatMoneyAmount:N0} 元",
                                            OnClickGiveMoney, ColBtn);
            Place((RectTransform)moneyBtn.transform, 14f, 288f, 260f, 56f);

            var allWeaponBtn = UIBuilder.Button(root, "AllWeaponBtn", "取得所有武器（每種一把）",
                                                OnClickGiveAllWeapons, ColBtn);
            Place((RectTransform)allWeaponBtn.transform, 289f, 288f, 330f, 56f);

            // 狀態列（本次結果）——兩組共用，放在最下面
            _giveStatus = UIBuilder.Text(root, "GiveStatus", "", 22, ColOk, TextAnchor.UpperLeft);
            Place(_giveStatus.rectTransform, 0f, 384f, GroupW, 80f);
        }

        // ───────────────────────── 分頁：鑲嵌（能力珠測試）─────────────────────────
        //
        // 孔數與珠子等級平常是隨機骰的（見 RandomRules），要驗證極端組合很花時間，
        // 所以這裡提供直接指定的捷徑。見 readme/GEM_SOCKET.md。

        InputField _socketInput;
        Text _socketStatus;

        void BuildSocketSection(RectTransform root)
        {
            var head = UIBuilder.Text(root, "Head", "鑲嵌測試", 28, ColAccent, TextAnchor.UpperLeft);
            Place(head.rectTransform, 0f, 0f, 600f, 40f);

            var hint = UIBuilder.Text(root, "Hint",
                "孔數與珠子等級平常是隨機的；這裡可以直接指定，方便驗證極端組合。",
                18, new Color(1f, 1f, 1f, 0.6f), TextAnchor.UpperLeft);
            Place(hint.rectTransform, 0f, 46f, GroupW, 30f);

            // ── 第一組：改裝備中武器的孔數 ──
            const float G1Y = 92f, G1H = 124f, Row1Y = 136f, RowH = 48f;
            AddGroup(root, "改「裝備中武器」的孔數", G1Y, G1H, ColGroupA);

            var lbl = UIBuilder.Text(root, "SockLabel", "孔數 0~6", 22, Color.white, TextAnchor.MiddleLeft);
            Place(lbl.rectTransform, 14f, Row1Y, 130f, RowH);
            _socketInput = UIBuilder.InputField(root, "SockInput", "例：6", 24, 1, ColInputBg);
            _socketInput.contentType = InputField.ContentType.IntegerNumber;
            _socketInput.text = "6";
            Place((RectTransform)_socketInput.transform, 149f, Row1Y, 120f, RowH);

            var applyBtn = UIBuilder.Button(root, "SockBtn", "重開孔位（隨機位置）", OnClickReroll, ColBtn);
            Place((RectTransform)applyBtn.transform, 289f, Row1Y, 300f, RowH);

            AddDivider(root, 232f);

            // ── 第二組：一鍵給滿等能力珠 ──
            const float G2Y = 246f, G2H = 118f;
            AddGroup(root, "一鍵快捷", G2Y, G2H, ColGroupB);

            var gemBtn = UIBuilder.Button(root, "GemBtn", "每種能力珠各給一顆（Lv3）", OnClickGiveGems, ColBtn);
            Place((RectTransform)gemBtn.transform, 14f, 288f, 360f, 56f);

            var armorBtn = UIBuilder.Button(root, "ArmorBtn", "給測試護身符＋戒指", OnClickGiveArmor, ColBtn);
            Place((RectTransform)armorBtn.transform, 389f, 288f, 300f, 56f);

            _socketStatus = UIBuilder.Text(root, "SockStatus", "", 22, ColOk, TextAnchor.UpperLeft);
            Place(_socketStatus.rectTransform, 0f, 384f, GroupW, 80f);
        }

        void SetSocketStatus(string msg, bool ok)
        {
            if (_socketStatus == null) return;
            _socketStatus.text = msg;
            _socketStatus.color = ok ? ColOk : ColErr;
        }

        // 把「目前裝備中的武器」重開孔位（位置一樣是隨機挑的）。已鑲的珠子會先退回背包，避免憑空消失。
        void OnClickReroll()
        {
            var inv = Dipan.Inventory.InventorySystem.Instance;
            if (inv == null) { SetSocketStatus("找不到背包系統。", false); return; }

            var st = inv.GetEquippedStack(Dipan.Inventory.EquipSlot.Weapon);
            if (st.IsEmpty) { SetSocketStatus("武器欄是空的，請先裝備一把武器。", false); return; }

            int n = 6;
            if (!string.IsNullOrEmpty(_socketInput?.text) && !int.TryParse(_socketInput.text, out n)) n = 6;
            n = Mathf.Clamp(n, 0, Dipan.Inventory.ItemInstance.SocketMax);

            if (st.Inst != null && st.Inst.HasSockets)
                for (int i = 0; i < st.Inst.sockets.Count; i++)
                {
                    var g = st.Inst.TakeGem(i);
                    if (g != null) inv.AddStack(Dipan.Inventory.ItemManager.FromGemRef(g));
                }

            st.Inst = Dipan.Inventory.ItemInstance.FromSocketLayout(Dipan.Rules.RandomRules.LayoutFor(n));
            inv.SetEquippedStack(Dipan.Inventory.EquipSlot.Weapon, st);

            var d = inv.GetData(st.ItemId);
            SetSocketStatus($"「{(d != null ? d.Name : st.ItemId.ToString())}」已改成 {n} 孔（位置隨機）。", true);
            AlertPanel.Toast($"作弊：武器改成 {n} 孔");
        }

        // 每一種能力珠各給一顆滿等的，方便一次驗證所有能力。
        void OnClickGiveGems()
        {
            var inv = Dipan.Inventory.InventorySystem.Instance;
            if (inv == null) { SetSocketStatus("找不到背包系統。", false); return; }

            int given = 0, full = 0;
            foreach (var kv in inv.Db.Items)
            {
                if (kv.Value == null || !kv.Value.IsGem) continue;
                var st = Dipan.Inventory.ItemManager.CreateGem(kv.Key, 3);
                if (st.IsEmpty) continue;
                if (inv.AddStack(st) > 0) full++; else given++;
            }
            SetSocketStatus(full > 0 ? $"給了 {given} 顆能力珠（Lv3），{full} 顆因背包已滿放不下。"
                                     : $"給了 {given} 顆能力珠（Lv3）。", full == 0);
            AlertPanel.Toast($"作弊：能力珠 ×{given}");
        }

        // 測試用防具（護身符/戒指）——驗證「珠子鑲在別的裝備上也會加到武器身上」。
        void OnClickGiveArmor()
        {
            var inv = Dipan.Inventory.InventorySystem.Instance;
            if (inv == null) { SetSocketStatus("找不到背包系統。", false); return; }
            int ok = 0;
            foreach (int id in new[] { 501, 502 })
            {
                if (inv.GetData(id) == null) continue;
                if (inv.AddStack(Dipan.Inventory.ItemManager.Create(id, 1)) == 0) ok++;
            }
            SetSocketStatus(ok > 0 ? $"已給予 {ok} 件測試防具。" : "背包已滿或物品表裡沒有 501/502。", ok > 0);
        }

        /// <summary>作弊快捷鈕一次給多少錢。</summary>
        const int CheatMoneyAmount = 10000;

        // 直接加金錢數字（金錢不是背包道具，所以走 SaveManager，不經過背包/臨時包）。
        void OnClickGiveMoney()
        {
            var sm = Dipan.Save.SaveManager.Instance;
            if (sm == null || !sm.HasActiveCharacter)
            {
                SetGiveStatus("還沒載入角色，現在沒有錢包可以加錢。", false);
                return;
            }
            sm.AddCurrency(CheatMoneyAmount);
            SetGiveStatus($"已獲得 {CheatMoneyAmount:N0} 元（目前 {sm.Currency:N0}）。", true);
            AlertPanel.Toast($"作弊：獲得 {CheatMoneyAmount:N0} 元");
        }

        /// <summary>
        /// 一鍵取得所有武器：物品表裡**每一種武器各給一把**，背包滿了就停在那裡。
        /// 試各種武器手感時不用一顆一顆填 ID。
        ///
        /// 【兩個刻意的決定】
        /// ① **來源是「物品表」不是「武器表」**——背包裝的是物品，武器表的一列要有對應的物品才拿得到。
        ///    武器表裡有些是怪物/Boss 專用（例：紅嫁衣的「召喚家人」），本來就沒有給玩家的物品，
        ///    所以從物品表這一側列舉才不會給出一堆玩家根本裝不上的東西。
        /// ② **判斷用 `EquipSlot == Weapon`，不是 `WeaponID > 0`**——劇本道具「劇本-紅嫁衣」(104)
        ///    也填了 WeaponID（它要指定關卡用的武器），但它不是武器、裝不上武器欄。用 WeaponID 判斷會誤給。
        ///
        /// 已經有的（背包裡或身上穿著的）會跳過，所以連按幾次都不會塞出一堆重複的。
        /// 要重骰孔位請用「鑲嵌」分頁的「重開孔位」，不是重複給武器。
        /// </summary>
        void OnClickGiveAllWeapons()
        {
            var inv = InventorySystem.Instance;
            if (inv == null) { SetGiveStatus("找不到背包系統（InventorySystem）。", false); return; }
            if (inv.Db == null) { SetGiveStatus("物品表還沒載入。", false); return; }

            // 先收集再排序：Dictionary 的走訪順序不保證，不排的話每次按背包裡的排列都不一樣。
            var ids = new List<int>();
            foreach (var kv in inv.Db.Items)
                if (kv.Value != null && kv.Value.EquipSlot == EquipSlot.Weapon) ids.Add(kv.Key);
            ids.Sort();

            if (ids.Count == 0)
            {
                SetGiveStatus("物品表裡沒有任何武器（EquipSlot = Weapon）。", false);
                return;
            }

            int given = 0, already = 0, full = 0;
            foreach (int id in ids)
            {
                if (inv.HasAnywhere(id)) { already++; continue; }
                // 一定要走 ItemManager.Create——武器需要實例資料（孔位要現場骰），
                // 直接 AddItem(id) 會拿到一把沒有孔的裸裝。見 readme/GEM_SOCKET.md。
                if (inv.AddStack(ItemManager.Create(id, 1)) > 0) full++;
                else given++;
            }

            var sb = new System.Text.StringBuilder();
            sb.Append($"物品表共 {ids.Count} 把武器：新給 {given} 把");
            if (already > 0) sb.Append($"、已經有 {already} 把（跳過）");
            if (full > 0) sb.Append($"、{full} 把因裝備包已滿放不下");
            sb.Append('。');
            SetGiveStatus(sb.ToString(), full == 0);
            AlertPanel.Toast(given > 0 ? $"作弊：武器 ×{given}" : "作弊：武器已經都有了");
        }

        void OnClickGive()
        {
            var inv = InventorySystem.Instance;
            if (inv == null) { SetGiveStatus("找不到背包系統（InventorySystem）。", false); return; }

            // 解析 ID
            if (!int.TryParse(_idInput != null ? _idInput.text : "", out int id) || id <= 0)
            {
                SetGiveStatus("請輸入有效的物品 ID（正整數）。", false);
                return;
            }

            // 解析數量（留空 = 1）
            string ct = _countInput != null ? _countInput.text : "";
            int count = 1;
            if (!string.IsNullOrEmpty(ct) && !int.TryParse(ct, out count)) count = 1;
            if (count <= 0) count = 1;

            // 驗證物品存在
            var data = inv.GetData(id);
            if (data == null)
            {
                SetGiveStatus($"物品表裡沒有 ID {id}（請確認 ItemTable.csv）。", false);
                return;
            }

            // 走「取得物品的統一入口」，但**指定直接進真背包**（toRealBag）：
            // 作弊給的東西不是「這趟關卡的收穫」，進臨時包的話死亡就歸零、要通關才落袋，
            // 測試時（尤其在競技場裡調裝備）等於東西給了卻看不到，所以一律跳過臨時包。
            // 走統一入口是為了保留另外兩件事：需要實例的物品（裝備/能力珠）會經 ItemManager 骰好孔位，
            // 給 101 銅錢會自動轉成金錢數字（不佔背包格）。
            int leftover = RunProgress.Exists ? RunProgress.Instance.GiveItem(id, count, toRealBag: true)
                                              : inv.AddItem(id, count);
            int added = count - leftover;
            string name = string.IsNullOrEmpty(data.Name) ? $"#{id}" : data.Name;

            if (leftover > 0)
                SetGiveStatus($"背包已滿：{name} 放入 {added} 個，還有 {leftover} 個放不下。", added > 0);
            else
                SetGiveStatus($"已給予 {name} ×{added}（ID {id}）。", true);

            AlertPanel.Toast(added > 0 ? $"作弊：獲得 {name} ×{added}" : $"背包已滿，{name} 放不下");
        }

        void SetGiveStatus(string msg, bool ok)
        {
            if (_giveStatus == null) return;
            _giveStatus.text = msg;
            _giveStatus.color = ok ? ColOk : ColErr;
        }

        // ───────────────────────── 版面小工具 ─────────────────────────

        // 座標映射：把元件錨到父物件左上角、anchoredPosition=(px,-py)（與 SettingsPanel.Place 同慣例）。
        /// <summary>
        /// 畫一塊「功能分組」的底板＋小標題。不同功能用不同底色隔開，
        /// 之後作弊項目變多時才不會糊成一片按鈕海。控制項照樣用 Place 擺在這塊範圍內。
        /// </summary>
        void AddGroup(RectTransform root, string title, float y, float h, Color tint)
        {
            var bg = UIBuilder.Image(root, $"Group_{title}", null, tint);
            bg.raycastTarget = false;
            Place(bg.rectTransform, 0f, y, GroupW, h);

            var t = UIBuilder.Text(root, $"GroupTitle_{title}", title, 20, ColAccent, TextAnchor.MiddleLeft);
            Place(t.rectTransform, 14f, y + 8f, GroupW - 28f, 26f);
        }

        void AddDivider(RectTransform root, float y)
        {
            var line = UIBuilder.Image(root, "Divider", null, ColDivider);
            line.raycastTarget = false;
            Place(line.rectTransform, 0f, y, GroupW, 2f);
        }

        /// <summary>分組底板寬度（內容區 = FrameW - NavW - 左右內距）。</summary>
        const float GroupW = FrameW - NavW - Pad * 2f;

        void Place(RectTransform rt, float px, float py, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(px, -py);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
