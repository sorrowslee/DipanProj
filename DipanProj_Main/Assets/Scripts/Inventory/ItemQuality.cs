using System.Collections.Generic;
using UnityEngine;
using Dipan.Exchange;

namespace Dipan.Inventory
{
    /// <summary>裝備的大類（決定能長／能鑲哪一類效果）。見 readme/EQUIPMENT.md §2。</summary>
    public enum EquipClass
    {
        None,      // 不是裝備
        Weapon,    // 武器：只有武器類效果，且要對這把的發射模式有效
        Armor,     // 防具（胸甲／鞋子／手套）：只有防具類效果
        Jewelry,   // 珠寶（護身符／戒指）：兩類都可以
    }

    /// <summary>
    /// **品質系統的規則集中處**（2026-11-17，見 readme/EQUIPMENT.md）：
    ///   ‧ 品質怎麼算（裝備＝內建效果條數＋1；非裝備＝模板屬性，目前一律一般）
    ///   ‧ 顯示（「品名 名稱」、品質色）
    ///   ‧ 部位 × 效果類別 × 武器模式：生成時能骰哪些效果、鍛造時能鑲哪些珠子
    ///   ‧ 依規格產生一件裝備的內建效果（寫死／補骰／不夠就降級）
    ///
    /// 顏色與品名的單一真相仍是 <see cref="RarityInfo"/>；機率在 <see cref="Dipan.Rules.RandomRules"/>。
    /// </summary>
    public static class ItemQuality
    {
        /// <summary>格子外框從哪個品質開始畫（一般不畫框，免得背包每件裝備都有一圈白框）。要全部都畫改成 White。</summary>
        public const ItemRarity FrameMinRarity = ItemRarity.Blue;

        /// <summary>需要「這一件已經有分裂」才可能骰到的效果（廣角）。見 §2.3。</summary>
        const string AttrNeedsSplit = "SpreadAngle";
        const string AttrSplit = "Split";

        // ───────────────────────── 分類 ─────────────────────────

        public static EquipClass ClassOf(ItemData d)
        {
            if (d == null) return EquipClass.None;
            switch (d.EquipSlot)
            {
                case EquipSlot.Weapon: return EquipClass.Weapon;
                case EquipSlot.Chest: case EquipSlot.Boots: case EquipSlot.Gloves: return EquipClass.Armor;
                case EquipSlot.Amulet: case EquipSlot.Ring: return EquipClass.Jewelry;
                default: return EquipClass.None;
            }
        }

        // ───────────────────────── 品質與顯示 ─────────────────────────

        /// <summary>
        /// 這一件的品質。裝備＝內建效果條數＋1（不另外存，永遠跟效果對得上）；
        /// 非裝備＝模板屬性（ItemTable 的 Rarity 欄，留空＝一般）。
        /// </summary>
        public static ItemRarity RarityOf(ItemData d, ItemInstance inst)
        {
            if (d == null) return ItemRarity.White;
            if (d.IsEquippable)
                return RarityInfo.Normalize((ItemRarity)(1 + Mathf.Min(inst != null ? inst.AffixCount : 0, ItemInstance.AffixMax)));
            return RarityInfo.Normalize(d.Rarity);
        }

        public static ItemRarity RarityOf(ItemStack st)
        {
            var inv = InventorySystem.Instance;
            var d = (!st.IsEmpty && inv != null) ? inv.GetData(st.ItemId) : null;
            return RarityOf(d, st.Inst);
        }

        public static Color ColorOf(ItemData d, ItemInstance inst) => RarityInfo.ColorOf(RarityOf(d, inst));

        /// <summary>
        /// 顯示名稱：裝備＝「品名 名稱」（例：良品 兒童彈弓）；非裝備＝原名（只上色、不加字樣）。
        /// </summary>
        public static string DisplayName(ItemData d, ItemInstance inst)
        {
            if (d == null) return "";
            if (!d.IsEquippable) return d.Name;
            return RarityInfo.GradeName(RarityOf(d, inst)) + " " + d.Name;
        }

        /// <summary>這一件要不要在格子上畫品質框（只有裝備、而且品質達 <see cref="FrameMinRarity"/>）。</summary>
        public static bool WantsFrame(ItemData d, ItemInstance inst)
            => d != null && d.IsEquippable && RarityOf(d, inst) >= FrameMinRarity;

        /// <summary>給 Unity 富文字用的色碼（不含 #）。</summary>
        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        // ───────────────────────── 參考武器 ─────────────────────────

        static WeaponManager _wm;
        static WeaponManager Wm => _wm != null ? _wm : (_wm = Object.FindObjectOfType<WeaponManager>());

        /// <summary>關掉 Domain Reload 後 static 不會歸零，進 Play 時由 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode() { _wm = null; _warnedNoWm = false; }

        static bool _warnedNoWm;

        /// <summary>武器物品對應的表格武器（含配方，看模式與起點用）；不是武器或查不到回 null。</summary>
        public static WeaponData WeaponOf(ItemData d)
        {
            if (d == null || d.WeaponID <= 0) return null;
            var wm = Wm;
            if (wm == null)
            {
                if (!_warnedNoWm) { _warnedNoWm = true; Debug.LogWarning("[ItemQuality] 場景上沒有 WeaponManager，武器的效果池不分模式（只分類別）。"); }
                return null;
            }
            return wm.All != null && wm.All.TryGetValue(d.WeaponID, out var w) ? w : null;
        }

        // ───────────────────────── 效果能不能長在／鑲在這件上 ─────────────────────────

        /// <summary>
        /// 這種效果的**類別**能不能出現在這件裝備上（生成與鑲嵌共用）：
        /// 武器只吃武器類、防具只吃防具類、珠寶兩類都吃。不是裝備回 false。
        /// </summary>
        public static bool ClassAllows(ItemData equip, GemData effect)
        {
            if (effect == null) return false;
            switch (ClassOf(equip))
            {
                case EquipClass.Weapon: return !effect.IsArmorClass;
                case EquipClass.Armor: return effect.IsArmorClass;
                case EquipClass.Jewelry: return true;
                default: return false;
            }
        }

        /// <summary>武器 × 模式：這個效果對這把武器的發射模式有沒有作用。不是武器（或查不到武器）回 true。</summary>
        public static bool ModeAllows(ItemData equip, GemData effect)
        {
            if (ClassOf(equip) != EquipClass.Weapon || effect == null) return true;
            var w = WeaponOf(equip);
            if (w == null || w.Recipe == null) return true;
            return EquipAttributes.IsEffective(w.Recipe.Mode, effect);
        }

        /// <summary>武器起點有沒有分裂（配方的 SplitLv）。</summary>
        static bool StartHasSplit(ItemData equip)
        {
            var w = WeaponOf(equip);
            return w != null && w.Recipe != null && w.Recipe.SplitLv > 0;
        }

        /// <summary>這件裝備（含已經骰到的效果）有沒有分裂——廣角要先有分裂才可能骰到。</summary>
        static bool HasSplit(ItemData equip, List<Affix> current, GemDatabase gems)
        {
            if (StartHasSplit(equip)) return true;
            if (current == null) return false;
            for (int i = 0; i < current.Count; i++)
            {
                var g = gems.Get(current[i].effectId);
                if (g != null && g.Attribute == AttrSplit) return true;
            }
            return false;
        }

        /// <summary>生成時：這個效果能不能骰在這件上（類別＋模式＋廣角要先有分裂＋不重複）。</summary>
        static bool CanRoll(ItemData equip, GemData g, List<Affix> current, GemDatabase gems)
        {
            if (g == null || !ClassAllows(equip, g) || !ModeAllows(equip, g)) return false;
            if (current != null)
                for (int i = 0; i < current.Count; i++) if (current[i].effectId == g.GemID) return false;
            if (g.Attribute == AttrNeedsSplit && !HasSplit(equip, current, gems)) return false;
            return true;
        }

        /// <summary>鑲嵌被擋的原因。</summary>
        public enum SocketBlock { None, WrongClassWeapon, WrongClassArmor, ModeIneffective }

        /// <summary>
        /// 鍛造鑲嵌時：這顆珠子能不能鑲在這件裝備上。
        /// 武器擋「防具類珠子」與「對這把模式無效的珠子」；防具擋「武器類珠子」；珠寶一律可以（無效只提示，見 ForgingPanel）。
        /// 廣角缺分裂**不擋**（可能靠戒指上的分裂生效）。
        /// </summary>
        public static SocketBlock CheckSocket(ItemData equip, GemData gem)
        {
            if (equip == null || gem == null) return SocketBlock.None;
            switch (ClassOf(equip))
            {
                case EquipClass.Weapon:
                    if (gem.IsArmorClass) return SocketBlock.WrongClassWeapon;
                    if (!ModeAllows(equip, gem)) return SocketBlock.ModeIneffective;
                    return SocketBlock.None;
                case EquipClass.Armor:
                    return gem.IsArmorClass ? SocketBlock.None : SocketBlock.WrongClassArmor;
                default:
                    return SocketBlock.None;
            }
        }

        // ───────────────────────── 產生內建效果 ─────────────────────────

        /// <summary>
        /// 這件裝備最高能到什麼品質（有效效果池夠不夠 3 條）。給兌換表／掉落表載入時檢查「寫死極品卻達不到」用。
        /// </summary>
        public static int MaxQuality(ItemData equip)
        {
            if (equip == null || !equip.IsEquippable) return 1;
            var gems = ItemManager.Gems;
            var picked = new List<Affix>();
            // 逐條貪婪挑：先挑分裂（讓廣角變得可骰），其餘照表順序——只算「最多幾條」，不是真的骰
            for (int pass = 0; pass < 2 && picked.Count < ItemInstance.AffixMax; pass++)
                foreach (var kv in gems.Gems)
                {
                    if (picked.Count >= ItemInstance.AffixMax) break;
                    var g = kv.Value;
                    if (pass == 0 && g.Attribute != AttrSplit) continue;
                    if (CanRoll(equip, g, picked, gems)) picked.Add(new Affix { effectId = g.GemID, level = 1 });
                }
            return 1 + picked.Count;
        }

        /// <summary>
        /// 依規格決定一件裝備的內建效果（裝備以外回 null）。
        /// 流程（§6.2）：寫死的效果先放（不合這件的丟掉並警告）→ 決定品質（寫死 q ＞ 寫死效果條數 ＞ 骰）
        /// → 不足的條數從有效池隨機補、各自骰等級 → 池子不夠就降級（寫死品質卻達不到會警告）。
        /// </summary>
        public static List<Affix> RollAffixes(ItemData equip, ItemSpec spec, int cycle)
        {
            if (equip == null || !equip.IsEquippable) return null;
            var gems = ItemManager.Gems;
            var list = new List<Affix>();

            // ① 寫死的效果
            if (spec != null && spec.Effects != null)
            {
                foreach (var fx in spec.Effects)
                {
                    var g = gems.Get(fx.effectId);
                    if (g == null)
                    { Debug.LogWarning($"[ItemQuality] {equip.Name}：規格寫了效果 {fx.effectId}，GemTable 沒有這個 GemID，略過。"); continue; }
                    if (!ClassAllows(equip, g) || !ModeAllows(equip, g) || list.Exists(a => a.effectId == fx.effectId))
                    { Debug.LogWarning($"[ItemQuality] {equip.Name}：規格寫的「{g.Name}」不能長在這件上（部位類別或武器模式不合），略過。"); continue; }
                    list.Add(new Affix { effectId = fx.effectId, level = Mathf.Clamp(fx.level, 1, 3) });
                }
            }

            // ② 目標品質
            int quality;
            if (spec != null && spec.Quality > 0) quality = spec.Quality;
            else if (spec != null && spec.Effects != null) quality = spec.Effects.Count + 1;
            else quality = Dipan.Rules.RandomRules.RollQuality(cycle);
            int want = Mathf.Clamp(quality - 1, 0, ItemInstance.AffixMax);

            // ③ 補骰
            var pool = new List<GemData>();
            while (list.Count < want)
            {
                pool.Clear();
                foreach (var kv in gems.Gems)
                    if (CanRoll(equip, kv.Value, list, gems)) pool.Add(kv.Value);
                int pick = Dipan.Rules.RandomRules.PickUniform(pool.Count);
                if (pick < 0) break;   // 池子空了 → 降級
                list.Add(new Affix { effectId = pool[pick].GemID, level = Dipan.Rules.RandomRules.RollAffixLevel(cycle) });
            }

            if (list.Count < want && spec != null && spec.Quality > 0)
                Debug.LogWarning($"[ItemQuality] {equip.Name}：規格寫死品質 {quality}，但這件能長的效果只有 {list.Count} 條，降成品質 {list.Count + 1}。");

            return list.Count > 0 ? list : null;
        }
    }
}
