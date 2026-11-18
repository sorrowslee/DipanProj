using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Dipan.Data;

namespace Dipan.Inventory
{
    /// <summary>效果的分類（2026-11-18 起分三類：攻擊／防禦／輔助，見 readme/EQUIPMENT.md §2、readme/STATUS_EFFECT.md）。
    /// Recipe／Weapon 都算攻擊類（差別只是改配方層或武器表層）。</summary>
    public enum GemTarget
    {
        /// <summary>武器類（配方層：反彈、穿透、速度、分裂…）。</summary>
        Recipe = 0,
        /// <summary>武器類（武器表層：傷害、大小、耗魔…）。</summary>
        Weapon = 1,
        /// <summary>防禦類（護體、回春…；能出現在胸甲／手套／珠寶）。CSV 填 Player 或 Defense。</summary>
        Player = 2,
        /// <summary>輔助類（疾行…；能出現在鞋子／珠寶）。CSV 填 Support。2026-11-18 加。</summary>
        Support = 3,
    }

    /// <summary>狀態效果什麼時候觸發（2026-11-18，readme/STATUS_EFFECT.md §4）。觸發時機屬於效果本身：同一個狀態不同時機＝不同效果、不同珠子。</summary>
    public enum StatusTrigger
    {
        /// <summary>命中時：減益上在被打中的那一方、增益上在自己身上。</summary>
        OnHit = 0,
        /// <summary>發射時（每一發；持續型武器啟動時與之後每秒）：增益上在自己身上。</summary>
        OnFire = 1,
        /// <summary>擊殺時（最後一下是自己打的，DoT 燒死也算）：增益上在自己身上。</summary>
        OnKill = 2,
        /// <summary>受擊時（被一般攻擊打到；DoT 的一跳不算）：增益上在自己身上、減益上在打我的人身上。</summary>
        OnHurt = 3,
    }

    /// <summary>
    /// 效果的算法（readme/EQUIPMENT.md §4）。
    /// </summary>
    public enum EffectCalc
    {
        /// <summary>百分比：強化基礎屬性（傷害、攻速、射程…）。同一屬性的百分比全部加總後套一次；越小越好的屬性用除法。</summary>
        Percent = 0,
        /// <summary>純等級：行為（穿透、反彈、分裂…）。武器起點也填等級，全身等級相加後查表得到數值。</summary>
        Level = 1,
        /// <summary>加值：模式參數的數量或角度（環繞數、召喚數、扇形角度…）。起點填數值，效果查表得到的值加上去。</summary>
        Add = 2,
    }

    /// <summary>
    /// 一種效果的定義（GemTable.csv 一列）。**能力珠與裝備的「內建特殊效果」共用這一份定義**。
    ///
    /// 等級曲線：數值＝<see cref="Lv1"/> ＋（等級－1）× <see cref="PerLv"/>；等級超過 <see cref="MaxLv"/> 照 MaxLv 算；等級 0＝沒有。
    /// 疊加規則是「全身等級相加後查一次表」——所以曲線一路定義到 MaxLv，不再只有 Lv1~Lv3 三格。
    /// 見 readme/EQUIPMENT.md。
    /// </summary>
    public class GemData
    {
        public int GemID;
        public string Name;
        public EffectCalc Calc = EffectCalc.Percent;
        /// <summary>改哪個屬性（Damage／AttackRate／Pierce…）；依武器模式對應到實際欄位由 <see cref="EquipAttributes"/> 決定。</summary>
        public string Attribute;
        public GemTarget Target = GemTarget.Recipe;
        /// <summary>第 1 級的數值。百分比型存小數（5% → 0.05）。</summary>
        public float Lv1;
        /// <summary>每多一級加多少。百分比型存小數。</summary>
        public float PerLv;
        /// <summary>總等級的上限；超過照這一級算。</summary>
        public int MaxLv = 50;

        /// <summary>疊在珠身上的能力符號檔名後半段（<c>gemIcon_&lt;這裡&gt;</c>）。空 = 不疊符號。</summary>
        public string Icon;

        /// <summary>
        /// 珠身顏色（<c>gemBase_&lt;這裡&gt;_lv1~3</c>）。
        /// CSV 留空時由 <see cref="Target"/> 自動推導：Recipe/Weapon → red（技能珠）、Player → blue（屬性珠）。
        /// </summary>
        public string BaseColor;

        public string Note;

        // ── 狀態效果（2026-11-18，readme/STATUS_EFFECT.md §4）：StatusID ＞ 0 的列是「觸發時給某個狀態」，不改武器數值 ──
        /// <summary>對應 StatusEffectTable 的狀態 ID；0＝不是狀態效果（一般的穿透、銳利…）。</summary>
        public int StatusId;
        /// <summary>什麼時候觸發。</summary>
        public StatusTrigger Trigger = StatusTrigger.OnHit;
        /// <summary>觸發機率（0～1；空＝1）。暈眩／冰凍另外由狀態表的等級決定機率，這裡留 1。</summary>
        public float Chance = 1f;
        /// <summary>內建冷卻（秒；空＝0）：觸發成功後這麼久內不再觸發（增益防常駐用）。</summary>
        public float Cooldown;

        /// <summary>這一列是狀態效果（觸發時給狀態），等級＝狀態的等級（全身相加後查 StatusEffectTable）。</summary>
        public bool IsStatusEffect => StatusId > 0;

        public bool IsPercent => Calc == EffectCalc.Percent;
        /// <summary>攻擊類（Recipe／Weapon）：能出現在武器與珠寶上，會改武器數值或替武器加命中效果。</summary>
        public bool IsAttackClass => Target == GemTarget.Recipe || Target == GemTarget.Weapon;
        /// <summary>防禦類：能出現在胸甲／手套／珠寶上。</summary>
        public bool IsDefenseClass => Target == GemTarget.Player;
        /// <summary>輔助類：能出現在鞋子／珠寶上。</summary>
        public bool IsSupportClass => Target == GemTarget.Support;

        /// <summary>
        /// 某個「總等級」對應的數值。等級 ≤ 0 回 0（沒有這個效果）；超過 <see cref="MaxLv"/> 照 MaxLv 算。
        /// 百分比型回小數（lv1 的 5% → 0.05）。
        /// </summary>
        public float ValueAt(int level)
        {
            if (level <= 0) return 0f;
            int lv = Mathf.Min(level, Mathf.Max(1, MaxLv));
            return Lv1 + (lv - 1) * PerLv;
        }

        /// <summary>給 UI 看的一行：「穿透 Lv2（穿 2 隻）」這類。<paramref name="level"/> 是這一個來源的等級。</summary>
        public string Describe(int level)
        {
            float v = ValueAt(level);
            if (IsStatusEffect)
            {
                // 狀態效果：「命中增傷 Lv2（命中時 20%，冷卻 5 秒）」——等級＝給出去的狀態等級（全身相加）
                string when = Trigger == StatusTrigger.OnFire ? "發射時" : Trigger == StatusTrigger.OnKill ? "擊殺時"
                            : Trigger == StatusTrigger.OnHurt ? "受擊時" : "命中時";
                string extra = (Chance < 0.999f ? $" {Chance * 100f:0.#}%" : "") + (Cooldown > 0f ? $"，冷卻 {Cooldown:0.#} 秒" : "");
                return $"{Name} Lv{level}（{when}{extra}）";
            }
            switch (Calc)
            {
                case EffectCalc.Percent: return $"{Name} Lv{level}（+{v * 100f:0.#}%）";
                case EffectCalc.Add:     return $"{Name} Lv{level}（+{v:0.##}）";
                default:                 return $"{Name} Lv{level}";
            }
        }
    }

    /// <summary>
    /// 效果定義表（Assets/Data/GemTable.csv）。
    /// 由場景上的 <see cref="GemTableProvider"/> 把 CSV 拖進來提供，與其他資料表同慣例。
    /// </summary>
    public class GemDatabase
    {
        readonly Dictionary<int, GemData> _gems = new Dictionary<int, GemData>();
        readonly Dictionary<string, GemData> _byAttribute = new Dictionary<string, GemData>(System.StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<int, GemData> Gems => _gems;

        /// <summary>有沒有成功讀到表（讀不到時讀表層會改用 <see cref="EquipAttributes"/> 的備援曲線）。</summary>
        public bool Loaded => _gems.Count > 0;

        public GemData Get(int gemId)
        {
            _gems.TryGetValue(gemId, out var g);
            return g;
        }

        /// <summary>依屬性找效果（例：「Pierce」→ 穿透）。一個屬性只會有一種效果。</summary>
        public GemData FindByAttribute(string attribute)
        {
            if (string.IsNullOrEmpty(attribute)) return null;
            _byAttribute.TryGetValue(attribute, out var g);
            return g;
        }

        public void LoadFromTextAsset(TextAsset csv)
        {
            if (csv == null) { Debug.LogError("[GemDatabase] 傳入的 GemTable TextAsset 為 null。"); return; }
            LoadFromText(csv.text);
        }

        /// <summary>找不到直接的 TextAsset 時：先找場景上的 provider，（編輯器）再直接讀資產，最後退回 Resources。</summary>
        public void LoadAuto(string resourcesPath = "Data/GemTable")
        {
            var provider = Object.FindObjectOfType<GemTableProvider>();
            if (provider != null && provider.gemCSV != null) { LoadFromText(provider.gemCSV.text); return; }

#if UNITY_EDITOR
            // 編輯器下沒開 MainScene 時（例如武器工坊在別的場景開著），直接讀資產，讓等級換算照樣正確。
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/GemTable.csv");
            if (asset != null) { LoadFromText(asset.text); return; }
#endif

            var csv = Resources.Load<TextAsset>(resourcesPath);
            if (csv == null)
            {
                Debug.LogWarning("[GemDatabase] 找不到 GemTable。請把 Assets/Data/GemTable.csv 拖進場景上 " +
                                 "GemTableProvider 元件的 Gem CSV 欄（見 readme/GEM_SOCKET.md）。");
                return;
            }
            LoadFromText(csv.text);
        }

        void LoadFromText(string text)
        {
            _gems.Clear();
            _byAttribute.Clear();
            var table = CsvTable.Parse(text ?? "", "GemTable");
            table.Require("GemID", "Name", "Calc", "Attribute", "Target", "Lv1", "PerLv", "MaxLv");
            foreach (var err in table.Errors) Debug.LogError(err);

            foreach (var row in table.Rows)
            {
                int id = row.GetInt("GemID", 0);
                if (id <= 0) continue;

                var g = new GemData();
                g.GemID = id;
                g.Name = row.Get("Name");
                g.Attribute = row.Get("Attribute");
                g.Target = ParseTarget(row.Get("Target"));
                if (!TryParseCalc(row.Get("Calc"), out g.Calc))
                {
                    Debug.LogWarning($"[GemDatabase] 效果 {id} '{g.Name}' 的 Calc「{row.Get("Calc")}」不認得（Percent／Level／Add），已略過。");
                    continue;
                }
                g.Lv1 = ParseValue(row.Get("Lv1"));
                g.PerLv = ParseValue(row.Get("PerLv"));
                g.MaxLv = Mathf.Max(1, row.GetInt("MaxLv", 50));
                g.Icon = row.Get("Icon");
                g.BaseColor = row.Get("BaseColor");
                if (string.IsNullOrEmpty(g.BaseColor)) g.BaseColor = DefaultColorFor(g.Target);
                g.Note = row.Get("Note");
                // 狀態效果（選填的四欄；舊表沒有這四欄也照常讀）
                g.StatusId = row.GetInt("StatusID", 0);
                if (g.IsStatusEffect)
                {
                    string trig = row.Get("Trigger").Trim();
                    if (!string.IsNullOrEmpty(trig) && !System.Enum.TryParse(trig, true, out g.Trigger))
                        Debug.LogWarning($"[GemDatabase] 效果 {id} '{g.Name}' 的 Trigger「{trig}」不認得（OnHit／OnFire／OnKill／OnHurt），改用 OnHit。");
                    string ch = row.Get("Chance").Trim();
                    g.Chance = string.IsNullOrEmpty(ch) ? 1f : Mathf.Clamp01(ParseValue(ch));
                    g.Cooldown = Mathf.Max(0f, row.GetFloat("Cooldown", 0f));
                }

                if (string.IsNullOrEmpty(g.Attribute))
                {
                    Debug.LogWarning($"[GemDatabase] 效果 {id} '{g.Name}' 沒填 Attribute（要改哪個屬性），已略過。");
                    continue;
                }
                // 武器類的屬性必須是程式認得的，否則這個效果永遠不會生效（而且不會報錯）
                if (g.IsAttackClass && !g.IsStatusEffect && !EquipAttributes.IsKnown(g.Attribute))
                    Debug.LogWarning($"[GemDatabase] 效果 {id} '{g.Name}' 的 Attribute「{g.Attribute}」程式不認得，不會有效果。可用屬性見 EquipAttributes.cs。");
                if (g.IsAttackClass && !g.IsStatusEffect && EquipAttributes.IsKnown(g.Attribute) && EquipAttributes.CalcOf(g.Attribute) != g.Calc)
                    Debug.LogWarning($"[GemDatabase] 效果 {id} '{g.Name}'：屬性 {g.Attribute} 的算法應該是 {EquipAttributes.CalcOf(g.Attribute)}，表上填 {g.Calc}，以程式為準。");
                if (g.IsStatusEffect) { }   // 狀態效果都填 Attribute=Status、彼此靠 GemID 區分，不進「一個屬性一種效果」的檢查
                else if (_byAttribute.ContainsKey(g.Attribute))
                    Debug.LogWarning($"[GemDatabase] 屬性 {g.Attribute} 有兩種效果（{_byAttribute[g.Attribute].GemID} 與 {id}），全身等級相加會分不清，請合併。");
                else
                    _byAttribute[g.Attribute] = g;
                _gems[g.GemID] = g;
            }
            Debug.Log($"[GemDatabase] 載入 {_gems.Count} 種效果。");
        }

        /// <summary>沒填 BaseColor 時的預設珠身顏色：攻擊類紅、防禦類藍、輔助類綠。</summary>
        static string DefaultColorFor(GemTarget t) => t == GemTarget.Player ? "blue" : t == GemTarget.Support ? "green" : "red";

        static GemTarget ParseTarget(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return GemTarget.Recipe;
            switch (s.Trim().ToLowerInvariant())
            {
                case "weapon": return GemTarget.Weapon;
                case "player": return GemTarget.Player;
                case "defense": return GemTarget.Player;
                case "support": return GemTarget.Support;
                default: return GemTarget.Recipe;
            }
        }

        static bool TryParseCalc(string s, out EffectCalc calc)
        {
            calc = EffectCalc.Percent;
            if (string.IsNullOrWhiteSpace(s)) return false;
            switch (s.Trim().ToLowerInvariant())
            {
                case "percent": calc = EffectCalc.Percent; return true;
                case "level":   calc = EffectCalc.Level;   return true;
                case "add":     calc = EffectCalc.Add;     return true;
                default: return false;
            }
        }

        /// <summary>解析數值；尾端帶 % 就除以 100（"5%" → 0.05）。</summary>
        static float ParseValue(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0f;
            s = s.Trim();
            if (s.EndsWith("%"))
            {
                s = s.Substring(0, s.Length - 1).Trim();
                return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float p) ? p / 100f : 0f;
            }
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;
        }
    }
}
