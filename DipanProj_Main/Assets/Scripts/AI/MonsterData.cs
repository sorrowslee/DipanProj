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

    public string DisplayName = "";     // 顯示名稱（boss 開戰資訊等 UI 用；留空＝退回 Name）。注意 Name 是程式鍵（動畫資料夾/GameObject 名），不能拿來顯示。
    public string PortraitPath = "";    // 頭像立繪 catalog id（例 Modules/RedBridalGown/Talk/redBridalGown_angry；boss 開戰資訊用，與 Talk 立繪同一條管線）

    public float ContactDamage = 10f;   // 碰到玩家造成的傷害（留空 = 10）
    public float AttackInterval = 0.5f; // 接觸攻擊間隔秒＝攻速（CSV: AttackInterval，越小攻越快，留空 = 0.5）
    public float DamageReduction = 0f;   // 受擊減傷 %（留空 = 0；掛勾，之後接減傷/抗性）
    public float DetectionRange = 10f;  // 感測玩家半徑（CSV: DetectionRange，留空 = 10）。怪 dist<=此值才發現玩家去追；紅嫁衣房約 18x10、對角 ~20.6，要全域看到玩家設 ~25。
}
