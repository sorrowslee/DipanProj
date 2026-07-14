using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 過關延時期間，畫面上方的倒數提示「X 秒後即將進入結算」（HUD 層、不擋操作、不暫停）。
    /// 由 GameFlowManager 在「過關 → 卍字離場」之間的等待期間顯示，倒數歸零就關閉、接卍字。
    /// 全程式建構。
    /// </summary>
    public class ExitCountdownPanel : UIPanel
    {
        public override UILayer Layer => UILayer.HUD;
        public override bool BlocksGameplayInput => false;
        public override bool PausesGame => false;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;
        public override float FadeDuration => 0f;   // 每秒更新數字，不要淡入淡出

        Text _text;

        protected override void OnBuild()
        {
            _text = UIBuilder.Text(transform, "Countdown", "", 44,
                                   new Color(0.96f, 0.90f, 0.66f, 1f), TextAnchor.MiddleCenter);
            _text.fontStyle = FontStyle.Bold;
            _text.raycastTarget = false;
            var rt = _text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);   // 上方置中
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(1000f, 80f);
            rt.anchoredPosition = new Vector2(0f, -70f);

            var ol = _text.gameObject.AddComponent<Outline>();     // 描邊，亮/暗場景都讀得清楚
            ol.effectColor = new Color(0f, 0f, 0f, 0.75f);
            ol.effectDistance = new Vector2(2f, -2f);
        }

        /// <summary>設定倒數秒數文字。</summary>
        public void SetSeconds(int sec)
        {
            if (_text != null) _text.text = $"{Mathf.Max(0, sec)} 秒後即將進入結算";
        }
    }
}
