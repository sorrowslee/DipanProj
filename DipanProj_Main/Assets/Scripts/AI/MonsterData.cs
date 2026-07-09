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

    public float Speed = 3f;            // 移動速度（CSV: Speed，留空 = 3）

    public float AnimFPS = 8f;          // 程式逐格動畫播放幀率（CSV: AnimFPS，留空 = 8；見 MonsterAnimator）

    public float InvincibleTimeMs;
    public float KnockbackThreshold;
    public float KnockbackPercent;

    public float ContactDamage = 10f;   // 碰到玩家造成的傷害（留空 = 10）
    public float AttackInterval = 0.5f; // 接觸攻擊間隔秒＝攻速（CSV: AttackInterval，越小攻越快，留空 = 0.5）
    public float DamageReduction = 0f;   // 受擊減傷 %（留空 = 0；掛勾，之後接減傷/抗性）
}
