/// <summary>
/// 陣營：決定「攻擊誰、被誰攻擊、放哪個 Layer」。
///  ‧ Enemy（預設）＝一般敵怪／boss／boss 召喚物：追玩家、接觸傷害打玩家(+友軍)，放 Enemy 層（玩家子彈打得到）。
///  ‧ PlayerAlly＝玩家召喚出來的協戰怪：追最近的敵對目標、接觸傷害打敵對陣營，放 Ally 層（玩家子彈打不到、也不推玩家）。
///  ‧ Neutral＝中立（NPC）：不打任何人、也不被任何人打，放 Ally 層。
///  ‧ Werewolf／Vampire＝**劇本部族**（狼人×吸血鬼三方陣營劇本）：和平期視同中立；
///    開戰（factionWar 鏈動作）後兩族互打（演戲傷害 1/100）＋攻擊玩家；玩家結盟（joinFaction）後
///    該族對玩家轉友好並切回 Ally 層（玩家武器打不到）。狀態只活在「這趟關卡」內。
/// 由 MonsterSpawner.SpawnMonster 的 faction 參數或 MonsterData.csv 的 Faction 欄指定；NPC 由 NpcSpawner 設定。
/// 「誰能傷誰／傷害乘數／放哪層」**只寫在 <see cref="FactionRelations"/> 一處**——要改陣營規則只改它。
/// </summary>
public enum MonsterFaction { Enemy, PlayerAlly, Neutral, Werewolf, Vampire }
