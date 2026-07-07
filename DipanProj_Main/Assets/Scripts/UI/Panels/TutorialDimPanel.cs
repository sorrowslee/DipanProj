using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 教學用黑幕（覆蓋層、不擋輸入、不暫停）。兩種：
    ///   ShowFull()      ：整個螢幕壓黑（突顯畫面上方的教學文字）。
    ///   ShowSpotlight() ：黑幕中央留一個矩形洞（突顯畫面中央的目標，如鏡頭已對準的傳送門）。
    /// 中央洞用「上下左右四塊實心黑」框出來（實心 Image 一定畫得出來；程序生成的圓洞貼圖在某些環境不顯示）。
    /// 黑幕畫在遊戲畫面之上、HUD/提示文字/手指之下。
    /// </summary>
    public class TutorialDimPanel : UIPanel
    {
        public override UILayer Layer => UILayer.HUD;
        public override bool BlocksGameplayInput => false;
        public override bool PausesGame => false;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;
        public override float FadeDuration => 0f;

        // 壓黑用的統一色調與 alpha（全黑與圓洞黑幕共用；改這裡一次兩處都變）。
        static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
        // 中央洞占螢幕的半寬/半高比例（0.13 → 洞寬 26% 螢幕；0.22 → 洞高 44% 螢幕）。
        const float HoleHalfX = 0.14f, HoleHalfY = 0.24f;

        Image _full;                 // ShowFull 用：整片黑
        Image _top, _bottom, _left, _right;  // ShowSpotlight 用：四塊框出中央洞

        protected override void OnBuild()
        {
            var holder = UIBuilder.Create("DimHolder", transform);
            UIBuilder.Stretch(UIBuilder.Rect(holder));
            var cv = holder.AddComponent<Canvas>();
            cv.overrideSorting = true; cv.sortingOrder = -5;   // 遊戲之上、HUD/文字/手指之下

            _full = Solid(holder.transform, "Full");
            UIBuilder.Stretch(_full.rectTransform);

            _top = Solid(holder.transform, "Top");
            _bottom = Solid(holder.transform, "Bottom");
            _left = Solid(holder.transform, "Left");
            _right = Solid(holder.transform, "Right");
            ShowNone();
        }

        static Image Solid(Transform parent, string name)
        {
            var img = UIBuilder.Image(parent, name, null, DimColor);
            img.raycastTarget = false;
            return img;
        }

        void ShowNone()
        {
            _full.enabled = false;
            _top.enabled = _bottom.enabled = _left.enabled = _right.enabled = false;
        }

        /// <summary>整個螢幕壓黑。</summary>
        public void ShowFull()
        {
            ShowNone();
            _full.enabled = true;
        }

        /// <summary>黑幕中央留一個矩形洞（框住畫面中央的目標）。</summary>
        public void ShowSpotlight()
        {
            ShowNone();
            _top.enabled = _bottom.enabled = _left.enabled = _right.enabled = true;
            float x0 = 0.5f - HoleHalfX, x1 = 0.5f + HoleHalfX;
            float y0 = 0.5f - HoleHalfY, y1 = 0.5f + HoleHalfY;
            SetAnchors(_top, 0f, y1, 1f, 1f);        // 洞上方整條
            SetAnchors(_bottom, 0f, 0f, 1f, y0);     // 洞下方整條
            SetAnchors(_left, 0f, y0, x0, y1);       // 洞左側（介於上下條之間）
            SetAnchors(_right, x1, y0, 1f, y1);      // 洞右側
        }

        static void SetAnchors(Image img, float ax, float ay, float bx, float by)
        {
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(ax, ay);
            rt.anchorMax = new Vector2(bx, by);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static void ShowFullBlack() { var p = UIManager.Instance?.Open<TutorialDimPanel>(); p?.ShowFull(); }
        public static void ShowSpotlightCenter() { var p = UIManager.Instance?.Open<TutorialDimPanel>(); p?.ShowSpotlight(); }
        public static void Hide() => UIManager.Instance?.Close<TutorialDimPanel>();
    }
}
