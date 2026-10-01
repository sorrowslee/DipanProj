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
    public bool FlipYWhenLeft;          // 往左飛時上下翻轉子彈圖（有上下之分的圖用，例：引魂幡鬼頭）
    public bool HitEffectAlignBullet;   // 命中特效跟子彈同角度／同翻轉（有方向性的命中圖用，例：餓鬼牙符往哪飛就往哪咬）
    public int SlashStyle;              // 近戰揮擊刀光樣式（0＝不畫、走舊行為；1＝血月三爪）。見 MeleeSlashFx
    public bool HitEffectEnemyOnly;     // 命中特效只在打到怪時播（打牆／可破壞地上物不播；血花這類圖用，例：血滴子）。只對有「命中對象」的模式有效，見 WeaponModeSpec

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
    public string PixelBeamSet;            // 空白 = shader 雷射；A_Blue = Pack 4 像素砲口／平鋪中心／撞擊端

    // ── 一次性特效（VFX）ID，引用 VfxTable；0 / 留空 = 不觸發 ──
    public int FireEffectID;   // 發射時在玩家身上播放（朝瞄準方向）
    public int HitEffectID;    // 子彈／光束命中怪物、障礙物、拋物線落地時，在命中點播放
    public int TrailEffectID;  // 沿子彈飛行路徑每隔 TrailStep 距離種一個（地刺武器靠這個沿路長出尖刺）
    public int SummonEffectID; // 召喚型武器：在每個生怪點播放一次，特效播完才生怪；0 / 留空 = 不播、立即生怪

    // ── 浮游本體（只有 Mode=Familiar 讀；見 WeaponFamiliar）──
    public int FamiliarVfxId;          // 本體外觀（VfxTable ID，該列 Loop=1、Duration=-1）。0 = 沒有本體 ⇒ 不運作
    public float FamiliarSize = 0.8f;  // 單顆本體顯示高度（世界單位，再乘血統體型）
    public float FamiliarSpin = 60f;   // 繞行轉速（度/秒，正＝逆時針）

    public RecipeEntry Recipe;
    public GameObject BulletPrefab;
    public Sprite WeaponSprite;
    public Sprite[] WeaponSprites;

    // 雷射砲口/命中圓形光暈素材（由 WeaponManager 載入）
    public Sprite BeamMuzzleSprite;
    public Sprite BeamImpactSprite;
}
