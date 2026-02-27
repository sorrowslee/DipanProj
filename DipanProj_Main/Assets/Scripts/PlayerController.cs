using UnityEngine;
using Sorrows.Ballistics;

public class PlayerController : MonoBehaviour
{
    public float MoveSpeed = 5f;
    public GameObject BulletPrefab; 
    public LayerMask EnvLayer; 
    public ProjectileDefinition MyProjectileData; 

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        transform.position += new Vector3(h, v, 0).normalized * MoveSpeed * Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            Shoot();
        }
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