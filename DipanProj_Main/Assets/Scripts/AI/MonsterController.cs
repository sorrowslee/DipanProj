using UnityEngine;

public class MonsterController : MonoBehaviour, IDamageable, ICombatModifiers
{
    private MonsterSensor _sensor;
    private MonsterActuator _actuator;
    private IMonsterBrain _brain;
    private HitReactionHandler _hitReaction;

    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rb;

    public string MonsterName;
    public float MaxHealth = 50f;
    public float HitboxPadding = 0.2f;
    public bool IsFacingRightByDefault = true;
    private float _currentHealth;
    private bool _isDead = false;

    [Header("Hit Reaction")]
    public float InvincibleTimeMs = 0f;
    public float KnockbackThreshold = 0f;
    public float KnockbackPercent = 0f;

    [Header("Combat")]
    public float ContactDamage = 10f;            // 碰到玩家造成的傷害（CSV: MonsterData.ContactDamage）
    public float DamageReductionPercent = 0f;    // 受擊減傷 %（掛勾；目前 CSV 預設 0，之後接減傷/抗性）

    void Start()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();
        
        if (_brain == null)
        {
            _currentHealth = MaxHealth;
            _sensor = gameObject.AddComponent<MonsterSensor>();
            _actuator = gameObject.AddComponent<MonsterActuator>();
            _brain = new ChaseBrain();
            AutoAdjustCollider();
        }

        _hitReaction = gameObject.AddComponent<HitReactionHandler>();
        _hitReaction.Configure(_spriteRenderer, _rb,
            InvincibleTimeMs, KnockbackThreshold, KnockbackPercent);

        // 接觸傷害：碰到玩家就扣血（幾何重疊判定，見 EnemyContactDamage）。ContactDamage 由 Initialize 從 CSV 設定，
        // 手動放置的怪用預設值。Initialize 在 Start 之前由 MonsterSpawner 呼叫，故此時值已就緒。
        var contact = gameObject.AddComponent<EnemyContactDamage>();
        contact.Configure(ContactDamage);
    }

    public void Initialize(MonsterData data)
    {
        MonsterName = data.Name;
        MaxHealth = data.HP;
        _currentHealth = MaxHealth;

        InvincibleTimeMs = data.InvincibleTimeMs;
        KnockbackThreshold = data.KnockbackThreshold;
        KnockbackPercent = data.KnockbackPercent;

        ContactDamage = data.ContactDamage;
        DamageReductionPercent = data.DamageReduction;

        _sensor = gameObject.GetComponent<MonsterSensor>();
        if (_sensor == null) _sensor = gameObject.AddComponent<MonsterSensor>();
        
        _actuator = gameObject.GetComponent<MonsterActuator>();
        if (_actuator == null) _actuator = gameObject.AddComponent<MonsterActuator>();

        switch (data.BrainType)
        {
            case "Chase":
                _brain = new ChaseBrain();
                break;
            default:
                _brain = new ChaseBrain();
                break;
        }

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

        if (_hitReaction == null || !_hitReaction.IsKnockedBack)
        {
            _brain.Think(_actuator, player);
        }

        HandleVisuals(player);
    }

    private void HandleVisuals(Transform player)
    {
        if (_animator == null || _spriteRenderer == null) return;

        float currentSpeed = (_rb != null) ? _rb.velocity.magnitude : 0f;
        _animator.SetBool("isMoving", currentSpeed > 0.1f);

        // 2. 左右翻轉 (Flip)：根據玩家位置與圖片原始朝向決定
        if (player != null)
        {
            bool playerIsOnRight = player.position.x > transform.position.x;
            // IsFacingRightByDefault = true 代表圖片原始朝左，需要 flipX 才能朝右
            _spriteRenderer.flipX = IsFacingRightByDefault ? playerIsOnRight : !playerIsOnRight;
        }
    }

    public void TakeDamage(float amount, Vector2 hitDirection)
    {
        if (_isDead) return;

        if (_hitReaction != null && !_hitReaction.TryHitReaction(amount, hitDirection))
            return;

        _currentHealth -= amount;
        DamageNumberManager.Show(gameObject, amount);   // 頭上跳傷害數字（已過無敵判定 = 確實吃到傷害）
        Debug.Log($"{MonsterName} took {amount} damage. HP: {_currentHealth}/{MaxHealth}");

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, Vector2.zero);
    }

    // ── ICombatModifiers：怪物作為攻擊方無加成（1）；作為受擊方套用減傷掛勾（目前 CSV 預設 0 = 不減傷）──
    public float OutgoingDamageMultiplier(in DamageInfo info) => 1f;

    public float IncomingDamageMultiplier(in DamageInfo info)
    {
        float r = Mathf.Clamp(DamageReductionPercent, 0f, 90f);
        return 1f - r / 100f;
    }

    void Die()
    {
        _isDead = true;
        // 簡單處理：直接銷毀物件
        Destroy(gameObject);
    }
}