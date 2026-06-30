using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 全螢幕載入頁（Overlay 層）。換地圖時蓋住整個畫面、顯示該關卡的載入圖 + 底部進度條，
    /// 載完淡出。特性：
    /// - **Overlay 層**：壓在所有 UI 之上，連 HUD 都蓋住。
    /// - **擋玩家輸入**（BlocksGameplayInput=true）：載入期間玩家不能動/開火（PlayerController 已遵守此旗標）。
    /// - **不暫停遊戲**（PausesGame=false）：分幀載入靠協程逐幀推進，暫停會讓協程的逐幀 yield 停擺。
    ///
    /// 用法（MapManager）：
    ///   var lp = UIManager.Instance.Open&lt;LoadingPanel&gt;();
    ///   lp.SetModule("RedBridalGown");   // 載 Resources/Loading/&lt;module&gt;.png；找不到 = 純黑
    ///   lp.SetProgress(0.5f);
    ///   UIManager.Instance.Close&lt;LoadingPanel&gt;();
    /// 全程式建構、零 prefab，圖走 Resources（同 UIBuilder 慣例）。見 readme/UI_SYSTEM.md。
    /// </summary>
    public class LoadingPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Overlay;   // 壓最上層，蓋住一切
        public override bool PausesGame => false;           // 分幀載入需要逐幀推進，不能暫停
        public override bool BlocksGameplayInput => true;   // 載入期間鎖玩家輸入
        public override bool CloseOnEscape => false;
        public override bool InStack => false;              // 不入 ESC 堆疊
        public override bool ShowBackdrop => false;         // 自己就是全螢幕不透明底
        public override bool KeepOpenOnSceneChange => false;
        public override float FadeDuration => 0.25f;

        Image _bg;               // 全螢幕載入圖（找不到時藏起、只露黑底）
        Image _barFill;          // 進度條填充
        RectTransform _barFillRect;
        Text _percent;           // 百分比文字

        const float BarWidth = 600f, BarHeight = 16f, BarBottom = 90f;

        protected override void OnBuild()
        {
            // 全螢幕黑底：保證未載到圖/破圖時也是黑幕，不會露出後面的場景。也擋住下方點擊。
            var black = UIBuilder.SolidPanel(transform, "Black", Color.black);
            black.raycastTarget = true;

            // 載入圖（鋪滿畫面）。16:9 圖鋪 16:9 參考解析度；非 16:9 螢幕由 CanvasScaler 處理邊緣。
            _bg = UIBuilder.Image(transform, "LoadingImage", null, Color.white);
            UIBuilder.Stretch(_bg.rectTransform);
            _bg.raycastTarget = false;
            _bg.enabled = false;   // 還沒設圖前先不顯示

            // 進度條（底 + 填充），錨在畫面底部中央。
            var bar = UIBuilder.Create("Bar", transform);
            var barRt = UIBuilder.Rect(bar);
            UIBuilder.Anchor(barRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                             new Vector2(0.5f, 0f), new Vector2(0f, BarBottom), new Vector2(BarWidth, BarHeight));
            var barBgImg = bar.AddComponent<Image>();
            barBgImg.color = new Color(1f, 1f, 1f, 0.18f);
            barBgImg.raycastTarget = false;

            var fillGo = UIBuilder.Create("Fill", bar.transform);
            _barFillRect = UIBuilder.Rect(fillGo);
            _barFillRect.anchorMin = new Vector2(0f, 0f);
            _barFillRect.anchorMax = new Vector2(0f, 1f);   // 縱向貼滿條高
            _barFillRect.pivot = new Vector2(0f, 0.5f);     // 從左邊往右長
            _barFillRect.anchoredPosition = Vector2.zero;
            _barFillRect.sizeDelta = new Vector2(0f, 0f);   // 寬度由進度決定（y=0 因縱向已靠錨點貼滿）
            _barFill = fillGo.AddComponent<Image>();
            _barFill.color = new Color(0.95f, 0.85f, 0.55f, 0.95f);
            _barFill.raycastTarget = false;

            _percent = UIBuilder.Text(transform, "Percent", "", 26, Color.white, TextAnchor.MiddleCenter);
            UIBuilder.Anchor(_percent.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                             new Vector2(0.5f, 0f), new Vector2(0f, BarBottom + BarHeight + 22f), new Vector2(360f, 32f));
        }

        /// <summary>設定要顯示哪一關的載入圖（Resources/Loading/&lt;module&gt;.png）。找不到 → 只露黑底。</summary>
        public void SetModule(string module)
        {
            Sprite sp = string.IsNullOrEmpty(module) ? null : Resources.Load<Sprite>($"Loading/{module}");
            if (sp == null && !string.IsNullOrEmpty(module))
                Debug.LogWarning($"[LoadingPanel] 找不到載入圖 Resources/Loading/{module}，改用純黑底。");
            if (_bg != null)
            {
                _bg.sprite = sp;
                _bg.enabled = sp != null;
            }
        }

        /// <summary>更新進度（0~1）：拉長進度條、刷新百分比文字。</summary>
        public void SetProgress(float t)
        {
            t = Mathf.Clamp01(t);
            if (_barFillRect != null) _barFillRect.sizeDelta = new Vector2(BarWidth * t, 0f);
            if (_percent != null) _percent.text = $"載入中… {Mathf.RoundToInt(t * 100f)}%";
        }

        protected override void OnOpen() => SetProgress(0f);
    }
}
