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
    /// <para><b>版面是工程版面</b>（純色塊 + 系統字，比照 SaveSlotPanel），等美術示意圖來了再重排。
    /// 表演邏輯與版面數字是分開的，換版面不會動到滾動那段。</para>
    ///
    /// 表演流程：待機時直欄緩慢往下滾 → 按抽選後高速滾動 → 減速、一格一格穿過中選欄位 → 停在結果那格
    /// → 該道具放大旋轉後定位、上面寫道具名稱。按 Skip 直接跳過滾動，直接放大顯示結果（不轉）。
    /// 連抽則是同一條直欄連續跑 N 次，每次停下把結果收進下方的結果列。
    /// </summary>
    public class GachaPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool ShowBackdrop => true;
        public override bool CloseOnEscape => true;   // 看完可以直接走，不強迫抽

        // ── 顯示字串（工程版面用；正式版面時改走 Dipan.Localization.Language.GetText）──
        const string TxtSingle = "抽 選";
        const string TxtMulti = "連 抽";
        const string TxtSkip = "跳過表演";
        const string TxtClose = "離 開";
        const string TxtEmptyPool = "這座祭壇還沒有可抽的東西";
        const string TxtBadPool = "這座祭壇沒有設定（檢查 GachaPoolTable.csv）";

        // ── 版面（量測常數；實機不合再調）──
        // ⚠ 畫布基準是 1920×1080（UIManager 的 CanvasScaler referenceResolution），
        //   所以整條直欄的高度 VisibleCells × Pitch 必須明顯小於 1080，否則會把標題、金錢、
        //   提示文字整個蓋掉（直欄視窗是不透明黑底），而且提示會被擠出畫面外看不到。
        //   目前 3 × 160 = 480，畫面 y 約落在 ±240，上下都留得下東西。
        const float CellW = 150f;      // 直欄一格的寬
        const float CellH = 150f;      // 直欄一格的高
        const float CellGap = 10f;     // 格與格的間距
        const int VisibleCells = 3;    // 視窗裡看得到幾格（奇數，正中間那格＝中選欄位）
        const int StripCells = 5;      // 直欄實際做幾格（比看得到的多兩格，滾動時才不會露邊）
        const float ResultIconSize = 200f;

        static float Pitch => CellH + CellGap;
        const int CenterIndex = (StripCells - 1) / 2;   // 直欄正中央那格的索引

        // ── 表演節奏 ──
        const float IdleSpeed = 26f;        // 待機緩慢下滾（像素/秒）
        const float FastSpeed = 2600f;      // 高速滾動（像素/秒）
        const float FastDurSingle = 0.70f;
        const float FastDurMulti = 0.22f;   // 連抽每一次都跑一遍，節奏要快很多
        const int DecelCells = 12;          // 減速階段總共再走幾格（必須 > CenterIndex）
        const float DecelDurSingle = 1.35f;
        const float DecelDurMulti = 0.55f;
        const float LandDurSingle = 0.65f;  // 放大旋轉定位
        const float LandDurMulti = 0.12f;

        // ── 版面元件 ──
        Text _title, _moneyText, _hintText, _resultName;
        RectTransform _strip, _resultRow;
        Image[] _cellIcons;
        Text[] _cellLabels;
        Image _resultIcon;
        RectTransform _resultRoot;
        Button _singleBtn, _multiBtn, _skipBtn;
        Text _singleLabel, _multiLabel;

        // ── 狀態 ──
        string _poolId;
        GachaPoolDef _pool;
        List<GachaRollEntry> _candidates = new List<GachaRollEntry>();
        readonly int[] _contents = new int[StripCells];   // 直欄每格目前顯示的道具 id（索引 0 = 最上面）
        float _offset;                                    // 直欄相對「對齊中選欄位」的位移（0 = 剛好對齊）
        Coroutine _spin;
        bool _spinning, _skip;

        // 快取一份「隨機取候選」的委派：C# 9 的 method group 轉換每次都會配置新的 Func，
        // 而這支在待機與高速滾動時是每幀呼叫的，直接寫 RandomCandidate 會每幀產生垃圾。
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

            var bg = UIBuilder.SolidPanel(transform, "BG", new Color(0.06f, 0.05f, 0.08f, 0.97f));
            bg.raycastTarget = true;

            // 先建直欄，標題/金錢等文字後建 → sibling 在後 = 畫在上面。
            // （直欄視窗有不透明黑底，先建文字的話萬一位置重疊就會被整段蓋掉。）
            BuildReel();

            _title = UIBuilder.Text(transform, "Title", "", 48, new Color(0.93f, 0.89f, 0.82f));
            UIBuilder.Anchor(_title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -70f), new Vector2(900f, 70f));

            _moneyText = UIBuilder.Text(transform, "Money", "", 28, new Color(1f, 0.87f, 0.5f));
            UIBuilder.Anchor(_moneyText.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -142f), new Vector2(900f, 40f));

            // 提示文字：緊接在直欄視窗下緣（視窗半高 + 一點間距），不要用會掉出畫面的絕對值。
            _hintText = UIBuilder.Text(transform, "Hint", "", 26, new Color(0.75f, 0.7f, 0.66f));
            UIBuilder.Anchor(_hintText.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -(VisibleCells * Pitch) * 0.5f - 32f), new Vector2(900f, 34f));

            BuildResultOverlay();
            BuildResultRow();
            BuildButtons();
        }

        // 中央直欄：外框（裁切視窗）＋ 會滾動的一長條格子 ＋ 疊在最上層的中選欄位。
        void BuildReel()
        {
            float viewH = VisibleCells * Pitch;

            var viewGo = UIBuilder.Create("ReelViewport", transform);
            var viewRt = UIBuilder.Rect(viewGo);
            UIBuilder.Center(viewRt, CellW + 16f, viewH, Vector2.zero);
            var viewBg = viewGo.AddComponent<Image>();
            viewBg.color = new Color(0.03f, 0.03f, 0.05f, 1f);
            viewBg.raycastTarget = false;
            viewGo.AddComponent<RectMask2D>();   // 超出視窗的格子裁掉

            var stripGo = UIBuilder.Create("Strip", viewGo.transform);
            _strip = UIBuilder.Rect(stripGo);
            UIBuilder.Center(_strip, CellW, StripCells * Pitch, Vector2.zero);

            _cellIcons = new Image[StripCells];
            _cellLabels = new Text[StripCells];
            for (int i = 0; i < StripCells; i++)
            {
                var cellGo = UIBuilder.Create($"Cell{i}", _strip);
                var cellRt = UIBuilder.Rect(cellGo);
                // 索引 0 在最上面；CenterIndex 那格在 local y = 0。
                UIBuilder.Center(cellRt, CellW, CellH, new Vector2(0f, (CenterIndex - i) * Pitch));

                var slotBg = cellGo.AddComponent<Image>();
                slotBg.color = new Color(0.12f, 0.11f, 0.15f, 1f);
                slotBg.raycastTarget = false;

                // icon 往上挪一點、縮小一點，讓底下的名稱列不會被壓到（原本兩者重疊約 7px）。
                _cellIcons[i] = UIBuilder.Image(cellGo.transform, "Icon", null, Color.white);
                _cellIcons[i].preserveAspect = true;
                _cellIcons[i].raycastTarget = false;
                UIBuilder.Center(_cellIcons[i].rectTransform, CellH - 66f, CellH - 66f, new Vector2(0f, 24f));

                _cellLabels[i] = UIBuilder.Text(cellGo.transform, "Label", "", 18, new Color(0.82f, 0.79f, 0.75f));
                UIBuilder.Anchor(_cellLabels[i].rectTransform,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 16f), new Vector2(CellW - 10f, 34f));
            }

            // 中選欄位：跟一格一樣大，疊在直欄之上（最後建 = sibling 在後 = 畫在上面）。
            var sel = UIBuilder.Create("SelectFrame", transform);
            var selRt = UIBuilder.Rect(sel);
            UIBuilder.Center(selRt, CellW + 26f, CellH + 18f, Vector2.zero);
            var selImg = sel.AddComponent<Image>();
            selImg.color = new Color(1f, 0.84f, 0.42f, 0.16f);
            selImg.raycastTarget = false;
            // 上下兩條亮邊，讓「中選」的位置一眼看得出來。
            for (int s = 0; s < 2; s++)
            {
                var edge = UIBuilder.Image(sel.transform, s == 0 ? "EdgeTop" : "EdgeBottom", null,
                                           new Color(1f, 0.84f, 0.42f, 0.95f));
                edge.raycastTarget = false;
                UIBuilder.Anchor(edge.rectTransform,
                    new Vector2(0f, s == 0 ? 1f : 0f), new Vector2(1f, s == 0 ? 1f : 0f),
                    new Vector2(0.5f, s == 0 ? 1f : 0f), Vector2.zero, new Vector2(0f, 4f));
            }
        }

        // 結果特寫：抽完後在正中央放大旋轉定位的那個圖 ＋ 底下的道具名稱。
        void BuildResultOverlay()
        {
            var go = UIBuilder.Create("ResultRoot", transform);
            _resultRoot = UIBuilder.Rect(go);
            UIBuilder.Center(_resultRoot, ResultIconSize, ResultIconSize + 80f, Vector2.zero);

            _resultIcon = UIBuilder.Image(go.transform, "ResultIcon", null, Color.white);
            _resultIcon.preserveAspect = true;
            _resultIcon.raycastTarget = false;
            UIBuilder.Center(_resultIcon.rectTransform, ResultIconSize, ResultIconSize, new Vector2(0f, 30f));

            _resultName = UIBuilder.Text(go.transform, "ResultName", "", 34, new Color(1f, 0.93f, 0.72f));
            UIBuilder.Anchor(_resultName.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, -6f), new Vector2(760f, 46f));

            go.SetActive(false);
        }

        // 連抽的結果列：每抽完一次就把結果縮圖收進這一排。
        void BuildResultRow()
        {
            var go = UIBuilder.Create("ResultRow", transform);
            _resultRow = UIBuilder.Rect(go);
            UIBuilder.Anchor(_resultRow,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 205f), new Vector2(1100f, 74f));
        }

        void BuildButtons()
        {
            _singleBtn = MakeButton("SingleBtn", TxtSingle, new Vector2(-230f, 118f), new Color(0.20f, 0.26f, 0.34f, 1f),
                                    () => StartRoll(false));
            _singleLabel = _singleBtn.GetComponentInChildren<Text>();

            _multiBtn = MakeButton("MultiBtn", TxtMulti, new Vector2(0f, 118f), new Color(0.30f, 0.24f, 0.16f, 1f),
                                   () => StartRoll(true));
            _multiLabel = _multiBtn.GetComponentInChildren<Text>();

            _skipBtn = MakeButton("SkipBtn", TxtSkip, new Vector2(230f, 118f), new Color(0.26f, 0.22f, 0.28f, 1f),
                                  () => _skip = true);

            MakeButton("CloseBtn", TxtClose, new Vector2(0f, 44f), new Color(0.18f, 0.16f, 0.20f, 1f),
                       () => UIManager.Instance?.Close(this));
        }

        Button MakeButton(string name, string label, Vector2 posFromBottom, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIBuilder.Button(transform, name, label, onClick, bg);
            b.targetGraphic = b.GetComponent<Image>();   // 程式建鈕需手動指（見 PROBLEMS D4）
            UIBuilder.Anchor((RectTransform)b.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                posFromBottom, new Vector2(210f, 62f));
            return b;
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
            ClearResultRow();
            RefreshStatic();
        }

        protected override void OnClose()
        {
            if (_spin != null) { StopCoroutine(_spin); _spin = null; }
            _spinning = false;
        }

        // 標題／金錢／按鈕文字與可按狀態。抽完、開啟時都要刷。
        void RefreshStatic()
        {
            if (_pool == null)
            {
                if (_title != null) _title.text = "祭 壇";
                if (_moneyText != null) _moneyText.text = "";
                if (_hintText != null) _hintText.text = TxtBadPool;
                _candidates.Clear();   // 不清的話待機捲動還在跑上一個池的內容
                SetInteractable(_singleBtn, false);
                SetInteractable(_multiBtn, false);
                if (_multiBtn != null) _multiBtn.gameObject.SetActive(false);
                if (_skipBtn != null) _skipBtn.gameObject.SetActive(false);
                // 只有「真的指定了池代號卻找不到」才是設定錯誤；面板剛建好還沒 Configure 時不要吼。
                if (!string.IsNullOrEmpty(_poolId))
                    Debug.LogWarning($"[GachaPanel] 找不到抽選池「{_poolId}」。檢查地圖編輯器 openPanel 的 arg 與 GachaPoolTable.csv 的 PoolId。");
                return;
            }

            _candidates = GachaService.BuildCandidates(_pool);

            if (_title != null) _title.text = $"{_pool.DisplayName} 祭 壇";
            if (_moneyText != null)
                _moneyText.text = $"{GachaService.MoneyName(_pool)} {GachaService.MoneyHeld(_pool)}";

            if (_singleLabel != null) _singleLabel.text = $"{TxtSingle}\n{_pool.CostSingle}";
            if (_multiLabel != null) _multiLabel.text = $"{TxtMulti} ×{_pool.MultiCount}\n{_pool.CostMulti}";

            if (_multiBtn != null) _multiBtn.gameObject.SetActive(_pool.AllowsMulti);
            if (_skipBtn != null) _skipBtn.gameObject.SetActive(_spinning);

            // 候選清單上面已經組好了，直接傳進去，不要讓 CanRoll 再各組一次（一次刷新原本要組三遍）。
            bool canSingle = GachaService.CanRoll(_pool, false, out string whySingle, _candidates);
            bool canMulti = _pool.AllowsMulti && GachaService.CanRoll(_pool, true, out _, _candidates);
            SetInteractable(_singleBtn, canSingle && !_spinning);
            SetInteractable(_multiBtn, canMulti && !_spinning);

            if (_hintText != null)
            {
                if (_spinning) _hintText.text = "";
                else if (_candidates.Count == 0) _hintText.text = TxtEmptyPool;
                else _hintText.text = canSingle ? $"池中共 {_candidates.Count} 種" : whySingle;
            }
        }

        static void SetInteractable(Button b, bool on)
        {
            if (b == null) return;
            b.interactable = on;
            var img = b.GetComponent<Image>();
            if (img != null)
            {
                var c = img.color;
                c.a = on ? 1f : 0.4f;
                img.color = c;
            }
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

        // 直欄往下滾一格：所有格子的內容往下推一格，最上面補一個新的。
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
                var d = inv != null ? inv.GetData(_contents[i]) : null;
                if (_cellIcons[i] != null)
                {
                    _cellIcons[i].sprite = d != null ? d.Icon : null;
                    _cellIcons[i].enabled = d != null && d.Icon != null;
                }
                if (_cellLabels[i] != null)
                    _cellLabels[i].text = d != null ? d.Name : "";
            }
        }

        void ApplyOffset()
        {
            // _offset 是「已經往下走了多少」，uGUI 的 +y 是往上，所以要取負號才是往下滾。
            // 這個負號很關鍵：正號會變成往上滾，而且每次跨格時內容會往回跳一格（看起來像抽格）。
            if (_strip != null) _strip.anchoredPosition = new Vector2(0f, -_offset);
        }

        // 往下移動 delta 像素，跨過整格就推一格內容進來。nextContent 決定「補進最上面那格」的內容。
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
            // 待機：直欄緩慢往下滾，看起來是活的。
            // 結果特寫還蓋在中央時不要滾——底下在動、上面定住，看起來很怪。
            if (_spinning || !IsOpen) return;
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

            // 這一刻錢已扣、獎品已進背包。以下純表演。
            _skip = false;
            _spinning = true;
            HideResult();
            ClearResultRow();
            RefreshStatic();
            if (_skipBtn != null) _skipBtn.gameObject.SetActive(true);

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
                    // ① 高速滾動（內容隨機）
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
                    // ② 減速：總共再走 DecelCells 格，越走越慢，最後剛好停在對齊位置。
                    //    第 (DecelCells - CenterIndex) 次補進來的格子，走完之後剛好落在中選欄位——把結果排在那一格。
                    int wraps = 0;
                    // 走完 DecelCells 格之後，第 (DecelCells - CenterIndex) 次補進來的那格剛好落在中選欄位。
                    // Mathf.Max(1,…) 是防呆：萬一有人把 DecelCells 調到 <= CenterIndex，result 會永遠餵不進去，
                    // 變成「演的跟拿到的不一樣」——那種 bug 很難查。
                    int resultInjection = Mathf.Max(1, DecelCells - CenterIndex);
                    // 扣掉高速段留下的殘量，讓終點自然落在對齊位置（否則最後要硬歸零，
                    // 會在減速最慢、最顯眼的那一刻整條瞬移最多一格）。
                    float startOffset = _offset;
                    float distance = DecelCells * Pitch - startOffset;
                    float traveled = 0f;
                    float t = 0f;
                    // 注意：Advance 是「先 wraps++ 再呼叫這個 feed」，所以這裡比對的是遞增後的值。
                    System.Func<int> feed = () => (wraps == resultInjection) ? result : RandomCandidate();

                    while (t < decelDur && !_skip)
                    {
                        t += Time.unscaledDeltaTime;
                        float e = Mathf.Clamp01(t / decelDur);
                        e = 1f - Mathf.Pow(1f - e, 3f);           // ease-out：快 → 一格一格慢慢穿過
                        float target = distance * e;
                        Advance(target - traveled, feed, ref wraps);
                        traveled = target;
                        yield return null;
                    }

                    // 收尾：把剩下的格數補完並對齊（浮點誤差不讓它停在半格）。
                    while (wraps < DecelCells) { wraps++; ShiftDown(wraps == resultInjection ? result : RandomCandidate()); }
                    _offset = 0f;
                    ApplyOffset();
                }

                // Skip：直接把結果放到中選欄位，不滾。
                if (_skip)
                {
                    _contents[CenterIndex] = result;
                    RefreshCells();
                    _offset = 0f;
                    ApplyOffset();
                }

                // ③ 落定表演（連抽時每次只閃一下，收進結果列；單抽不用結果列，大特寫就是全部）
                if (multi) AddToResultRow(result);
                if (!multi) yield return ShowResult(result, animate: !_skip, dur: landDur);
                else if (!_skip)
                {
                    float t = 0f;
                    while (t < landDur) { t += Time.unscaledDeltaTime; yield return null; }
                }
            }

            // 連抽：最後把最後一個結果也放大顯示一下，讓畫面有收尾。
            if (multi && results.Count > 0)
                yield return ShowResult(results[results.Count - 1], animate: !_skip, dur: LandDurSingle);

            _spinning = false;
            _skip = false;
            if (_skipBtn != null) _skipBtn.gameObject.SetActive(false);
            RefreshStatic();
            _spin = null;
        }

        // 結果特寫：animate=true 走「放大 → 旋轉 → 定位」；false（skip）直接顯示定位後的樣子。
        IEnumerator ShowResult(int itemId, bool animate, float dur)
        {
            var inv = InventorySystem.Instance;
            var d = inv != null ? inv.GetData(itemId) : null;

            if (_resultIcon != null)
            {
                _resultIcon.sprite = d != null ? d.Icon : null;
                _resultIcon.enabled = d != null && d.Icon != null;
            }
            if (_resultName != null) _resultName.text = d != null ? d.Name : $"#{itemId}";
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
                // 0.3 →（衝過頭）1.25 → 1.0，同時轉兩圈後回正。
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

            int index = _resultRow.childCount;
            const float size = 64f, gap = 8f;

            var cell = UIBuilder.Create($"R{index}", _resultRow);
            var rt = UIBuilder.Rect(cell);
            var bgImg = cell.AddComponent<Image>();
            bgImg.color = new Color(0.14f, 0.13f, 0.17f, 1f);
            bgImg.raycastTarget = false;

            var icon = UIBuilder.Image(cell.transform, "Icon", d != null ? d.Icon : null, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = d != null && d.Icon != null;
            UIBuilder.Center(icon.rectTransform, size - 10f, size - 10f, Vector2.zero);

            UIBuilder.Center(rt, size, size, Vector2.zero);

            // 每加一個就把整排重新置中。
            int n = _resultRow.childCount;
            float totalW = n * size + (n - 1) * gap;
            float startX = -totalW * 0.5f + size * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var c = (RectTransform)_resultRow.GetChild(i);
                UIBuilder.Center(c, size, size, new Vector2(startX + i * (size + gap), 0f));
            }
        }
    }
}
