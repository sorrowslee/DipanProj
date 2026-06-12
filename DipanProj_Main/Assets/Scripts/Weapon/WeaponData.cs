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

    // ── 雷射外觀（換風格只改這幾欄；行為仍在 RecipeTable）──
    public string BeamTexturePath;
    public Color BeamColor = Color.white;
    public float BeamWidth = 0.5f;
    public float ScrollSpeed = 0f;

    public RecipeEntry Recipe;
    public GameObject BulletPrefab;
    public Sprite WeaponSprite;
    public Sprite[] WeaponSprites;

    // 雷射用素材（由 WeaponManager 載入）
    public Texture2D BeamTexture;
    public Sprite BeamMuzzleSprite;
    public Sprite BeamImpactSprite;
}
