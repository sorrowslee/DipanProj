using UnityEngine;

// 只強制 Rigidbody2D；碰撞框由 MonsterController 依「圖的不透明像素」建一個貼合的 BoxCollider2D
// （見 MonsterController.AutoAdjustCollider / FitVisibleBoxCollider）。不再強制 CircleCollider2D，
// 否則瘦長的怪會被一個「以最大邊為半徑」的大圓撐爆碰撞範圍。
[RequireComponent(typeof(Rigidbody2D))]
public class MonsterActuator : MonoBehaviour
{
    public float MoveSpeed = 3f;
    public bool IsMoving { get; private set; }

    private Rigidbody2D _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0;
        _rb.freezeRotation = true;
    }

    public void MoveTowards(Vector2 targetPos)
    {
        Vector2 dir = (targetPos - (Vector2)transform.position).normalized;
        _rb.velocity = dir * MoveSpeed;
        IsMoving = true;
    }

    public void Stop()
    {
        _rb.velocity = Vector2.zero;
        IsMoving = false;
    }
}