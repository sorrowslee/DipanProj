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
}
