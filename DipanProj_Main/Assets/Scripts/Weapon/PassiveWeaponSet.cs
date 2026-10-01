using System.Collections.Generic;
using UnityEngine;
using Dipan.Inventory;

/// <summary>
/// **玩家身上的被動武器清單**（2026-10-01）。與 <see cref="PlayerAbilities"/> 對稱：
/// 那邊收集「修飾型能力」（珠子）、這邊收集「被動型武器」（裝備著就自己運作、沒有扳機的武器，如 Familiar 浮游）。
///
/// ── 來源（全部收、不互斥）──
///   ① **工坊模擬／劇情覆寫**的武器是被動型 ⇒ 算一把（工坊 Play 中才調得到浮游；夢境武器也能給被動）。
///      ⚠ 背包**武器欄**不算：被動武器不能當一般武器裝在武器欄（作者 2026-10-01 拍板）——
///      被動武器的道具列 `EquipSlot` 應該是護身符／戒指等、`WeaponID` 留空、靠 `PassiveWeaponIds` 掛載。
///      `PlayerController.OnInventoryChanged` 遇到 `WeaponID` 指到被動型武器會印警告並當成空手。
///   ② 所有裝備欄物品的 <c>ItemData.PassiveWeaponIds</c>（ItemTable 第 19 欄）。
///   ③ 當前血統的 <c>BloodlineDef.PassiveWeaponIds</c>（BloodlineTable 第 27 欄）。
///
/// ── 每一把都過 <see cref="WeaponManager.AbilityResolver"/> ──
///   所以珠子是「全身」生效：戒指上的群環珠會讓每一把被動武器的本體都 +1（作者 2026-10-01 拍板：珠子影響全身，
///   血統給的被動也吃）。<see cref="PlayerAbilities"/> 已依模式過濾（Familiar 在 WeaponModeSpec 的矩陣裡）。
///
/// ── 重算時機 ──
///   由 <c>PlayerController</c> 每幀比對簽章（背包 LoadoutVersion ＋ 血統 Id ＋ 當前武器參照）決定，
///   變了才 <see cref="Rebuild"/>。刻意不在 BloodlineSystem／WeaponManager 各加事件——三個來源的變動路徑太多
///   （喝藥、夢境覆寫、讀檔、工坊每幀改值…），比對三個值最便宜也最不會漏。
///
/// ── 不驗證、不存檔 ──
///   清單完全由表推導，不進存檔。ID 找不到、或指到的武器不是被動型模式，在這裡印 Warning 並略過
///   （ItemDatabase／BloodlineTable 載入時只切字串，因為那時 WeaponTable 可能還沒載好）。
///
/// 見 readme/PASSIVE_WEAPON.md。
/// </summary>
public class PassiveWeaponSet
{
    /// <summary>一把被動武器與它的來源（來源字串只給除錯／tooltip 用）。</summary>
    public struct Entry
    {
        public string Source;      // 例：「武器欄」「護身符 應龍水珠」「血統 應龍」
        public int WeaponId;
        public WeaponData Weapon;  // 已過 AbilityResolver 的玩家專屬拷貝
    }

    readonly List<Entry> _entries = new List<Entry>();
    readonly List<WeaponData> _weapons = new List<WeaponData>();
    readonly HashSet<string> _warned = new HashSet<string>();

    public IReadOnlyList<Entry> Entries => _entries;
    /// <summary>只要武器資料的清單（給 <see cref="WeaponFamiliar"/> 用）。與 <see cref="Entries"/> 同序。</summary>
    public IReadOnlyList<WeaponData> Weapons => _weapons;
    public int Count => _entries.Count;

    // 上次重算時的簽章
    int _builtLoadoutVersion = int.MinValue;
    int _builtBloodlineId = int.MinValue;
    WeaponData _builtCurrent;

    /// <summary>三個來源有任何一個變了就回 true。便宜：三個比較。</summary>
    public bool NeedsRebuild(InventorySystem inv, int bloodlineId, WeaponData currentWeapon)
    {
        int lv = inv != null ? inv.LoadoutVersion : -1;
        return lv != _builtLoadoutVersion
            || bloodlineId != _builtBloodlineId
            || !ReferenceEquals(currentWeapon, _builtCurrent);
    }

    /// <summary>重新收集。<paramref name="currentWeapon"/> 傳 <c>WeaponManager.GetCurrentWeapon()</c>（已解析過的那把）。</summary>
    public void Rebuild(InventorySystem inv, int bloodlineId, WeaponData currentWeapon, WeaponManager wm)
    {
        _entries.Clear();
        _weapons.Clear();
        _builtLoadoutVersion = inv != null ? inv.LoadoutVersion : -1;
        _builtBloodlineId = bloodlineId;
        _builtCurrent = currentWeapon;
        if (wm == null) return;

        // ① 工坊模擬／劇情覆寫是被動型（已經過 AbilityResolver，直接用）。背包武器欄刻意不算（見檔頭）。
        if (IsPassive(currentWeapon) && (wm.SimulationOverride != null || wm.ScriptedOverrideId > 0))
            Push(wm.SimulationOverride != null ? "工坊模擬" : "劇情武器", currentWeapon.ID, currentWeapon);

        // ② 裝備欄
        if (inv != null)
        {
            foreach (var kv in inv.EquippedItems())
            {
                var data = inv.GetData(kv.Value.ItemId);
                if (data == null || !data.HasPassiveWeapons) continue;
                string src = $"{SlotLabel(kv.Key)} {data.Name}";
                foreach (int id in data.PassiveWeaponIds) PushResolved(src, id, wm);
            }
        }

        // ③ 血統
        var def = Dipan.Gacha.BloodlineTable.Get(bloodlineId);
        if (def != null && def.PassiveWeaponIds != null && def.PassiveWeaponIds.Length > 0)
        {
            string src = $"血統 {def.DisplayName}";
            foreach (int id in def.PassiveWeaponIds) PushResolved(src, id, wm);
        }
    }

    static bool IsPassive(WeaponData w)
        => w != null && w.Recipe != null && WeaponModeSpec.IsPassive(w.Recipe.Mode);

    void PushResolved(string source, int weaponId, WeaponManager wm)
    {
        string key = $"{source}:{weaponId}";
        if (!wm.All.TryGetValue(weaponId, out var baseWeapon) || baseWeapon == null)
        {
            // 自己印、只印一次：Rebuild 在工坊 Play 中改值時會每幀跑，走 GetWeapon 會把 Console 洗掉。
            if (_warned.Add(key))
                Debug.LogError($"[PassiveWeaponSet] {source} 的 PassiveWeaponIds 指到 WeaponTable 找不到的武器 ID {weaponId}，已略過。");
            return;
        }
        if (!IsPassive(baseWeapon))
        {
            if (_warned.Add(key))
                Debug.LogWarning($"[PassiveWeaponSet] {source} 的 PassiveWeaponIds 指到武器 {weaponId}「{baseWeapon.Name}」，" +
                                 $"但它是 {WeaponModeSpec.ModeLabel(baseWeapon.Recipe != null ? baseWeapon.Recipe.Mode : WeaponMode.Normal)} 模式、不是被動型，已略過。" +
                                 "只有被動型模式（目前：Familiar 浮游）才能掛在裝備或血統上。");
            return;
        }
        var resolver = WeaponManager.AbilityResolver;
        var w = resolver != null ? resolver(baseWeapon) : baseWeapon;
        Push(source, weaponId, w);
    }

    void Push(string source, int id, WeaponData w)
    {
        _entries.Add(new Entry { Source = source, WeaponId = id, Weapon = w });
        _weapons.Add(w);
    }

    static string SlotLabel(EquipSlot s)
    {
        switch (s)
        {
            case EquipSlot.Weapon: return "武器";
            case EquipSlot.Chest:  return "胸甲";
            case EquipSlot.Boots:  return "鞋子";
            case EquipSlot.Gloves: return "手套";
            case EquipSlot.Amulet: return "護身符";
            case EquipSlot.Ring:   return "戒指";
            default: return s.ToString();
        }
    }

    /// <summary>除錯用：列出每一把與來源。</summary>
    public string Describe()
    {
        if (_entries.Count == 0) return "（沒有被動武器）";
        var sb = new System.Text.StringBuilder();
        foreach (var e in _entries)
            sb.Append(e.Source).Append(" → ").Append(e.WeaponId).Append(' ').Append(e.Weapon != null ? e.Weapon.Name : "?").Append('\n');
        return sb.ToString().TrimEnd('\n');
    }
}
