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
    ///      給 101 銅錢會轉成金錢數字）。另有一鍵快捷：給錢、**取得所有武器（每種一把）**、**取得所有血統藥劑（每種一瓶）**。
    ///   2.「鑲嵌」：改裝備中武器的孔數、每種能力珠各給一顆 Lv3、給測試防具。
    ///   3.「玩家」：無敵開關＝具名不死保護 <c>CombatStats.SetDeathGuard("CheatPanel", …)</c>（照常被打、扣血、跳數字，只是不會死；不存檔）。
    ///   4.「動作」：idle／walk／attack／dead 四顆互斥開關，開一顆＝主角一直輪播那個動作（測序列圖用，dead 也看得到）；
    ///      再按一次同一顆＝全關；玩家按 WASD 移動或真的攻擊出去也會自動關。實作在 <c>PlayerAnimator.PreviewState</c>。不存檔。
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
        InputField _idInput, _countInput, _specInput;
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
            AddSection("玩家", BuildPlayerSection);
            AddSection("動作", BuildAnimSection);
            AddSection("狀態", BuildStatusSection);   // buff／debuff／DoT（2026-11-18，readme/STATUS_EFFECT.md）

            ShowSection(0);
        }

        protected override void OnOpen()
        {
            if (_giveStatus != null) _giveStatus.text = "";   // 每次開啟清掉上次的結果提示
            RefreshInvincibleButton();                         // 開關狀態以靜態旗標為準（換場景後面板重建也對得上）
            RefreshAnimButtons();                              // 同上：以 PlayerAnimator.PreviewState 為準
            RefreshStatusInfo();                               // 狀態分頁：玩家目前身上的狀態
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
                "物品 ID 見 Assets/Data/ItemTable.csv（例：201 回血藥）。規格：q=品質1~4、s=孔數、fx=效果ID@等級|…、lv=珠子等級，用 ; 分隔；留空＝裝備給一般 6 孔、填「隨機」＝照遊戲規則骰。",
                18, new Color(1f, 1f, 1f, 0.6f), TextAnchor.UpperLeft);
            Place(hint.rectTransform, 0f, 46f, GroupW, 30f);

            // ── 第一組：依 ID 給道具 ──
            //    ID、數量、確認是同一件事的三個步驟，排成一列才好操作（填、填、按）。
            const float G1Y = 92f, G1H = 180f, Row1Y = 136f, Row2Y = 192f, RowH = 48f;
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

            // 第二列：生成規格（品質系統，readme/EQUIPMENT.md §6）——跟兌換表、掉落表、劇本 giveItem 同一個解析器
            var specLabel = UIBuilder.Text(root, "SpecLabel", "規格", 22, Color.white, TextAnchor.MiddleLeft);
            Place(specLabel.rectTransform, 14f, Row2Y, 110f, RowH);
            _specInput = UIBuilder.InputField(root, "SpecInput", "例：q=4　或　fx=6@2|1@3（留空＝一般 6 孔；隨機＝照規則骰）", 22, 64, ColInputBg);
            Place((RectTransform)_specInput.transform, 129f, Row2Y, 680f, RowH);

            AddDivider(root, 288f);

            // ── 第二組：一鍵快捷（不用填任何欄位）──
            const float G2Y = 302f, G2H = 118f;
            AddGroup(root, "一鍵快捷", G2Y, G2H, ColGroupB);

            var moneyBtn = UIBuilder.Button(root, "MoneyBtn", $"獲得 {CheatMoneyAmount:N0} 功德值",
                                            OnClickGiveMoney, ColBtn);
            Place((RectTransform)moneyBtn.transform, 14f, 344f, 260f, 56f);

            var allWeaponBtn = UIBuilder.Button(root, "AllWeaponBtn", "取得所有武器（每種一把）",
                                                OnClickGiveAllWeapons, ColBtn);
            Place((RectTransform)allWeaponBtn.transform, 289f, 344f, 330f, 56f);

            var allBloodBtn = UIBuilder.Button(root, "AllBloodBtn", "獲得所有血統藥劑",
                                               OnClickGiveAllBloodlinePotions, ColBtn);
            Place((RectTransform)allBloodBtn.transform, 634f, 344f, 330f, 56f);

            // 狀態列（本次結果）——兩組共用，放在最下面
            _giveStatus = UIBuilder.Text(root, "GiveStatus", "", 22, ColOk, TextAnchor.UpperLeft);
            Place(_giveStatus.rectTransform, 0f, 440f, GroupW, 80f);
        }

        // ───────────────────────── 分頁：玩家（無敵…）─────────────────────────
        //
        // 測頭目時不想一直死。**無敵＝不死，不是不受傷**（作者 2026-10-27）：照常被擊中、閃白、擊退、跳數字、扣血，
        // 只是最多扣到剩 1 血——這樣才分得出「怪沒打到」和「打到了但我開著無敵」。
        // 走 CombatStats 既有的具名不死保護（教學劇情也在用，具名所以互不干擾）；進 Play 時會被清掉，按鈕狀態直接讀它。

        Button _invincibleBtn;
        static readonly Color ColToggleOn = new Color(0.20f, 0.50f, 0.25f, 1f);

        void BuildPlayerSection(RectTransform root)
        {
            var head = UIBuilder.Text(root, "Head", "玩家", 28, ColAccent, TextAnchor.UpperLeft);
            Place(head.rectTransform, 0f, 0f, 600f, 40f);

            var hint = UIBuilder.Text(root, "Hint",
                "測試用狀態開關；不會存檔，重新進 Play／重開遊戲就恢復關閉。",
                18, new Color(1f, 1f, 1f, 0.6f), TextAnchor.UpperLeft);
            Place(hint.rectTransform, 0f, 46f, GroupW, 30f);

            const float G1Y = 92f, G1H = 124f, Row1Y = 136f;
            AddGroup(root, "無敵（照常受傷、扣血、跳數字，但血最多扣到 1、不會死）", G1Y, G1H, ColGroupA);

            // ⚠ 標籤不能傳空字串：UIBuilder.Button 遇到空標籤**不會建 Text 子物件**，之後 RefreshInvincibleButton 就找不到字可以改（按鈕會一片空白）。
            _invincibleBtn = UIBuilder.Button(root, "InvincibleBtn", InvincibleLabel(), OnClickToggleInvincible, ColBtn);
            Place((RectTransform)_invincibleBtn.transform, 14f, Row1Y, 260f, 56f);
            RefreshInvincibleButton();
        }

        void OnClickToggleInvincible()
        {
            bool on = !CombatStats.HasDeathGuard(CheatGuardOwner);
            CombatStats.SetDeathGuard(CheatGuardOwner, on);
            RefreshInvincibleButton();
            AlertPanel.Toast(on ? "作弊：無敵 開（不會死）" : "作弊：無敵 關");
        }

        const string CheatGuardOwner = "CheatPanel";
        static string InvincibleLabel() => CombatStats.HasDeathGuard(CheatGuardOwner) ? "無敵：開" : "無敵：關";

        void RefreshInvincibleButton()
        {
            if (_invincibleBtn == null) return;
            bool on = CombatStats.HasDeathGuard(CheatGuardOwner);
            var lbl = _invincibleBtn.GetComponentInChildren<Text>();
            if (lbl != null) lbl.text = InvincibleLabel();
            var img = _invincibleBtn.GetComponent<Image>();
            if (img != null) img.color = on ? ColToggleOn : ColBtn;
        }

        // ───────────────────────── 分頁：動作（序列圖預覽）─────────────────────────
        //
        // 測新血統的序列圖用（作者 2026-11-11）：地圖裡平常只看得到 idle／walk／attack，dead 要真的死才看得到。
        // 四顆**互斥**開關：開一顆＝主角一直輪播那個動作；開另一顆＝前一顆自動關；再按亮著的那顆＝全關、回正常。
        // 玩家一操作（WASD 移動／真的攻擊出去）也自動全關（PlayerController 呼叫 PlayerAnimator.EndPreview）——
        // 按鈕外觀每次開面板都從 PreviewState 重讀，所以再開 L 時自然顯示成關閉。
        // 狀態存在 static 的 PlayerAnimator.PreviewState（換圖、換血統都持續生效），按鈕外觀一律從它讀。
        // 面板開著時遊戲暫停，所以要**關掉面板**才看得到動畫。

        static readonly (PlayerAnimator.State state, string label)[] AnimOptions =
        {
            (PlayerAnimator.State.Idle,   "idle"),
            (PlayerAnimator.State.Walk,   "walk"),
            (PlayerAnimator.State.Attack, "attack"),
            (PlayerAnimator.State.Dead,   "dead"),
        };
        readonly List<Button> _animButtons = new List<Button>();
        Text _animStatus;

        void BuildAnimSection(RectTransform root)
        {
            var head = UIBuilder.Text(root, "Head", "動作", 28, ColAccent, TextAnchor.UpperLeft);
            Place(head.rectTransform, 0f, 0f, 600f, 40f);

            var hint = UIBuilder.Text(root, "Hint",
                "開一個動作，主角就會一直輪播它（關掉面板才看得到）。一次只能開一個；再按一次亮著的那顆、或用 WASD 移動／攻擊，就會關閉。\n" +
                "dead／attack 播完會在最後一幀停 " + PlayerAnimator.PreviewEndHold.ToString("0.#") + " 秒再重播；attack 播全部幀（不是實戰的起播～結束幀）。不存檔。",
                18, new Color(1f, 1f, 1f, 0.6f), TextAnchor.UpperLeft);
            Place(hint.rectTransform, 0f, 46f, GroupW, 56f);

            const float G1Y = 116f, G1H = 124f, Row1Y = 160f;
            const float BtnW = 200f, BtnGap = 16f;
            AddGroup(root, "輪播動作（互斥，可全關）", G1Y, G1H, ColGroupB);

            _animButtons.Clear();
            for (int i = 0; i < AnimOptions.Length; i++)
            {
                var opt = AnimOptions[i];
                // ⚠ 標籤不能傳空字串（理由同 InvincibleBtn：空標籤不會建 Text 子物件）。
                var btn = UIBuilder.Button(root, $"AnimBtn_{opt.label}", opt.label, () => OnClickAnim(opt.state), ColBtn);
                Place((RectTransform)btn.transform, 14f + i * (BtnW + BtnGap), Row1Y, BtnW, 56f);
                _animButtons.Add(btn);
            }

            _animStatus = UIBuilder.Text(root, "AnimStatus", "", 20, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperLeft);
            Place(_animStatus.rectTransform, 14f, G1Y + G1H + 16f, GroupW - 28f, 90f);

            RefreshAnimButtons();
        }

        void OnClickAnim(PlayerAnimator.State s)
        {
            bool turnOff = PlayerAnimator.PreviewState.HasValue && PlayerAnimator.PreviewState.Value == s;
            PlayerAnimator.PreviewState = turnOff ? (PlayerAnimator.State?)null : s;
            RefreshAnimButtons();

            if (turnOff) { AlertPanel.Toast("作弊：動作預覽 關"); return; }
            var anim = FindPlayerAnimator();
            if (anim != null && anim.FrameCount(s) == 0)
                AlertPanel.Toast($"作弊：目前血統沒有 {s.ToString().ToLowerInvariant()} 的圖，畫面不會變");
            else
                AlertPanel.Toast($"作弊：輪播 {s.ToString().ToLowerInvariant()}（關掉面板觀看）");
        }

        void RefreshAnimButtons()
        {
            var cur = PlayerAnimator.PreviewState;
            for (int i = 0; i < _animButtons.Count && i < AnimOptions.Length; i++)
            {
                var img = _animButtons[i] != null ? _animButtons[i].GetComponent<Image>() : null;
                if (img != null) img.color = (cur.HasValue && cur.Value == AnimOptions[i].state) ? ColToggleOn : ColBtn;
            }
            if (_animStatus == null) return;

            var anim = FindPlayerAnimator();
            var pc = anim != null ? anim.GetComponent<PlayerController>() : null;
            if (anim == null) { _animStatus.text = "找不到主角（不在地圖裡？）"; return; }
            var sb = new System.Text.StringBuilder();
            sb.Append("目前血統：").Append(pc != null ? pc.Bloodline : "?").Append('\n');
            sb.Append("幀數　");
            foreach (var opt in AnimOptions)
            {
                int n = anim.FrameCount(opt.state);
                sb.Append(opt.label).Append(' ').Append(n > 0 ? n.ToString() : "（無圖）").Append("　");
            }
            sb.Append('\n').Append("預覽中：").Append(cur.HasValue ? cur.Value.ToString().ToLowerInvariant() : "無");
            _animStatus.text = sb.ToString();
        }

        static PlayerAnimator FindPlayerAnimator()
        {
            var pc = UnityEngine.Object.FindObjectOfType<PlayerController>();
            return pc != null ? pc.GetComponent<PlayerAnimator>() : null;
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

        /// <summary>
        /// 作弊面板給裝備的預設規格：**一般品質（沒有任何內建效果）＋ 6 孔全開**（2026-11-17 作者定）。
        /// 測試時要什麼效果自己鑲珠子，或在規格欄寫死；想要全隨機在規格欄填「隨機」。
        /// </summary>
        static ItemSpec CheatEquipSpec => new ItemSpec { Quality = 1, Sockets = ItemInstance.SocketMax };

        // 測試用防具（護身符/戒指）——驗證「珠子鑲在別的裝備上也會加到武器身上」。
        void OnClickGiveArmor()
        {
            var inv = Dipan.Inventory.InventorySystem.Instance;
            if (inv == null) { SetSocketStatus("找不到背包系統。", false); return; }
            int ok = 0;
            foreach (int id in new[] { 501, 502 })
            {
                if (inv.GetData(id) == null) continue;
                if (inv.AddStack(Dipan.Inventory.ItemManager.Create(id, 1, CheatEquipSpec)) == 0) ok++;
            }
            SetSocketStatus(ok > 0 ? $"已給予 {ok} 件測試防具。" : "背包已滿或物品表裡沒有 501/502。", ok > 0);
        }

        /// <summary>作弊快捷鈕一次給多少錢。</summary>
        const int CheatMoneyAmount = 10000;

        // 直接加金錢數字（金錢不是背包道具，所以走 SaveManager，不經過背包）。
        void OnClickGiveMoney()
        {
            var sm = Dipan.Save.SaveManager.Instance;
            if (sm == null || !sm.HasActiveCharacter)
            {
                SetGiveStatus("還沒載入角色，現在沒有錢包可以加錢。", false);
                return;
            }
            sm.AddCurrency(CheatMoneyAmount);
            SetGiveStatus($"已獲得 {CheatMoneyAmount:N0} 功德值（目前 {sm.Currency:N0}）。", true);
            AlertPanel.Toast($"作弊：獲得 {CheatMoneyAmount:N0} 功德值");
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
                if (inv.AddStack(ItemManager.Create(id, 1, CheatEquipSpec)) > 0) full++;   // 一般、6 孔（沒有內建效果）
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

        /// <summary>
        /// 一鍵取得所有血統藥劑：物品表裡**每一種血統藥劑各給一瓶**。涵蓋兩類，
        /// 判斷一律走 <see cref="ItemData"/> 的欄位、**不比對名稱前綴**（之後新增藥劑不必回來改這裡）：
        ///   ① **系列起始藥劑**（`IsBloodlineStarter`，即 CSV 的 `BloodlineID > 0`）：ID＝3000＋血統 Id（例 3010 殭屍、3020 血族），見 BLOODLINE §3。
        ///   ② **進階藥劑**（`IsBloodlineUpgrade`，即 CSV 的 `BloodlineUpgrade > 0`）：310 中階、311 高階，全系列通用。
        ///
        /// 每種一瓶（起始藥劑本來就 MaxStack=1）。進階藥劑要更多就連按幾次，或用上面的「依 ID 給道具」。
        /// 這裡只負責把東西塞進背包：**能不能喝**（起始藥劑一世一次不可逆、進階藥劑必須逐階）
        /// 由 ItemUse / BloodlineSystem 判定，見 readme/BLOODLINE.md。
        ///
        /// 與「取得所有武器」不同，這裡**不跳過已經有的**——藥劑是消耗品，喝掉就沒了，
        /// 「再按一次補一瓶」比「已經有就不給」實用。
        /// </summary>
        void OnClickGiveAllBloodlinePotions()
        {
            var inv = InventorySystem.Instance;
            if (inv == null) { SetGiveStatus("找不到背包系統（InventorySystem）。", false); return; }
            if (inv.Db == null) { SetGiveStatus("物品表還沒載入。", false); return; }

            // 先收集再排序：Dictionary 的走訪順序不保證，不排的話每次進背包的排列都不一樣。
            var ids = new List<int>();
            foreach (var kv in inv.Db.Items)
                if (kv.Value != null && kv.Value.IsBloodline) ids.Add(kv.Key);
            ids.Sort();

            if (ids.Count == 0)
            {
                SetGiveStatus("物品表裡沒有任何血統藥劑（BloodlineID 與 BloodlineUpgrade 都是 0）。", false);
                return;
            }

            int given = 0, full = 0;
            foreach (int id in ids)
            {
                // 與「依 ID 給道具」走同一條路：統一入口 + 指定 toRealBag，
                // 走取得物品的統一入口（toRealBag 已無作用，2026-11-06 拿掉臨時包後一律進真背包）。
                int leftover = RunProgress.Exists ? RunProgress.Instance.GiveItem(id, 1, toRealBag: true)
                                                  : inv.AddItem(id, 1);
                if (leftover > 0) full++;
                else given++;
            }

            SetGiveStatus(full > 0 ? $"血統藥劑共 {ids.Count} 種：給了 {given} 瓶，{full} 瓶因消耗品包已滿放不下。"
                                   : $"血統藥劑共 {ids.Count} 種，每種各給一瓶。", full == 0);
            AlertPanel.Toast(given > 0 ? $"作弊：血統藥劑 ×{given}" : "背包已滿，血統藥劑放不下");
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

            // 解析規格。格式錯就不給，直接說哪裡錯。
            // 留空：裝備＝一般品質＋6 孔（CheatEquipSpec，作者 2026-11-17 定：作弊給的裝備一律沒有內建效果）；其他物品＝照規則骰。
            // 填「隨機」＝完全照遊戲規則骰（品質、效果、孔數都隨機）。
            string specText = (_specInput != null ? _specInput.text : "").Trim();
            ItemSpec spec;
            bool fullRandom = specText == "隨機" || specText == "随机" || specText.Equals("random", System.StringComparison.OrdinalIgnoreCase);
            if (fullRandom) spec = null;
            else
            {
                if (!ItemSpec.TryParse(specText, out spec, out var specErr))
                {
                    SetGiveStatus($"規格有誤：{specErr}", false);
                    return;
                }
                if (spec.IsEmpty) spec = data.IsEquippable ? CheatEquipSpec : null;
            }

            // 走「取得物品的統一入口」，但**指定直接進真背包**（toRealBag）：
            // 走取得物品的統一入口（一律進真背包；toRealBag 已無作用，2026-11-06 拿掉臨時包）。
            // 走統一入口是為了保留另外兩件事：需要實例的物品（裝備/能力珠）會經 ItemManager 骰好孔位，
            // 給 101 銅錢會自動轉成金錢數字（不佔背包格）。
            int leftover = RunProgress.Exists ? RunProgress.Instance.GiveItem(id, count, toRealBag: true, spec: spec)
                                              : GiveWithoutRun(inv, data, id, count, spec);
            int added = count - leftover;
            string name = string.IsNullOrEmpty(data.Name) ? $"#{id}" : data.Name;
            string specNote = spec == null ? ""
                : ItemManager.NeedsInstance(data) ? $"，規格 [{spec}]" : "；它不是裝備或能力珠，規格沒有作用";

            if (leftover > 0)
                SetGiveStatus($"背包已滿：{name} 放入 {added} 個，還有 {leftover} 個放不下。", added > 0);
            else
                SetGiveStatus($"已給予 {name} ×{added}（ID {id}{specNote}）。滑鼠移到那一件上可看品質與效果。", true);

            AlertPanel.Toast(added > 0 ? $"作弊：獲得 {name} ×{added}" : $"背包已滿，{name} 放不下");
        }

        /// <summary>沒有 RunProgress（單獨測場景）時的退路：需要實例的物品一件一件照規格產生，其餘直接加。</summary>
        static int GiveWithoutRun(InventorySystem inv, ItemData data, int id, int count, ItemSpec spec)
        {
            if (!ItemManager.NeedsInstance(data)) return inv.AddItem(id, count);
            int left = 0;
            for (int i = 0; i < count; i++) left += inv.AddStack(ItemManager.Create(id, 1, spec));
            return left;
        }

        void SetGiveStatus(string msg, bool ok)
        {
            if (_giveStatus == null) return;
            _giveStatus.text = msg;
            _giveStatus.color = ok ? ColOk : ColErr;
        }

        // ───────────────────────── 分頁：狀態效果（buff／debuff／DoT）─────────────────────────
        //
        // 第②階段驗收用（2026-11-18，readme/STATUS_EFFECT.md）：直接對玩家或附近怪物上狀態。
        // ‧ DoT 一律用「固定值型」（作弊沒有一下命中傷害可以當百分比基準）。
        // ‧ 控制類（暈眩、冰凍）：作弊按鈕略過機率必中；兩種可同時存在、各自計時（2026-11-18 晚作者定）。
        // ‧ 狀態列 UI 第③階段才做；現在靠下方文字＋頭上的 DoT 數字、移速、傷害數字變化來看。

        int _statusLv = 1;
        Button _statusLvBtn;
        Text _statusInfo;
        static readonly int[] StatusLvSteps = { 1, 3, 5, 10 };
        const float StatusMonsterRadius = 8f;
        readonly List<StatusController.View> _statusViews = new List<StatusController.View>();
        readonly List<string> _triggerSummary = new List<string>();

        static List<StatusEffectDef> TestableStatuses()
        {
            var list = new List<StatusEffectDef>();
            foreach (var d in StatusEffectDatabase.All) list.Add(d);   // 第⑤階段起暈眩、冰凍也有行為了，一起放
            return list;
        }

        void BuildStatusSection(RectTransform root)
        {
            var head = UIBuilder.Text(root, "Head", "狀態效果", 28, ColAccent, TextAnchor.UpperLeft);
            Place(head.rectTransform, 0f, 0f, 600f, 40f);

            var hint = UIBuilder.Text(root, "Hint",
                "按鈕是開關：按下＝常駐（不會到期），再按一次＝移除。DoT 用固定值型；暈眩、冰凍必中（兩種可以同時開，冰凍時動作定格優先）。",
                18, new Color(1f, 1f, 1f, 0.6f), TextAnchor.UpperLeft);
            Place(hint.rectTransform, 0f, 46f, GroupW, 30f);

            var defs = TestableStatuses();
            const float BtnW = 156f, BtnH = 46f, GapX = 10f, GapY = 10f;
            const int PerRow = 6;   // 11 種狀態排兩列（第⑤階段多了暈眩、冰凍）
            int rows = Mathf.Max(1, (defs.Count + PerRow - 1) / PerRow);
            float groupH = 40f + rows * (BtnH + GapY) + 8f;

            float g1Y = 92f;
            AddGroup(root, "給玩家", g1Y, groupH, ColGroupA);
            float g2Y = g1Y + groupH + 14f;
            AddGroup(root, $"給附近怪物（玩家 {StatusMonsterRadius:0} 格內所有敵對怪；頭目對減速免疫）", g2Y, groupH, ColGroupB);

            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                float x = 14f + (i % PerRow) * (BtnW + GapX);
                float dy = 40f + (i / PerRow) * (BtnH + GapY);
                var bp = UIBuilder.Button(root, $"StP_{d.Key}", d.Name, () => OnClickStatusToPlayer(d), ColBtn);
                Place((RectTransform)bp.transform, x, g1Y + dy, BtnW, BtnH);
                _statusBtnP[d.Id] = bp;
                var bm = UIBuilder.Button(root, $"StM_{d.Key}", d.Name, () => OnClickStatusToMonsters(d), ColBtn);
                Place((RectTransform)bm.transform, x, g2Y + dy, BtnW, BtnH);
                _statusBtnM[d.Id] = bm;
            }

            float rowY = g2Y + groupH + 14f;
            _statusLvBtn = UIBuilder.Button(root, "StLv", StatusLvLabel(), OnClickStatusLv, ColBtn);
            Place((RectTransform)_statusLvBtn.transform, 0f, rowY, 180f, 52f);
            var clrP = UIBuilder.Button(root, "StClrP", "清除玩家狀態", OnClickStatusClearPlayer, ColBtn);
            Place((RectTransform)clrP.transform, 194f, rowY, 220f, 52f);
            var clrM = UIBuilder.Button(root, "StClrM", "清除附近怪物狀態", OnClickStatusClearMonsters, ColBtn);
            Place((RectTransform)clrM.transform, 428f, rowY, 260f, 52f);
            // 第④階段（2026-11-18）：狀態珠（GemTable 的狀態效果列）各給一顆，鑲上去測命中／發射／受擊觸發
            var gemsBtn = UIBuilder.Button(root, "StGems", "狀態珠各給一顆（Lv3）", OnClickGiveStatusGems, ColBtn);
            Place((RectTransform)gemsBtn.transform, 702f, rowY, 320f, 52f);
            // 第⑥階段（2026-11-18）：三件狀態測試裝備（良品、內建效果寫死 Lv3、6 孔），只從這裡拿
            var eqBtn = UIBuilder.Button(root, "StEquip", "給狀態測試裝備（蓮華鱗甲＝受擊護體・青芽護手＝受擊回春・風翎行靴＝受擊疾行）", OnClickGiveStatusEquip, ColBtn);
            Place((RectTransform)eqBtn.transform, 0f, rowY + 62f, 1022f, 52f);

            // 第⑥階段：效能壓測（玩家四周生一圈怪、每隻上滿狀態；左上角顯示幀時間）
            var s50 = UIBuilder.Button(root, "StStress50", "壓測：生 50 隻上狀態", () => OnClickStress(50), ColBtn);
            Place((RectTransform)s50.transform, 0f, rowY + 124f, 250f, 52f);
            var s100 = UIBuilder.Button(root, "StStress100", "壓測：生 100 隻", () => OnClickStress(100), ColBtn);
            Place((RectTransform)s100.transform, 264f, rowY + 124f, 230f, 52f);
            var sClr = UIBuilder.Button(root, "StStressClr", "清掉壓測怪", OnClickStressClear, ColBtn);
            Place((RectTransform)sClr.transform, 508f, rowY + 124f, 200f, 52f);
            var sProbe = UIBuilder.Button(root, "StProbe", "效能顯示 開／關", () => StatusPerfProbe.Toggle(), ColBtn);
            Place((RectTransform)sProbe.transform, 722f, rowY + 124f, 300f, 52f);

            // 效能記錄（2026-11-18 作者：自己抄數字容易抄錯）：開始 → 關面板玩一段 → 開面板按結束，自動寫 TempImage/perf_logs/perf_log.csv
            var recA = UIBuilder.Button(root, "StRecStart", "開始記錄", () => OnClickRecordStart(null), ColBtn);
            Place((RectTransform)recA.transform, 0f, rowY + 186f, 250f, 52f);
            var recB = UIBuilder.Button(root, "StRecFire", "開始記錄（掃射中）", () => OnClickRecordStart("＋掃射"), ColBtn);
            Place((RectTransform)recB.transform, 264f, rowY + 186f, 300f, 52f);
            var recEnd = UIBuilder.Button(root, "StRecEnd", "結束記錄（寫檔）", OnClickRecordEnd, ColBtn);
            Place((RectTransform)recEnd.transform, 578f, rowY + 186f, 444f, 52f);

            _statusInfo = UIBuilder.Text(root, "StInfo", "", 20, ColOk, TextAnchor.UpperLeft);
            Place(_statusInfo.rectTransform, 0f, rowY + 252f, GroupW, 120f);
            RefreshStatusInfo();
        }

        string StatusLvLabel() => $"等級：Lv{_statusLv}（點我切換）";

        void OnClickStatusLv()
        {
            int idx = Array.IndexOf(StatusLvSteps, _statusLv);
            _statusLv = StatusLvSteps[(idx + 1) % StatusLvSteps.Length];
            var lbl = _statusLvBtn != null ? _statusLvBtn.GetComponentInChildren<Text>() : null;
            if (lbl != null) lbl.text = StatusLvLabel();
        }

        static PlayerController FindPlayer() => UnityEngine.Object.FindObjectOfType<PlayerController>();

        // ── 狀態開關（2026-11-18 作者：暈眩 1 秒關掉面板就結束了，連 tip 都來不及看 ⇒ 改成按下常駐、再按移除）──
        readonly Dictionary<int, Button> _statusBtnP = new Dictionary<int, Button>();
        readonly Dictionary<int, Button> _statusBtnM = new Dictionary<int, Button>();
        readonly HashSet<int> _monsterPinned = new HashSet<int>();                       // 怪物那組哪些開著
        readonly Dictionary<int, List<MonsterController>> _monsterPinnedOn = new Dictionary<int, List<MonsterController>>();   // 開的時候上到哪些怪

        void OnClickStatusToPlayer(StatusEffectDef d)
        {
            var pc = FindPlayer();
            if (pc == null || pc.Status == null) { AlertPanel.Toast("找不到玩家"); return; }
            if (pc.Status.IsPinned(d.Id))
            {
                pc.Status.Remove(d.Id);
                AlertPanel.Toast($"玩家：移除 {d.Name}");
            }
            else
            {
                var r = StatusSystem.Apply(pc.gameObject, d, _statusLv, null, 0f, true, forceChance: true, pinned: true);
                AlertPanel.Toast($"玩家：{d.Name} Lv{_statusLv} 常駐（{ResultText(r)}）");
            }
            RefreshStatusInfo();
        }

        void OnClickStatusToMonsters(StatusEffectDef d)
        {
            if (_monsterPinned.Remove(d.Id))
            {
                // 關：先前上過的怪＋現在附近的怪都拿掉
                int removed = 0;
                if (_monsterPinnedOn.TryGetValue(d.Id, out var list))
                    foreach (var m in list) if (m != null && !m.IsDead && m.Status != null && m.Status.Remove(d.Id)) removed++;
                ForEachNearbyFoe(m => { if (m.Status != null && m.Status.Remove(d.Id)) removed++; });
                _monsterPinnedOn.Remove(d.Id);
                AlertPanel.Toast($"怪物：移除 {d.Name}（{removed} 隻）");
            }
            else
            {
                int n = 0, immune = 0;
                var srcPc = FindPlayer();
                GameObject srcGo = srcPc != null ? srcPc.gameObject : null;
                var list = new List<MonsterController>();
                ForEachNearbyFoe(m =>
                {
                    var r = StatusSystem.Apply(m.gameObject, d, _statusLv, srcGo, 0f, true, forceChance: true, pinned: true);
                    if (r == StatusController.ApplyResult.Immune) immune++; else { n++; list.Add(m); }
                });
                _monsterPinned.Add(d.Id);
                _monsterPinnedOn[d.Id] = list;
                AlertPanel.Toast($"{d.Name} Lv{_statusLv} 常駐：{n} 隻怪" + (immune > 0 ? $"（{immune} 隻免疫）" : ""));
            }
            RefreshStatusToggles();
        }

        static string ResultText(StatusController.ApplyResult r)
        {
            switch (r)
            {
                case StatusController.ApplyResult.Applied: return "上身";
                case StatusController.ApplyResult.Refreshed: return "原本就有，改成常駐";
                case StatusController.ApplyResult.Immune: return "免疫";
                case StatusController.ApplyResult.Ignored: return "沒上身";
                default: return "沒上身（目標已死或無效）";
            }
        }

        /// <summary>開關按鈕的外觀：玩家那組看玩家身上實際有沒有常駐的（死亡、清除、離開關卡都會自己對上）；怪物那組看自己記的。</summary>
        void RefreshStatusToggles()
        {
            var pc = FindPlayer();
            var sc = pc != null ? pc.Status : null;
            foreach (var kv in _statusBtnP) SetToggleLook(kv.Value, sc != null && sc.IsPinned(kv.Key));
            foreach (var kv in _statusBtnM) SetToggleLook(kv.Value, _monsterPinned.Contains(kv.Key));
        }

        void SetToggleLook(Button b, bool on)
        {
            if (b == null) return;
            var img = b.GetComponent<Image>();
            if (img != null) img.color = on ? ColToggleOn : ColBtn;
            var lbl = b.GetComponentInChildren<Text>();
            if (lbl != null)
            {
                string name = lbl.text.StartsWith("● ") ? lbl.text.Substring(2) : lbl.text;
                lbl.text = on ? "● " + name : name;
            }
        }

        /// <summary>狀態測試裝備：ItemTable 504～506，內建效果＝GemTable 37／38／39（Lv3，單一來源上限），6 孔全開。</summary>
        static readonly (int itemId, string spec)[] StatusTestEquip =
        {
            (504, "fx=37@3;s=6"),   // 蓮華鱗甲（胸甲）：受擊護體
            (505, "fx=38@3;s=6"),   // 青芽護手（手套）：受擊回春
            (506, "fx=39@3;s=6"),   // 風翎行靴（鞋子）：受擊疾行
        };

        void OnClickGiveStatusEquip()
        {
            var inv = Dipan.Inventory.InventorySystem.Instance;
            if (inv == null) { AlertPanel.Toast("找不到背包系統"); return; }
            int ok = 0, full = 0;
            foreach (var (id, spec) in StatusTestEquip)
            {
                if (inv.GetData(id) == null) continue;
                var sp = Dipan.Inventory.ItemSpec.Parse(spec, $"作弊面板狀態測試裝備 {id}");
                if (inv.AddStack(Dipan.Inventory.ItemManager.Create(id, 1, sp)) == 0) ok++; else full++;
            }
            AlertPanel.Toast(full > 0 ? $"作弊：狀態測試裝備 ×{ok}（{full} 件放不下）" : $"作弊：狀態測試裝備 ×{ok}");
        }

        // ── 效能壓測（第⑥階段）──
        // 用 2～12 號小怪輪流（不同外觀＝不同貼圖，比較接近實戰的合批情況）；血量拉高，免得 DoT 幾秒就把壓測怪燒光。
        // 狀態在「下一幀」才上：怪物的受擊反應、影子等是在 Start 才掛，同一幀上狀態，身上的染色會找不到元件。
        static readonly int[] StressMonsterIds = { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        const float StressHp = 100000f;
        readonly List<MonsterController> _stressMonsters = new List<MonsterController>();
        readonly List<MonsterController> _stressPending = new List<MonsterController>();
        int _stressPendingFrame;

        void OnClickStress(int count)
        {
            var pc = FindPlayer();
            var spawner = UnityEngine.Object.FindObjectOfType<MonsterSpawner>();
            if (pc == null || spawner == null) { AlertPanel.Toast("找不到玩家或怪物生成器（要在關卡裡用）"); return; }
            Vector2 o = pc.transform.position;
            int made = 0;
            for (int i = 0; i < count; i++)
            {
                float ang = i * Mathf.PI * 2f / count;
                float r = 4f + (i % 3) * 1.6f;   // 三圈：4／5.6／7.2 格
                var go = spawner.SpawnMonster(StressMonsterIds[i % StressMonsterIds.Length],
                    o + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r, null, MonsterFaction.Enemy, null, false);
                var mc = go != null ? go.GetComponent<MonsterController>() : null;
                if (mc == null) continue;
                mc.DebugSetHealth(StressHp);
                _stressMonsters.Add(mc);
                _stressPending.Add(mc);
                made++;
            }
            _stressPendingFrame = Time.frameCount;
            if (!StatusPerfProbe.Visible) StatusPerfProbe.Toggle();
            AlertPanel.Toast($"壓測：生了 {made} 隻（下一幀上狀態；關掉面板後開始動）");
        }

        void Update()
        {
            if (_stressPending.Count == 0 || Time.frameCount <= _stressPendingFrame) return;
            var defs = new[] { "Burn", "Poison", "Slow", "Vulnerable", "Weak" };
            var pc = FindPlayer();
            GameObject src = pc != null ? pc.gameObject : null;
            foreach (var mc in _stressPending)
            {
                if (mc == null || mc.IsDead) continue;
                foreach (var key in defs)
                {
                    var d = StatusEffectDatabase.Get(key);
                    if (d == null) continue;
                    // 中毒疊 3 層（測頭上層數字）；DoT 用固定值
                    int times = key == "Poison" ? 3 : 1;
                    for (int t = 0; t < times; t++) StatusSystem.Apply(mc.gameObject, d, 1, src, 0f, true);
                }
            }
            _stressPending.Clear();
        }

        void OnClickRecordStart(string suffix)
        {
            if (StatusPerfProbe.Recording) { AlertPanel.Toast("已經在記錄了，先按「結束記錄」"); return; }
            int alive = 0;
            foreach (var m in MonsterController.Active) if (m != null && !m.IsDead) alive++;
            string label = $"怪物 {alive}" + (suffix ?? "");
            StatusPerfProbe.BeginRecord(label);
            AlertPanel.Toast($"開始記錄「{label}」：關掉面板玩 10 秒以上，再打開面板按「結束記錄」（面板開著的時間不算）");
        }

        void OnClickRecordEnd()
        {
            string msg = StatusPerfProbe.EndRecord();
            AlertPanel.Toast(msg ?? "現在沒有在記錄");
            if (msg != null && _statusInfo != null) _statusInfo.text = msg + "\n檔案：" + StatusPerfProbe.LogPath();
        }

        void OnClickStressClear()
        {
            int n = 0;
            foreach (var mc in _stressMonsters)
                if (mc != null && !mc.IsDead) { mc.Kill(); n++; }
            _stressMonsters.Clear();
            _stressPending.Clear();
            AlertPanel.Toast($"清掉 {n} 隻壓測怪");
        }

        void OnClickGiveStatusGems()
        {
            var inv = Dipan.Inventory.InventorySystem.Instance;
            var gems = Dipan.Inventory.ItemManager.Gems;
            if (inv == null || gems == null) { AlertPanel.Toast("找不到背包系統"); return; }
            int given = 0, full = 0;
            foreach (var kv in inv.Db.Items)
            {
                var d = kv.Value;
                if (d == null || !d.IsGem) continue;
                var g = gems.Get(d.GemID);
                if (g == null || !g.IsStatusEffect) continue;
                var st = Dipan.Inventory.ItemManager.CreateGem(kv.Key, 3);
                if (st.IsEmpty) continue;
                if (inv.AddStack(st) > 0) full++; else given++;
            }
            AlertPanel.Toast(full > 0 ? $"作弊：狀態珠 ×{given}（{full} 顆放不下）" : $"作弊：狀態珠 ×{given}");
        }

        void OnClickStatusClearPlayer()
        {
            StatusSystem.ClearPlayer();
            RefreshStatusInfo();
        }

        void OnClickStatusClearMonsters()
        {
            int n = 0;
            ForEachNearbyFoe(m => { if (m.Status != null) { m.Status.ClearAll(); n++; } });
            foreach (var kv in _monsterPinnedOn)
                foreach (var m in kv.Value) if (m != null && !m.IsDead && m.Status != null) m.Status.ClearAll();
            _monsterPinned.Clear();
            _monsterPinnedOn.Clear();
            RefreshStatusToggles();
            AlertPanel.Toast($"清除 {n} 隻怪的狀態");
        }

        void ForEachNearbyFoe(Action<MonsterController> act)
        {
            var pc = FindPlayer();
            if (pc == null) return;
            Vector2 o = pc.transform.position;
            float r2 = StatusMonsterRadius * StatusMonsterRadius;
            var all = MonsterController.Active;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var m = all[i];
                if (m == null || m.IsDead || m.IsCompanion) continue;
                if (m.Faction == MonsterFaction.PlayerAlly || m.Faction == MonsterFaction.Neutral) continue;
                if (((Vector2)m.transform.position - o).sqrMagnitude > r2) continue;
                act(m);
            }
        }

        void RefreshStatusInfo()
        {
            RefreshStatusToggles();
            if (_statusInfo == null) return;
            var pc = FindPlayer();
            var sc = pc != null ? pc.Status : null;
            if (sc == null) { _statusInfo.text = "玩家目前沒有狀態。"; return; }
            sc.GetViews(_statusViews);
            var sb = new System.Text.StringBuilder();
            // 觸發器（第④階段）：全身相加後目前生效的狀態效果
            if (pc.StatusTriggers != null)
            {
                pc.StatusTriggers.GetSummary(_triggerSummary);
                sb.Append(_triggerSummary.Count == 0 ? "觸發效果：無\n" : $"觸發效果：{string.Join("、", _triggerSummary)}\n");
            }
            sb.Append($"玩家倍率：造成傷害 ×{sc.OutgoingMultiplier:0.00}　受到傷害 ×{sc.IncomingMultiplier:0.00}　移速 ×{sc.MoveSpeedMultiplier:0.00}\n");
            if (_statusViews.Count == 0) sb.Append("玩家目前沒有狀態。");
            foreach (var v in _statusViews)
            {
                sb.Append($"{v.Def.Name} Lv{v.Level}");
                if (v.Stacks > 1) sb.Append($" ×{v.Stacks}層");
                sb.Append(v.Pinned ? "　常駐" : $"　剩 {v.Remaining:0.0} 秒");
                switch (v.Def.Category)
                {
                    case StatusCategory.Dot: sb.Append($"　每秒 {v.Magnitude:0.#} 傷害"); break;
                    case StatusCategory.Hot: sb.Append($"　每秒回 {v.Magnitude:0.#}"); break;
                    case StatusCategory.Stat: sb.Append($"　{v.Magnitude * 100f:+0;-0}%"); break;
                }
                sb.Append('\n');
            }
            _statusInfo.text = sb.ToString();
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
