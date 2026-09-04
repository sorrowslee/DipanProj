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
    ///   t=0                          上下電影黑邊滑入；**黑霧**（UI/BossAura）湧入籠罩整個畫面、翻騰
    ///   t=TextDelay                  **「強敵現身」文字從霧裡凝聚成形**（UI/SmokeDissolve 反向跑：_Progress 1→0）
    ///   字成形後停 TextHoldSeconds   撐住這一拍
    ///   TextBlowSeconds              **只有文字被吹散**（_Progress 0→1）——⚠ **霧不動、繼續籠罩**
    ///   散完 +SlideGap               boss 頭像（Talk 立繪）從左緣、**空白**姓名牌匾從右緣滑入（ease-out），
    ///                                **疊在霧上面**
    ///   到位+NameDelay 之後          boss 名字在牌匾上浮現：扭曲抖動的半透明字 → 漸漸復原＋淡入（NameFadeSeconds）
    ///   名字成形後停 HoldSeconds     撐住這一拍
    ///   SlideOutSeconds              頭像/名牌各自往左右滑出畫面（與進場對稱）
    ///   FogBlowSeconds               **最後才輪到霧被吹散**（霧的 _Progress 0→1 ＋強度淡掉）
    ///                                → 整體淡出關閉 → 觸發鏈接 next
    ///
    /// ── 前奏的兩支 shader（都是 uGUI Image 用，掛 material 的範式同 BloodlineIntroPanel）──
    ///   `Resources/Shaders/BossAura.shader`（UI/BossAura）      黑霧；**吃兩張灰階密度圖當原料**
    ///        `Resources/UI/BossIntroPanel/BossIntroPanel_Smoke1`（厚重霧體）與 `_Smoke2`（細絮煙流）
    ///   `Resources/Shaders/SmokeDissolve.shader`（UI/SmokeDissolve） 煙霧凝聚／消散，一支做正反兩個方向
    /// 兩邊吃**同一個 _T 與同一組風的參數**（_Rise/_Turb/_EdgeSoft/_UpBias），所以是同一陣風吹走的——
    /// 要調風就兩邊一起調，只調一邊會看出兩層各走各的。
    /// ⚠ 兩支都用外部餵的 `_T` 而不是 shader 內建 `_Time`：本面板 PausesGame=true，
    ///   `_Time` 來自 `Time.timeSinceLevelLoad` ＝受 timeScale 影響，timeScale=0 時整個凍住（2026-09-04 踩過）。
    /// ⚠ 霧在的期間**壓黑底版與血色暈影都必須是 0**：霧自己就是不透明底（兼任壓黑），
    ///   再疊一層黑只會變一坨死黑，暈影也會把紅光邊緣吃掉。
    ///
    /// ── 前奏沿革（2026-09-04，同一天四版）──
    /// ① VfxTable **14「警告」**的霓虹 WARNING 序列圖 → 調性不搭，移除。
    /// ② 「電視雜訊／訊號干擾」相機後處理 → 兩版都不合格，移除（`TvNoise.shader`＋`TvNoiseController.cs` 已刪）。
    /// ③ 紅光背景用**純程序 fbm** ＋ 文字煙霧 → 作者評「很像廉價的畫面，背景一點質感都沒有」，退回。
    ///    診斷：fbm 生得出雲斑、生不出煙的絲與捲；那是形狀不是噪聲。對照 ART_DIRECTION 紀律四
    ///    「質感要像畫出來的，不是渲染出來的」——純程序漸層正是那條禁止的東西。
    /// ④ 現在這套：**作者出的兩張灰階煙霧密度圖當原料，shader 只負責行為**（翻騰／聚攏／上色／吹散）。
    /// 每一版的失敗理由都記在 readme/PROGRESS.md 那一條，**要改前奏之前先讀它**。
    /// VfxTable 14 的素材與 trigger 的 warnVfxId 參數都還在（沒清資料），但**已不再被讀取**。
    ///
    /// **不可跳過**：刻意讓玩家完整看完開戰資訊才開打（沒有任何按鍵/點擊捷徑）。
    ///
    /// 資料來源：MonsterData.csv 的 DisplayName / PortraitPath（trigger 只填 monsterId）。
    /// 頭像走 Talk 立繪同一條 catalog 管線（DramaTalkDatabase.ResolvePortrait），零新載圖程式。
    /// 姓名牌匾用專屬圖 Resources/UI/BossIntroPanel/BossIntroPanelNameBG（PlateSpritePath 可換）。
    ///
    /// HUD：整段表演把 HUD 層藏起來、關閉時復原（同 ScreenFxPlayer 對過場特效的統一處理）。
    /// 開戰資訊是演出、血球浮在上面很出戲；之後若接相機後處理型的前奏特效，這件事也是必要的
    /// （後處理碰不到 ScreenSpaceOverlay 的 HUD）。
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

        // ───────── 前奏：黑霧 ＋「強敵現身」文字（秒，unscaled）─────────
        // 演出：黑霧佈滿全螢幕翻騰 → 聚攏到畫面中央 → 文字從霧裡凝聚成形 → 撐一拍 → 文字與霧被同一陣風吹散。
        // ⚠ 這幾個是**從表演開始（t=0）算起的絕對時刻/時長**，刻意允許各段重疊
        //    （例如文字通常在霧還在聚攏時就開始浮現，銜接才不會一段一段的）。
        [Header("前奏（黑霧 ＋ 強敵現身文字）")]
        [Tooltip("霧湧入、淡到滿濃度的秒數")]
        public float FogInSeconds = 0.60f;
        [Tooltip("表演開始後多久，霧才開始往畫面中央聚攏")]
        public float GatherDelay = 0.45f;
        [Tooltip("霧聚攏的秒數")]
        public float GatherSeconds = 0.95f;
        [Tooltip("聚攏的上限（0=完全不聚攏、整片維持；1=收成中央一團）。霧要全程籠罩住頭像/名牌，所以預設只聚一點點")]
        [Range(0f, 1f)] public float GatherMax = 0.35f;
        [Tooltip("表演開始後多久，文字才開始從霧裡凝聚")]
        public float TextDelay = 1.15f;
        [Tooltip("文字凝聚成形的秒數")]
        public float TextFormSeconds = 0.80f;
        [Tooltip("文字成形後撐住多久才被吹散")]
        public float TextHoldSeconds = 0.80f;
        [Tooltip("**只有文字**被吹散的秒數（霧不動、繼續籠罩）")]
        public float TextBlowSeconds = 0.90f;
        [Tooltip("頭像/名牌收掉之後，**霧**被吹散的秒數（整段表演的最後一步）")]
        public float FogBlowSeconds = 1.00f;

        // ───────── 表演節奏（秒，unscaled）─────────
        [Header("表演節奏（秒，unscaled）")]
        [Tooltip("文字散完後停多久，頭像/名牌才開始滑入")]
        public float SlideGap = 0.25f;
        [Tooltip("頭像/名牌滑入時長")]
        public float SlideDuration = 0.8f;
        [Tooltip("全部到位後停留多久才收")]
        public float HoldSeconds = 1.20f;
        [Tooltip("頭像/名牌往左右滑出畫面的秒數（收掉之後才輪到霧散）")]
        public float SlideOutSeconds = 0.50f;
        [Tooltip("收尾淡出秒數（開場淡入也用這個值）")]
        public float CloseFadeSeconds = 0.35f;

        // ───────── 名字浮現 ─────────
        [Header("名字浮現（牌匾到位後：扭曲抖動的半透明字 → 漸漸復原＋淡入）")]
        [Tooltip("名字浮現時長（秒；0=名牌到位即直接顯示、無效果）")]
        public float NameFadeSeconds = 1.2f;
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
        // ⚠ 2026-09-04 起**預設關閉**（作者要求拿掉那層紅色底色漸層）：霧本身已經是全畫面的暗底，
        //    再疊一層暗紅暈影只會讓畫面更糊、也把黑霧染回紅的。要開回來把 VignetteAlpha 調大即可。
        [Header("血色暈影（邊緣暗紅、呼吸脈動；預設關閉）")]
        [Tooltip("暈影最大不透明度（0=不顯示）")]
        public float VignetteAlpha = 0f;
        [Tooltip("暈影顏色（暗紅）")]
        public Color VignetteColor = new Color(0.45f, 0f, 0.02f);
        [Tooltip("呼吸脈動速度（Perlin）")]
        public float VignettePulseSpeed = 0.7f;
        [Tooltip("脈動幅度（0~1：alpha 在 最大值×(1-幅度) ~ 最大值 之間呼吸）")]
        public float VignettePulseAmount = 0.4f;

        // ───────── 版面（CanvasScaler 參考解析度 1920×1080 下的尺寸；每次表演開始時重算，Inspector 調完重觸發即生效）─────────
        [Header("版面（1920×1080 參考解析度）")]
        [Tooltip("壓黑底版不透明度（0~1；0=不顯示。前奏期間強制 0，紅光散掉後才淡到這個值）")]
        public float DimAlpha = 0.6f;
        [Tooltip("前奏結束後，壓黑底版與血色暈影淡入的秒數（隨滑入同時進行）")]
        public float DimFadeInSeconds = 0.6f;
        [Tooltip("「強敵現身」文字圖（Resources 路徑，不含副檔名與語言資料夾；LocalizedArt 會自動改寫成當前語言）")]
        public string WarnTextPath = "UI/Texts/BossInfo_Warning";
        [Tooltip("文字顯示寬度（高依原圖比例；原圖 2172×724）")]
        public float WarnTextWidth = 1150f;
        [Tooltip("文字中心相對畫面中心的垂直位移（+上）")]
        public float WarnTextY = 40f;
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

        static readonly Color NameColor = new Color(1f, 0.86f, 0.5f);   // 與 TalkPanel 姓名同色

        const string AuraShaderPath  = "Shaders/BossAura";       // 黑霧
        const string SmokeShaderPath = "Shaders/SmokeDissolve";  // 煙霧凝聚／消散
        // 霧的兩張灰階密度圖（原料）。⚠ 這兩張的匯入設定有三項是必要條件，改錯會直接壞掉：
        // Wrap Mode=Repeat（要平鋪捲動）、Generate Mip Maps=開（多層縮放取樣）、sRGB=關（它們是密度不是顏色）。
        const string SmokeTexAPath = "UI/BossIntroPanel/BossIntroPanel_Smoke1";   // 厚重霧體
        const string SmokeTexBPath = "UI/BossIntroPanel/BossIntroPanel_Smoke2";   // 細絮煙流
        /// <summary>
        /// 文字圖外圈要留多少「可以飄出去的空白」（Image 顯示尺寸的放大倍率）。
        /// ⚠ **這個值同時餵給 shader 的 _Pad，兩邊必須一致**——Image 放大是為了騰出畫布，
        /// shader 內縮是為了把圖畫回原本大小，任何一邊改了另一邊沒跟上，字就會整個縮掉或爆框。
        /// 調大＝煙能飄更遠但字的有效解析度變低（同一張圖畫在更大的 quad 上）。
        /// </summary>
        const float SmokePad = 1.6f;

        Image _dim, _aura, _warnText, _vignette, _portrait, _plate, _barTop, _barBottom;
        Material _auraMat, _smokeMat;         // 前奏兩層各自的材質實例（OnBuild 建、OnDestroy 銷毀）
        RectTransform _content;               // 頭像/名牌的容器
        Text _name;
        NameWarpEffect _warp;                 // 名字扭曲抖動（BaseMeshEffect，浮現期間逐幀重建文字頂點）
        Sprite _vignetteSprite;               // 程序生成的暈影漸層（instance 欄位、不用 static：避免關 Domain Reload 後第二次 Play 拿到殭屍貼圖，見 PROBLEMS I 系）

        // 本次表演狀態
        float _t;                             // 開演至今（unscaled 秒）
        bool _fogRunning;                     // 霧還在畫面上（從開演一路到最後霧散完，中間不中斷）
        float _formStart, _formEnd;           // 文字凝聚的起訖時刻
        float _textBlowStart, _textBlowEnd;   // **文字**被吹散的起訖時刻（霧不動）
        float _slideStart, _slideEnd;         // 頭像/名牌滑入的起訖時刻
        float _nameStart;                     // 名字開始浮現時刻（= 牌匾到位 + NameDelay）
        float _slideOutStart, _slideOutEnd;   // 頭像/名牌滑出的起訖時刻
        float _fogBlowStart, _fogBlowEnd;     // **霧**被吹散的起訖時刻（整段表演的最後一步）
        float _endTime;                       // 自動收尾時刻
        bool _running;
        bool _hidHud;                         // 這次表演有沒有藏過 HUD 層（關閉時要復原）
        System.Action _onFinished;

        // 滑入的起點/終點（anchoredPosition.x）
        float _portraitFromX, _portraitToX, _plateFromX, _plateToX;

        /// <summary>播放 boss 開戰資訊。onFinished 在面板關閉後（延一幀）呼叫。</summary>
        public static void Show(int monsterId, System.Action onFinished)
        {
            if (UIManager.Instance == null)
            {
                Debug.LogWarning("[BossIntroPanel] 無 UIManager（單場景測試？），略過表演直接完成。");
                onFinished?.Invoke();
                return;
            }
            var p = UIManager.Instance.Open<BossIntroPanel>();
            if (p == null) { onFinished?.Invoke(); return; }
            p.Begin(monsterId, onFinished);
        }

        // OnBuild 只建物件骨架；所有尺寸/座標/圖在每次 Begin 重算（讓 Inspector 調完重觸發就生效）。
        // 疊層順序（先建=最底）：壓黑底版 → 血色暈影 → 內容容器（頭像/名牌）→ 上下黑邊條。
        protected override void OnBuild()
        {
            // 壓黑底版：墊底、把場景壓暗聚焦表演（Overlay 層吃不到 UIManager 共用遮罩，那張只服務 Window 層，故自帶）。
            // ⚠ 前奏期間這張是 alpha 0——紅光自己就是不透明底、兼任壓黑，再疊一層黑只會變一坨死黑。
            _dim = UIBuilder.SolidPanel(transform, "Dim", new Color(0f, 0f, 0f, 0f));
            _dim.raycastTarget = false;

            // ── 前奏第一層：黑霧（全螢幕、無 sprite，uv 就是畫面 0~1；形狀來自兩張密度圖、行為在 UI/BossAura shader）──
            _aura = UIBuilder.SolidPanel(transform, "Aura", Color.white);
            _aura.raycastTarget = false;
            _aura.enabled = false;

            // ── 前奏第二層：「強敵現身」文字（視覺全在 UI/SmokeDissolve shader）──
            // ⚠ Image 的顯示尺寸要放大 SmokePad 倍、shader 內再內縮回來：多出來的外圈是「煙可以飄出去的空白」，
            //    沒有它煙一飄出圖框就被切平（見 SmokeDissolve.shader 檔頭）。
            _warnText = UIBuilder.Image(transform, "WarnText", null);
            _warnText.preserveAspect = true;
            _warnText.raycastTarget = false;
            _warnText.enabled = false;
            var wrt = _warnText.rectTransform;
            wrt.anchorMin = wrt.anchorMax = wrt.pivot = new Vector2(0.5f, 0.5f);

            // 兩支前奏 shader 的材質實例。載不到就退化成「沒有前奏、直接演頭像」——表演仍會完整跑完。
            var auraShader = Resources.Load<Shader>(AuraShaderPath);
            var smokeShader = Resources.Load<Shader>(SmokeShaderPath);
            if (auraShader == null || smokeShader == null)
                Debug.LogWarning($"[BossIntroPanel] 找不到 Resources/{AuraShaderPath} 或 Resources/{SmokeShaderPath}，" +
                                 "前奏（黑霧＋強敵現身）停用，直接演頭像/名牌。");
            else
            {
                _auraMat = new Material(auraShader) { hideFlags = HideFlags.HideAndDontSave };
                _smokeMat = new Material(smokeShader) { hideFlags = HideFlags.HideAndDontSave };
                _aura.material = _auraMat;
                _warnText.material = _smokeMat;

                // 霧的原料：兩張灰階密度圖。**這是霧唯一的形狀來源**——沒有它們只剩一片程序漸層，
                // 那正是 2026-09-04 第一版被退回的樣子（見 BossAura.shader 檔頭）。
                var texA = LoadSmokeTex(SmokeTexAPath);
                var texB = LoadSmokeTex(SmokeTexBPath);
                if (texA == null || texB == null)
                    Debug.LogWarning($"[BossIntroPanel] 找不到霧的密度圖 Resources/{SmokeTexAPath} 或 Resources/{SmokeTexBPath}，" +
                                     "霧會變成一片死板的漸層。請確認兩張圖在 Assets/Resources/UI/BossIntroPanel/ 底下。");
                else
                {
                    _auraMat.SetTexture("_SmokeA", texA);
                    _auraMat.SetTexture("_SmokeB", texB);
                }
            }

            // 血色暈影：程序生成的邊緣漸層（白圖、用 color 上色），Update 內 Perlin 呼吸。
            _vignette = UIBuilder.Image(transform, "Vignette", GetVignetteSprite());
            _vignette.raycastTarget = false;
            _vignette.enabled = false;
            UIBuilder.Stretch(_vignette.rectTransform);

            // 內容容器（頭像/名牌集中掛這層，方便整組加效果）。
            var contentGO = UIBuilder.Create("Content", transform);
            _content = UIBuilder.Rect(contentGO);
            UIBuilder.Stretch(_content);

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

        /// <summary>
        /// 載煙霧密度圖。兩張圖匯入成 Sprite 類型，主資產仍是 Texture2D 所以直接載得到；
        /// 萬一哪天匯入設定被改動，退一步從 Sprite 取它的 texture，不要因為型別就整個前奏失效。
        /// </summary>
        static Texture2D LoadSmokeTex(string path)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null) return tex;
            var spr = Resources.Load<Sprite>(path);
            return spr != null ? spr.texture : null;
        }

        // 解析資料、套版面、啟動表演。找不到的部分各自略過（無頭像＝只滑名牌），不擋流程。
        void Begin(int monsterId, System.Action onFinished)
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
            // 霧本身就是全畫面的不透明暗底、兼任壓黑，所以霧在的時候壓黑底版一定是 0
            // （再疊一層黑只會變一坨死黑）。壓黑底版現在只剩一個用途：**霧不可用時的後備**——
            // shader 或密度圖載不到時前奏整段跳過，那時沒有它，頭像會直接疊在明亮的場景上。
            _vignette.color = new Color(VignetteColor.r, VignetteColor.g, VignetteColor.b, 0f);
            _vignette.enabled = false;

            // ── 前奏：文字圖（LocalizedArt 會把 UI/Texts/ 的路徑改寫成當前語言資料夾，中英切換零額外程式）──
            var warnSprite = UIBuilder.LoadSprite(WarnTextPath);
            _warnText.sprite = warnSprite;
            if (warnSprite == null)
                Debug.LogWarning($"[BossIntroPanel] 找不到文字圖 Resources/{WarnTextPath}——" +
                                 "請放 Assets/Resources/UI/Texts/tw/BossInfo_Warning.png（英文版放 en/ 底下、檔名要完全同名）。" +
                                 "前奏只剩紅光。");
            else
            {
                // 顯示尺寸 ×SmokePad：多出來的外圈給煙飄（shader 內會把 uv 內縮同樣倍率映射回原圖）。
                float aspect = warnSprite.rect.height > 0f ? warnSprite.rect.width / warnSprite.rect.height : 3f;
                float w = WarnTextWidth * SmokePad;
                _warnText.rectTransform.sizeDelta = new Vector2(w, w / aspect);
                _warnText.rectTransform.anchoredPosition = new Vector2(0f, WarnTextY);
            }

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

            // ── 初始可見性 ──
            _portrait.enabled = false;
            _plate.enabled = false;
            _t = 0f;
            _running = true;

            // ── HUD：整段表演藏起來（開戰資訊是演出，血球浮在上面很出戲）；OnClose 復原 ──
            _hidHud = false;
            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetLayerVisible(UILayer.HUD, false);
                _hidHud = true;
            }

            // ── 整段表演的時間軸一次算完 ──
            // 順序：霧籠罩 → 文字凝聚 → 撐住 → **只有文字**散去 → 頭像/名牌滑入 → 名字浮現 → 撐住
            //       → 頭像/名牌滑出 → **最後才輪到霧**散去 → 收。
            // ⚠ 霧從頭到尾都在（`_fogRunning` 一路跑到 `_fogBlowEnd`），頭像/名牌是疊在霧上面演的。
            _fogRunning = _auraMat != null && _smokeMat != null;
            _aura.enabled = _fogRunning;
            _warnText.enabled = _fogRunning && warnSprite != null;

            // 壓黑底版：只在霧不可用時當後備（見上面 OnBuild 那段的說明）
            bool needDim = !_fogRunning && DimAlpha > 0.001f;
            _dim.color = new Color(0f, 0f, 0f, needDim ? Mathf.Clamp01(DimAlpha) : 0f);
            _dim.enabled = needDim;

            // 缺 shader 或缺文字圖 → 前奏整段跳過（時長歸零），直接從頭像/名牌開始演，表演仍完整跑完。
            float textDelay = _fogRunning ? Mathf.Max(0f, TextDelay) : 0f;
            float textForm  = _fogRunning ? Mathf.Max(0.01f, TextFormSeconds) : 0f;
            float textHold  = _fogRunning ? Mathf.Max(0f, TextHoldSeconds) : 0f;
            float textBlow  = _fogRunning ? Mathf.Max(0.01f, TextBlowSeconds) : 0f;
            float fogBlow   = _fogRunning ? Mathf.Max(0.01f, FogBlowSeconds) : 0f;

            _formStart = textDelay;
            _formEnd = _formStart + textForm;
            _textBlowStart = _formEnd + textHold;
            _textBlowEnd = _textBlowStart + textBlow;

            _slideStart = _textBlowEnd + Mathf.Max(0f, SlideGap);
            _slideEnd = _slideStart + Mathf.Max(0.01f, SlideDuration);
            _nameStart = _slideEnd + Mathf.Max(0f, NameDelay);              // 牌匾到位後才浮現名字
            float infoReady = NameFadeSeconds > 0f ? _nameStart + NameFadeSeconds : _slideEnd;

            _slideOutStart = infoReady + Mathf.Max(0f, HoldSeconds);
            _slideOutEnd = _slideOutStart + Mathf.Max(0.01f, SlideOutSeconds);
            _fogBlowStart = _slideOutEnd;
            _fogBlowEnd = _fogBlowStart + fogBlow;
            _endTime = _fogBlowEnd;

            if (_fogRunning) PushPreludeUniforms();   // 第一幀就推一次，避免用到材質的預設值閃一格
        }

        void Update()
        {
            if (!IsOpen || !_running) return;
            _t += Time.unscaledDeltaTime;

            // 電影黑邊：開場滑入（ease-out）
            if (_barTop.enabled && LetterboxSlideSeconds >= 0f)
            {
                float k = LetterboxSlideSeconds > 0f ? Mathf.Clamp01(_t / LetterboxSlideSeconds) : 1f;
                float e = 1f - Mathf.Pow(1f - k, 3f);
                _barTop.rectTransform.anchoredPosition = new Vector2(0f, LetterboxHeight * (1f - e));
                _barBottom.rectTransform.anchoredPosition = new Vector2(0f, -LetterboxHeight * (1f - e));
            }

            // ── 霧與文字：**整段表演都在推**（霧從頭籠罩到最後才散，不像舊版跟文字一起走）──
            if (_fogRunning)
            {
                PushPreludeUniforms();
                // 文字散完就關掉：省一次全螢幕繪製，也避免萬一 shader 沒收乾淨留下殘影。
                if (_warnText.enabled && _t >= _textBlowEnd) _warnText.enabled = false;
                if (_t >= _fogBlowEnd)
                {
                    _fogRunning = false;
                    _aura.enabled = false;
                    _warnText.enabled = false;
                }
            }

            // 血色暈影：預設關閉（VignetteAlpha=0）。要開回來才會走這段。
            if (VignetteAlpha > 0.001f && _t >= _slideStart)
            {
                _vignette.enabled = true;
                float dk = DimFadeInSeconds > 0f ? Mathf.Clamp01((_t - _slideStart) / DimFadeInSeconds) : 1f;
                // Perlin 呼吸脈動（alpha 在 最大×(1-幅度) ~ 最大 之間游走）
                float breathe = Mathf.PerlinNoise(Time.unscaledTime * Mathf.Max(0.01f, VignettePulseSpeed), 0.37f);
                float va = Mathf.Clamp01(VignetteAlpha) * (1f - Mathf.Clamp01(VignettePulseAmount) * breathe) * dk;
                var vc = _vignette.color; vc.a = va; _vignette.color = vc;
            }

            // ── 頭像/名牌：滑入（ease-out cubic）→ 撐住 → 滑出（ease-in，像被抽走）──
            // 兩段共用同一組起點/終點座標，只是方向相反；滑出用 ease-in 才有「被拉走」的力道，
            // 用 ease-out 會變成「慢慢飄出去」，收尾軟掉。
            if (_t >= _slideStart)
            {
                float e;
                if (_t < _slideOutStart)
                {
                    float k = Mathf.Clamp01((_t - _slideStart) / Mathf.Max(0.01f, SlideDuration));
                    e = 1f - Mathf.Pow(1f - k, 3f);        // 進場：ease-out
                }
                else
                {
                    float k = Mathf.Clamp01((_t - _slideOutStart) / Mathf.Max(0.01f, SlideOutSeconds));
                    e = 1f - k * k * k;                    // 退場：ease-in（1→0，回到畫面外的起點）
                }

                if (_portrait.sprite != null)
                {
                    _portrait.enabled = true;
                    _portrait.rectTransform.anchoredPosition =
                        new Vector2(Mathf.LerpUnclamped(_portraitFromX, _portraitToX, e), PortraitY);
                }
                _plate.enabled = true;   // 名字是牌匾的子物件，會跟著一起滑出
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

        /// <summary>
        /// 把這一幀的狀態推進兩支 shader。**霧與文字吃同一個 _T**（同一陣風），但**吹散的時機是分開的**：
        /// 文字先散、霧繼續籠罩，等頭像/名牌都收掉了霧才散。
        /// 四條曲線：霧的強度、霧的聚攏、霧的吹散、文字的進度（凝聚 1→0、撐住 0、吹散 0→1）。
        /// 曲線一律走 SmoothStep：兩端平滑，不會有「突然開始動」或「走到底一頓」的機械感。
        /// </summary>
        void PushPreludeUniforms()
        {
            if (_auraMat == null || _smokeMat == null) return;

            // 文字的吹散（早，霧不跟）
            float textBlow = _t > _textBlowStart
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_t - _textBlowStart) / Mathf.Max(0.01f, _textBlowEnd - _textBlowStart)))
                : 0f;

            // 霧的吹散（晚，整段表演的最後一步）
            float fogBlow = _t > _fogBlowStart
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_t - _fogBlowStart) / Mathf.Max(0.01f, _fogBlowEnd - _fogBlowStart)))
                : 0f;

            // 霧的強度：湧入 → 滿（整段表演都維持）→ 最後吹散的**後半段**才開始淡掉
            // （太早淡會變成「霧先不見、才看到它散開」，收尾就沒有被吹走的感覺）
            float auraAmount = FogInSeconds > 0f ? Mathf.Clamp01(_t / FogInSeconds) : 1f;
            if (fogBlow > 0f) auraAmount *= 1f - Mathf.SmoothStep(0.45f, 1f, fogBlow);

            // 霧的聚攏：乘上 GatherMax 當上限——霧要全程籠罩住頭像/名牌，收太緊兩側就空了。
            float gather = (_t > GatherDelay
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_t - GatherDelay) / Mathf.Max(0.01f, GatherSeconds)))
                : 0f) * Mathf.Clamp01(GatherMax);

            // 文字進度：凝聚段 1→0（同一支 shader 反向跑）、撐住 0、吹散段 0→1
            float textP;
            if (_t < _formStart) textP = 1f;
            else if (_t < _formEnd)
                textP = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_t - _formStart) / Mathf.Max(0.01f, _formEnd - _formStart)));
            else textP = textBlow;

            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);

            _auraMat.SetFloat("_T", _t);
            _auraMat.SetFloat("_Amount", auraAmount);
            _auraMat.SetFloat("_Gather", gather);
            _auraMat.SetFloat("_Progress", fogBlow);
            _auraMat.SetFloat("_Aspect", aspect);

            _smokeMat.SetFloat("_T", _t);
            _smokeMat.SetFloat("_Progress", textP);
            _smokeMat.SetFloat("_Pad", SmokePad);   // 必須與 Image 的放大倍率一致，見 SmokePad 註解
        }

        void OnDestroy()
        {
            // 材質是 HideAndDontSave 的執行期實例，不會被場景卸載回收 → 面板被銷毀時自己清掉。
            if (_auraMat != null) Destroy(_auraMat);
            if (_smokeMat != null) Destroy(_smokeMat);
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
            _fogRunning = false;

            // HUD 復原（Begin 藏起來的）。放在接鏈之前：接鏈常同步開載入頁/新面板，HUD 早一步亮回來不會被看到。
            if (_hidHud && UIManager.Instance != null) UIManager.Instance.SetLayerVisible(UILayer.HUD, true);
            _hidHud = false;

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
