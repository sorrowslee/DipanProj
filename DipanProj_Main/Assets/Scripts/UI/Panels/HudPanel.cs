using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 抬頭顯示：左上角的血量（紅）＋ 魔力（藍）條。
    ///
    /// 特性：HUD 層、**不暫停、不擋輸入、不遮罩、不入 ESC 堆疊**——純呈現。
    /// 資料來源 = 玩家身上的 <see cref="CombatStats"/>（每幀讀其 Health/Mana，玩家死亡/重生會自動重找）。
    /// 由 PlayerController 在 Start 時 Open。風格對齊專案：全程式建構、零 prefab/Inspector 接線。
    /// 見 readme/COMBAT.md、readme/UI_SYSTEM.md。
    /// </summary>
    public class HudPanel : UIPanel
    {
        public override UILayer Layer => UILayer.HUD;
        public override bool PausesGame => false;
        public override bool BlocksGameplayInput => false;
        public override bool ShowBackdrop => false;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;
        public override bool KeepOpenOnSceneChange => true;

        const float BarWidth = 260f;
        const float BarHeight = 24f;
        const float Gap = 8f;
        const float Inset = 2f;   // 填色條相對外框的內縮

        static readonly Color HpColor = new Color(0.82f, 0.18f, 0.18f, 0.95f);
        static readonly Color MpColor = new Color(0.20f, 0.45f, 0.95f, 0.95f);
        static readonly Color BgColor = new Color(0f, 0f, 0f, 0.6f);

        RectTransform _hpFill, _mpFill;
        Text _hpLabel, _mpLabel;
        CombatStats _stats;

        protected override void OnBuild()
        {
            var root = UIBuilder.Create("HudRoot", transform);
            var rt = UIBuilder.Rect(root);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // 螢幕左上
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);
            rt.sizeDelta = new Vector2(BarWidth, BarHeight * 2f + Gap);

            BuildBar(root.transform, 0f, HpColor, out _hpFill, out _hpLabel, "HP");
            BuildBar(root.transform, BarHeight + Gap, MpColor, out _mpFill, out _mpLabel, "MP");
        }

        void BuildBar(Transform parent, float yOffset, Color fillColor,
            out RectTransform fill, out Text label, string name)
        {
            var row = UIBuilder.Create(name + "Bar", parent);
            var rrt = UIBuilder.Rect(row);
            rrt.anchorMin = rrt.anchorMax = new Vector2(0f, 1f);
            rrt.pivot = new Vector2(0f, 1f);
            rrt.anchoredPosition = new Vector2(0f, -yOffset);
            rrt.sizeDelta = new Vector2(BarWidth, BarHeight);

            // 外框（深色半透明）
            var bg = UIBuilder.Image(row.transform, "BG", null, BgColor);
            UIBuilder.Stretch(UIBuilder.Rect(bg));

            // 填色條：靠左、上下內縮 Inset，寬度每幀依比例設定
            var fillImg = UIBuilder.Image(row.transform, "Fill", null, fillColor);
            fill = UIBuilder.Rect(fillImg);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = new Vector2(Inset, 0f);
            fill.sizeDelta = new Vector2(BarWidth - Inset * 2f, -Inset * 2f);

            // 數值文字（蓋在條上、不吃點擊）
            label = UIBuilder.Text(row.transform, "Label", name, 15, Color.white, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            var lrt = UIBuilder.Rect(label);
            UIBuilder.Stretch(lrt);
        }

        void Update()
        {
            EnsureStats();
            if (_stats == null) return;
            SetBar(_hpFill, _hpLabel, _stats.Health, _stats.MaxHealth, "HP");
            SetBar(_mpFill, _mpLabel, _stats.Mana, _stats.MaxMana, "MP");
        }

        void SetBar(RectTransform fill, Text label, float current, float max, string name)
        {
            if (fill == null) return;
            float ratio = (max > 0f) ? Mathf.Clamp01(current / max) : 0f;
            fill.sizeDelta = new Vector2((BarWidth - Inset * 2f) * ratio, -Inset * 2f);
            if (label != null) label.text = $"{name}  {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
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
