using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dipan.Cutscene
{
    /// <summary>
    /// 「走出隧道」互動表演（全程式建構、零美術）。
    ///
    /// 表現：整片黑，遠方一個發光的洞口（不規則形）。玩家每按一次空白鍵 / 點滑鼠左鍵，
    /// 畫面晃一下（模擬踏一步）＋洞口放大一級（靠近）。按滿 `Steps` 下（可配置）就走出隧道
    /// （洞口撐滿、白光罩住）→ 觸發 `OnComplete`（之後由 cutscene 串接影片 / 換圖）。
    ///
    /// 自己在 Awake 建一套 Screen-Space Overlay Canvas（放在自生子物件上），不依賴 UI 框架。
    /// 動畫一律走 unscaledDeltaTime，所以可以在 `PauseWhilePlaying` 把 Time.timeScale 設 0（暫停遊戲）。
    /// 測試：勾 PlayOnStart 或按 `TestPlayKey` 播放。
    /// </summary>
    [DisallowMultipleComponent]
    public class TunnelWalkController : MonoBehaviour
    {
        [Header("步數")]
        [Tooltip("要按幾下才徹底走出隧道")] public int Steps = 5;
        [Tooltip("起播前延遲（秒）")] public float StartDelay = 0.2f;

        [Header("洞口（隧道盡頭的光，拱門形）")]
        [Tooltip("自備洞口圖（毛筆/插畫）：拖一張白色去背 PNG 進來就用它取代程式畫的拱門。留空＝程式畫")]
        public Sprite ExitImage;
        [Tooltip("洞口光的顏色（暖白最像出口天光）")] public Color ExitColor = new Color(1f, 0.96f, 0.85f, 1f);
        [Tooltip("最初（最遠）洞口大小 = 螢幕高 × 此值")] [Range(0.05f, 1f)] public float ExitStartFrac = 0.30f;
        [Tooltip("走出時（最近）洞口大小 = 螢幕高 × 此值（>1 撐滿畫面）")] [Range(0.5f, 3f)] public float ExitEndFrac = 1.7f;
        [Tooltip("每按一下洞口放大的動畫時間（秒）")] public float GrowSeconds = 0.30f;
        [Tooltip("洞口在畫面上的垂直位置（0=中央；負=偏下、正=偏上，螢幕高比例）")] [Range(-0.3f, 0.3f)] public float ExitYOffset = 0f;

        [Header("踏步晃動（左右交替衝一下）")]
        [Tooltip("每按一下左右橫衝的幅度（像素）；左右交替模擬踏步")] public float ShakeAmount = 52f;
        [Tooltip("晃動持續時間（秒）")] public float ShakeSeconds = 0.26f;

        [Header("操作 / 行為")]
        public KeyCode AdvanceKey = KeyCode.Space;
        public bool ClickToAdvance = true;
        [Tooltip("播放期間把 Time.timeScale 設 0（暫停遊戲、玩家不會在後面亂走）")] public bool PauseWhilePlaying = true;
        [Tooltip("走出隧道後、白光淡出露出新地圖的時間（秒）。換圖在白幕後面發生")] public float FadeOutSeconds = 0.5f;

        [Header("測試")]
        public bool PlayOnStart = false;
        public KeyCode TestPlayKey = KeyCode.T;

        /// <summary>走出隧道（收尾白光結束）時觸發。</summary>
        public event Action OnComplete;

        // ───────────── 內部 ─────────────
        enum Phase { Idle, Delay, Walking, Finishing, Fadeout, Done }
        Phase _phase = Phase.Idle;
        bool _built;
        Canvas _canvas;
        RectTransform _root;       // 內容根（晃動位移加在這）
        Image _backdrop;           // 全黑
        Image _exit;               // 洞口光
        RectTransform _exitRt;
        Image _flash;              // 收尾白光

        int _step;
        float _delayLeft;
        float _exitFrom, _exitTo, _exitCur, _growT;
        float _shakeLeft;
        int _shakeDir = 1;          // 左右交替（每按一步翻向）
        float _finishT;
        float _fadeT;
        float _prevTimeScale = 1f;

        Sprite _whiteSprite;
        System.Random _rnd = new System.Random(20260629);

        void Awake() { Build(); }

        void Start() { if (PlayOnStart) Play(); }

        // ───────────── 公開 API ─────────────

        public void Play()
        {
            if (!_built) Build();
            _step = 0;
            _delayLeft = Mathf.Max(0f, StartDelay);
            _phase = Phase.Delay;
            _exitCur = _exitFrom = _exitTo = ExitStartFrac;
            _growT = 1f;
            _shakeLeft = 0f;
            _finishT = 0f;
            _fadeT = 0f;
            if (_canvas) _canvas.enabled = true;
            if (_flash) SetA(_flash, 0f);
            ApplyExit(_exitCur);
            if (_root) _root.anchoredPosition = Vector2.zero;
            if (PauseWhilePlaying) { _prevTimeScale = Time.timeScale; Time.timeScale = 0f; }
        }

        /// <summary>直接走完（跳過）。</summary>
        public void Skip()
        {
            if (_phase == Phase.Idle || _phase == Phase.Done) return;
            _step = Steps;
            BeginFinish();
        }

        // ───────────── 更新 ─────────────

        void Update()
        {
            if (_phase == Phase.Idle || _phase == Phase.Done) { if (Input.GetKeyDown(TestPlayKey)) Play(); return; }

            float dt = Time.unscaledDeltaTime;

            if (_phase == Phase.Delay)
            {
                _delayLeft -= dt;
                if (_delayLeft <= 0f) _phase = Phase.Walking;
                return;
            }

            if (_phase == Phase.Walking)
            {
                bool advance = Input.GetKeyDown(AdvanceKey) || (ClickToAdvance && Input.GetMouseButtonDown(0));
                if (advance) StepForward();
                AnimateGrow(dt);
                AnimateShake(dt);
                return;
            }

            if (_phase == Phase.Finishing)
            {
                AnimateGrow(dt);
                AnimateShake(dt);
                _finishT += dt;
                float k = Mathf.Clamp01(_finishT / 0.6f);
                if (_flash) SetA(_flash, k);                 // 白光罩滿
                if (k >= 1f) DoComplete();
                return;
            }

            if (_phase == Phase.Fadeout)
            {
                // 白光罩著時已觸發 OnComplete（換圖在白幕後面發生），現在把白光淡出、露出新地圖。
                _fadeT += dt;
                float k = Mathf.Clamp01(_fadeT / Mathf.Max(0.05f, FadeOutSeconds));
                if (_flash) SetA(_flash, 1f - k);
                if (k >= 1f) { if (_canvas) _canvas.enabled = false; _phase = Phase.Done; }
            }
        }

        void StepForward()
        {
            _step++;
            // 踏一步：左右交替衝一下
            _shakeLeft = ShakeSeconds;
            _shakeDir = -_shakeDir;
            if (_step >= Steps) { BeginFinish(); return; }
            // 洞口放大一級（等比，每步視覺變化比例一致 → 第一下不突兀）
            _exitFrom = _exitCur; _exitTo = ExitSizeAt(_step); _growT = 0f;
        }

        // 第 step 步的洞口大小：等比內插（start → end），每步乘上固定倍率。
        float ExitSizeAt(int step)
        {
            float k = Mathf.Clamp01((float)step / Mathf.Max(1, Steps));
            float a = Mathf.Max(0.01f, ExitStartFrac);
            float b = Mathf.Max(0.01f, ExitEndFrac);
            return a * Mathf.Pow(b / a, k);
        }

        void BeginFinish()
        {
            _phase = Phase.Finishing;
            _exitFrom = _exitCur; _exitTo = ExitEndFrac; _growT = 0f;
            _shakeLeft = ShakeSeconds;
            _finishT = 0f;
        }

        void AnimateGrow(float dt)
        {
            if (_growT >= 1f) return;
            _growT += dt / Mathf.Max(0.05f, GrowSeconds);
            float e = EaseOutCubic(Mathf.Clamp01(_growT));
            _exitCur = Mathf.Lerp(_exitFrom, _exitTo, e);
            ApplyExit(_exitCur);
        }

        void AnimateShake(float dt)
        {
            if (_root == null) return;
            if (_shakeLeft > 0f)
            {
                _shakeLeft -= dt;
                float k = Mathf.Clamp01(_shakeLeft / Mathf.Max(0.01f, ShakeSeconds));   // 1→0 衰減
                float swing = Mathf.Sin((1f - k) * Mathf.PI);                            // 0→1→0 一次橫擺（衝出再回）
                float x = _shakeDir * swing * ShakeAmount
                          + (float)(_rnd.NextDouble() * 2 - 1) * ShakeAmount * k * 0.22f;   // 主橫衝 + 一點雜訊
                float y = (float)(_rnd.NextDouble() * 2 - 1) * ShakeAmount * k * 0.20f;      // 輕微上下顛
                _root.anchoredPosition = new Vector2(x, y);
            }
            else _root.anchoredPosition = Vector2.zero;
        }

        void DoComplete()
        {
            if (_phase == Phase.Fadeout || _phase == Phase.Done) return;
            if (PauseWhilePlaying) Time.timeScale = _prevTimeScale;   // 換圖前恢復時間
            _fadeT = 0f;
            _phase = Phase.Fadeout;                                   // 進入「白幕淡出」；OnComplete 在白幕下觸發換圖
            try { OnComplete?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            Debug.Log("[TunnelWalk] 走出隧道（OnComplete）→ 白幕後換圖、淡出露出新地圖。");
        }

        void ApplyExit(float frac)
        {
            if (_exitRt == null) return;
            float h = 1080f * frac;
            _exitRt.sizeDelta = new Vector2(h, h);
            _exitRt.anchoredPosition = new Vector2(0f, ExitYOffset * 1080f);
        }

        // ───────────── 建構 ─────────────

        void Build()
        {
            if (_built) return;
            _whiteSprite = SpriteOf(MakeSolid(Color.white));

            // 洞口圖：自備圖(Inspector) → Resources/InitialStory/TunnelMouth → 程式畫的拱門。
            Sprite exitSp = ExitImage;
            if (exitSp == null)
            {
                var tex = Resources.Load<Texture2D>("InitialStory/TunnelMouth");
                if (tex != null) exitSp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            if (exitSp == null) exitSp = SpriteOf(MakeTunnelMouth(256));

            var canvasGo = new GameObject("TunnelCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1200;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var canvasRt = canvasGo.GetComponent<RectTransform>();

            // 內容根（晃動加這上面）
            var rootGo = new GameObject("Content", typeof(RectTransform));
            rootGo.transform.SetParent(canvasRt, false);
            _root = (RectTransform)rootGo.transform;
            Stretch(_root);

            _backdrop = NewImage(_root, "Black", _whiteSprite, Color.black);
            Stretch((RectTransform)_backdrop.transform);

            _exit = NewImage(_root, "Exit", exitSp, ExitColor);
            _exit.preserveAspect = true;   // 自備圖不變形
            _exitRt = (RectTransform)_exit.transform;
            _exitRt.anchorMin = _exitRt.anchorMax = new Vector2(0.5f, 0.5f);
            _exitRt.pivot = new Vector2(0.5f, 0.5f);

            // 收尾白光（蓋全螢幕，在最上層）
            _flash = NewImage(canvasRt, "Flash", _whiteSprite, new Color(1f, 1f, 1f, 0f));
            Stretch((RectTransform)_flash.transform);

            _canvas.enabled = false;   // Play 時才開
            _built = true;
        }

        // ───────────── 工具 ─────────────

        static void SetA(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
        static float EaseOutCubic(float x) { float u = 1f - x; return 1f - u * u * u; }

        Image NewImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite; img.color = color; img.raycastTarget = false;
            return img;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        static Sprite SpriteOf(Texture2D t) => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);

        static Texture2D MakeSolid(Color c)
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            t.SetPixels(px); t.Apply();
            return t;
        }

        // 隧道口：拱門形（半圓頂＋直立兩側＋平底）＝卡通火車隧道口；柔邊＋外圈柔暈。白色，顏色由 Image.color 染。
        static Texture2D MakeTunnelMouth(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            float c = (n - 1) * 0.5f;
            const float halfW = 0.42f, bottomY = -0.82f, archY = -0.05f, fe = 0.05f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float nx = (x - c) / c, ny = (y - c) / c;   // ny 由下往上
                    // 拱門 = 直牆矩形(有上界) ∪ 頂端半圓盤。
                    float dDisk = halfW - Mathf.Sqrt(nx * nx + (ny - archY) * (ny - archY));
                    float dRect = Mathf.Min(Mathf.Min(halfW - Mathf.Abs(nx), ny - bottomY), archY - ny);
                    float d = Mathf.Max(dRect, dDisk);          // 內部為正
                    float core = Mathf.Clamp01((d + fe) / (2f * fe));
                    core = core * core * (3f - 2f * core);      // 柔邊
                    float halo = Mathf.Clamp01((d + 0.32f) / 0.32f) * 0.40f;   // 外圈柔暈
                    float a = Mathf.Clamp01(core + halo * (1f - core));
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px); t.Apply();
            return t;
        }
    }
}
