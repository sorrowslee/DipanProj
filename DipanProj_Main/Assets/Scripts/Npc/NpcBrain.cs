using UnityEngine;

/// <summary>
/// NPC 的決策機（IMonsterBrain）：**原地不動** 或 **沿路徑點乒乓來回走**。
/// 移動走 <see cref="MonsterActuator.MoveTowards"/> → A* 導航免費附贈（路徑點之間有家具也會繞）。
/// 對話中（<see cref="Talking"/>）停在原地；面向由 MonsterController 處理
/// （FaceMovement：走路面向移動方向、停下時玩家在感測範圍內就面向玩家）。
/// 未來擴充：護送（escort）＝加一個 Mode，「玩家在半徑內才沿路徑前進」——資料與地基都在，不必動別處。
/// </summary>
public class NpcBrain : IMonsterBrain
{
    public enum Mode { Idle, Patrol }

    /// <summary>對話中＝true：停在原地（由 NpcAgent 於對話開/關時設定）。</summary>
    public bool Talking;

    readonly Mode _mode;
    readonly Vector2[] _points;   // 巡邏路徑：[站位, 路徑點1, 路徑點2, …]，乒乓來回（走到尾再倒著走回站位）
    readonly float _dwell;        // 抵達每個路徑點後停留秒數（0＝不停留）

    int _idx;                     // 目前的目標點
    int _dir = 1;                 // 乒乓方向
    float _waitUntil = -1f;       // >0＝抵達後停留中，到時間才選下一點
    const float ReachDist = 0.18f;

    public NpcBrain(Mode mode, Vector2[] patrolPoints, float dwellSeconds)
    {
        _mode = mode;
        _points = patrolPoints;
        _dwell = Mathf.Max(0f, dwellSeconds);
        // 一開始站在 points[0]（站位），先朝第 1 個路徑點走
        _idx = (_points != null && _points.Length > 1) ? 1 : 0;
    }

    public void Think(in MonsterContext ctx)
    {
        var act = ctx.Actuator;
        if (act == null) return;

        if (Talking || _mode == Mode.Idle || _points == null || _points.Length < 2)
        {
            act.Stop();
            return;
        }

        Vector2 pos = act.transform.position;
        Vector2 target = _points[_idx];

        // 抵達目標點：先停留（若有設定），再乒乓選下一點。
        if ((target - pos).sqrMagnitude <= ReachDist * ReachDist)
        {
            if (_waitUntil < 0f) _waitUntil = Time.time + _dwell;
            if (Time.time < _waitUntil) { act.Stop(); return; }
            _waitUntil = -1f;

            int next = _idx + _dir;
            if (next >= _points.Length) { _dir = -1; next = _points.Length - 2; }
            else if (next < 0) { _dir = 1; next = _points.Length > 1 ? 1 : 0; }
            _idx = Mathf.Clamp(next, 0, _points.Length - 1);
            target = _points[_idx];
        }

        act.MoveTowards(target);
    }
}
