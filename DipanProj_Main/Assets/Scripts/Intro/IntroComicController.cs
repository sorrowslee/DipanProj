using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Dipan.Intro
{
    /// <summary>
    /// 序章「韓漫式開場漫畫」播放器（全程式建構、零 prefab、零美術接線）。
    ///
    /// 玩法：每按一次空白鍵（或點畫面），下一格分鏡從指定方向「滑入」畫面、累積成構圖；
    /// 到情緒轉折（叢集 Cluster 改變）會先清空前面所有格、換下一組構圖；
    /// 最後幾格（墜落）標成 Fullscreen，會清空全部、用整個畫面、依 HoldSeconds 自動計時換圖，
    /// 播完接 IntroFallController（墜落程式動畫）或載入下一個場景。
    ///
    /// 「只露出重要部分」＝白框是一個視窗，圖在 runtime 依每格的 Crop（正規化子矩形）裁切後填滿白框。
    /// 所以「露出哪一塊」「擺哪裡」「從哪滑入」「轉幾度」全部是 Inspector 上的資料，改數字即可，不動程式。
    ///
    /// 設計同 IntroFallController：自己在 Awake 建一套 Screen-Space Overlay Canvas。
    /// 圖走 Resources/InitialStory/Story_NN（用 Texture2D 載入＋Sprite.Create 裁切，不挑 import 類型）。
    ///
    /// 測試鍵：空白鍵/點畫面＝下一格；R＝重播；Esc＝直接跳到結尾。
    /// </summary>
    [DisallowMultipleComponent]
    public class IntroComicController : MonoBehaviour
    {
        public enum SlideDir { Left, Right, Top, Bottom, FadeOnly }

        /// <summary>一格分鏡的完整設定（全部 Inspector 可調，方便反覆微調）。</summary>
        [Serializable]
        public class ComicPanel
        {
            [Tooltip("圖名（對應 Resources/InitialStory/ 下的檔名，不含副檔名），例如 Story_01")]
            public string Image = "Story_01";

            [Tooltip("叢集編號：相同編號的格會累積在同一畫面；編號一變＝先清空前面所有格再演下一組")]
            public int Cluster = 0;

            [Tooltip("白框在畫面上的位置（0~1，左下=0,0 右上=1,1；x 0.5=水平置中）")]
            public Vector2 Pos = new Vector2(0.5f, 0.5f);

            [Tooltip("白框高度 = 參考螢幕高(1080) × 此值；寬度依裁切後的長寬比自動算")]
            [Range(0.1f, 1.2f)] public float HeightFrac = 0.7f;

            [Tooltip("露出原圖的哪一塊（正規化：x,y 左下角；w,h 寬高；(0,0,1,1)=整張）。y 由下往上")]
            public Rect Crop = new Rect(0f, 0f, 1f, 1f);

            [Tooltip("白框旋轉角度（度，正=逆時針）")]
            public float Rotation = 0f;

            [Tooltip("從哪個方向滑入畫面")]
            public SlideDir From = SlideDir.Right;

            [Tooltip("是否由程式套白框＋裁切。成品圖（已自帶框/對白）關掉＝原樣呈現；純插畫開著＝程式幫你框")]
            public bool DrawFrame = true;

            [Tooltip("滑入時間（秒）；0 或負＝用全域 DefaultSlideSeconds")]
            public float SlideSeconds = 0f;

            [Tooltip("彈入回彈感（overshoot）：給「重擊格」用，例如抓手腕那一格")]
            public bool Punch = false;

            [Tooltip("滑入後輕微抖動一下：給「斷裂格」那種衝擊用")]
            public bool Shake = false;

            [Tooltip("全篇幅特寫：清空所有格、填滿整個畫面（給墜落 09~11）")]
            public bool Fullscreen = false;

            [Tooltip("全篇幅自動換圖的停留秒數；>0＝到時自動演下一格，<=0＝等玩家按鍵")]
            public float HoldSeconds = 0f;
        }

        // ───────────── Inspector 設定 ─────────────

        [Header("分鏡清單（留空＝自動帶入 Story_01~11 的首版排版）")]
        public List<ComicPanel> Panels = new List<ComicPanel>();

        [Header("圖來源")]
        [Tooltip("Resources 路徑前綴；圖放在 Resources/InitialStory/Story_NN")]
        public string ResourcePrefix = "InitialStory/";

        [Header("舞台底（別讓漫畫浮在預設藍底上）")]
        [Tooltip("鋪一塊深色氛圍底，蓋掉攝影機預設色")] public bool ShowBackdrop = true;
        [Tooltip("底色（深、偏冷的暗色最有氛圍）")] public Color BackdropColor = new Color(0.05f, 0.055f, 0.07f, 1f);
        [Tooltip("四角壓暗的暗角，增加聚焦與氛圍")] public bool ShowVignette = true;

        [Header("白框外觀")]
        [Tooltip("框色（純白偏冷硬；暖白像舊照片更有質感）")] public Color FrameColor = new Color(0.96f, 0.95f, 0.92f, 1f);
        [Tooltip("白框邊框厚度（參考解析度像素）")] public float FrameBorder = 14f;
        [Tooltip("白框後方的柔和投影")] public bool ShowShadow = true;
        [Tooltip("投影顏色/濃度")] public Color ShadowColor = new Color(0f, 0f, 0f, 0.45f);
        [Tooltip("投影偏移")] public Vector2 ShadowOffset = new Vector2(14f, -20f);
        [Tooltip("投影比白框外擴多少（參考像素），越大越柔越散")] public float ShadowSpread = 38f;

        [Header("滑入手感")]
        [Tooltip("沒在該格指定 SlideSeconds 時用的預設滑入時間")] public float DefaultSlideSeconds = 0.45f;
        [Tooltip("清空舊格時的淡出時間")] public float ClearFadeSeconds = 0.3f;
        [Tooltip("起播前延遲（秒）")] public float StartDelay = 0.3f;

        [Header("操作鍵")]
        public KeyCode AdvanceKey = KeyCode.Space;
        [Tooltip("是否也能用滑鼠左鍵點畫面推進")] public bool ClickToAdvance = true;
        public KeyCode ReplayKey = KeyCode.R;
        public KeyCode SkipKey = KeyCode.Escape;

        [Header("收尾轉場（三選一優先序：Fall > 載場景 > 只發事件）")]
        [Tooltip("漫畫播完要接的墜落控制器；場景中先把它的 GameObject 停用，這裡拖進來。播完會自動啟用它（自動開始墜落）")]
        public IntroFallController FallToTrigger;
        [Tooltip("沒有指定墜落控制器時，是否自動載入下一個場景")] public bool AutoLoadNextScene = false;
        [Tooltip("下一個場景名（需加進 Build Settings）")] public string NextSceneName = "SampleScene";

        [Header("除錯（排查用，正式關閉）")]
        [Tooltip("在畫面左上角顯示目前狀態（排查輸入/進度），並印 Console log")]
        public bool ShowDebugHud = false;

        /// <summary>漫畫播完時觸發；外部可接「開始墜落 / 載遊戲」。</summary>
        public event Action OnComplete;

        // ───────────── 內部狀態 ─────────────

        Canvas _canvas;
        RectTransform _root;
        bool _built;
        bool _began;
        bool _done;
        float _delayLeft;
        int _index = -1;            // 目前播到第幾格
        int _curCluster = int.MinValue;
        float _autoTimer = -1f;     // 全篇幅自動換圖計時（<0＝不計時）

        readonly Dictionary<string, Texture2D> _texCache = new();

        // 一個畫面上的活動格
        class View
        {
            public GameObject Go;
            public RectTransform Rt;
            public CanvasGroup Cg;
            public Vector2 FromPos, RestPos;
            public float T, Dur, RotZ;
            public bool Punch, Shake;
            public enum St { In, Rest, Out }
            public St State;
            public float OutT;
        }
        readonly List<View> _views = new();

        Sprite _whiteSprite, _vignetteSprite, _softBlobSprite;
        Text _hud;

        // ───────────── 生命週期 ─────────────

        void Awake()
        {
            BuildCanvas();
            if (Panels == null || Panels.Count == 0) Panels = BuildDefaultPanels();
            Debug.Log($"[IntroComic] Awake：Panels={Panels.Count} 筆、AdvanceKey={AdvanceKey}、ClickToAdvance={ClickToAdvance}");
        }

        void OnEnable()
        {
            ResetAll();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // 操作鍵
            if (Input.GetKeyDown(ReplayKey)) { ResetAll(); return; }
            if (Input.GetKeyDown(SkipKey)) { SkipToEnd(); return; }
            bool advance = Input.GetKeyDown(AdvanceKey) || (ClickToAdvance && Input.GetMouseButtonDown(0));

            // 起播延遲後自動顯示第一格
            if (!_began)
            {
                _delayLeft -= dt;
                if (_delayLeft <= 0f) { _began = true; Advance(); }
            }
            else if (advance)
            {
                Advance();
            }

            // 全篇幅自動換圖
            if (_autoTimer > 0f)
            {
                _autoTimer -= dt;
                if (_autoTimer <= 0f)
                {
                    _autoTimer = -1f;
                    if (_index >= Panels.Count - 1) Complete();
                    else Advance();
                }
            }

            AnimateViews(dt);

            if (_hud != null)
            {
                _hud.transform.SetAsLastSibling();
                _hud.text = $"IntroComic  began:{_began}  進度:{_index + 1}/{(Panels?.Count ?? 0)}  delay:{Mathf.Max(0f, _delayLeft):0.00}  done:{_done}  views:{_views.Count}\n(空白鍵/點畫面=下一格  R=重播  Esc=跳尾) — 若沒反應請先點一下 Game 視窗給它鍵盤焦點";
            }
        }

        // ───────────── 公開 API ─────────────

        public void ResetAll()
        {
            if (!_built) BuildCanvas();
            for (int i = _views.Count - 1; i >= 0; i--) if (_views[i].Go) Destroy(_views[i].Go);
            _views.Clear();
            _index = -1;
            _curCluster = int.MinValue;
            _autoTimer = -1f;
            _done = false;
            _began = false;
            _delayLeft = Mathf.Max(0f, StartDelay);
        }

        /// <summary>直接跳到最後一格的結尾（測試用）。</summary>
        public void SkipToEnd()
        {
            if (_done) return;
            Complete();
        }

        /// <summary>推進到下一格。</summary>
        public void Advance()
        {
            if (_done) return;
            if (Panels == null || Panels.Count == 0) { Debug.LogError("[IntroComic] Panels 是空的，無法播放（檢查 Awake 是否有跑、或 Inspector 自行清空了清單）。"); return; }

            // 推進前先把仍在滑入的格瞬間定位（避免按太快卡在半路）
            foreach (var v in _views) if (v.State == View.St.In) { v.Rt.anchoredPosition = v.RestPos; v.Cg.alpha = 1f; v.State = View.St.Rest; }

            if (_index >= Panels.Count - 1) { Complete(); return; }
            _index++;
            var p = Panels[_index];
            Debug.Log($"[IntroComic] Advance → #{_index} {p.Image}（cluster {p.Cluster}, fullscreen {p.Fullscreen}）");

            // 叢集改變（或進入全篇幅）＝先清空前面所有格
            if (p.Cluster != _curCluster || p.Fullscreen)
            {
                ClearAll();
                _curCluster = p.Cluster;
            }

            SpawnView(p);

            // 全篇幅自動計時
            _autoTimer = (p.Fullscreen && p.HoldSeconds > 0f) ? p.HoldSeconds : -1f;
        }

        // ───────────── 顯示一格 ─────────────

        void SpawnView(ComicPanel p)
        {
            var tex = LoadTex(p.Image);
            if (tex == null)
            {
                Debug.LogWarning($"[IntroComic] 找不到圖 Resources/{ResourcePrefix}{p.Image}");
                return;
            }

            if (p.Fullscreen) { SpawnFullscreen(p, tex); return; }

            int tw = tex.width, th = tex.height;
            Rect c = p.Crop;
            var pr = new Rect(
                Mathf.Clamp01(c.x) * tw,
                Mathf.Clamp01(c.y) * th,
                Mathf.Clamp01(c.width) * tw,
                Mathf.Clamp01(c.height) * th);
            if (pr.width < 2f || pr.height < 2f) { pr = new Rect(0, 0, tw, th); }
            var sprite = Sprite.Create(tex, pr, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);

            float frameH = p.HeightFrac * 1080f;
            float cropAspect = pr.width / Mathf.Max(1f, pr.height);
            float frameW = frameH * cropAspect;

            // 根（定位＋旋轉＋淡入用 CanvasGroup）
            var go = new GameObject("Panel_" + p.Image, typeof(RectTransform));
            go.transform.SetParent(_root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = p.Pos;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(frameW, frameH);
            rt.localEulerAngles = new Vector3(0, 0, p.Rotation);
            var cg = go.AddComponent<CanvasGroup>();

            // 柔和投影（用羽化 blob，外擴 ShadowSpread）
            if (ShowShadow)
            {
                var sh = NewImage(rt, "Shadow", _softBlobSprite, ShadowColor);
                var srt = (RectTransform)sh.transform;
                Stretch(srt);
                srt.offsetMin = new Vector2(-ShadowSpread + ShadowOffset.x, -ShadowSpread + ShadowOffset.y);
                srt.offsetMax = new Vector2(ShadowSpread + ShadowOffset.x, ShadowSpread + ShadowOffset.y);
            }

            // 白框（成品圖可關，DrawFrame=false 時原樣呈現）
            if (p.DrawFrame)
            {
                var frame = NewImage(rt, "Frame", _whiteSprite, FrameColor);
                Stretch((RectTransform)frame.transform);
            }

            // 內容（裁切後的圖；有框就內縮邊框厚度，無框則填滿）
            float pad = p.DrawFrame ? FrameBorder : 0f;
            var content = NewImage(rt, "Art", sprite, Color.white);
            var crt = (RectTransform)content.transform;
            Stretch(crt);
            crt.offsetMin = new Vector2(pad, pad);
            crt.offsetMax = new Vector2(-pad, -pad);
            content.type = Image.Type.Simple;
            content.preserveAspect = false;

            RegisterView(go, rt, cg, Vector2.zero, SlideOffset(p.From), DurOf(p), p.Punch, p.Shake);
        }

        void SpawnFullscreen(ComicPanel p, Texture2D tex)
        {
            Rect c = p.Crop;
            int tw = tex.width, th = tex.height;
            var pr = new Rect(Mathf.Clamp01(c.x) * tw, Mathf.Clamp01(c.y) * th,
                              Mathf.Clamp01(c.width) * tw, Mathf.Clamp01(c.height) * th);
            if (pr.width < 2f || pr.height < 2f) pr = new Rect(0, 0, tw, th);
            var sprite = Sprite.Create(tex, pr, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);

            var go = new GameObject("Full_" + p.Image, typeof(RectTransform));
            go.transform.SetParent(_root, false);
            var rt = (RectTransform)go.transform;
            Stretch(rt);
            var cg = go.AddComponent<CanvasGroup>();

            // 黑底
            var bg = NewImage(rt, "BG", _whiteSprite, Color.black);
            Stretch((RectTransform)bg.transform);

            // 圖：等比放大到「覆蓋」整個畫面（cover），多餘裁掉
            var art = NewImage(rt, "Art", sprite, Color.white);
            var art_rt = (RectTransform)art.transform;
            art_rt.anchorMin = art_rt.anchorMax = new Vector2(0.5f, 0.5f);
            art_rt.pivot = new Vector2(0.5f, 0.5f);
            float refW = 1920f, refH = 1080f;
            float imgAspect = pr.width / Mathf.Max(1f, pr.height);
            float scrAspect = refW / refH;
            Vector2 size = imgAspect > scrAspect
                ? new Vector2(refH * imgAspect, refH)   // 比螢幕寬：高貼齊
                : new Vector2(refW, refW / imgAspect);  // 比螢幕高：寬貼齊
            art_rt.sizeDelta = size;
            art.preserveAspect = false;

            // 全篇幅：純淡入（FadeOnly）
            RegisterView(go, rt, cg, Vector2.zero, Vector2.zero, DurOf(p), false, false);
        }

        void RegisterView(GameObject go, RectTransform rt, CanvasGroup cg, Vector2 restPos, Vector2 fromOffset, float dur, bool punch, bool shake)
        {
            rt.anchoredPosition = restPos + fromOffset;
            cg.alpha = (fromOffset == Vector2.zero) ? 0f : 0.001f;
            var v = new View
            {
                Go = go, Rt = rt, Cg = cg,
                FromPos = restPos + fromOffset, RestPos = restPos,
                T = 0f, Dur = Mathf.Max(0.05f, dur), RotZ = rt.localEulerAngles.z,
                Punch = punch, Shake = shake, State = View.St.In, OutT = 0f
            };
            _views.Add(v);
        }

        // ───────────── 動畫推進 ─────────────

        void AnimateViews(float dt)
        {
            for (int i = _views.Count - 1; i >= 0; i--)
            {
                var v = _views[i];
                if (v.Go == null) { _views.RemoveAt(i); continue; }

                if (v.State == View.St.In)
                {
                    v.T += dt;
                    float k = Mathf.Clamp01(v.T / v.Dur);
                    float e = v.Punch ? EaseOutBack(k) : EaseOutCubic(k);
                    v.Rt.anchoredPosition = Vector2.LerpUnclamped(v.FromPos, v.RestPos, e);
                    v.Cg.alpha = Mathf.Clamp01(k * 2.5f);
                    if (k >= 1f)
                    {
                        v.Rt.anchoredPosition = v.RestPos;
                        v.Cg.alpha = 1f;
                        v.State = View.St.Rest;
                        if (v.Shake) v.T = 0f; // 借 T 當抖動計時
                    }
                }
                else if (v.State == View.St.Rest && v.Shake)
                {
                    v.T += dt;
                    float dur = 0.4f;
                    if (v.T < dur)
                    {
                        float a = (1f - v.T / dur) * 6f;           // 衰減
                        float wob = Mathf.Sin(v.T * 50f) * a;       // 度
                        var ang = v.Rt.localEulerAngles; ang.z = v.RotZ + wob; v.Rt.localEulerAngles = ang;
                    }
                    else { var ang = v.Rt.localEulerAngles; ang.z = v.RotZ; v.Rt.localEulerAngles = ang; v.Shake = false; }
                }
                else if (v.State == View.St.Out)
                {
                    v.OutT += dt;
                    float k = Mathf.Clamp01(v.OutT / Mathf.Max(0.05f, ClearFadeSeconds));
                    v.Cg.alpha = 1f - k;
                    if (k >= 1f) { Destroy(v.Go); _views.RemoveAt(i); }
                }
            }
        }

        void ClearAll()
        {
            foreach (var v in _views)
                if (v.State != View.St.Out) { v.State = View.St.Out; v.OutT = 0f; }
        }

        // ───────────── 收尾 ─────────────

        void Complete()
        {
            if (_done) return;
            _done = true;
            _autoTimer = -1f;

            try { OnComplete?.Invoke(); } catch (Exception e) { Debug.LogException(e); }

            if (FallToTrigger != null)
            {
                // 啟用墜落控制器（它的 OnEnable 會自動開始墜落）；漫畫畫面收起來。
                FallToTrigger.gameObject.SetActive(true);
                if (_canvas) _canvas.enabled = false;
                return;
            }
            if (AutoLoadNextScene && !string.IsNullOrEmpty(NextSceneName))
            {
                if (Application.CanStreamedLevelBeLoaded(NextSceneName))
                    SceneManager.LoadScene(NextSceneName);
                else
                    Debug.LogWarning($"[IntroComic] 下一個場景 '{NextSceneName}' 不在 Build Settings，未載入。");
            }
        }

        // ───────────── 建構 / 工具 ─────────────

        void BuildCanvas()
        {
            if (_built) return;
            _whiteSprite = Sprite.Create(MakeWhite(), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            _vignetteSprite = SpriteOf(MakeVignette(256));
            _softBlobSprite = SpriteOf(MakeSoftBlob(128));

            // 把 Canvas 放在「自己生成的子物件」上（保證乾淨、必含 RectTransform），
            // 不去動控制器自身那顆空物件的 Transform，避免「無法在此 GameObject 加 Canvas」的問題。
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

            // 舞台底（最先建＝在所有分鏡後方）
            if (ShowBackdrop)
            {
                var bd = NewImage(_root, "Backdrop", _whiteSprite, BackdropColor);
                Stretch((RectTransform)bd.transform);
            }
            if (ShowVignette)
            {
                var vg = NewImage(_root, "Vignette", _vignetteSprite, new Color(0f, 0f, 0f, 0.9f));
                Stretch((RectTransform)vg.transform);
            }

            _built = true;
            BuildDebugHud();
        }

        void BuildDebugHud()
        {
            if (!ShowDebugHud || _hud != null) return;
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (font == null) { try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }

            var go = new GameObject("DebugHud", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);
            rt.sizeDelta = new Vector2(1700f, 90f);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = 30;
            t.color = new Color(1f, 1f, 0.55f, 0.95f);
            t.raycastTarget = false;
            t.text = font == null ? "" : "IntroComic 啟動中…";
            _hud = t;
        }

        Texture2D LoadTex(string image)
        {
            if (_texCache.TryGetValue(image, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>(ResourcePrefix + image);
            if (t != null) _texCache[image] = t;
            return t;
        }

        Vector2 SlideOffset(SlideDir d)
        {
            const float W = 2200f, H = 1400f;   // 大於參考解析度，保證滑出畫面外
            switch (d)
            {
                case SlideDir.Left: return new Vector2(-W, 0);
                case SlideDir.Right: return new Vector2(W, 0);
                case SlideDir.Top: return new Vector2(0, H);
                case SlideDir.Bottom: return new Vector2(0, -H);
                default: return Vector2.zero;   // FadeOnly
            }
        }

        float DurOf(ComicPanel p) => p.SlideSeconds > 0f ? p.SlideSeconds : DefaultSlideSeconds;

        static float EaseOutCubic(float x) { float u = 1f - x; return 1f - u * u * u; }
        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = x - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        Image NewImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        static Texture2D MakeWhite()
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            t.SetPixels(px); t.Apply();
            return t;
        }

        static Sprite SpriteOf(Texture2D t)
            => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);

        // 暗角：中央透明、四周漸黑（白色，alpha 隨半徑上升）。
        static Texture2D MakeVignette(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421f;   // 0 中心 ~1 角落
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.45f) / 0.55f));
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        // 羽化方塊：中央實心、邊緣淡出（給柔和投影用）。
        static Texture2D MakeSoftBlob(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            float feather = n * 0.30f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Min(x, n - 1 - x);
                    float dy = Mathf.Min(y, n - 1 - y);
                    float d = Mathf.Min(dx, dy);
                    float a = Mathf.Clamp01(d / feather);
                    a = a * a * (3f - 2f * a);   // smoothstep
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        // ───────────── 首版排版（對應討論用的蒙太奇分鏡）─────────────
        // 全部都能在 Inspector 直接改。Pos 是白框中心(0~1, 左下原點)；Crop 是露出的子矩形(y 由下往上)。
        List<ComicPanel> BuildDefaultPanels()
        {
            return new List<ComicPanel>
            {
                // 叢集 0：登場與相遇
                new ComicPanel{ Image="Story_01", Cluster=0, Pos=new Vector2(0.34f,0.48f), HeightFrac=0.80f, Crop=new Rect(0f,0f,1f,1f),        Rotation=-2, From=SlideDir.Bottom },
                new ComicPanel{ Image="Story_02", Cluster=0, Pos=new Vector2(0.70f,0.64f), HeightFrac=0.50f, Crop=new Rect(0f,0.20f,1f,0.55f),   Rotation=3,  From=SlideDir.Right },

                // 叢集 1：危機
                new ComicPanel{ Image="Story_03", Cluster=1, Pos=new Vector2(0.31f,0.62f), HeightFrac=0.56f, Crop=new Rect(0f,0.42f,1f,0.58f),   Rotation=-3, From=SlideDir.Top },
                new ComicPanel{ Image="Story_04", Cluster=1, Pos=new Vector2(0.70f,0.48f), HeightFrac=0.74f, Crop=new Rect(0f,0f,1f,1f),          Rotation=4,  From=SlideDir.Right },
                new ComicPanel{ Image="Story_05", Cluster=1, Pos=new Vector2(0.49f,0.34f), HeightFrac=0.42f, Crop=new Rect(0.08f,0.12f,0.86f,0.72f), Rotation=-2, From=SlideDir.Bottom, Punch=true },

                // 叢集 2：救命之恩・溫情
                new ComicPanel{ Image="Story_06", Cluster=2, Pos=new Vector2(0.40f,0.52f), HeightFrac=0.74f, Crop=new Rect(0f,0f,1f,1f),          Rotation=-1, From=SlideDir.Top },
                new ComicPanel{ Image="Story_07", Cluster=2, Pos=new Vector2(0.75f,0.48f), HeightFrac=0.66f, Crop=new Rect(0f,0f,1f,1f),          Rotation=3,  From=SlideDir.Right },

                // 叢集 3：斷裂
                new ComicPanel{ Image="Story_08", Cluster=3, Pos=new Vector2(0.50f,0.50f), HeightFrac=0.86f, Crop=new Rect(0f,0.10f,1f,0.90f),   Rotation=-3, From=SlideDir.Left, Shake=true },

                // 叢集 4：墜入深淵（全篇幅、自動每 1.1 秒換一張）
                new ComicPanel{ Image="Story_09", Cluster=4, Fullscreen=true, HoldSeconds=1.1f },
                new ComicPanel{ Image="Story_10", Cluster=4, Fullscreen=true, HoldSeconds=1.1f },
                new ComicPanel{ Image="Story_11", Cluster=4, Fullscreen=true, HoldSeconds=1.1f },
            };
        }
    }
}
