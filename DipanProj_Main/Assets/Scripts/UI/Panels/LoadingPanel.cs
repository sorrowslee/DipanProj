using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 全螢幕載入頁（Overlay 層）。換地圖時蓋住整個畫面、顯示該關卡的載入圖 + 底部進度條，載完淡出。
    /// - Overlay 層：壓在所有 UI 之上；擋玩家輸入；不暫停遊戲（分幀載入靠協程逐幀推進）。
    ///
    /// 進度條＝美術素材（Resources/UI/LoadingBarPanel/）：底框(蓮花吊飾+深色軌道) + 金色填充 + 金色端蓋，錨在畫面底部中央。
    /// ⚠️ 兩張素材沒畫在對齊位置：底框軌道 y[52,120]（中心86、高68）、金色填充 y[78,180]（中心129、高102）。
    ///    所以填充要「垂直壓成軌道高 + 頂端對齊」才會落進軌道（見下方常數推導）。
    /// 進度用 **遮罩(RectMask2D)** 裁切金色填充：可見右緣＝遮罩寬度，端蓋放在同一個寬度上 → 端蓋一定黏在金條尾巴（不會飄）。
    /// 上方「載入中…XX%」用預設字型 + 燙金漸層(UIVerticalGradient) + 暗描邊。版面全走常數。
    ///
    /// 用法（MapManager）：Open&lt;LoadingPanel&gt;() → SetModule(module) → SetProgress(0..1) → Close&lt;LoadingPanel&gt;()。
    /// 全程式建構、零 prefab。見 readme/UI_SYSTEM.md、readme/RESOURCE_LOADING.md。
    /// </summary>
    public class LoadingPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Overlay;
        public override bool PausesGame => false;
        public override bool BlocksGameplayInput => true;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;
        public override bool ShowBackdrop => false;
        public override bool KeepOpenOnSceneChange => false;
        public override float FadeDuration => 0.25f;

        // ── 版面常數（螢幕 px，1920×1080 參考解析度；要移位/縮放改這裡）──
        const float BarWidth         = 1000f;        // 進度條寬
        const float BarAspect        = 250f / 1000f; // 素材原生比例（高/寬）→ 高 = 寬 × 0.25
        const float BarBottomMargin  = 20f;          // 進度條底（含吊穗）離畫面底的距離（越大越往上）
        const float EndCapHeightFrac = 0.42f;        // 端蓋高 = 進度條高 × 此值（素材為正方形）
        const float PercentGap       = -20f;         // 百分比文字底緣相對「底框頂端」的位移（越大越高、負值壓進框頂上緣）
        const int   PercentFontSize  = 52;

        // ── 素材量測值（art 空間 1000×250，y 由上往下；換素材才需要動）──
        const float GoldTopFrac = 78f  / 250f, GoldBotFrac = 180f / 250f;  // 金色填充在其素材裡的垂直範圍
        const float ChanTopFrac = 52f  / 250f, ChanBotFrac = 120f / 250f;  // 底框深色軌道的垂直範圍
        const float TrackXL = 0.051f, TrackXR = 0.946f;                    // 軌道水平範圍（= 金色寬 51..946 / 1000）
        const float FrameTopFrac = 18f / 250f;                             // 底框最頂（蓮花尖）距素材頂端的比例，給文字定位

        const string ResDir = "UI/LoadingBarPanel/";

        Image _bg;                 // 全螢幕載入圖
        RectTransform _goldClipRt; // 金色填充的遮罩（寬度＝進度）
        Image _endCap;             // 金色端蓋（隨進度移動）
        RectTransform _endCapRt;
        Text  _percent;

        float _barW, _barH, _fillH, _trackCenterFromTop;

        protected override void OnBuild()
        {
            var black = UIBuilder.SolidPanel(transform, "Black", Color.black);
            black.raycastTarget = true;

            _bg = UIBuilder.Image(transform, "LoadingImage", null, Color.white);
            UIBuilder.Stretch(_bg.rectTransform);
            _bg.raycastTarget = false;
            _bg.enabled = false;

            _barW = BarWidth;
            _barH = BarWidth * BarAspect;
            _fillH = _barH * (ChanBotFrac - ChanTopFrac) / (GoldBotFrac - GoldTopFrac);   // 金色壓成軌道高（≈0.667×）
            _trackCenterFromTop = _barH * (ChanTopFrac + ChanBotFrac) * 0.5f;             // 軌道中心離進度條頂端

            // 進度條容器：底部中央（吊穗底離畫面底 BarBottomMargin）。
            var bar = UIBuilder.Create("Bar", transform);
            UIBuilder.Anchor(UIBuilder.Rect(bar), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                             new Vector2(0.5f, 0f), new Vector2(0f, BarBottomMargin), new Vector2(_barW, _barH));

            // 底框（蓮花吊飾 + 深色軌道），鋪滿容器。
            var frame = UIBuilder.Image(bar.transform, "Frame",
                            UIBuilder.LoadSprite(ResDir + "LoadingBarPanel_ProgressBarBg"), Color.white);
            UIBuilder.Stretch(frame.rectTransform);
            frame.raycastTarget = false;

            // 金色填充＝「遮罩(GoldClip) + 完整金色(GoldFull)」。遮罩寬度＝進度，右緣裁掉多的金色。
            var clip = UIBuilder.Create("GoldClip", bar.transform);
            _goldClipRt = UIBuilder.Rect(clip);
            _goldClipRt.anchorMin = _goldClipRt.anchorMax = new Vector2(0f, 1f);   // 容器左上
            _goldClipRt.pivot = new Vector2(0f, 1f);
            _goldClipRt.anchoredPosition = Vector2.zero;
            _goldClipRt.sizeDelta = new Vector2(TrackXL * _barW, _fillH);
            clip.AddComponent<RectMask2D>();

            var gold = UIBuilder.Image(clip.transform, "GoldFull",
                           UIBuilder.LoadSprite(ResDir + "LoadingBarPanel_ProgressBar"), Color.white);
            var gr = gold.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0f, 1f); gr.pivot = new Vector2(0f, 1f);
            gr.anchoredPosition = Vector2.zero;
            gr.sizeDelta = new Vector2(_barW, _fillH);   // 完整金色（全寬、壓成軌道高、頂端貼齊）；超出遮罩的部分被裁掉
            gold.raycastTarget = false;

            // 金色端蓋（隨進度移動）：錨在容器左上、置中對到軌道中線、放在最上層。
            float endH = _barH * EndCapHeightFrac;
            _endCap = UIBuilder.Image(bar.transform, "EndCap",
                          UIBuilder.LoadSprite(ResDir + "LoadingBarPanel_ProgressEnd"), Color.white);
            _endCapRt = _endCap.rectTransform;
            _endCapRt.anchorMin = _endCapRt.anchorMax = new Vector2(0f, 1f);
            _endCapRt.pivot = new Vector2(0.5f, 0.5f);
            _endCapRt.sizeDelta = new Vector2(endH, endH);   // 素材 500×500 正方形
            _endCap.raycastTarget = false;

            // 百分比文字（預設字型 + 燙金漸層 + 暗描邊），貼在底框正上方一點點。
            _percent = UIBuilder.Text(transform, "Percent", "", PercentFontSize, Color.white, TextAnchor.LowerCenter);
            var grad = _percent.gameObject.AddComponent<UIVerticalGradient>();
            grad.top    = new Color(0.98f, 0.90f, 0.58f, 1f);   // 上：亮金
            grad.bottom = new Color(0.77f, 0.55f, 0.17f, 1f);   // 下：深琥珀
            var outline = _percent.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.06f, 0.02f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            float frameTopFromBottom = BarBottomMargin + _barH * (1f - FrameTopFrac);
            UIBuilder.Anchor(_percent.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                             new Vector2(0.5f, 0f), new Vector2(0f, frameTopFromBottom + PercentGap),
                             new Vector2(800f, PercentFontSize + 16f));
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

        /// <summary>更新進度（0~1）：遮罩寬度＝進度、端蓋黏在同一寬度、刷新百分比文字。</summary>
        public void SetProgress(float t)
        {
            t = Mathf.Clamp01(t);
            float frac = TrackXL + t * (TrackXR - TrackXL);   // 素材空間裡的填充比例（遮罩右緣）
            float edgeX = frac * _barW;                       // 金條可見右緣（＝端蓋位置），兩者同一來源
            if (_goldClipRt != null) _goldClipRt.sizeDelta = new Vector2(edgeX, _fillH);
            if (_endCapRt != null)
            {
                _endCapRt.anchoredPosition = new Vector2(edgeX, -_trackCenterFromTop);
                if (_endCap != null) _endCap.enabled = t > 0.001f;   // 0% 時不露端蓋
            }
            if (_percent != null) _percent.text = $"載入中…{Mathf.RoundToInt(t * 100f)}%";
        }

        protected override void OnOpen() => SetProgress(0f);
    }
}
