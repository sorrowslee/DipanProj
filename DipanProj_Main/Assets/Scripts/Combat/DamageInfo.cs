using UnityEngine;

/// <summary>
/// 一次傷害事件的完整描述。取代過去到處傳的「裸 float」，讓中央的 <see cref="CombatSystem"/>
/// 能依來源／目標／類型套用加成、減傷、抗性等修正。見 readme/COMBAT.md。
/// </summary>
public struct DamageInfo
{
    /// <summary>傷害來源（玩家／怪物／地面特效…）。可為 null（例如無主的地面特效）。用來讀「攻擊方加成」。</summary>
    public GameObject Source;

    /// <summary>受擊目標（必填）。用來讀「受擊方減傷」並結算到其 <see cref="IDamageable"/>。</summary>
    public GameObject Target;

    /// <summary>武器／效果的「原始」傷害（修正前）。通常 = 武器表 Damage 或地面特效 Damage。</summary>
    public float BaseAmount;

    /// <summary>擊退方向（來源 → 目標）。傳給目標的受擊反應用。</summary>
    public Vector2 HitDirection;

    /// <summary>傷害類型（給未來的抗性／屬性克制用；目前管線只是帶著走）。</summary>
    public DamageType Type;

    /// <summary>是否為持續傷害（DOT／debuff／地面特效）。供未來區分 i-frame 規則、特效表現用。</summary>
    public bool IsDot;

    /// <summary>真實傷害：跳過所有加成／減傷修正，直接以 BaseAmount 結算（仍受目標無敵時間影響）。</summary>
    public bool IgnoreModifiers;
}

/// <summary>傷害類型。目前僅 Physical 在用，其餘為未來的屬性／抗性系統預留。</summary>
public enum DamageType
{
    Physical = 0,
    Fire = 1,
    Ice = 2,
    Lightning = 3,
    Poison = 4,
    /// <summary>無視一切減傷／抗性的「真實傷害」。</summary>
    True = 99,
}

/// <summary>
/// 可選元件：任何「會影響傷害數值」的戰鬥單位都可實作（玩家的 <see cref="CombatStats"/>、怪物的
/// MonsterController…）。<see cref="CombatSystem"/> 會在來源上讀 Outgoing、在目標上讀 Incoming。
/// 沒有實作此介面的物件 = 沒有任何修正（乘數 1）。加成／減傷目前多半回傳 1（掛勾就位、之後接 CSV）。
/// </summary>
public interface ICombatModifiers
{
    /// <summary>作為「攻擊方」時，對打出去的傷害乘上的倍率（1 = 不變；1.2 = +20% 傷害加成）。</summary>
    float OutgoingDamageMultiplier(in DamageInfo info);

    /// <summary>作為「受擊方」時，對受到的傷害乘上的倍率（1 = 不變；0.8 = 減傷 20%）。</summary>
    float IncomingDamageMultiplier(in DamageInfo info);
}
