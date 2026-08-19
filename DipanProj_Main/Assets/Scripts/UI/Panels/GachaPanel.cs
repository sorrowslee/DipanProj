using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Gacha;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 祭壇抽選面板（老虎機）。玩家走到祭壇前按 F 開啟（地圖編輯器的 <c>openPanel</c> 觸發，
    /// panelId=gacha、arg=抽選池代號），花錢抽一個東西。
    ///
    /// <para><b>結果先算、表演後演</b>：按下抽選的當下 <see cref="GachaService.Roll"/> 就把錢扣掉、
    /// 結果算完、獎品也發進背包了。中間那段滾動純粹是把已知結果演出來——所以玩家 skip 跟不 skip
    /// 拿到的東西完全一樣，表演途中把面板關掉也不會弄丟東西。</para>
    ///
    /// <para><b>版面對齊的依據</b>：機台底圖 <c>GachaPanel_GachaBg</c> 上面畫了「內窗」與「層板橫線」，
    /// 直欄的格子必須剛好落在層板上，否則道具會浮在橫線上。所以版面不是隨便量的，而是以機台圖為基準：
    /// 下面那組 <c>Win*</c> 常數是從圖檔量出來的比例（內窗佔整張圖的百分比、層板間距＝圖高的 10%），
    /// 換圖之後只要重量這幾個數字，其他東西會自己跟著對齊。</para>
    ///
    /// 素材：
    ///   Resources/UI/GachaPanel/GachaPanel_Bg           整個面板最底層的殿堂背景（機台就站在它的地磚上）
    ///   Resources/UI/GachaPanel/GachaPanel_GachaBg      機台本體（含內窗、層板、兩側滾筒、底座）
    ///   Resources/UI/GachaPanel/GachaPanel_TitleBg      頂部標題橫幅（放池名）
    ///   Resources/UI/GachaPanel/GachaPanel_CoinBg       金錢橫條（左側圓座 + 右側數字）
    ///   Resources/UI/GachaPanel/GachaPanel_SelectFrame  中選欄位（金框 + 兩側蓮花旋鈕）
    ///   Resources/UI/GachaPanel/GachaPanel_StartIcon    抽選鈕上的法輪圖示
    ///   Resources/UI/Common/GachaPanel_StartBtn         抽選鈕底板
    /// </summary>
    public class GachaPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool ShowBackdrop => true;
        // ESC 不關：抽選中途被 ESC 關掉會讓表演斷在一半（東西其實已經到手了，但玩家不知道），
        // 而且十連結算也會跟著消失。一律要按「離開」才收起來。
        public override bool CloseOnEscape => false;

        // ── 素材路徑 ──
        const string Dir = "UI/GachaPanel/";
        const string CommonDir = "UI/Common/";
        const string ClearDir = "UI/ClearStagePanel/";   // 十連結算沿用過關結算的框與牌匾（純美術）
        const string TextDir  = "UI/Texts/";             // 圖片型文字（依語言解析，見 LocalizedArt）
        const string TitleFontPath = "Fonts/Bakudai/Bakudai-Bold";   // 毛筆字（同 BossIntroPanel 姓名牌）

        // ── 顯示字串（正式多語系時改走 Dipan.Localization.Language.GetText）──
        const string TxtSingle = "抽 選";
        const string TxtMulti = "十 連";
        const string TxtSkip = "跳 過";
        const string TxtClose = "離 開";
        const string TxtConfirm = "確 定";
        const string TxtEmptyPool = "這座祭壇還沒有可抽的東西";
        const string TxtBadPool = "這座祭壇沒有設定（檢查 GachaPoolTable.csv）";

        // ── 殿堂背景（最底層）──
        // 這張圖是 4:3（1448x1086）而畫布是 16:9，四邊又都是實心的（沒有透明邊可以裁），
        // 所以三種擺法只能選一種：
        //   ① 橫向拉滿 → 兩側的菩薩浮雕會被拉寬 33%，一眼看得出來變形
        //   ② 等比放大到蓋滿寬度 → 高度變 1440，上下各切掉 180，頂端的鈴鐺鏈與底部的地磚蓮燈全沒了
        //   ③ 等比對齊高度、兩側留邊 ← 採用這個
        // 選 ③ 是因為這張圖的構圖上下都有東西（頂端垂鏈、底部地磚與蓮花燈），切掉哪一邊都可惜；
        // 而它的左右邊緣本來就幾乎全黑（量到 RGB 約 6~7/255），只要底色調成一樣的黑，接縫就看不出來。
        const float HallH = 1080f;                  // ＝畫布參考高度，整張構圖完整保留（寬度由 ArtHall 的比例算）
        // 兩側補色：取自背景圖最外緣的平均色，讓「圖」與「補的底色」看起來是同一片牆。
        // ⚠ 必須不透明——底下是遊戲畫面，留一點透明度會讓兩側隱約透出場景、破功。
        static readonly Color HallEdge = new Color(0.027f, 0.026f, 0.028f, 1f);

        // ── 機台（版面的錨，其他東西都掛在它身上）──
        const float MachineH = 840f;             // 機台高（畫布 1080 的 78%）
        const float MachineAspect = 835f / 978f; // 機台圖的內容長寬比
        static float MachineW => MachineH * MachineAspect;

        // 從機台圖量出來的比例（換圖要重量這幾個）：
        const float WinCenterX = 0.500f;   // 內窗中心 x（佔圖寬）
        const float WinWidth = 0.280f;     // 內窗寬（佔圖寬）
        const float WinTop = 0.190f;       // 內窗上緣（佔圖高，從上算）
        const float WinBottom = 0.730f;    // 內窗下緣
        const float ShelfPitch = 0.100f;   // 層板間距（佔圖高）＝一格的間距

        static float Pitch => MachineH * ShelfPitch;                    // 一格間距（像素）
        static float WinW => MachineW * WinWidth;                       // 內窗寬（像素）
        static float WinCenterYFromTop => (WinTop + WinBottom) * 0.5f;  // 內窗中心（佔圖高）

        // 中選欄位固定在畫面正中央（y=0），機台再依內窗中心反推位置——
        // 這樣不管機台多大，「被選中的那一格」永遠在畫面正中間。
        //   內窗中心要落在 y=0 ⇒ 機台中心 y = -(內窗中心距機台中心的距離)
        //   內窗中心距機台頂端 = WinCenterYFromTop × 機台高，機台頂端距中心 = 機台高/2
        //   ⇒ 機台中心 y = (0.5 - WinCenterYFromTop) × 機台高 × -1 = (WinCenterYFromTop - 0.5) × 機台高
        static float MachineCenterY => MachineH * (WinCenterYFromTop - 0.5f);

        const int VisibleCells = 5;   // 內窗高 54% ÷ 層板間距 10% ≈ 5 格
        const int StripCells = 7;     // 多做兩格當緩衝，滾動時不露邊
        const int CenterIndex = (StripCells - 1) / 2;
        const float CellGap = 10f;
        static float CellH => Pitch - CellGap;
        static float CellW => WinW - 14f;

        // ── 其他元件（相對機台的比例，改機台大小會一起動）──
        const float FrameW = 340f;                   // 中選框寬
        const float FrameAspect = 852f / 299f;
        const float TitleW = 620f, TitleAspect = 1369f / 332f, TitleY = 440f;
        const float TitleInnerYOffset = -0.085f;     // 橫幅內凹面板的中心（佔橫幅高，往下為負）
        const float CoinW = 430f, CoinAspect = 925f / 185f, CoinY = 322f;
        const float CoinTextXOffset = 0.045f;        // 錢幣數字中心 x（佔錢條寬，避開左側圓座）
        // 抽選鈕：兩顆並排時的總寬要控制在機台寬度以內，不然按鈕會比機台還寬、看起來像兩塊招牌。
        const float BtnAspect = 1857f / 398f, BtnY = -360f;
        const float BtnWSingle = 430f, BtnWDual = 330f, BtnDualGapX = 175f;
        const float ResultRowY = -270f, ResultCell = 52f, ResultGap = 6f;
        const float CloseY = -462f;
        // 提示文字擺最下面：它是次要訊息（真的按不動時另有中央 Toast），
        // 放在錢條下方會壓到機台的門楣浮雕，放這裡才不會跟任何東西打架。
        const float HintY = -505f;

        // ── 單抽中獎特寫 ──
        // 放大的道具圖如果直接疊在還亮著的直欄上，會讓人分不清是「中獎特寫」還是「圖跑版」，
        // 所以中獎時在內窗範圍鋪一層壓暗當舞台，背後的格子退下去，放大的圖才讀得出來是刻意的。
        // 圖框刻意做成「寬度略小於內窗、高度可以超出金框」：細長的矛/劍靠高度撐滿，
        // 寬扁的東西則被寬度擋住，不會頂到兩側柱子。
        const float ResultIconW = 186f, ResultIconH = 180f;
        const float StageDimAlpha = 0.80f;
        // 名稱壓在放大圖的正下方，但要留出間隙——細長的矛撐滿 180 高時下緣剛好到 -90，
        // 名字擺 -125 才不會被矛尖戳到。寬度收在內窗以內，不要壓到兩側柱子的浮雕。
        const float ResultNameY = -125f, ResultNameW = 196f;

        // ── 十連結算小面板（抽完十連跳出來，讓玩家看清楚抽到什麼）──
        const float SumFrameH = 340f, SumFrameY = -10f;
        const float SumBannerW = 460f, SumTitleW = 300f, SumBannerY = 160f;
        const float SumGridW = 1200f, SumGridH = 270f, SumGridY = -15f;
        const float SumBtnW = 300f, SumBtnY = -250f;

        // ── 表演節奏 ──
        const float IdleSpeed = 26f;
        const float FastSpeed = 2600f;
        const float FastDurSingle = 0.70f, FastDurMulti = 0.22f;
        const int DecelCells = 12;
        const float DecelDurSingle = 1.35f, DecelDurMulti = 0.55f;
        const float LandDurSingle = 0.65f, LandDurMulti = 0.12f;
        const float ShakeAmp = 7f, ShakeFreq = 26f;   // 中選框「機器運轉中」的上下抖動

        // ── 版面元件 ──
        Text _title, _moneyText, _hintText, _resultName;
        RectTransform _strip, _resultRow, _frameRt, _resultRoot;
        Vector2 _frameHome;   // 中選框的原位（抖動以它為基準）
        Image[] _cellIcons;
        Image _resultIcon, _stageDim;
        Button _singleBtn, _multiBtn, _skipBtn;
        RectTransform _summaryRoot, _summaryGrid;
        Text _singleLabel, _multiLabel;

        // ── 狀態 ──
        string _poolId;
        GachaPoolDef _pool;
        List<GachaRollEntry> _candidates = new List<GachaRollEntry>();
        readonly int[] _contents = new int[StripCells];
        float _offset;
        Coroutine _spin;
        bool _spinning, _skip;
        System.Func<int> _randomFeed;

        // ───────────────────────── 對外開啟入口 ─────────────────────────

        /// <summary>開啟某座祭壇的抽選面板。poolId = GachaPoolTable.csv 的 PoolId。</summary>
        public static GachaPanel OpenFor(string poolId)
        {
            var ui = UIManager.Instance;
            if (ui == null) return null;
            var p = ui.Open<GachaPanel>();
            if (p != null) p.Configure(poolId);
            return p;
        }

        // ───────────────────────── 建構（只跑一次）─────────────────────────

        protected override void OnBuild()
        {
            _randomFeed = RandomCandidate;

            // 純色底：① 擋住底下的遊戲畫面與點擊 ② 補殿堂背景兩側（與畫布比例不同）留下的空白
            var bg = UIBuilder.SolidPanel(transform, "BG", HallEdge);
            bg.raycastTarget = true;

            BuildHall();        // 殿堂背景（最底層的圖）
            BuildMachine();     // 機台底圖（站在殿堂的地磚上）
            BuildReel();        // 直欄格子（疊在機台內窗上）
            BuildStageDim();    // 中獎舞台的壓暗（蓋住格子，但在中選框之下 → 金框仍然亮著）
            BuildSelectFrame(); // 中選框（疊在格子之上）
            BuildTitle();
            BuildCoin();
            BuildResultOverlay();
            BuildResultRow();
            BuildButtons();
            BuildMultiSummary();   // 最後建 = 疊在最上層（它的全螢幕遮罩要擋住底下的抽選鈕）
        }

        // 殿堂背景：等比對齊畫布高度、置中；兩側露出來的部分由上面那層純色底補（同色，看不出接縫）。
        void BuildHall()
        {
            PlaceArt(MakeArt(transform, "HallBg", ArtHall), ArtHall, HallH, Vector2.zero);
        }

        void BuildMachine()
        {
            PlaceArt(MakeArt(transform, "MachineBg", ArtMachine), ArtMachine, MachineH, new Vector2(0f, MachineCenterY));
        }

        // 直欄：一長條格子，靠 RectMask2D 裁在機台內窗裡。格子本身不畫底——底就是機台圖上的層板。
        void BuildReel()
        {
            float viewH = VisibleCells * Pitch;

            var viewGo = UIBuilder.Create("ReelViewport", transform);
            var viewRt = UIBuilder.Rect(viewGo);
            UIBuilder.Center(viewRt, WinW, viewH, new Vector2(MachineW * (WinCenterX - 0.5f), 0f));
            viewGo.AddComponent<RectMask2D>();

            var stripGo = UIBuilder.Create("Strip", viewGo.transform);
            _strip = UIBuilder.Rect(stripGo);
            UIBuilder.Center(_strip, CellW, StripCells * Pitch, Vector2.zero);

            _cellIcons = new Image[StripCells];
            for (int i = 0; i < StripCells; i++)
            {
                var cellGo = UIBuilder.Create($"Cell{i}", _strip);
                UIBuilder.Center(UIBuilder.Rect(cellGo), CellW, CellH, new Vector2(0f, (CenterIndex - i) * Pitch));

                _cellIcons[i] = UIBuilder.Image(cellGo.transform, "Icon", null, Color.white);
                _cellIcons[i].preserveAspect = true;
                _cellIcons[i].raycastTarget = false;
                float s = CellH * 0.84f;
                UIBuilder.Center(_cellIcons[i].rectTransform, s, s, Vector2.zero);
            }
        }

        // 中獎舞台：只壓暗「內窗」那一塊，不動標題、金錢與按鈕（那些還要看得見、按得到）。
        void BuildStageDim()
        {
            _stageDim = UIBuilder.Image(transform, "StageDim", null, new Color(0f, 0f, 0f, StageDimAlpha));
            _stageDim.raycastTarget = false;
            UIBuilder.Center(_stageDim.rectTransform, WinW, VisibleCells * Pitch,
                             new Vector2(MachineW * (WinCenterX - 0.5f), 0f));
            _stageDim.gameObject.SetActive(false);
        }

        void BuildSelectFrame()
        {
            var img = MakeArt(transform, "SelectFrame", ArtFrame);
            _frameRt = img.rectTransform;
            PlaceArt(img, ArtFrame, FrameW / FrameAspect, Vector2.zero);
            _frameHome = _frameRt.anchoredPosition;   // 抖動要以這個位置為基準（圖有透明邊，不是 0）
        }

        void BuildTitle()
        {
            float h = TitleW / TitleAspect;
            PlaceArt(MakeArt(transform, "TitleBg", ArtTitle), ArtTitle, h, new Vector2(0f, TitleY));

            _title = UIBuilder.Text(transform, "Title", "", 44, new Color(0.96f, 0.90f, 0.74f));
            _title.font = UIBuilder.LoadFont(TitleFontPath);
            UIBuilder.Center(_title.rectTransform, TitleW * 0.7f, h * 0.5f,
                             new Vector2(0f, TitleY + h * TitleInnerYOffset));
        }

        void BuildCoin()
        {
            float h = CoinW / CoinAspect;
            PlaceArt(MakeArt(transform, "CoinBg", ArtCoin), ArtCoin, h, new Vector2(0f, CoinY));

            _moneyText = UIBuilder.Text(transform, "Money", "", 30, new Color(1f, 0.88f, 0.55f));
            UIBuilder.Center(_moneyText.rectTransform, CoinW * 0.6f, h * 0.7f,
                             new Vector2(CoinW * CoinTextXOffset, CoinY));

            _hintText = UIBuilder.Text(transform, "Hint", "", 22, new Color(0.72f, 0.66f, 0.6f));
            UIBuilder.Center(_hintText.rectTransform, 900f, 28f, new Vector2(0f, HintY));
        }

        void BuildResultOverlay()
        {
            var go = UIBuilder.Create("ResultRoot", transform);
            _resultRoot = UIBuilder.Rect(go);
            UIBuilder.Center(_resultRoot, 260f, 260f, Vector2.zero);

            _resultIcon = UIBuilder.Image(go.transform, "ResultIcon", null, Color.white);
            _resultIcon.preserveAspect = true;
            _resultIcon.raycastTarget = false;
            UIBuilder.Center(_resultIcon.rectTransform, ResultIconW, ResultIconH, Vector2.zero);

            go.SetActive(false);

            // 名稱不放在會旋轉的 root 底下，否則會跟著轉。
            // 用描邊而不是底板來保證可讀性——底板會變成畫面上多一個看不出用途的黑方塊。
            _resultName = UIBuilder.Text(transform, "ResultName", "", 26, new Color(1f, 0.93f, 0.72f));
            _resultName.font = UIBuilder.LoadFont(TitleFontPath);
            UIBuilder.Center(_resultName.rectTransform, ResultNameW, 44f, new Vector2(0f, ResultNameY));
            var outline = _resultName.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            _resultName.gameObject.SetActive(false);
        }

        // ── 十連結算小面板 ──
        // 十連的滾動太快，玩家記不住抽到什麼（按跳過更是完全沒看到），所以抽完統一跳一張清單。
        // 美術直接沿用過關結算那套（框／「獲得獎勵」牌匾／按鈕），視覺語言一致又不用另外做圖。
        // 整個面板底下鋪一層全螢幕遮罩，擋住下面的抽選鈕——玩家一定要按「確定」才會回到機台。
        void BuildMultiSummary()
        {
            var rootGo = UIBuilder.Create("MultiSummary", transform);
            _summaryRoot = UIBuilder.Rect(rootGo);
            UIBuilder.Stretch(_summaryRoot);

            var dim = UIBuilder.SolidPanel(rootGo.transform, "Dim", new Color(0f, 0f, 0f, 0.62f));
            dim.raycastTarget = true;   // 吃掉點擊，結算沒關掉前不能再抽

            PlaceArt(MakeArt(rootGo.transform, "Frame", ArtSumFrame), ArtSumFrame, SumFrameH, new Vector2(0f, SumFrameY));
            PlaceArt(MakeArt(rootGo.transform, "Banner", ArtSumBanner), ArtSumBanner,
                     SumBannerW / ArtSumBanner.Aspect, new Vector2(0f, SumBannerY));
            // 「獲得獎勵」是圖片型文字 → 內容框不能用寫死的，要照實際載到的那一版重新量（見 MeasuredSpec）。
            var bannerText = MakeArt(rootGo.transform, "BannerText", ArtSumTitle);
            var sumTitleSpec = MeasuredSpec(ArtSumTitle, bannerText.sprite);
            PlaceArt(bannerText, sumTitleSpec, SumTitleW / sumTitleSpec.Aspect, new Vector2(0f, SumBannerY));

            var gridGo = UIBuilder.Create("Grid", rootGo.transform);
            _summaryGrid = UIBuilder.Rect(gridGo);
            UIBuilder.Center(_summaryGrid, SumGridW, SumGridH, new Vector2(0f, SumGridY));

            var btn = UIBuilder.Button(rootGo.transform, "ConfirmBtn", "", HideMultiSummary, new Color(1f, 1f, 1f, 0f));
            btn.targetGraphic = btn.GetComponent<Image>();
            float bh = SumBtnW / ArtSumBtn.Aspect;
            UIBuilder.Center((RectTransform)btn.transform, SumBtnW, bh, new Vector2(0f, SumBtnY));
            var bgArt = MakeArt(btn.transform, "Bg", ArtSumBtn);
            bgArt.transform.SetAsFirstSibling();
            PlaceArt(bgArt, ArtSumBtn, bh, Vector2.zero);
            var lab = UIBuilder.Text(btn.transform, "Label", TxtConfirm, 30, new Color(1f, 0.93f, 0.75f));
            lab.font = UIBuilder.LoadFont(TitleFontPath);
            UIBuilder.Center(lab.rectTransform, SumBtnW * 0.7f, bh * 0.6f, new Vector2(0f, bh * 0.04f));

            rootGo.SetActive(false);
        }

        /// <summary>把這一輪十連的結果攤成清單顯示。相同的東西會合併成「名稱 ×N」。</summary>
        void ShowMultiSummary(List<int> results)
        {
            if (_summaryRoot == null || _summaryGrid == null || results == null) return;

            // 合併重複（保持第一次抽到的順序，看起來比較自然）
            var order = new List<int>();
            var count = new Dictionary<int, int>();
            foreach (int id in results)
            {
                if (id <= 0) continue;
                if (!count.ContainsKey(id)) { count[id] = 0; order.Add(id); }
                count[id]++;
            }

            for (int i = _summaryGrid.childCount - 1; i >= 0; i--) Destroy(_summaryGrid.GetChild(i).gameObject);

            int n = order.Count;
            if (n > 0)
            {
                int rows = n <= 5 ? 1 : 2;
                int perRow = Mathf.CeilToInt(n / (float)rows);
                float cellW = Mathf.Min(230f, SumGridW / perRow);
                float cellH = rows == 1 ? 200f : 130f;
                float iconS = rows == 1 ? 110f : 76f;

                var inv = InventorySystem.Instance;
                for (int i = 0; i < n; i++)
                {
                    int row = i / perRow;
                    int col = i % perRow;
                    int inThisRow = Mathf.Min(perRow, n - row * perRow);
                    float rowW = inThisRow * cellW;
                    float x = -rowW * 0.5f + cellW * 0.5f + col * cellW;
                    float y = rows == 1 ? 0f : (row == 0 ? cellH * 0.52f : -cellH * 0.52f);

                    int id = order[i];
                    var d = inv != null ? inv.GetData(id) : null;

                    var cell = UIBuilder.Create($"Sum{i}", _summaryGrid);
                    UIBuilder.Center(UIBuilder.Rect(cell), cellW, cellH, new Vector2(x, y));

                    var icon = UIBuilder.Image(cell.transform, "Icon", null, Color.white);
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    UIBuilder.Center(icon.rectTransform, iconS, iconS, new Vector2(0f, cellH * 0.14f));
                    ItemIcons.Apply(icon, id);

                    if (count[id] > 1)
                    {
                        var cnt = UIBuilder.Text(cell.transform, "Count", $"×{count[id]}", 24,
                                                 new Color(1f, 0.88f, 0.5f), TextAnchor.LowerRight);
                        cnt.fontStyle = FontStyle.Bold;
                        UIBuilder.Center(cnt.rectTransform, iconS, iconS,
                                         new Vector2(cellW * 0.16f, cellH * 0.14f - iconS * 0.12f));
                    }

                    var nameT = UIBuilder.Text(cell.transform, "Name", d != null ? d.Name : $"#{id}",
                                               rows == 1 ? 22 : 18, new Color(0.94f, 0.88f, 0.74f));
                    UIBuilder.Center(nameT.rectTransform, cellW - 6f, 30f, new Vector2(0f, -cellH * 0.34f));
                }
            }

            ClearResultRow();          // 結算清單已經涵蓋，底下那排小縮圖就不用了
            HideResult();
            _summaryRoot.gameObject.SetActive(true);
            _summaryRoot.SetAsLastSibling();
        }

        void HideMultiSummary()
        {
            if (_summaryRoot != null) _summaryRoot.gameObject.SetActive(false);
        }

        bool SummaryOpen => _summaryRoot != null && _summaryRoot.gameObject.activeSelf;

        void BuildResultRow()
        {
            var go = UIBuilder.Create("ResultRow", transform);
            _resultRow = UIBuilder.Rect(go);
            UIBuilder.Center(_resultRow, 1100f, ResultCell, new Vector2(0f, ResultRowY));
        }

        void BuildButtons()
        {
            _singleBtn = MakeArtButton("SingleBtn", TxtSingle, () => StartRoll(false));
            _singleLabel = _singleBtn.GetComponentInChildren<Text>();

            _multiBtn = MakeArtButton("MultiBtn", TxtMulti, () => StartRoll(true));
            _multiLabel = _multiBtn.GetComponentInChildren<Text>();

            _skipBtn = MakeArtButton("SkipBtn", TxtSkip, () => _skip = true);

            var close = UIBuilder.Button(transform, "CloseBtn", TxtClose,
                                         () => UIManager.Instance?.Close(this),
                                         new Color(0.16f, 0.14f, 0.17f, 0.9f));
            close.targetGraphic = close.GetComponent<Image>();   // 程式建鈕需手動指（見 PROBLEMS D4）
            UIBuilder.Center((RectTransform)close.transform, 190f, 52f, new Vector2(0f, CloseY));
        }

        // 一顆「底板 + 法輪圖示 + 文字」的抽選鈕。
        // 按鈕本體是一張**全透明**的 Image：它只負責接點擊，尺寸剛好等於底板圖案的可見範圍，
        // 這樣點擊區才不會被圖檔那圈透明邊撐大（StartBtn 的透明邊上下各佔 16%）。
        // 底板美術是它的子物件，用 PlaceArt 擺（要補償透明邊）。
        Button MakeArtButton(string name, string label, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIBuilder.Button(transform, name, "", onClick, new Color(1f, 1f, 1f, 0f));
            var hit = b.GetComponent<Image>();
            hit.raycastTarget = true;          // 全透明仍然接得到點擊（uGUI 不看 alpha）
            b.targetGraphic = hit;

            var bg = MakeArt(b.transform, "Bg", ArtBtn);
            bg.transform.SetAsFirstSibling();  // 底板畫在圖示與文字底下

            MakeArt(b.transform, "Icon", ArtIcon);

            var t = UIBuilder.Text(b.transform, "Label", label, 30, new Color(1f, 0.92f, 0.72f));
            t.font = UIBuilder.LoadFont(TitleFontPath);
            return b;
        }

        // 依「有沒有連抽」排抽選鈕：有＝兩顆並排、沒有＝一顆置中。表演中則換成一顆置中的「跳過」。
        void LayoutButtons()
        {
            bool dual = _pool != null && _pool.AllowsMulti;

            if (_spinning)
            {
                _singleBtn.gameObject.SetActive(false);
                _multiBtn.gameObject.SetActive(false);
                _skipBtn.gameObject.SetActive(true);
                PlaceButton(_skipBtn, 0f, BtnWSingle);
                return;
            }

            _skipBtn.gameObject.SetActive(false);
            _singleBtn.gameObject.SetActive(true);
            _multiBtn.gameObject.SetActive(dual);
            if (dual)
            {
                PlaceButton(_singleBtn, -BtnDualGapX, BtnWDual);
                PlaceButton(_multiBtn, BtnDualGapX, BtnWDual);
            }
            else PlaceButton(_singleBtn, 0f, BtnWSingle);
        }

        void PlaceButton(Button b, float x, float w)
        {
            float h = w / BtnAspect;
            // 按鈕本體（＝點擊區）就是底板圖案的可見大小。
            UIBuilder.Center((RectTransform)b.transform, w, h, new Vector2(x, BtnY));

            var bg = b.transform.Find("Bg") as RectTransform;
            if (bg != null) PlaceArt(bg.GetComponent<Image>(), ArtBtn, h, Vector2.zero);

            var icon = b.transform.Find("Icon") as RectTransform;
            if (icon != null) PlaceArt(icon.GetComponent<Image>(), ArtIcon, h * 0.66f, new Vector2(-w * 0.26f, 0f));

            var label = b.transform.Find("Label") as RectTransform;
            if (label != null) UIBuilder.Center(label, w * 0.52f, h * 0.6f, new Vector2(w * 0.08f, 0f));
        }

        // ───────────────────────── 開關 ─────────────────────────

        void Configure(string poolId)
        {
            _poolId = poolId;
            _pool = GachaPoolTable.Get(poolId);
            RefreshStatic();
            ResetReel();
        }

        protected override void OnOpen()
        {
            _spinning = false;
            _skip = false;
            HideResult();
            HideMultiSummary();
            ClearResultRow();
            RefreshStatic();
        }

        protected override void OnClose()
        {
            if (_spin != null) { StopCoroutine(_spin); _spin = null; }
            _spinning = false;
            HideMultiSummary();
            if (_frameRt != null) _frameRt.anchoredPosition = _frameHome;
        }

        void RefreshStatic()
        {
            if (_pool == null)
            {
                if (_title != null) _title.text = "祭 壇";
                if (_moneyText != null) _moneyText.text = "";
                if (_hintText != null) _hintText.text = TxtBadPool;
                _candidates.Clear();
                SetInteractable(_singleBtn, false);
                SetInteractable(_multiBtn, false);
                LayoutButtons();
                if (!string.IsNullOrEmpty(_poolId))
                    Debug.LogWarning($"[GachaPanel] 找不到抽選池「{_poolId}」。檢查地圖編輯器 openPanel 的 arg 與 GachaPoolTable.csv 的 PoolId。");
                return;
            }

            _candidates = GachaService.BuildCandidates(_pool);

            if (_title != null) _title.text = _pool.DisplayName;
            if (_moneyText != null) _moneyText.text = GachaService.MoneyHeld(_pool).ToString();
            if (_singleLabel != null) _singleLabel.text = $"{TxtSingle}  {_pool.CostSingle}";
            if (_multiLabel != null) _multiLabel.text = $"{TxtMulti}  {_pool.CostMulti}";

            LayoutButtons();

            bool canSingle = GachaService.CanRoll(_pool, false, out string whySingle, _candidates);
            bool canMulti = _pool.AllowsMulti && GachaService.CanRoll(_pool, true, out _, _candidates);
            SetInteractable(_singleBtn, canSingle && !_spinning);
            SetInteractable(_multiBtn, canMulti && !_spinning);

            if (_hintText != null)
            {
                if (_spinning) _hintText.text = "";
                else if (_candidates.Count == 0) _hintText.text = TxtEmptyPool;
                else _hintText.text = canSingle ? "" : whySingle;
            }
        }

        // 反灰要染「底板美術」，不是按鈕本體（本體是全透明的點擊區，染它看不出來）。
        static void SetInteractable(Button b, bool on)
        {
            if (b == null) return;
            b.interactable = on;
            var bg = b.transform.Find("Bg");
            var img = bg != null ? bg.GetComponent<Image>() : null;
            if (img != null) img.color = on ? Color.white : new Color(0.5f, 0.46f, 0.46f, 0.7f);
            var icon = b.transform.Find("Icon");
            var ii = icon != null ? icon.GetComponent<Image>() : null;
            if (ii != null) ii.color = on ? Color.white : new Color(0.5f, 0.46f, 0.46f, 0.7f);
        }

        // ───────────────────────── 直欄內容 ─────────────────────────

        void ResetReel()
        {
            _offset = 0f;
            for (int i = 0; i < StripCells; i++) _contents[i] = RandomCandidate();
            RefreshCells();
            ApplyOffset();
        }

        int RandomCandidate()
        {
            if (_candidates == null || _candidates.Count == 0) return 0;
            return _candidates[Random.Range(0, _candidates.Count)].ItemId;
        }

        void ShiftDown(int newTopItemId)
        {
            for (int i = StripCells - 1; i >= 1; i--) _contents[i] = _contents[i - 1];
            _contents[0] = newTopItemId;
            RefreshCells();
        }

        void RefreshCells()
        {
            var inv = InventorySystem.Instance;
            for (int i = 0; i < StripCells; i++)
            {
                if (_cellIcons[i] == null) continue;
                ItemIcons.Apply(_cellIcons[i], _contents[i]);
            }
        }

        void ApplyOffset()
        {
            // _offset 是「已經往下走了多少」，uGUI 的 +y 是往上，所以要取負號才是往下滾。
            if (_strip != null) _strip.anchoredPosition = new Vector2(0f, -_offset);
        }

        void Advance(float delta, System.Func<int> nextContent, ref int wraps)
        {
            _offset += delta;
            while (_offset >= Pitch)
            {
                _offset -= Pitch;
                wraps++;
                ShiftDown(nextContent());
            }
            ApplyOffset();
        }

        void Update()
        {
            if (!IsOpen) return;

            // 中選框在表演中上下抖動，模仿機器運轉。停下就回正位。
            if (_frameRt != null)
            {
                float shake = _spinning
                    ? (Mathf.Sin(Time.unscaledTime * ShakeFreq * Mathf.PI * 2f) * 0.65f
                       + Mathf.Sin(Time.unscaledTime * ShakeFreq * 3.7f) * 0.35f) * ShakeAmp
                    : 0f;
                _frameRt.anchoredPosition = _frameHome + new Vector2(0f, shake);
            }

            // 待機：直欄緩慢往下滾，看起來是活的。結果特寫還蓋在中央時不滾。
            if (_spinning || SummaryOpen) return;
            if (_resultRoot != null && _resultRoot.gameObject.activeSelf) return;
            if (_candidates == null || _candidates.Count == 0) return;

            int ignore = 0;
            Advance(IdleSpeed * Time.unscaledDeltaTime, _randomFeed, ref ignore);
        }

        // ───────────────────────── 抽選流程 ─────────────────────────

        void StartRoll(bool multi)
        {
            if (_spinning || _pool == null) return;

            var res = GachaService.Roll(_poolId, multi);
            if (!res.Ok)
            {
                AlertPanel.Toast(res.Reason ?? "現在不能抽");
                RefreshStatic();
                return;
            }

            _skip = false;
            _spinning = true;
            HideResult();
            HideMultiSummary();
            ClearResultRow();
            RefreshStatic();

            if (_spin != null) StopCoroutine(_spin);
            _spin = StartCoroutine(PlayRoll(res.ItemIds, multi));
        }

        IEnumerator PlayRoll(List<int> results, bool multi)
        {
            float fastDur = multi ? FastDurMulti : FastDurSingle;
            float decelDur = multi ? DecelDurMulti : DecelDurSingle;
            float landDur = multi ? LandDurMulti : LandDurSingle;

            for (int n = 0; n < results.Count; n++)
            {
                int result = results[n];

                if (!_skip)
                {
                    float t = 0f;
                    int wraps = 0;
                    while (t < fastDur && !_skip)
                    {
                        t += Time.unscaledDeltaTime;
                        Advance(FastSpeed * Time.unscaledDeltaTime, _randomFeed, ref wraps);
                        yield return null;
                    }
                }

                if (!_skip)
                {
                    // 減速：再走 DecelCells 格，越走越慢，最後剛好停在對齊位置。
                    int wraps = 0;
                    int resultInjection = Mathf.Max(1, DecelCells - CenterIndex);
                    float startOffset = _offset;
                    float distance = DecelCells * Pitch - startOffset;
                    float traveled = 0f;
                    float t = 0f;
                    // Advance 是「先 wraps++ 再呼叫 feed」，所以這裡比對的是遞增後的值。
                    System.Func<int> feed = () => (wraps == resultInjection) ? result : RandomCandidate();

                    while (t < decelDur && !_skip)
                    {
                        t += Time.unscaledDeltaTime;
                        float e = Mathf.Clamp01(t / decelDur);
                        e = 1f - Mathf.Pow(1f - e, 3f);
                        float target = distance * e;
                        Advance(target - traveled, feed, ref wraps);
                        traveled = target;
                        yield return null;
                    }

                    while (wraps < DecelCells) { wraps++; ShiftDown(wraps == resultInjection ? result : RandomCandidate()); }
                    _offset = 0f;
                    ApplyOffset();
                }

                if (_skip)
                {
                    _contents[CenterIndex] = result;
                    RefreshCells();
                    _offset = 0f;
                    ApplyOffset();
                }

                if (multi) AddToResultRow(result);
                if (!multi) yield return ShowResult(result, animate: !_skip, dur: landDur);
                else if (!_skip)
                {
                    float t = 0f;
                    while (t < landDur) { t += Time.unscaledDeltaTime; yield return null; }
                }
            }

            _spinning = false;
            _skip = false;
            RefreshStatic();
            _spin = null;

            // 十連：不論有沒有按跳過，最後都跳一張結算清單——不然玩家根本不知道抽到了什麼。
            if (multi) ShowMultiSummary(results);
        }

        IEnumerator ShowResult(int itemId, bool animate, float dur)
        {
            var inv = InventorySystem.Instance;
            var d = inv != null ? inv.GetData(itemId) : null;

            if (_resultIcon != null)
            {
                ItemIcons.Apply(_resultIcon, itemId);
            }
            if (_resultName != null)
            {
                _resultName.text = d != null ? d.Name : $"#{itemId}";
                _resultName.gameObject.SetActive(true);
            }
            if (_stageDim != null) _stageDim.gameObject.SetActive(true);
            if (_resultRoot != null)
            {
                _resultRoot.gameObject.SetActive(true);
                _resultRoot.localScale = Vector3.one;
                _resultRoot.localRotation = Quaternion.identity;
            }

            if (!animate || _resultRoot == null) yield break;

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float e = Mathf.Clamp01(t / dur);
                float ease = 1f - Mathf.Pow(1f - e, 3f);
                float scale = Mathf.LerpUnclamped(0.3f, 1f, ease) + Mathf.Sin(e * Mathf.PI) * 0.25f;
                _resultRoot.localScale = Vector3.one * scale;
                _resultRoot.localRotation = Quaternion.Euler(0f, 0f, 720f * (1f - ease));
                yield return null;
            }
            _resultRoot.localScale = Vector3.one;
            _resultRoot.localRotation = Quaternion.identity;
        }

        void HideResult()
        {
            if (_resultRoot != null) _resultRoot.gameObject.SetActive(false);
            if (_resultName != null) _resultName.gameObject.SetActive(false);
            if (_stageDim != null) _stageDim.gameObject.SetActive(false);
        }

        void ClearResultRow()
        {
            if (_resultRow == null) return;
            for (int i = _resultRow.childCount - 1; i >= 0; i--) Destroy(_resultRow.GetChild(i).gameObject);
        }

        void AddToResultRow(int itemId)
        {
            if (_resultRow == null) return;
            var inv = InventorySystem.Instance;
            var d = inv != null ? inv.GetData(itemId) : null;

            var cell = UIBuilder.Create($"R{_resultRow.childCount}", _resultRow);
            var bgImg = cell.AddComponent<Image>();
            bgImg.color = new Color(0.10f, 0.08f, 0.10f, 0.85f);
            bgImg.raycastTarget = false;

            var icon = UIBuilder.Image(cell.transform, "Icon", null, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            UIBuilder.Center(icon.rectTransform, ResultCell - 10f, ResultCell - 10f, Vector2.zero);
            ItemIcons.Apply(icon, itemId);

            int n = _resultRow.childCount;
            float totalW = n * ResultCell + (n - 1) * ResultGap;
            float startX = -totalW * 0.5f + ResultCell * 0.5f;
            for (int i = 0; i < n; i++)
                UIBuilder.Center((RectTransform)_resultRow.GetChild(i), ResultCell, ResultCell,
                                 new Vector2(startX + i * (ResultCell + ResultGap), 0f));
        }

        // ───────────────────────── 素材擺放（內容框補償）─────────────────────────
        //
        // ⚠ 這幾張圖是「整張畫布輸出」的：實際圖案只佔中間一塊，四周是大片透明。
        //   例如中選框的圖案只有 852x299，卻存在 1536x1024 的畫布裡。
        //   如果直接把整張圖塞進想要的方框，Unity 會拿「整張畫布」去對齊，
        //   結果圖案會又小又偏。所以下面記下每張圖「不透明內容」在畫布中的位置，
        //   由 PlaceArt 反推出該給多大的方框、要偏移多少，讓**內容**剛好落在指定位置與尺寸。
        //
        // 【重新輸出圖檔之後要做什麼】數字會對不上，但不會靜默出錯——
        //   LoadArt 會比對圖檔尺寸，一旦和下表的 fullW/fullH 不同就印警告叫你重量。
        //   重量方法：把圖丟進任何影像工具，看不透明像素的邊界框（左, 上, 寬, 高）。
        struct ArtSpec
        {
            public string path;
            public float fullW, fullH;   // 圖檔完整尺寸
            public float bx, by, bw, bh; // 不透明內容的邊界框（左上為原點）
            public ArtSpec(string path, float fullW, float fullH, float bx, float by, float bw, float bh)
            { this.path = path; this.fullW = fullW; this.fullH = fullH; this.bx = bx; this.by = by; this.bw = bw; this.bh = bh; }
            public float Aspect => bw / bh;
        }

        // 殿堂背景是唯一一張「整張都是內容」的圖（實心 RGB、沒有透明邊），所以邊界框＝整張畫布。
        // 仍然走 ArtSpec 是為了沿用 LoadArt 的尺寸檢查——換圖時尺寸不同會出警告，不會默默跑位。
        static readonly ArtSpec ArtHall    = new ArtSpec(Dir + "GachaPanel_Bg",          1448, 1086,   0,   0, 1448, 1086);
        static readonly ArtSpec ArtMachine = new ArtSpec(Dir + "GachaPanel_GachaBg",     1536, 1024, 350,   8,  835, 978);
        static readonly ArtSpec ArtTitle   = new ArtSpec(Dir + "GachaPanel_TitleBg",     1536, 1024,  83, 233, 1369, 332);
        static readonly ArtSpec ArtCoin    = new ArtSpec(Dir + "GachaPanel_CoinBg",      1536, 1024, 305, 398,  925, 185);
        static readonly ArtSpec ArtFrame   = new ArtSpec(Dir + "GachaPanel_SelectFrame", 1536, 1024, 341, 323,  852, 299);
        static readonly ArtSpec ArtIcon    = new ArtSpec(Dir + "GachaPanel_StartIcon",   1118,  760, 239,  85,  615, 564);
        static readonly ArtSpec ArtBtn     = new ArtSpec(CommonDir + "GachaPanel_StartBtn", 2072, 588, 113, 110, 1857, 398);
        // 十連結算沿用過關結算的美術（同一套視覺語言，不用另外做圖）
        static readonly ArtSpec ArtSumFrame = new ArtSpec(ClearDir + "ClearStagePanel_ItemBg",       2244, 701,  73,  75, 2097, 550);
        static readonly ArtSpec ArtSumBanner = new ArtSpec(ClearDir + "ClearStagePanel_GainItemBg",  1000, 250,  80,  37,  852, 152);
        // ⚠ **這一張是圖片型文字**，後面四個數字只對繁中版成立（每種語言的字寬高都不一樣）。
        //   實際擺放時會用 MeasuredSpec 依載到的 sprite 重新量一次，所以這裡的數字只是後備值。
        //   見 readme/LOCALIZATION.md。
        static readonly ArtSpec ArtSumTitle = new ArtSpec(TextDir + "ClearStagePanel_GainItemText",   866, 288, 179,  80,  508, 116);
        static readonly ArtSpec ArtSumBtn   = new ArtSpec(ClearDir + "ClearStagePanel_ReturnBtn",   1000, 250, 169,  33,  660, 197);

        /// <summary>建一張美術圖（不擋點擊）。載不到就留一個透明的殼，版面不會塌。</summary>
        static Image MakeArt(Transform parent, string name, ArtSpec spec)
        {
            var img = UIBuilder.Image(parent, name, LoadArt(spec), Color.white);
            img.raycastTarget = false;
            img.preserveAspect = false;   // 尺寸由 PlaceArt 精算，不要讓 preserveAspect 再插手
            return img;
        }

        /// <summary>
        /// 把圖擺好，讓「不透明內容」剛好是 contentH 高、落在 center 這個位置（寬度依內容比例自動算）。
        /// 方框會比 contentH 大（因為要把透明邊也算進去），位置也會補償內容在畫布中的偏移。
        /// </summary>
        /// <summary>
        /// 用**實際載到的 sprite** 重新量內容框，覆寫 <see cref="ArtSpec"/> 裡寫死的那四個數字。
        ///
        /// 只給 <c>UI/Texts/</c> 底下的**圖片型文字**用。那些圖每種語言的字寬高、留白都不一樣
        /// （實測：「獲得獎勵」繁中版的字佔畫布寬 64%、英文版 REWARDS 佔 69%），
        /// 寫死的數字只對母版成立，換語言就會被擺歪、縮錯——而 <see cref="LoadArt"/> 的檢查
        /// 只比畫布比例，兩版畫布都接近 3:1 時**完全不會警告**。
        ///
        /// ⚠ 其他圖**不要**套這個：它們的四個數字是人工量、依美術判斷微調過的
        /// （alpha 門檻、要不要含外發光），自動量會蓋掉那些調整。
        /// </summary>
        static ArtSpec MeasuredSpec(ArtSpec spec, Sprite sp)
        {
            if (sp == null) return spec;
            if (!spec.path.StartsWith(Dipan.Localization.LocalizedArt.Root, System.StringComparison.Ordinal))
                return spec;

            var c = IconFit.ContentPx(sp);                 // 相對 sprite rect 左下角的像素
            float fw = sp.rect.width, fh = sp.rect.height;
            if (c.width < 1f || c.height < 1f || fw < 1f || fh < 1f) return spec;

            // 量出來幾乎等於整張畫布 = 這張圖的 Mesh Type 不是 Tight（ContentPx 量不到東西，退回整張）。
            // 那個結果比寫死的數字更差，所以維持原 spec。
            if (c.width >= fw * 0.99f && c.height >= fh * 0.99f) return spec;

            // ArtSpec 的 by 以「左上」為原點，ContentPx 以左下 → 換算。
            return new ArtSpec(spec.path, fw, fh, c.x, fh - (c.y + c.height), c.width, c.height);
        }

        static void PlaceArt(Image img, ArtSpec spec, float contentH, Vector2 center)
        {
            float contentW = contentH * spec.Aspect;
            float rectW = contentW * (spec.fullW / spec.bw);
            float rectH = contentH * (spec.fullH / spec.bh);

            // 內容中心相對「畫布中心」的偏移（圖檔座標左上原點 → UI 座標 +y 向上）
            float ox = (spec.bx + spec.bw * 0.5f) - spec.fullW * 0.5f;
            float oy = spec.fullH * 0.5f - (spec.by + spec.bh * 0.5f);
            // 換算成顯示後的像素
            float dx = ox * (rectW / spec.fullW);
            float dy = oy * (rectH / spec.fullH);

            UIBuilder.Center(img.rectTransform, rectW, rectH, center - new Vector2(dx, dy));
        }

        /// <summary>
        /// 載圖並檢查它跟 ArtSpec 記的還對不對得上（重新輸出圖檔時把靜默偏移變成明確警告）。
        ///
        /// ⚠ 比的是**畫布比例**而不是像素數。原因：Unity 匯入設定的 `Max Size`（本專案預設 2048）
        ///   會把超過的圖**等比縮小**，`sprite.rect` 拿到的是縮小後的尺寸——例如 GachaPanel_StartBtn
        ///   是 2072x588，進到遊戲裡變 2048x581。而 PlaceArt 的算式全是比值，等比縮放完全不影響結果，
        ///   那種情況報警只是雜訊（見 readme/PROBLEMS.md D12）。
        /// </summary>
        static Sprite LoadArt(ArtSpec spec)
        {
            var sp = LoadSprite(spec.path);
            if (sp != null)
            {
                float w = sp.rect.width, h = sp.rect.height;
                if (w > 0f && h > 0f && spec.fullH > 0f)
                {
                    float got = w / h, want = spec.fullW / spec.fullH;
                    if (Mathf.Abs(got - want) > want * 0.01f)   // 差 1% 以上才算真的換圖了
                        Debug.LogWarning($"[GachaPanel]「{spec.path}」的畫布比例對不上：實際 {w}x{h}（{got:F3}），" +
                                         $"版面表記的是 {spec.fullW}x{spec.fullH}（{want:F3}）。" +
                                         "圖重新輸出過了嗎？請重新量它的不透明內容邊界框並更新 GachaPanel 的 ArtSpec，否則位置與大小會偏掉。");
                }
            }
            return sp;
        }

        // ───────────────────────── 小工具 ─────────────────────────

        // Resources 載圖：優先 Sprite；匯入型別是 Texture 就自己 Create；都失敗回 null。
        static Sprite LoadSprite(string path)
        {
            // ⚠ UI/Texts/ 底下的是「圖片型文字」：實際檔案在 UI/Texts/<語言>/ 裡，
            //    這裡改寫成當前語言的路徑，缺當前語言就退回母版（繁中）。見 Localization/LocalizedArt。
            path = Dipan.Localization.LocalizedArt.ResolveExisting(path);

            var sp = Resources.Load<Sprite>(path);
            if (sp != null) return sp;
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null) return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            Debug.LogWarning($"[GachaPanel] 載不到美術 Resources/{path}（沒放圖，或匯入型別不是 Sprite/Texture？）。");
            return null;
        }
    }
}
