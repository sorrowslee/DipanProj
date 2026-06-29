using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Dipan.Intro
{
    /// <summary>
    /// 序章「持續墜落深淵」程式動畫（全程式建構、零 prefab、零美術）。
    ///
    /// 設計：放在獨立的 Intro 場景，漫畫播完之後接這段；本控制器在 Awake 自己建一整套
    /// Screen-Space Canvas + 圖層（變色漸層背景、變色光霧、速度線、角色立繪、暗角、閃光），
    /// 不依賴遊戲任何系統。固定秒數墜落，收尾時鏡頭「停止跟隨」＝角色越來越小沒入深淵，
    /// 最後淡出並（可選）載入下一個場景，把開場一條龍交棒給正式遊戲。
    ///
    /// 速度線 / 背景 / 光霧 / 暗角的貼圖全部 runtime 程序生成（比照 BlobShadow / VfxManager 的做法）。
    ///
    /// 兩種鏡頭可即時切換比較：
    ///   - Side：側面拍，垂直速度線往上刷、角色微幅翻滾浮動。
    ///   - Front：俯視往下拍，速度線從角色背後的消失點放射、角色俯衝呼吸。
    /// 測試鍵（預設 Tab）切鏡頭並重播；Skip 鍵（預設 Esc）直接收尾。
    /// </summary>
    [DisallowMultipleComponent]
    public class IntroFallController : MonoBehaviour
    {
        public enum FallView { Side, Front }

        // ───────────── Inspector 設定 ─────────────

        [Header("立繪（留空則自動從 Resources/InitialStory 載入）")]
        public Sprite FrontSprite;   // Story_ActorFall_Front
        public Sprite SideSprite;    // Story_ActorFall_Side

        // ── 分段時間（秒）──：依序播放 側面墜落 → 正面墜落 → 正面加速墜落(縮小沒入)。
        //    要改各段長度就改這三個值。鏡頭會依時間自動切換，不需要手動指定。
        [Header("分段時間（秒，可自由修改）")]
        [Tooltip("第一段：側面墜落")] public float SideSeconds = 3f;
        [Tooltip("第二段：正面墜落")] public float FrontSeconds = 3f;
        [Tooltip("第三段：正面加速墜落（角色縮小沒入＋淡出）")] public float FinaleSeconds = 2f;
        [Tooltip("起播前的延遲")] public float StartDelay = 0.2f;

        [Header("強度")]
        [Tooltip("速度線流動倍率（1＝原速，2＝兩倍快）")] public float SpeedLineFlowScale = 2f;
        [Range(0.05f, 1f)] [Tooltip("側面速度線數量（越小越少；改完要重新 Play 才生效）")] public float SideSpeedDensity = 0.3f;
        [Range(0.02f, 0.6f)] [Tooltip("側面速度線濃淡（越小越淡，只要一點點就好）")] public float SideSpeedStrength = 0.16f;
        [Range(0.2f, 5f)] [Tooltip("側面速度線捲動速度（獨立於正面；越小越慢）")] public float SideSpeedScroll = 1.5f;
        [Tooltip("整段下墜感從 1 倍加速到幾倍（收尾再額外爆衝）")] public float SpeedRampMax = 2.6f;
        [Tooltip("色彩推移：每個顏色停留幾秒")] public float ColorHoldSeconds = 2.2f;
        [Tooltip("側面段角色高度 = 螢幕高 × 此值")] [Range(0.1f, 0.9f)] public float SideCharHeightFraction = 0.29f;
        [Tooltip("正面段角色高度 = 螢幕高 × 此值")] [Range(0.1f, 0.9f)] public float FrontCharHeightFraction = 0.58f;
        [Tooltip("中央變色光霧（會在角色後方形成橢圓光暈）；先關起來看感覺")] public bool ShowColorFog = false;
        [Tooltip("邊緣壓暗暗角（寬螢幕下會變成橢圓暗框）；關閉＝畫面全亮")] public bool ShowVignette = false;

        [Header("山壁（側面：整片岩壁背景、無限往上捲動）")]
        [Tooltip("側面墜落時鋪滿整個畫面的岩壁背景、無限縱向捲動；切正面時淡出")] public bool ShowRockBackground = true;
        [Tooltip("岩壁貼圖（留空則用 Resources/InitialStory/Story_RockWall，再退回程序生成）")] public Texture2D WallTexture;
        [Tooltip("側面速度線（先關掉單看山壁；要恢復就打勾）")] public bool ShowSideSpeedLines = false;
        [Range(0.4f, 3f)] [Tooltip("岩壁縮放（越大紋理越小、畫面塊數越多）")] public float RockScale = 1f;
        [Tooltip("岩壁往上捲動的速度")] public float WallScrollSpeed = 1.0f;

        [Header("色調穿越")]
        [Tooltip("側面墜落時的正常色調（現實感，尚未穿越）")] public Color NormalTone = new Color(0.07f, 0.08f, 0.11f, 1f);
        [Tooltip("切到正面後，幾秒內由正常色調轉成詭異色（穿越時空）")] public float ColorShiftSeconds = 1.5f;

        [Header("時空扭曲（正面墜落用 shader）")]
        [Tooltip("開啟正面放射速度線的漩渦/漣漪扭曲；若畫面變洋紅代表 shader 沒編過，先關掉")] public bool EnableWarp = true;
        [Range(0f, 2f)] [Tooltip("扭曲強度")] public float WarpStrength = 1f;

        [Header("正面：旋轉卍字（神聖→墮落）")]
        [Tooltip("正面墜落時，於角色後方加一個緩緩旋轉的佛教卍字（左旋），隨穿越由金色漸變紫色")]
        public bool ShowManji = true;
        [Tooltip("旋轉速度（度/秒，正＝逆時針）")] public float ManjiRotateSpeed = 32f;
        [Tooltip("大小 = 螢幕高 × 此值")] [Range(0.2f, 1.4f)] public float ManjiSizeFraction = 0.98f;
        [Tooltip("整體不透明度上限（半透明當背景光暈、不蓋住角色）")] [Range(0f, 1f)] public float ManjiAlpha = 0.55f;
        [Tooltip("起始：金色（神聖）")] public Color ManjiGold = new Color(1f, 0.80f, 0.42f, 1f);
        [Tooltip("結束：紫色（墮落／異界）")] public Color ManjiPurple = new Color(0.56f, 0.30f, 0.78f, 1f);
        [Tooltip("自備卍字圖（毛筆草書）：拖一張進來就用它取代程式生成。建議白色/灰階＋去背 PNG，金→紫染色才會準。留空＝用程式畫的")]
        public Sprite ManjiImage;
        [Tooltip("用自備圖時是否仍套金→紫染色：開＝染色(圖須白色去背)；關＝保留圖原本顏色、只做淡入淡出")]
        public bool ManjiTintImage = true;

        [Header("收尾轉場")]
        [Tooltip("墜落結束後自動載入下一個場景")] public bool AutoLoadNextScene = true;
        [Tooltip("下一個場景名（需加進 Build Settings）")] public string NextSceneName = "MainScene";

        [Header("測試（正式上線可關）")]
        public KeyCode SkipKey = KeyCode.Escape;      // 直接收尾
        public KeyCode ReplayKey = KeyCode.R;         // 重播

        // 分段邊界（由上面三個秒數推算）
        float Total => SideSeconds + FrontSeconds + FinaleSeconds;
        float FinaleStart => SideSeconds + FrontSeconds;

        [Header("色盤（留空用內建異世界色）")]
        public Color[] Palette;

        /// <summary>墜落播完（收尾結束）時觸發。外部可接「載入遊戲 / 生玩家」。</summary>
        public event Action OnComplete;

        // ───────────── 內部狀態 ─────────────

        enum Phase { Idle, Falling, Done }
        Phase _phase = Phase.Idle;
        FallView _activeView = FallView.Side;   // 由時間軸驅動的當前鏡頭
        float _t;                 // 已播放秒數（不含 StartDelay）
        float _delayLeft;
        bool _built;

        Canvas _canvas;
        RectTransform _root;

        Image _bg;                // 變色漸層背景
        readonly List<Image> _fog = new();   // 變色光霧
        Image _character;
        RectTransform _charRt;
        Image _vignette;
        Image _flash;             // 白色閃光（穿越打點）
        Image _fade;              // 黑色收尾淡出
        Image _manji, _manjiGlow; // 旋轉卍字 + 後方光暈
        Sprite _manjiSprite;
        float _manjiAngle, _manjiA;   // 當前角度、當前不透明度（淡入用）
        bool _manjiUsingImage;        // 是否用自備圖（決定要不要染色）

        // 速度線元件（依鏡頭重建）
        class SpeedElem
        {
            public RawImage Img;
            public RectTransform Rt;
            public bool Radial;       // true＝Front 放射、false＝Side 垂直捲動
            public bool InFront;      // 是否畫在角色前方
            public float Phase, Speed, BaseAlpha, MinScale, MaxScale, TileX, TileY;
        }
        readonly List<SpeedElem> _speed = new();

        Vector2 _charBaseSize;
        float _flashPulse;        // 隨機白閃殘量
        System.Random _rnd = new System.Random(12345);

        // 程序貼圖快取
        Sprite _gradientSprite, _softCircleSprite, _vignetteSprite, _whiteSprite;
        Texture2D _streakTex, _radialTex, _rockTex;
        RawImage _rockBG;
        float _wallScroll;
        Material _warpMat;        // 正面放射速度線的時空扭曲材質
        float _weird;             // 0＝正常色調（側面）→ 1＝詭異色（正面穿越後）

        static readonly Color NeutralLine = new Color(0.90f, 0.92f, 1.0f, 1f);  // 側面速度線的正常亮色

        // ───────────── 生命週期 ─────────────

        void Awake()
        {
            if (Palette == null || Palette.Length < 2) Palette = DefaultPalette();
            BuildAll();
        }

        void OnEnable()
        {
            // 場景一載入就自動開始（漫畫之後接這個場景）。
            Play();
        }

        void Update()
        {
            HandleDebugKeys();

            if (_phase == Phase.Idle) return;

            float dt = Time.unscaledDeltaTime;

            if (_delayLeft > 0f)
            {
                _delayLeft -= dt;
                return;
            }

            if (_phase == Phase.Falling)
            {
                _t += dt;
                Tick(dt);
                if (_t >= Total) Finish();
            }
        }

        // ───────────── 公開 API ─────────────

        public void Play()
        {
            if (!_built) BuildAll();
            _t = 0f;
            _delayLeft = Mathf.Max(0f, StartDelay);
            _phase = Phase.Falling;
            _flashPulse = 0f;
            _manjiA = 0f; _manjiAngle = 0f;
            _activeView = FallView.Side;   // 從第一段（側面）開始
            ConfigureView();
            // 立即套用第 0 幀狀態，避免閃一下預設值。
            Tick(0f);
            if (_fade) SetAlpha(_fade, 0f);
        }

        public void Skip()
        {
            if (_phase != Phase.Falling) return;
            _t = Total;
            Tick(0f);
            Finish();
        }

        public void SetView(FallView v)
        {
            _activeView = v;
            ConfigureView();
        }

        // ───────────── 主時間軸 ─────────────

        void Tick(float dt)
        {
            float total = Mathf.Max(0.01f, Total);
            float p = Mathf.Clamp01(_t / total);                     // 整體進度
            float fin = (FinaleSeconds <= 0f)
                ? 0f
                : Mathf.Clamp01((_t - FinaleStart) / FinaleSeconds);  // 收尾進度（正面加速縮小）
            float finEase = fin * fin * (3f - 2f * fin);             // smoothstep

            // 依時間自動切鏡頭：前 SideSeconds 秒側面，之後正面（含收尾加速）。
            FallView want = (_t < SideSeconds) ? FallView.Side : FallView.Front;
            if (want != _activeView) SetView(want);

            // 詭異度：側面＝0（正常色調）；切正面後在 ColorShiftSeconds 內 0→1（染成穿越色）。
            _weird = (ColorShiftSeconds <= 0f)
                ? (_t >= SideSeconds ? 1f : 0f)
                : Mathf.Clamp01((_t - SideSeconds) / ColorShiftSeconds);

            // 速度感：整段慢慢加速，收尾再爆衝一下。
            float speedFactor = Mathf.Lerp(1f, SpeedRampMax, p) + finEase * 1.4f;

            UpdateBackground(dt);
            UpdateWalls(dt, speedFactor);
            UpdateFog(dt, speedFactor);
            UpdateSpeed(dt, speedFactor);
            UpdateCharacter(dt, p, finEase);
            UpdateManji(dt, finEase);
            UpdateFlashAndFade(dt, p, fin, finEase);
        }

        void UpdateManji(float dt, float finEase)
        {
            if (_manji == null) return;

            // 只在正面顯示；側面（尚未穿越）隱藏。收尾沒入時隨之淡出。
            bool front = _activeView == FallView.Front;
            float target = front ? ManjiAlpha * (1f - finEase) : 0f;
            _manjiA = Mathf.MoveTowards(_manjiA, target, dt / 0.6f);

            // 緩緩旋轉（左旋＝逆時針）。
            _manjiAngle += ManjiRotateSpeed * dt;
            _manji.transform.localEulerAngles = new Vector3(0f, 0f, _manjiAngle);

            // 金 → 紫：跟著穿越進度 _weird。（自備圖且關閉染色時＝白，保留圖原色只做淡入淡出）
            bool tint = !_manjiUsingImage || ManjiTintImage;
            Color c = tint ? Color.Lerp(ManjiGold, ManjiPurple, _weird) : Color.white;

            // 神聖呼吸：輕微脈動大小與亮度。
            float pulse = 1f + 0.04f * Mathf.Sin(_t * 1.8f);
            _manji.transform.localScale = Vector3.one * pulse;

            var mc = c; mc.a = _manjiA; _manji.color = mc;
            if (_manjiGlow)
            {
                var gc = c; gc.a = _manjiA * 0.5f; _manjiGlow.color = gc;
                _manjiGlow.transform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(_t * 1.8f + 1f));
            }
        }

        void UpdateBackground(float dt)
        {
            // 在色盤之間平滑推移：整個背景的色調隨時間變換（穿越異世界感）。
            float hold = Mathf.Max(0.2f, ColorHoldSeconds);
            float f = _t / hold;
            int n = Palette.Length;
            int i = Mathf.FloorToInt(f) % n;
            int j = (i + 1) % n;
            float frac = Mathf.SmoothStep(0f, 1f, f - Mathf.Floor(f));
            Color eerie = Color.Lerp(Palette[i], Palette[j], frac);
            // 側面維持正常暗色調，切正面後才漸染成詭異色。
            Color c = Color.Lerp(NormalTone, eerie, _weird);
            if (_bg) _bg.color = c;
        }

        void UpdateWalls(float dt, float speedFactor)
        {
            if (!ShowRockBackground || _rockBG == null) return;
            // 背景往上移＝角色往下墜（uvRect.y 遞減＝畫面內容往上捲）；×2 速度。
            _wallScroll -= dt * WallScrollSpeed * speedFactor * 1.0f;   // uv 單位/秒
            float a = 1f - _weird;            // 穿越時淡出
            bool on = a > 0.001f;
            if (_rockBG.gameObject.activeSelf != on) _rockBG.gameObject.SetActive(on);
            if (!on) return;
            var c = _rockBG.color; c.a = a; _rockBG.color = c;
            var uv = _rockBG.uvRect; uv.y = _wallScroll; _rockBG.uvRect = uv;   // 無限往上捲動
        }

        void UpdateFog(float dt, float speedFactor)
        {
            if (!ShowColorFog || _fog.Count == 0) return;
            for (int k = 0; k < _fog.Count; k++)
            {
                var rt = (RectTransform)_fog[k].transform;
                // 緩慢漂移 + 呼吸縮放。
                float ph = k * 2.1f + 1f;
                float drift = 90f + 30f * k;
                float x = Mathf.Sin(_t * 0.23f + ph) * drift;
                float y = Mathf.Cos(_t * 0.17f + ph * 1.3f) * drift - _t * 6f * speedFactor % 600f;
                rt.anchoredPosition = new Vector2(x, ((y % 700f) + 700f) % 700f - 350f);
                float s = 1f + 0.18f * Mathf.Sin(_t * 0.5f + ph);
                rt.localScale = Vector3.one * (2.6f + 0.4f * k) * s;
                // 光霧顏色取色盤的偏移，比背景亮一階。
                Color fc = Palette[(Mathf.FloorToInt(_t / Mathf.Max(0.2f, ColorHoldSeconds)) + k + 1) % Palette.Length];
                fc = Color.Lerp(fc, Color.white, 0.25f);
                fc.a = 0.10f + 0.05f * Mathf.Sin(_t * 0.6f + ph);
                _fog[k].color = fc;
            }
        }

        void UpdateSpeed(float dt, float speedFactor)
        {
            foreach (var e in _speed)
            {
                if (e.Radial)
                {
                    // Front：放射線從中心往外不斷放大流動（俯衝感）。alpha 用正弦包絡隱藏循環接縫。
                    float zt = Mathf.Repeat(_t * e.Speed * speedFactor * SpeedLineFlowScale + e.Phase, 1f);
                    float scale = Mathf.Lerp(e.MinScale, e.MaxScale, zt);
                    e.Rt.localScale = new Vector3(scale, scale, 1f);
                    e.Rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(_t * 0.3f + e.Phase * 6f) * 4f);
                    float env = Mathf.Sin(Mathf.PI * zt);   // 0→1→0
                    var c = Color.Lerp(NeutralLine, TintFromPalette(0.55f), _weird);
                    c.a = e.BaseAlpha * env;
                    e.Img.color = c;
                }
                else
                {
                    // Side：短碎條往上捲動（速度獨立 = SideSpeedScroll，不吃正面的 SpeedLineFlowScale）。
                    e.Phase -= dt * e.Speed * speedFactor;
                    var uv = e.Img.uvRect;
                    uv = new Rect(0f, e.Phase, e.TileX, e.TileY);
                    e.Img.uvRect = uv;
                    var c = Color.Lerp(NeutralLine, TintFromPalette(0.6f), _weird);
                    c.a = e.BaseAlpha;
                    e.Img.color = c;
                }
            }
        }

        void UpdateCharacter(float dt, float p, float finEase)
        {
            if (!_charRt) return;

            // 基本擺盪（墜落中的不安定感）。
            float swayRot, bobX, bobY, breathe;
            if (_activeView == FallView.Front)
            {
                swayRot = Mathf.Sin(_t * 1.1f) * 5f + Mathf.Sin(_t * 0.37f) * 3f;
                bobX = Mathf.Sin(_t * 0.8f) * 22f;
                bobY = Mathf.Sin(_t * 1.3f + 1f) * 16f;
                breathe = 1f + 0.03f * Mathf.Sin(_t * 1.7f);
            }
            else
            {
                swayRot = Mathf.Sin(_t * 1.5f) * 9f + Mathf.Sin(_t * 0.6f) * 4f;
                bobX = Mathf.Sin(_t * 0.7f) * 30f;
                bobY = Mathf.Sin(_t * 1.1f) * 26f;
                breathe = 1f;
            }

            // 收尾：鏡頭停止跟隨 → 角色越來越小、微微往上飄沒入深淵。
            float shrink = Mathf.Lerp(1f, 0.05f, finEase);
            float recedeY = Mathf.Lerp(0f, 70f, finEase);

            _charRt.anchoredPosition = new Vector2(bobX * (1f - finEase), bobY * (1f - finEase) + recedeY);
            _charRt.localScale = Vector3.one * breathe * shrink;
            _charRt.localRotation = Quaternion.Euler(0, 0, swayRot * (1f - finEase * 0.7f));
            if (_character)
            {
                var cc = _character.color;
                cc.a = Mathf.Lerp(1f, 0f, Mathf.Clamp01((finEase - 0.6f) / 0.4f)); // 最後 40% 才淡掉
                _character.color = cc;
            }
        }

        void UpdateFlashAndFade(float dt, float p, float fin, float finEase)
        {
            // 偶發白閃（穿越虛空的能量打點），強度隨進度增加。
            _flashPulse = Mathf.Max(0f, _flashPulse - dt * 2.2f);
            if (_phase == Phase.Falling && fin <= 0f)
            {
                float chance = (0.15f + 0.5f * p) * dt;
                if (_rnd.NextDouble() < chance) _flashPulse = Mathf.Lerp(0.12f, 0.4f, p);
            }
            // 收尾起手一記較強的白閃。
            float finaleFlash = (fin > 0f && fin < 0.25f) ? Mathf.Sin(fin / 0.25f * Mathf.PI) * 0.6f : 0f;
            if (_flash) SetAlpha(_flash, Mathf.Clamp01(_flashPulse + finaleFlash));

            // 黑色收尾淡出（收尾後段）。
            if (_fade)
            {
                float fadeA = Mathf.Clamp01((finEase - 0.55f) / 0.45f);
                SetAlpha(_fade, fadeA);
            }
        }

        void Finish()
        {
            if (_phase == Phase.Done) return;
            _phase = Phase.Done;
            if (_fade) SetAlpha(_fade, 1f);
            try { OnComplete?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            if (AutoLoadNextScene && !string.IsNullOrEmpty(NextSceneName))
            {
                if (Application.CanStreamedLevelBeLoaded(NextSceneName))
                    SceneManager.LoadScene(NextSceneName);
                else
                    Debug.LogWarning($"[IntroFall] 下一個場景 '{NextSceneName}' 不在 Build Settings，未載入。");
            }
        }

        void HandleDebugKeys()
        {
            if (Input.GetKeyDown(ReplayKey)) Play();
            if (Input.GetKeyDown(SkipKey)) Skip();
        }

        // ───────────── 建立圖層（一次）─────────────

        void BuildAll()
        {
            if (_built) return;
            GenerateTextures();
            BuildWarpMaterial();

            // Canvas（Screen Space Overlay，覆蓋一切；獨立 Intro 場景所以沒競爭對手，仍給高排序保險）。
            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;
            var scaler = gameObject.GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // 確保填滿任何長寬比
            if (gameObject.GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            _root = (RectTransform)transform;

            // 背景（漸層、可染色）。
            _bg = NewImage("BG", _gradientSprite);
            Stretch((RectTransform)_bg.transform);
            _bg.color = Palette[0];

            // 山壁背景（整片鋪滿、無限往上捲動）。
            if (ShowRockBackground)
                _rockBG = NewRockBG();

            // 光霧（兩團）。預設關閉（ShowColorFog=false）；開啟才建立。
            if (ShowColorFog)
            {
                for (int i = 0; i < 2; i++)
                {
                    var f = NewImage("Fog" + i, _softCircleSprite);
                    var rt = (RectTransform)f.transform;
                    Center(rt, new Vector2(900, 900));
                    _fog.Add(f);
                }
            }

            // 角色立繪。
            EnsureSprites();
            _character = NewImage("Character", SideSprite != null ? SideSprite : FrontSprite);
            _character.preserveAspect = true;
            _charRt = (RectTransform)_character.transform;
            Center(_charRt, new Vector2(600, 800));

            // 旋轉卍字（正面用）＋後方柔光暈。先建好、預設透明，UpdateManji 控制顯示/旋轉/變色。
            if (ShowManji)
            {
                float ms = 1080f * ManjiSizeFraction;
                _manjiGlow = NewImage("ManjiGlow", _softCircleSprite);
                Center((RectTransform)_manjiGlow.transform, new Vector2(ms * 1.7f, ms * 1.7f));
                _manjiGlow.color = new Color(1f, 1f, 1f, 0f);
                // 有自備圖就用圖 → 退回 Resources/InitialStory/Manji → 再退回程式生成。
                // 用 Texture2D 載入＋建 Sprite，免去 PNG 在 Resources 未被設成 Sprite 類型的雷。
                Sprite manjiSp = ManjiImage;
                if (manjiSp == null)
                {
                    var mtex = Resources.Load<Texture2D>("InitialStory/Manji");
                    if (mtex != null) manjiSp = Sprite.Create(mtex, new Rect(0, 0, mtex.width, mtex.height), new Vector2(0.5f, 0.5f), 100f);
                }
                if (manjiSp == null) manjiSp = _manjiSprite;
                _manjiUsingImage = manjiSp != _manjiSprite;
                _manji = NewImage("Manji", manjiSp);
                Center((RectTransform)_manji.transform, new Vector2(ms, ms));
                _manji.preserveAspect = true;   // 自備圖不變形
                _manji.color = new Color(1f, 1f, 1f, 0f);
            }

            // 暗角（預設關閉；寬螢幕下方形貼圖會拉成橢圓暗框）。
            if (ShowVignette)
            {
                _vignette = NewImage("Vignette", _vignetteSprite);
                Stretch((RectTransform)_vignette.transform);
                _vignette.color = new Color(0, 0, 0, 0.9f);
                _vignette.raycastTarget = false;
            }

            // 白閃。
            _flash = NewImage("Flash", _whiteSprite);
            Stretch((RectTransform)_flash.transform);
            _flash.color = new Color(1, 1, 1, 0);
            _flash.raycastTarget = false;

            // 黑色收尾淡出。
            _fade = NewImage("Fade", _whiteSprite);
            Stretch((RectTransform)_fade.transform);
            _fade.color = new Color(0, 0, 0, 0);
            _fade.raycastTarget = false;

            _built = true;
            ConfigureView();
        }

        void BuildWarpMaterial()
        {
            if (!EnableWarp) { _warpMat = null; return; }
            var sh = Resources.Load<Shader>("Shaders/IntroWarp");
            if (sh == null) { Debug.LogWarning("[IntroFall] 找不到 Resources/Shaders/IntroWarp，正面扭曲關閉、退回乾淨放射線。"); _warpMat = null; return; }
            _warpMat = new Material(sh);
            _warpMat.SetFloat("_Amp", 0.035f * WarpStrength);
            _warpMat.SetFloat("_Swirl", 0.6f * WarpStrength);
            _warpMat.SetFloat("_Freq", 5f);
            _warpMat.SetFloat("_Speed", 1.2f);
        }

        void EnsureSprites()
        {
            if (FrontSprite == null) FrontSprite = Resources.Load<Sprite>("InitialStory/Story_ActorFall_Front");
            if (SideSprite == null) SideSprite = Resources.Load<Sprite>("InitialStory/Story_ActorFall_Side");
            if (FrontSprite == null && SideSprite == null)
                Debug.LogWarning("[IntroFall] 找不到墜落立繪（Resources/InitialStory/Story_ActorFall_*），請在 Inspector 拖入或確認 Resources 內有圖。");
        }

        // ───────────── 依鏡頭設定（可重複呼叫切換）─────────────

        void ConfigureView()
        {
            if (!_built) return;
            EnsureSprites();

            // 換立繪 + 尺寸。
            Sprite s = _activeView == FallView.Front ? FrontSprite : SideSprite;
            if (s == null) s = _activeView == FallView.Front ? SideSprite : FrontSprite;
            if (s != null && _character)
            {
                _character.sprite = s;
                float frac = _activeView == FallView.Front ? FrontCharHeightFraction : SideCharHeightFraction;
                float h = 1080f * frac;
                float aspect = s.rect.width / Mathf.Max(1f, s.rect.height);
                _charBaseSize = new Vector2(h * aspect, h);
                _charRt.sizeDelta = _charBaseSize;
                _character.color = Color.white;
            }

            // 重建速度線。
            foreach (var e in _speed) if (e.Img) Destroy(e.Img.gameObject);
            _speed.Clear();

            if (_activeView == FallView.Side)
            {
                if (ShowSideSpeedLines)
                    // 一層散佈的短碎條，畫在山壁之上但角色之後（不蓋臉）。
                    AddSpeedSide("SpeedLines", radialFront: false, speed: SideSpeedScroll, alpha: SideSpeedStrength, tileX: 1f, tileY: 1f, phase: 0f);
            }
            else
            {
                // 兩道相位錯開的放射層畫在角色後方（連續俯衝），一道淡的畫在前方。
                AddSpeedRadial("DiveBack1", inFront: false, speed: 0.5f, alpha: 0.5f, phase: 0f, min: 0.25f, max: 2.6f);
                AddSpeedRadial("DiveBack2", inFront: false, speed: 0.5f, alpha: 0.5f, phase: 0.5f, min: 0.25f, max: 2.6f);
                AddSpeedRadial("DiveFront", inFront: true, speed: 0.85f, alpha: 0.22f, phase: 0.25f, min: 0.6f, max: 3.4f);
            }

            ApplyLayerOrder();
        }

        void AddSpeedSide(string name, bool radialFront, float speed, float alpha, float tileX, float tileY, float phase)
        {
            var ri = NewRawImage(name, _streakTex);
            Stretch((RectTransform)ri.transform);
            ri.uvRect = new Rect(0, 0, tileX, tileY);
            ri.raycastTarget = false;
            _speed.Add(new SpeedElem
            {
                Img = ri, Rt = (RectTransform)ri.transform, Radial = false, InFront = radialFront,
                Speed = speed, BaseAlpha = alpha, TileX = tileX, TileY = tileY, Phase = phase
            });
        }

        void AddSpeedRadial(string name, bool inFront, float speed, float alpha, float phase, float min, float max)
        {
            var ri = NewRawImage(name, _radialTex);
            var rt = (RectTransform)ri.transform;
            Center(rt, new Vector2(2400, 2400));   // 夠大，放大後覆蓋整個畫面
            ri.raycastTarget = false;
            if (EnableWarp && _warpMat != null) ri.material = _warpMat;   // 時空扭曲
            _speed.Add(new SpeedElem
            {
                Img = ri, Rt = rt, Radial = true, InFront = inFront,
                Speed = speed, BaseAlpha = alpha, Phase = phase, MinScale = min, MaxScale = max
            });
        }

        void ApplyLayerOrder()
        {
            // 由後到前：背景 → 山壁 → 光霧 → 後方速度線 → 角色 → 前方速度線 → 暗角 → 白閃 → 黑幕。
            if (_bg) _bg.transform.SetAsLastSibling();
            if (_rockBG) _rockBG.transform.SetAsLastSibling();
            foreach (var f in _fog) f.transform.SetAsLastSibling();
            if (_manjiGlow) _manjiGlow.transform.SetAsLastSibling();
            if (_manji) _manji.transform.SetAsLastSibling();
            foreach (var e in _speed) if (!e.InFront) e.Img.transform.SetAsLastSibling();
            if (_character) _character.transform.SetAsLastSibling();
            foreach (var e in _speed) if (e.InFront) e.Img.transform.SetAsLastSibling();
            if (_vignette) _vignette.transform.SetAsLastSibling();
            if (_flash) _flash.transform.SetAsLastSibling();
            if (_fade) _fade.transform.SetAsLastSibling();
        }

        // ───────────── 小工具 ─────────────

        Color TintFromPalette(float whiten)
        {
            int idx = (Mathf.FloorToInt(_t / Mathf.Max(0.2f, ColorHoldSeconds)) + 2) % Palette.Length;
            return Color.Lerp(Palette[idx], Color.white, whiten);
        }

        Image NewImage(string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        RawImage NewRawImage(string name, Texture2D tex)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var ri = go.AddComponent<RawImage>();
            ri.texture = tex;
            ri.raycastTarget = false;
            return ri;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        static void Center(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        static void SetAlpha(Graphic g, float a)
        {
            var c = g.color; c.a = a; g.color = c;
        }

        Color[] DefaultPalette() => new[]
        {
            Hex(0x1B1F3A), // 午夜藍
            Hex(0x3A1B5E), // 靛紫
            Hex(0x5E1B49), // 暗洋紅
            Hex(0x7A1F2B), // 血色
            Hex(0x123A4A), // 深青
            Hex(0x1E4A2A), // 異綠
        };

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        // ───────────── 程序貼圖生成 ─────────────

        void GenerateTextures()
        {
            _gradientSprite = SpriteOf(MakeGradient(8, 256));
            _softCircleSprite = SpriteOf(MakeSoftCircle(256));
            _manjiSprite = SpriteOf(MakeManji(512));
            _vignetteSprite = SpriteOf(MakeVignette(512));
            _whiteSprite = SpriteOf(MakeSolid(4, 4, Color.white));
            _streakTex = MakeStreak(512, 512, seed: 7, density: SideSpeedDensity);
            _radialTex = MakeRadialStreak(512, seed: 3, rays: 150);
            // 山壁貼圖：Inspector 指定 > Resources 圖 > 程序生成 後備。
            _rockTex = WallTexture != null ? WallTexture : Resources.Load<Texture2D>("InitialStory/Story_RockWall");
            if (_rockTex == null) _rockTex = MakeRockWall(256, 512, seed: 11);
            _rockTex.wrapMode = TextureWrapMode.Repeat;   // 縱向捲動需平鋪
        }

        RawImage NewRockBG()
        {
            var ri = NewRawImage("RockBG", _rockTex);
            Stretch((RectTransform)ri.transform);   // 鋪滿整個畫面
            ri.raycastTarget = false;
            ri.color = Color.white;
            // 橫向滿版、縱向依長寬比避免拉伸；可無限平鋪往上捲動。
            float aspectFix = (Screen.height / (float)Mathf.Max(1, Screen.width)) * (_rockTex.width / (float)_rockTex.height);
            ri.uvRect = new Rect(0f, 0f, RockScale, aspectFix * RockScale);
            return ri;
        }

        static Sprite SpriteOf(Texture2D t) => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);

        static Texture2D MakeSolid(int w, int h, Color c)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            t.SetPixels(px); t.Apply();
            return t;
        }

        static Texture2D MakeGradient(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);          // 0 底 → 1 頂
                // 上亮下暗，並讓中段略提亮，做出空間縱深。
                float val = Mathf.Lerp(0.32f, 1.0f, Mathf.Pow(v, 0.8f));
                for (int x = 0; x < w; x++) px[y * w + x] = new Color(val, val, val, 1f);
            }
            t.SetPixels(px); t.Apply();
            return t;
        }

        Texture2D MakeStreak(int w, int h, int seed, float density)
        {
            var rnd = new System.Random(seed);
            var a = new float[w * h];   // alpha 緩衝
            // 散佈的「短碎條」：各自隨機 x/y 位置、隨機長度（畫面高的 5~13%）、上下交錯、不規律。
            // y 用模數環繞（跨越上下邊的碎條接到另一端）→ 可無縫垂直平鋪捲動。
            int dashes = Mathf.Max(4, Mathf.RoundToInt(density * 120f));
            for (int d = 0; d < dashes; d++)
            {
                int cx = rnd.Next(w);
                int cy = rnd.Next(h);
                int len = Mathf.Max(4, Mathf.RoundToInt(h * (0.05f + (float)rnd.NextDouble() * 0.08f)));
                float inten = 0.5f + (float)rnd.NextDouble() * 0.5f;
                for (int j = 0; j < len; j++)
                {
                    int y = ((cy + j) % h + h) % h;
                    float capf = Mathf.Sin((j / (float)(len - 1)) * Mathf.PI);   // 兩端柔淡、中間最亮
                    for (int dx = -2; dx <= 2; dx++)   // ~3px 細線
                    {
                        int x = ((cx + dx) % w + w) % w;
                        float fall = Mathf.Exp(-(dx * dx) / 1.2f);
                        float v = inten * fall * capf;
                        int idx = y * w + x;
                        if (v > a[idx]) a[idx] = v;
                    }
                }
            }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1, 1, 1, Mathf.Clamp01(a[i]));
            t.SetPixels(px); t.Apply();
            return t;
        }

        Texture2D MakeRadialStreak(int size, int seed, int rays)
        {
            var rnd = new System.Random(seed);
            var rayInten = new float[rays];
            for (int i = 0; i < rays; i++) rayInten[i] = (float)rnd.NextDouble() < 0.72f ? 0.3f + (float)rnd.NextDouble() * 0.7f : 0f;

            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            float cx = (size - 1) * 0.5f, cy = (size - 1) * 0.5f;
            float maxR = size * 0.5f;
            const float halfWidthFrac = 0.30f;   // 條紋佔扇形中央比例（越小越細）
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 0f;
                    if (r <= maxR && r > 1f)
                    {
                        float ang = Mathf.Atan2(dy, dx);                  // -π..π
                        float fang = (ang + Mathf.PI) / (2f * Mathf.PI) * rays;
                        int ray = ((int)fang) % rays;
                        float frac = fang - Mathf.Floor(fang);            // 0..1 在扇形內位置
                        float line = Mathf.Clamp01(1f - Mathf.Abs(frac - 0.5f) / halfWidthFrac);
                        line = line * line;
                        float rr = r / maxR;
                        float env = Mathf.Clamp01((rr - 0.12f) / 0.2f) * Mathf.Clamp01((0.98f - rr) / 0.25f); // 近心淡、邊緣淡
                        a = rayInten[ray] * line * env;
                    }
                    px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(a));
                }
            }
            t.SetPixels(px); t.Apply();
            return t;
        }

        static Texture2D MakeSoftCircle(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            float c = (size - 1) * 0.5f, maxR = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / maxR;
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a;   // 柔邊
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        // 佛教卍字（左旋＝逆時針，神聖法輪）；草書狂野風：粗筆、毛筆提按(寬度起伏)、邊緣毛躁、
        // 腳尖收鋒(飛白)、略帶斜勢。純程式畫、白色 RGBA，顏色由 Image.color 染。
        static Texture2D MakeManji(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            float c = (n - 1) * 0.5f;
            float L = n * 0.285f;          // 臂長
            float F = n * 0.245f;          // 腳長（拉長＋收鋒）
            float baseHalf = n * 0.062f;   // 粗筆
            float shear = 0.07f;           // 斜勢（草書傾筆）

            // 段：ax,ay,bx,by,taperB（b 端＝外側腳尖，收鋒成尖）
            var seg = new float[][]
            {
                new[]{ -L, 0f,  L, 0f, 0f },
                new[]{ 0f,-L,  0f, L, 0f },
                new[]{ 0f, L, -F, L, 1f },   // 上臂 → 左
                new[]{ -L, 0f,-L,-F, 1f },   // 左臂 → 下
                new[]{ 0f,-L,  F,-L, 1f },   // 下臂 → 右
                new[]{  L, 0f,  L, F, 1f },   // 右臂 → 上
            };

            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float qx = x - c, qy = y - c;
                    qx += shear * qy;   // 斜勢

                    float best = 0f;
                    for (int i = 0; i < seg.Length; i++)
                    {
                        var s = seg[i];
                        float vx = s[2] - s[0], vy = s[3] - s[1];
                        float wx = qx - s[0], wy = qy - s[1];
                        float len2 = vx * vx + vy * vy;
                        float tt = len2 > 1e-4f ? Mathf.Clamp01((wx * vx + wy * vy) / len2) : 0f;
                        float cx = s[0] + tt * vx, cy = s[1] + tt * vy;
                        float d = Mathf.Sqrt((qx - cx) * (qx - cx) + (qy - cy) * (qy - cy));

                        // 毛筆提按：沿筆寬度起伏。
                        float w = baseHalf * (0.68f + 0.62f * Mathf.PerlinNoise(i * 5.3f + tt * 4.2f, 1.3f));
                        // 腳尖收鋒成尖。
                        if (s[4] > 0.5f) w *= Mathf.SmoothStep(0f, 0.34f, 1f - tt);
                        // 邊緣毛躁。
                        float en = (Mathf.PerlinNoise(qx * 0.05f + 9f, qy * 0.05f + 4f) - 0.5f) * baseHalf * 0.85f;
                        float a = Mathf.Clamp01((w + en - d) / (baseHalf * 0.42f));
                        // 飛白（乾筆）。
                        float dry = Mathf.PerlinNoise(qx * 0.035f - 3f, qy * 0.035f + i * 2f);
                        a *= Mathf.Clamp01(0.5f + 0.95f * dry);

                        best = Mathf.Max(best, a);
                    }
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(best));
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        static Texture2D MakeVignette(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            float c = (size - 1) * 0.5f, maxR = size * 0.5f;
            const float inner = 0.55f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / maxR;
                    float a = Mathf.Clamp01((r - inner) / (1f - inner));
                    a = a * a;   // 邊緣才暗
                    px[y * size + x] = new Color(0, 0, 0, a);
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        // 程序生成的岩壁貼圖：縱向可無縫平鋪（給捲動用），含多層噪訊、縱向裂縫、橫向層理。
        static Texture2D MakeRockWall(int w, int h, int seed)
        {
            var rnd = new System.Random(seed);
            float ox = (float)rnd.NextDouble() * 100f, oy = (float)rnd.NextDouble() * 100f;

            // 縱向裂縫：隨機幾根較暗的直紋。
            var crack = new float[w];
            int cracks = Mathf.Max(3, w / 40);
            for (int s = 0; s < cracks; s++)
            {
                int cx = rnd.Next(w);
                int hw = 1 + rnd.Next(4);
                float depth = 0.3f + (float)rnd.NextDouble() * 0.5f;
                for (int dx = -hw * 2; dx <= hw * 2; dx++)
                {
                    int x = ((cx + dx) % w + w) % w;
                    float f = Mathf.Exp(-(dx * dx) / (float)(hw * hw)) * depth;
                    if (f > crack[x]) crack[x] = f;
                }
            }

            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color[w * h];
            Color lo = new Color(0.09f, 0.085f, 0.08f);   // 陰影岩色
            Color hi = new Color(0.34f, 0.31f, 0.27f);    // 受光岩色
            for (int y = 0; y < h; y++)
            {
                float ty = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float tx = x / (float)w;
                    // 縱向可平鋪的多octave value noise（用上下混合做無縫）。
                    float n = 0f, amp = 0.6f, fr = 3f;
                    for (int o = 0; o < 3; o++)
                    {
                        float n1 = Mathf.PerlinNoise(ox + tx * fr * 2f, oy + ty * fr);
                        float n2 = Mathf.PerlinNoise(ox + tx * fr * 2f, oy + (ty - 1f) * fr);
                        n += Mathf.Lerp(n1, n2, ty) * amp;
                        amp *= 0.5f; fr *= 2f;
                    }
                    n = Mathf.Clamp01(n);
                    // 橫向層理：細微亮帶。
                    float strata = 0.5f + 0.5f * Mathf.Sin((ty * 14f + n * 1.5f) * Mathf.PI * 2f);
                    float val = Mathf.Clamp01(n * (0.75f + 0.25f * strata));
                    Color c = Color.Lerp(lo, hi, val);
                    float d = 1f - crack[x] * (0.6f + 0.4f * n);   // 裂縫壓暗
                    c.r *= d; c.g *= d; c.b *= d;
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            t.SetPixels(px); t.Apply();
            return t;
        }
    }

}
