using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 通關／死亡結算畫面（覆蓋層、暫停遊戲、擋輸入）。全程式建構。
    /// 底圖＝該關卡的 loading 圖（Resources/Loading/&lt;module&gt;.png）＋壓暗；其餘用美術元件
    /// （Resources/UI/ClearStagePanel/*）：標題、獎勵框、「獲得獎勵」標頭、返回廣場按鈕。
    ///
    /// 過關＝顯示「通關結算」標題圖；死亡＝沒有專屬圖 → 退回文字「殞命」（暗紅）。
    /// 獎勵格區（<see cref="RewardsArea"/>）這版**仍留空**，之後把「圖示＋名稱＋×數量」塞進去即可。
    /// 開關由 GameFlowManager 驅動；「返回廣場」→ GameFlowManager.ReturnToHubFromResult()。
    /// </summary>
    public class ResultPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Overlay;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;

        const string ArtDir = "UI/ClearStagePanel/";
        static readonly Color LoseTitleColor = new Color(0.82f, 0.20f, 0.20f, 1f);
        static readonly Color SubtitleColor  = new Color(0.94f, 0.80f, 0.42f, 1f);   // 金

        Image _bg, _titleImg, _titleDeadImg;
        Text _titleLoseText, _subtitle, _emptyText;
        RectTransform _rewardsArea;

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
            _titleImg = MakeArt("Title", LoadSprite(ArtDir + "ClearStagePanel_Title"), 760f, new Vector2(0f, 422f));
            _titleDeadImg = MakeArt("TitleDead", LoadSprite(ArtDir + "ClearStagePanel_DeadTitle"), 760f, new Vector2(0f, 422f));
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
            MakeArt("GainText", LoadSprite(ArtDir + "ClearStagePanel_GainItemText"), 300f, new Vector2(0f, 195f));

            // 獎勵格容器（留空，之後往這塞獎勵）
            var areaGo = UIBuilder.Create("RewardsArea", transform);
            _rewardsArea = (RectTransform)areaGo.transform;
            _rewardsArea.anchorMin = _rewardsArea.anchorMax = _rewardsArea.pivot = new Vector2(0.5f, 0.5f);
            _rewardsArea.sizeDelta = new Vector2(1200f, 230f);
            _rewardsArea.anchoredPosition = new Vector2(0f, -70f);

            // 返回廣場按鈕（底圖 + 字圖）。字做成按鈕的子物件、往上移到紅牌匾中心（底圖下半是流蘇、牌匾偏上）。
            var btn = UIBuilder.Button(transform, "BackToHub", "",
                () => Dipan.Flow.GameFlowManager.Instance?.ReturnToHubFromResult(),
                Color.white, LoadSprite(ArtDir + "ClearStagePanel_ReturnBtn"));
            var brt = btn.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(440f, 110f);
            brt.anchoredPosition = new Vector2(0f, -430f);

            var retSp = LoadSprite(ArtDir + "ClearStagePanel_ReturnText");
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
        /// 開啟結算畫面並在獎勵區顯示這趟落袋的臨時包內容（null/空＝顯示「無」）。
        /// rewards 是完整的 ItemStack，所以有孔的裝備會在圖示下方標出孔數。
        /// </summary>
        public static void Show(bool win, bool showTitle, string module, string displayName,
                                IList<ItemStack> rewards)
        {
            var p = UIManager.Instance?.Open<ResultPanel>();
            p?.Setup(win, showTitle, module, displayName);
            p?.PopulateRewards(rewards);
        }

        /// <summary>把過關落袋的道具鋪進獎勵區（圖示＋×數量、置中排列、自動換行）。空 → 顯示大「無」字。</summary>
        void PopulateRewards(IList<ItemStack> rewards)
        {
            if (_rewardsArea == null) return;

            // 清掉上一次的獎勵格子（面板常駐複用）。
            for (int i = _rewardsArea.childCount - 1; i >= 0; i--)
                Destroy(_rewardsArea.GetChild(i).gameObject);

            int n = rewards != null ? rewards.Count : 0;
            if (n > 0)
            {
                var inv = InventorySystem.Instance;
                const float cell = 120f, gap = 24f, iconSize = 84f, rowH = 120f;
                float areaW = _rewardsArea.sizeDelta.x;
                int perRow = Mathf.Max(1, Mathf.FloorToInt((areaW + gap) / (cell + gap)));
                int rows = Mathf.CeilToInt(n / (float)perRow);

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

                    // ×數量（有孔的裝備改標孔數，讓玩家一眼看到這趟打到什麼好貨）
                    string label = $"×{kv.Count}";
                    if (kv.Inst != null)
                    {
                        if (kv.Inst.HasSockets && kv.Inst.UnlockedCount > 0) label = $"{kv.Inst.UnlockedCount} 孔";
                        else if (kv.Inst.level > 0) label = $"Lv{kv.Inst.level}";
                    }
                    var ct = UIBuilder.Text(_rewardsArea, $"RewardCnt{i}", label, 30,
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
    }
}
