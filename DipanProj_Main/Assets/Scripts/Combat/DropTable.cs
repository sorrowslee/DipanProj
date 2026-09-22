using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// **掉落表**（CSV 在 `Assets/Data/DropTable.csv`，由場景上的 <see cref="DropTableProvider"/> 提供）。
///
/// <para>一列 ＝ 一張掉落表；`MonsterData.csv` 的 `DropTableId` 欄引用它的 `ID`。
/// 每列有 8 個**獨立**的掉落槽（`Drop1`~`Drop8`），彼此不互斥——各自擲一次骰。</para>
///
/// <para>槽的格式：<c>itemId:機率%:數量</c><br/>
/// ‧ <c>101:100:1-5</c> ＝ 銅錢 100% 掉 1~5 個<br/>
/// ‧ <c>201:17.5</c> ＝ 小回血瓶 17.5% 掉 1 個（數量留空＝1）<br/>
/// ‧ <c>301</c> ＝ 必掉 1 個（機率留空＝100）</para>
///
/// <para>⚠ <b>`DropTableId` 留空／0 ＝ 完全不掉寶</b>（作者 2026-09-22 拍板）。
/// 既有的怪在導入這張表時**一律填 1**（＝原本寫死在 `MonsterController.DropRunLoot` 的那組掉落），
/// 所以導入本身是零行為變化；之後要調哪一隻就改哪一隻的 `DropTableId`。</para>
///
/// <para>加新掉落組合 ＝ CSV 加一列，不動程式（同 SceneFxTable／VfxTable 的做法）。</para>
/// </summary>
public static class DropTable
{
    /// <summary>一個掉落槽。</summary>
    public struct Entry
    {
        public int itemId;
        public float chance;     // 0~100
        public int countMin, countMax;
    }

    static Dictionary<int, List<Entry>> _byId;

    /// <summary>進 Play 時清快取（Domain Reload 已關）。由 PlayModeStaticReset 呼叫。</summary>
    public static void ResetForPlayMode() => _byId = null;

    /// <summary>這張表有沒有內容（找不到表也回 false ⇒ 不掉寶）。</summary>
    public static bool Has(int dropTableId)
    {
        if (dropTableId <= 0) return false;
        EnsureLoaded();
        return _byId.TryGetValue(dropTableId, out var list) && list.Count > 0;
    }

    /// <summary>
    /// 擲一次這張表，回傳實際掉出來的東西（itemId, count）。每個槽**各自獨立**擲骰。
    /// `dropTableId` ≤ 0 或查無此表 ⇒ 空清單（不掉寶）。
    /// </summary>
    public static List<(int itemId, int count)> Roll(int dropTableId)
    {
        var result = new List<(int, int)>();
        if (dropTableId <= 0) return result;
        EnsureLoaded();
        if (!_byId.TryGetValue(dropTableId, out var list)) return result;

        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e.itemId <= 0) continue;
            if (e.chance < 100f && Random.value * 100f >= e.chance) continue;
            int n = (e.countMax > e.countMin) ? Random.Range(e.countMin, e.countMax + 1) : e.countMin;
            if (n > 0) result.Add((e.itemId, n));
        }
        return result;
    }

    static void EnsureLoaded()
    {
        if (_byId != null) return;
        _byId = new Dictionary<int, List<Entry>>();

        var provider = Object.FindObjectOfType<DropTableProvider>();
        var ta = (provider != null && provider.dropCSV != null)
            ? provider.dropCSV
            : Resources.Load<TextAsset>("Data/DropTable");
        if (ta == null || string.IsNullOrWhiteSpace(ta.text))
        {
            Debug.LogWarning("[DropTable] 找不到 DropTable.csv（場景上沒掛 DropTableProvider 或沒拖 CSV）。" +
                             "所有怪都將不掉寶——把 Assets/Data/DropTable.csv 拖進 GameManagers 上的 DropTableProvider 即可。");
            return;
        }

        string[] lines = ta.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)          // 第 0 列是表頭
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] f = lines[i].Split(',');
            if (f.Length < 3) continue;
            if (!int.TryParse(f[0].Trim(), out int id) || id <= 0) continue;

            var list = new List<Entry>();
            for (int c = 2; c < f.Length; c++)          // 第 1 欄是 Name，從第 2 欄起都是掉落槽
                if (TryParseEntry(f[c], out var e)) list.Add(e);
            _byId[id] = list;
        }
        Debug.Log($"[DropTable] 載入 {_byId.Count} 張掉落表。");
    }

    /// <summary>解析一個掉落槽：<c>itemId[:機率%[:數量 或 下限-上限]]</c>。空白／格式錯 ⇒ false（靜靜略過）。</summary>
    static bool TryParseEntry(string raw, out Entry e)
    {
        e = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string[] p = raw.Trim().Split(':');
        if (!int.TryParse(p[0].Trim(), out int itemId) || itemId <= 0)
        {
            Debug.LogWarning($"[DropTable] 掉落槽「{raw}」的 itemId 無效，略過。格式：itemId:機率%:數量");
            return false;
        }

        float chance = 100f;
        if (p.Length > 1 && !string.IsNullOrWhiteSpace(p[1]))
            float.TryParse(p[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out chance);

        int min = 1, max = 1;
        if (p.Length > 2 && !string.IsNullOrWhiteSpace(p[2]))
        {
            string q = p[2].Trim();
            int dash = q.IndexOf('-');
            if (dash > 0)
            {
                int.TryParse(q.Substring(0, dash).Trim(), out min);
                int.TryParse(q.Substring(dash + 1).Trim(), out max);
            }
            else { int.TryParse(q, out min); max = min; }
        }
        if (max < min) max = min;

        e = new Entry { itemId = itemId, chance = Mathf.Clamp(chance, 0f, 100f), countMin = min, countMax = max };
        return true;
    }
}
