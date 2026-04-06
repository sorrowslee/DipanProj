using UnityEngine;

public class WeaponData
{
    public int ID;
    public string Name;
    public float Damage;
    public int RecipeID;
    public string BulletPrefabPath;
    public string WeaponSpritePath;

    public RecipeEntry Recipe;
    public GameObject BulletPrefab;
}
