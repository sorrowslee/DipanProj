using System.Collections.Generic;
using UnityEngine;
using Dipan.Inventory;

/// <summary>
/// 狀態效果的<b>清單語法</b>（2026-11-18 作者選定：一欄寫清單，表不會變寬、新狀態不用加欄）。見 readme/STATUS_EFFECT.md §6。
///
/// <para>格式：<c>名稱或ID:等級;名稱或ID:等級</c>，例如 <c>燃燒:2;減速:1</c>。等級省略＝1；全形「：」「；」也可以。
/// CSV 裡不能有半形逗號，所以用分號分隔。</para>
///
/// <para>兩種清單，查的表不同：
///   ‧ <b>效果清單</b>（<see cref="ParseEffects"/>）：查 GemTable 的<b>狀態效果列</b>（帶觸發時機，例：「命中增傷」「發射疾行」）。
///     用在配方 <c>StatusLv</c>、血統 <c>Effects</c>——它們是「全身等級相加」的一個來源。
///   ‧ <b>狀態清單</b>（<see cref="ParseStatuses"/>）：直接查 StatusEffectTable（例：「燃燒」「中毒」）。
///     用在怪物 <c>ContactStatus</c>、地面特效 <c>StatusFx</c>、道具 <c>UseStatus</c>、觸發鏈 <c>applyStatus</c>——這些是「直接上身」，沒有觸發時機。</para>
/// </summary>
public static class StatusSpec
{
    public struct Entry
    {
        public int Id;
        public int Level;
    }

    static readonly Dictionary<string, List<Entry>> _effectCache = new Dictionary<string, List<Entry>>();
    static readonly Dictionary<string, List<Entry>> _statusCache = new Dictionary<string, List<Entry>>();
    static readonly List<Entry> Empty = new List<Entry>(0);

    public static void ResetForPlayMode() { _effectCache.Clear(); _statusCache.Clear(); }

    /// <summary>效果清單 → GemTable 狀態效果的 GemID＋等級。查不到的項目印警告後略過。結果快取（同一字串只解析一次）。</summary>
    public static List<Entry> ParseEffects(string raw, string context)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Empty;
        if (_effectCache.TryGetValue(raw, out var cached)) return cached;
        var gems = ItemManager.Gems;
        var list = new List<Entry>();
        foreach (var (key, lv) in Split(raw))
        {
            GemData hit = null;
            if (gems != null)
                foreach (var kv in gems.Gems)
                {
                    var g = kv.Value;
                    if (!g.IsStatusEffect) continue;
                    if (g.GemID.ToString() == key || g.Name == key) { hit = g; break; }
                }
            if (hit == null) { Debug.LogWarning($"[StatusSpec] {context}：效果「{key}」在 GemTable 的狀態效果裡找不到，已略過。"); continue; }
            list.Add(new Entry { Id = hit.GemID, Level = lv });
        }
        if (gems != null && gems.Loaded) _effectCache[raw] = list;   // 表還沒載入時不快取，下次再試
        return list;
    }

    /// <summary>狀態清單 → StatusEffectTable 的狀態 ID＋等級（名稱、英文 Key、ID 都可以）。</summary>
    public static List<Entry> ParseStatuses(string raw, string context)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Empty;
        if (_statusCache.TryGetValue(raw, out var cached)) return cached;
        var list = new List<Entry>();
        foreach (var (key, lv) in Split(raw))
        {
            StatusEffectDef hit = null;
            if (int.TryParse(key, out int id)) hit = StatusEffectDatabase.Get(id);
            if (hit == null) hit = StatusEffectDatabase.Get(key);   // 英文 Key
            if (hit == null)
                foreach (var d in StatusEffectDatabase.All)
                    if (d.Name == key) { hit = d; break; }
            if (hit == null) { Debug.LogWarning($"[StatusSpec] {context}：狀態「{key}」在 StatusEffectTable 找不到，已略過。"); continue; }
            list.Add(new Entry { Id = hit.Id, Level = lv });
        }
        if (StatusEffectDatabase.All.Count > 0) _statusCache[raw] = list;
        return list;
    }

    static IEnumerable<(string key, int lv)> Split(string raw)
    {
        foreach (var part in raw.Replace('；', ';').Split(';'))
        {
            var p = part.Trim();
            if (p.Length == 0) continue;
            var kv = p.Replace('：', ':').Split(':');
            string key = kv[0].Trim();
            int lv = 1;
            if (kv.Length > 1 && !int.TryParse(kv[1].Trim(), out lv)) lv = 1;
            if (key.Length == 0 || lv <= 0) continue;
            yield return (key, lv);
        }
    }
}
