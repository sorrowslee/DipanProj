using UnityEngine;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public struct PrefabMapping
{
    public string Path;
    public GameObject Prefab;
}

public class MonsterSpawner : MonoBehaviour
{
    public TextAsset MonsterCSV;
    public List<PrefabMapping> PrefabMappings = new List<PrefabMapping>();
    public LayerMask EnemyLayer;
    
    private List<MonsterData> _monsterDatabase = new List<MonsterData>();
    private Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();

    void Awake()
    {
        InitializePrefabCache();
        LoadMonsterData();
    }

    private void InitializePrefabCache()
    {
        foreach (var mapping in PrefabMappings)
        {
            if (!string.IsNullOrEmpty(mapping.Path) && mapping.Prefab != null)
            {
                _prefabCache[mapping.Path] = mapping.Prefab;
            }
        }
    }

    [Header("自動生成設定")]
    public bool AutoSpawn = true;         // 關掉 = 純由地圖 monsterSpawn 出生點生怪（推薦給用 MapLoader 的場景）
    public int MonsterIDToSpawn = 1;      // 要生成的怪物 ID
    public float SpawnInterval = 1.0f;    // 生成間隔 (秒)
    public float SpawnRadius = 5.0f;      // 隨機生成範圍
    private float _spawnTimer = 0f;

    void Update()
    {
        if (!AutoSpawn) return;           // 地圖驅動模式：不自動亂生，改由 MapLoader 在出生點生怪
        _spawnTimer += Time.deltaTime;
        if (_spawnTimer >= SpawnInterval)
        {
            _spawnTimer = 0f;
            SpawnRandomMonster();
        }
    }

    private void SpawnRandomMonster()
    {
        // 在半徑內隨機取得位置 (以 Spawner 座標為中心)
        Vector2 randomOffset = Random.insideUnitCircle * SpawnRadius;
        Vector2 spawnPos = (Vector2)transform.position + randomOffset;
        
        SpawnMonster(MonsterIDToSpawn, spawnPos);
    }

    private void LoadMonsterData()
    {
        if (MonsterCSV == null)
        {
            Debug.LogError("Monster CSV is not assigned!");
            return;
        }

        string[] lines = MonsterCSV.text.Split('\n');
        // 跳過標題列 (ID,Name,HP,BrainType,Weapon,Scale,PrefabPath)
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            
            string[] values = lines[i].Split(',');
            if (values.Length < 7) continue;

            MonsterData data = new MonsterData();
            data.ID = int.Parse(values[0]);
            data.Name = values[1];
            data.HP = float.Parse(values[2]);
            data.BrainType = values[3].Trim();   // 必須 Trim：CSV 值常帶前導空白，不 Trim 會讓 BrainType switch（如 RedBridalGown）對不上而掉回 default=Chase
            data.Weapon = values[4].Trim();
            data.Scale = float.Parse(values[5]);
            data.PrefabPath = values[6].Trim();

            data.InvincibleTimeMs = values.Length > 7 ? float.Parse(values[7]) : 0;
            data.KnockbackThreshold = values.Length > 8 ? float.Parse(values[8]) : 0;
            data.KnockbackPercent = values.Length > 9 ? float.Parse(values[9]) : 0;

            data.ContactDamage = (values.Length > 10 && !string.IsNullOrWhiteSpace(values[10])) ? float.Parse(values[10]) : 10f;
            data.DamageReduction = (values.Length > 11 && !string.IsNullOrWhiteSpace(values[11])) ? float.Parse(values[11]) : 0f;
            data.Speed = (values.Length > 12 && !string.IsNullOrWhiteSpace(values[12])) ? float.Parse(values[12]) : 3f;
            data.AnimFPS = (values.Length > 13 && !string.IsNullOrWhiteSpace(values[13])) ? float.Parse(values[13]) : 8f;
            data.AttackInterval = (values.Length > 14 && !string.IsNullOrWhiteSpace(values[14])) ? float.Parse(values[14]) : 0.5f;
            data.DetectionRange = (values.Length > 15 && !string.IsNullOrWhiteSpace(values[15])) ? float.Parse(values[15]) : 10f;

            // 顯示名/頭像（boss 開戰資訊用；一般怪留空即可）。字串欄一律 Trim（CSV 值常帶空白，見 PROBLEMS F4）。
            data.DisplayName = (values.Length > 16 && !string.IsNullOrWhiteSpace(values[16])) ? values[16].Trim() : "";
            data.PortraitPath = (values.Length > 17 && !string.IsNullOrWhiteSpace(values[17])) ? values[17].Trim() : "";

            // 遊戲中說話：句子1~句子4（索引 18~21）。每格可空；有內容才加入。格式見 ParseSpeechLine。
            // ⚠️ CSV 用半形逗號分欄 → 句子內不能有半形逗號，要用全形「，」（見 readme/PROBLEMS）。
            data.SpeechLines.Clear();
            for (int c = 18; c <= 21; c++)
            {
                if (values.Length <= c) break;
                if (ParseSpeechLine(values[c], out var line)) data.SpeechLines.Add(line);
            }

            _monsterDatabase.Add(data);
        }

        Debug.Log($"Loaded {_monsterDatabase.Count} monsters from CSV.");
    }

    /// <summary>
    /// 解析一格說話文本 → <see cref="MonsterSpeechLine"/>。空白格回 false（不加入）。
    /// 前綴「N%:」或「N%：」（半/全形冒號皆可）＝血量比例剩 N% 以下才解鎖；無前綴＝門檻 100（一直可講）。
    /// 例：「我要殺了你~~」→ 門檻100；「30%: 你真的惹怒我了」→ 門檻30、文字「你真的惹怒我了」。
    /// 只有「開頭是數字＋%＋冒號」才當門檻，一般句子含冒號（如「他說：快跑」）不受影響。
    /// </summary>
    private static bool ParseSpeechLine(string raw, out MonsterSpeechLine line)
    {
        line = new MonsterSpeechLine { UnlockAtPercent = 100f, Text = "" };
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string s = raw.Trim();
        int colon = s.IndexOf(':');
        if (colon < 0) colon = s.IndexOf('：');   // 全形冒號也吃
        if (colon > 0)
        {
            string head = s.Substring(0, colon).Trim();
            // head 必須形如「數字%」或「數字％」才視為門檻前綴
            if (head.Length >= 2 && (head[head.Length - 1] == '%' || head[head.Length - 1] == '％'))
            {
                string numPart = head.Substring(0, head.Length - 1).Trim();
                if (float.TryParse(numPart, out float pct))
                {
                    line.UnlockAtPercent = Mathf.Clamp(pct, 0f, 100f);
                    line.Text = s.Substring(colon + 1).Trim();
                    return !string.IsNullOrEmpty(line.Text);
                }
            }
        }

        line.Text = s;   // 無門檻前綴：整句都是文字，門檻 100（一直可講）
        return true;
    }

    /// <summary>依 ID 取怪物配方資料（找不到回 null、不印錯誤）。給 BossIntroPanel 等 UI 端查 DisplayName / PortraitPath 用。</summary>
    public MonsterData GetData(int id) => _monsterDatabase.Find(m => m.ID == id);

    /// <summary>
    /// 生一隻怪。
    /// <paramref name="spawnKey"/>：地圖一次性出生點的唯一 key（有值＝死亡時記進 RunProgress『已清』，本趟不再重生）；召喚物與「重複產生」的出生點留空。
    /// <paramref name="dropsLoot"/>：死亡是否掉寶。留 null＝沿用舊語意（有 spawnKey 才掉）；重複產生的出生點明確傳 true（不記進度但照常掉寶）。
    /// </summary>
    public GameObject SpawnMonster(int id, Vector2 position, string deathFlag = null, MonsterFaction faction = MonsterFaction.Enemy, string spawnKey = null, bool? dropsLoot = null)
    {
        MonsterData data = _monsterDatabase.Find(m => m.ID == id);
        if (data == null)
        {
            Debug.LogError($"Monster ID {id} not found!");
            return null;
        }

        GameObject go;
        if (!string.IsNullOrEmpty(data.PrefabPath) && _prefabCache.TryGetValue(data.PrefabPath, out GameObject prefab))
        {
            // 舊路線（向下相容）：CSV 有填 PrefabPath 且有對應 prefab → 用該 prefab（含自帶 Animator 的舊怪）。
            go = Instantiate(prefab, position, Quaternion.identity);
        }
        else
        {
            // 路線 B（量產）：沒指定 prefab → 程式建一隻通用怪，外觀由 MonsterAnimator 依怪名載圖。
            // 加新怪 = 丟圖到 Monsters/SequenceImage/<怪名>/ ＋ CSV 加一列，不必拉 prefab/Animation。
            go = BuildMonsterGameObject(data.Name, position);
        }

        // Layer：Enemy 陣營用 Inspector 指定的 EnemyLayer（不寫死編號）；PlayerAlly 用 Ally 層（玩家子彈打不到、不推玩家）。
        if (faction == MonsterFaction.PlayerAlly && FactionLayers.AllyLayer >= 0)
        {
            go.layer = FactionLayers.AllyLayer;
        }
        else if (EnemyLayer != 0)
        {
            int layerIndex = Mathf.RoundToInt(Mathf.Log(EnemyLayer.value, 2));
            go.layer = layerIndex;
        }

        // 設定縮放
        go.transform.localScale = Vector3.one * data.Scale;

        MonsterController controller = go.GetComponent<MonsterController>();
        if (controller == null)
        {
            controller = go.AddComponent<MonsterController>();
        }
        
        controller.Initialize(data);
        controller.Faction = faction;       // 陣營：決定追誰/打誰/在哪層（在 Start 之前設好，contact/目標選擇才讀得到）
        if (faction == MonsterFaction.PlayerAlly) controller.SetBrain(new AllyBrain());   // 玩家召喚物＝聰明跟班（跟玩家+打敵怪）
        controller.DeathFlag = deathFlag;   // 出生點 trigger 的「死亡觸發旗標」；此擺放專屬，空＝不寫旗標
        controller.SpawnKey = spawnKey;     // 關卡進度用：本張地圖唯一的出生點 key（有值＝一次性地圖出生怪，死了記進度）。見 RunProgress
        // 掉寶與「記不記進度」拆開：一次性出生點兩者都有；重複產生的出生點只掉寶不記進度；召喚物兩者都沒有（防無限刷）。
        controller.DropsLoot = dropsLoot ?? !string.IsNullOrEmpty(spawnKey);

        // 🟢 初始面向設定：根據主角位置決定面向
        SetInitialOrientation(go);

        return go;   // 回傳生成物件（召喚技能用來追蹤同時存在上限；一般呼叫端可忽略）
    }

    /// <summary>
    /// 路線 B：程式建一隻通用怪的外殼（零 prefab）。只放最基本的 SpriteRenderer；
    /// MonsterController.Initialize / Start 會自行補上 MonsterActuator(帶 Rigidbody2D+CircleCollider2D)、
    /// MonsterAnimator(程式動畫)、HitReaction、接觸傷害、影子等。外觀由 MonsterAnimator 依怪名載圖。
    /// </summary>
    private GameObject BuildMonsterGameObject(string monsterName, Vector2 position)
    {
        var go = new GameObject(string.IsNullOrEmpty(monsterName) ? "Monster" : monsterName);
        go.transform.position = position;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;   // 初始值；實際排序由 MonsterController 掛的 YSortByFeet 每幀依腳底 Y 覆寫（見 MapDepthSort）

        go.AddComponent<MonsterController>();
        return go;
    }

    private void SetInitialOrientation(GameObject monsterGo)
    {
        // 透過 Tag 尋找玩家，不依賴任何具體玩家類別
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        SpriteRenderer sr = monsterGo.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;

        // 依「來源圖朝向」換算（同 MonsterController.HandleVisuals）：面右=玩家在右。
        // 來源朝右 → 面右不翻；來源朝左 → 面右才翻。MonsterController 的 _spriteRenderer 此時可能還沒備好，
        // 故直接讀其欄位、設這顆 sr（生成瞬間先擺對，之後每幀由 MonsterController 維持）。
        var mc = monsterGo.GetComponent<MonsterController>();
        bool srcFacesRight = (mc == null) || mc.SpriteSourceFacesRight;
        float diffX = playerObj.transform.position.x - monsterGo.transform.position.x;
        bool faceRight = diffX > 0;
        sr.flipX = (faceRight != srcFacesRight);
    }

    // 🟢 繪製生成區域，方便在編輯器中確認範圍是否有超出圍牆
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, SpawnRadius);
    }
}
