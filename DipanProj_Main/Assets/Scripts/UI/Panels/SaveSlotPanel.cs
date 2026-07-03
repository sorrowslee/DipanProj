using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Save;
using Dipan.Flow;

namespace Dipan.UI
{
    /// <summary>
    /// 三欄存讀檔畫面（Window 層、全螢幕）。每欄 = 一條獨立進度線（一個角色）。
    /// - 空欄：顯示「空欄位」＋「新建遊戲」。
    /// - 有檔：顯示「周目 N ・ 完成 M 關 ・ 上次遊玩」＋ 繼續 / 覆蓋（ConfirmPopup 先問）/ 刪除（測試用，先問）。
    /// 資料只讀 roster 摘要（SaveManager.GetSlotProfile），動作呼叫 GameFlowManager。
    /// **佔位視覺**，之後換卡片/按鈕素材即可。見 readme/TITLE_AND_SAVE_UI.md。
    /// </summary>
    public class SaveSlotPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool CloseOnEscape => true;      // ESC = 返回標題（露出底下的 TitlePanel）
        public override bool ShowBackdrop => false;

        const float CardW = 460f, CardH = 660f, CardGap = 40f;

        RectTransform _cardsRoot;
        readonly List<GameObject> _cards = new List<GameObject>();

        protected override void OnBuild()
        {
            var bg = UIBuilder.SolidPanel(transform, "BG", new Color(0.07f, 0.06f, 0.09f, 1f));
            bg.raycastTarget = true;

            var title = UIBuilder.Text(transform, "Header", "選 擇 存 檔", 56,
                new Color(0.92f, 0.88f, 0.82f), TextAnchor.MiddleCenter);
            UIBuilder.Anchor(title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -110f), new Vector2(1200f, 90f));

            var rootGO = UIBuilder.Create("Cards", transform);
            _cardsRoot = UIBuilder.Rect(rootGO);
            UIBuilder.Anchor(_cardsRoot,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 10f), new Vector2(CardW * 3 + CardGap * 2, CardH));

            var back = UIBuilder.Button(transform, "BackButton", "返回", () => UIManager.Instance.Close(this),
                new Color(0.18f, 0.16f, 0.20f, 1f));
            back.targetGraphic = back.GetComponent<Image>();
            UIBuilder.Anchor((RectTransform)back.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 90f), new Vector2(240f, 76f));
        }

        protected override void OnOpen() => Refresh();

        /// <summary>依 roster 重畫三張卡片（新建/覆蓋/刪除後呼叫）。</summary>
        public void Refresh()
        {
            for (int i = 0; i < _cards.Count; i++) if (_cards[i] != null) Destroy(_cards[i]);
            _cards.Clear();

            int n = SaveConstants.SlotCount;
            float totalW = CardW * n + CardGap * (n - 1);
            float startX = -totalW * 0.5f + CardW * 0.5f;
            for (int i = 0; i < n; i++)
                BuildCard(i, startX + i * (CardW + CardGap));
        }

        void BuildCard(int slot, float x)
        {
            var cardGO = UIBuilder.Create($"Card{slot}", _cardsRoot);
            _cards.Add(cardGO);
            var cardRt = UIBuilder.Rect(cardGO);
            UIBuilder.Anchor(cardRt,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, 0f), new Vector2(CardW, CardH));

            var frame = UIBuilder.Image(cardGO.transform, "Frame", null, new Color(0.13f, 0.12f, 0.16f, 1f));
            UIBuilder.Stretch(frame.rectTransform);
            frame.raycastTarget = true;

            var head = UIBuilder.Text(cardGO.transform, "SlotHead", $"欄位 {slot + 1}", 34,
                new Color(0.85f, 0.82f, 0.78f), TextAnchor.MiddleCenter);
            UIBuilder.Anchor(head.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -46f), new Vector2(CardW - 40f, 60f));

            var prof = SaveManager.Instance != null ? SaveManager.Instance.GetSlotProfile(slot) : null;

            if (prof == null) BuildEmptyCard(cardGO.transform, slot);
            else BuildFilledCard(cardGO.transform, slot, prof);
        }

        // ── 空欄 ──
        void BuildEmptyCard(Transform card, int slot)
        {
            var empty = UIBuilder.Text(card, "Empty", "空 欄 位", 30,
                new Color(0.55f, 0.52f, 0.5f), TextAnchor.MiddleCenter);
            UIBuilder.Anchor(empty.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 60f), new Vector2(CardW - 60f, 60f));

            AddButton(card, "新建遊戲", new Vector2(0f, -180f), new Color(0.22f, 0.30f, 0.22f, 1f),
                () => DoNewGame(slot, false));
        }

        // ── 有檔 ──
        void BuildFilledCard(Transform card, int slot, CharacterProfile prof)
        {
            string info = $"周目 {Math.Max(1, prof.generation)}\n完成 {prof.clearedModuleCount} 關";
            var body = UIBuilder.Text(card, "Info", info, 32,
                new Color(0.90f, 0.86f, 0.80f), TextAnchor.MiddleCenter);
            UIBuilder.Anchor(body.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 120f), new Vector2(CardW - 60f, 130f));

            var played = UIBuilder.Text(card, "Played", "上次遊玩 " + PlayedText(prof.lastPlayedUtc), 20,
                new Color(0.6f, 0.58f, 0.56f), TextAnchor.MiddleCenter);
            UIBuilder.Anchor(played.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 34f), new Vector2(CardW - 40f, 40f));

            AddButton(card, "繼 續", new Vector2(0f, -70f), new Color(0.20f, 0.26f, 0.34f, 1f),
                () => { if (GameFlowManager.Instance != null) GameFlowManager.Instance.ContinueGame(slot); });

            AddButton(card, "覆蓋（新建）", new Vector2(0f, -170f), new Color(0.30f, 0.24f, 0.16f, 1f),
                () => ConfirmPopup.Show($"欄位 {slot + 1} 已有存檔，確定覆蓋並重新開始？", () => DoNewGame(slot, true)));

            AddButton(card, "刪除（測試）", new Vector2(0f, -258f), new Color(0.34f, 0.16f, 0.16f, 1f),
                () => ConfirmPopup.Show($"確定刪除欄位 {slot + 1} 的存檔？", () =>
                {
                    if (GameFlowManager.Instance != null) GameFlowManager.Instance.DeleteSlotForTest(slot);
                    else if (SaveManager.Instance != null) SaveManager.Instance.DeleteSlot(slot);
                    Refresh();
                }));
        }

        void DoNewGame(int slot, bool overwrite)
        {
            // 名字先用預設（正式建名輸入框之後再補）。覆蓋與否，StartNewGameInSlot 都會處理。
            string defaultName = $"存檔{slot + 1}";
            if (GameFlowManager.Instance != null) GameFlowManager.Instance.StartNewGame(slot, defaultName);
        }

        Button AddButton(Transform card, string label, Vector2 anchoredPos, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIBuilder.Button(card, label, label, onClick, bg);
            b.targetGraphic = b.GetComponent<Image>();   // 程式建鈕需手動指（見 PROBLEMS D4）
            UIBuilder.Anchor((RectTransform)b.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                anchoredPos, new Vector2(CardW - 80f, 72f));
            return b;
        }

        static string PlayedText(string iso)
        {
            if (!string.IsNullOrEmpty(iso) &&
                DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            return "—";
        }
    }
}
