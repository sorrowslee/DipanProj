using System;
using System.Collections.Generic;

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

    // 陣營（CSV: Faction，末欄）：空＝Enemy（既有行為）；可填 Werewolf/狼人、Vampire/吸血鬼、Neutral/中立。
    // 解析與規則見 FactionRelations；召喚系統呼叫 SpawnMonster 時明確傳 PlayerAlly，不受此欄影響。
    public string FactionStr = "";

    // 逐動作顯示倍率（CSV 表尾三欄，2026-09-17）。**留空＝維持既有的「自動依可見高對齊 idle」**（零行為變化）；
    // 有填就完全覆寫那個自動值。<= 0 視同留空。pant 沿用 IdleScale。
    // ⚠ 為什麼需要手填：自動那套量的是**可見高度**，對「同一個視角、只是畫粗了一圈」很準，但對
    //   **四足獸**（idle 是 3/4 正面站姿、walk 是側面奔跑壓低身體）會**適得其反**——高度矮就被放大。
    //   戰狼實測 walk 被放大 ×1.288，等效寬度 221→285px，比 idle 的 181 大 57%。見 readme/PROBLEMS.md G11 家族。
    //   這種「視角本身換了」的素材，演算法永遠猜不準，作者的眼睛一眼就知道，所以開成手填。
    public float IdleScale = 0f;
    public float WalkScale = 0f;
    public float AttackScale = 0f;

    // 遊戲中怪物頭上會講的話（CSV: 句子1~句子4，最多 4 句）。每句可選前綴「N%:」＝血量剩 N% 以下才解鎖；
    // 無前綴＝一直可講（門檻 100%）。發現玩家後才會定時隨機挑一句「已解鎖」的講（見 MonsterSpeech / MonsterSpeechPanel）。
    public List<MonsterSpeechLine> SpeechLines = new List<MonsterSpeechLine>();
}
