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
///   10 = 風雪（陰冷暴風：冷灰調 + 翻騰白霧 + 不規則橫向風絲）；11 = 強風（去白霧、只留斜向風絲）；
///   12 = 綿綿細雨／毛毛雨（＝大雨的半速半密度）；13 = 大雨（細密雨點往下落）；
///   14 = 陰森森林鬼霧（畫面偏暗、陰綠冷調 + 漂移黑霧、偶爾一陣濃）；
///   15 = 電視雜訊（雪花 + 掃描線 + 滾動同步條 + 偶發水平撕裂 + 灰調閃爍）。
///   留空 / 缺欄 / 無法解析 = 預設 1（正常）。換地圖時自動切換，所以可「室外→傳送→古墓」變氛圍。
/// - NoWeapon = 這張地圖是否**禁止玩家使用武器**：0 = 可用（預設）；1 = 禁用。
///   禁用時按左鍵／空白鍵完全沒反應——不發射、不扣 MP、不擺攻擊動作、也不轉身面向滑鼠。
///   用在劇情用地圖（開場山道、初始洞窟）與邪佛廣場這種「亂放武器很奇怪」的大廳。
///   只擋玩家發射，移動／互動／背包／喝藥一律正常，怪物用武器也不受影響。
/// - EnvBright = **環境亮度** 0~100（留空 / 缺欄 = 100，完全不壓暗，與舊行為相同）。
///   只在 Atmosphere = 1（正常）時生效：把整張圖壓暗到這個亮度，場上的燈（火把/燈籠，見 LightSource）
///   再把周圍照回來。用途是「不到幽暗等級、但想讓火把有存在感」的室內走廊/地窖。
///   例：100 = 白天室外；70 = 陰天/傍晚；45 = 昏暗室內（火把明顯）；25 = 只靠火把看得見路。
///   Atmosphere >= 2 時忽略此欄（那些氛圍的暗度由氛圍本身定義）。見 readme/ATMOSPHERE.md。
/// - SceneTip = **場景說明文字圖的 key**（留空 / 缺欄 = 這張圖不顯示場景說明）。
///   進圖後會載入 <c>Resources/UI/Texts/SceneTipPanel_Text_&lt;key&gt;</c>（前綴寫死在 SceneTipPanel，
///   語言資料夾由 LocalizedArt 自動解析），淡入淡出跳一次場景名。
///   ⚠ **key 不是地圖 Name**：Name 是程式/檔案的內部名（Main_Square），key 是美術命名（BuddhaSquare），
///   兩者刻意不綁在一起——地圖檔改名時圖不會跟著壞。
///   ⚠ **同一趟關卡內同一個 key 只顯示一次**（去重用 key 不是地圖 id），所以整個關卡的房間可以
///   全部填同一個 key：不管玩家先進哪一間都會跳、之後房間互跳都不會再跳。見 readme/SCENE_TIP.md。
/// - AtmoTint = **場景主色染色**（6 碼 16 進位 RRGGBB，不含 #；留空 / 缺欄 = 不染，與舊行為相同）。
///   把整張畫面的「暗部」往這個色相拉（亮度不變、只動色相/飽和），燈池中心與亮部幾乎不受影響——
///   用來執行美術紀律的「色彩劇本」：紅嫁衣填暗絳紅、別的圖填各自的主色，暗就從「灰的暗」變「有主題的暗」。
///   任何 Atmosphere 型別都可疊加；「Atmosphere=1 且 EnvBright=100」的圖填了也會生效（會為此啟用後處理）。
///   見 readme/ATMOSPHERE.md〈場景主色染色〉與 readme/art_direction/SHADER_GUIDELINE.md。
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
    public int sceneEffect = 0; // 場景特效：0 = 無；1 = 火雨（見 SceneEffectController，預設 0）
    public int enterEffect = 0; // 進場一次性全螢幕過場＝ScreenFxTable 的 id：0=無 / 1=睜眼醒來 / 2=破幻術 / 3=馬賽克清晰（與劇情 screenFx 共用同一份 id；預設 0）
    public bool noWeapon = false; // 禁止玩家使用武器：0/空 = 可用（預設）；1 = 禁用（劇情地圖、大廳）
    public int envBright = 100;   // 環境亮度 0~100：只在 atmosphere==1 生效，把整張圖壓暗、讓場上的燈照回來（100/空 = 不壓暗）
    public string sceneTip = "";  // 場景說明文字圖 key（空 = 這張圖不顯示場景說明）；圖＝UI/Texts/SceneTipPanel_Text_<key>
    public string atmoTint = "";  // 場景主色染色 RRGGBB（空 = 不染）：暗部往此色相拉、亮度不變（見 AtmosphereController）
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

            // SceneEffect 第 8 欄為新增、向下相容：缺欄 / 留空 / 無法解析都退回預設 0（無）。
            int sceneEffect = 0;
            if (v.Length >= 8 && int.TryParse(v[7].Trim(), out int se)) sceneEffect = se;

            // EnterEffect 第 9 欄為新增、向下相容：缺欄 / 留空 / 無法解析都退回預設 0（無）。
            int enterEffect = 0;
            if (v.Length >= 9 && int.TryParse(v[8].Trim(), out int ee)) enterEffect = ee;

            // NoWeapon 第 10 欄為新增、向下相容：缺欄 / 留空 / 無法解析都退回預設 0（可用武器）。
            bool noWeapon = false;
            if (v.Length >= 10 && int.TryParse(v[9].Trim(), out int nw)) noWeapon = nw != 0;

            // EnvBright 第 11 欄為新增、向下相容：缺欄 / 留空 / 無法解析都退回預設 100（不壓暗＝舊行為）。
            int envBright = 100;
            if (v.Length >= 11 && int.TryParse(v[10].Trim(), out int eb)) envBright = Mathf.Clamp(eb, 0, 100);

            // SceneTip 第 12 欄為新增、向下相容：缺欄 / 留空都 = 不顯示場景說明（舊行為）。
            string sceneTip = v.Length >= 12 ? v[11].Trim() : "";

            // AtmoTint 第 13 欄為新增、向下相容：缺欄 / 留空 = 不染色（舊行為）。
            // 末欄的值會帶著行尾的 \r —— Trim() 一併吃掉（同 name/module 的處理）。
            string atmoTint = v.Length >= 13 ? v[12].Trim() : "";

            var row = new MapTableRow
            {
                id = id,
                name = v[1].Trim(),
                module = v[2].Trim(),
                path = v[3].Trim(),
                isLevelStart = v[4].Trim() == "1",
                mode = mode,
                atmosphere = atmosphere,
                sceneEffect = sceneEffect,
                enterEffect = enterEffect,
                noWeapon = noWeapon,
                envBright = envBright,
                sceneTip = sceneTip,
                atmoTint = atmoTint,
            };

            if (_byId.ContainsKey(id))
                Debug.LogWarning($"[MapTable] 地圖 ID {id} 重複，後者覆蓋前者。");
            _rows.Add(row);
            _byId[id] = row;
        }

        Debug.Log($"[MapTable] 載入 {_rows.Count} 張地圖。");
    }
}
