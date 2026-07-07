using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 指引手指（覆蓋層、不擋輸入、不暫停）。**通用元件，任何系統都能用**（不限新手教學）：
    ///   PointAtUI(target)：指尖貼著某個 UI 元件（劇本格/傳送按鈕），往目標輕點。
    ///   PointToWorld(anchor, target)：手指出現在 anchor（通常是玩家）頭上，**指尖角度隨時朝向世界目標**，往目標方向前後擺。
    /// 手指圖：Resources/UI/Common/Guide_Finger.png（去背、斜的、未旋轉時指尖朝左上 135°）。
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
        const float BounceAmp = 16f;        // 往目標「輕點」幅度
        const float BounceSpeed = 6f;
        const float HeadWorldOffset = 1.2f; // 手指「根部」擺在玩家頭上多高（世界單位）
        const float TipBaseAngle = 135f;    // 手指圖未旋轉時指尖朝的角度（左上）；旋轉量 = 目標角度 - 此值
        const float HideNearDistance = 2.5f; // 太靠近目標就收起手指（避免角度亂飄）；離遠了自動再出現

        enum Mode { None, UiTarget, World }
        Mode _mode;
        RectTransform _rt, _self;
        Image _fingerImg;
        RectTransform _uiTarget;
        Transform _worldAnchor;
        Vector3 _worldTarget;
        Camera _cam;

        protected override void OnBuild()
        {
            _self = (RectTransform)transform;
            var sp = Resources.Load<Sprite>(Res);
            var img = UIBuilder.Image(transform, "Finger", sp, Color.white);
            img.preserveAspect = true; img.raycastTarget = false;
            _fingerImg = img;
            _rt = img.rectTransform;
            _rt.pivot = new Vector2(0f, 1f);   // 指尖在左上 → 用左上（指尖）當定位/旋轉點
            float w = FingerHeight * (sp != null && sp.rect.height > 0 ? sp.rect.width / sp.rect.height : 0.7f);
            _rt.sizeDelta = new Vector2(w, FingerHeight);

            var cv = img.gameObject.AddComponent<Canvas>();   // 自己一張最高層 Canvas，永遠畫在最上面；不加 Raycaster、不擋點擊
            cv.overrideSorting = true; cv.sortingOrder = 500;
        }

        /// <summary>指尖貼著某個 UI 元件（會每幀跟著它）。</summary>
        public void PointAtUI(RectTransform target)
        {
            _mode = Mode.UiTarget; _uiTarget = target;
            UIManager.Instance?.Open<GuideFingerPanel>();
        }

        /// <summary>手指出現在 anchor 頭上、指尖隨時朝向世界目標 target（通用：找路、指方向都用這個）。</summary>
        public void PointToWorld(Transform anchor, Vector3 target)
        {
            _mode = Mode.World; _worldAnchor = anchor; _worldTarget = target;
            UIManager.Instance?.Open<GuideFingerPanel>();
        }

        public static void ShowAtUI(RectTransform target) => UIManager.Instance?.Open<GuideFingerPanel>()?.PointAtUI(target);
        public static void ShowWorldGuide(Transform anchor, Vector3 target) => UIManager.Instance?.Open<GuideFingerPanel>()?.PointToWorld(anchor, target);
        public static void HidePanel() => UIManager.Instance?.Close<GuideFingerPanel>();

        void Update()
        {
            if (_rt == null || !IsOpen) return;
            float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * BounceSpeed)) * BounceAmp;

            if (_mode == Mode.UiTarget)
            {
                if (_fingerImg != null) _fingerImg.enabled = true;   // 世界模式可能把它關掉，UI 模式一律開回來
                if (_uiTarget == null) return;
                _rt.pivot = new Vector2(0f, 1f);   // 指 UI：用指尖(左上)定位
                _rt.localRotation = Quaternion.identity;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, _uiTarget.position);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_self, screen, null, out Vector2 local))
                {
                    _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
                    Vector2 corner = local + new Vector2(34f, -34f);                 // 目標中心右下方一小段
                    _rt.anchoredPosition = corner + new Vector2(-0.707f, 0.707f) * bob; // 往左上（朝目標）輕點
                }
                return;
            }

            if (_mode == Mode.World)
            {
                if (_worldAnchor == null) return;
                if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

                // 太靠近目標 → 收起手指（方向會亂飄）；離遠了再自動顯示。
                Vector3 dir = _worldTarget - _worldAnchor.position;
                bool near = dir.sqrMagnitude < HideNearDistance * HideNearDistance;
                if (_fingerImg != null) _fingerImg.enabled = !near;
                if (near) return;

                // 指尖角度：朝「anchor → 目標」的世界方向（俯視角，世界 XY 直接對應螢幕 XY）。
                float theta = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                // 用「手的根部(右下角 pivot)」對齊頭上點、繞根部旋轉 → 根部穩定不飄，只有指尖轉向目標。
                _rt.pivot = new Vector2(1f, 0f);
                _rt.localRotation = Quaternion.Euler(0f, 0f, theta - TipBaseAngle);

                Vector3 headWorld = _worldAnchor.position + Vector3.up * HeadWorldOffset;
                Vector2 screen = _cam.WorldToScreenPoint(headWorld);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_self, screen, null, out Vector2 local))
                {
                    _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
                    Vector2 tipDir = new Vector2(Mathf.Cos(theta * Mathf.Deg2Rad), Mathf.Sin(theta * Mathf.Deg2Rad));
                    _rt.anchoredPosition = local + tipDir * bob;   // 沿指向方向輕點（根部大致仍在頭上）
                }
            }
        }
    }
}
