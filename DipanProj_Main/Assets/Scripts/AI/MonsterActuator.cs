using UnityEngine;

// 確保這行寫在 class 定義的正上方，且沒有拼字錯誤
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class MonsterActuator : MonoBehaviour
{
    public float MoveSpeed = 3f;
    public bool IsMoving { get; private set; }

    private Rigidbody2D _rb;
    private Vector2 _moveDirection = Vector2.zero;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0;
        _rb.freezeRotation = true;
    }

    // Brain 在 Update 裡呼叫此方法，只記錄方向，不直接移動
    public void MoveTowards(Vector2 targetPos)
    {
        _moveDirection = (targetPos - (Vector2)transform.position).normalized;
        IsMoving = true;
    }

    public void Stop()
    {
        _moveDirection = Vector2.zero;
        IsMoving = false;
    }

    // 實際移動在 FixedUpdate 執行，確保與物理引擎同步
    void FixedUpdate()
    {
        if (IsMoving)
        {
            _rb.MovePosition(_rb.position + _moveDirection * MoveSpeed * Time.fixedDeltaTime);
        }
    }
}