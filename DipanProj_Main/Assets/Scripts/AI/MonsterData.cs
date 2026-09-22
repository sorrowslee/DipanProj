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

    // jump 的顯示倍率（CSV 表尾，接在 AttackScale 後面）。
    // ⚠ **留空的語義和上面三個不一樣**：上面三個留空＝走「自動依可見高對齊 idle」，
    //   jump 留空＝**直接沿用 idle 的顯示倍率、不做自動對齊**。
    //   因為跳躍的可見高度**本來就是動作的內容**（蹲下時矮、騰空伸展時又不同），
    //   對它做高度正規化等於把跳躍最重要的那段身體變化整個抵銷，而且越蜷縮的幀被放得越大
    //   ⇒ 騰空時怪會忽然膨脹一圈（狂族皇家衛士實測 1.05~1.31 倍）。詳見 readme/BOSS_MODULE.md §9.7。
    //   有填就完全照填的走（作者自己看畫面決定，永遠比演算法準）。
    public float JumpScale = 0f;

    // ── 跳躍踐踏（BrainType=LeapSlam）專用，CSV 表尾兩欄。其他怪一律留空、用不到 ──
    // 留空/0 時由 LeapSlamBrain 給退路（傷害＝ContactDamage×2、半徑＝1.6），所以既有怪不填也不會壞。
    // 揮舞型近戰（BrainType=MeleeChase／LeapSlam）的**命中幀**：attack 序列圖的第幾張是「武器揮到位」。
    // 留空＝張數 × 0.7 的粗估（能動，但**不準**）。
    // ⚠ **這個值一定要逐怪量**，比例完全靠不住——實測：狂族皇家衛士 9/12（75%）、狼人兵 8/24（33%）、
    //   吸血鬼兵 11/25（44%）。量法：印出 attack 每一幀不透明像素的 bbox，**寬度／邊緣突然暴增的那一幀**
    //   就是武器揮出去的時刻（見 readme/BOSS_MODULE.md §10.4）。
    public int AttackHitFrame = 0;

    public float LeapDamage = 0f;   // 落地踐踏的一次性傷害
    public float LeapRadius = 0f;   // 踐踏殺傷半徑（世界單位）；地面裂痕的視覺大小也吃它

    // ── 射手型（BrainType=Archer）專用，CSV 表尾一欄 ──
    // attack 序列圖的第幾張是「武器已經舉定、可以射了」。留空/0 ＝ ArcherBrain 的退路值 14。
    // ⚠ **幀號即事件**（同 AttackHitFrame）：寫幀號而不是秒數，所以改 CSV 的 AnimFPS 時機會自動跟著對。
    // ⚠ 一定要逐怪量：狂族弩手 attack 25 張、14（維持瞄準那段的開頭）；
    //   ZhaYu_Gun attack 22 張、幀 6 就完全水平舉定且 6~22 都維持，取 9（舉定後穩住三幀才射）。
    //   量法同 AttackHitFrame：印每幀不透明像素的 bbox，武器伸到定位後 bbox 就不再變化。
    public int ReleaseFrame = 0;

    // ── 自爆型（BrainType=SuicideBomb）專用，CSV 表尾三欄 ──
    // 留空/0 時由 SuicideBombBrain 給退路（傷害＝ContactDamage×3、半徑＝1.8、引信＝0.6 秒），
    // 所以既有怪不填也不會壞。
    // ⚠ **實際殺傷範圍 ＝ BombRadius ＋ 目標碰撞框半徑**（玩家約 0.5）——同 LeapRadius，
    //   因為傷害走 EnemyContactDamage 的「兩個碰撞框邊緣距離」（見 ImpactDamageArea 的註解）。
    public float BombDamage = 0f;
    public float BombRadius = 0f;
    public float BombFuse = 0f;     // 引信秒數：貼近後站定閃爍幾秒才爆。進了引信就一定會爆（見 SuicideBombBrain）

    // 遊戲中怪物頭上會講的話（CSV: 句子1~句子4，最多 4 句）。每句可選前綴「N%:」＝血量剩 N% 以下才解鎖；
    // 無前綴＝一直可講（門檻 100%）。發現玩家後才會定時隨機挑一句「已解鎖」的講（見 MonsterSpeech / MonsterSpeechPanel）。
    public List<MonsterSpeechLine> SpeechLines = new List<MonsterSpeechLine>();
}
