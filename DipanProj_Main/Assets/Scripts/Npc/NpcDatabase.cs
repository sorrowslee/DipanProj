using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC 種類表（NpcTable.csv）的載入與查詢。懶漢快取：第一次存取 <see cref="Instance"/> 載入一次。
/// 來源：場景上 <see cref="NpcTableProvider"/> 的 TextAsset（主）→ Resources/Data/NpcTable（後備，印指引）。
/// 解析**依表頭欄名**取值（同 CsvTable 精神）：之後在表尾加欄不用改程式、缺欄給預設。
/// </summary>
public class NpcDatabase
{
    static NpcDatabase _instance;
    public static NpcDatabase Instance
    {
        get
        {
            if (_instance == null) { _instance = new NpcDatabase(); _instance.Load(); }
            return _instance;
        }
    }

    /// <summary>進 Play 時丟掉單例（已關 Domain Reload；由 PlayModeStaticReset 呼叫）。</summary>
    public static void ResetForPlayMode() => _instance = null;

    readonly Dictionary<int, NpcData> _byId = new Dictionary<int, NpcData>();

    public NpcData Get(int id) => _byId.TryGetValue(id, out var d) ? d : null;
    public IEnumerable<NpcData> All => _byId.Values;

    void Load()
    {
        var provider = Object.FindObjectOfType<NpcTableProvider>();
        if (provider != null && provider.npcCSV != null) { LoadFromText(provider.npcCSV.text); return; }

        var csv = Resources.Load<TextAsset>("Data/NpcTable");
        if (csv == null)
        {
            Debug.LogError("[NpcDatabase] 找不到 NpcTable：請在場景 GameManagers 上掛 NpcTableProvider，" +
                           "把 Assets/Data/NpcTable.csv 拖進 Npc CSV 欄（同 DramaTableProvider 作法）。");
            return;
        }
        LoadFromText(csv.text);
    }

    void LoadFromText(string text)
    {
        _byId.Clear();
        if (string.IsNullOrEmpty(text)) return;
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lines.Length < 2) return;

        // 表頭欄名 → 欄位索引（欄名不分大小寫、去空白）
        var header = lines[0].Split(',');
        var col = new Dictionary<string, int>();
        for (int i = 0; i < header.Length; i++)
        {
            string k = header[i].Trim().ToLowerInvariant();
            if (k.Length > 0 && !col.ContainsKey(k)) col[k] = i;
        }

        string Cell(string[] v, string key)
        {
            if (!col.TryGetValue(key, out int idx) || idx >= v.Length) return "";
            return v[idx].Trim();
        }
        float Num(string[] v, string key, float fallback)
            => float.TryParse(Cell(v, key), out float f) ? f : fallback;

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var v = lines[i].Split(',');
            if (!int.TryParse(Cell(v, "id"), out int id)) continue;

            var d = new NpcData
            {
                ID = id,
                Name = Cell(v, "name"),
                DisplayName = Cell(v, "displayname"),
                Scale = Num(v, "scale", 1f),
                AnimFPS = Num(v, "animfps", 8f),
                Speed = Num(v, "speed", 2f),
                HP = Num(v, "hp", 50f),
            };
            if (string.IsNullOrEmpty(d.Name))
            {
                Debug.LogWarning($"[NpcDatabase] NpcTable ID={id} 的 Name（序列圖資料夾名）是空的，略過。");
                continue;
            }
            _byId[id] = d;
        }
        Debug.Log($"[NpcDatabase] 載入 {_byId.Count} 種 NPC。");
    }
}
