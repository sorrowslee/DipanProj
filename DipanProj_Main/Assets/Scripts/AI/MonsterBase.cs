using UnityEngine;

// 決策機介面
public interface IMonsterBrain
{
    void Think(MonsterActuator actuator, Transform player);
}

// 感知器：負責找玩家
public class MonsterSensor : MonoBehaviour
{
    public float DetectionRange = 10f;
    public Transform GetTargetPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        
        if (playerObj == null) {
            return null;
        }

        float dist = Vector2.Distance(transform.position, playerObj.transform.position);
        if (dist <= DetectionRange) {
            return playerObj.transform;
        } else {
            return null;
        }
    }
}