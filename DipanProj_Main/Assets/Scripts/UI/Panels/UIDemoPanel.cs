using UnityEngine;

namespace Dipan.UI
{
    /// <summary>
    /// 範例/測試面板——用來驗證 UI 底層（開關、ESC、暫停、遮罩、淡入淡出）。
    /// 也是「如何用 UIBuilder 寫一個面板」的最小範例。背包做好後可刪。
    /// </summary>
    public class UIDemoPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;          // 開著時暫停遊戲（驗證暫停）
        public override bool BlocksGameplayInput => true;  // 開著時擋住玩家輸入（驗證輸入閘門）
        public override bool ShowBackdrop => true;         // 背後鋪遮罩（驗證遮罩）

        protected override void OnBuild()
        {
            // 置中的視窗主體
            var box = UIBuilder.SolidPanel(transform, "Box", new Color(0.10f, 0.10f, 0.13f, 0.97f));
            UIBuilder.Center(box.rectTransform, 640, 420);

            var title = UIBuilder.Text(box.transform, "Title", "UI 系統測試面板", 40, Color.white, TextAnchor.MiddleCenter);
            UIBuilder.Anchor(title.rectTransform,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(0, -40), new Vector2(0, 60));

            var hint = UIBuilder.Text(box.transform, "Hint",
                "這是底層自我測試：\n• 開啟時遊戲已暫停、玩家輸入被擋\n• 按 ESC 或下方按鈕關閉\n• 背後有半透明遮罩",
                24, new Color(0.85f, 0.85f, 0.9f), TextAnchor.UpperCenter);
            UIBuilder.Anchor(hint.rectTransform,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
                new Vector2(0, -10), new Vector2(-60, -160));

            var close = UIBuilder.Button(box.transform, "CloseBtn", "關閉",
                () => UIManager.Instance.Close(this));
            UIBuilder.Anchor(close.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 40), new Vector2(200, 60));
        }
    }
}
