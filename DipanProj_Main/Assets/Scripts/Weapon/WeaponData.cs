using UnityEngine;

public class WeaponData
{
    public int ID;
    public string Name;
    public float Damage;
    public int RecipeID;
    public string WeaponSpritePath;
    public float SpriteAngleOffset;

    public string WeaponAniPath;
    public int WeaponAniNumber;
    public float AnimFPS;
    public float BulletScale = 1f;

    public RecipeEntry Recipe;
    public GameObject BulletPrefab;
    public Sprite WeaponSprite;
    public Sprite[] WeaponSprites;
}
