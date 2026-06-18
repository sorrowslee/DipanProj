using UnityEngine;
using System.Collections.Generic;

public class MainSpawner : MonoBehaviour
{
    [System.Serializable]
    public struct PlayerMapping
    {
        public string PlayerID;
        public GameObject Prefab;
    }

    public List<PlayerMapping> PlayerMappings = new List<PlayerMapping>();
    public string DefaultPlayerID = "Player";

    // 由 MapManager 接管生玩家時請關掉（MapManager 在 Awake 會主動關閉，防止重複生成）。
    public bool SpawnOnStart = true;

    void Start()
    {
        if (SpawnOnStart) SpawnPlayer(DefaultPlayerID);
    }

    public GameObject SpawnPlayer(string id)
    {
        PlayerMapping mapping = PlayerMappings.Find(m => m.PlayerID == id);
        if (mapping.Prefab == null)
        {
            Debug.LogError($"Player Prefab with ID '{id}' not found in MainSpawner mappings!");
            return null;
        }

        GameObject go = Instantiate(mapping.Prefab, transform.position, Quaternion.identity);
        Debug.Log($"Player '{id}' spawned at {transform.position}");
        return go;
    }
}
