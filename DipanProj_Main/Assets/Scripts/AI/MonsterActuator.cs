using UnityEngine;

// 只強制 Rigidbody2D；碰撞框由 MonsterController 依「圖的不透明像素」建一個貼合的 BoxCollider2D
// （見 MonsterController.AutoAdjustCollider / FitVisibleBoxCollider）。
[RequireComponent(typeof(Rigidbody2D))]
public class MonsterActuator : MonoBehaviour
{
    public float MoveSpeed = 3f;
    public bool IsMoving { get; private set; }

    // ── 障礙迴避（繞牆/地上物、鑽窄縫；卡住會自動側滑脫困；**不會原地凍住**）──
    // 追擊/跟隨的怪預設會繞障礙；某些刻意「直線衝/直線逃」的行為（如紅嫁衣 boss 逃跑）可把它關掉。
    public bool AvoidObstacles = true;
    public float AvoidLookahead = 1.2f;    // 往前探多遠（世界單位）：越大越早轉
    public float AvoidProbeScale = 0.75f;  // 探測圓半徑 = 自身半身寬 × 此值（<1 才鑽得進略窄的縫）

    // 解卡：想動卻幾乎沒位移超過 StuckSeconds → 側滑 UnstickSeconds 脫離卡點
    const float StuckSeconds = 0.25f;
    const float UnstickSeconds = 0.4f;
    const float UnstickAngle = 75f;

    private Rigidbody2D _rb;
    private Collider2D _col;
    private int _obstacleMask = -1;
    private int _avoidSign = 1;             // 上次繞行/解卡的偏轉側
    private Vector2 _lastPos;
    private float _stuckTime;
    private float _unstickUntil = -1f;

    // 逐步加大的偏轉角（度）：先小角度貼著障礙滑，繞不過再加大
    private static readonly float[] AvoidAngles = { 18f, 34f, 52f, 72f, 96f, 124f, 155f };

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0;
        _rb.freezeRotation = true;
        _lastPos = transform.position;
    }

    /// <summary>朝目標移動；會自動繞牆/地上物、鑽窄縫，卡住時側滑脫困，不會原地凍住。</summary>
    public void MoveTowards(Vector2 targetPos)
    {
        Vector2 pos = transform.position;
        Vector2 desired = targetPos - pos;
        if (desired.sqrMagnitude < 1e-6f) { Stop(); _lastPos = pos; return; }
        desired.Normalize();

        Vector2 dir;
        if (AvoidObstacles)
        {
            UpdateStuck(pos);
            dir = (Time.time < _unstickUntil)
                ? Rotate(desired, UnstickAngle * _avoidSign)   // 解卡：往側邊滑脫離卡點
                : SteerAround(pos, desired);                    // 一般：繞障礙
        }
        else dir = desired;

        _lastPos = pos;
        _rb.velocity = dir * MoveSpeed;
        IsMoving = true;
    }

    public void Stop()
    {
        _rb.velocity = Vector2.zero;
        IsMoving = false;
        _stuckTime = 0f;
    }

    // 卡住偵測：上一幀想動、實際幾乎沒位移 → 累積；超過門檻進入側滑解卡一小段時間，換邊避免一直撞同側。
    private void UpdateStuck(Vector2 pos)
    {
        if (!IsMoving) { _stuckTime = 0f; return; }
        if (Time.time < _unstickUntil) return;             // 解卡中不重複判定
        float moved = (pos - _lastPos).magnitude;
        float expected = MoveSpeed * Time.deltaTime * 0.4f;
        if (moved < expected) _stuckTime += Time.deltaTime; else _stuckTime = 0f;
        if (_stuckTime > StuckSeconds)
        {
            _unstickUntil = Time.time + UnstickSeconds;
            _stuckTime = 0f;
            _avoidSign = -_avoidSign;
        }
    }

    // 局部避障：直線暢通就直走；被擋就往兩側逐步加大角度找暢通方向滑過去。
    // 一整圈都被擋時仍朝目標推（不原地凍住）——真正卡死交給上面的側滑解卡處理。
    private Vector2 SteerAround(Vector2 pos, Vector2 desired)
    {
        float radius = ProbeRadius();
        if (IsClear(pos, desired, radius)) return desired;

        foreach (float a in AvoidAngles)
        {
            Vector2 pref = Rotate(desired, a * _avoidSign);
            if (IsClear(pos, pref, radius)) return pref;
            Vector2 other = Rotate(desired, -a * _avoidSign);
            if (IsClear(pos, other, radius)) { _avoidSign = -_avoidSign; return other; }
        }
        return desired;   // 一整圈都被擋：仍朝目標推（靠物理＋側滑解卡脫困）
    }

    private bool IsClear(Vector2 pos, Vector2 dir, float radius)
    {
        int mask = ObstacleMask();
        if (mask == 0) return true;   // 沒有障礙層可查（保險）：當暢通、不干預
        RaycastHit2D hit = Physics2D.CircleCast(pos, radius, dir, AvoidLookahead, mask);
        return hit.collider == null;
    }

    private int ObstacleMask()
    {
        if (_obstacleMask == -1) _obstacleMask = LayerMask.GetMask("Environment", "Water");
        return _obstacleMask;
    }

    private float ProbeRadius()
    {
        if (_col == null) _col = GetComponent<Collider2D>();
        float r = 0.2f;
        if (_col != null) { Vector3 e = _col.bounds.extents; r = Mathf.Max(0.05f, Mathf.Min(e.x, e.y) * AvoidProbeScale); }
        return r;
    }

    private static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
