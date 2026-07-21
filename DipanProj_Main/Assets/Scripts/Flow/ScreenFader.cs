using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Dipan.Flow
{
    /// <summary>
    /// 全螢幕黑幕淡入/淡出（跨場景常駐、用 unscaledTime 因為過場時遊戲可能暫停）。
    /// 自建 Canvas＋黑 Image，sortingOrder 極高（蓋在所有 UI 之上），平時透明不擋點擊。
    ///
    /// 用途：切場景時先蓋黑再切，避免看到「標題/選單淡出露餡」之類的破綻。
    /// 例：新建遊戲 → 蓋黑 → 關選單 → 載入 Intro → 淡出露出開場（見 GameFlowManager）。
    /// </summary>
    [DisallowMultipleComponent]
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        CanvasGroup _cg;
        Coroutine _fade;

        public static ScreenFader Ensure()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("[ScreenFader]");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;   // 高於所有 UI 分層（UIManager 用 i*100）

            var fader = go.AddComponent<ScreenFader>();

            var cgGO = new GameObject("Black", typeof(RectTransform));
            cgGO.transform.SetParent(go.transform, false);
            var rt = (RectTransform)cgGO.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var img = cgGO.AddComponent<Image>();
            img.color = Color.black;

            fader._cg = cgGO.AddComponent<CanvasGroup>();
            fader._cg.alpha = 0f;
            fader._cg.blocksRaycasts = false;
            fader._cg.interactable = false;

            Instance = fader;
            return fader;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>即時設定黑幕不透明度（0=透明、1=全黑），不做動畫。用於切場景前先壓黑、撐過場景載入避免亮閃。</summary>
        public void SetInstant(float target)
        {
            target = Mathf.Clamp01(target);
            if (_fade != null) { StopCoroutine(_fade); _fade = null; }
            if (_cg != null) { _cg.alpha = target; _cg.blocksRaycasts = target > 0.001f; }
        }

        /// <summary>啟動淡出到透明（自身跨場景常駐，協程不會因換場景中斷）。</summary>
        public void FadeOut(float duration) { StartCoroutine(FadeTo(0f, duration)); }

        /// <summary>
        /// 立刻壓全黑 → 撐 holdSeconds（讓切到的新場景/漫畫背景就位）→ 自動淡出 fadeSeconds。
        /// 全程跑在本元件（跨場景常駐）上，協程會延續到下一個場景，不依賴任何場景物件去清它。
        /// 用途：劇情尾段切到 Intro 墜落漫畫時，一路黑到漫畫出來、無亮閃、也不會卡在全黑。
        /// </summary>
        public void BlackThenFadeOut(float holdSeconds, float fadeSeconds)
        {
            if (_fade != null) { StopCoroutine(_fade); _fade = null; }
            if (_cg != null) { _cg.alpha = 1f; _cg.blocksRaycasts = true; }
            StartCoroutine(BlackThenFadeRoutine(Mathf.Max(0f, holdSeconds), Mathf.Max(0f, fadeSeconds)));
        }

        IEnumerator BlackThenFadeRoutine(float hold, float fade)
        {
            float t = 0f;
            while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }
            yield return FadeTo(0f, fade);
        }

        /// <summary>淡到指定不透明度（0=透明、1=全黑）。用 unscaledTime。</summary>
        public IEnumerator FadeTo(float target, float duration)
        {
            target = Mathf.Clamp01(target);
            if (_fade != null) StopCoroutine(_fade);
            _cg.blocksRaycasts = target > 0.001f;   // 蓋黑時擋住底下點擊
            _fade = StartCoroutine(FadeRoutine(target, Mathf.Max(0f, duration)));
            yield return _fade;
        }

        IEnumerator FadeRoutine(float target, float dur)
        {
            float start = _cg.alpha;
            if (dur <= 0f) { _cg.alpha = target; _cg.blocksRaycasts = target > 0.001f; _fade = null; yield break; }
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                _cg.alpha = Mathf.Lerp(start, target, t / dur);
                yield return null;
            }
            _cg.alpha = target;
            _cg.blocksRaycasts = target > 0.001f;
            _fade = null;
        }
    }
}
