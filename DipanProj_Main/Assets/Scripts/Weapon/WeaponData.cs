using UnityEngine;
using Sorrows.Ballistics;

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

    // ── 雷射外觀（在 WeaponTable 只填編號；數字定義在 BeamStyleLibrary）──
    public BeamStyle BeamStyle;            // 由 BeamStyle 編號(1~10)解析的整組外型參數
    public Color BeamColor = Color.white;  // 由 BeamColor 編號(1~10)解析的顏色
    public float BeamWidth = 0.5f;         // 雷射粗細（視覺與命中共用）

    public RecipeEntry Recipe;
    public GameObject BulletPrefab;
    public Sprite WeaponSprite;
    public Sprite[] WeaponSprites;

    // 雷射砲口/命中圓形光暈素材（由 WeaponManager 載入）
    public Sprite BeamMuzzleSprite;
    public Sprite BeamImpactSprite;
}
