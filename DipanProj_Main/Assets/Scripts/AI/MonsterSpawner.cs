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
            data.BrainType = values[3];
            data.Weapon = values[4];
            data.Scale = float.Parse(values[5]);
            data.PrefabPath = values[6].Trim();

            data.InvincibleTimeMs = values.Length > 7 ? float.Parse(values[7]) : 0;
            data.KnockbackThreshold = values.Length > 8 ? float.Parse(values[8]) : 0;
            data.KnockbackPercent = values.Length > 9 ? float.Parse(values[9]) : 0;

            _monsterDatabase.Add(data);
        }
        
        Debug.Log($"Loaded {_monsterDatabase.Count} monsters from CSV.");
    }

    public void SpawnMonster(int id, Vector2 position)
    {
        MonsterData data = _monsterDatabase.Find(m => m.ID == id);
        if (data == null)
        {
            Debug.LogError($"Monster ID {id} not found!");
            return;
        }

        if (!_prefabCache.TryGetValue(data.PrefabPath, out GameObject prefab))
        {
            Debug.LogError($"Prefab for path '{data.PrefabPath}' not found in mappings!");
            return;
        }

        GameObject go = Instantiate(prefab, position, Quaternion.identity);

        // 將 Layer 設為 Inspector 指定的 EnemyLayer，不寫死編號
        if (EnemyLayer != 0)
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

        // 🟢 初始面向設定：根據主角位置決定面向
        SetInitialOrientation(go);
    }

    private void SetInitialOrientation(GameObject monsterGo)
    {
        // 透過 Tag 尋找玩家，不依賴任何具體玩家類別
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        SpriteRenderer sr = monsterGo.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;

        float diffX = playerObj.transform.position.x - monsterGo.transform.position.x;
        sr.flipX = diffX > 0;
    }

    // 🟢 繪製生成區域，方便在編輯器中確認範圍是否有超出圍牆
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, SpawnRadius);
    }
}
