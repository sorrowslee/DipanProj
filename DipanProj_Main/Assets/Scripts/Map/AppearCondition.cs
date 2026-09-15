using UnityEngine;
using Dipan.Gacha;
using Dipan.Inventory;

/// <summary>
/// 通用「出現／觸發條件」——全遊戲共用的一小串條件字串，AND 結算。
/// 見 readme/TRIGGER_CHAIN.md〈通用條件（conditions）〉。
///
/// 目前有四個使用端（全部共用這支求值器與編輯器的同一個 UI 元件）：
///   ‧ 觸發點（含怪物出生點）：TriggerRegion 的 <c>conditions</c> 參數 → TriggerChain.RequirementMet
///   ‧ NPC 擺放：NpcInstance.conditions（出現與否）＋ conditionalDramas（講哪一句）
///   ‧ 地上物擺放：ObjectInstance.conditions（出現與否）
///
/// 字串格式：<c>kind:value</c>，前綴 <c>!</c>＝「沒有／不是」，<c>|</c> 分隔多條＝AND。
///   series:2          血統系列＝血族（BloodlineSeriesTable 的 SeriesId；整個系列三階都算）
///   !series:2|!series:3   不是血族、也不是狂族
///   bloodline:22      血統＝該隱（BloodlineTable 的 Id，只認那一階）
///   item:104          背包裡有 104 號道具（只算背包格、不含裝備欄，同 requireItem）
///   flag:hallGateOpen 旗標成立（走旗標登記表；與 requireFlag 同一套）
///
/// ⚠ 這是**加法**：既有的 requireFlag / requireItem / requireCycle* / requireClears* 一個都沒動，
///   conditions 只是再多一組 AND 條件。舊地圖缺這個欄位＝空字串＝永遠成立。
///
/// ⚠ 判定時機一律是「問的當下」，使用端決定什麼時候問：
///   NPC／地上物在**進圖生成當下**問一次（作者拍板：關卡中途變身不即時換人，換到下一張圖才反應）；
///   觸發點則是每次要觸發時問（同既有條件欄）。
///
/// ⚠ 無存檔的單場景測試（DevQuickStart／編輯器直測）：血統照實判定＝人類，
///   所以 series/bloodline 條件一律不成立。這是刻意的——否則「非血族才出現」的 NPC
///   會和血族版一起冒出來，比看不到更難查。要測血統分支請用測試選單進關。
/// </summary>
public static class AppearCondition
{
    public const string KindSeries = "series";        // 血統系列（SeriesId）
    public const string KindBloodline = "bloodline";  // 血統（單一階的 Id）
    public const string KindItem = "item";            // 背包道具（itemId）
    public const string KindFlag = "flag";            // 旗標（裸名，同觸發鏈）

    /// <summary>條件是否全部成立（AND）。空字串／null＝沒有條件＝成立。</summary>
    public static bool Met(string conditions)
    {
        if (string.IsNullOrWhiteSpace(conditions)) return true;

        string[] parts = conditions.Split('|');
        for (int i = 0; i < parts.Length; i++)
        {
            string raw = parts[i].Trim();
            if (raw.Length == 0) continue;               // 空欄（作者按了＋還沒填）＝略過，不擋人

            bool wantNot = raw[0] == '!';
            if (wantNot) raw = raw.Substring(1).Trim();

            // 只切第一個冒號：旗標名可能自己帶「永久:」前綴，後面整段都是值。
            int colon = raw.IndexOf(':');
            if (colon <= 0) { WarnOnce(parts[i]); continue; }
            string kind = raw.Substring(0, colon).Trim().ToLowerInvariant();
            string value = raw.Substring(colon + 1).Trim();
            if (value.Length == 0) continue;             // 種類選了、id 還沒填＝略過

            if (!TryEvaluate(kind, value, out bool have)) { WarnOnce(parts[i]); continue; }
            if (have == wantNot) return false;           // 要有卻沒有、或要沒有卻有 → 擋
        }
        return true;
    }

    /// <summary>單條條件的成立與否。回 false＝這個 kind 不認得（呼叫端當作沒這條）。</summary>
    static bool TryEvaluate(string kind, string value, out bool have)
    {
        have = false;
        switch (kind)
        {
            case KindSeries:
            {
                if (!int.TryParse(value, out int seriesId) || seriesId <= 0) return false;
                var s = BloodlineSystem.CurrentSeries;      // 人類／表A 沒登記時是 null
                have = s != null && s.SeriesId == seriesId;
                return true;
            }
            case KindBloodline:
            {
                if (!int.TryParse(value, out int bid) || bid <= 0) return false;
                have = BloodlineSystem.CurrentBloodlineId == bid;
                return true;
            }
            case KindItem:
            {
                if (!int.TryParse(value, out int itemId) || itemId <= 0) return false;
                var inv = InventorySystem.Instance;          // 無背包系統（單場景測試）＝視為沒有
                have = inv != null && inv.CountOf(itemId) > 0;
                return true;
            }
            case KindFlag:
                have = TriggerChain.FlagTrue(value);         // 生命週期查旗標登記表，與 requireFlag 同一套
                return true;
        }
        return false;
    }

    // 打錯字/舊資料的未知條件：**視為成立**（不擋人）＋ Console 印一次。
    // 選「不擋」是因為「東西莫名其妙不見了」比「東西多出現」難查非常多。
    static readonly System.Collections.Generic.HashSet<string> _warned = new System.Collections.Generic.HashSet<string>();
    static void WarnOnce(string clause)
    {
        string c = (clause ?? "").Trim();
        if (c.Length == 0 || !_warned.Add(c)) return;
        Debug.LogWarning($"[AppearCondition] 看不懂的條件「{c}」，已當作成立（不擋）。" +
                         $"格式是 kind:value（kind＝{KindSeries}／{KindBloodline}／{KindItem}／{KindFlag}），" +
                         "前綴 ! ＝沒有，| 分隔＝AND。見 readme/TRIGGER_CHAIN.md。");
    }

    /// <summary>進 Play 模式時清掉「已警告過」的記錄（已關 Domain Reload），讓同一個打錯的條件每次測試都看得到。</summary>
    public static void ResetForPlayMode() => _warned.Clear();
}
