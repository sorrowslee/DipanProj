using UnityEngine;

// 確保這行寫在 class 定義的正上方，且沒有拼字錯誤
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class MonsterActuator : MonoBehaviour
{
    public float MoveSpeed = 3f;
    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;

    public bool isFacingRightByDefault = true;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0;
        _rb.freezeRotation = true;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // 初始化面向
        if (_spriteRenderer != null)
        {
            _spriteRenderer.flipX = isFacingRightByDefault;
        }
    }

    void Update()
    {
        // 自動鏡像翻轉 (根據水平速度且原始圖片面朝左)
        if (_spriteRenderer != null)
        {
            if (_rb.velocity.x < -0.01f) _spriteRenderer.flipX = false;
            else if (_rb.velocity.x > 0.01f) _spriteRenderer.flipX = true;
        }
    }

    public void MoveTowards(Vector2 targetPos)
    {
        Vector2 dir = (targetPos - (Vector2)transform.position).normalized;
        _rb.velocity = dir * MoveSpeed;
    }

    public void Stop()
    {
        _rb.velocity = Vector2.zero;
    }
}