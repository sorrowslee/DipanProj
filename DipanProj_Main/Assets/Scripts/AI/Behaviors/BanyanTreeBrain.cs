using UnityEngine;

/// <summary>
/// 榕樹妖 Boss 戰鬥模組（第二個 boss 級 Brain）。本體「不可直接打」——樹是背景、臉是地上物——
/// 玩家要打牠的攻擊物「地刺」(<see cref="BossSpike"/>) 來反傷本體。
/// 本體＝**無圖隱形的 MonsterController**（怪物出生點放，沒有圖 → 自動沒碰撞框、打不到、也不會亂動），只管血量＋編排攻擊。
///
/// 行為（第一階段）：不動；玩家在場、冷卻好就在**隨機可走點**生一波地刺（每根 BossSpike 自己跑預警→冒出→收回）。
/// 血量階段（&lt;50% 加量加速、&lt;20% 兩招大絕）與出招換臉之後再接。手感常數在下方，要調就改這裡。
/// </summary>
public class BanyanTreeBrain : IMonsterBrain
{
    // ── 手感常數 ──
    const float DetectionRange   = 40f;  // 整個場地都算得到玩家（本體不動，要能一直施壓）
    const float VolleyInterval   = 3.0f; // 每波地刺間隔（秒）
    const int   SpikesPerVolley  = 3;    // 每波幾根
    const float SpikeDamage      = 10f;  // 地刺碰玩家的傷害
    const float SpikeScale       = 1f;   // 一般地刺大小
    const float FirstVolleyDelay = 1.5f; // 進場後第一波的緩衝

    bool _inited;
    MonsterController _boss;
    float _nextVolley;

    void EnsureInit(in MonsterContext ctx)
    {
        if (_inited) return;
        _inited = true;
        if (ctx.Sensor != null) ctx.Sensor.DetectionRange = DetectionRange;
        _boss = ctx.Self;
        _nextVolley = Time.time + FirstVolleyDelay;
    }

    public void Think(in MonsterContext ctx)
    {
        EnsureInit(ctx);
        if (ctx.Actuator != null) ctx.Actuator.Stop();   // 榕樹妖不移動

        if (ctx.Player == null) return;                  // 沒偵測到玩家 → 待機
        if (Time.time < _nextVolley) return;
        _nextVolley = Time.time + VolleyInterval;

        FireVolley();
    }

    // 在隨機可走點生一波地刺（BossSpike 自己跑完整的預警→冒出→收回）。
    void FireVolley()
    {
        var nav = MapNavGrid.Instance;
        if (nav == null || _boss == null) return;
        for (int i = 0; i < SpikesPerVolley; i++)
            if (nav.TryGetRandomWalkable(out Vector2 p))
                BossSpike.Fire(p, SpikeScale, _boss, SpikeDamage);
    }
}
