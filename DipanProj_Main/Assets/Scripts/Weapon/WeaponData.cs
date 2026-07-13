using UnityEngine;
using Sorrows.Ballistics;

public class WeaponData
{
    public int ID;
    public string Name;
    public float Damage;
    public float ManaCost = 1f;   // 發射消耗的魔力（離散武器每發；雷射/佛光每秒）。留空 = 1。見 readme/COMBAT.md
    public int RecipeID;
    public string WeaponSpritePath;
    public float SpriteAngleOffset;

    public string WeaponAniPath;
    public int WeaponAniNumber;
    public float AnimFPS;
    public float BulletScale = 1f;
    // 單次施放的視覺倍率快照；一般射擊為 1，完整集氣射擊為 2。
    public float CastVisualScale = 1f;

    // ── 雷射外觀（在 WeaponTable 只填編號；數字定義在 BeamStyleLibrary）──
    public BeamStyle BeamStyle;            // 由 BeamStyle 編號(1~10)解析的整組外型參數
    public Color BeamColor = Color.white;  // 由 BeamColor 編號(1~10)解析的顏色
    public float BeamWidth = 0.5f;         // 雷射粗細（視覺與命中共用）

    // ── 一次性特效（VFX）ID，引用 VfxTable；0 / 留空 = 不觸發 ──
    public int FireEffectID;   // 發射時在玩家身上播放（朝瞄準方向）
    public int HitEffectID;    // 子彈／光束命中怪物、障礙物、拋物線落地時，在命中點播放
    public int TrailEffectID;  // 沿子彈飛行路徑每隔 TrailStep 距離種一個（地刺武器靠這個沿路長出尖刺）
    public int SummonEffectID; // 召喚型武器：在每個生怪點播放一次，特效播完才生怪；0 / 留空 = 不播、立即生怪

    public RecipeEntry Recipe;
    public GameObject BulletPrefab;
    public Sprite WeaponSprite;
    public Sprite[] WeaponSprites;

    // 雷射砲口/命中圓形光暈素材（由 WeaponManager 載入）
    public Sprite BeamMuzzleSprite;
    public Sprite BeamImpactSprite;
}
