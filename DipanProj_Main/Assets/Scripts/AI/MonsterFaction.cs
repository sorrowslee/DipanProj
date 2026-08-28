/// <summary>
/// 陣營：決定「攻擊誰、被誰攻擊、放哪個 Layer」。
///  ‧ Enemy（預設）＝一般敵怪／boss／boss 召喚物：追玩家、接觸傷害打玩家(+友軍)，放 Enemy 層（玩家子彈打得到）。
///  ‧ PlayerAlly＝玩家召喚出來的協戰怪：追最近的敵怪、接觸傷害打敵怪，放 Ally 層（玩家子彈打不到、也不推玩家）。
///  ‧ Neutral＝中立（NPC）：不打任何人、也不被任何人打，放 Ally 層。
/// 由 MonsterSpawner.SpawnMonster 的 faction 參數指定（召喚時 SummonSystem 傳入）；NPC 由 NpcSpawner 設定。
/// 「誰能傷誰」的判定**只寫在 <see cref="FactionRelations"/> 一處**——未來做多方陣營（狼人/血族互打、
/// 玩家變陣營）時把那裡改成查關係表即可，其他程式不動。
/// </summary>
public enum MonsterFaction { Enemy, PlayerAlly, Neutral }
