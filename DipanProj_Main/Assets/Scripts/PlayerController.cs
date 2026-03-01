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

    private Rigidbody2D _rb;
    private Vector2 _moveInput;

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        _moveInput = new Vector2(h, v).normalized;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            Shoot();
        }
    }

    void FixedUpdate()
    {
        _rb.velocity = _moveInput * MoveSpeed;
    }

    void Shoot()
    {
        if (BulletPrefab == null || MyProjectileData == null) return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        Vector2 fireDirection = (mousePos - transform.position).normalized;

        // 🟢 直接傳入 HandleBulletHit，確保第一幀就能收到通知
        BallisticsEngine.Spawn(MyProjectileData, BulletPrefab, transform.position, fireDirection, EnvLayer | EnemyLayer, HandleBulletHit);
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