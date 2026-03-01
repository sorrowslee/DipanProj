using UnityEngine;

public class ChaseBrain : IMonsterBrain
{
    public float StopDistance = 1f; // 不要貼太近

    public void Think(MonsterActuator actuator, Transform player)
    {
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