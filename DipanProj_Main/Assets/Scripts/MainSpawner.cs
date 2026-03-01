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

    void Start()
    {
        SpawnPlayer(DefaultPlayerID);
    }

    public void SpawnPlayer(string id)
    {
        PlayerMapping mapping = PlayerMappings.Find(m => m.PlayerID == id);
        if (mapping.Prefab == null)
        {
            Debug.LogError($"Player Prefab with ID '{id}' not found in MainSpawner mappings!");
            return;
        }

        Instantiate(mapping.Prefab, transform.position, Quaternion.identity);
        Debug.Log($"Player '{id}' spawned at {transform.position}");
    }
}
