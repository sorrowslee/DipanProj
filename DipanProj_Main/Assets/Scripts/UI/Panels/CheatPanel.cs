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
    ///   1.「給道具」：填入物品 ID + 數量 → 按確認 → 走 RunProgress.GiveItem（關卡內進臨時包、廣場進真背包；
    ///      給 101 銅錢會轉成金錢數字）。
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
                "物品 ID 見 Assets/Data/ItemTable.csv（例：201 回血藥）。",
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

            // 狀態列（本次結果）——兩組共用，放在最下面
            _giveStatus = UIBuilder.Text(root, "GiveStatus", "", 22, ColOk, TextAnchor.UpperLeft);
            Place(_giveStatus.rectTransform, 0f, 384f, GroupW, 80f);
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

            // 走「取得物品的統一入口」：關卡內進臨時包、廣場進真背包，
            // 而且給 101 銅錢時會自動轉成金錢數字（金錢不再佔背包格）。
            int leftover = RunProgress.Exists ? RunProgress.Instance.GiveItem(id, count) : inv.AddItem(id, count);
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
