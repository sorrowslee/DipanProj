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

    private Transform _playerTransform;

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            _playerTransform = playerObj.transform;
    }

    public Transform GetTargetPlayer()
    {
        // 若快取遺失（玩家死亡/重生），嘗試重新尋找一次
        if (_playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null) return null;
            _playerTransform = playerObj.transform;
        }

        float dist = Vector2.Distance(transform.position, _playerTransform.position);
        return dist <= DetectionRange ? _playerTransform : null;
    }
}