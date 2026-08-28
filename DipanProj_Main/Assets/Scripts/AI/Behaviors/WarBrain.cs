using UnityEngine;

/// <summary>
/// 三方陣營劇本的部族戰士（狼人／吸血鬼）：**追最近的敵對目標貼上去互咬**。
/// 目標由 MonsterController 依 <see cref="FactionRelations"/> 算好塞進 ctx：
///  ‧ ctx.Enemy＝感測範圍內最近的敵對怪（開戰後＝另一族；玩家結盟後也含玩家召喚物）。
///  ‧ ctx.Player＝會攻擊玩家時（開戰且未結盟）感測範圍內的玩家，否則 null。
/// 兩個都有＝挑比較近的——作者刻意把兩族擺得近，開戰瞬間他們彼此最近、自然先互咬而不衝主角（設計即規則，不寫特例）。
/// 和平期兩個目標都是 null → 原地發呆（部族戰士本來就是開戰旗標成立後才由出生點生出來的）。
/// 傷害走既有接觸傷害（EnemyContactDamage），兩族互打的 1/100 演戲乘數在 CombatSystem 統一結算。
/// </summary>
public class WarBrain : IMonsterBrain
{
    const float AttackStop = 0.2f;   // 貼上目標才吃得到接觸傷害（怪都是 trigger、可重疊，同 ChaseBrain/AllyBrain）

    public void Think(in MonsterContext ctx)
    {
        MonsterActuator act = ctx.Actuator;
        Vector2 pos = act.transform.position;

        Transform target = Nearest(pos, ctx.Enemy, ctx.Player);
        if (target == null) { act.Stop(); return; }

        float d = Vector2.Distance(pos, target.position);
        if (d > AttackStop) act.MoveTowards(target.position);
        else act.Stop();
    }

    static Transform Nearest(Vector2 pos, Transform a, Transform b)
    {
        if (a == null) return b;
        if (b == null) return a;
        float da = ((Vector2)a.position - pos).sqrMagnitude;
        float db = ((Vector2)b.position - pos).sqrMagnitude;
        return da <= db ? a : b;
    }
}
