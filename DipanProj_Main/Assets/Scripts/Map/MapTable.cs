using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地圖總表 MapsTable.csv 的 runtime 載入器（仿 WeaponManager 讀 CSV）。
/// 欄位：ID, Name, Module, Path, IsLevelStart
/// - ID 全域唯一整數，傳送點用它指向目標地圖。
/// - Module = 關卡（對得上 .dipanmap 的 module 欄）。
/// - Path = 相對路徑，格式同 MapLoader.mapPath（例：Modules/RedBridalGown/Maps/RedBridalGown_01.dipanmap）。
/// - IsLevelStart = 該 Module 的首張地圖（進入關卡時載入這張），每個 Module 應恰好一張。
/// 見 readme/MAP_SYSTEM.md。
/// </summary>
public class MapTableRow
{
    public int id;
    public string name;
    public string module;
    public string path;
    public bool isLevelStart;
}

public class MapTable : MonoBehaviour
{
    public TextAsset MapsCSV;

    private readonly Dictionary<int, MapTableRow> _byId = new Dictionary<int, MapTableRow>();
    private readonly List<MapTableRow> _rows = new List<MapTableRow>();

    void Awake()
    {
        Load();
    }

    public MapTableRow Get(int id) => _byId.TryGetValue(id, out var r) ? r : null;

    /// <summary>找某 Module 的首張地圖（IsLevelStart=1）；多張時取第一張並警告，找不到回 null。</summary>
    public MapTableRow FindLevelStart(string module)
    {
        MapTableRow found = null;
        foreach (var r in _rows)
        {
            if (r.module != module || !r.isLevelStart) continue;
            if (found != null)
            {
                Debug.LogWarning($"[MapTable] Module「{module}」有多張 IsLevelStart，使用 #{found.id}。");
                break;
            }
            found = r;
        }
        if (found == null)
        {
            string avail = _rows.Count == 0
                ? "（表是空的 → 多半是 MapTable 的 Maps CSV 欄沒指到正確檔、或該 TextAsset 沒重新匯入）"
                : string.Join(" ｜ ", _rows.ConvertAll(r => $"#{r.id} module=\"{r.module}\" start={r.isLevelStart}"));
            Debug.LogError($"[MapTable] 找不到 Module「{module}」的首張地圖（IsLevelStart=1）。目前表內 {_rows.Count} 列：{avail}");
        }
        return found;
    }

    private void Load()
    {
        if (MapsCSV == null)
        {
            Debug.LogError("[MapTable] MapsCSV 未指定！請把 MapsTable.csv 拖進 MapTable 元件的 Maps CSV 欄。");
            return;
        }
        if (string.IsNullOrWhiteSpace(MapsCSV.text))
        {
            Debug.LogError($"[MapTable] 指定的 CSV「{MapsCSV.name}」內容是空的（可能匯入到舊的空檔）。請右鍵該檔 → Reimport，或重新指定正確的 MapsTable.csv。");
            return;
        }

        string[] lines = MapsCSV.text.Split('\n');
        // 跳過標題列 (ID,Name,Module,Path,IsLevelStart)
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] v = lines[i].Split(',');
            if (v.Length < 5) continue;
            if (!int.TryParse(v[0].Trim(), out int id)) continue;

            var row = new MapTableRow
            {
                id = id,
                name = v[1].Trim(),
                module = v[2].Trim(),
                path = v[3].Trim(),
                isLevelStart = v[4].Trim() == "1",
            };

            if (_byId.ContainsKey(id))
                Debug.LogWarning($"[MapTable] 地圖 ID {id} 重複，後者覆蓋前者。");
            _rows.Add(row);
            _byId[id] = row;
        }

        Debug.Log($"[MapTable] 載入 {_rows.Count} 張地圖。");
    }
}
