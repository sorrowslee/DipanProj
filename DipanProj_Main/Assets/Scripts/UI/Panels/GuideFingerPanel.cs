using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 新手教學的指引手指（覆蓋層、不擋輸入、不暫停）。兩種用法：
    ///   PointAtUI(target)：指尖貼著某個 UI 元件（劇本格/傳送按鈕），手往右下延伸、往目標輕點。
    ///   PointUp()：指向畫面上方（找邪佛用），放在上方中央、上下跳動。
    /// 手指圖：Resources/UI/Common/Guide_Finger.png（去背、斜的、指尖在左上）。座標為量測值，實機可微調常數。
    /// </summary>
    public class GuideFingerPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Overlay;
        public override bool BlocksGameplayInput => false;
        public override bool PausesGame => false;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;

        const string Res = "UI/Common/Guide_Finger";
        const float FingerHeight = 130f;   // 手指顯示高度
        const float BounceAmp = 14f;        // 輕點幅度
        const float BounceSpeed = 6f;

        RectTransform _rt;      // 手指本體
        RectTransform _self;    // 面板（整層）
        RectTransform _uiTarget;
        bool _upMode;

        protected override void OnBuild()
        {
            _self = (RectTransform)transform;
            var sp = Resources.Load<Sprite>(Res);
            var img = UIBuilder.Image(transform, "Finger", sp, Color.white);
            img.preserveAspect = true; img.raycastTarget = false;
            _rt = img.rectTransform;
            _rt.pivot = new Vector2(0f, 1f);   // 指尖在左上 → 用左上當定位點
            float w = FingerHeight * (sp != null && sp.rect.height > 0 ? sp.rect.width / sp.rect.height : 0.7f);
            _rt.sizeDelta = new Vector2(w, FingerHeight);

            // 自己一張最高層 Canvas，確保永遠畫在教學遮罩與面板之上。**不加 GraphicRaycaster**，
            // 手指完全不吃點擊（配合 raycastTarget=false），才不會擋到底下要點的元件。
            var cv = img.gameObject.AddComponent<Canvas>();
            cv.overrideSorting = true; cv.sortingOrder = 500;
        }

        /// <summary>指尖貼著某個 UI 元件（會每幀跟著它）。</summary>
        public void PointAtUI(RectTransform target)
        {
            _uiTarget = target; _upMode = false;
            if (_rt != null) _rt.localRotation = Quaternion.identity;
            UIManager.Instance?.Open<GuideFingerPanel>();
        }

        /// <summary>指向畫面上方（找邪佛）。</summary>
        public void PointUp()
        {
            _uiTarget = null; _upMode = true;
            if (_rt != null) _rt.localRotation = Quaternion.Euler(0f, 0f, -45f);   // 斜的指尖轉成朝上
            UIManager.Instance?.Open<GuideFingerPanel>();
        }

        public static void ShowAtUI(RectTransform target) => UIManager.Instance?.Open<GuideFingerPanel>()?.PointAtUI(target);
        public static void ShowUp() => UIManager.Instance?.Open<GuideFingerPanel>()?.PointUp();
        public static void HidePanel() => UIManager.Instance?.Close<GuideFingerPanel>();

        void Update()
        {
            if (_rt == null || !IsOpen) return;
            float bob = Mathf.Sin(Time.unscaledTime * BounceSpeed) * BounceAmp;

            if (_upMode)
            {
                // 上方中央，上下跳動。
                _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 1f);
                _rt.anchoredPosition = new Vector2(0f, -90f - Mathf.Abs(bob));
                return;
            }
            if (_uiTarget == null) return;

            // 追 UI 目標：把指尖放在目標的「右下角外側」，手往右下延伸——完全不蓋住目標，才不會擋點擊。
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, _uiTarget.position);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_self, screen, null, out Vector2 local))
            {
                _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
                // 指尖固定放在目標中心的「右下方一小段」(不隨目標大小放大)，靠近又不擋住可見度。
                Vector2 corner = local + new Vector2(34f, -34f);
                Vector2 toTarget = new Vector2(-0.707f, 0.707f) * Mathf.Abs(bob);     // 往左上（朝目標）輕點
                _rt.anchoredPosition = corner + toTarget;
            }
        }
    }
}
