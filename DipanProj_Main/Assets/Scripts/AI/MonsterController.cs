using UnityEngine;

public class MonsterController : MonoBehaviour
{
    private MonsterSensor _sensor;
    private MonsterActuator _actuator;
    private IMonsterBrain _brain;

    public float MaxHealth = 50f;
    private float _currentHealth;
    private bool _isDead = false;

    void Start()
    {
        _sensor = gameObject.AddComponent<MonsterSensor>();
        _actuator = gameObject.AddComponent<MonsterActuator>();
        
        // 目前先寫死是追擊大腦，以後可以從外部傳入
        _brain = new ChaseBrain(); 

        _currentHealth = MaxHealth;
    }

    void Update()
    {
        Transform player = _sensor.GetTargetPlayer();
        _brain.Think(_actuator, player);
    }

    // 預留給彈道系統的傷害介面
    public void TakeDamage(float amount)
    {
        if (_isDead) return;

        _currentHealth -= amount;

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