using UnityEngine;
using Sorrows.Ballistics; // 這裡成功引用，代表 Package 綁定成功！

public class PlayerController : MonoBehaviour
{
    public float MoveSpeed = 5f;
    public GameObject BulletPrefab; // 之後要把子彈預製物拖進來

    void Update()
    {
        // 1. 基礎移動邏輯 (WASD)
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 moveDir = new Vector3(h, v, 0).normalized;
        transform.position += moveDir * MoveSpeed * Time.deltaTime;

        // 2. 發射邏輯 (空白鍵)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Shoot();
        }
    }

    void Shoot()
    {
        if (BulletPrefab == null) return;

        // 生成子彈
        GameObject go = Instantiate(BulletPrefab, transform.position, Quaternion.identity);
        
        // 取得彈道組件並賦予初始速度 (往右飛)
        BaseBullet bullet = go.GetComponent<BaseBullet>();
        if (bullet != null)
        {
            bullet.Velocity = new Vector2(10f, 0f); 
        }
    }
}