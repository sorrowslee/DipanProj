using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Inventory;
using Dipan.Localization;
using Dipan.Progress;

namespace Dipan.UI
{
    /// <summary>
    /// 通關／死亡結算畫面（覆蓋層、暫停遊戲、擋輸入）。全程式建構。
    /// 底圖＝該關卡的 loading 圖（Resources/Loading/&lt;module&gt;.png）＋壓暗；其餘用美術元件
    /// （Resources/UI/ClearStagePanel/*）：標題、獎勵框、「獲得獎勵」標頭、返回廣場按鈕。
    ///
    /// 過關＝顯示「通關結算」標題圖；死亡＝沒有專屬圖 → 退回文字「殞命」（暗紅）。
    /// 獎勵格區（<see cref="RewardsArea"/>）：2026-11-16 起列出這一趟的功德值總額、新拿到的證明、所有取得的道具（GameFlowManager.BuildRunRewards）。
    /// 開關由 GameFlowManager 驅動；「返回廣場」→ GameFlowManager.ReturnToHubFromResult()。
    /// </summary>
    public class ResultPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Overlay;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;

        const string ArtDir  = "UI/ClearStagePanel/";   // 純美術：底版、獎勵框、按鈕底
        // 圖片型文字（字畫在圖裡）一律走 UI/Texts/，實際檔案在 UI/Texts/<語言>/ 底下，
        // 由 LoadSprite → LocalizedArt.ResolveExisting 依當前語言解析。見 readme/LOCALIZATION.md。
        const string TextDir = "UI/Texts/";
        static readonly Color LoseTitleColor = new Color(0.82f, 0.20f, 0.20f, 1f);
        static readonly Color SubtitleColor  = new Color(0.94f, 0.80f, 0.42f, 1f);   // 金

        Image _bg, _titleImg, _titleDeadImg;
        Text _titleLoseText, _subtitle, _emptyText;
        RectTransform _rewardsArea;
        RectTransform _backBtnRt;

        // 獎勵格的物品說明（2026-11-16）：滑鼠停在圖示上就跳共用 ItemTooltip。
        // 結算畫面在 Overlay 層、會壓過 Popup，所以 tooltip 掛 System 層（同道具獲得提示 AlertPanel.ToastItem）。
        // 不靠 raycast 事件，每幀用游標座標判斷（同 AlertPanel 的做法）。
        ItemTooltip _tip;
        readonly List<KeyValuePair<RectTransform, ItemStack>> _rewardHover = new List<KeyValuePair<RectTransform, ItemStack>>();
        int _hoverIdx = -1;

        // ── 劇本證明三行（2026-11-16，見 readme/CORE_LOOP_DESIGN.md §8.7、readme/LEVEL_END_FLOW.md）──
        // 獎勵框下方、返回鈕上方。這個劇本在 ProofTable.csv 沒有任何一列＝整區不顯示、返回鈕留在原位。
        // ⚠ 沒達成的一律只顯示「?????」，**絕不顯示條件**（作者：不提示怎麼達成，讓玩家去討論）。
        const float ProofRowY0 = -290f, ProofRowGap = 46f, ProofRowH = 44f, ProofRowW = 1200f;
        const int ProofFont = 32;
        const float BackBtnY = -430f, BackBtnYWithProofs = -472f;   // 有證明三行時返回鈕往下讓位
        const float ProofStartDelay = 0.8f;   // 面板開了多久才開始第一行演出（秒，真實時間：結算畫面會暫停遊戲）
        const float ProofShakeTime = 0.55f, ProofShatterTime = 0.65f, ProofPopTime = 0.30f, ProofBetween = 0.25f;
        const int TxtNameFmt = 11007, TxtHidden = 11005, TxtClaimed = 11004;
        static readonly Color ProofGrey = new Color(0.56f, 0.54f, 0.50f, 0.90f);
        static readonly Color ProofGold = new Color(0.98f, 0.80f, 0.34f, 1f);
        static readonly Color ProofClaimedTagColor = new Color(0.78f, 0.68f, 0.46f, 0.85f);
        RectTransform _proofArea;
        Coroutine _proofAnim;

        /// <summary>一行證明：外層（搖擺用）＋文字。</summary>
        sealed class ProofRow
        {
            public RectTransform Root;
            public Text Label;
            public string Revealed;   // 達成後的金色字（「B級證明：打敗紅嫁衣女殭屍」）
        }

        /// <summary>獎勵內容容器（這版留空）。之後往這底下加「圖示＋名稱＋×數量」的 UI 即可。</summary>
        public RectTransform RewardsArea => _rewardsArea;

        protected override void OnBuild()
        {
            // 底圖（關卡 loading 圖）＋壓暗
            _bg = UIBuilder.Image(transform, "ResultBg", null, Color.white);
            UIBuilder.Stretch(_bg.rectTransform);
            _bg.raycastTarget = false;
            var dim = UIBuilder.SolidPanel(transform, "Dim", new Color(0f, 0f, 0f, 0.45f));
            dim.raycastTarget = false;

            // 標題：過關用「通關結算」圖、死亡用「死亡結算」圖；兩者皆缺圖時退回文字。（往上提，避免壓到關卡名）
            _titleImg = MakeArt("Title", LoadSprite(TextDir + "ClearStagePanel_Title"), 760f, new Vector2(0f, 422f));
            _titleDeadImg = MakeArt("TitleDead", LoadSprite(TextDir + "ClearStagePanel_DeadTitle"), 760f, new Vector2(0f, 422f));
            _titleLoseText = MakeText("TitleLose", "殞命", 88, LoseTitleColor, new Vector2(0f, 422f), new Vector2(1200f, 150f));
            _titleLoseText.fontStyle = FontStyle.Bold;

            // 關卡名（動態）——提高、與「獲得獎勵」標頭拉開不重疊
            _subtitle = MakeText("Subtitle", "", 46, SubtitleColor, new Vector2(0f, 288f), new Vector2(1400f, 64f));

            // 獎勵框（位置不動）
            MakeArt("RewardFrame", LoadSprite(ArtDir + "ClearStagePanel_ItemBg"), 1360f, new Vector2(0f, -40f));

            // 空框提示「無」（沒有道具時顯示；置於框內中央、放大好看清）
            _emptyText = MakeText("Empty", "無", 120, new Color(0.86f, 0.80f, 0.66f, 0.92f), new Vector2(0f, -60f), new Vector2(600f, 200f));
            _emptyText.fontStyle = FontStyle.Bold;

            // 「獲得獎勵」標頭（紅底 + 字，疊在框的上緣）——跟著提高一點
            MakeArt("GainBanner", LoadSprite(ArtDir + "ClearStagePanel_GainItemBg"), 440f, new Vector2(0f, 195f));
            MakeArt("GainText", LoadSprite(TextDir + "ClearStagePanel_GainItemText"), 300f, new Vector2(0f, 195f));

            _tip = ItemTooltip.Create(transform, UILayer.System);

            // 獎勵格容器（內容每次開結算時重建）
            var areaGo = UIBuilder.Create("RewardsArea", transform);
            _rewardsArea = (RectTransform)areaGo.transform;
            _rewardsArea.anchorMin = _rewardsArea.anchorMax = _rewardsArea.pivot = new Vector2(0.5f, 0.5f);
            _rewardsArea.sizeDelta = new Vector2(1200f, 230f);
            _rewardsArea.anchoredPosition = new Vector2(0f, -70f);

            // 返回廣場按鈕（底圖 + 字圖）。字做成按鈕的子物件、往上移到紅牌匾中心（底圖下半是流蘇、牌匾偏上）。
            var btn = UIBuilder.Button(transform, "BackToHub", "",
                RequestReturn,
                Color.white, LoadSprite(ArtDir + "ClearStagePanel_ReturnBtn"));
            var brt = btn.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(440f, 110f);
            brt.anchoredPosition = new Vector2(0f, BackBtnY);
            _backBtnRt = brt;

            // 劇本證明三行的容器（內容每次開結算時重建）
            var pa = UIBuilder.Create("ProofArea", transform);
            _proofArea = (RectTransform)pa.transform;
            _proofArea.anchorMin = _proofArea.anchorMax = _proofArea.pivot = new Vector2(0.5f, 0.5f);
            _proofArea.sizeDelta = new Vector2(ProofRowW, ProofRowGap * 3f);
            _proofArea.anchoredPosition = Vector2.zero;

            var retSp = LoadSprite(TextDir + "ClearStagePanel_ReturnText");
            var retTxt = UIBuilder.Image(btn.transform, "ReturnText", retSp, Color.white);
            retTxt.raycastTarget = false;
            var rtxt = retTxt.rectTransform;
            rtxt.anchorMin = rtxt.anchorMax = rtxt.pivot = new Vector2(0.5f, 0.5f);
            float rtw = 250f;
            float rth = (retSp != null && retSp.rect.width > 0.01f) ? rtw * retSp.rect.height / retSp.rect.width : rtw * 0.33f;
            rtxt.sizeDelta = new Vector2(rtw, rth);
            rtxt.anchoredPosition = new Vector2(0f, 7f);   // 上移到紅牌匾中心（見量測：牌匾中心高於整圖中心）
            if (retSp == null) retTxt.enabled = false;
        }

        // 建一張美術圖：寬固定、高依原圖比例自動（不變形）；置中於指定位置。sprite 為 null 則隱藏。
        Image MakeArt(string name, Sprite sp, float width, Vector2 pos)
        {
            var img = UIBuilder.Image(transform, name, sp, Color.white);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            float h = (sp != null && sp.rect.width > 0.01f) ? width * sp.rect.height / sp.rect.width : width * 0.25f;
            rt.sizeDelta = new Vector2(width, h);
            rt.anchoredPosition = pos;
            if (sp == null) img.enabled = false;
            return img;
        }

        Text MakeText(string name, string content, int fontSize, Color color, Vector2 pos, Vector2 size)
        {
            var t = UIBuilder.Text(transform, name, content, fontSize, color, TextAnchor.MiddleCenter);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            t.raycastTarget = false;
            return t;
        }

        // Resources 載圖：優先 Sprite；匯入型別是 Texture 就自己 Create；都失敗回 null（呼叫端隱藏該圖）。
        static Sprite LoadSprite(string path)
        {
            // ⚠ UI/Texts/ 底下的是「圖片型文字」：實際檔案在 UI/Texts/<語言>/ 裡，
            //    這裡改寫成當前語言的路徑，缺當前語言就退回母版（繁中）。見 Localization/LocalizedArt。
            path = Dipan.Localization.LocalizedArt.ResolveExisting(path);

            var sp = Resources.Load<Sprite>(path);
            if (sp != null) return sp;
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null) return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            Debug.LogWarning($"[ResultPanel] 載不到美術 Resources/{path}（型別非 Sprite/Texture？）。");
            return null;
        }

        /// <summary>
        /// 開啟結算畫面。win＝過關（通關結算圖）／false＝死亡（死亡結算圖）。
        /// showTitle＝false 時不顯示任何標題（主動返回廣場用）。module 決定底圖、displayName 顯示關卡名。
        /// </summary>
        public static void Show(bool win, bool showTitle, string module, string displayName)
            => Show(win, showTitle, module, displayName, null);

        /// <summary>
        /// 開啟結算畫面並在獎勵區顯示 rewards（null/空＝顯示「無」）。2026-11-16 起 GameFlowManager 傳這一趟的功德值、新證明、所有取得道具。
        /// rewards 是完整的 ItemStack，所以有孔的裝備會在圖示下方標出孔數。
        /// </summary>
        public static void Show(bool win, bool showTitle, string module, string displayName,
                                IList<ItemStack> rewards)
        {
            var p = UIManager.Instance?.Open<ResultPanel>();
            p?.Setup(win, showTitle, module, displayName);
            p?.PopulateRewards(rewards);
            p?.BuildProofRows(module);
        }

        /// <summary>把過關落袋的道具鋪進獎勵區（圖示＋×數量、置中排列、自動換行）。空 → 顯示大「無」字。</summary>
        void PopulateRewards(IList<ItemStack> rewards)
        {
            if (_rewardsArea == null) return;

            // 清掉上一次的獎勵格子（面板常駐複用）。
            for (int i = _rewardsArea.childCount - 1; i >= 0; i--)
                Destroy(_rewardsArea.GetChild(i).gameObject);
            _rewardHover.Clear();
            _hoverIdx = -1;
            _tip?.Hide();

            int n = rewards != null ? rewards.Count : 0;
            if (n > 0)
            {
                var inv = InventorySystem.Instance;
                float cell = 120f, gap = 24f, iconSize = 84f, rowH = 120f;
                int cntFont = 30;
                float areaW = _rewardsArea.sizeDelta.x;
                int perRow = Mathf.Max(1, Mathf.FloorToInt((areaW + gap) / (cell + gap)));
                int rows = Mathf.CeilToInt(n / (float)perRow);
                if (rows > 2)
                {
                    // 東西多（超過兩排）就整體縮小，塞得進獎勵框（2026-11-16 開始列出這一趟所有收穫）
                    cell = 92f; gap = 14f; iconSize = 62f; rowH = 92f; cntFont = 24;
                    perRow = Mathf.Max(1, Mathf.FloorToInt((areaW + gap) / (cell + gap)));
                    rows = Mathf.CeilToInt(n / (float)perRow);
                }

                for (int i = 0; i < n; i++)
                {
                    var kv = rewards[i];
                    var data = inv != null ? inv.GetData(kv.ItemId) : null;
                    int row = i / perRow, col = i % perRow;
                    int rowCount = Mathf.Min(perRow, n - row * perRow);
                    float totalW = rowCount * cell + (rowCount - 1) * gap;
                    float x = -totalW / 2f + cell / 2f + col * (cell + gap);
                    float y = (rows - 1) * rowH * 0.5f - row * rowH;

                    // 圖示
                    var icon = UIBuilder.Image(_rewardsArea, $"Reward{i}", null, Color.white);
                    icon.raycastTarget = false;
                    icon.preserveAspect = true;
                    var irt = icon.rectTransform;
                    irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
                    irt.sizeDelta = new Vector2(iconSize, iconSize);
                    irt.anchoredPosition = new Vector2(x, y + 12f);
                    ItemIcons.Apply(icon, kv);   // 珠子會連能力符號一起畫（見 readme/GEM_SOCKET.md）
                    _rewardHover.Add(new KeyValuePair<RectTransform, ItemStack>(irt, kv));

                    // ×數量（有孔的裝備改標孔數，讓玩家一眼看到這趟打到什麼好貨）
                    string label = $"×{kv.Count:N0}";
                    if (kv.Inst != null)
                    {
                        if (kv.Inst.HasSockets && kv.Inst.UnlockedCount > 0) label = $"{kv.Inst.UnlockedCount} 孔";
                        else if (kv.Inst.level > 0) label = $"Lv{kv.Inst.level}";
                    }
                    var ct = UIBuilder.Text(_rewardsArea, $"RewardCnt{i}", label, cntFont,
                        new Color(0.96f, 0.90f, 0.66f, 1f), TextAnchor.MiddleCenter);
                    var crt = ct.rectTransform;
                    crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
                    crt.sizeDelta = new Vector2(cell, 34f);
                    crt.anchoredPosition = new Vector2(x, y - iconSize * 0.5f - 6f);
                    ct.raycastTarget = false;
                }
            }

            if (_emptyText != null) _emptyText.enabled = _rewardsArea.childCount == 0;
        }

        void Setup(bool win, bool showTitle, string module, string displayName)
        {
            _returnRequested = false;
            // 底圖：Resources/Loading/<module>.png（找不到＝純黑底）
            Sprite sp = string.IsNullOrEmpty(module) ? null : Resources.Load<Sprite>($"Loading/{module}");
            _bg.sprite = sp;
            _bg.color = sp != null ? Color.white : new Color(0.03f, 0.02f, 0.02f, 1f);
            _bg.enabled = true;

            // 標題：showTitle=false（主動返回）完全不顯示；否則過關用通關圖、死亡用死亡圖，對應圖缺了才退回文字。
            bool hasWinArt = _titleImg.sprite != null;
            bool hasDeadArt = _titleDeadImg.sprite != null;
            _titleImg.enabled = showTitle && win && hasWinArt;
            _titleDeadImg.enabled = showTitle && !win && hasDeadArt;
            _titleLoseText.enabled = showTitle && ((win && !hasWinArt) || (!win && !hasDeadArt));
            _titleLoseText.text = win ? "通關結算" : "殞命";

            string lv = string.IsNullOrEmpty(displayName) ? module : displayName;
            _subtitle.text = string.IsNullOrEmpty(lv) ? "" : $"關卡：{lv}";

            // 獎勵格沒有任何道具時，框內顯示大「無」字。
            if (_emptyText != null) _emptyText.enabled = _rewardsArea == null || _rewardsArea.childCount == 0;
        }
            // ══════════════════ 劇本證明三行 ══════════════════

        // 保險：面板被停用（淡出結束、或被其他流程直接關掉）時一定收掉 tooltip——它掛在 System 層，不會跟著面板消失。
        void OnDisable()
        {
            _tip?.Hide();
            _hoverIdx = -1;
        }

        protected override void OnClose()
        {
            if (_proofAnim != null) { StopCoroutine(_proofAnim); _proofAnim = null; }
            _tip?.Hide();
            _hoverIdx = -1;
        }

        // 「返回廣場」：按鈕與 ESC 共用，同一次結算只送一次（連按不會重複跑返回流程）。
        bool _returnRequested;

        void RequestReturn()
        {
            if (_returnRequested) return;
            _returnRequested = true;
            _tip?.Hide();
            Dipan.Flow.GameFlowManager.Instance?.ReturnToHubFromResult();
        }

        // 獎勵格 hover → 物品說明（游標在哪個圖示的方框內就顯示哪個；用真實游標座標，暫停中照樣運作）。
        // ESC（2026-11-16 作者要求所有介面都能 ESC 關）＝返回廣場。這個面板不入 UIManager 的視窗堆疊（InStack=false），
        // 所以 UIManager 的 ESC 不會處理它，在這裡自己接。
        void Update()
        {
            if (IsOpen && !_returnRequested && Input.GetKeyDown(KeyCode.Escape)) { RequestReturn(); return; }
            if (_tip == null) return;
            // ⚠ 關閉後還有 0.12 秒淡出，這段時間面板仍是 active、Update 照跑——不擋的話游標若停在圖示上，
            //   OnClose 剛收掉的 tooltip 會被這裡再打開，接著面板被停用、再也沒人收它 ⇒ 說明框一路掛到邪佛廣場
            //   （2026-11-16 作者實測，PROBLEMS E49）。所以「已關閉」就一律收掉、不再判斷 hover。
            if (!IsOpen)
            {
                if (_hoverIdx >= 0) { _tip.Hide(); _hoverIdx = -1; }
                return;
            }
            int hit = -1;
            for (int i = 0; i < _rewardHover.Count; i++)
            {
                var rt = _rewardHover[i].Key;
                if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null)) { hit = i; break; }
            }
            if (hit != _hoverIdx)
            {
                _hoverIdx = hit;
                if (hit >= 0) _tip.Show(_rewardHover[hit].Value);
                else _tip.Hide();
            }
            if (hit >= 0) _tip.Follow();
        }

        /// <summary>
        /// 依「這一趟新拿到的」與「這個角色領過的」重建三行（B／A／S 由上到下）：
        /// 沒達成＝灰「?????」；以前就領過＝一開始就是金色條件字＋「已領取」；這一趟新拿到＝先灰色，再依序播搖擺→破碎→變金。
        /// 過關、死亡、中途離開一律顯示（資料來源 ScriptRecords，見 readme/SAVE_SYSTEM.md §15）。
        /// </summary>
        void BuildProofRows(string module)
        {
            if (_proofArea == null) return;
            if (_proofAnim != null) { StopCoroutine(_proofAnim); _proofAnim = null; }
            for (int i = _proofArea.childCount - 1; i >= 0; i--) Destroy(_proofArea.GetChild(i).gameObject);

            bool show = ProofTable.HasModule(module);
            _proofArea.gameObject.SetActive(show);
            if (_backBtnRt != null) _backBtnRt.anchoredPosition = new Vector2(0f, show ? BackBtnYWithProofs : BackBtnY);
            if (!show) return;

            var pending = new List<ProofRow>();
            int idx = 0;
            foreach (var tier in ProofTierInfo.All)
            {
                var row = MakeProofRow(tier, idx++);
                string name = ProofTierInfo.Name(tier);
                row.Revealed = Fmt(name, ProofTable.ConditionText(module, tier));
                bool newly = ScriptRecords.GrantedThisRunContains(module, tier);
                bool claimed = ScriptRecords.IsClaimed(module, tier);

                if (claimed && !newly)
                {
                    // 以前就領過：靜態金色＋「已領取」
                    row.Label.text = row.Revealed + $"  <size={ProofFont - 8}><color=#{ColorUtility.ToHtmlStringRGBA(ProofClaimedTagColor)}>（{Language.GetText(TxtClaimed)}）</color></size>";
                    row.Label.color = ProofGold;
                }
                else
                {
                    row.Label.text = Fmt(name, Language.GetText(TxtHidden));
                    row.Label.color = ProofGrey;
                    if (newly) pending.Add(row);
                }
            }
            if (pending.Count > 0) _proofAnim = StartCoroutine(RevealRoutine(pending));
        }

        static string Fmt(string name, string body)
        {
            string f = Language.GetText(TxtNameFmt);
            return f.Contains("{0}") ? string.Format(f, name, body) : name + "：" + body;
        }

        ProofRow MakeProofRow(ProofTier tier, int index)
        {
            var go = UIBuilder.Create("Proof_" + ProofTierInfo.Key(tier), _proofArea);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(ProofRowW, ProofRowH);
            rt.anchoredPosition = new Vector2(0f, ProofRowY0 - index * ProofRowGap);

            var t = UIBuilder.Text(rt, "Label", "", ProofFont, ProofGrey, TextAnchor.MiddleCenter);
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sh.effectDistance = new Vector2(2f, -2f);
            return new ProofRow { Root = rt, Label = t };
        }

        // 依序揭示：搖擺 → 破碎（字一顆顆飛散＋碎片）→ 金色字彈出。全部用真實時間（結算畫面暫停遊戲）。
        IEnumerator RevealRoutine(List<ProofRow> rows)
        {
            yield return Wait(ProofStartDelay);
            foreach (var row in rows)
            {
                if (row.Root == null) yield break;
                yield return Shake(row);
                Shatter(row);
                yield return Wait(ProofShatterTime * 0.45f);
                yield return Pop(row);
                yield return Wait(ProofBetween);
            }
            _proofAnim = null;
        }

        static IEnumerator Wait(float sec)
        {
            float t = 0f;
            while (t < sec) { t += Time.unscaledDeltaTime; yield return null; }
        }

        // 搖擺：越搖越兇（左右晃＋小角度擺動），結束時歸位。
        static IEnumerator Shake(ProofRow row)
        {
            var rt = row.Root;
            Vector2 home = rt.anchoredPosition;
            float t = 0f;
            while (t < ProofShakeTime && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / ProofShakeTime);
                float amp = Mathf.Lerp(1.5f, 7f, k);
                rt.anchoredPosition = home + new Vector2(Mathf.Sin(t * 55f) * amp, Mathf.Sin(t * 37f) * amp * 0.35f);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 43f) * amp * 0.45f);
                yield return null;
            }
            if (rt != null) { rt.anchoredPosition = home; rt.localRotation = Quaternion.identity; }
        }

        // 破碎：把整行灰字拆成一顆顆字往外飛（帶重力、旋轉、淡出），再噴幾片碎屑；原本那行字立刻藏起來。
        void Shatter(ProofRow row)
        {
            var label = row.Label;
            string plain = label.text;
            int n = plain.Length;
            float width = label.preferredWidth;
            float charW = n > 0 ? width / n : 0f;
            Vector2 center = row.Root.anchoredPosition;
            label.enabled = false;

            for (int i = 0; i < n; i++)
            {
                if (char.IsWhiteSpace(plain[i])) continue;
                var c = UIBuilder.Text(_proofArea, "Frag", plain[i].ToString(), ProofFont, ProofGrey, TextAnchor.MiddleCenter);
                c.raycastTarget = false;
                c.horizontalOverflow = HorizontalWrapMode.Overflow;
                var crt = c.rectTransform;
                crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
                crt.sizeDelta = new Vector2(Mathf.Max(charW, 10f) + 8f, ProofRowH);
                crt.anchoredPosition = center + new Vector2(-width * 0.5f + charW * (i + 0.5f), 0f);
                var v = new Vector2(Random.Range(-260f, 260f), Random.Range(120f, 420f));
                StartCoroutine(Fly(crt, c, v, Random.Range(-540f, 540f)));
            }
            for (int i = 0; i < 14; i++)
            {
                var img = UIBuilder.Image(_proofArea, "Shard", null, i % 3 == 0 ? ProofGold : ProofGrey);
                img.raycastTarget = false;
                var irt = img.rectTransform;
                irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
                float sz = Random.Range(4f, 10f);
                irt.sizeDelta = new Vector2(sz, sz * Random.Range(0.4f, 1.2f));
                irt.anchoredPosition = center + new Vector2(Random.Range(-width * 0.5f, width * 0.5f), Random.Range(-10f, 10f));
                var v = new Vector2(Random.Range(-320f, 320f), Random.Range(80f, 460f));
                StartCoroutine(Fly(irt, img, v, Random.Range(-720f, 720f)));
            }
        }

        static IEnumerator Fly(RectTransform rt, Graphic g, Vector2 vel, float spin)
        {
            const float Gravity = -1400f;
            float t = 0f;
            Color c0 = g.color;
            while (t < ProofShatterTime && rt != null)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                vel.y += Gravity * dt;
                rt.anchoredPosition += vel * dt;
                rt.localRotation = Quaternion.Euler(0f, 0f, rt.localEulerAngles.z + spin * dt);
                float a = 1f - Mathf.Clamp01(t / ProofShatterTime);
                g.color = new Color(c0.r, c0.g, c0.b, c0.a * a);
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        // 變金：換成條件字，從大縮回原尺寸（彈一下），背後閃一道金光。
        IEnumerator Pop(ProofRow row)
        {
            var label = row.Label;
            label.text = row.Revealed;
            label.color = ProofGold;
            label.enabled = true;

            var flash = UIBuilder.Image(row.Root, "Flash", null, new Color(ProofGold.r, ProofGold.g, ProofGold.b, 0.45f));
            flash.raycastTarget = false;
            flash.transform.SetAsFirstSibling();
            var frt = flash.rectTransform;
            frt.anchorMin = frt.anchorMax = frt.pivot = new Vector2(0.5f, 0.5f);
            frt.sizeDelta = new Vector2(Mathf.Min(label.preferredWidth + 60f, ProofRowW), ProofRowH * 0.9f);

            var rt = row.Root;
            float t = 0f;
            while (t < ProofPopTime && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / ProofPopTime);
                float s = Mathf.Lerp(1.35f, 1f, 1f - (1f - k) * (1f - k));   // ease-out
                rt.localScale = new Vector3(s, s, 1f);
                if (flash != null) flash.color = new Color(ProofGold.r, ProofGold.g, ProofGold.b, 0.45f * (1f - k));
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
            if (flash != null) Destroy(flash.gameObject);
        }
    }
}
