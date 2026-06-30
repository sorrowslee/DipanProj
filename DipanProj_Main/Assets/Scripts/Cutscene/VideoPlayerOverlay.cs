using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Dipan.Cutscene
{
    /// <summary>
    /// 全螢幕播放一段影片（mp4）的覆蓋層（全程式建構、零 prefab）。
    ///
    /// 影片檔放在 `Assets/StreamingAssets/Video/`（原樣複製進 build，用 VideoPlayer.url 讀）。
    /// 流程：Prepare（準備好第一幀才顯示，避免黑閃）→ 播放 → 結束/跳過 → 淡黑 → 回呼（換圖在黑幕後）→ 淡出。
    /// 播放期間可暫停遊戲（Time.timeScale = 0）。給 cutscene（穿隧道之後）接邪佛動畫用。
    /// </summary>
    [DisallowMultipleComponent]
    public class VideoPlayerOverlay : MonoBehaviour
    {
        [Tooltip("影片放在 StreamingAssets 的哪個子資料夾")]
        public string SubFolder = "Video";
        [Tooltip("可按鍵/點擊跳過影片")] public bool AllowSkip = true;
        public KeyCode SkipKey = KeyCode.Escape;
        public bool ClickToSkip = true;
        [Tooltip("播放期間把 Time.timeScale 設 0（暫停遊戲）")] public bool PauseWhilePlaying = true;
        [Tooltip("結束/跳過後黑幕淡入、換圖、再淡出的時間（秒）")] public float FadeSeconds = 0.4f;

        enum St { Idle, Preparing, Playing, FadeIn, FadeOut, Done }
        St _state = St.Idle;
        bool _built;
        Canvas _canvas;
        RawImage _video;
        Image _fade;
        RenderTexture _rt;
        VideoPlayer _vp;
        Action _onDone;
        float _fadeT;
        float _prevTimeScale = 1f;

        void Awake() { Build(); }

        /// <summary>播放 StreamingAssets/&lt;SubFolder&gt;/&lt;fileName&gt; 影片；播完(或跳過)呼叫 onDone。fileName 空＝直接 onDone。</summary>
        public void Play(string fileName, Action onDone)
        {
            if (string.IsNullOrEmpty(fileName)) { onDone?.Invoke(); return; }
            if (!_built) Build();

            _onDone = onDone;
            string url = Path.Combine(Application.streamingAssetsPath, SubFolder ?? "", fileName);
            _vp.url = url;
            _vp.prepareCompleted -= OnPrepared; _vp.prepareCompleted += OnPrepared;
            _vp.loopPointReached -= OnEnd; _vp.loopPointReached += OnEnd;
            _vp.errorReceived -= OnError; _vp.errorReceived += OnError;

            SetA(_fade, 0f);
            _fadeT = 0f;
            _state = St.Preparing;
            // 立刻用全黑 BG 蓋住整個畫面（Canvas 在最上層 1300）：即使影片還在準備（首次載入較慢），
            // 也絕不會露出後面的場景。影片第一幀還沒好，先把 RawImage 關掉（RenderTexture 內容可能是雜訊/殘影）。
            if (_video) _video.enabled = false;
            if (_canvas) _canvas.enabled = true;
            if (PauseWhilePlaying) { _prevTimeScale = Time.timeScale; Time.timeScale = 0f; }
            _vp.Prepare();
            Debug.Log($"[VideoOverlay] 準備影片：{url}");
        }

        void OnPrepared(VideoPlayer vp)
        {
            vp.prepareCompleted -= OnPrepared;
            if (_state != St.Preparing) return;
            if (_video) _video.enabled = true;   // 第一幀已就緒，開始顯示影片
            _canvas.enabled = true;
            _state = St.Playing;
            vp.Play();
        }

        void OnEnd(VideoPlayer vp) { if (_state == St.Playing) _state = St.FadeIn; }

        void OnError(VideoPlayer vp, string msg)
        {
            string u = (vp != null) ? vp.url : "?";
            Debug.LogError($"[VideoOverlay] 影片錯誤（{u}）：{msg}。略過影片直接繼續。");
            Finish();
        }

        void Update()
        {
            if (_state == St.Idle || _state == St.Done || _state == St.Preparing) return;
            float dt = Time.unscaledDeltaTime;

            if (_state == St.Playing)
            {
                if (AllowSkip && (Input.GetKeyDown(SkipKey) || (ClickToSkip && Input.GetMouseButtonDown(0))))
                    _state = St.FadeIn;
                return;
            }

            if (_state == St.FadeIn)   // 黑幕淡入蓋住影片
            {
                _fadeT += dt;
                float k = Mathf.Clamp01(_fadeT / Mathf.Max(0.05f, FadeSeconds));
                SetA(_fade, k);
                if (k >= 1f) { if (_vp) _vp.Pause(); SwitchUnderBlack(); }
                return;
            }

            if (_state == St.FadeOut)  // 黑幕淡出露出新地圖
            {
                _fadeT += dt;
                float k = Mathf.Clamp01(_fadeT / Mathf.Max(0.05f, FadeSeconds));
                SetA(_fade, 1f - k);
                if (k >= 1f) Finish();
                return;
            }
        }

        // 黑幕全黑時：呼叫 onDone（換圖在黑幕後面發生），再轉淡出。
        void SwitchUnderBlack()
        {
            // 影片已暫停；把影片畫面關掉，否則接下來「黑幕淡出」會把暫停的最後一幀再露出來一下
            // （症狀：跳過後影片關掉→又閃一下→才真正關閉）。淡出時只剩黑底，乾淨過渡到載入頁/新地圖。
            if (_video) _video.enabled = false;

            var cb = _onDone; _onDone = null;
            try { cb?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            _fadeT = 0f;
            _state = St.FadeOut;
        }

        void Finish()
        {
            _state = St.Done;
            if (_vp) { _vp.Stop(); _vp.loopPointReached -= OnEnd; _vp.errorReceived -= OnError; _vp.prepareCompleted -= OnPrepared; }
            if (PauseWhilePlaying) Time.timeScale = _prevTimeScale;
            if (_canvas) _canvas.enabled = false;
            // 若是錯誤路徑直接 Finish（沒走 SwitchUnderBlack），補呼叫 onDone。
            if (_onDone != null) { var cb = _onDone; _onDone = null; try { cb(); } catch (Exception e) { Debug.LogException(e); } }
        }

        void Build()
        {
            if (_built) return;
            _rt = new RenderTexture(1920, 1080, 0) { name = "VideoRT" };

            var go = new GameObject("VideoCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1300;   // 蓋在隧道(1200)之上
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var rt = go.GetComponent<RectTransform>();

            var bg = NewImage(rt, "BG", Color.black);
            Stretch((RectTransform)bg.transform);

            var vgo = new GameObject("Video", typeof(RectTransform));
            vgo.transform.SetParent(rt, false);
            _video = vgo.AddComponent<RawImage>();
            _video.texture = _rt; _video.raycastTarget = false;
            Stretch((RectTransform)vgo.transform);

            _fade = NewImage(rt, "Fade", new Color(0, 0, 0, 0));
            Stretch((RectTransform)_fade.transform);

            _vp = gameObject.AddComponent<VideoPlayer>();
            _vp.playOnAwake = false;
            _vp.isLooping = false;
            _vp.source = VideoSource.Url;
            _vp.renderMode = VideoRenderMode.RenderTexture;
            _vp.targetTexture = _rt;
            _vp.aspectRatio = VideoAspectRatio.FitInside;
            _vp.audioOutputMode = VideoAudioOutputMode.Direct;
            _vp.skipOnDrop = true;

            _canvas.enabled = false;
            _built = true;
        }

        Image NewImage(Transform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = c; img.raycastTarget = false;
            return img;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        static void SetA(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
    }
}
