using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 選擇劇本 UI（邪佛發牌）：全螢幕模態面板，擺出一張或多張「關卡卡片」，玩家按「領取」拿走那張劇本。
    ///
    /// 用途（核心迴圈的 access 軸）：以前邪佛對話後直接用觸發鏈的 giveItem 把紅嫁衣劇本(道具 104)塞進背包；
    /// 現在改成邪佛對話後開啟本面板，玩家在卡片按「領取」→ 把該劇本加進背包 → 關閉 → 之後照現有流程
    /// （走到傳送門 → 把劇本放進傳送門 UI → 開門進關卡）。由觸發鏈動作 <c>selectScript</c> 叫出（見 TriggerChain）。
    ///
    /// 目前階段：**初始關卡＝只有一張紅嫁衣卡置中**（特殊裂紋框）。日後有其他關卡與進度保存後，再擴充成
    /// 「3 新關」或「3 新 + 1 舊（業障回響）」的多卡排版——本面板已支援傳入多張卡（水平置中排列），
    /// 屆時把每輪抽到的關卡清單餵進 <see cref="Open"/> 即可。
    ///
    /// 卡片素材依「關卡 module 名」自動載入（劇本道具 → TargetMapId → MapsTable → module）：
    ///   示意圖  Resources/UI/SelectScriptPanel/StageImage/SelectScriptPanel_StageImage_&lt;module&gt;
    ///   關卡名  Resources/UI/Texts/Text_StageName_&lt;module&gt;
    ///   一般框  Resources/UI/SelectScriptPanel/SelectScriptPanel_Frame
    ///   特殊框  Resources/UI/SelectScriptPanel/SelectScriptPanel_SpecialFrame（業障回響/紅嫁衣用）
    ///   領取鈕  Resources/UI/SelectScriptPanel/SelectScriptPanel_Btn ＋ 字 Resources/UI/Texts/Text_Gain
    ///
    /// 版面數字都是量測常數（實機不合再調本檔上方 const）。設計哲學同其他面板：全程式建構、零 prefab。
    /// </summary>
    public class SelectScriptPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool ShowBackdrop => true;
        public override bool CloseOnEscape => false;   // 領取是唯一出路（沒有關閉/ESC），比照邪佛發牌不可略過

        // ── 素材路徑 ──
        const string PanelDir = "UI/SelectScriptPanel/";
        const string StageDir = "UI/SelectScriptPanel/StageImage/";
        const string TextDir  = "UI/Texts/";

        // ── 版面（量測常數；實機不合再調）──
        const float CardHeight   = 860f;   // 卡片高（寬依框圖比例自動算）
        const float CardAspect   = 0.60f;  // 後備寬高比（載不到框圖時用）：寬 = 高 × 此值
        const float CardGap      = 40f;    // 多卡並排時的水平間距（初始單卡用不到）

        const float StageBoxW    = 440f;   // 卡內示意圖「窗口」寬（示意圖以 preserveAspect 塞進此框；留白＝框內黑底、看不出邊）
        const float StageBoxH    = 500f;   // 卡內示意圖「窗口」高
        const float StageOffsetY = 130f;   // 示意圖窗口中心相對卡中心的 Y（正＝往上，落在框上方窗口）

        const float NameWidth    = 360f;   // 關卡名圖寬（高依原圖比例自動）
        const float NameOffsetY  = -210f;  // 關卡名中心 Y（落在框下方名牌區）

        const float BtnWidth     = 300f;   // 領取鈕寬
        const float BtnHeight    = 96f;    // 領取鈕高
        const float BtnOffsetY   = -330f;  // 領取鈕中心 Y（卡片底部）
        const float GainTextW    = 180f;   // 「領取」字寬（疊在鈕上）

        /// <summary>一張卡片的內容：要領取的劇本道具 id ＋ 是否用特殊（裂紋）框。</summary>
        public struct CardSpec
        {
            public int scriptItemId;
            public bool special;
            public CardSpec(int scriptItemId, bool special) { this.scriptItemId = scriptItemId; this.special = special; }
        }

        // 共用素材（OnBuild 載一次）
        Sprite _frameSprite, _specialFrameSprite, _btnSprite, _gainTextSprite;
        RectTransform _cardsRoot;

        static MapTable _mapTable;
        List<CardSpec> _cards = new List<CardSpec>();
        Action<int> _onClaimed;   // 領取完成回呼（帶被領取的劇本 id）；由開啟端（觸發鏈）接鏈用

        // ───────────────────────── 對外開啟入口 ─────────────────────────

        /// <summary>
        /// 開啟選擇劇本面板。cards＝要擺的卡片（初始＝單張紅嫁衣）；onClaimed＝玩家領取某張後的回呼（帶劇本 id）。
        /// 領取行為＝把該劇本加進背包，之後由 onClaimed 接續（觸發鏈用它接 next）。
        /// </summary>
        public static SelectScriptPanel Open(List<CardSpec> cards, Action<int> onClaimed = null)
        {
            var ui = UIManager.Instance;
            if (ui == null) return null;
            var p = ui.Open<SelectScriptPanel>();
            if (p != null) p.Configure(cards, onClaimed);
            return p;
        }

        /// <summary>初始關卡便捷入口：只擺一張紅嫁衣卡（特殊框）。scriptItemId 預設 104（劇本-紅嫁衣）。</summary>
        public static SelectScriptPanel OpenInitialRedBridalGown(int scriptItemId = 104, Action<int> onClaimed = null)
            => Open(new List<CardSpec> { new CardSpec(scriptItemId, true) }, onClaimed);

        // ───────────────────────── 建構 ─────────────────────────

        protected override void OnBuild()
        {
            _frameSprite        = LoadSprite(PanelDir + "SelectScriptPanel_Frame");
            _specialFrameSprite = LoadSprite(PanelDir + "SelectScriptPanel_SpecialFrame");
            _btnSprite          = LoadSprite(PanelDir + "SelectScriptPanel_Btn");
            _gainTextSprite     = LoadSprite(TextDir  + "Text_Gain");

            // 卡片容器：整層置中，卡片以它為原點左右排列。
            var go = UIBuilder.Create("CardsRoot", transform);
            _cardsRoot = UIBuilder.Rect(go);
            _cardsRoot.anchorMin = _cardsRoot.anchorMax = _cardsRoot.pivot = new Vector2(0.5f, 0.5f);
            _cardsRoot.anchoredPosition = Vector2.zero;
            _cardsRoot.sizeDelta = Vector2.zero;
        }

        /// <summary>設定要擺的卡片與領取回呼，並重建卡片（每次開啟都重建，支援換牌）。</summary>
        void Configure(List<CardSpec> cards, Action<int> onClaimed)
        {
            _cards = cards ?? new List<CardSpec>();
            _onClaimed = onClaimed;
            RebuildCards();
        }

        void RebuildCards()
        {
            if (_cardsRoot == null) return;
            for (int i = _cardsRoot.childCount - 1; i >= 0; i--)
                Destroy(_cardsRoot.GetChild(i).gameObject);

            int n = _cards.Count;
            if (n == 0) return;

            float cardW = CardWidth();
            float totalW = n * cardW + (n - 1) * CardGap;
            float startX = -totalW * 0.5f + cardW * 0.5f;   // 最左卡的中心 X（單卡＝0，置中）

            for (int i = 0; i < n; i++)
            {
                float x = startX + i * (cardW + CardGap);
                BuildCard(_cards[i], x);
            }
        }

        float CardWidth()
        {
            if (_frameSprite != null && _frameSprite.rect.height > 0.01f)
                return CardHeight * _frameSprite.rect.width / _frameSprite.rect.height;
            return CardHeight * CardAspect;
        }

        void BuildCard(CardSpec spec, float centerX)
        {
            float cardW = CardWidth();
            var cardGo = UIBuilder.Create("Card", _cardsRoot);
            var cardRt = UIBuilder.Rect(cardGo);
            UIBuilder.Center(cardRt, cardW, CardHeight, new Vector2(centerX, 0f));

            string module = ModuleOf(spec.scriptItemId);

            // 卡框（一般 / 特殊裂紋）——先放框（含不透明黑底＋邊框）。
            var frameSp = spec.special ? _specialFrameSprite : _frameSprite;
            var frame = UIBuilder.Image(cardRt, "Frame", frameSp,
                                        frameSp != null ? Color.white : new Color(0.16f, 0.13f, 0.14f, 0.96f));
            frame.raycastTarget = false;
            frame.preserveAspect = true;
            UIBuilder.Center(frame.rectTransform, cardW, CardHeight, Vector2.zero);

            // 示意圖：畫在框「之上」的窗口區（框中間是不透明黑底，放框後方會被整片蓋掉——這就是原本看不到圖的原因）。
            // 以 preserveAspect 塞進 StageBoxW×StageBoxH 的窗口框，留白處＝框內黑底、看不出邊。位置/大小調 StageBoxW/H、StageOffsetY。
            var stageSprite = string.IsNullOrEmpty(module) ? null : LoadSprite(StageDir + "SelectScriptPanel_StageImage_" + module);
            if (stageSprite != null)
            {
                var stage = UIBuilder.Image(cardRt, "Stage", stageSprite, Color.white);
                stage.raycastTarget = false;
                stage.preserveAspect = true;
                UIBuilder.Center(stage.rectTransform, StageBoxW, StageBoxH, new Vector2(0f, StageOffsetY));
            }

            // 關卡名（圖）
            var nameSprite = string.IsNullOrEmpty(module) ? null : LoadSprite(TextDir + "Text_StageName_" + module);
            if (nameSprite != null)
                MakeArt(cardRt, "StageName", nameSprite, NameWidth, new Vector2(0f, NameOffsetY));

            // 領取鈕（底圖 ＋「領取」字圖）。點下＝領取這張卡的劇本。
            int scriptId = spec.scriptItemId;
            var btn = UIBuilder.Button(cardRt, "GainBtn", "", () => OnClaim(scriptId), Color.white, _btnSprite);
            var bimg = btn.GetComponent<Image>();
            bimg.preserveAspect = true;
            btn.targetGraphic = bimg;
            UIBuilder.Center((RectTransform)btn.transform, BtnWidth, BtnHeight, new Vector2(0f, BtnOffsetY));

            if (_gainTextSprite != null)
            {
                var gt = UIBuilder.Image(btn.transform, "GainText", _gainTextSprite, Color.white);
                gt.raycastTarget = false;
                gt.preserveAspect = true;
                float w = GainTextW;
                float h = _gainTextSprite.rect.width > 0.01f ? w * _gainTextSprite.rect.height / _gainTextSprite.rect.width : w * 0.35f;
                UIBuilder.Center(gt.rectTransform, w, h, Vector2.zero);
            }
        }

        // ───────────────────────── 領取 ─────────────────────────

        void OnClaim(int scriptItemId)
        {
            var inv = InventorySystem.Instance;
            if (inv != null)
            {
                int leftover = inv.AddItem(scriptItemId, 1);
                var d = inv.GetData(scriptItemId);
                string display = d != null ? d.Name : $"#{scriptItemId}";
                if (leftover <= 0) AlertPanel.Toast($"獲得 {display}");
                else AlertPanel.Toast("背包已滿，無法領取劇本");
            }
            else
            {
                Debug.LogWarning("[SelectScriptPanel] 沒有背包系統，無法領取劇本。");
            }

            var cb = _onClaimed;
            _onClaimed = null;
            UIManager.Instance?.Close(this);
            cb?.Invoke(scriptItemId);   // 面板已關 → 通知開啟端（觸發鏈接 next：鏡頭聚焦/傳送門對話…）
        }

        // ───────────────────────── 小工具 ─────────────────────────

        // 劇本道具 → 目的地關卡(TargetMapId) → MapsTable → module 名（用來組素材路徑）。查不到回 null（卡片仍有框+鈕）。
        static string ModuleOf(int scriptItemId)
        {
            var inv = InventorySystem.Instance;
            var d = inv != null ? inv.GetData(scriptItemId) : null;
            if (d == null || d.TargetMapId <= 0) return null;
            if (_mapTable == null) _mapTable = UnityEngine.Object.FindObjectOfType<MapTable>();
            var row = _mapTable != null ? _mapTable.Get(d.TargetMapId) : null;
            return row != null ? row.module : null;
        }

        // 建一張美術圖：寬固定、高依原圖比例（不變形）、置中於指定位置。
        static Image MakeArt(Transform parent, string name, Sprite sp, float width, Vector2 pos)
        {
            var img = UIBuilder.Image(parent, name, sp, Color.white);
            img.raycastTarget = false;
            img.preserveAspect = true;
            float h = (sp != null && sp.rect.width > 0.01f) ? width * sp.rect.height / sp.rect.width : width;
            UIBuilder.Center(img.rectTransform, width, h, pos);
            return img;
        }

        // Resources 載圖：優先 Sprite；匯入型別是 Texture 就自己 Create；都失敗回 null（呼叫端會隱藏/退回）。
        static Sprite LoadSprite(string path)
        {
            var sp = Resources.Load<Sprite>(path);
            if (sp != null) return sp;
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null) return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            Debug.LogWarning($"[SelectScriptPanel] 載不到美術 Resources/{path}（型別非 Sprite/Texture？）。");
            return null;
        }
    }
}
