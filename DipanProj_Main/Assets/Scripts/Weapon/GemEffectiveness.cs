using UnityEngine;
using Dipan.Inventory;

/// <summary>
/// 給 UI 用的小工具：「這顆珠子對哪把武器有沒有效果」。
///
/// 珠子可以鑲在護身符／戒指上跨裝備疊加到當前武器（GEM_SOCKET §3），所以「有沒有效」不是珠子或裝備的屬性，
/// 而是**珠子 × 參考武器**——鐵砧上是武器就看那把；否則看目前裝備的武器；換武器答案就變。
/// 因此鍛造介面的做法是**提示不擋**：允許鑲，但灰顯＋toast＋tooltip 說明。判斷本身走
/// <see cref="PlayerAbilities.IsGemEffective(WeaponMode, GemData)"/>（與實際套用同一份規則）。見 readme/GEM_SOCKET.md。
/// </summary>
public static class GemEffectiveness
{
    static WeaponManager _wm;
    static WeaponManager Wm => _wm != null ? _wm : (_wm = Object.FindObjectOfType<WeaponManager>());

    /// <summary>
    /// 參考武器：<paramref name="contextItemId"/> 是武器（ItemTable 的 WeaponID &gt; 0）就回那把的表格資料；
    /// 否則回目前裝備中的武器（含鑲嵌拷貝，Mode 一樣）。沒有武器回 null。
    /// </summary>
    public static WeaponData ReferenceWeapon(int contextItemId = 0)
    {
        var wm = Wm;
        if (wm == null) return null;
        // 武器工坊模擬中：不管鐵砧上放的是哪把，玩家實際射出去的是模擬武器，一律以它為準
        if (wm.SimulationOverride != null) return wm.GetCurrentWeapon();
        if (contextItemId > 0 && InventorySystem.Instance != null)
        {
            var d = InventorySystem.Instance.GetData(contextItemId);
            if (d != null && d.WeaponID > 0 && wm.All.TryGetValue(d.WeaponID, out var w)) return w;
        }
        return wm.GetCurrentWeapon();
    }

    /// <summary>某個珠子物品對參考武器有沒有效果。不是珠子、或沒有參考武器 → true（沒東西可以無效）。</summary>
    public static bool IsEffective(int gemItemId, WeaponData reference)
    {
        if (reference == null || reference.Recipe == null || gemItemId <= 0 || InventorySystem.Instance == null) return true;
        var d = InventorySystem.Instance.GetData(gemItemId);
        if (d == null || !d.IsGem) return true;
        var gd = ItemManager.Gems.Get(d.GemID);
        return PlayerAbilities.IsGemEffective(reference, gd);
    }

    public static bool IsEffective(ItemStack gem, WeaponData reference) => IsEffective(gem.IsEmpty ? 0 : gem.ItemId, reference);
    public static bool IsEffective(GemRef gem, WeaponData reference) => IsEffective(gem != null ? gem.itemId : 0, reference);

    /// <summary>列出某件裝備上「對參考武器無效」的珠子名稱（用 、 連接）；沒有回空字串。</summary>
    public static string IneffectiveGemNames(ItemInstance inst, WeaponData reference)
    {
        if (inst == null || !inst.HasSockets || reference == null || InventorySystem.Instance == null) return "";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < inst.sockets.Count; i++)
        {
            var g = inst.GemAt(i);
            if (g == null || IsEffective(g, reference)) continue;
            var d = InventorySystem.Instance.GetData(g.itemId);
            if (sb.Length > 0) sb.Append('、');
            sb.Append(d != null ? d.Name : $"#{g.itemId}");
        }
        return sb.ToString();
    }
}
