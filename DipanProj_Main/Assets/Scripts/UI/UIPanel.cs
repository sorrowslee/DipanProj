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

        // ───────────── 防連點（給對話這類「一下就翻過去」的面板用）─────────────
        //
        // 問題：劇情對話時猛按左鍵／空白鍵，會一次跳掉好幾句，有時立繪都還沒顯示出來就被跳過。
        // 作法：把「前進一次」節流成每 InputCooldown 秒最多一次；面板剛開啟時也先擋一次冷卻，
        //       確保玩家至少看得到第一句與立繪。
        //
        // ⚠ 這是 opt-in 的工具：**基底不會自動套用**，只有主動呼叫 TryConsumeInput 的面板才有節流
        //   （否則背包、設定這種需要連續操作的面板會變得很鈍）。
        // ⚠ 一律用 unscaledTime——對話面板 PausesGame=true，Time.time 在暫停時不會前進。

        /// <summary>防連點的預設冷卻秒數。</summary>
        public const float InputCooldown = 0.5f;

        float _inputReadyAt;

        /// <summary>接下來 seconds 秒內的 <see cref="TryConsumeInput"/> 一律當作沒按。開啟面板／剛換頁時呼叫。</summary>
        protected void BlockInputFor(float seconds) => _inputReadyAt = Time.unscaledTime + seconds;

        /// <summary>
        /// 嘗試吃掉一次「前進 / 關閉」輸入：還在冷卻內回 false（這次連點忽略），
        /// 否則回 true 並重新起算冷卻。呼叫端在回 false 時直接 return 即可。
        /// </summary>
        protected bool TryConsumeInput(float cooldown = InputCooldown)
        {
            if (Time.unscaledTime < _inputReadyAt) return false;
            _inputReadyAt = Time.unscaledTime + cooldown;
            return true;
        }

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
