using UnityEngine;
using System.Collections.Generic;

public class WeaponManager : MonoBehaviour
{
    public TextAsset WeaponCSV;
    public RecipeManager RecipeManager;
    public List<PrefabMapping> BulletPrefabMappings = new List<PrefabMapping>();

    private Dictionary<int, WeaponData> _weapons = new Dictionary<int, WeaponData>();
    private Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();

    void Awake()
    {
        InitializePrefabCache();
    }

    void Start()
    {
        LoadWeapons();
    }

    public WeaponData GetWeapon(int id)
    {
        if (_weapons.TryGetValue(id, out WeaponData weapon))
            return weapon;

        Debug.LogError($"Weapon ID {id} not found!");
        return null;
    }

    private void InitializePrefabCache()
    {
        foreach (var mapping in BulletPrefabMappings)
        {
            if (!string.IsNullOrEmpty(mapping.Path) && mapping.Prefab != null)
            {
                _prefabCache[mapping.Path] = mapping.Prefab;
            }
        }
    }

    private void LoadWeapons()
    {
        if (WeaponCSV == null)
        {
            Debug.LogError("Weapon CSV is not assigned!");
            return;
        }

        string[] lines = WeaponCSV.text.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] v = lines[i].Split(',');
            if (v.Length < 6) continue;

            var weapon = new WeaponData();
            weapon.ID = int.Parse(v[0]);
            weapon.Name = v[1].Trim();
            weapon.Damage = float.Parse(v[2]);
            weapon.RecipeID = int.Parse(v[3]);
            weapon.BulletPrefabPath = v[4].Trim();
            weapon.WeaponSpritePath = v[5].Trim();

            weapon.Recipe = RecipeManager.GetRecipe(weapon.RecipeID);

            if (_prefabCache.TryGetValue(weapon.BulletPrefabPath, out GameObject prefab))
            {
                weapon.BulletPrefab = prefab;
            }
            else
            {
                Debug.LogWarning($"Bullet prefab for path '{weapon.BulletPrefabPath}' not found in mappings for weapon '{weapon.Name}'.");
            }

            _weapons[weapon.ID] = weapon;
        }

        Debug.Log($"Loaded {_weapons.Count} weapons from CSV.");
    }
}
