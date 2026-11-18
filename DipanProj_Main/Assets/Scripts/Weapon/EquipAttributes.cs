using System;
using System.Collections.Generic;
using UnityEngine;
using Dipan.Inventory;

/// <summary>
/// **效果屬性 → 武器欄位的對應表（單一真相）**（2026-11-17 裝備體系，見 readme/EQUIPMENT.md §3、§4）。
///
/// GemTable 的 <c>Attribute</c> 欄寫的是抽象屬性（Damage／AttackRate／Pierce…），不是 CSV 欄名——
/// 因為同一個屬性在不同模式對應不同欄位：「攻擊頻率」在單發型是 FireInterval、雷射是 DotInterval、佛光是地面特效的節拍；
/// 「射程」在子彈是 LifeTime、雷射與施放類是 Range。合併珠子（遠射＝綿延＋遠射、疾發＝疾發＋頻擊、須彌＝須彌＋粗束）也靠這張表。
///
/// 三個地方共用，不要各寫各的：
/// <list type="number">
/// <item>讀表層（<see cref="RecipeEntry.FromFields"/>）：等級欄（PierceLv…）換算成數值 → <see cref="LevelValue"/>。</item>
/// <item>能力容器（<see cref="PlayerAbilities"/>）：全身等級加總後依屬性套到哪些欄位。</item>
/// <item>有效性（鍛造提示／之後的生成與鑲嵌規則）：<see cref="IsEffective(WeaponMode, GemData)"/>。</item>
/// </list>
/// 加新屬性：在 <see cref="Build"/> 加一行、在 PlayerAbilities 寫怎麼套，GemTable 加一列。
/// </summary>
public static class EquipAttributes
{
    sealed class Attr
    {
        public string Name;
        public EffectCalc Calc;
        /// <summary>越小越好（攻擊間隔、耗魔、集氣時間）：百分比用除法 基礎 ÷（1＋總和），永遠不會歸零。</summary>
        public bool Divide;
        /// <summary>可能對應的欄位（WeaponModeSpec 的欄名）；某模式只要其中一欄有效，這個屬性對該模式就有效。</summary>
        public string[] Fields;
    }

    static readonly Dictionary<string, Attr> _attrs = Build();

    static Dictionary<string, Attr> Build()
    {
        var d = new Dictionary<string, Attr>(StringComparer.OrdinalIgnoreCase);
        void A(string name, EffectCalc calc, bool divide, params string[] fields)
            => d[name] = new Attr { Name = name, Calc = calc, Divide = divide, Fields = fields };

        // ── 百分比：強化基礎屬性 ──
        A("Damage",        EffectCalc.Percent, false, "Damage");
        A("AttackRate",    EffectCalc.Percent, true,  "FireInterval", "DotInterval");   // 佛光另外處理（地面特效的節拍）
        A("Speed",         EffectCalc.Percent, false, "Speed");                         // 拋物線／頭頂投彈換算成縮短 FlightTime
        A("Range",         EffectCalc.Percent, false, "LifeTime", "Range");
        A("Size",          EffectCalc.Percent, false, "BulletScale", "BeamWidth");
        A("ManaCost",      EffectCalc.Percent, true,  "ManaCost");
        A("OrbitalRadius", EffectCalc.Percent, false, "OrbitalRadius");
        A("ChainRadius",   EffectCalc.Percent, false, "ChainRadius");
        A("DashDistance",  EffectCalc.Percent, false, "DashDistance");
        A("ChargeTime",    EffectCalc.Percent, true,  "ChargeTimeReduction");

        // ── 純等級：行為（武器起點也填等級） ──
        A("Pierce",   EffectCalc.Level, false, "PierceLv");
        A("Bounce",   EffectCalc.Level, false, "BounceLv");
        A("Split",    EffectCalc.Level, false, "SplitLv");
        A("Homing",   EffectCalc.Level, false, "HomingLv");
        A("Burst",    EffectCalc.Level, false, "BurstLv");
        A("Parallel", EffectCalc.Level, false, "ParallelLv");

        // ── 加值：模式參數的數量或角度（起點填數值） ──
        A("OrbitalCount",   EffectCalc.Add, false, "OrbitalCount");
        A("SummonCount",    EffectCalc.Add, false, "SummonCount");
        A("SummonMaxAlive", EffectCalc.Add, false, "SummonMaxAlive");
        A("ChainCount",     EffectCalc.Add, false, "ChainCount");
        A("SpreadAngle",    EffectCalc.Add, false, "SpreadMaxAngle");
        A("MeleeAngle",     EffectCalc.Add, false, "MeleeAngle");
        A("AimConeAngle",   EffectCalc.Add, false, "AimConeAngle");
        A("SnapRadius",     EffectCalc.Add, false, "SnapRadius");
        return d;
    }

    /// <summary>程式認不認得這個屬性。</summary>
    public static bool IsKnown(string attribute) => !string.IsNullOrEmpty(attribute) && _attrs.ContainsKey(attribute);

    /// <summary>這個屬性的算法（不認得回 Percent）。</summary>
    public static EffectCalc CalcOf(string attribute)
        => (!string.IsNullOrEmpty(attribute) && _attrs.TryGetValue(attribute, out var a)) ? a.Calc : EffectCalc.Percent;

    /// <summary>越小越好、百分比要用除法的屬性。</summary>
    public static bool IsDivide(string attribute)
        => !string.IsNullOrEmpty(attribute) && _attrs.TryGetValue(attribute, out var a) && a.Divide;

    /// <summary>這個屬性可能對應的欄位（給文件與除錯用）。</summary>
    public static IReadOnlyList<string> FieldsOf(string attribute)
        => (!string.IsNullOrEmpty(attribute) && _attrs.TryGetValue(attribute, out var a)) ? a.Fields : Array.Empty<string>();

    // ───────────────────────── 有效性 ─────────────────────────

    /// <summary>
    /// 這個屬性對這個發射模式有沒有作用。對應欄位只要有一欄對該模式有效就算有效（依 <see cref="WeaponModeSpec"/>），另外兩個特例：
    /// 「飛行速度 × 拋物線／頭頂投彈」換算成縮短落下秒數；「攻擊頻率 × 佛光」改的是地面特效的結算節拍。
    /// </summary>
    public static bool IsEffective(WeaponMode mode, string attribute)
    {
        if (!_attrs.TryGetValue(attribute ?? "", out var a)) return false;
        if (a.Name == "Speed" && (mode == WeaponMode.Parabolic || mode == WeaponMode.Overhead)) return true;
        if (a.Name == "AttackRate" && mode == WeaponMode.Aura) return true;
        foreach (var f in a.Fields)
            if (WeaponModeSpec.IsEffective(mode, f)) return true;
        return false;
    }

    /// <summary>某個效果對這個模式有沒有作用。防具類效果跟武器無關，一律 true；null 回 false。</summary>
    public static bool IsEffective(WeaponMode mode, GemData gem)
    {
        if (gem == null) return false;
        if (gem.IsArmorClass) return true;
        return IsEffective(mode, gem.Attribute);
    }

    // ───────────────────────── 等級 → 數值 ─────────────────────────

    static bool _warnedFallback;

    /// <summary>
    /// 純等級屬性（Pierce／Bounce／Split／Homing／Burst／Parallel）的「總等級 → 數值」。等級 ≤ 0 回 0。
    /// 正常查 GemTable；萬一讀不到表（不該發生）用內建備援曲線並警告一次，免得整個遊戲的武器行為歸零。
    /// </summary>
    public static float LevelValue(string attribute, int level)
    {
        if (level <= 0) return 0f;
        var g = ItemManager.Gems.FindByAttribute(attribute);
        if (g != null && g.Calc == EffectCalc.Level) return g.ValueAt(level);

        if (!_warnedFallback)
        {
            _warnedFallback = true;
            Debug.LogWarning($"[EquipAttributes] GemTable 裡找不到等級型屬性「{attribute}」，改用內建備援曲線（請確認 GemTableProvider 有拖 GemTable.csv）。");
        }
        // 備援曲線＝GemTable 2026-11-17 的初版值
        int lv = Mathf.Min(level, 50);
        return string.Equals(attribute, "Homing", StringComparison.OrdinalIgnoreCase) ? 60f + (lv - 1) * 30f : lv;
    }

    /// <summary>關掉 Domain Reload 後 static 不會歸零，進 Play 時由 PlayModeStaticReset 呼叫。</summary>
    public static void ResetForPlayMode() => _warnedFallback = false;
}
