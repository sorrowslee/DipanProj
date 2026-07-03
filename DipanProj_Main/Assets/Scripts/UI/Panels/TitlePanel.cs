using UnityEngine;
using UnityEngine.UI;
using Dipan.Flow;

namespace Dipan.UI
{
    /// <summary>
    /// 標題畫面（Overlay 層、全螢幕）。目前＝標題文字 ＋「開始遊戲」鈕；按下開始 → 開三欄存讀檔畫面。
    /// **佔位視覺**（純色底 + 內建字型 + 純色鈕），之後換上正式標題圖與按鈕素材即可。
    /// 未來要加「設定 / 離開遊戲 / 製作團隊」等鈕就往這裡加。見 readme/TITLE_AND_SAVE_UI.md。
    /// </summary>
    public class TitlePanel : UIPanel
    {
        // Window 層（不是 Overlay）：UI Canvas 已在遊戲世界相機之上，整片不透明底就能蓋住背後場景；
        // 且放 Window 層，覆蓋/刪除的 ConfirmPopup（Popup 層）才會顯示在本面板之上。
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool CloseOnEscape => false;        // 標題不因 ESC 關閉
        public override bool ShowBackdrop => false;         // 自己就是整片不透明底

        protected override void OnBuild()
        {
            // 全螢幕底（佔位：深色）。之後換成標題背景圖：UIBuilder.Image(transform,"BG",UIBuilder.LoadSprite("UI/Title/Background"))
            var bg = UIBuilder.SolidPanel(transform, "BG", new Color(0.06f, 0.05f, 0.08f, 1f));
            bg.raycastTarget = true;

            // 標題文字（佔位）
            var title = UIBuilder.Text(transform, "Title", "燃燈計畫", 96,
                new Color(0.90f, 0.20f, 0.20f), TextAnchor.MiddleCenter);
            UIBuilder.Anchor(title.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 180f), new Vector2(1200f, 200f));

            var sub = UIBuilder.Text(transform, "Sub", "Project Dipankara", 34,
                new Color(0.75f, 0.72f, 0.68f), TextAnchor.MiddleCenter);
            UIBuilder.Anchor(sub.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 90f), new Vector2(1000f, 80f));

            // 開始遊戲鈕（佔位）
            var start = UIBuilder.Button(transform, "StartButton", "開 始 遊 戲", OnStart,
                new Color(0.20f, 0.18f, 0.24f, 1f));
            start.targetGraphic = start.GetComponent<Image>();   // 程式建鈕需手動指（見 PROBLEMS D4）
            UIBuilder.Anchor((RectTransform)start.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -80f), new Vector2(420f, 96f));
        }

        void OnStart()
        {
            if (GameFlowManager.Instance != null) GameFlowManager.Instance.OpenSlotSelect();
            else UIManager.Instance.Open<SaveSlotPanel>();
        }
    }
}
