using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Dipan.Data;
using Dipan.Localization;

// ═══════════════════════════════════════════════════════════════════════════
// 狀態效果（buff／debuff／DoT）的定義與讀表（2026-11-18，見 readme/STATUS_EFFECT.md）。
// 一種狀態＝StatusEffectTable.csv 一列；「誰身上有什麼」在 StatusController，施加入口在 StatusSystem。
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>增益或減益：決定狀態列放哪一列、外框顏色。DoT 也算減益。</summary>
public enum StatusKind { Buff = 0, Debuff = 1 }

/// <summary>狀態的種類。</summary>
public enum StatusCategory
{
    /// <summary>數值修正（增傷、易傷、減速…）：改 <see cref="StatusAttribute"/> 指定的那個乘數。</summary>
    Stat = 0,
    /// <summary>持續傷害：每 <see cref="StatusSystem.TickInterval"/> 秒結算一次。</summary>
    Dot = 1,
    /// <summary>持續回復：每 <see cref="StatusSystem.TickInterval"/> 秒回一次血。</summary>
    Hot = 2,
    /// <summary>控制（暈眩、冰凍）：等級＝觸發機率；行為在第⑤階段接上。</summary>
    Control = 3,
}

/// <summary>同一種狀態再上身時怎麼處理。</summary>
public enum StatusStack
{
    /// <summary>只有一個：時間重設、等級取高（DoT 每秒傷害也取高）。</summary>
    Refresh = 0,
    /// <summary>疊層：每層各自計時、強度相加，最多 MaxStack 層；滿了換掉最快到期的那層。</summary>
    Stack = 1,
    /// <summary>只留最強的一個：新的不比現在弱就整個換掉（含時間），比較弱就忽略。</summary>
    Strongest = 2,
    /// <summary>延長時間：剩餘時間加上 Duration，最多 Duration × MaxStack；等級取高。</summary>
    Extend = 3,
}

/// <summary>Stat／Control 改的是什麼。</summary>
public enum StatusAttribute
{
    None = 0,
    /// <summary>造成的傷害（增傷＋、虛弱－）。</summary>
    Outgoing = 1,
    /// <summary>受到的傷害（易傷＋、護體－）。</summary>
    Incoming = 2,
    /// <summary>移動速度（疾行＋、減速－）。</summary>
    MoveSpeed = 3,
    /// <summary>控制：暈眩。</summary>
    Stun = 10,
    /// <summary>控制：冰凍。</summary>
    Freeze = 11,
    /// <summary>控制：束縛（2026-11-25，古神黑洞）：鎖移動＋鎖攻擊，但**原地播走路**（看起來像在掙扎）。跟暈眩／冰凍同時在身上時，以冰凍＞暈眩＞束縛的表現為準。</summary>
    Bind = 12,
}

/// <summary>一種狀態效果的定義（StatusEffectTable.csv 一列）。純資料。</summary>
public class StatusEffectDef
{
    public int Id;
    public string Key;
    public int NameId;
    public int DescId;
    public StatusKind Kind = StatusKind.Debuff;
    public StatusCategory Category = StatusCategory.Stat;
    public StatusAttribute Attribute = StatusAttribute.None;
    public StatusStack Stack = StatusStack.Refresh;
    public int MaxStack = 1;
    public float Duration = 1f;
    /// <summary>主強度曲線（小數：10% → 0.1）。Stat＝百分比、Dot＝每秒傷害佔命中傷害的比例、Hot＝每秒回最大血量的比例、Control＝觸發機率。</summary>
    public float Lv1, PerLv;
    public int MaxLv = 50;
    /// <summary>DoT 固定值型：每秒傷害（已是 ×10 尺度）。</summary>
    public float FlatLv1, FlatPerLv;
    public DamageType DamageType = DamageType.Physical;
    public Color Color = Color.white;
    /// <summary>圖示檔名（Resources/UI/Icons/Status/ 底下）；空＝還沒有圖。</summary>
    public string Icon;

    public string Name => NameId > 0 ? Language.GetText(NameId) : Key;

    /// <summary>某個等級的主強度。等級 ≤ 0 回 0；超過 MaxLv 照 MaxLv 算。</summary>
    public float ValueAt(int level)
    {
        if (level <= 0) return 0f;
        int lv = Mathf.Min(level, Mathf.Max(1, MaxLv));
        return Lv1 + (lv - 1) * PerLv;
    }

    /// <summary>DoT 固定值型：某個等級的每秒傷害。</summary>
    public float FlatAt(int level)
    {
        if (level <= 0) return 0f;
        int lv = Mathf.Min(level, Mathf.Max(1, MaxLv));
        return FlatLv1 + (lv - 1) * FlatPerLv;
    }

    /// <summary>
    /// 「行動控制」：頭目（MonsterData 的 Controllable＝0）一律免疫（作者 2026-11-18 定：含減速）。
    /// ＝控制類，或會讓移動變慢的數值修正。
    /// </summary>
    public bool IsMovementImpairing =>
        Category == StatusCategory.Control || (Attribute == StatusAttribute.MoveSpeed && Lv1 < 0f);

    public bool IsTicking => Category == StatusCategory.Dot || Category == StatusCategory.Hot;
}

/// <summary>
/// 狀態效果表（StatusEffectTable.csv）。第一次查詢時自動載入：場景上的 GemTableProvider（statusEffectCSV 欄，MainScene 已掛）
/// →（編輯器）直接讀資產。關掉 Domain Reload 的保險在 <see cref="ResetForPlayMode"/>。
/// </summary>
public static class StatusEffectDatabase
{
    const string AssetPath = "Assets/Data/StatusEffectTable.csv";

    static readonly Dictionary<int, StatusEffectDef> _byId = new Dictionary<int, StatusEffectDef>();
    static readonly Dictionary<string, StatusEffectDef> _byKey = new Dictionary<string, StatusEffectDef>(StringComparer.OrdinalIgnoreCase);
    static readonly List<StatusEffectDef> _ordered = new List<StatusEffectDef>();
    static bool _loaded;

    public static void ResetForPlayMode() { _byId.Clear(); _byKey.Clear(); _ordered.Clear(); _loaded = false; }

    /// <summary>依表上順序的全部狀態。</summary>
    public static IReadOnlyList<StatusEffectDef> All { get { EnsureLoaded(); return _ordered; } }

    public static StatusEffectDef Get(int id)
    {
        EnsureLoaded();
        return _byId.TryGetValue(id, out var d) ? d : null;
    }

    public static StatusEffectDef Get(string key)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(key) && _byKey.TryGetValue(key.Trim(), out var d) ? d : null;
    }

    public static void EnsureLoaded() { if (!_loaded) LoadAuto(); }

    public static void LoadAuto()
    {
        _loaded = true;   // 失敗也只試一次，免得每次查詢都 FindObjectOfType
        var provider = UnityEngine.Object.FindObjectOfType<Dipan.Inventory.GemTableProvider>();
        if (provider != null && provider.statusEffectCSV != null) { LoadFromText(provider.statusEffectCSV.text); return; }
#if UNITY_EDITOR
        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath);
        if (asset != null) { LoadFromText(asset.text); return; }
#endif
        Debug.LogWarning("[StatusEffectDatabase] 找不到 StatusEffectTable。請把 " + AssetPath +
                         " 拖進場景上 GemTableProvider 的 Status Effect CSV 欄（見 readme/STATUS_EFFECT.md）。");
    }

    /// <summary>從 CSV 文字載入（之後預覽器／工具即時套用也走這裡）。</summary>
    public static void LoadFromText(string text)
    {
        _loaded = true;
        _byId.Clear(); _byKey.Clear(); _ordered.Clear();
        var table = CsvTable.Parse(text ?? "", "StatusEffectTable");
        table.Require("ID", "Key", "Kind", "Category", "Duration", "Lv1");
        foreach (var err in table.Errors) Debug.LogError(err);

        foreach (var row in table.Rows)
        {
            int id = row.GetInt("ID", 0);
            if (id <= 0) continue;
            var d = new StatusEffectDef
            {
                Id = id,
                Key = row.Get("Key").Trim(),
                NameId = row.GetInt("NameId", 0),
                DescId = row.GetInt("DescId", 0),
                Kind = ParseEnum(row.Get("Kind"), StatusKind.Debuff, id, "Kind"),
                Category = ParseEnum(row.Get("Category"), StatusCategory.Stat, id, "Category"),
                Attribute = ParseEnum(row.Get("Attribute"), StatusAttribute.None, id, "Attribute"),
                Stack = ParseEnum(row.Get("Stack"), StatusStack.Refresh, id, "Stack"),
                MaxStack = Mathf.Max(1, row.GetInt("MaxStack", 1)),
                Duration = Mathf.Max(0.05f, row.GetFloat("Duration", 1f)),
                Lv1 = Fraction(row.Get("Lv1")),
                PerLv = Fraction(row.Get("PerLv")),
                MaxLv = Mathf.Max(1, row.GetInt("MaxLv", 50)),
                FlatLv1 = row.GetFloat("FlatLv1", 0f),
                FlatPerLv = row.GetFloat("FlatPerLv", 0f),
                DamageType = ParseEnum(row.Get("DamageType"), DamageType.Physical, id, "DamageType"),
                Icon = row.Get("Icon").Trim(),
            };
            string hex = row.Get("Color").Trim();
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c)) d.Color = c;

            if (string.IsNullOrEmpty(d.Key)) { Debug.LogWarning($"[StatusEffectDatabase] 狀態 {id} 沒填 Key，已略過。"); continue; }
            if (d.Category == StatusCategory.Stat && (d.Attribute == StatusAttribute.None || d.Attribute >= StatusAttribute.Stun))
                Debug.LogWarning($"[StatusEffectDatabase] 狀態 {id} {d.Key} 是 Stat，Attribute 要填 Outgoing／Incoming／MoveSpeed（目前「{row.Get("Attribute")}」），不會有效果。");
            if (_byId.ContainsKey(id)) { Debug.LogWarning($"[StatusEffectDatabase] 狀態 ID {id} 重複，後面那列已略過。"); continue; }
            _byId[id] = d;
            _byKey[d.Key] = d;
            _ordered.Add(d);
        }
        Debug.Log($"[StatusEffectDatabase] 載入 {_byId.Count} 種狀態效果。");
    }

    /// <summary>「10%」→ 0.1；不帶 % 就照原數字（已經是小數）。</summary>
    static float Fraction(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0f;
        s = s.Trim();
        bool pct = s.EndsWith("%");
        if (pct) s = s.Substring(0, s.Length - 1).Trim();
        if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return 0f;
        return pct ? v / 100f : v;
    }

    static T ParseEnum<T>(string s, T fallback, int id, string column) where T : struct
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        if (Enum.TryParse(s.Trim(), true, out T v)) return v;
        Debug.LogWarning($"[StatusEffectDatabase] 狀態 {id} 的 {column}「{s}」不認得，改用 {fallback}。");
        return fallback;
    }
}
