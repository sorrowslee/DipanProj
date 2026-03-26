using UnityEngine;
using Sorrows.Ballistics;

public class PlayerController : MonoBehaviour
{
    public float MoveSpeed = 5f;
    public GameObject BulletPrefab; 
    public LayerMask EnvLayer; 
    // 新增：讓子彈知道哪些是敵人
    public LayerMask EnemyLayer; 
    public ProjectileDefinition MyProjectileData; 

    private Animator _animator;
    private Rigidbody2D _rb;
    private Vector2 _moveInput;
    private SpriteRenderer _spriteRenderer;

    public bool isFacingRightByDefault = true;

    void Start()
    {
        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // 初始化面向 (原始圖片朝左，若預設右則 flipX = true)
        if (_spriteRenderer != null)
        {
            _spriteRenderer.flipX = isFacingRightByDefault;
        }
    }
    
    private float _fireTimer = 0f;

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        _moveInput = new Vector2(h, v).normalized;

        // 自動鏡像翻轉邏輯
        if (_spriteRenderer != null)
        {
            // 🟢 優先權 1：攻擊中，根據滑鼠位置翻轉
            bool isAttacking = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);
            if (isAttacking)
            {
                Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                float mouseDiffX = mousePos.x - transform.position.x;
                
                if (mouseDiffX < 0) _spriteRenderer.flipX = false;
                else if (mouseDiffX > 0) _spriteRenderer.flipX = true;
            }
            // 🟢 優先權 2：沒攻擊且有移動，根據移動方向翻轉
            else if (Mathf.Abs(h) > 0.01f)
            {
                if (h < 0) _spriteRenderer.flipX = false;
                else if (h > 0) _spriteRenderer.flipX = true;
            }
        }

        // 🟢 更新發射冷卻計時器
        if (_fireTimer > 0) _fireTimer -= Time.deltaTime;

        // 🟢 改為 GetKey 支援長按連發
        if ((Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0)) && _fireTimer <= 0)
        {
            Shoot();
            // 重置間隔時鐘
            if (MyProjectileData != null) _fireTimer = MyProjectileData.FireInterval;
        }

        HandleVisuals();
    }

    // 🟢 新增：視覺與動畫控制邏輯
    private void HandleVisuals()
    {
        if (_animator == null || _spriteRenderer == null) return;

        // Kinematic 模式下 velocity 永遠為 0，改用 moveInput 判斷是否在移動
        _animator.SetBool("isMoving", _moveInput.magnitude > 0.01f);
    }

    void FixedUpdate()
    {
        _rb.MovePosition(_rb.position + _moveInput * MoveSpeed * Time.fixedDeltaTime);
    }

    void Shoot()
    {
        if (BulletPrefab == null || MyProjectileData == null) return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        Vector2 fireDirection = (mousePos - transform.position).normalized;

        // 🟢 修復：子彈從角色稍微前方一點的地方產出，避免出生即自撞物理判定
        Vector2 spawnPos = (Vector2)transform.position + fireDirection * 0.5f;

        // 直接傳入 HandleBulletHit，確保第一幀就能收到通知
        // EnemyLayer 同時作為「可穿透層」與「不反彈層」，讓彈道系統不需要知道 Layer 編號
        BallisticsEngine.Spawn(MyProjectileData, BulletPrefab, spawnPos, fireDirection, EnvLayer | EnemyLayer, EnemyLayer, EnemyLayer, HandleBulletHit);
    }

    // 主遊戲的傷害處理器
    void HandleBulletHit(BulletInstance bullet, GameObject target, RaycastHit2D hit)
    {
        // 嘗試取得怪物組件
        MonsterController monster = target.GetComponent<MonsterController>();
        if (monster != null)
        {
            // 威力目前寫死，之後可以根據 MyProjectileData 的 ID 來查表
            float damage = 10f; 
            monster.TakeDamage(damage);
        }
    }
}