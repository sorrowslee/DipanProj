using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Dipan.Intro
{
    /// <summary>
    /// 序章「整頁漫畫 + 導讀式鏡頭」播放器（全程式建構、零 prefab）。
    ///
    /// 做法：每一「頁」是一張你自己排好版的完成圖。程式把整頁放大，鏡頭依序對焦每一「格」
    /// （你在 Focuses 填每格在頁面上的矩形），平移＋縮放讓那一格填滿畫面、看得清楚；按空白鍵
    /// 鏡頭移到下一格，整頁看完換下一頁。最後幾頁可設 Fullscreen（下墜：全篇幅、自動每 N 秒換），
    /// 播完接 IntroFallController 或載入下一個場景。
    ///
    /// 對齊與解析度由「你出的整頁高解析圖」決定（程式只負責鏡頭運動），所以對得準、夠清晰。
    ///
    /// 測試鍵：空白鍵/點畫面＝下一格/下一頁；R＝重播；Esc＝直接跳結尾。
    /// </summary>
    [DisallowMultipleComponent]
    public class IntroComicController : MonoBehaviour
    {
        /// <summary>一個鏡頭（一格或一組）。</summary>
        [Serializable]
        public class Focus
        {
            [Tooltip("這格/這組在頁面上的矩形：x,y=左上角，w,h=寬高，皆為頁面比例 0~1，y 由上往下。要綁兩格就框住兩格（中心會落在兩格之間）")]
            public Rect Area = new Rect(0f, 0f, 1f, 0.3f);

            [Tooltip("縮放微調：1=這格剛好填滿；>1 更近(裁更多)、<1 拉遠(留邊)")]
            public float Zoom = 1f;

            [Tooltip("左右微調：正數=畫面內容往右移，負數往左。單位=螢幕寬比例(0.1≈10%)")]
            public float XOffset = 0f;

            [Tooltip("上下微調：正數=畫面內容往上移(修正『這格太靠下』)，負數往下。單位=螢幕高比例(0.1≈10%)")]
            public float YOffset = 0f;
        }

        /// <summary>一頁的設定。</summary>
        [Serializable]
        public class ComicPage
        {
            [Tooltip("頁面圖名（Resources/InitialStory/ 下的檔名，不含副檔名），例如 Page_01")]
            public string Image = "Page_01";

            [Tooltip("依序對焦的鏡頭清單。鏡頭會一格一格移過去；留空＝整頁當一格")]
            public Focus[] Focuses = new Focus[0];

            [Tooltip("全篇幅模式（給下墜 13~15）：不對焦、整張 cover 滿版、依 HoldSeconds 自動換")]
            public bool Fullscreen = false;

            [Tooltip("Fullscreen 自動換頁停留秒數；>0=到時自動下一頁，<=0=等按鍵")]
            public float HoldSeconds = 0f;
        }

        // ───────────── Inspector ─────────────

        [Header("頁面清單（留空＝帶入 Page_01~03 + 下墜 Story_13~15 的預設）")]
        public List<ComicPage> Pages = new List<ComicPage>();

        [Header("圖來源")]
        public string ResourcePrefix = "InitialStory/";

        [Header("鏡頭")]
        [Tooltip("焦點格是否填滿整個畫面（cover，可能裁掉一點邊）；關閉＝完整顯示整格(fit，邊緣會看到鄰格)。每格的 Zoom 在此基礎上微調")]
        public bool FillScreen = true;
        [Tooltip("鏡頭平移/縮放到下一格的時間（秒）")] public float PanSeconds = 0.6f;
        [Tooltip("進每一頁時，從『整頁』推進到第一格的時間（秒）")] public float EnterSeconds = 0.7f;

        [Header("舞台底")]
        public bool ShowBackdrop = true;
        public Color BackdropColor = new Color(0.05f, 0.055f, 0.07f, 1f);
        public bool ShowVignette = true;

        [Header("換頁手感")]
        public float ClearFadeSeconds = 0.35f;
        public float StartDelay = 0.3f;

        [Header("操作鍵")]
        public KeyCode AdvanceKey = KeyCode.Space;
        public bool ClickToAdvance = true;
        public KeyCode ReplayKey = KeyCode.R;
        public KeyCode SkipKey = KeyCode.Escape;

        [Header("收尾轉場（優先序：Fall > 載場景 > 只發事件）")]
        [Tooltip("播完要接的墜落控制器；場景中先停用它的 GameObject、拖進來，播完自動啟用")]
        public IntroFallController FallToTrigger;
        public bool AutoLoadNextScene = false;
        public string NextSceneName = "MainScene";

        public event Action OnComplete;

        // ───────────── 內部 ─────────────

        Canvas _canvas;
        RectTransform _root;
        bool _built, _began, _done;
        float _delayLeft;
        int _pageIdx = -1, _focusIdx = 0;
        float _autoTimer = -1f;
        readonly Dictionary<string, Texture2D> _texCache = new();
        Sprite _whiteSprite, _vignetteSprite;
        Image _backdrop, _vignette;
        Text _skip;

        class PageView
        {
            public GameObject Go;
            public CanvasGroup Cg;
            public RectTransform PageRT;     // 整頁圖（縮放/平移做鏡頭）
            public float PageW0, PageH0;     // scale=1 時的頁面像素大小（H0=1080=整頁滿高）
            public bool Fullscreen;
            // 鏡頭運動
            public float ScaleCur, ScaleFrom, ScaleTo;
            public Vector2 PosCur, PosFrom, PosTo;
            public float AnimT, AnimDur;
            public bool Animating;
            // 進場/淡出
            public bool FadingOut; public float FadeT;
        }
        PageView _cur;
        readonly List<PageView> _fading = new();

        // ───────────── 生命週期 ─────────────

        void Awake()
        {
            BuildCanvas();
            if (Pages == null || Pages.Count == 0) Pages = BuildDefaultPages();
            Debug.Log($"[IntroComic] Awake：Pages={Pages.Count}、AdvanceKey={AdvanceKey}");
        }

        void OnEnable() => ResetAll();

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            bool mouseDown = Input.GetMouseButtonDown(0);
            bool overSkip = mouseDown && OverSkip();

            if (Input.GetKeyDown(ReplayKey)) { ResetAll(); return; }
            if (Input.GetKeyDown(SkipKey) || overSkip) { DoSkip(); return; }
            bool advance = Input.GetKeyDown(AdvanceKey) || (ClickToAdvance && mouseDown && !overSkip);

            if (!_began)
            {
                _delayLeft -= dt;
                if (_delayLeft <= 0f) { _began = true; Advance(); }
            }
            else if (advance) Advance();

            if (_autoTimer > 0f)
            {
                _autoTimer -= dt;
                if (_autoTimer <= 0f) { _autoTimer = -1f; Advance(); }
            }

            AnimateCamera(dt);
            AnimateFading(dt);

            if (_vignette) _vignette.transform.SetAsLastSibling();
            if (_skip) _skip.transform.SetAsLastSibling();   // Skip 永遠在最上層
        }

        // Skip：漫畫中按＝跳到墜落動畫；墜落中再按＝結束墜落、進 trigger 場景。
        void DoSkip()
        {
            if (!_done) { Complete(); }                       // 第一段：結束漫畫 → 接墜落動畫
            else if (FallToTrigger != null)
            {
                if (_skip) _skip.gameObject.SetActive(false);
                FallToTrigger.Skip();                         // 第二段：結束墜落 → 載入 trigger 場景
            }
        }

        bool OverSkip()
        {
            return _skip != null && _skip.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(_skip.rectTransform, Input.mousePosition, null);
        }

        // ───────────── 流程 ─────────────

        public void ResetAll()
        {
            if (!_built) BuildCanvas();
            if (_cur != null && _cur.Go) Destroy(_cur.Go);
            _cur = null;
            for (int i = _fading.Count - 1; i >= 0; i--) if (_fading[i].Go) Destroy(_fading[i].Go);
            _fading.Clear();
            _pageIdx = -1; _focusIdx = 0; _autoTimer = -1f; _done = false; _began = false;
            _delayLeft = Mathf.Max(0f, StartDelay);
            // 還原舞台（重播時）
            if (_skip) _skip.gameObject.SetActive(true);
            if (_backdrop) _backdrop.enabled = true;
            if (_vignette) _vignette.enabled = true;
            if (_canvas) { _canvas.enabled = true; _canvas.sortingOrder = 1000; }
        }

        public void Advance()
        {
            if (_done) return;
            if (Pages == null || Pages.Count == 0) { Debug.LogError("[IntroComic] Pages 是空的。"); return; }

            if (_cur != null && _cur.Animating) { SnapCamera(); return; }   // 移動中按鍵＝立刻定位

            var page = (_pageIdx >= 0 && _pageIdx < Pages.Count) ? Pages[_pageIdx] : null;
            int focusCount = (page != null && page.Focuses != null) ? page.Focuses.Length : 0;

            if (page != null && !page.Fullscreen && _focusIdx < focusCount - 1)
            {
                _focusIdx++;
                StartPan(FocusOf(page, _focusIdx), PanSeconds);
            }
            else
            {
                NextPage();
            }
        }

        void NextPage()
        {
            _pageIdx++;
            if (_pageIdx >= Pages.Count) { Complete(); return; }

            FadeOutCurrent();
            BuildPage(Pages[_pageIdx]);
            _focusIdx = 0;

            var p = Pages[_pageIdx];
            Debug.Log($"[IntroComic] Page → #{_pageIdx} {p.Image}（fullscreen {p.Fullscreen}）");
            bool hasFocus = p.Focuses != null && p.Focuses.Length > 0;
            if (p.Fullscreen)
            {
                _autoTimer = (p.HoldSeconds > 0f) ? p.HoldSeconds : -1f;
                if (hasFocus) StartPan(FocusOf(p, 0), EnterSeconds);   // 有指定焦點＝用你框的鏡頭，否則維持 BuildPage 的填滿
            }
            else { _autoTimer = -1f; StartPan(FocusOf(p, 0), EnterSeconds); }   // 從整頁推進到第一格
        }

        void Complete()
        {
            if (_done) return;
            _done = true; _autoTimer = -1f;
            try { OnComplete?.Invoke(); } catch (Exception e) { Debug.LogException(e); }

            if (FallToTrigger != null)
            {
                // 清掉漫畫畫面，只留右上角 Skip 浮在墜落動畫之上（不關整個 canvas）。
                if (_cur != null && _cur.Go) Destroy(_cur.Go);
                _cur = null;
                for (int i = _fading.Count - 1; i >= 0; i--) if (_fading[i].Go) Destroy(_fading[i].Go);
                _fading.Clear();
                if (_backdrop) _backdrop.enabled = false;
                if (_vignette) _vignette.enabled = false;
                if (_canvas) _canvas.sortingOrder = 1100;   // 蓋在墜落 canvas(1000) 之上
                FallToTrigger.gameObject.SetActive(true);
                return;
            }
            if (AutoLoadNextScene && !string.IsNullOrEmpty(NextSceneName))
            {
                if (Application.CanStreamedLevelBeLoaded(NextSceneName)) SceneManager.LoadScene(NextSceneName);
                else Debug.LogWarning($"[IntroComic] 場景 '{NextSceneName}' 不在 Build Settings。");
            }
        }

        static Focus FocusOf(ComicPage p, int i)
        {
            if (p.Focuses != null && i >= 0 && i < p.Focuses.Length) return p.Focuses[i];
            return new Focus { Area = new Rect(0f, 0f, 1f, 1f), Zoom = 1f, YOffset = 0f };   // 沒填＝整頁
        }

        // ───────────── 建立一頁 ─────────────

        void BuildPage(ComicPage page)
        {
            var tex = LoadTex(page.Image);
            if (tex == null) { Debug.LogWarning($"[IntroComic] 找不到圖 Resources/{ResourcePrefix}{page.Image}"); return; }
            float aspect = tex.width / Mathf.Max(1f, (float)tex.height);
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);

            var go = new GameObject("Page_" + page.Image, typeof(RectTransform));
            go.transform.SetParent(_root, false);
            var cg = go.AddComponent<CanvasGroup>();
            var rt = (RectTransform)go.transform;
            Stretch(rt);

            // 統一用「整頁圖 + 鏡頭」呈現（下墜也走這條，才能用 Focuses 微調、且不被裁）。
            float pageH0 = 1080f;
            float pageW0 = pageH0 * aspect;
            var pageGo = new GameObject("PageImg", typeof(RectTransform)).GetComponent<RectTransform>();
            pageGo.SetParent(rt, false);
            pageGo.anchorMin = pageGo.anchorMax = new Vector2(0.5f, 0.5f);
            pageGo.pivot = new Vector2(0.5f, 0.5f);
            pageGo.sizeDelta = new Vector2(pageW0, pageH0);
            var art = pageGo.gameObject.AddComponent<Image>();
            art.sprite = sprite; art.raycastTarget = false; art.preserveAspect = true;

            var view = new PageView { Go = go, Cg = cg, Fullscreen = page.Fullscreen, PageRT = pageGo, PageW0 = pageW0, PageH0 = pageH0 };
            cg.alpha = 0f;

            bool hasFocus = page.Focuses != null && page.Focuses.Length > 0;
            if (page.Fullscreen && !hasFocus)
            {
                // 全篇幅但沒指定焦點：填滿畫面(cover)、置中、靜止（消除黑邊）。會裁掉一點上下，
                // 所以圖請把主角擺在中間一帶，別太靠下。
                var (s, pos) = CamCover(view);
                view.ScaleCur = s; view.PosCur = pos; ApplyCam(view, s, pos);
            }
            else
            {
                // 一般頁：先擺整頁(scale=1)，稍後 pan 到第一格。
                view.ScaleCur = 1f; view.PosCur = Vector2.zero; ApplyCam(view, 1f, Vector2.zero);
            }

            _cur = view;
        }

        // 填滿畫面（cover）、置中。
        (float scale, Vector2 pos) CamCover(PageView v)
        {
            float s = Mathf.Max(1920f / v.PageW0, 1080f / v.PageH0);
            return (s, Vector2.zero);
        }

        // 算某個焦點框要的鏡頭 scale 與位移。
        (float scale, Vector2 pos) CamFor(PageView v, Focus f)
        {
            Rect a = f.Area;
            float fw = Mathf.Max(0.01f, a.width) * v.PageW0;
            float fh = Mathf.Max(0.01f, a.height) * v.PageH0;
            float sFit = Mathf.Min(1920f / fw, 1080f / fh);
            float sFill = Mathf.Max(1920f / fw, 1080f / fh);
            float s = (FillScreen ? sFill : sFit) * Mathf.Max(0.05f, f.Zoom);

            float fcx = a.x + a.width * 0.5f;
            float fcy = a.y + a.height * 0.5f;
            float offX = (fcx - 0.5f) * v.PageW0;        // 焦點中心相對頁面中心（scale=1, px）
            float offY = -(fcy - 0.5f) * v.PageH0;        // y 由上往下 → 反轉
            // XOffset 正數=內容往右移（螢幕寬比例）、YOffset 正數=內容往上移（螢幕高比例）
            return (s, new Vector2(-offX * s + f.XOffset * 1920f, -offY * s + f.YOffset * 1080f));
        }

        void StartPan(Focus focus, float dur)
        {
            if (_cur == null || _cur.PageRT == null) return;
            var (s, pos) = CamFor(_cur, focus);
            StartCam(s, pos, dur);
        }

        void StartCam(float scaleTo, Vector2 posTo, float dur)
        {
            if (_cur == null) return;
            _cur.ScaleFrom = _cur.ScaleCur; _cur.PosFrom = _cur.PosCur;
            _cur.ScaleTo = scaleTo; _cur.PosTo = posTo;
            _cur.AnimT = 0f; _cur.AnimDur = Mathf.Max(0.05f, dur);
            _cur.Animating = true;
        }

        void SnapCamera()
        {
            if (_cur == null) return;
            _cur.ScaleCur = _cur.ScaleTo; _cur.PosCur = _cur.PosTo;
            _cur.Animating = false;
            ApplyCam(_cur, _cur.ScaleCur, _cur.PosCur);
        }

        void ApplyCam(PageView v, float scale, Vector2 pos)
        {
            if (v.PageRT == null) return;
            v.PageRT.localScale = new Vector3(scale, scale, 1f);
            v.PageRT.anchoredPosition = pos;
        }

        void AnimateCamera(float dt)
        {
            if (_cur == null) return;
            if (_cur.Cg.alpha < 1f) _cur.Cg.alpha = Mathf.Min(1f, _cur.Cg.alpha + dt / Mathf.Max(0.05f, EnterSeconds));
            if (!_cur.Animating) return;

            _cur.AnimT += dt;
            float k = Mathf.Clamp01(_cur.AnimT / _cur.AnimDur);
            float e = k * k * (3f - 2f * k);   // smoothstep（緩入緩出）
            _cur.ScaleCur = Mathf.Lerp(_cur.ScaleFrom, _cur.ScaleTo, e);
            _cur.PosCur = Vector2.Lerp(_cur.PosFrom, _cur.PosTo, e);
            ApplyCam(_cur, _cur.ScaleCur, _cur.PosCur);
            if (k >= 1f) _cur.Animating = false;
        }

        void FadeOutCurrent()
        {
            if (_cur == null) return;
            _cur.FadingOut = true; _cur.FadeT = 0f;
            _fading.Add(_cur);
            _cur = null;
        }

        void AnimateFading(float dt)
        {
            for (int i = _fading.Count - 1; i >= 0; i--)
            {
                var v = _fading[i];
                if (v.Go == null) { _fading.RemoveAt(i); continue; }
                v.FadeT += dt;
                float k = Mathf.Clamp01(v.FadeT / Mathf.Max(0.05f, ClearFadeSeconds));
                v.Cg.alpha = 1f - k;
                if (k >= 1f) { Destroy(v.Go); _fading.RemoveAt(i); }
            }
        }

        // ───────────── 建構 / 工具 ─────────────

        void BuildCanvas()
        {
            if (_built) return;
            _whiteSprite = Sprite.Create(MakeWhite(), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            _vignetteSprite = SpriteOf(MakeVignette(256));

            var canvasGo = new GameObject("ComicCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            _root = canvasGo.GetComponent<RectTransform>();

            if (ShowBackdrop) { _backdrop = NewImage(_root, "Backdrop", _whiteSprite, BackdropColor); Stretch((RectTransform)_backdrop.transform); }
            if (ShowVignette) { _vignette = NewImage(_root, "Vignette", _vignetteSprite, new Color(0, 0, 0, 0.9f)); Stretch((RectTransform)_vignette.transform); }

            _built = true;
            BuildSkip();
        }

        // 右上角「Skip」字樣：粗體、放大、無背景、帶外框讓任何底色都看得到。
        void BuildSkip()
        {
            if (_skip != null) return;
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (font == null) { try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }

            var go = new GameObject("Skip", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);   // 右上角
            rt.anchoredPosition = new Vector2(-52f, -40f);
            rt.sizeDelta = new Vector2(380f, 130f);                         // 點擊範圍
            var t = go.AddComponent<Text>();
            t.font = font; t.fontSize = 78; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.UpperRight;
            t.color = new Color(1f, 1f, 1f, 0.92f);
            t.text = "Skip";
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(3f, -3f);
            _skip = t;
        }

        Texture2D LoadTex(string image)
        {
            if (_texCache.TryGetValue(image, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>(ResourcePrefix + image);
            if (t != null) _texCache[image] = t;
            return t;
        }

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

        static Texture2D MakeWhite()
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            t.SetPixels(px); t.Apply();
            return t;
        }

        static Texture2D MakeVignette(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.5f) / 0.5f));
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        // 在 Inspector 右鍵元件標題 →「帶入預設頁面排版…」一鍵把預設寫進 Pages（之後才能逐格調 Focuses）。
        [ContextMenu("帶入預設頁面排版（Page_01~03 + 下墜 13~15）")]
        void FillDefaultPages()
        {
            Pages = BuildDefaultPages();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            Debug.Log($"[IntroComic] 已帶入預設 {Pages.Count} 頁，可在 Inspector 展開 Pages 逐格調 Focuses。");
        }

        // 元件首次加入 / 按 Reset 時自動帶入一份，方便直接編輯。
        void Reset()
        {
            if (Pages == null || Pages.Count == 0) Pages = BuildDefaultPages();
        }

        // 預設：三頁（你已放 Page_01~03）＋下墜 13~15。Focuses 是「每格在頁面上的矩形(0~1,y由上往下)」，依你版面微調。
        List<ComicPage> BuildDefaultPages()
        {
            return new List<ComicPage>
            {
                new ComicPage{ Image="Page_01", Focuses=new[]{
                    new Focus{ Area=new Rect(0f,0f,1f,0.30f) },     // 1 上寬
                    new Focus{ Area=new Rect(0f,0.31f,1f,0.22f) },  // 2+3 綁一組（中心在兩格之間）
                    new Focus{ Area=new Rect(0f,0.55f,1f,0.45f) },  // 4 下寬
                }},
                new ComicPage{ Image="Page_02", Focuses=new[]{
                    new Focus{ Area=new Rect(0f,0f,0.52f,1f) },        // 5 直幅左
                    new Focus{ Area=new Rect(0.52f,0f,0.48f,0.33f) }, // 6 右上
                    new Focus{ Area=new Rect(0.52f,0.33f,0.48f,0.34f) },// 7 右中
                    new Focus{ Area=new Rect(0.52f,0.67f,0.48f,0.33f) },// 8 右下
                }},
                new ComicPage{ Image="Page_03", Focuses=new[]{
                    new Focus{ Area=new Rect(0f,0f,1f,0.30f) },     // 9 上
                    new Focus{ Area=new Rect(0f,0.32f,1f,0.38f) },  // 10+11 綁一組（中心在兩格之間）
                    new Focus{ Area=new Rect(0f,0.72f,1f,0.28f) },  // 12 下
                }},

                new ComicPage{ Image="Story_13", Fullscreen=true, HoldSeconds=1.0f },
                new ComicPage{ Image="Story_14", Fullscreen=true, HoldSeconds=1.0f },
                new ComicPage{ Image="Story_15", Fullscreen=true, HoldSeconds=1.0f },
            };
        }
    }
}
