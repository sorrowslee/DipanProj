using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 跟隨地上掉落物的拾取提示（「按 F 鍵拾取 XXX」）。由 LootManager 每幀驅動：
    /// 有最近且在範圍內的掉落物就 <see cref="ShowAt"/>（指定世界座標 + 文字），否則 <see cref="HideTip"/>。
    ///
    /// 特性：HUD 層、不暫停/不擋輸入/不遮罩/不入堆疊。面板本體常開（透明無內容時等於隱形），
    /// 只切換內含 content 的顯示，避免每次進出範圍都觸發淡入淡出閃爍。
    /// 螢幕定位：世界座標 → 螢幕點 → 本層 Canvas 區域內的 local 座標（ScreenSpaceOverlay 用 null camera）。
    /// </summary>
    public class PickupTipPanel : UIPanel
    {
        public override UILayer Layer => UILayer.HUD;
        public override bool PausesGame => false;
        public override bool BlocksGameplayInput => false;
        public override bool ShowBackdrop => false;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;
        public override bool KeepOpenOnSceneChange => true;   // HUD 常駐，換場景不關（避免快取引用失效）
        public override float FadeDuration => 0f;   // 本體常開、不淡

        RectTransform _content;   // 跟著掉落物移動的小提示框
        Text _text;

        /// <summary>取得（必要時開啟）面板實例。</summary>
        public static PickupTipPanel Ensure()
        {
            if (UIManager.Instance == null) return null;
            return UIManager.Instance.Open<PickupTipPanel>();
        }

        protected override void OnBuild()
        {
            var go = UIBuilder.Create("TipBox", transform);
            _content = UIBuilder.Rect(go);
            _content.anchorMin = _content.anchorMax = _content.pivot = new Vector2(0.5f, 0.5f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.72f);
            bg.raycastTarget = false;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(14, 14, 7, 7);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _text = UIBuilder.Text(go.transform, "Label", "", 22, new Color(1f, 0.95f, 0.7f, 1f), TextAnchor.MiddleCenter);
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var le = _text.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 22f;

            _content.gameObject.SetActive(false);
        }

        /// <summary>在某世界座標上方顯示提示文字。</summary>
        public void ShowAt(Vector3 worldPos, string text)
        {
            if (_content == null) return;
            var cam = Camera.main;
            if (cam == null) { HideTip(); return; }

            if (!_content.gameObject.activeSelf) _content.gameObject.SetActive(true);
            if (_text.text != text) _text.text = text;

            Vector3 screen = cam.WorldToScreenPoint(worldPos);
            if (screen.z < 0f) { HideTip(); return; }   // 在相機後方

            RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screen, null, out var local);
            _content.anchoredPosition = local;
        }

        /// <summary>隱藏提示。</summary>
        public void HideTip()
        {
            if (_content != null && _content.gameObject.activeSelf)
                _content.gameObject.SetActive(false);
        }
    }
}
