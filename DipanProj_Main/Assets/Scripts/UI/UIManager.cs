using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Dipan.UI
{
    /// <summary>
    /// UI 系統的大腦：跨場景常駐單例（DontDestroyOnLoad）。是「開關任何 UI 的唯一入口」。
    ///
    /// 多場景策略：本元件與其底下的分層 Canvas 由 UIBootstrap 在開場前自動建立一次，之後換 Unity 場景都不被銷毀，
    /// 所以主選單、遊戲、未來任何場景共用同一套 UI。切場景時自動關掉「非常駐」面板，避免殘留上一場景的視窗。
    ///
    /// 面板特性（暫停遊戲、擋輸入、ESC 關閉、遮罩）由各 UIPanel 自行宣告，本元件只負責統合執行。
    /// 風格對齊既有 Manager：static Instance、繁中註解、元件自掛、零手動接線（見 readme/UI_SYSTEM.md）。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("解析度基準（CanvasScaler）")]
        public Vector2 referenceResolution = new Vector2(1920, 1080);

        [Header("背景遮罩顏色")]
        public Color backdropColor = new Color(0f, 0f, 0f, 0.6f);

        Canvas[] _layerCanvas;                              // 依 (int)UILayer 索引
        Image _backdrop;                                   // 共用半透明遮罩（在 Window 層）
        readonly Dictionary<Type, UIPanel> _panels = new Dictionary<Type, UIPanel>();  // 已建立的面板（快取重用）
        readonly List<UIPanel> _stack = new List<UIPanel>();                            // 視窗/彈窗堆疊順序

        bool _inputBlocked;
        bool _extBlock, _extPause;   // 非面板系統（如進場睜眼過場）要求的擋輸入/暫停

        /// <summary>遊戲輸入是否該被 UI 擋住（給 PlayerController 等查詢）。無 UIManager 時恆為 false。</summary>
        public static bool IsGameplayInputBlocked => Instance != null && Instance._inputBlocked;

        /// <summary>
        /// 給非面板的系統掛「擋輸入 / 暫停」需求（例如進場睜眼過場：播放中暫停＋不能操作，播完解除）。
        /// 與面板的需求一起參與 Recompute（任一要求就生效），所以不會被載入頁關閉時的重算覆蓋掉。
        /// </summary>
        public void SetExternalHold(bool block, bool pause)
        {
            _extBlock = block;
            _extPause = pause;
            Recompute();
        }

        // 「沒有任何視窗開著時，按 ESC 要開的根面板」（例如設定）。由該面板的 launcher 註冊。
        Type _escapeRootPanel;

        /// <summary>設定「沒有視窗開著時按 ESC 要開哪個面板」（例如設定面板）。在同一個 ESC 分支裡處理，不會與「ESC 關閉最上層」打架。</summary>
        public void SetEscapeRootPanel<T>() where T : UIPanel => _escapeRootPanel = typeof(T);
        public void ClearEscapeRootPanel() => _escapeRootPanel = null;

        // ───────────────────────── 生命週期 ─────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            BuildLayers();
            EnsureEventSystem();
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.activeSceneChanged -= OnActiveSceneChanged;
                Instance = null;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                var top = TopStackPanel();
                if (top != null)
                {
                    // 有視窗開著 → 關閉最上層（若該面板允許 ESC 關閉）。
                    if (top.CloseOnEscape) Close(top);
                }
                else if (_escapeRootPanel != null)
                {
                    // 沒有任何視窗 → 開啟根面板（例如設定）。同一分支，不會關掉又重開。
                    Open(_escapeRootPanel);
                }
            }
        }

        // ───────────────────────── 對外 API ─────────────────────────

        /// <summary>開啟（或聚焦已開啟的）面板。回傳該面板實例。</summary>
        public T Open<T>() where T : UIPanel => (T)Open(typeof(T));

        public UIPanel Open(Type panelType)
        {
            var p = GetOrCreate(panelType);
            if (p == null) return null;

            if (p.IsOpen)
            {
                Focus(p);
                return p;
            }

            p.transform.SetParent(_layerCanvas[(int)p.Layer].transform, false);
            p.DoOpen();
            if (p.InStack && !_stack.Contains(p)) _stack.Add(p);

            UpdateBackdrop();
            Recompute();
            return p;
        }

        /// <summary>關閉指定面板。</summary>
        public void Close(UIPanel p)
        {
            if (p == null || !p.IsOpen) return;
            p.DoClose();
            _stack.Remove(p);
            UpdateBackdrop();
            Recompute();
        }

        public void Close<T>() where T : UIPanel => Close(Get<T>());

        /// <summary>關閉堆疊最上層（ESC 的程式版）。</summary>
        public void CloseTop()
        {
            var top = TopStackPanel();
            if (top != null) Close(top);
        }

        /// <summary>切換：開著就關、關著就開。</summary>
        public T Toggle<T>() where T : UIPanel
        {
            if (IsOpen<T>()) { Close<T>(); return null; }
            return Open<T>();
        }

        public bool IsOpen<T>() where T : UIPanel
        {
            var p = Get<T>();
            return p != null && p.IsOpen;
        }

        /// <summary>取得已建立的面板實例（沒建立過回 null，不會新建）。</summary>
        public T Get<T>() where T : UIPanel
        {
            return _panels.TryGetValue(typeof(T), out var p) ? (T)p : null;
        }

        /// <summary>關閉所有開啟中的面板。</summary>
        public void CloseAll()
        {
            // 複製一份避免列舉時修改集合
            var open = new List<UIPanel>();
            foreach (var p in _panels.Values)
                if (p != null && p.IsOpen) open.Add(p);
            for (int i = 0; i < open.Count; i++) Close(open[i]);
        }

        // ───────────────────────── 內部 ─────────────────────────

        UIPanel GetOrCreate(Type panelType)
        {
            if (_panels.TryGetValue(panelType, out var existing) && existing != null)
                return existing;

            if (!typeof(UIPanel).IsAssignableFrom(panelType))
            {
                Debug.LogError($"[UIManager] {panelType.Name} 不是 UIPanel。");
                return null;
            }

            var go = new GameObject(panelType.Name, typeof(RectTransform));
            // 先暫掛到 Window 層，等取得實例後再依其 Layer 重新掛。
            go.transform.SetParent(_layerCanvas[(int)UILayer.Window].transform, false);
            var p = (UIPanel)go.AddComponent(panelType);   // RequireComponent 會自動補上 CanvasGroup
            go.transform.SetParent(_layerCanvas[(int)p.Layer].transform, false);
            p.Init();
            _panels[panelType] = p;
            return p;
        }

        /// <summary>把面板移到堆疊最上層並置頂顯示。</summary>
        void Focus(UIPanel p)
        {
            if (p.InStack)
            {
                _stack.Remove(p);
                _stack.Add(p);
            }
            p.transform.SetAsLastSibling();
            UpdateBackdrop();
            Recompute();
        }

        /// <summary>重算「是否擋輸入」與「是否暫停」：任一開啟面板要求就生效。</summary>
        void Recompute()
        {
            bool block = false, pause = false;
            foreach (var p in _panels.Values)
            {
                if (p == null || !p.IsOpen) continue;
                if (p.BlocksGameplayInput) block = true;
                if (p.PausesGame) pause = true;
            }
            _inputBlocked = block || _extBlock;
            Time.timeScale = (pause || _extPause) ? 0f : 1f;
        }

        /// <summary>
        /// 共用遮罩：只要有任一「Window 層且要求遮罩」的面板開著就鋪一層，**放在所有視窗的最底層**。
        /// 因此不論同時開幾個視窗都只有「一層、在全部視窗後面」——不會卡在兩個並排視窗之間蓋住下面那個，
        /// 也不會疊加（全程只有這一張 _backdrop）。
        /// </summary>
        void UpdateBackdrop()
        {
            if (_backdrop == null) return;

            bool any = false;
            foreach (var p in _panels.Values)
                if (p != null && p.IsOpen && p.ShowBackdrop && p.Layer == UILayer.Window) { any = true; break; }

            if (!any)
            {
                _backdrop.gameObject.SetActive(false);
                return;
            }

            _backdrop.transform.SetParent(_layerCanvas[(int)UILayer.Window].transform, false);
            _backdrop.gameObject.SetActive(true);
            _backdrop.transform.SetAsFirstSibling();   // 永遠在所有視窗最底層 → 一層、不蓋任何視窗
        }

        UIPanel TopStackPanel()
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
                if (_stack[i] != null && _stack[i].IsOpen) return _stack[i];
            return null;
        }

        // ───────────────────────── 建構：分層 Canvas / EventSystem / 遮罩 ─────────────────────────

        void BuildLayers()
        {
            int n = Enum.GetValues(typeof(UILayer)).Length;
            _layerCanvas = new Canvas[n];

            for (int i = 0; i < n; i++)
            {
                var go = new GameObject($"Layer_{(UILayer)i}", typeof(RectTransform));
                go.transform.SetParent(transform, false);

                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = i * 100;   // 每層之間留 100 的空檔

                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = referenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                go.AddComponent<GraphicRaycaster>();
                _layerCanvas[i] = canvas;
            }

            // 共用遮罩放在 Window 層，預設關閉。
            _backdrop = UIBuilder.SolidPanel(_layerCanvas[(int)UILayer.Window].transform, "Backdrop", backdropColor);
            _backdrop.raycastTarget = true;   // 擋住下方點擊
            _backdrop.gameObject.SetActive(false);
        }

        void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();   // 對應專案使用的舊版 Input system
            DontDestroyOnLoad(go);
        }

        void OnActiveSceneChanged(Scene previous, Scene next)
        {
            // 切場景時關掉非常駐面板，避免殘留上一個場景的 UI（實例仍快取、之後可重用）。
            var toClose = new List<UIPanel>();
            foreach (var p in _panels.Values)
                if (p != null && p.IsOpen && !p.KeepOpenOnSceneChange) toClose.Add(p);
            for (int i = 0; i < toClose.Count; i++) Close(toClose[i]);
        }
    }
}
