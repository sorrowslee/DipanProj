using UnityEngine;

public class MonsterController : MonoBehaviour, IDamageable, ICombatModifiers
{
    private MonsterSensor _sensor;
    private MonsterActuator _actuator;
    private IMonsterBrain _brain;
    private HitReactionHandler _hitReaction;

    private Animator _animator;          // 舊路線後備：若怪用自帶 Unity Animator 的 prefab 才有
    private MonsterAnimator _monAnim;    // 路線 B：程式逐格動畫（量產怪用這個，零 prefab/Animator）
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rb;

    public string MonsterName;
    public float MaxHealth = 50f;
    public float HitboxPadding = 0.2f;
    public bool IsFacingRightByDefault = true;
    public float AnimFPS = 8f;           // 程式動畫播放幀率（CSV: AnimFPS，留空 = 8）
    public float AttackRange = 1.3f;     // 進入此距離且有 attack 圖 → 播攻擊動畫（略大於 ChaseBrain.StopDistance）
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

        // 腳下影子（見 readme/SHADOW.md）
        if (GetComponent<BlobShadow>() == null) gameObject.AddComponent<BlobShadow>();

        // 走路動畫速度跟著實際移動速度（避免腳滑；見 readme/CHARACTER_SETUP.md）。
        // ReferenceSpeed 設成這隻怪的正常移動速度 → 正常追擊時 1×，被放慢時動畫自動變慢。
        if (GetComponent<AnimatorSpeedByVelocity>() == null)
        {
            var asv = gameObject.AddComponent<AnimatorSpeedByVelocity>();
            var act = GetComponent<MonsterActuator>();
            if (act != null) asv.ReferenceSpeed = act.MoveSpeed;
        }

        // 路線 B：程式逐格動畫——依怪名載 idle/walk/attack 並播放（見 MonsterAnimator / MonsterSpriteLibrary）。
        // 只在「沒有 Unity Animator ＋ 有怪名」時啟用，避免和舊 prefab 的 Animator 同時搶著換 sprite：
        //   ‧ 量產怪（程式建、無 Animator、Initialize 給了怪名）→ 走這條（route B）。
        //   ‧ 舊 prefab 怪（自帶 Animator）→ 交給 Animator（下方 HandleVisuals 的 isMoving 後備）。
        if (_animator == null && !string.IsNullOrEmpty(MonsterName))
        {
            _monAnim = GetComponent<MonsterAnimator>();
            if (_monAnim == null) _monAnim = gameObject.AddComponent<MonsterAnimator>();
            var actForFps = GetComponent<MonsterActuator>();
            float refSpeed = actForFps != null ? actForFps.MoveSpeed : 3f;
            _monAnim.Setup(MonsterName, AnimFPS, refSpeed);

            // 碰撞框依「圖的不透明像素」貼合（瘦長的鬼魂不會被透明邊撐大）；取不到再退回整張 sprite。
            Vector2 vSize, vOff;
            if (MonsterSpriteLibrary.Instance.TryGetVisibleBox(MonsterName, "idle", out vSize, out vOff)
                || MonsterSpriteLibrary.Instance.TryGetVisibleBox(MonsterName, "walk", out vSize, out vOff))
                FitVisibleBoxCollider(vSize, vOff);
            else
                AutoAdjustCollider();   // 後備：用整張 sprite bounds（Setup 已指上第 0 幀）
        }
    }

    /// <summary>
    /// 把碰撞框設成貼合「圖的不透明像素」的 BoxCollider2D（size/offset 為 scale 1 的世界單位，
    /// 會隨怪物 transform 的 Scale 一起縮放，與顯示的圖對齊）。整體大小用 MonsterData 的 Scale 調、
    /// 鬆緊用 HitboxPadding 調。
    /// </summary>
    private void FitVisibleBoxCollider(Vector2 visSize, Vector2 visOffset)
    {
        var col = GetComponent<Collider2D>();
        BoxCollider2D box = col as BoxCollider2D;
        if (box == null)
        {
            if (col != null) Destroy(col);   // 萬一有別型 collider（如舊圓）→ 換成貼合的 Box
            box = gameObject.AddComponent<BoxCollider2D>();
        }
        box.size = new Vector2(Mathf.Max(0.01f, visSize.x + HitboxPadding),
                               Mathf.Max(0.01f, visSize.y + HitboxPadding));
        box.offset = visOffset;
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
        AnimFPS = data.AnimFPS;

        _sensor = gameObject.GetComponent<MonsterSensor>();
        if (_sensor == null) _sensor = gameObject.AddComponent<MonsterSensor>();
        
        _actuator = gameObject.GetComponent<MonsterActuator>();
        if (_actuator == null) _actuator = gameObject.AddComponent<MonsterActuator>();
        _actuator.MoveSpeed = data.Speed;   // 移動速度資料化（CSV: Speed）；走路動畫的 ReferenceSpeed 之後在 Start 自動讀這個值

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
        if (_spriteRenderer == null) return;

        float currentSpeed = (_rb != null) ? _rb.velocity.magnitude : 0f;
        bool moving = currentSpeed > 0.1f;

        // 1. 狀態決策（路線 B）：在攻擊範圍內且有 attack 圖 → 攻擊；否則 移動→走路 / 靜止→發呆。
        //    沒有 attack 圖的怪不會被選到 Attack（Has 防呆），自然只演走路/發呆。
        if (_monAnim != null)
        {
            MonsterAnimator.State st;
            if (player != null
                && _monAnim.Has(MonsterAnimator.State.Attack)
                && Vector2.Distance(transform.position, player.position) <= AttackRange)
                st = MonsterAnimator.State.Attack;
            else if (moving) st = MonsterAnimator.State.Walk;
            else st = MonsterAnimator.State.Idle;

            _monAnim.SetState(st, currentSpeed);
        }

        // 舊路線後備：若這隻怪用的是自帶 Unity Animator 的 prefab，沿用 isMoving 驅動。
        if (_animator != null) _animator.SetBool("isMoving", moving);

        // 2. 左右翻轉 (Flip)：根據玩家位置與圖片原始朝向決定（與動畫系統無關）
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