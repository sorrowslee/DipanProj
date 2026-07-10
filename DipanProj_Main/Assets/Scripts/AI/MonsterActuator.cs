using System.Collections.Generic;
using UnityEngine;

// 只強制 Rigidbody2D；碰撞框由 MonsterController 依「圖的不透明像素」建一個貼合的 BoxCollider2D。
[RequireComponent(typeof(Rigidbody2D))]
public class MonsterActuator : MonoBehaviour
{
    public float MoveSpeed = 3f;
    public bool IsMoving { get; private set; }


    // ── 尋徑：優先用全域 A*（MapNavGrid）繞過牆/地上物；沒 nav 時退回局部避障；卡住自動側滑；不會原地凍住 ──
    // 某些刻意「直線衝/直線逃」的行為（如紅嫁衣 boss 逃跑）可把 AvoidObstacles 關掉走直線。
    public bool AvoidObstacles = true;
    public float AvoidLookahead = 1.2f;    // 局部避障往前探多遠（沒 nav 時用）
    public float AvoidProbeScale = 0.75f;  // 探測圓半徑 = 自身半身寬 × 此值

    // A* 路徑跟隨
    const float RepathInterval = 0.35f;    // 最短重算間隔（秒）
    const float RepathMoveSq = 0.6f * 0.6f;// 目標移動超過這距離就重算
    const float WaypointReachSq = 0.35f * 0.35f;
    // 解卡：想動卻幾乎沒位移超過 StuckSeconds → 側滑 UnstickSeconds
    const float StuckSeconds = 0.3f;
    const float UnstickSeconds = 0.4f;
    const float UnstickAngle = 75f;

    private Rigidbody2D _rb;
    private Collider2D _col;
    private int _obstacleMask = -1;
    private int _avoidSign = 1;
    private Vector2 _lastPos;
    private float _stuckTime;
    private float _unstickUntil = -1f;

    private readonly List<Vector2> _path = new List<Vector2>();
    private int _pathIdx;
    private Vector2 _pathTarget;
    private float _repathAt;

    private static readonly float[] AvoidAngles = { 18f, 34f, 52f, 72f, 96f, 124f, 155f };

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0;
        _rb.freezeRotation = true;
        _lastPos = transform.position;
    }

    /// <summary>朝目標移動：能直線走就直走；被牆/地上物擋住就走 A* 路徑；卡住自動側滑脫困，不會原地凍住。</summary>
    public void MoveTowards(Vector2 targetPos)
    {
        Vector2 pos = transform.position;
        Vector2 toTarget = targetPos - pos;
        if (toTarget.sqrMagnitude < 1e-6f) { Stop(); _lastPos = pos; return; }

        if (!AvoidObstacles)
        {
            _rb.velocity = toTarget.normalized * MoveSpeed; IsMoving = true; _lastPos = pos; return;
        }

        UpdateStuck(pos);

        Vector2 dir;
        if (Time.time < _unstickUntil)
        {
            dir = Rotate(toTarget.normalized, UnstickAngle * _avoidSign);   // 解卡：側滑脫離卡點
        }
        else if (DirectClear(pos, targetPos))
        {
            _path.Clear();                                                   // 直線可達 → 直走（含最後貼近玩家）
            dir = toTarget.normalized;
        }
        else
        {
            dir = FollowPath(pos, targetPos);                               // 需要繞 → A* 路徑
        }

        _lastPos = pos;
        _rb.velocity = dir * MoveSpeed;
        IsMoving = true;
    }

    public void Stop()
    {
        _rb.velocity = Vector2.zero;
        IsMoving = false;
        _stuckTime = 0f;
        _path.Clear();
    }

    // ── A* 路徑跟隨（需要重算就重算、跟到下一個航點；沒 nav / 找不到路 → 退回局部避障）──
    private Vector2 FollowPath(Vector2 pos, Vector2 target)
    {
        var nav = MapNavGrid.Instance;
        if (nav != null && nav.Ready)
        {
            bool needRepath = _path.Count == 0
                || (target - _pathTarget).sqrMagnitude > RepathMoveSq
                || Time.time >= _repathAt;
            if (needRepath)
            {
                if (nav.TryFindPath(pos, target, _path)) { _pathIdx = 0; _pathTarget = target; _repathAt = Time.time + RepathInterval; }
                else _path.Clear();
            }
        }

        if (_path.Count == 0)
            return SteerAround(pos, (target - pos).normalized);            // 沒 nav / 找不到路：局部避障

        while (_pathIdx < _path.Count - 1 && (_path[_pathIdx] - pos).sqrMagnitude < WaypointReachSq) _pathIdx++;
        Vector2 d = _path[_pathIdx] - pos;
        return d.sqrMagnitude < 1e-6f ? (target - pos).normalized : d.normalized;
    }

    // 卡住偵測：上一幀想動卻幾乎沒位移 → 累積；超過門檻進入側滑解卡、換邊。
    private void UpdateStuck(Vector2 pos)
    {
        if (!IsMoving) { _stuckTime = 0f; return; }
        if (Time.time < _unstickUntil) return;
        float moved = (pos - _lastPos).magnitude;
        float expected = MoveSpeed * Time.deltaTime * 0.4f;
        if (moved < expected) _stuckTime += Time.deltaTime; else _stuckTime = 0f;
        if (_stuckTime > StuckSeconds) { _unstickUntil = Time.time + UnstickSeconds; _stuckTime = 0f; _avoidSign = -_avoidSign; _path.Clear(); }
    }

    // 目標是否「直線可達」（中心到目標的直線沒撞牆）→ 是就不必尋徑，直接走。
    // 用細射線（非圓）：牆是單一 CompositeCollider2D，圓一碰到牆就會因 queriesStartInColliders=false 整片被忽略而誤判暢通；
    // 從中心射細線不會（怪中心幾乎不會在牆內）。偶爾細線穿過身體塞不下的窄縫＝改走直線後由解卡側滑處理，罕見。
    private bool DirectClear(Vector2 pos, Vector2 target)
    {
        // 有 nav 格：用「格視線」判定（和 A* 同一份障礙圖，含家具膨脹）——這樣「直線可達」與 A* 一致，
        // 不會發生「細射線穿過家具的淨空而誤判可直走、結果整個身體撞上家具卡住」的鬼打牆。
        var nav = MapNavGrid.Instance;
        if (nav != null && nav.Ready)
            return nav.HasLineOfSight(pos, target);

        // 沒有 nav（單場景測試）→ 退回物理細射線。
        int mask = ObstacleMask();
        if (mask == 0) return true;
        Vector2 d = target - pos; float dist = d.magnitude;
        if (dist < 0.01f) return true;
        return Physics2D.Raycast(pos, d / dist, dist, mask).collider == null;
    }

    // 局部避障（fallback）：直線被擋就往兩側逐步加大角度找暢通方向；一整圈都被擋仍朝目標推（不凍住）。
    private Vector2 SteerAround(Vector2 pos, Vector2 desired)
    {
        float radius = ProbeRadius();
        if (IsClear(pos, desired, radius, AvoidLookahead)) return desired;
        foreach (float a in AvoidAngles)
        {
            Vector2 pref = Rotate(desired, a * _avoidSign);
            if (IsClear(pos, pref, radius, AvoidLookahead)) return pref;
            Vector2 other = Rotate(desired, -a * _avoidSign);
            if (IsClear(pos, other, radius, AvoidLookahead)) { _avoidSign = -_avoidSign; return other; }
        }
        return desired;
    }

    private bool IsClear(Vector2 pos, Vector2 dir, float radius, float dist)
    {
        int mask = ObstacleMask();
        if (mask == 0) return true;
        return Physics2D.CircleCast(pos, radius, dir, dist, mask).collider == null;
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
