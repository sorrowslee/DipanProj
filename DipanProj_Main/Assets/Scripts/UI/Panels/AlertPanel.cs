using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 中央短訊息提示（toast）。專門用來顯示「拾取到 XXX」這類短暫訊息。
    ///
    /// 特性：HUD 層、**不暫停遊戲、不擋輸入、不鋪遮罩、不入 ESC 堆疊**——純表演層。
    /// 每則訊息撐約 <see cref="HoldSeconds"/> 秒後淡出自毀；短時間內多則往上疊（最新在最上）。
    ///
    /// 用法（任何系統）：<c>AlertPanel.Toast("獲得 銅錢 ×5");</c>
    /// 風格對齊專案：全程式建構、零 prefab/Inspector 接線。見 readme/UI_SYSTEM.md。
    /// </summary>
    public class AlertPanel : UIPanel
    {
        // ── HUD toast：不暫停、不擋輸入、不遮罩、ESC 不關、不入堆疊 ──
        public override UILayer Layer => UILayer.HUD;
        public override bool PausesGame => false;
        public override bool BlocksGameplayInput => false;
        public override bool ShowBackdrop => false;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;
        public override bool KeepOpenOnSceneChange => true;   // HUD 常駐，換場景不關（避免快取引用失效）

        const float HoldSeconds = 1.6f;   // 完全顯示的時間
        const float FadeSeconds = 0.4f;   // 之後淡出的時間（合計約 2 秒）

        RectTransform _stack;   // 垂直堆疊容器（置中）

        /// <summary>確保 AlertPanel 已開啟並推一則訊息。任何系統都可呼叫。</summary>
        public static void Toast(string message)
        {
            if (UIManager.Instance == null || string.IsNullOrEmpty(message)) return;
            var p = UIManager.Instance.Open<AlertPanel>();
            if (p != null) p.Push(message);
        }

        protected override void OnBuild()
        {
            // 置中、由下往上長的垂直容器；ContentSizeFitter 讓它隨訊息數量自動撐高。
            var go = UIBuilder.Create("ToastStack", transform);
            _stack = UIBuilder.Rect(go);
            _stack.anchorMin = _stack.anchorMax = _stack.pivot = new Vector2(0.5f, 0.5f);
            _stack.anchoredPosition = new Vector2(0f, 220f);   // 略高於正中央，不擋準星

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 8f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>推一則訊息（最新顯示在最上）。</summary>
        public void Push(string message)
        {
            if (_stack == null) return;

            // 一則 toast = 半透明深色底 + 文字，外加 HorizontalLayoutGroup+Fitter 自動貼合文字寬度。
            var toast = UIBuilder.Create("Toast", _stack);
            var bg = toast.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.72f);
            bg.raycastTarget = false;

            var hlg = toast.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(22, 22, 10, 10);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            // 不加 ContentSizeFitter：toast 由父層 _stack 的 VerticalLayoutGroup（childControlWidth/Height）
            // 依本 HLG 回報的 preferred 尺寸排版，避免 Fitter 與父 LayoutGroup 衝突。

            var cg = toast.AddComponent<CanvasGroup>();

            var t = UIBuilder.Text(toast.transform, "Msg", message, 28, Color.white, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;   // 不換行，靠 fitter 撐寬
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 28f;

            toast.transform.SetAsFirstSibling();   // 最新在最上
            StartCoroutine(LifeRoutine(toast, cg));
        }

        IEnumerator LifeRoutine(GameObject toast, CanvasGroup cg)
        {
            cg.alpha = 1f;
            float t = 0f;
            while (t < HoldSeconds) { t += Time.unscaledDeltaTime; yield return null; }

            t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                if (cg != null) cg.alpha = Mathf.Lerp(1f, 0f, t / FadeSeconds);
                yield return null;
            }
            if (toast != null) Destroy(toast);
        }
    }
}
