using UnityEngine;
using UnityEngine.UI;
using Dipan.Drama;

namespace Dipan.UI
{
    /// <summary>
    /// 血統揭示面板（變身演出的最後一段，由 <c>BloodlineSystem</c> 在爬起來之後開啟）。
    ///
    /// 表演時間軸（全程遊戲暫停、一律 unscaled 時間，同 <see cref="BossIntroPanel"/> 慣例）：
    /// <code>
    /// t=0                        壓黑遮罩＋破碎框底版＋頂端標題「血統轉換」隨面板淡入，
    ///                            框內是**變身前**的血統立繪
    /// t=OldHoldSeconds           舊立繪開始斑駁剝落（_Cutoff 0→1，暗紅燒蝕邊）
    ///   ＋NewDelay               新血統立繪同步從空白浮現（_Cutoff 1→0，不同種子＝破法不同）
    ///   ＋PlateDelay             姓名底版從下方飄上來（ease-out）
    /// 牌匾到位＋NameDelay        血統名在牌匾上浮現（扭曲抖動 → 復原＋淡入，借 NameWarpEffect）
    /// 全部到齊後停 HoldSeconds   自己淡出（FadeSeconds）→ **淡完才** Close → 回呼 onFinished
    ///                            → BloodlineSystem 這時才解除暫停，玩家回到一個看得見的畫面
    /// </code>
    ///
    /// **不可跳過**：刻意沒有任何按鍵/點擊捷徑，玩家必須看完自己變成了什麼。
    ///
    /// 資料來源：立繪走 Talk 立繪同一條 catalog 管線
    /// （<c>DramaTalkDatabase.ResolvePortrait("Actor_normal", 血統資料夾)</c>），零新載圖程式；
    /// 血統名由呼叫端傳入（<c>BloodlineTable.NameOf</c>）。
    /// 兩張純美術底圖在 <c>Resources/UI/BloodlinePanel/</c>：破碎框 <c>BloodlinePanel_Bg</c>（1024×1536）、
    /// 姓名石碑 <c>BloodlinePanel_NameBg</c>（866×288）。
    ///
    /// 頂端標題「血統轉換」是**圖片型文字**，所以放在全專案共用的
    /// <c>Resources/UI/Texts/BloodlinePanel_Title</c>（866×288，與石碑同比例）——
    /// 與 <c>SelectScriptPanel</c> 的 <c>Text_Gain</c>／<c>Text_StageName_&lt;module&gt;</c> 同一個資料夾。
    /// **凡是「畫成圖的字」都要放那裡**，之後做多語系時只換那一個資料夾就好，
    /// 不必翻遍各面板自己的素材夾。（純美術的框、石碑、按鈕底不算，那些不隨語言變。）
    /// 字型則是全專案共用的毛筆字 <c>Fonts/Bakudai/Bakudai-Bold</c>。
    ///
    /// ⚠ <b>四個要知道的點</b>：
    /// <list type="number">
    /// <item><b>解除暫停不歸本面板管。</b>本面板 PausesGame=true，但真正橫跨「變身演出＋本面板」
    ///   的鎖是 BloodlineSystem 掛的具名 external hold。面板關閉到 hold 解除之間若有空窗，
    ///   玩家會有一兩幀可動；所以 onFinished 一定要被呼叫（OnClose 保證）。</item>
    /// <item><b>立繪比例不保證一致。</b>Base 是 1122×1402、三個殭屍階段是 1024×1536。
    ///   所以立繪一律「等比縮到框內的 PortraitBox 並靠下對齊」，不能直接照高度縮
    ///   （Base 會爆寬撐出框外）。</item>
    /// <item><b>兩張立繪各自一份材質。</b>_Cutoff 是材質參數，共用一份的話兩張會一起溶解。
    ///   材質用 instance 欄位、HideAndDontSave，隨面板生滅（關掉 Domain Reload 後
    ///   static 快取會拿到上一輪已銷毀的物件，見 PROBLEMS I 系）。</item>
    /// <item><b>姓名石碑是淺色的 ⇒ 字必須是深色。</b><c>NameColor</c> 預設深血紅，
    ///   **不是** BossIntroPanel 那個暖金色（那張牌匾是深色底）。石碑載不到時的後備純色底
    ///   也刻意是淺石色而非半透明黑。日後若換回深色底版，字色一定要同步改回亮色。</item>
    /// </list>
    ///
    /// 調表演：欄位都是 public——Play 模式中在 Hierarchy 選
    /// [UIManager] → Layer_Overlay → BloodlineIntroPanel（第一次播過後才存在）即可即時調，
    /// 重新喝一次藥立刻套用（版面每次 Begin 重算）。⚠️ Play 模式調的值退出後不會保存，
    /// 調到滿意要回填本檔的預設值。
    /// </summary>
    public class BloodlineIntroPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Overlay;   // 蓋在 HUD/視窗之上；Overlay 不入堆疊、ESC 不會誤關
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool CloseOnEscape => false;

        /// <summary>
        /// 開場淡入用 <see cref="FadeSeconds"/>；**收尾時回 0**。
        ///
        /// ⚠ 收尾淡出是本面板自己在 <see cref="Update"/> 裡做的，不是交給 UIPanel 的淡出。
        /// 原因：<c>UIPanel.DoClose()</c> 是「先叫 OnClose、再開始淡出」，
        /// 若把回呼掛在 OnClose 上，暫停會在畫面還有八成不透明度的時候就解除——
        /// 玩家等於在一片幾乎全黑的遮罩後面被丟回戰場，怪物照打。
        /// 所以流程改成「自己淡到全透明 → 才 Close（此時 FadeDuration=0，立刻收）→ 才回呼」。
        /// </summary>
        public override float FadeDuration => _fadingOut ? 0f : FadeSeconds;

        // ───────── 表演節奏（秒，unscaled）─────────
        [Header("表演節奏（秒，unscaled）")]
        [Tooltip("舊血統立繪先停多久，才開始斑駁剝落")]
        public float OldHoldSeconds = 1.0f;
        [Tooltip("剝落／浮現的時長")]
        public float DissolveSeconds = 0.7f;
        [Tooltip("新立繪比舊立繪晚多久開始浮現（留一瞬空白，交替才看得出來）")]
        public float NewDelay = 0.12f;
        [Tooltip("剝落開始後多久，姓名底版才從下方飄進來")]
        public float PlateDelay = 0.25f;
        [Tooltip("姓名底版飄入時長")]
        public float PlateSlideSeconds = 0.5f;
        [Tooltip("牌匾到位後停多久，血統名才開始浮現")]
        public float NameDelay = 0.1f;
        [Tooltip("血統名浮現時長（0=牌匾到位即直接顯示）")]
        public float NameFadeSeconds = 0.8f;
        [Tooltip("全部到齊後停留多久才收（讓玩家看仔細）")]
        public float HoldSeconds = 1.0f;
        [Tooltip("開場淡入／收尾淡出秒數")]
        public float FadeSeconds = 0.4f;

        // ───────── 名字浮現（同 BossIntroPanel）─────────
        [Header("名字浮現")]
        [Tooltip("起始扭曲強度（參考解析度像素；隨浮現進度歸零。0=不扭曲、只剩淡入）")]
        public float NameWarpAmount = 22f;
        [Tooltip("扭曲抖動速度（Perlin 時間倍率）")]
        public float NameWarpSpeed = 14f;

        // ───────── 斑駁溶解外觀 ─────────
        [Header("斑駁溶解（Resources/Shaders/BloodlineDissolve）")]
        [Tooltip("噪點粗細：越大破口越細碎，越小越像整片剝落")]
        public float NoiseScale = 7f;
        [Tooltip("細粒強度（0=只有大塊剝落、邊緣平滑）")]
        public float Detail = 0.45f;
        [Tooltip("燒蝕邊寬度")]
        public float EdgeWidth = 0.10f;
        [Tooltip("燒蝕邊顏色（暗紅；Linear 色彩空間下會比直覺再強一點，見 PROBLEMS E11）")]
        public Color EdgeColor = new Color(0.55f, 0.05f, 0.03f, 1f);
        [Tooltip("燒蝕邊亮度倍率")]
        public float EdgeBoost = 1.6f;

        // ───────── 版面（CanvasScaler 參考解析度 1920×1080）─────────
        [Header("版面（1920×1080 參考解析度）")]
        [Tooltip("壓黑遮罩不透明度（0~1；0=不顯示）")]
        public float DimAlpha = 0.72f;
        [Tooltip("底版圖（Resources 路徑，不含副檔名）")]
        public string FrameSpritePath = "UI/BloodlinePanel/BloodlinePanel_Bg";
        [Tooltip("底版顯示高度（寬依原圖比例；原圖 1024×1536）")]
        public float FrameHeight = 880f;
        [Tooltip("底版中心相對畫面中心的垂直位移（+上）")]
        public float FrameY = 30f;
        [Tooltip("標題「血統轉換」圖（Resources 路徑，不含副檔名）。原圖 866×288，與姓名石碑同一套比例。"
               + "⚠ 這是「圖片型文字」，一律放 UI/Texts/——之後要做多語系時只換那一個資料夾")]
        public string TitleSpritePath = "UI/Texts/BloodlinePanel_Title";
        [Tooltip("標題顯示寬（高依原圖比例＝寬 ÷ 3.007）")]
        public float TitleW = 370f;
        [Tooltip("標題中心距破碎框**頂邊**的距離（往下為正）")]
        public float TitleYFromFrameTop = 78f;
        [Tooltip("立繪可用區佔底版的比例（寬, 高）。立繪等比縮到這個框內、靠下對齊")]
        public Vector2 PortraitBox = new Vector2(0.78f, 0.70f);
        [Tooltip("立繪底邊距破碎框底邊的距離，佔框高度的比例。要比姓名底版高，否則下半身會被石碑蓋住")]
        public float PortraitBottomInset = 0.13f;
        [Tooltip("姓名底版圖（Resources 路徑，不含副檔名）。血統專用的淺色石碑，原圖 866×288")]
        public string PlateSpritePath = "UI/BloodlinePanel/BloodlinePanel_NameBg";
        [Tooltip("姓名底版顯示寬（原圖 866×288＝比例 3.007，改寬記得同步改高）")]
        public float PlateW = 360f;
        [Tooltip("姓名底版顯示高")]
        public float PlateH = 120f;
        [Tooltip("姓名底版中心距破碎框底邊的高度（+上；正值＝壓在框的下緣裡）")]
        public float PlateYFromFrameBottom = 105f;
        [Tooltip("姓名底版從多低的地方飄上來（相對到位位置往下的距離）")]
        public float PlateSlideFrom = 260f;
        [Tooltip("名字在牌匾上的區域（牌匾相對錨點：x,y=左下角、w,h=寬高，0~1）。避開左右尖刺與上下中央的裝飾")]
        public Rect NameArea = new Rect(0.20f, 0.22f, 0.60f, 0.56f);
        [Tooltip("顯示名字級")]
        public int NameFontSize = 56;
        [Tooltip("血統名顏色。⚠ 石碑是淺色的，用亮色字會看不見——預設是深血紅")]
        public Color NameColor = new Color(0.30f, 0.035f, 0.035f);
        [Tooltip("姓名專用字型（Resources 路徑，不含副檔名；留空＝用全 UI 預設字型）")]
        public string NameFontPath = "Fonts/Bakudai/Bakudai-Bold";

        const string ShaderPath = "Shaders/BloodlineDissolve";
        const string PortraitEmotion = "Actor_normal";   // 揭示用的是 normal.png

        Image _dim, _frame, _oldPortrait, _newPortrait, _plate, _title;
        Text _name;
        NameWarpEffect _warp;
        Material _oldMat, _newMat;      // instance 欄位：兩張立繪各自的 _Cutoff 不能共用
        Shader _dissolveShader;

        // 本次表演狀態
        float _t;                       // 開演至今（unscaled 秒）
        float _dissolveStart, _plateStart, _nameStart, _endTime;
        float _plateFromY, _plateToY;
        bool _running;          // 主時間軸跑動中
        bool _fadingOut;        // 自己在做收尾淡出（還沒真的 Close）
        float _fadeOutT;
        bool _released;         // 完成回呼已放行（只放一次）
        System.Action _onFinished;

        /// <summary>表演進行中（含淡出）。給熱鍵封鎖查詢；BloodlineSystem.IsPerforming 會 OR 進去。</summary>
        public static bool IsShowing { get; private set; }

        /// <summary>進 Play 時歸零（Domain Reload 已關）。由 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode() => IsShowing = false;

        /// <summary>
        /// 播放血統揭示。<paramref name="fromFolder"/>／<paramref name="toFolder"/> 是血統的
        /// SpriteFolder（＝ Talk 立繪資料夾名，例 Base / Jiangshi）。
        /// <paramref name="onFinished"/> 在面板關閉後（延一幀）呼叫，**一定會被呼叫**。
        /// </summary>
        public static void Show(string fromFolder, string toFolder, string displayName, System.Action onFinished)
        {
            if (UIManager.Instance == null)
            {
                Debug.LogWarning("[BloodlineIntroPanel] 無 UIManager（單場景測試？），略過表演直接完成。");
                onFinished?.Invoke();
                return;
            }
            var p = UIManager.Instance.Open<BloodlineIntroPanel>();
            if (p == null) { onFinished?.Invoke(); return; }
            p.Begin(fromFolder, toFolder, displayName, onFinished);
        }

        // OnBuild 只建骨架；尺寸/座標/圖/材質參數全部在每次 Begin 重算（Inspector 調完重觸發就生效）。
        // 疊層順序（先建=最底）：壓黑 → 破碎框底版 →（框的子物件）新立繪 → 舊立繪 → 姓名底版 → 名字 → 標題。
        protected override void OnBuild()
        {
            _dim = UIBuilder.SolidPanel(transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
            _dim.raycastTarget = false;

            _frame = UIBuilder.Image(transform, "Frame", null);
            _frame.raycastTarget = false;
            _frame.enabled = false;
            var frt = _frame.rectTransform;
            frt.anchorMin = frt.anchorMax = frt.pivot = new Vector2(0.5f, 0.5f);

            // 兩張立繪掛在底版底下 → 調底版位置時立繪自動跟著走。
            // 新的先建＝畫在下面：舊立繪剝落露出的破口，底下就是新血統。
            _newPortrait = MakePortrait("PortraitNew");
            _oldPortrait = MakePortrait("PortraitOld");

            _plate = UIBuilder.Image(transform, "NamePlate", null);
            _plate.raycastTarget = false;
            _plate.enabled = false;
            var prt = _plate.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);

            _name = UIBuilder.Text(_plate.transform, "Name", "", NameFontSize, NameColor, TextAnchor.MiddleCenter);
            _name.fontStyle = FontStyle.Bold;
            _name.raycastTarget = false;
            _warp = _name.gameObject.AddComponent<NameWarpEffect>();   // 借 BossIntroPanel 的頂點扭曲特效

            // 標題「血統轉換」。**最後建＝畫在最上層**，蓋過立繪的頭頂而不是被它壓住。
            // 從頭到尾都在（不做進場動畫）：它是「接下來要發生什麼」的標頭，
            // 開場就該讀得到，而不是等演到一半才出現。隨面板的 CanvasGroup 一起淡入淡出。
            _title = UIBuilder.Image(transform, "Title", null);
            _title.raycastTarget = false;
            _title.enabled = false;
            var trt = _title.rectTransform;
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);

            _dissolveShader = Resources.Load<Shader>(ShaderPath);
            if (_dissolveShader == null)
                Debug.LogWarning($"[BloodlineIntroPanel] 找不到 Resources/{ShaderPath}，" +
                                 "斑駁溶解退化成直接淡入淡出（表演仍會完整跑完）。");
            else
            {
                _oldMat = new Material(_dissolveShader) { hideFlags = HideFlags.HideAndDontSave };
                _newMat = new Material(_dissolveShader) { hideFlags = HideFlags.HideAndDontSave };
                _oldPortrait.material = _oldMat;
                _newPortrait.material = _newMat;
            }
        }

        Image MakePortrait(string name)
        {
            var img = UIBuilder.Image(_frame.transform, name, null);
            img.raycastTarget = false;
            img.enabled = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);   // 錨底版底邊中央
            rt.pivot = new Vector2(0.5f, 0f);                      // 以立繪底邊為基準往上長
            return img;
        }

        // 解析立繪、套版面、啟動表演。缺圖的部分各自略過（不擋流程，一定會走到 Finish）。
        void Begin(string fromFolder, string toFolder, string displayName, System.Action onFinished)
        {
            _onFinished = onFinished;
            _released = false;
            _fadingOut = false;
            _fadeOutT = 0f;
            IsShowing = true;

            // ── 立繪（Talk 立繪同一條 catalog 管線）──
            var db = DramaTalkDatabase.Instance;
            Sprite oldSp = db != null ? db.ResolvePortrait(PortraitEmotion, Blood(fromFolder)) : null;
            Sprite newSp = db != null ? db.ResolvePortrait(PortraitEmotion, Blood(toFolder)) : null;
            if (db == null)
                Debug.LogWarning("[BloodlineIntroPanel] 無 DramaTalkDatabase，立繪缺席（表演照跑）。");

            // ── 壓黑／底版 ──
            _dim.color = new Color(0f, 0f, 0f, Mathf.Clamp01(DimAlpha));
            _dim.enabled = DimAlpha > 0.001f;

            var frameSp = UIBuilder.LoadSprite(FrameSpritePath);
            _frame.sprite = frameSp;
            _frame.enabled = frameSp != null;
            float fh = Mathf.Max(1f, FrameHeight);
            float fw = fh * Aspect(frameSp, 0.6667f);
            _frame.rectTransform.sizeDelta = new Vector2(fw, fh);
            _frame.rectTransform.anchoredPosition = new Vector2(0f, FrameY);

            // ── 標題：貼在破碎框頂端，高度依原圖比例 ──
            // 素材在 UI/Texts/（圖片型文字的共用資料夾），不在 UI/BloodlinePanel/——見檔頭說明。
            var titleSp = UIBuilder.LoadSprite(TitleSpritePath);
            _title.sprite = titleSp;
            _title.enabled = titleSp != null;
            if (titleSp != null)
            {
                float tw = Mathf.Max(1f, TitleW);
                float th = tw / Mathf.Max(0.0001f, Aspect(titleSp, 3.007f));
                _title.rectTransform.sizeDelta = new Vector2(tw, th);
                _title.rectTransform.anchoredPosition = new Vector2(0f, FrameY + fh * 0.5f - TitleYFromFrameTop);
            }

            // ── 立繪版面：等比縮到 PortraitBox 內、靠下對齊 ──
            // ⚠ 這裡的高度上限（PortraitBox.y）與 PortraitBottomInset 是**跟標題搶空間**的：
            //    立繪頭頂 = 框高 − 底邊距離 − 立繪高，標題底邊 = TitleYFromFrameTop + 標題高的一半。
            //    前者要大於後者，不然標題會壓在角色頭上。目前算下來頭頂距框頂 150、標題底邊 140，留 10 的餘裕。
            // ⚠ 不能照高度縮：Base 是 1122×1402（比例 0.80）、殭屍三階是 1024×1536（0.667），
            //   照高度縮的話 Base 會比框還寬、直接撐出破碎框外面。
            Vector2 box = new Vector2(fw * Mathf.Clamp01(PortraitBox.x), fh * Mathf.Clamp01(PortraitBox.y));
            LayoutPortrait(_oldPortrait, oldSp, box, fh);
            LayoutPortrait(_newPortrait, newSp, box, fh);

            // ── 溶解材質參數（每次 Begin 重套，Inspector 調完重觸發即生效）──
            // 種子刻意給兩個不同值：兩張圖的破法一樣的話，交替看起來像同一張圖在原地換色。
            ApplyDissolve(_oldMat, cutoff: 0f, seed: 0f);
            ApplyDissolve(_newMat, cutoff: 1f, seed: 37.5f);
            // 沒有著色器時的退化路徑：改用整張圖的 alpha 淡入淡出。
            // 初始值要在這裡先設好——只靠 Update 的話第一幀新立繪會整張蓋在舊立繪上面閃一下。
            SetAlpha(_oldPortrait, 1f);
            if (_newMat == null) SetAlpha(_newPortrait, 0f);

            // ── 姓名底版 ──
            // 底版圖載不到也**不能**讓牌匾整個不動——名字是牌匾的子物件，牌匾停在畫面外
            // 就會變成一行血統名孤零零飄在螢幕底下。所以沒圖時退化成一塊純色底，
            // 而且飄入那段一律照跑（下面 Update 不再檢查 sprite）。
            // ⚠ 後備色刻意是**淺石色**不是半透明黑：血統名是深血紅（配淺色石碑），
            //   墊深色底的話字會整個看不見。
            var plateSp = UIBuilder.LoadSprite(PlateSpritePath);
            _plate.sprite = plateSp;
            _plate.color = plateSp != null ? Color.white : new Color(0.84f, 0.82f, 0.77f, 0.94f);
            _plate.rectTransform.sizeDelta = new Vector2(PlateW, PlateH);
            _plateToY = FrameY - fh * 0.5f + PlateYFromFrameBottom;
            _plateFromY = _plateToY - Mathf.Max(0f, PlateSlideFrom);
            _plate.rectTransform.anchoredPosition = new Vector2(0f, _plateFromY);
            _plate.enabled = false;                                   // 飄入時刻才開

            _name.text = string.IsNullOrEmpty(displayName) ? "" : displayName;
            _name.fontSize = NameFontSize;
            _name.font = string.IsNullOrEmpty(NameFontPath) ? UIBuilder.DefaultFont : UIBuilder.LoadFont(NameFontPath);
            var nrt = _name.rectTransform;
            nrt.anchorMin = new Vector2(NameArea.x, NameArea.y);
            nrt.anchorMax = new Vector2(NameArea.x + NameArea.width, NameArea.y + NameArea.height);
            nrt.offsetMin = nrt.offsetMax = Vector2.zero;

            bool nameFx = NameFadeSeconds > 0f;
            _name.color = nameFx ? new Color(NameColor.r, NameColor.g, NameColor.b, 0f) : NameColor;
            _warp.Strength = nameFx ? Mathf.Max(0f, NameWarpAmount) : 0f;
            _warp.Speed = NameWarpSpeed;
            _name.SetVerticesDirty();

            // ── 時間軸 ──
            _t = 0f;
            _dissolveStart = Mathf.Max(0f, OldHoldSeconds);
            _plateStart = _dissolveStart + Mathf.Max(0f, PlateDelay);
            _nameStart = _plateStart + Mathf.Max(0f, PlateSlideSeconds) + Mathf.Max(0f, NameDelay);
            float lastBeat = Mathf.Max(
                _dissolveStart + Mathf.Max(0f, NewDelay) + Mathf.Max(0f, DissolveSeconds),
                nameFx ? _nameStart + NameFadeSeconds : _nameStart);
            _endTime = lastBeat + Mathf.Max(0f, HoldSeconds);
            _running = true;
        }

        /// <summary>把立繪等比縮到 box 內（contain），底邊貼在底版底部往上 PortraitBottomInset。</summary>
        void LayoutPortrait(Image img, Sprite sp, Vector2 box, float frameHeight)
        {
            img.sprite = sp;
            img.enabled = sp != null;
            if (sp == null) return;

            float a = Aspect(sp, 0.75f);                              // 寬/高
            float h = box.y;
            float w = h * a;
            if (w > box.x) { w = box.x; h = w / Mathf.Max(0.0001f, a); }   // 太寬 → 改由寬度決定
            img.rectTransform.sizeDelta = new Vector2(w, h);
            img.rectTransform.anchoredPosition = new Vector2(0f, frameHeight * PortraitBottomInset);
        }

        void ApplyDissolve(Material m, float cutoff, float seed)
        {
            if (m == null) return;
            m.SetFloat("_Cutoff", Mathf.Clamp01(cutoff));
            m.SetFloat("_NoiseScale", Mathf.Max(0.1f, NoiseScale));
            m.SetFloat("_Detail", Mathf.Clamp01(Detail));
            m.SetFloat("_Seed", seed);
            m.SetFloat("_EdgeWidth", Mathf.Max(0.001f, EdgeWidth));
            m.SetColor("_EdgeColor", EdgeColor);
            m.SetFloat("_EdgeBoost", EdgeBoost);
        }

        void Update()
        {
            if (!IsOpen) return;

            // 收尾淡出：自己把 CanvasGroup 淡到全透明，淡完才 Close（見 FadeDuration 的說明）。
            if (_fadingOut)
            {
                _fadeOutT += Time.unscaledDeltaTime;
                float d = Mathf.Max(0.0001f, FadeSeconds);
                CanvasGroup.alpha = Mathf.Clamp01(1f - _fadeOutT / d);
                if (_fadeOutT >= d) CloseNow();
                return;
            }

            if (!_running) return;
            _t += Time.unscaledDeltaTime;

            // 斑駁交替：舊的 0→1 剝落，新的 1→0 浮現（晚 NewDelay 起跑、種子不同）。
            // 沒有著色器時退化成整張圖的 alpha 淡入淡出——表演節奏一模一樣，只是不斑駁。
            float kOld = Progress(_dissolveStart, DissolveSeconds);
            float kNew = Progress(_dissolveStart + NewDelay, DissolveSeconds);
            if (_oldMat != null) _oldMat.SetFloat("_Cutoff", kOld);
            else SetAlpha(_oldPortrait, 1f - kOld);
            if (_newMat != null) _newMat.SetFloat("_Cutoff", 1f - kNew);
            else SetAlpha(_newPortrait, kNew);

            // 姓名底版：從下方飄入（ease-out cubic）。不檢查 sprite——名字是它的子物件，
            // 沒圖也必須把它移到定位，否則名字會停在畫面外（見 Begin 裡的後備圖處理）。
            if (_t >= _plateStart)
            {
                _plate.enabled = true;
                float k = PlateSlideSeconds > 0f ? Mathf.Clamp01((_t - _plateStart) / PlateSlideSeconds) : 1f;
                float e = 1f - Mathf.Pow(1f - k, 3f);
                _plate.rectTransform.anchoredPosition =
                    new Vector2(0f, Mathf.LerpUnclamped(_plateFromY, _plateToY, e));
            }

            // 血統名浮現：扭曲抖動的半透明字 → 漸漸復原＋淡入。
            // 抖動要「動」必須每幀重建文字頂點；窗口多留 0.2s 讓最後一次以 Strength=0 重建、字形乾淨歸位。
            if (NameFadeSeconds > 0f && _t >= _nameStart && _t < _nameStart + NameFadeSeconds + 0.2f)
            {
                float k = Mathf.Clamp01((_t - _nameStart) / NameFadeSeconds);
                float e = 1f - Mathf.Pow(1f - k, 3f);
                _name.color = new Color(NameColor.r, NameColor.g, NameColor.b, e);
                _warp.Strength = (1f - e) * Mathf.Max(0f, NameWarpAmount);
                _warp.Speed = NameWarpSpeed;
                _name.SetVerticesDirty();
            }

            // 不可跳過：時間到才收尾（刻意無任何按鍵/點擊捷徑）
            if (_t >= _endTime) Finish();
        }

        /// <summary>某段動畫的 0~1 進度（還沒開始=0、已結束=1）。</summary>
        float Progress(float start, float duration)
        {
            if (_t < start) return 0f;
            if (duration <= 0f) return 1f;
            return Mathf.Clamp01((_t - start) / duration);
        }

        static void SetAlpha(Image img, float a)
        {
            if (img == null) return;
            var c = img.color; c.a = Mathf.Clamp01(a); img.color = c;
        }

        static float Aspect(Sprite sp, float fallback)
            => sp != null && sp.rect.height > 0f ? sp.rect.width / sp.rect.height : fallback;

        /// <summary>血統資料夾名；留空退回 Base（同 DramaTalkDatabase 的慣例）。</summary>
        static string Blood(string folder) => string.IsNullOrEmpty(folder) ? "Base" : folder.Trim();

        /// <summary>時間軸走完 → 進入自管的收尾淡出（不是立刻 Close，見 FadeDuration）。</summary>
        void Finish()
        {
            if (!_running) return;
            _running = false;
            _fadingOut = true;
            _fadeOutT = 0f;
        }

        /// <summary>淡到全透明了，真的關閉。此刻 FadeDuration=0，UIPanel 會立刻收掉並叫 OnClose。</summary>
        void CloseNow()
        {
            if (UIManager.Instance != null) UIManager.Instance.Close(this);
            else ReleaseCallback();   // 沒有 UIManager（單場景測試）也一定要放行，不然玩家永遠被鎖著
        }

        /// <summary>
        /// 每次開啟時把收尾狀態清乾淨。
        /// ⚠ 必須在這裡清、不能在 <c>OnClose</c> 清：<c>DoClose()</c> 是「先 OnClose 再 StartFade」，
        /// 在 OnClose 把 <c>_fadingOut</c> 設回 false 會讓 <see cref="FadeDuration"/> 又變成 0.4 秒，
        /// UIPanel 就會多跑一段「從 alpha 0 淡到 0」的空淡出、物件晚 0.4 秒才停用。
        /// <c>DoOpen()</c> 的順序是 OnOpen → StartFade，所以在這裡清正好趕得上開場淡入。
        /// </summary>
        protected override void OnOpen()
        {
            _fadingOut = false;
            _fadeOutT = 0f;
        }

        protected override void OnClose()
        {
            _running = false;
            ReleaseCallback();
        }

        /// <summary>
        /// 放行完成回呼（只會放一次）。
        /// 延後一幀再叫（同 BossIntroPanel）：此刻可能正在 OnClose，同步回呼又開新模態面板會重入卡死
        /// （見 PROBLEMS D8）。<see cref="IsShowing"/> 也一起延到那一刻才清——
        /// 早一幀清的話「熱鍵解鎖」會比「解除暫停」早，玩家能在暫停狀態下按 B 開背包。
        /// </summary>
        void ReleaseCallback()
        {
            if (_released) return;
            _released = true;
            var cb = _onFinished;
            _onFinished = null;
            TriggerChainRunner.NextFrame(() => { IsShowing = false; cb?.Invoke(); });
        }

        void OnDestroy()
        {
            // 面板被銷毀（退出 Play／外力銷毀）時未必走得到 OnClose → 這裡保底放行，
            // 不然 BloodlineSystem 的 external hold 永遠不會放，玩家整場不能動而且沒有錯誤訊息。
            // 這裡**直接呼叫**、不走 NextFrame：物件正在銷毀，排到下一幀的動作未必還有人跑。
            IsShowing = false;
            if (!_released)
            {
                _released = true;
                var cb = _onFinished;
                _onFinished = null;
                cb?.Invoke();
            }
            if (_oldMat != null) Destroy(_oldMat);
            if (_newMat != null) Destroy(_newMat);
        }
    }
}
