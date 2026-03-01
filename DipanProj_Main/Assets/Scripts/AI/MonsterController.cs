using UnityEngine;

public class MonsterController : MonoBehaviour
{
    private MonsterSensor _sensor;
    private MonsterActuator _actuator;
    private IMonsterBrain _brain;

    public string MonsterName;
    public float MaxHealth = 50f;
    private float _currentHealth;
    private bool _isDead = false;

    public void Initialize(MonsterData data)
    {
        MonsterName = data.Name;
        MaxHealth = data.HP;
        _currentHealth = MaxHealth;

        _sensor = gameObject.GetComponent<MonsterSensor>();
        if (_sensor == null) _sensor = gameObject.AddComponent<MonsterSensor>();
        
        _actuator = gameObject.GetComponent<MonsterActuator>();
        if (_actuator == null) _actuator = gameObject.AddComponent<MonsterActuator>();

        // 根據 BrainType 決定 AI 邏輯
        switch (data.BrainType)
        {
            case "Chase":
                _brain = new ChaseBrain();
                break;
            default:
                _brain = new ChaseBrain(); // 預設追擊
                break;
        }
    }

    void Start()
    {
        // 如果沒有外部初始化，則給予預設值
        if (_brain == null)
        {
            _currentHealth = MaxHealth;
            _sensor = gameObject.AddComponent<MonsterSensor>();
            _actuator = gameObject.AddComponent<MonsterActuator>();
            _brain = new ChaseBrain();
        }
    }

    void Update()
    {
        if (_isDead) return;
        Transform player = _sensor.GetTargetPlayer();
        _brain.Think(_actuator, player);
    }

    // 預留給彈道系統的傷害介面
    public void TakeDamage(float amount)
    {
        if (_isDead) return;

        _currentHealth -= amount;
        Debug.Log($"{MonsterName} took {amount} damage. HP: {_currentHealth}/{MaxHealth}");

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        _isDead = true;
        // 簡單處理：直接銷毀物件
        Destroy(gameObject);
    }
}