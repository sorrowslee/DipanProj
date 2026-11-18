using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Localization;

namespace Dipan.UI
{
    /// <summary>
    /// <b>玩家狀態列</b>（2026-11-18，見 readme/STATUS_EFFECT.md §5）：底部 HUD 框正上方，
    /// <b>debuff＋DoT 一列（靠近 HUD）、buff 一列（在上面）</b>，各自由中間往兩側排。
    /// 每格：外框（buff 暗金、debuff 暗紅，極簡細邊）＋圖示＋剩餘時間的轉圈遮罩＋層數；滑鼠移上去有一行說明（HoverTip）。
    ///
    /// <para>由 <see cref="BottomHudPanel"/> 建在自己底下 ⇒ HUD 關掉（劇情演出隱藏血量 HUD）時一起消失。
    /// 格子用到才建、之後重用；只在狀態增減時重排，每幀只更新轉圈（而且只在身上有狀態時）。</para>
    /// </summary>
    public class StatusBarView : MonoBehaviour
    {
        // ── 版面（畫布座標：1920×1080 參考解析度；離螢幕底的高度）──
        // 底部 HUD 中央最高處約在 244（框圖第 157 列），所以 debuff 列中心放 270、buff 列再往上一格。
        const float IconSize = 40f;
        const float Gap = 6f;
        const float DebuffRowY = 270f;
        const float BuffRowY = DebuffRowY + IconSize + 8f;
        const float Border = 2f;

        static readonly Color BuffBorder = new Color32(0xB0, 0x8A, 0x55, 0xFF);    // 暗古金（UI_ICON_GUIDELINE 色票）
        static readonly Color DebuffBorder = new Color32(0x9A, 0x24, 0x1C, 0xFF);  // 深緋紅
        static readonly Color CellBg = new Color32(0x13, 0x0F, 0x0A, 0xE6);
        static readonly Color SweepColor = new Color(0f, 0f, 0f, 0.55f);

        sealed class Cell
        {
            public RectTransform Rt;
            public Image Border, Icon, Sweep;
            public Text Stack;
            public StatusEffectDef Def;
        }

        RectTransform _root;
        readonly List<Cell> _buff = new List<Cell>();
        readonly List<Cell> _debuff = new List<Cell>();
        readonly List<StatusController.View> _views = new List<StatusController.View>();
        StatusController _sc;
        float _nextFind;
        bool _dirty = true;

        /// <summary>建在 <paramref name="parent"/>（BottomHudPanel 的根）底下。</summary>
        public static StatusBarView Create(Transform parent)
        {
            var go = UIBuilder.Create("StatusBar", parent);
            var rt = UIBuilder.Rect(go);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            var v = go.AddComponent<StatusBarView>();
            v._root = rt;
            return v;
        }

        void OnDisable() => Unbind();
        void OnDestroy() => Unbind();

        void Unbind()
        {
            if (_sc != null) _sc.Changed -= OnChanged;
            _sc = null;
        }

        void OnChanged() => _dirty = true;

        void Update()
        {
            // 找玩家（玩家物件死亡重建／換存檔時會換人）：每秒最多找一次
            if (_sc == null && Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 1f;
                var pc = FindObjectOfType<PlayerController>();
                if (pc != null && pc.Status != null) { _sc = pc.Status; _sc.Changed += OnChanged; _dirty = true; }
            }
            if (_sc == null) { if (_dirty) { HideFrom(_buff, 0); HideFrom(_debuff, 0); _dirty = false; } return; }

            if (_dirty) { _dirty = false; Rebuild(); }
            else if (_sc.Count > 0) UpdateSweeps();
        }

        void Rebuild()
        {
            _sc.GetViews(_views);
            int nb = 0, nd = 0;
            for (int i = 0; i < _views.Count; i++)
            {
                var v = _views[i];
                bool buff = v.Def.Kind == StatusKind.Buff;
                var list = buff ? _buff : _debuff;
                int k = buff ? nb++ : nd++;
                var c = Ensure(list, k, buff);
                c.Def = v.Def;
                var sp = StatusIcons.Get(v.Def);
                c.Icon.sprite = sp;
                c.Icon.enabled = sp != null;
                c.Stack.text = v.Stacks > 1 ? v.Stacks.ToString() : "";
                c.Rt.gameObject.SetActive(true);
            }
            HideFrom(_buff, nb);
            HideFrom(_debuff, nd);
            Layout(_buff, nb, BuffRowY);
            Layout(_debuff, nd, DebuffRowY);
            UpdateSweeps();
        }

        void UpdateSweeps()
        {
            _sc.GetViews(_views);
            for (int i = 0; i < _views.Count; i++)
            {
                var v = _views[i];
                var list = v.Def.Kind == StatusKind.Buff ? _buff : _debuff;
                for (int k = 0; k < list.Count; k++)
                {
                    var c = list[k];
                    if (c.Def != v.Def || !c.Rt.gameObject.activeSelf) continue;
                    // 已經過去的比例蓋黑：剛上身＝全亮，快到期＝幾乎全黑
                    c.Sweep.fillAmount = v.Duration > 0.01f ? Mathf.Clamp01(1f - v.Remaining / v.Duration) : 0f;
                    break;
                }
            }
        }

        static void HideFrom(List<Cell> list, int from)
        {
            for (int i = from; i < list.Count; i++) { list[i].Rt.gameObject.SetActive(false); list[i].Def = null; }
        }

        static void Layout(List<Cell> list, int n, float y)
        {
            float step = IconSize + Gap;
            float x0 = -(n - 1) * step * 0.5f;
            for (int i = 0; i < n; i++) list[i].Rt.anchoredPosition = new Vector2(x0 + i * step, y);
        }

        Cell Ensure(List<Cell> list, int i, bool buff)
        {
            while (list.Count <= i)
            {
                var c = new Cell();
                c.Border = UIBuilder.Image(_root, (buff ? "Buff" : "Debuff") + list.Count, null, buff ? BuffBorder : DebuffBorder);
                c.Rt = c.Border.rectTransform;
                c.Rt.anchorMin = c.Rt.anchorMax = new Vector2(0.5f, 0f);
                c.Rt.pivot = new Vector2(0.5f, 0.5f);
                c.Rt.sizeDelta = new Vector2(IconSize, IconSize);
                c.Border.raycastTarget = true;   // 讓 HoverTip 收得到滑鼠

                var bg = UIBuilder.Image(c.Rt, "Bg", null, CellBg);
                UIBuilder.Stretch(bg.rectTransform, Border, Border, Border, Border);
                bg.raycastTarget = false;

                c.Icon = UIBuilder.Image(c.Rt, "Icon", null, Color.white);
                UIBuilder.Stretch(c.Icon.rectTransform, Border + 1f, Border + 1f, Border + 1f, Border + 1f);
                c.Icon.preserveAspect = true;
                c.Icon.raycastTarget = false;

                c.Sweep = UIBuilder.Image(c.Rt, "Sweep", StatusIcons.White, SweepColor);
                UIBuilder.Stretch(c.Sweep.rectTransform, Border, Border, Border, Border);
                c.Sweep.type = Image.Type.Filled;
                c.Sweep.fillMethod = Image.FillMethod.Radial360;
                c.Sweep.fillOrigin = (int)Image.Origin360.Top;
                c.Sweep.fillClockwise = false;
                c.Sweep.fillAmount = 0f;
                c.Sweep.raycastTarget = false;

                c.Stack = UIBuilder.Text(c.Rt, "Stack", "", 16, Color.white, TextAnchor.LowerRight);
                UIBuilder.Stretch(c.Stack.rectTransform, 0f, 3f, 0f, 1f);
                c.Stack.raycastTarget = false;
                var sh = c.Stack.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
                sh.effectDistance = new Vector2(1f, -1f);

                var cell = c;
                HoverTip.Attach(c.Border.gameObject, () => TipFor(cell));
                list.Add(c);
            }
            return list[i];
        }

        /// <summary>「燃燒 Lv3：每秒受到 40 點傷害（剩餘 2.5 秒）」</summary>
        string TipFor(Cell c)
        {
            if (c.Def == null || _sc == null) return "";
            _sc.GetViews(_views);
            for (int i = 0; i < _views.Count; i++)
            {
                var v = _views[i];
                if (v.Def != c.Def) continue;
                string mag;
                switch (v.Def.Category)
                {
                    case StatusCategory.Stat: mag = Mathf.RoundToInt(Mathf.Abs(v.Magnitude) * 100f).ToString(); break;
                    case StatusCategory.Dot:
                    case StatusCategory.Hot: mag = Mathf.Max(1, Mathf.RoundToInt(v.Magnitude)).ToString(); break;
                    default: mag = ""; break;
                }
                string desc = v.Def.DescId > 0 ? string.Format(Language.GetText(v.Def.DescId), mag, v.Stacks) : "";
                string left = string.Format(Language.GetText(StatusSystem.TxtRemaining), v.Remaining.ToString("0.0"));
                return $"{v.Def.Name} Lv{v.Level}：{desc}（{left}）";
            }
            return c.Def.Name;
        }
    }
}
