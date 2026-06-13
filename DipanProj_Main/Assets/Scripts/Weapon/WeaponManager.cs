using UnityEngine;
using System.Collections.Generic;

public class WeaponManager : MonoBehaviour
{
    public TextAsset WeaponCSV;
    public RecipeManager RecipeManager;
    public GameObject BulletPrefab;
    public int CurrentWeaponID = 1;

    private Dictionary<int, WeaponData> _weapons = new Dictionary<int, WeaponData>();
    private List<int> _weaponIDs = new List<int>();
    private WeaponData _currentWeapon;

    void Start()
    {
        LoadWeapons();
        // 初始武器 = 武器表裡的最後一號（_weaponIDs 已排序，最後一個 = 最高 ID）
        if (_weaponIDs.Count > 0)
            CurrentWeaponID = _weaponIDs[_weaponIDs.Count - 1];
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

    // 往前切換武器：往較小 ID 方向移動；到第一個再繞回最後一個
    public void SwitchToPreviousWeapon()
    {
        if (_weaponIDs.Count == 0) return;

        int currentIndex = _weaponIDs.IndexOf(CurrentWeaponID);
        int prevIndex = (currentIndex - 1 + _weaponIDs.Count) % _weaponIDs.Count;
        SwitchWeapon(_weaponIDs[prevIndex]);
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

        if (BulletPrefab == null)
        {
            Debug.LogError("BulletPrefab is not assigned on WeaponManager!");
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
            weapon.WeaponSpritePath = v[4].Trim();
            weapon.SpriteAngleOffset = float.Parse(v[5]);

            weapon.WeaponAniPath = (v.Length > 6) ? v[6].Trim() : "";
            weapon.WeaponAniNumber = (v.Length > 7 && !string.IsNullOrWhiteSpace(v[7])) ? int.Parse(v[7].Trim()) : 0;
            weapon.AnimFPS = (v.Length > 8 && !string.IsNullOrWhiteSpace(v[8])) ? float.Parse(v[8].Trim()) : 0f;
            weapon.BulletScale = (v.Length > 9 && !string.IsNullOrWhiteSpace(v[9])) ? float.Parse(v[9].Trim()) : 1f;

            // 雷射外觀欄位（只填編號；數字定義在 BeamStyleLibrary）
            int styleId = (v.Length > 10 && !string.IsNullOrWhiteSpace(v[10])) ? int.Parse(v[10].Trim()) : 2;   // 預設 2=標準雷射
            int colorId = (v.Length > 11 && !string.IsNullOrWhiteSpace(v[11])) ? int.Parse(v[11].Trim()) : 9;   // 預設 9=白
            weapon.BeamStyle = BeamStyleLibrary.Get(styleId);
            weapon.BeamColor = BeamStyleLibrary.GetColor(colorId);
            weapon.BeamWidth = (v.Length > 12 && !string.IsNullOrWhiteSpace(v[12])) ? float.Parse(v[12].Trim()) : 0.5f;

            // 一次性特效 ID（引用 VfxTable）；留空 / 0 = 不觸發
            weapon.FireEffectID = (v.Length > 13 && !string.IsNullOrWhiteSpace(v[13])) ? int.Parse(v[13].Trim()) : 0;
            weapon.HitEffectID = (v.Length > 14 && !string.IsNullOrWhiteSpace(v[14])) ? int.Parse(v[14].Trim()) : 0;
            weapon.TrailEffectID = (v.Length > 15 && !string.IsNullOrWhiteSpace(v[15])) ? int.Parse(v[15].Trim()) : 0;

            weapon.Recipe = RecipeManager.GetRecipe(weapon.RecipeID);
            weapon.BulletPrefab = BulletPrefab;

            if (weapon.Recipe != null && weapon.Recipe.Data != null && weapon.Recipe.Data.IsLaser)
                LoadBeamAssets(weapon);

            if (!string.IsNullOrEmpty(weapon.WeaponAniPath) && weapon.WeaponAniNumber > 0)
            {
                var sprites = new Sprite[weapon.WeaponAniNumber];
                bool allLoaded = true;
                for (int f = 0; f < weapon.WeaponAniNumber; f++)
                {
                    string framePath = $"{weapon.WeaponAniPath}_{(f + 1):D2}";
                    sprites[f] = Resources.Load<Sprite>(framePath);
                    if (sprites[f] == null)
                    {
                        Debug.LogWarning($"Animation sprite not found at '{framePath}' for weapon '{weapon.Name}'.");
                        allLoaded = false;
                    }
                }
                weapon.WeaponSprites = sprites;
                weapon.WeaponSprite = (allLoaded && sprites.Length > 0) ? sprites[0] : null;
            }
            else if (weapon.Recipe != null && weapon.Recipe.Data != null && weapon.Recipe.Data.IsLaser)
            {
                // 雷射武器使用光束自身視覺（beam_core / 光暈），不需要 WeaponSprite
            }
            else if (!string.IsNullOrWhiteSpace(weapon.WeaponSpritePath))
            {
                Sprite sprite = Resources.Load<Sprite>(weapon.WeaponSpritePath);
                if (sprite != null)
                {
                    weapon.WeaponSprite = sprite;
                }
                else
                {
                    Debug.LogWarning($"Weapon sprite not found at Resources path '{weapon.WeaponSpritePath}' for weapon '{weapon.Name}'.");
                }
            }
            // WeaponSpritePath 留空且非動畫 = 隱形子彈（例如地刺，只靠 TrailEffectID 沿路種刺），不需飛行圖

            _weapons[weapon.ID] = weapon;
            _weaponIDs.Add(weapon.ID);
        }

        _weaponIDs.Sort();
        Debug.Log($"Loaded {_weapons.Count} weapons from CSV.");
    }

    // 雷射光暈素材路徑（相對 Resources/，不含副檔名）。光束本體已全參數化，不再需要 beam 貼圖。
    private const string BeamMuzzlePath = "Laser/laser_glow";
    private const string BeamImpactPath = "Laser/laser_impact";

    private void LoadBeamAssets(WeaponData weapon)
    {
        weapon.BeamMuzzleSprite = Resources.Load<Sprite>(BeamMuzzlePath);
        weapon.BeamImpactSprite = Resources.Load<Sprite>(BeamImpactPath);
    }
}
