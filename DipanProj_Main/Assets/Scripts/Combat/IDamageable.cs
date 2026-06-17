using UnityEngine;

/// <summary>
/// 任何「可以被武器傷害」的東西的共用介面。怪物(MonsterController)與可破壞地上物
/// (DestructibleObject)都實作它,讓所有傷害來源(子彈、雷射、地面特效、拋物線爆炸…)
/// 用同一條路徑造成傷害,不必各自針對具體型別。
/// </summary>
public interface IDamageable
{
    /// <summary>受到傷害。hitDirection 用於擊退方向(地上物可忽略)。</summary>
    void TakeDamage(float amount, Vector2 hitDirection);
}
