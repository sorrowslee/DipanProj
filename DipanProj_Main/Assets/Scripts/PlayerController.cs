using UnityEngine;
using Sorrows.Ballistics;

public class PlayerController : MonoBehaviour
{
    public float MoveSpeed = 5f;
    public GameObject BulletPrefab; 
    public LayerMask EnvLayer; 
    public ProjectileDefinition MyProjectileData; 

    private Rigidbody2D _rb;
    private Vector2 _moveInput;

    void Start()
    {
        // 取得身上掛的 Rigidbody 2D
        _rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // 1. 取得輸入 (不要在這裡移動)
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        _moveInput = new Vector2(h, v).normalized;

        // 2. 發射邏輯
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            Shoot();
        }
    }

    void FixedUpdate()
    {
        // 3. 物理移動建議放在 FixedUpdate，這會讓碰撞最精準
        // 透過控制速度，Unity 會自動幫你處理牆壁碰撞
        _rb.velocity = _moveInput * MoveSpeed;
    }

    void Shoot()
    {
        if (BulletPrefab == null || MyProjectileData == null) return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        Vector2 fireDirection = (mousePos - transform.position).normalized;

        BallisticsEngine.Spawn(MyProjectileData, BulletPrefab, transform.position, fireDirection, EnvLayer);
    }
}