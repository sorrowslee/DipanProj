using UnityEngine;

public class MonsterController : MonoBehaviour
{
    private MonsterSensor _sensor;
    private MonsterActuator _actuator;
    private IMonsterBrain _brain;

    // 🟢 新增視覺組件參照
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rb;

    public string MonsterName;
    public float MaxHealth = 50f;
    public float HitboxPadding = 0.2f;
    public bool IsFacingRightByDefault = true; // 圖片原始朝向：true = 原始朝左（需 flipX 才朝右）
    private float _currentHealth;
    private bool _isDead = false;

    void Start()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();
        
        // 若沒有經過 Initialize()（手動放置的怪物），在此給予預設值並調整碰撞箱
        if (_brain == null)
        {
            _currentHealth = MaxHealth;
            _sensor = gameObject.AddComponent<MonsterSensor>();
            _actuator = gameObject.AddComponent<MonsterActuator>();
            _brain = new ChaseBrain();
            AutoAdjustCollider();
        }
        // 已經過 Initialize() 的怪物，AutoAdjustCollider 已在其中呼叫，不重複執行
    }

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

    void Update()
    {
        if (_isDead) return;
        Transform player = _sensor.GetTargetPlayer();
        _brain.Think(_actuator, player);

        // 🟢 每一幀處理動畫與轉向
        HandleVisuals(player);
    }

    // 🟢 新增：視覺與動畫控制邏輯
    private void HandleVisuals(Transform player)
    {
        if (_animator == null || _spriteRenderer == null) return;

        // Kinematic 模式下 velocity 永遠為 0，改由 Actuator 的狀態旗標判斷
        _animator.SetBool("isMoving", _actuator != null && _actuator.IsMoving);

        // 2. 左右翻轉 (Flip)：根據玩家位置與圖片原始朝向決定
        if (player != null)
        {
            bool playerIsOnRight = player.position.x > transform.position.x;
            // IsFacingRightByDefault = true 代表圖片原始朝左，需要 flipX 才能朝右
            _spriteRenderer.flipX = IsFacingRightByDefault ? playerIsOnRight : !playerIsOnRight;
        }
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