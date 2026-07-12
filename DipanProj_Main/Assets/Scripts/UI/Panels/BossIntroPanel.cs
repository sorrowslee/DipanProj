using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Drama;

namespace Dipan.UI
{
    /// <summary>
    /// Boss 開戰資訊表演面板（bossIntro 鏈動作，見 TriggerChain.ExecuteBossIntro）。
    ///
    /// 表演時間軸（全程遊戲暫停、動畫一律 unscaled 時間，同 EyeOpenController 慣例）：
    ///   t=0                          壓黑底版＋血色暈影隨面板淡入；上下電影黑邊滑入；螢幕中央播「警告」特效
    ///   t=WarnSeconds                警告消失
    ///   t=WarnSeconds+SlideGap       boss 頭像（Talk 立繪）從左緣、**空白**姓名牌匾從右緣滑入（ease-out）
    ///   到位+NameDelay 之後          boss 名字在牌匾上浮現：扭曲抖動的半透明字 → 漸漸復原＋淡入（NameFadeSeconds）
    ///   名字成形後停 HoldSeconds     整體淡出關閉 → 觸發鏈接 next
    /// 警告與頭像/名牌是接力不重疊：警告先獨占畫面、消失後才進場。
    ///
    /// **不可跳過**：刻意讓玩家完整看完開戰資訊才開打（沒有任何按鍵/點擊捷徑）。
    ///
    /// 資料來源：MonsterData.csv 的 DisplayName / PortraitPath（trigger 只填 monsterId）。
    /// 頭像走 Talk 立繪同一條 catalog 管線（DramaTalkDatabase.ResolvePortrait），零新載圖程式；
    /// 警告序列幀直接借 VfxManager 已載好的 VfxData.AnimationSprites，在 UI 端逐格播
    /// （不能用 VfxManager.Spawn 在世界端播：世界端吃 scaled time，遊戲暫停就凍住）。
    /// 姓名牌匾用專屬圖 Resources/UI/BossIntroPanel/BossIntroPanelNameBG（PlateSpritePath 可換）。
    ///
    /// 壓迫感配件（每項都可關）：
    ///   電影黑邊  LetterboxHeight（上下黑邊條滑入；0=無）
    ///   血色暈影  VignetteAlpha（邊緣暗紅暈影、Perlin 呼吸脈動；0=無）——程序生成漸層貼圖、零素材
    ///   名字扭曲  NameWarpAmount（浮現期間文字頂點 Perlin 扭曲抖動、隨浮現進度復原；0=純淡入）——NameWarpEffect(BaseMeshEffect)、零素材
    ///
    /// 調表演：下面的節奏/版面欄位都是 public——Play 模式中在 Hierarchy 選
    /// [UIManager] → Layer_Overlay → BossIntroPanel（第一次播過後才存在）即可在 Inspector 即時調，
    /// 重新觸發 bossIntro 立刻套用（版面每次 Begin 重算）。⚠️ 面板是執行期程式生成，
    /// Play 模式調的值退出後不會保存——調到滿意後把數值回填本檔的預設值。
    /// </summary>
    public class BossIntroPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Overlay;   // 蓋在 HUD/視窗之上；Overlay 不入堆疊、ESC 不會誤關
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool CloseOnEscape => false;
        public override float FadeDuration => CloseFadeSeconds;

        // ───────── 表演節奏（秒，unscaled）─────────
        [Header("表演節奏（秒，unscaled）")]
        [Tooltip("警告特效顯示多久（時間到消失，之後頭像/名牌才滑入）。id 14 一輪約 2.13 秒")]
        public float WarnSeconds = 2.2f;
        [Tooltip("警告特效最多播幾輪；0 或負值 = 循環播滿 WarnSeconds")]
        public int WarnLoops = 0;
        [Tooltip("警告消失後停多久，頭像/名牌才開始滑入")]
        public float SlideGap = 0.2f;
        [Tooltip("頭像/名牌滑入時長")]
        public float SlideDuration = 0.8f;
        [Tooltip("全部到位後停留多久才收")]
        public float HoldSeconds = 2.5f;
        [Tooltip("收尾淡出秒數（開場淡入也用這個值）")]
        public float CloseFadeSeconds = 0.35f;

        // ───────── 名字浮現 ─────────
        [Header("名字浮現（牌匾到位後：扭曲抖動的半透明字 → 漸漸復原＋淡入）")]
        [Tooltip("名字浮現時長（秒；0=名牌到位即直接顯示、無效果）")]
        public float NameFadeSeconds = 2f;
        [Tooltip("名牌到位後停多久開始浮現名字")]
        public float NameDelay = 0.1f;
        [Tooltip("起始扭曲強度（參考解析度像素；隨浮現進度歸零。0=不扭曲、只剩淡入）")]
        public float NameWarpAmount = 30f;
        [Tooltip("扭曲抖動速度（Perlin 時間倍率）")]
        public float NameWarpSpeed = 1f;

        // ───────── 電影黑邊 ─────────
        [Header("電影黑邊（letterbox）")]
        [Tooltip("上下黑邊條高度（參考解析度像素；0=不顯示）")]
        public float LetterboxHeight = 110f;
        [Tooltip("黑邊條滑入時長（秒，從表演開始）")]
        public float LetterboxSlideSeconds = 0.4f;

        // ───────── 血色暈影 ─────────
        [Header("血色暈影（邊緣暗紅、呼吸脈動）")]
        [Tooltip("暈影最大不透明度（0=不顯示）")]
        public float VignetteAlpha = 0.5f;
        [Tooltip("暈影顏色（暗紅）")]
        public Color VignetteColor = new Color(0.45f, 0f, 0.02f);
        [Tooltip("呼吸脈動速度（Perlin）")]
        public float VignettePulseSpeed = 0.7f;
        [Tooltip("脈動幅度（0~1：alpha 在 最大值×(1-幅度) ~ 最大值 之間呼吸）")]
        public float VignettePulseAmount = 0.4f;

        // ───────── 版面（CanvasScaler 參考解析度 1920×1080 下的尺寸；每次表演開始時重算，Inspector 調完重觸發即生效）─────────
        [Header("版面（1920×1080 參考解析度）")]
        [Tooltip("壓黑底版不透明度（0~1；0=不顯示。與其他模態 UI 同款的半透明黑，隨面板一起淡入淡出）")]
        public float DimAlpha = 0.6f;
        [Tooltip("警告特效顯示高度（寬依原圖比例）")]
        public float WarnHeight = 520f;
        [Tooltip("警告中心相對畫面中心的垂直位移（+上）")]
        public float WarnY = 60f;
        [Tooltip("頭像立繪顯示高度（寬依原圖比例）")]
        public float PortraitHeight = 780f;
        [Tooltip("立繪距畫面左緣")]
        public float PortraitMargin = 170f;
        [Tooltip("立繪中心相對畫面垂直中線的位移（+上）")]
        public float PortraitY = -30f;
        [Tooltip("姓名牌匾圖（Resources 路徑，不含副檔名）")]
        public string PlateSpritePath = "UI/BossIntroPanel/BossIntroPanelNameBG";
        [Tooltip("姓名牌匾顯示寬（原圖 1000×250）")]
        public float PlateW = 720f;
        [Tooltip("姓名牌匾顯示高")]
        public float PlateH = 180f;
        [Tooltip("牌匾距畫面右緣")]
        public float PlateMargin = 150f;
        [Tooltip("牌匾中心相對畫面垂直中線的位移（+上）")]
        public float PlateY = 0f;
        [Tooltip("名字在牌匾上的區域（牌匾相對錨點：x,y=左下角、w,h=寬高，0~1）")]
        public Rect NameArea = new Rect(0.14f, 0.18f, 0.72f, 0.64f);
        [Tooltip("顯示名字級")]
        public int NameFontSize = 108;   // 72 的 1.5 倍（2026-07-12 依作者要求放大）
        [Tooltip("姓名專用字型（Resources 路徑，不含副檔名；留空＝用全 UI 預設字型）")]
        public string NameFontPath = "Fonts/Bakudai/Bakudai-Bold";

        const int DefaultWarnVfx = 14;   // 預設警告特效（VfxTable id）
        static readonly Color NameColor = new Color(1f, 0.86f, 0.5f);   // 與 TalkPanel 姓名同色

        Image _dim, _vignette, _warn, _portrait, _plate, _barTop, _barBottom;
        RectTransform _content;               // 警告/頭像/名牌的容器
        Text _name;
        NameWarpEffect _warp;                 // 名字扭曲抖動（BaseMeshEffect，浮現期間逐幀重建文字頂點）
        Sprite _vignetteSprite;               // 程序生成的暈影漸層（instance 欄位、不用 static：避免關 Domain Reload 後第二次 Play 拿到殭屍貼圖，見 PROBLEMS I 系）

        // 本次表演狀態
        Sprite[] _warnFrames;
        float _warnFps = 15f;
        float _t;                             // 開演至今（unscaled 秒）
        float _slideStart;                    // 頭像/名牌開始滑入時刻（= WarnSeconds + SlideGap，無警告特效時 = SlideGap）
        float _nameStart;                     // 名字開始浮現時刻（= 牌匾到位 + NameDelay）
        float _endTime;                       // 自動收尾時刻
        bool _running;
        System.Action _onFinished;

        // 滑入的起點/終點（anchoredPosition.x）
        float _portraitFromX, _portraitToX, _plateFromX, _plateToX;

        /// <summary>播放 boss 開戰資訊。warnVfxId ≤ 0 = 用預設（VfxTable 14）。onFinished 在面板關閉後（延一幀）呼叫。</summary>
        public static void Show(int monsterId, int warnVfxId, System.Action onFinished)
        {
            if (UIManager.Instance == null)
            {
                Debug.LogWarning("[BossIntroPanel] 無 UIManager（單場景測試？），略過表演直接完成。");
                onFinished?.Invoke();
                return;
            }
            var p = UIManager.Instance.Open<BossIntroPanel>();
            if (p == null) { onFinished?.Invoke(); return; }
            p.Begin(monsterId, warnVfxId, onFinished);
        }

        // OnBuild 只建物件骨架；所有尺寸/座標/圖在每次 Begin 重算（讓 Inspector 調完重觸發就生效）。
        // 疊層順序（先建=最底）：壓黑底版 → 血色暈影 → 內容容器（警告/頭像/名牌）→ 上下黑邊條。
        protected override void OnBuild()
        {
            // 壓黑底版：墊底、把場景壓暗聚焦表演（Overlay 層吃不到 UIManager 共用遮罩，那張只服務 Window 層，故自帶）。
            _dim = UIBuilder.SolidPanel(transform, "Dim", new Color(0f, 0f, 0f, 0.6f));
            _dim.raycastTarget = false;

            // 血色暈影：程序生成的邊緣漸層（白圖、用 color 上色），Update 內 Perlin 呼吸。
            _vignette = UIBuilder.Image(transform, "Vignette", GetVignetteSprite());
            _vignette.raycastTarget = false;
            _vignette.enabled = false;
            UIBuilder.Stretch(_vignette.rectTransform);

            // 內容容器（警告/頭像/名牌集中掛這層，方便整組加效果）。
            var contentGO = UIBuilder.Create("Content", transform);
            _content = UIBuilder.Rect(contentGO);
            UIBuilder.Stretch(_content);

            // 警告特效（中央）：UI 端逐格播 VfxTable 序列幀
            _warn = UIBuilder.Image(_content, "Warning", null);
            _warn.preserveAspect = true;
            _warn.raycastTarget = false;
            _warn.enabled = false;
            var wrt = _warn.rectTransform;
            wrt.anchorMin = wrt.anchorMax = new Vector2(0.5f, 0.5f);
            wrt.pivot = new Vector2(0.5f, 0.5f);

            // boss 頭像（左側，垂直置中；錨左緣、從畫面外滑入）
            _portrait = UIBuilder.Image(_content, "Portrait", null);
            _portrait.preserveAspect = true;
            _portrait.raycastTarget = false;
            _portrait.enabled = false;
            var prt = _portrait.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);

            // 姓名牌匾＋顯示名（右側，垂直置中；錨右緣、從畫面外滑入）
            _plate = UIBuilder.Image(_content, "NamePlate", null);
            _plate.preserveAspect = true;
            _plate.raycastTarget = false;
            _plate.enabled = false;
            var brt = _plate.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);

            _name = UIBuilder.Text(_plate.transform, "Name", "", 72, NameColor, TextAnchor.MiddleCenter);
            _name.fontStyle = FontStyle.Bold;
            _name.raycastTarget = false;
            _warp = _name.gameObject.AddComponent<NameWarpEffect>();   // 扭曲抖動頂點特效（Strength=0 時零開銷）

            // 電影黑邊條（最上層；貼上/下緣，從畫面外滑入）
            _barTop = UIBuilder.Image(transform, "BarTop", null, Color.black);
            _barTop.raycastTarget = false;
            _barTop.enabled = false;
            var trt = _barTop.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);

            _barBottom = UIBuilder.Image(transform, "BarBottom", null, Color.black);
            _barBottom.raycastTarget = false;
            _barBottom.enabled = false;
            var brt2 = _barBottom.rectTransform;
            brt2.anchorMin = new Vector2(0f, 0f); brt2.anchorMax = new Vector2(1f, 0f);
            brt2.pivot = new Vector2(0.5f, 0f);
        }

        // 解析資料、套版面、啟動表演。找不到的部分各自略過（無頭像＝只滑名牌；無警告＝直接滑入），不擋流程。
        void Begin(int monsterId, int warnVfxId, System.Action onFinished)
        {
            _onFinished = onFinished;

            // ── MonsterData：顯示名＋頭像 catalog id ──
            var spawner = Object.FindObjectOfType<MonsterSpawner>();
            var data = spawner != null ? spawner.GetData(monsterId) : null;
            if (data == null)
                Debug.LogWarning($"[BossIntroPanel] MonsterData 找不到 ID {monsterId}（或場上無 MonsterSpawner），名稱/頭像將缺席。");

            string display = data != null && !string.IsNullOrEmpty(data.DisplayName) ? data.DisplayName
                           : data != null ? data.Name : $"#{monsterId}";
            _name.text = display;
            _name.fontSize = NameFontSize;
            // 姓名專用字型（毛筆字只用在名牌，全 UI 其他文字維持預設字型）；留空或載不到退回預設。
            _name.font = string.IsNullOrEmpty(NameFontPath) ? UIBuilder.DefaultFont : UIBuilder.LoadFont(NameFontPath);

            Sprite portrait = null;
            if (data != null && !string.IsNullOrEmpty(data.PortraitPath))
                portrait = DramaTalkDatabase.Instance.ResolvePortrait(data.PortraitPath, CurrentBloodline());
            _portrait.sprite = portrait;

            // ── 壓黑底版／血色暈影／黑邊（每次套用，Inspector 調完重觸發即生效）──
            _dim.color = new Color(0f, 0f, 0f, Mathf.Clamp01(DimAlpha));
            _dim.enabled = DimAlpha > 0.001f;

            _vignette.color = new Color(VignetteColor.r, VignetteColor.g, VignetteColor.b, Mathf.Clamp01(VignetteAlpha));
            _vignette.enabled = VignetteAlpha > 0.001f;

            bool letterbox = LetterboxHeight > 0.5f;
            _barTop.enabled = letterbox;
            _barBottom.enabled = letterbox;
            if (letterbox)
            {
                _barTop.rectTransform.sizeDelta = new Vector2(0f, LetterboxHeight);
                _barBottom.rectTransform.sizeDelta = new Vector2(0f, LetterboxHeight);
                _barTop.rectTransform.anchoredPosition = new Vector2(0f, LetterboxHeight);      // 畫面外（上）
                _barBottom.rectTransform.anchoredPosition = new Vector2(0f, -LetterboxHeight);  // 畫面外（下）
            }

            // ── 姓名牌匾圖（每次 Begin 載入：Resources 有快取、Inspector 改路徑重觸發即換圖）──
            var plateSprite = UIBuilder.LoadSprite(PlateSpritePath);
            if (plateSprite == null) plateSprite = UIBuilder.LoadSprite("UI/DramaPanel/DramaPanelNameBG");   // 後備：對話姓名牌
            _plate.sprite = plateSprite;

            // ── 警告特效：借 VfxManager 已載好的序列幀（UI 端 unscaled 逐格播）──
            _warnFrames = null;
            var vm = Object.FindObjectOfType<VfxManager>();
            int warnId = warnVfxId > 0 ? warnVfxId : DefaultWarnVfx;
            var fx = vm != null ? vm.GetEffect(warnId) : null;   // 找不到 id 時 GetEffect 自己會印錯誤
            if (fx != null && fx.AnimationSprites != null && fx.AnimationSprites.Length > 0)
            {
                _warnFrames = fx.AnimationSprites;
                _warnFps = fx.AnimFPS > 0f ? fx.AnimFPS : 15f;

                _warn.sprite = _warnFrames[0];
                float aspect = _warnFrames[0].rect.height > 0f ? _warnFrames[0].rect.width / _warnFrames[0].rect.height : 1f;
                _warn.rectTransform.sizeDelta = new Vector2(WarnHeight * aspect, WarnHeight);
                _warn.rectTransform.anchoredPosition = new Vector2(0f, WarnY);
            }

            // ── 版面＋滑入起點/終點（每次重算，Inspector 調完重觸發即生效）──
            if (portrait != null)
            {
                float h = PortraitHeight;
                float aspect = portrait.rect.height > 0f ? portrait.rect.width / portrait.rect.height : 0.75f;
                float w = h * aspect;
                _portrait.rectTransform.sizeDelta = new Vector2(w, h);
                _portraitToX = PortraitMargin + w * 0.5f;   // 距左緣 PortraitMargin
                _portraitFromX = -w;                        // 完全在畫面外
                _portrait.rectTransform.anchoredPosition = new Vector2(_portraitFromX, PortraitY);
            }
            _plate.rectTransform.sizeDelta = new Vector2(PlateW, PlateH);
            _plateToX = -(PlateMargin + PlateW * 0.5f);     // 距右緣 PlateMargin（右錨、負向為畫面內）
            _plateFromX = PlateW;                            // 完全在畫面外
            _plate.rectTransform.anchoredPosition = new Vector2(_plateFromX, PlateY);

            var nrt = _name.rectTransform;                   // 名字貼齊牌匾上的 NameArea 區域
            nrt.anchorMin = new Vector2(NameArea.x, NameArea.y);
            nrt.anchorMax = new Vector2(NameArea.x + NameArea.width, NameArea.y + NameArea.height);
            nrt.offsetMin = nrt.offsetMax = Vector2.zero;

            // 名字浮現初始狀態：效果開啟＝先全透明＋滿扭曲（等牌匾到位後在 Update 邊復原邊淡入）；關閉＝直接顯示。
            bool nameFx = NameFadeSeconds > 0f;
            _name.color = nameFx ? new Color(NameColor.r, NameColor.g, NameColor.b, 0f) : NameColor;
            _warp.Strength = nameFx ? Mathf.Max(0f, NameWarpAmount) : 0f;
            _warp.Speed = NameWarpSpeed;
            _name.SetVerticesDirty();

            // ── 初始可見性與時間軸（接力：黑邊+警告 → 間隔 → 滑入(到位微震) → 停留）──
            _warn.enabled = false;       // t=0 的第一幀在 Update 開
            _portrait.enabled = false;
            _plate.enabled = false;
            _t = 0f;
            _slideStart = (_warnFrames != null ? WarnSeconds : 0f) + SlideGap;   // 無警告特效＝不留警告時段
            _nameStart = _slideStart + SlideDuration + Mathf.Max(0f, NameDelay); // 牌匾到位後才浮現名字
            _endTime = (nameFx ? _nameStart + NameFadeSeconds : _slideStart + SlideDuration) + HoldSeconds;
            _running = true;
        }

        void Update()
        {
            if (!IsOpen || !_running) return;
            _t += Time.unscaledDeltaTime;

            // 血色暈影：Perlin 呼吸脈動（alpha 在 最大×(1-幅度) ~ 最大 之間游走）
            if (_vignette.enabled)
            {
                float breathe = Mathf.PerlinNoise(Time.unscaledTime * Mathf.Max(0.01f, VignettePulseSpeed), 0.37f);
                float a = Mathf.Clamp01(VignetteAlpha) * (1f - Mathf.Clamp01(VignettePulseAmount) * breathe);
                var c = _vignette.color; c.a = a; _vignette.color = c;
            }

            // 電影黑邊：開場滑入（ease-out）
            if (_barTop.enabled && LetterboxSlideSeconds >= 0f)
            {
                float k = LetterboxSlideSeconds > 0f ? Mathf.Clamp01(_t / LetterboxSlideSeconds) : 1f;
                float e = 1f - Mathf.Pow(1f - k, 3f);
                _barTop.rectTransform.anchoredPosition = new Vector2(0f, LetterboxHeight * (1f - e));
                _barBottom.rectTransform.anchoredPosition = new Vector2(0f, -LetterboxHeight * (1f - e));
            }

            // 警告：只活在 [0, WarnSeconds)，時間到消失（頭像/名牌隨後才進場）。
            // WarnLoops>0 = 最多播 N 輪（播完提早消失）；≤0 = 循環播滿 WarnSeconds。
            if (_warnFrames != null)
            {
                int idx = Mathf.FloorToInt(_t * _warnFps);
                bool visible = _t < WarnSeconds && (WarnLoops <= 0 || idx < _warnFrames.Length * WarnLoops);
                _warn.enabled = visible;
                if (visible) _warn.sprite = _warnFrames[idx % _warnFrames.Length];
            }

            // 頭像/名牌滑入（ease-out cubic）
            if (_t >= _slideStart)
            {
                float k = SlideDuration > 0f ? Mathf.Clamp01((_t - _slideStart) / SlideDuration) : 1f;
                float e = 1f - Mathf.Pow(1f - k, 3f);
                if (_portrait.sprite != null)
                {
                    _portrait.enabled = true;
                    _portrait.rectTransform.anchoredPosition =
                        new Vector2(Mathf.LerpUnclamped(_portraitFromX, _portraitToX, e), PortraitY);
                }
                _plate.enabled = true;
                _plate.rectTransform.anchoredPosition =
                    new Vector2(Mathf.LerpUnclamped(_plateFromX, _plateToX, e), PlateY);
            }

            // 名字浮現：扭曲抖動的半透明字 → 漸漸復原＋淡入（牌匾到位＋NameDelay 之後）。
            // 抖動要「動」必須每幀重建文字頂點（SetVerticesDirty → NameWarpEffect.ModifyMesh），
            // 窗口多留 0.2s 讓最後一次以 Strength=0 重建、字形乾淨歸位。
            if (NameFadeSeconds > 0f && _t >= _nameStart && _t < _nameStart + NameFadeSeconds + 0.2f)
            {
                float k = Mathf.Clamp01((_t - _nameStart) / NameFadeSeconds);
                float e = 1f - Mathf.Pow(1f - k, 3f);   // ease-out：扭曲先快後慢地收斂
                _name.color = new Color(NameColor.r, NameColor.g, NameColor.b, e);
                _warp.Strength = (1f - e) * Mathf.Max(0f, NameWarpAmount);
                _warp.Speed = NameWarpSpeed;
                _name.SetVerticesDirty();
            }

            // 不可跳過：時間到才收尾（刻意無任何按鍵/點擊捷徑，玩家必須看完開戰資訊）
            if (_t >= _endTime) Finish();
        }

        void Finish()
        {
            if (!_running) return;
            _running = false;
            UIManager.Instance.Close(this);   // 淡出（CloseFadeSeconds）；OnClose 觸發完成回呼
        }

        protected override void OnClose()
        {
            _running = false;
            var cb = _onFinished;
            _onFinished = null;
            // 延後一幀再接鏈（同 TriggerChain.NotifyDramaClosed）：此刻面板正在 OnClose，
            // 若同步接鏈又開新模態面板會重入卡死（見 PROBLEMS D8）。
            if (cb != null) TriggerChainRunner.NextFrame(cb);
        }

        // 程序生成暈影漸層：白色、中心透明、往邊緣漸濃（矩形向量距離），供 Image.color 上色。
        // instance 欄位快取（面板每次 Play 重建，不會拿到上一輪被銷毀的貼圖）。
        Sprite GetVignetteSprite()
        {
            if (_vignetteSprite != null) return _vignetteSprite;
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                float dy = (y + 0.5f) / N * 2f - 1f;
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);                       // 0=中心、~1.414=角落
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, d));
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _vignetteSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _vignetteSprite;
        }

        /// <summary>取目前主角血統（PortraitPath 若填 Actor_&lt;情緒&gt; 時定位用；一般 boss 填 catalog id 用不到）。同 DramaTalkController。</summary>
        static string CurrentBloodline()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var pc = player != null ? player.GetComponent<PlayerController>() : null;
            string b = pc != null ? pc.Bloodline : null;
            return string.IsNullOrEmpty(b) ? "Base" : b;
        }
    }

    /// <summary>
    /// 文字扭曲抖動頂點特效（BossIntroPanel 名字浮現用）。
    /// 掛在 uGUI Text 上，ModifyMesh 時對每個字（6 頂點）做 Perlin 整字漂移＋每頂點各自扭（字形歪斜），
    /// 全部 unscaled 時間驅動、隨 <see cref="Strength"/> 線性縮放；Strength=0 直接 return＝零開銷。
    /// 抖動要「動」得由外部每幀呼叫 Text.SetVerticesDirty()（BossIntroPanel.Update 在浮現窗口內做）。
    /// </summary>
    public class NameWarpEffect : BaseMeshEffect
    {
        [System.NonSerialized] public float Strength;      // 目前扭曲強度（參考解析度像素）；0=不扭
        [System.NonSerialized] public float Speed = 16f;   // Perlin 時間倍率

        readonly List<UIVertex> _verts = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || Strength <= 0.001f) return;
            _verts.Clear();
            vh.GetUIVertexStream(_verts);   // 三角形串流：每個字 6 個頂點
            float t = Time.unscaledTime * Mathf.Max(0.01f, Speed);
            for (int i = 0; i < _verts.Count; i++)
            {
                int ch = i / 6;        // 第幾個字
                int corner = i % 6;    // 字內第幾個頂點
                var v = _verts[i];
                // 整字漂移（同字所有頂點一致）＋ 每頂點各自扭（字形被拉歪、邊緣顫動）
                float ox = (Mathf.PerlinNoise(t + ch * 1.71f, 0.31f) - 0.5f) * 2f;
                float oy = (Mathf.PerlinNoise(0.77f, t + ch * 2.39f) - 0.5f) * 2f;
                float wx = (Mathf.PerlinNoise(t * 1.31f + ch * 3.7f + corner * 0.83f, 7.7f) - 0.5f) * 2f;
                float wy = (Mathf.PerlinNoise(9.1f, t * 1.17f + ch * 2.9f + corner * 1.13f) - 0.5f) * 2f;
                v.position.x += (ox * 0.6f + wx * 0.7f) * Strength;
                v.position.y += (oy * 0.8f + wy * 0.9f) * Strength;
                _verts[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(_verts);
        }
    }
}
