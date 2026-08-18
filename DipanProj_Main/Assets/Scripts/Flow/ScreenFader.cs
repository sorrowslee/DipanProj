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

        // ── 閃光層（與黑幕分開的獨立 Image + CanvasGroup）──
        // 刻意不共用黑幕那一張：閃光要能在黑幕淡出到一半時插進來，共用會互相打斷；
        // 而且閃光**永遠不擋點擊**（blocksRaycasts 恆 false），黑幕蓋著時是要擋的。
        Image _flashImg;
        CanvasGroup _flashCg;
        Coroutine _flashCo;

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

            // 閃光層疊在黑幕之上（後加的 child 畫在後面＝上層）。
            var flashGO = new GameObject("Flash", typeof(RectTransform));
            flashGO.transform.SetParent(go.transform, false);
            var frt = (RectTransform)flashGO.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;

            fader._flashImg = flashGO.AddComponent<Image>();
            fader._flashImg.color = Color.white;
            fader._flashImg.raycastTarget = false;

            fader._flashCg = flashGO.AddComponent<CanvasGroup>();
            fader._flashCg.alpha = 0f;
            fader._flashCg.blocksRaycasts = false;
            fader._flashCg.interactable = false;

            Instance = fader;
            return fader;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>
        /// 全螢幕閃一下（打雷擊中、爆炸這種瞬間衝擊）。與黑幕互不干擾，**永遠不擋點擊**。
        /// 用 unscaledTime，所以暫停中也會播完。重複呼叫會取代上一次。
        /// </summary>
        /// <param name="color">閃光顏色（alpha 是峰值不透明度，例：白色 0.85）。</param>
        /// <param name="inSeconds">衝到峰值的時間（越短越「閃」）。</param>
        /// <param name="outSeconds">退回透明的時間。</param>
        public static void Flash(Color color, float inSeconds = 0.05f, float outSeconds = 0.2f)
        {
            var f = Ensure();
            if (f == null || f._flashCg == null) return;
            if (f._flashCo != null) f.StopCoroutine(f._flashCo);
            f._flashImg.color = new Color(color.r, color.g, color.b, 1f);
            f._flashCo = f.StartCoroutine(f.FlashRoutine(Mathf.Clamp01(color.a),
                Mathf.Max(0f, inSeconds), Mathf.Max(0f, outSeconds)));
        }

        IEnumerator FlashRoutine(float peak, float inSec, float outSec)
        {
            float t = 0f;
            while (t < inSec)
            {
                t += Time.unscaledDeltaTime;
                _flashCg.alpha = Mathf.Lerp(0f, peak, inSec <= 0f ? 1f : t / inSec);
                yield return null;
            }
            _flashCg.alpha = peak;
            t = 0f;
            while (t < outSec)
            {
                t += Time.unscaledDeltaTime;
                _flashCg.alpha = Mathf.Lerp(peak, 0f, outSec <= 0f ? 1f : t / outSec);
                yield return null;
            }
            _flashCg.alpha = 0f;
            _flashCo = null;
        }

        /// <summary>立刻收掉閃光（演出被打斷時用，免得畫面停在半白）。</summary>
        public static void ClearFlash()
        {
            var f = Instance;
            if (f == null || f._flashCg == null) return;
            if (f._flashCo != null) { f.StopCoroutine(f._flashCo); f._flashCo = null; }
            f._flashCg.alpha = 0f;
        }

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
