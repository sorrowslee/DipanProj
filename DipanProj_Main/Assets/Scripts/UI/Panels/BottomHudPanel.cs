using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 底部操控列 HUD：整條石雕框（含燃燈佛、法輪、血瓶槽）＋ 左 HP 紅球、右 MP 藍球（液體血球，見 <see cref="LiquidOrb"/>）。
    ///
    /// 特性：HUD 層、不暫停、不擋輸入、不遮罩、不入 ESC 堆疊、換場景保留——純呈現（同舊 <see cref="HudPanel"/>）。
    /// 資料來源＝玩家身上的 <see cref="CombatStats"/>（每幀讀 Health/Mana；玩家死亡/換地圖重生會自動重找）。
    /// 由 <c>PlayerController.Start</c> 開啟。全程式建構、零 prefab/Inspector 接線。
    ///
    /// 版面座標＝框圖 <c>BottomControlPanel_Bg.png</c>（原圖 2172×724）的像素，載入時依 <see cref="DisplayWidth"/> 等比縮放。
    /// 兩顆血球畫在框「之上」、剛好蓋住框圖裡實心畫的紅/藍球，停在 socket 邊緣（框沒鏤空、不必重切圖）。
    /// 見 readme/COMBAT.md、readme/UI_SYSTEM.md。
    /// </summary>
    public class BottomHudPanel : UIPanel
    {
        public override UILayer Layer => UILayer.HUD;
        public override bool PausesGame => false;
        public override bool BlocksGameplayInput => false;
        public override bool ShowBackdrop => false;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;
        public override bool KeepOpenOnSceneChange => true;

        // ── 素材 ──
        const string FrameSprite = "UI/BottomControlPanel/BottomControlPanel_Bg";

        // ── 版面（框圖像素；原圖 2172×724，不透明內容 y[109,606]）──
        const float ArtW = 2172f, ArtH = 724f;
        const float ArtBottomOpaque = 606f;                 // 不透明內容底邊（用來對齊螢幕底）

        static readonly Vector2 HpCenter = new Vector2(210f, 350f);   // 紅球圓心
        static readonly Vector2 MpCenter = new Vector2(1980f, 350f);  // 藍球圓心
        const float OrbRadius = 115f;                       // 球半徑（框圖像素）
        const float OrbCoverScale = 1.0f;                   // 液體圓盤相對實心球的大小（1=剛好蓋住，<1 露出畫邊）

        // 血瓶槽內框（框圖像素；先建空錨點，日後放血瓶 icon / 點擊區）
        static readonly Vector2 Slot0Center = new Vector2(986f, 416f);
        static readonly Vector2 Slot1Center = new Vector2(1141f, 416f);
        static readonly Vector2 SlotSize    = new Vector2(105f, 128f);

        // ── 螢幕呈現（實機可調）──
        const float DisplayWidth = 1180f;                   // 整條在螢幕上的寬度（高度依原圖比例）
        const float BottomPad    = 0f;                      // 內容底邊離螢幕底的像素（往上推為正）

        // ── 液體顏色（更暗版；配合 LiquidOrb 的 Brightness 旋鈕。要更亮/更暗改這四個或 LiquidOrb.Brightness）──
        static readonly Color HpLiquid = new Color(0.50f, 0.07f, 0.06f, 1f);
        static readonly Color HpDeep   = new Color(0.15f, 0.010f, 0.020f, 1f);
        static readonly Color MpLiquid = new Color(0.08f, 0.17f, 0.46f, 1f);
        static readonly Color MpDeep   = new Color(0.012f, 0.03f, 0.18f, 1f);

        float _scale;
        RectTransform _frame;
        LiquidOrb _hp, _mp;
        CombatStats _stats;

        protected override void OnBuild()
        {
            _scale = DisplayWidth / ArtW;

            // 框：底部置中。pivot 底中，往下拉到不透明內容底邊貼齊螢幕底。
            var frameImg = UIBuilder.Image(transform, "Frame", UIBuilder.LoadSprite(FrameSprite), Color.white);
            frameImg.raycastTarget = false;
            _frame = frameImg.rectTransform;
            _frame.anchorMin = _frame.anchorMax = new Vector2(0.5f, 0f);
            _frame.pivot = new Vector2(0.5f, 0f);
            _frame.sizeDelta = new Vector2(ArtW * _scale, ArtH * _scale);
            _frame.anchoredPosition = new Vector2(0f, -(ArtH - ArtBottomOpaque) * _scale + BottomPad);

            // 兩顆液體血球（框的子物件、畫在框之上）
            _hp = MakeOrb("HpOrb", HpCenter, HpLiquid, HpDeep, "HP");
            _mp = MakeOrb("MpOrb", MpCenter, MpLiquid, MpDeep, "MP");

            // 血瓶槽錨點（暫空）
            MakeSlotAnchor("PotionSlot0", Slot0Center);
            MakeSlotAnchor("PotionSlot1", Slot1Center);
        }

        LiquidOrb MakeOrb(string name, Vector2 artCenter, Color liquid, Color deep, string label)
        {
            var go = UIBuilder.Create(name, _frame);
            ArtRect(go, artCenter, new Vector2(2f * OrbRadius * OrbCoverScale, 2f * OrbRadius * OrbCoverScale));
            var orb = go.AddComponent<LiquidOrb>();
            orb.Init(liquid, deep, label);
            return orb;
        }

        RectTransform MakeSlotAnchor(string name, Vector2 artCenter)
        {
            var go = UIBuilder.Create(name, _frame);
            return ArtRect(go, artCenter, SlotSize);
        }

        // 用「框圖像素座標（原點左上、y 向下）」擺一個子物件，中心對準 artCenter、尺寸 artSize。
        RectTransform ArtRect(GameObject go, Vector2 artCenter, Vector2 artSize)
        {
            var rt = UIBuilder.Rect(go);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // 以框左上角為原點
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(artCenter.x * _scale, -artCenter.y * _scale);
            rt.sizeDelta = new Vector2(artSize.x * _scale, artSize.y * _scale);
            return rt;
        }

        void Update()
        {
            EnsureStats();
            if (_stats == null) return;
            if (_hp != null) _hp.SetStats(_stats.Health, _stats.MaxHealth);
            if (_mp != null) _mp.SetStats(_stats.Mana, _stats.MaxMana);
        }

        // 找玩家身上的 CombatStats（快取；玩家死亡/換地圖重生後自動重找）
        void EnsureStats()
        {
            if (_stats != null) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _stats = player.GetComponent<CombatStats>();
        }
    }
}
