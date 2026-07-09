using UnityEngine;

/// <summary>
/// 玩家召喚物（PlayerAlly）的行為：**聰明跟班**。
///  ‧ 附近有敵怪（AggroRange 內）→ 衝過去貼上打（接觸傷害）。
///  ‧ 沒有敵怪、或敵怪太遠 → 跟著玩家走（保持 FollowNear 距離，不黏太緊、不推玩家）。
/// 由 MonsterController 在 PlayerAlly 陣營時掛上（見 MonsterSpawner）。敵怪與玩家由 MonsterContext 傳入
/// （Self.FindNearestEnemy 走登記表算好塞進 ctx.Enemy、ctx.Player＝玩家）。
/// </summary>
public class AllyBrain : IMonsterBrain
{
    const float AggroRange = 7f;    // 敵怪在這距離內就去打（否則優先跟玩家）
    const float AttackStop = 1.0f;  // 打敵怪的停止距離（貼上去才吃得到接觸傷害）
    const float FollowNear = 2.2f;  // 跟玩家：距離 > 這值才靠近，內就停（不黏太緊）

    public void Think(in MonsterContext ctx)
    {
        MonsterActuator act = ctx.Actuator;
        Transform enemy = ctx.Enemy;
        Transform player = ctx.Player;

        // 1) 有夠近的敵怪 → 去打
        if (enemy != null)
        {
            float de = Vector2.Distance(act.transform.position, enemy.position);
            if (de <= AggroRange)
            {
                if (de > AttackStop) act.MoveTowards(enemy.position);
                else act.Stop();
                return;
            }
        }

        // 2) 否則跟玩家
        if (player != null)
        {
            float dp = Vector2.Distance(act.transform.position, player.position);
            if (dp > FollowNear) act.MoveTowards(player.position);
            else act.Stop();
        }
        else act.Stop();
    }
}
