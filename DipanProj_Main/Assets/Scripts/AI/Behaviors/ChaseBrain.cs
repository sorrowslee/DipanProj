using UnityEngine;

public class ChaseBrain : IMonsterBrain
{
    public float StopDistance = 0.2f; // 貼上玩家才吃得到接觸傷害（怪已是 trigger、不會推玩家，可以重疊）

    public void Think(in MonsterContext ctx)
    {
        MonsterActuator actuator = ctx.Actuator;
        Transform player = ctx.Player;

        if (player == null)
        {
            actuator.Stop();
            return;
        }

        float dist = Vector2.Distance(actuator.transform.position, player.position);
        if (dist > StopDistance)
            actuator.MoveTowards(player.position);
        else
            actuator.Stop();
    }
}