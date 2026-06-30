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

        [Header("洞口（遠方光亮出口）")]
        [Tooltip("洞口光的顏色（暖白最像出口天光）")] public Color ExitColor = new Color(1f, 0.96f, 0.85f, 1f);
        [Tooltip("最初（最遠）洞口大小 = 螢幕高 × 此值")] [Range(0.03f, 0.6f)] public float ExitStartFrac = 0.10f;
        [Tooltip("走出時（最近）洞口大小 = 螢幕高 × 此值（>1 撐滿畫面）")] [Range(0.5f, 3f)] public float ExitEndFrac = 2.2f;
        [Tooltip("每按一下洞口放大的動畫時間（秒）")] public float GrowSeconds = 0.35f;
        [Tooltip("洞口在畫面上的垂直位置（0=中央；負=偏下、正=偏上，螢幕高比例）")] [Range(-0.3f, 0.3f)] public float ExitYOffset = 0f;

        [Header("踏步晃動")]
        [Tooltip("每按一下畫面晃動幅度（像素）")] public float ShakeAmount = 26f;
        [Tooltip("晃動持續時間（秒）")] public float ShakeSeconds = 0.22f;

        [Header("操作 / 行為")]
        public KeyCode AdvanceKey = KeyCode.Space;
        public bool ClickToAdvance = true;
        [Tooltip("播放期間把 Time.timeScale 設 0（暫停遊戲、玩家不會在後面亂走）")] public bool PauseWhilePlaying = true;

        [Header("測試")]
        public bool PlayOnStart = false;
        public KeyCode TestPlayKey = KeyCode.T;

        /// <summary>走出隧道（收尾白光結束）時觸發。</summary>
        public event Action OnComplete;

        // ───────────── 內部 ─────────────
        enum Phase { Idle, Delay, Walking, Finishing, Done }
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
        float _finishT;
        float _prevTimeScale = 1f;

        Sprite _whiteSprite, _caveSprite;
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
                if (_flash) SetA(_flash, k);
                if (k >= 1f) Finish();
            }
        }

        void StepForward()
        {
            _step++;
            // 晃一下
            _shakeLeft = ShakeSeconds;
            if (_step >= Steps) { BeginFinish(); return; }
            // 洞口放大一級（往 end 推進）
            float target = Mathf.Lerp(ExitStartFrac, ExitEndFrac, (float)_step / Mathf.Max(1, Steps));
            _exitFrom = _exitCur; _exitTo = target; _growT = 0f;
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
                float amp = ShakeAmount * Mathf.Clamp01(_shakeLeft / Mathf.Max(0.01f, ShakeSeconds));
                _root.anchoredPosition = new Vector2(
                    (float)(_rnd.NextDouble() * 2 - 1) * amp,
                    (float)(_rnd.NextDouble() * 2 - 1) * amp * 0.7f);
            }
            else _root.anchoredPosition = Vector2.zero;
        }

        void Finish()
        {
            if (_phase == Phase.Done) return;
            _phase = Phase.Done;
            if (PauseWhilePlaying) Time.timeScale = _prevTimeScale;
            try { OnComplete?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            Debug.Log("[TunnelWalk] 走出隧道（OnComplete）。");
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
            _caveSprite = SpriteOf(MakeCaveMouth(256));

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

            _exit = NewImage(_root, "Exit", _caveSprite, ExitColor);
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

        // 洞口：不規則的柔光形（中央亮、向邊緣淡出；邊緣用幾個諧波做出有機的洞口輪廓）。白色，顏色由 Image.color 染。
        static Texture2D MakeCaveMouth(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    // 圓潤有機的洞口邊緣（小幅諧波擾動）＋外圈柔暈。
                    float edge = 0.50f + 0.045f * Mathf.Sin(3f * ang + 1.3f) + 0.028f * Mathf.Sin(5f * ang + 0.5f);
                    float core = Mathf.Clamp01((edge - r) / 0.14f);
                    core = core * core * (3f - 2f * core);             // 內亮（柔邊光盤）
                    float halo = Mathf.Clamp01((edge + 0.42f - r) / 0.55f) * 0.42f;   // 外圈柔暈
                    float a = Mathf.Clamp01(core + halo);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px); t.Apply();
            return t;
        }
    }
}
