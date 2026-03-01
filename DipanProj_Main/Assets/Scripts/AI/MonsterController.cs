using UnityEngine;

public class MonsterController : MonoBehaviour
{
    private MonsterSensor _sensor;
    private MonsterActuator _actuator;
    private IMonsterBrain _brain;

    public string MonsterName;
    public float MaxHealth = 50f;
    public float HitboxPadding = 0.2f; // 🟢 受擊判定補償：讓 Hitbox 比圖片稍微大一點
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

        // 自動調整碰撞箱以符合圖片大小並增加慷慨判定
        AutoAdjustCollider();
    }

    private void AutoAdjustCollider()
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        Collider2D col = GetComponent<Collider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();

        if (col is BoxCollider2D box)
        {
            // 🟢 安全保護：確保 size 不會小於等於 0
            float sizeX = Mathf.Max(0.01f, sr.sprite.bounds.size.x + HitboxPadding);
            float sizeY = Mathf.Max(0.01f, sr.sprite.bounds.size.y + HitboxPadding);
            box.size = new Vector2(sizeX, sizeY);
            box.offset = sr.sprite.bounds.center;
        }
        else if (col is CircleCollider2D circle)
        {
            float maxDim = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
            // 🟢 安全保護：確保 radius 不會小於等於 0
            circle.radius = Mathf.Max(0.005f, (maxDim / 2f) + (HitboxPadding / 2f));
            circle.offset = sr.sprite.bounds.center;
        }
    }

    // 🟢 在編輯器中顯示紅色受擊範圍，方便即時調整
    private void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = Color.red;
            if (col is BoxCollider2D box)
                Gizmos.DrawWireCube(transform.TransformPoint(box.offset), box.size);
            else if (col is CircleCollider2D circle)
                Gizmos.DrawWireSphere(transform.TransformPoint(circle.offset), circle.radius);
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

        // 確保手動放置的怪物也會自動調整判定範圍
        AutoAdjustCollider();
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