using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 新手教學提示條：畫面上方一行字，常駐顯示當前該做什麼。HUD 層、不暫停、不擋輸入、不入堆疊。
    /// 由 TutorialManager 設定文字與顯示/隱藏。全程式建構、零接線。
    /// </summary>
    public class TutorialHintPanel : UIPanel
    {
        public override UILayer Layer => UILayer.HUD;
        public override bool BlocksGameplayInput => false;
        public override bool PausesGame => false;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;

        Text _text;
        Image _bg;

        protected override void OnBuild()
        {
            _bg = UIBuilder.Image(transform, "HintBG", null, new Color(0f, 0f, 0f, 0.62f));
            UIBuilder.Anchor(_bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                             new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1200f, 110f));
            _bg.raycastTarget = false;
            // 高 sortingOrder：確保提示字畫在背包/傳送門面板之上，不被蓋住截斷（比面板層 100~300 高、比手指 500 低）。
            var cv = _bg.gameObject.AddComponent<Canvas>();
            cv.overrideSorting = true; cv.sortingOrder = 460;

            _text = UIBuilder.Text(_bg.transform, "HintText", "", 60, new Color(1f, 0.92f, 0.6f), TextAnchor.MiddleCenter);  // 放大約 2 倍
            UIBuilder.Stretch(_text.rectTransform, 20, 20, 6, 6);
            _text.fontStyle = FontStyle.Bold;
        }

        public void SetText(string s) { if (_text != null) _text.text = s; }

        /// <summary>顯示一行教學提示（沒開就開、已開就換字）。</summary>
        public static void Show(string s)
        {
            var ui = UIManager.Instance;
            if (ui == null) return;
            var p = ui.Open<TutorialHintPanel>();
            p?.SetText(s);
        }

        /// <summary>收起教學提示。</summary>
        public static void Hide() => UIManager.Instance?.Close<TutorialHintPanel>();
    }
}
