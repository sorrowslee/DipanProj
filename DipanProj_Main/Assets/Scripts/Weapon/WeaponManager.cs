using UnityEngine;
using System.Collections.Generic;

public class WeaponManager : MonoBehaviour
{
    public TextAsset WeaponCSV;
    public RecipeManager RecipeManager;
    public int CurrentWeaponID = 1;

    private Dictionary<int, WeaponData> _weapons = new Dictionary<int, WeaponData>();
    private WeaponData _currentWeapon;

    void Start()
    {
        LoadWeapons();
        RefreshCurrentWeapon();
    }

    public WeaponData GetCurrentWeapon()
    {
        return _currentWeapon;
    }

    public void SwitchWeapon(int weaponID)
    {
        CurrentWeaponID = weaponID;
        RefreshCurrentWeapon();
    }

    public WeaponData GetWeapon(int id)
    {
        if (_weapons.TryGetValue(id, out WeaponData weapon))
            return weapon;

        Debug.LogError($"Weapon ID {id} not found!");
        return null;
    }

    private void RefreshCurrentWeapon()
    {
        _currentWeapon = GetWeapon(CurrentWeaponID);
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

            GameObject prefab = Resources.Load<GameObject>(weapon.BulletPrefabPath);
            if (prefab != null)
            {
                weapon.BulletPrefab = prefab;
            }
            else
            {
                Debug.LogError($"Bullet prefab not found at Resources path '{weapon.BulletPrefabPath}' for weapon '{weapon.Name}'. Make sure the prefab is under Assets/Resources/.");
            }

            _weapons[weapon.ID] = weapon;
        }

        Debug.Log($"Loaded {_weapons.Count} weapons from CSV.");
    }
}
