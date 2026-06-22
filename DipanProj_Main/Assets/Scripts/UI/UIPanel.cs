using System.Collections;
using UnityEngine;

namespace Dipan.UI
{
    /// <summary>
    /// 所有 UI 面板的抽象基底。一個面板 = 掛在某層 Canvas 底下的一棵 UI 物件樹。
    ///
    /// 子類別要做兩件事：
    /// 1. 覆寫下方的「面板特性」屬性（屬於哪層、是否暫停遊戲、ESC 能不能關…）。
    /// 2. 實作 OnBuild()：用 UIBuilder 把版面拼出來（只會被呼叫一次）。
    ///
    /// 開關一律透過 UIManager.Open/Close，不要自己 SetActive。淡入淡出用 unscaledTime，
    /// 所以即使面板把遊戲暫停（Time.timeScale=0）動畫仍會播。
    /// 設計邊界：面板是「純呈現層」，不直接抓遊戲邏輯，資料透過事件/介面進出（見 readme/UI_SYSTEM.md）。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIPanel : MonoBehaviour
    {
        // ───────────── 面板特性（子類別覆寫）─────────────

        /// <summary>屬於哪一層 Canvas。預設主視窗層。</summary>
        public virtual UILayer Layer => UILayer.Window;

        /// <summary>開啟時是否暫停遊戲（Time.timeScale=0）。預設否。</summary>
        public virtual bool PausesGame => false;

        /// <summary>開啟時是否擋住遊戲輸入（玩家移動/射擊）。預設是。HUD 類請覆寫成 false。</summary>
        public virtual bool BlocksGameplayInput => true;

        /// <summary>按 ESC 是否關閉本面板（僅作用於堆疊最上層）。預設是。</summary>
        public virtual bool CloseOnEscape => true;

        /// <summary>開啟時是否在背後鋪一層半透明遮罩（擋住下方點擊、聚焦本視窗）。預設否。</summary>
        public virtual bool ShowBackdrop => false;

        /// <summary>是否納入視窗堆疊（影響 ESC 逐層關閉、遮罩、最上層判定）。預設：Window / Popup 才入堆疊。</summary>
        public virtual bool InStack => Layer == UILayer.Window || Layer == UILayer.Popup;

        /// <summary>切換 Unity 場景時是否保留開啟（預設關閉，避免殘留上一個場景的 UI）。</summary>
        public virtual bool KeepOpenOnSceneChange => false;

        /// <summary>淡入淡出時間（秒，unscaled）。0 = 立即。</summary>
        public virtual float FadeDuration => 0.12f;

        // ───────────── 狀態 ─────────────

        public bool IsOpen { get; private set; }
        protected CanvasGroup CanvasGroup { get; private set; }
        protected RectTransform Rect { get; private set; }

        Coroutine _fade;

        // ───────────── 由 UIManager 呼叫的內部生命週期 ─────────────

        /// <summary>建立後初始化一次：取 CanvasGroup、預設拉伸貼齊整層、呼叫子類別 OnBuild、收起來。</summary>
        internal void Init()
        {
            CanvasGroup = GetComponent<CanvasGroup>();
            Rect = (RectTransform)transform;
            UIBuilder.Stretch(Rect);   // 預設整層大小；子類別可在 OnBuild 裡對子物件重新錨定
            OnBuild();
            CanvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        internal void DoOpen()
        {
            if (IsOpen) return;
            IsOpen = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            CanvasGroup.alpha = 0f;
            OnOpen();
            StartFade(1f, deactivateAtEnd: false);
        }

        internal void DoClose()
        {
            if (!IsOpen) return;
            IsOpen = false;
            OnClose();
            StartFade(0f, deactivateAtEnd: true);
        }

        // ───────────── 子類別覆寫的鉤子 ─────────────

        /// <summary>建立版面（只會被呼叫一次）。用 UIBuilder 把子物件拼出來。</summary>
        protected abstract void OnBuild();

        /// <summary>每次開啟時呼叫（適合刷新資料、訂閱事件）。</summary>
        protected virtual void OnOpen() { }

        /// <summary>每次關閉時呼叫（適合退訂事件）。</summary>
        protected virtual void OnClose() { }

        // ───────────── 淡入淡出（unscaledTime，暫停時仍動）─────────────

        void StartFade(float target, bool deactivateAtEnd)
        {
            if (_fade != null) StopCoroutine(_fade);
            if (!gameObject.activeSelf && target > 0f) gameObject.SetActive(true);
            if (!gameObject.activeInHierarchy)
            {
                // 物件未啟用無法跑協程：直接套用結果。
                CanvasGroup.alpha = target;
                if (deactivateAtEnd && target <= 0f) gameObject.SetActive(false);
                return;
            }
            _fade = StartCoroutine(FadeRoutine(target, deactivateAtEnd));
        }

        IEnumerator FadeRoutine(float target, bool deactivateAtEnd)
        {
            CanvasGroup.interactable = target > 0f;
            CanvasGroup.blocksRaycasts = target > 0f;

            float dur = Mathf.Max(0f, FadeDuration);
            if (dur > 0f)
            {
                float start = CanvasGroup.alpha;
                float t = 0f;
                while (t < dur)
                {
                    t += Time.unscaledDeltaTime;
                    CanvasGroup.alpha = Mathf.Lerp(start, target, t / dur);
                    yield return null;
                }
            }
            CanvasGroup.alpha = target;
            if (deactivateAtEnd && target <= 0f) gameObject.SetActive(false);
            _fade = null;
        }
    }
}
