using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地圖總表 MapsTable.csv 的 runtime 載入器（仿 WeaponManager 讀 CSV）。
/// 欄位：ID, Name, Module, Path, IsLevelStart, MapMode
/// - ID 全域唯一整數，傳送點用它指向目標地圖。
/// - Module = 關卡（對得上 .dipanmap 的 module 欄）。
/// - Path = 相對路徑，格式同 MapLoader.mapPath（例：Modules/RedBridalGown/Maps/RedBridalGown_01.dipanmap）。
/// - IsLevelStart = 該 Module 的首張地圖（進入關卡時載入這張），每個 Module 應恰好一張。
/// - MapMode = 相機模式：1 = 整張地圖（縮放塞滿畫面，角色變小）；2 = 鏡頭跟隨（角色正常大小，鏡頭跟著走）。
///   留空 / 缺欄 / 無法解析 = 預設 2。實際是否跟隨還要看地圖夠不夠大（見 MapCameraController 門檻）。
/// - Atmosphere = 地圖氛圍後處理（見 AtmosphereController / readme/ATMOSPHERE.md）：
///   1 = 正常（不做處理，室外白天等）；2 = 幽暗+打光（看得到美術）；3 = 噩夢+打光（最壓迫）；
///   4 = 烈日曝曬；5 = 焦土餘燼；6 = 沙塵暴（4/5/6 為末日炎熱系，帶熱浪扭曲）；
///   7 = 淺海；8 = 深海；9 = 深海+恐怖（7/8/9 為海洋系，帶水下折射晃動；9 再套潛水燈光圈）；
///   10 = 山頂狂風（高空冷光 + 橫掃風絲 + 陣風時強時弱）。
///   留空 / 缺欄 / 無法解析 = 預設 1（正常）。換地圖時自動切換，所以可「室外→傳送→古墓」變氛圍。
/// 見 readme/MAP_SYSTEM.md。
/// </summary>
public class MapTableRow
{
    public int id;
    public string name;
    public string module;
    public string path;
    public bool isLevelStart;
    public int mode = 2;        // 1 = 整張地圖；2 = 鏡頭跟隨（預設）
    public int atmosphere = 1;  // 1 = 正常；2 = 幽暗+打光；3 = 噩夢+打光（預設 1）
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

            // MapMode 第 6 欄為新增、向下相容：缺欄 / 留空 / 無法解析都退回預設 2（鏡頭跟隨）。
            int mode = 2;
            if (v.Length >= 6 && int.TryParse(v[5].Trim(), out int m)) mode = m;

            // Atmosphere 第 7 欄為新增、向下相容：缺欄 / 留空 / 無法解析都退回預設 1（正常，不做處理）。
            int atmosphere = 1;
            if (v.Length >= 7 && int.TryParse(v[6].Trim(), out int a)) atmosphere = a;

            var row = new MapTableRow
            {
                id = id,
                name = v[1].Trim(),
                module = v[2].Trim(),
                path = v[3].Trim(),
                isLevelStart = v[4].Trim() == "1",
                mode = mode,
                atmosphere = atmosphere,
            };

            if (_byId.ContainsKey(id))
                Debug.LogWarning($"[MapTable] 地圖 ID {id} 重複，後者覆蓋前者。");
            _rows.Add(row);
            _byId[id] = row;
        }

        Debug.Log($"[MapTable] 載入 {_rows.Count} 張地圖。");
    }
}
