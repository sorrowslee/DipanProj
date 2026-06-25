using System;

[Serializable]
public class MonsterData
{
    public int ID;
    public string Name;
    public float HP;
    public string BrainType;
    public string Weapon;
    public float Scale;
    public string PrefabPath;

    public float InvincibleTimeMs;
    public float KnockbackThreshold;
    public float KnockbackPercent;

    public float ContactDamage = 10f;   // 碰到玩家造成的傷害（留空 = 10）
    public float DamageReduction = 0f;   // 受擊減傷 %（留空 = 0；掛勾，之後接減傷/抗性）
}
