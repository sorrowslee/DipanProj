using UnityEngine;

// 確保這行寫在 class 定義的正上方，且沒有拼字錯誤
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
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