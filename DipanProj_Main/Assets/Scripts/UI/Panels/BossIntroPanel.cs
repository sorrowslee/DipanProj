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
    ///   散完 +SlideGap               **角色介紹版面**（CharacterIntroPanel：立繪在左、姓名版＋說明在右）
    ///                                整張淡入（IntroFadeInSeconds），**疊在霧上面**（不鋪它自己的壓黑背景）
    ///   淡入開始+NameDelay 之後      名字浮現：扭曲抖動的半透明字 → 漸漸復原＋淡入（NameFadeSeconds）
    ///   全部到位後停 HoldSeconds     撐住這一拍（要讀說明，比舊版長）
    ///   IntroFadeOutSeconds          介紹整張淡出
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
    /// 資料來源：MonsterData.csv 的 DisplayName / PortraitPath / IntroTitle / IntroDesc（trigger 只填 monsterId）。
    /// 立繪走 Talk 立繪同一條 catalog 管線（DramaTalkDatabase.ResolvePortrait／ResolveFit），零新載圖程式。
    /// 版面整個交給 <see cref="CharacterIntroPanel"/>（2026-11-09 作者：劇情角色介紹的版面取代舊的「左頭像＋右水墨名牌」；
    /// 舊名牌圖 Resources/UI/BossIntroPanel/BossIntroPanelNameBG 已不再使用，檔案保留）。
    /// 介紹面板是另一張 ScreenSpaceOverlay Canvas（sortingOrder 5050），會蓋在本面板（黑霧、黑邊）之上；
    /// 它掛在場景根（不能掛在本面板底下：巢狀 Canvas 會失去自己的排序與縮放），所以換人／關閉／銷毀時一定要自己清掉。
    ///
    /// HUD：整段表演把 HUD 層藏起來、關閉時復原（同 ScreenFxPlayer 對過場特效的統一處理）。
    /// 開戰資訊是演出、血球浮在上面很出戲；之後若接相機後處理型的前奏特效，這件事也是必要的
    /// （後處理碰不到 ScreenSpaceOverlay 的 HUD）。
    ///
    /// 壓迫感配件（每項都可關）：
    ///   電影黑邊  LetterboxHeight（上下黑邊條滑入；0=無）
    ///   血色暈影  VignetteAlpha（邊緣暗紅暈影、Perlin 呼吸脈動；0=無）——程序生成漸層貼圖、零素材
    ///   名字扭曲  NameWarpAmount（浮現期間文字頂點 Perlin 扭曲抖動、隨浮現進度復原；0=純淡入）——NameWarpEffect(BaseMeshEffect)、零素材
    ///            （效果掛在 CharacterIntroPanel 的名字上，由本面板每幀 SetNameReveal 驅動）
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
        [Tooltip("文字散完後停多久，角色介紹才開始淡入")]
        public float SlideGap = 0.25f;
        [Tooltip("角色介紹整張淡入的秒數")]
        public float IntroFadeInSeconds = 0.6f;
        [Tooltip("全部到位（名字浮現完）後停留多久才收——要讀說明，所以比舊版（1.2）長")]
        public float HoldSeconds = 3.0f;
        [Tooltip("角色介紹整張淡出的秒數（收掉之後才輪到霧散）")]
        public float IntroFadeOutSeconds = 0.5f;
        [Tooltip("收尾淡出秒數（開場淡入也用這個值）")]
        public float CloseFadeSeconds = 0.35f;

        // ───────── 名字浮現 ─────────
        [Header("名字浮現（介紹淡入後：扭曲抖動的半透明字 → 漸漸復原＋淡入）")]
        [Tooltip("名字浮現時長（秒；0=介紹淡入時名字直接顯示、無效果）")]
        public float NameFadeSeconds = 1.2f;
        [Tooltip("介紹開始淡入後多久開始浮現名字（從淡入起點算）")]
        public float NameDelay = 0.3f;
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
        // 立繪／姓名版／說明的版面全在 CharacterIntroPanel 的常數（立繪固定在左；字型＝思源宋體）。

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

        Image _dim, _aura, _warnText, _vignette, _barTop, _barBottom;
        Material _auraMat, _smokeMat;         // 前奏兩層各自的材質實例（OnBuild 建、OnDestroy 銷毀）
        CharacterIntroPanel _intro;           // 目前這一位首領的角色介紹（另一張 Canvas、掛場景根；換人/關閉時銷毀）
        Sprite _vignetteSprite;               // 程序生成的暈影漸層（instance 欄位、不用 static：避免關 Domain Reload 後第二次 Play 拿到殭屍貼圖，見 PROBLEMS I 系）

        // 本次表演狀態
        float _t;                             // 開演至今（unscaled 秒）
        bool _fogRunning;                     // 霧還在畫面上（從開演一路到最後霧散完，中間不中斷）
        float _formStart, _formEnd;           // 文字凝聚的起訖時刻
        float _textBlowStart, _textBlowEnd;   // **文字**被吹散的起訖時刻（霧不動）
        float _slideStart, _slideEnd;         // 角色介紹淡入的起訖時刻（名稱沿用舊的 slide）
        float _nameStart;                     // 名字開始浮現時刻（= 淡入起點 + NameDelay）
        float _slideOutStart, _slideOutEnd;   // 角色介紹淡出的起訖時刻
        float _fogBlowStart, _fogBlowEnd;     // **霧**被吹散的起訖時刻（整段表演的最後一步）
        float _endTime;                       // 自動收尾時刻
        bool _running;
        bool _hidHud;                         // 這次表演有沒有藏過 HUD 層（關閉時要復原）
        System.Action _onFinished;

        // ── 多位首領（2026-11-07）──
        [Tooltip("多位首領一起開場時，前一位淡出到下一位淡入之間的間隔（秒）")]
        public float BossGapSeconds = 0.25f;
        int[] _ids;          // 這次要介紹的首領（MonsterData ID）
        int _bossIdx;        // 目前在演第幾位
        float _bossSpan;     // 每一位「滑入→撐住→滑出＋間隔」的長度

        /// <summary>播放 boss 開戰資訊。onFinished 在面板關閉後（延一幀）呼叫。</summary>
        public static void Show(int monsterId, System.Action onFinished) => Show(new[] { monsterId }, onFinished);

        /// <summary>
        /// 多位首領一起開場（2026-11-07，血狂之爭雙首領）：黑霧與「強敵現身」只演一次，
        /// 中間**依序**淡入／撐住／淡出每一位的角色介紹，最後一位淡出後黑霧才散。
        /// </summary>
        public static void Show(int[] monsterIds, System.Action onFinished)
        {
            if (UIManager.Instance == null)
            {
                Debug.LogWarning("[BossIntroPanel] 無 UIManager（單場景測試？），略過表演直接完成。");
                onFinished?.Invoke();
                return;
            }
            var p = UIManager.Instance.Open<BossIntroPanel>();
            if (p == null) { onFinished?.Invoke(); return; }
            p.Begin(monsterIds, onFinished);
        }

        // OnBuild 只建物件骨架；所有尺寸/座標/圖在每次 Begin 重算（讓 Inspector 調完重觸發就生效）。
        // 疊層順序（先建=最底）：壓黑底版 → 黑霧 → 文字 → 血色暈影 → 上下黑邊條；角色介紹是另一張 Canvas、蓋在全部之上。
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
        void Begin(int[] monsterIds, System.Action onFinished)
        {
            _onFinished = onFinished;
            _ids = (monsterIds != null && monsterIds.Length > 0) ? monsterIds : new[] { 0 };
            _bossIdx = 0;
            // 首領自己的資料（名字、頭像、頭像版面）在 ApplyBoss 套——多位首領時換人要重套一次。

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

            ApplyBoss(_ids[0]);   // 第一位的角色介紹（建好但先全透明，Update 到時間才淡入）
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
            // 順序：霧籠罩 → 文字凝聚 → 撐住 → **只有文字**散去 → 角色介紹淡入（名字扭曲浮現）→ 撐住
            //       → 角色介紹淡出 → **最後才輪到霧**散去 → 收。
            // ⚠ 霧從頭到尾都在（`_fogRunning` 一路跑到 `_fogBlowEnd`），角色介紹是疊在霧上面演的。
            _fogRunning = _auraMat != null && _smokeMat != null;
            _aura.enabled = _fogRunning;
            _warnText.enabled = _fogRunning && warnSprite != null;

            // 壓黑底版：只在霧不可用時當後備（見上面 OnBuild 那段的說明）
            bool needDim = !_fogRunning && DimAlpha > 0.001f;
            _dim.color = new Color(0f, 0f, 0f, needDim ? Mathf.Clamp01(DimAlpha) : 0f);
            _dim.enabled = needDim;

            // 缺 shader 或缺文字圖 → 前奏整段跳過（時長歸零），直接從角色介紹開始演，表演仍完整跑完。
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
            _slideEnd = _slideStart + Mathf.Max(0.01f, IntroFadeInSeconds);
            _nameStart = _slideStart + Mathf.Max(0f, NameDelay);            // 淡入途中就開始浮現名字
            float infoReady = Mathf.Max(_slideEnd, NameFadeSeconds > 0f ? _nameStart + NameFadeSeconds : _slideEnd);

            _slideOutStart = infoReady + Mathf.Max(0f, HoldSeconds);
            _slideOutEnd = _slideOutStart + Mathf.Max(0.01f, IntroFadeOutSeconds);
            // 多位首領：「淡入→撐住→淡出」整段（加一點間隔）依序重複，每位一段；霧等最後一位淡出才散
            _bossSpan = (_slideOutEnd - _slideStart) + Mathf.Max(0f, BossGapSeconds);
            _fogBlowStart = _slideOutEnd + (_ids.Length - 1) * _bossSpan;
            _fogBlowEnd = _fogBlowStart + fogBlow;
            _endTime = _fogBlowEnd;

            if (_fogRunning) PushPreludeUniforms();   // 第一幀就推一次，避免用到材質的預設值閃一格
        }

        /// <summary>
        /// 套用某一位首領：銷毀上一位的角色介紹，用 MonsterData 建這一位的（立繪在左、說明在右、不壓黑），
        /// 先全透明＋名字滿扭曲，Update 到時間才淡入。
        /// 文字欄位都走 TextRef（lang:／reveal:）：DisplayName＝名字、IntroTitle＝稱號、IntroDesc＝說明。
        /// 例：紅嫁衣的名字與稱號都填 reveal:redBridalReadLetter:…，沒看過家書兩者都顯示 ???，說明照常顯示。
        /// 找不到資料就各自缺席（無立繪＝只有文字欄），不擋流程。
        /// </summary>
        void ApplyBoss(int monsterId)
        {
            DestroyIntro();

            var spawner = Object.FindObjectOfType<MonsterSpawner>();
            var data = spawner != null ? spawner.GetData(monsterId) : null;
            if (data == null)
                Debug.LogWarning($"[BossIntroPanel] MonsterData 找不到 ID {monsterId}（或場上無 MonsterSpawner），名稱/立繪將缺席。");

            string display = data != null && !string.IsNullOrEmpty(data.DisplayName) ? TextRef.Resolve(data.DisplayName)
                           : data != null ? data.Name : $"#{monsterId}";
            string title = data != null ? TextRef.Resolve(data.IntroTitle) : "";
            string desc = data != null ? TextRef.Resolve(data.IntroDesc) : "";

            Sprite portrait = null;
            PortraitFit fit = PortraitFit.None;
            if (data != null && !string.IsNullOrEmpty(data.PortraitPath))
            {
                string bloodline = CurrentBloodline();
                portrait = DramaTalkDatabase.Instance.ResolvePortrait(data.PortraitPath, bloodline);
                fit = DramaTalkDatabase.Instance.ResolveFit(data.PortraitPath, bloodline);
            }

            // parent=null：掛場景根（見檔頭：不能掛在本面板這張 Canvas 底下）
            _intro = CharacterIntroPanel.Show(null, portrait, fit, false, title, display, desc, false);
            _intro.Alpha = 0f;
            _intro.SetNameReveal(NameFadeSeconds > 0f ? 0f : 1f, NameWarpAmount, NameWarpSpeed);
        }

        void DestroyIntro()
        {
            if (_intro != null) Destroy(_intro.gameObject);
            _intro = null;
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

            // 多位首領：算出現在演第幾位，換人時重套資料；之後的滑入／名字浮現都用「這一位自己的時間」tl。
            float tl = _t;
            if (_ids != null && _ids.Length > 1 && _t >= _slideStart && _bossSpan > 0.01f)
            {
                int idx = Mathf.Clamp(Mathf.FloorToInt((_t - _slideStart) / _bossSpan), 0, _ids.Length - 1);
                if (idx != _bossIdx) { _bossIdx = idx; ApplyBoss(_ids[idx]); }
                tl = _t - idx * _bossSpan;
            }

            // ── 角色介紹：淡入 → 撐住 → 淡出（整張 CanvasGroup）──
            if (_intro != null && tl >= _slideStart)
            {
                float alpha;
                if (tl < _slideOutStart)
                    alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((tl - _slideStart) / Mathf.Max(0.01f, IntroFadeInSeconds)));
                else
                    alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((tl - _slideOutStart) / Mathf.Max(0.01f, IntroFadeOutSeconds)));
                _intro.Alpha = alpha;
            }

            // 名字浮現：扭曲抖動的半透明字 → 漸漸復原＋淡入（淡入起點＋NameDelay 之後）。
            // 抖動要「動」必須每幀重建文字頂點（SetNameReveal 內 SetVerticesDirty → NameWarpEffect.ModifyMesh），
            // 窗口多留 0.2s 讓最後一次以 Strength=0 重建、字形乾淨歸位。
            if (_intro != null && NameFadeSeconds > 0f && tl >= _nameStart && tl < _nameStart + NameFadeSeconds + 0.2f)
            {
                float k = Mathf.Clamp01((tl - _nameStart) / NameFadeSeconds);
                float e = 1f - Mathf.Pow(1f - k, 3f);   // ease-out：扭曲先快後慢地收斂
                _intro.SetNameReveal(e, NameWarpAmount, NameWarpSpeed);
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
            DestroyIntro();   // 介紹面板掛在場景根，本面板被硬銷毀時也要帶走它
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
            DestroyIntro();   // 正常流程此時早已淡出（alpha 0），這裡只是保險

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
