using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class MonsterSpawner : MonoBehaviour
{
    public TextAsset MonsterCSV;
    public GameObject MonsterPrefab; // 暫時先用一個 Prefab 代表雜魚
    
    private List<MonsterData> _monsterDatabase = new List<MonsterData>();

    void Awake()
    {
        LoadMonsterData();
    }

    void Start()
    {
        // 測試：生出一隻 ID 為 1 的怪物
        SpawnMonster(1, transform.position);
    }

    private void LoadMonsterData()
    {
        if (MonsterCSV == null)
        {
            Debug.LogError("Monster CSV is not assigned!");
            return;
        }

        string[] lines = MonsterCSV.text.Split('\n');
        // 跳過標題列 (ID,Name,HP,BrainType,Weapon)
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            
            string[] values = lines[i].Split(',');
            if (values.Length < 5) continue;

            MonsterData data = new MonsterData();
            data.ID = int.Parse(values[0]);
            data.Name = values[1];
            data.HP = float.Parse(values[2]);
            data.BrainType = values[3];
            data.Weapon = values[4];

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

        if (MonsterPrefab == null)
        {
            Debug.LogError("Monster Prefab is not assigned!");
            return;
        }

        GameObject go = Instantiate(MonsterPrefab, position, Quaternion.identity);
        MonsterController controller = go.GetComponent<MonsterController>();
        if (controller == null)
        {
            controller = go.AddComponent<MonsterController>();
        }
        
        controller.Initialize(data);
    }
}
