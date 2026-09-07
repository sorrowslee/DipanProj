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
        // 中央洞的大小，單位是**世界格數**（不是螢幕比例）。
        //
        // ★ 為什麼用格數而不用螢幕比例（2026-09-07 踩過）：鏡頭跟隨模式的 orthographicSize 固定
        //   ⇒ **畫面高永遠是 10 格**，但**畫面寬幾格取決於視窗寬高比**（16:9 是 17.8 格、4:3 只有 13.3 格）。
        //   舊版把半寬寫成「螢幕寬的比例」，同一個值在窄視窗框住的世界範圍就少 21%，
        //   目標物的左右會被切掉——作者在 1356×968（比例 1.40）的視窗上實際撞到：門寬 299px、洞只有 274px。
        //   所以高直接除以 ViewTiles，寬要再乘 (螢幕高/螢幕寬) 把視窗比例補回來。
        //
        // ★ 這兩個數字是「配著畫面上那個目標物有多大」調出來的，不是隨手填的：
        //   邪佛廣場的傳送門是 3.09×2.73 格，洞比它大 1.16×/1.27× ＝ 剛好一圈貼身留白。
        //   （原值 0.14/0.24 螢幕比例＝ 4.98×4.80 格，是配 90×50 時代的門 4.29×3.79 格調的；
        //     2026-09-07 廣場畫布改 48×36、地上物統一縮 0.72，門變小而洞沒跟著 ⇒ 洞相對大了 45%。
        //     現值＝原值 ×0.72，留白倍率與當初完全一致。）
        //
        // ⚠ **日後若某張圖的聚焦目標大小差很多，不要直接改這裡**——這是全域值；
        //   那時應該把大小做成 cameraFocus 的參數（例如 dim 填「中央留洞:3.6,3.5」），逐個 trigger 決定。
        const float HoleTilesW = 3.59f, HoleTilesH = 3.46f;
        // 畫面高幾格 ＝ MapCameraController.followViewHeightTiles（跟隨模式的縮放基準）。
        // ⚠ 那邊改了這裡要跟著改，否則洞的大小會跟世界對不上。
        const float ViewTiles = 10f;

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
            // 格數 → 螢幕比例。高：畫面高就是 ViewTiles 格，直接除。
            // 寬：畫面寬是 ViewTiles×aspect 格，所以除完還要再除 aspect（＝乘 高/寬）。
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            float halfY = HoleTilesH / ViewTiles * 0.5f;
            float halfX = HoleTilesW / ViewTiles * 0.5f / Mathf.Max(0.01f, aspect);
            float x0 = 0.5f - halfX, x1 = 0.5f + halfX;
            float y0 = 0.5f - halfY, y1 = 0.5f + halfY;
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
