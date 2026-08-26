using UnityEngine;

/// <summary>
/// 紅嫁衣女殭屍 Boss 戰鬥模組（第一個 boss 級 Brain，示範「一隻強怪＝一個 Brain 模組」）。
///
/// 行為：
///  ‧ 躲玩家——玩家靠近就往「反方向」逃（走 A* 尋徑會自動繞牆／家具，和其他怪一樣不做硬碰撞）。
///    「讓玩家追得上」不靠被卡住，而是把她的 MonsterData.Speed 調慢即可。
///  ‧ 定時召喚家人幽靈當追兵——召喚是一把 WeaponTable 的「召喚武器」，冷卻/名單/數量/同時上限
///    全走配方（RecipeTable 的 Mode=Summon 那組欄位），由 <see cref="MonsterWeaponUser"/> 結算。
///    召喚不綁逃跑狀態、只看冷卻，確保 boss 持續施壓（她速度慢、多半在逃，若綁「安全才召」會幾乎不召）。
///
/// 未來每隻 boss：新增一個 XxxBrain + 在 MonsterController 的 BrainType switch 掛上、CSV 指定 BrainType 即可。
/// 手感全在下方常數，要調就改這裡。
/// </summary>
public class RedBridalGownBrain : IMonsterBrain
{
    // ── 手感常數（要調就改這裡）──
    const float FleeRange = 4.0f;      // 玩家進入此距離 → 開始逃
    const float SafeRange = 6.5f;      // 玩家拉開超過此距離 → 停下（做遲滯，> FleeRange 避免臨界抖動）
    const float DetectionRange = 30f;  // Boss 感測玩家範圍（比一般怪大，整個房間都算得到）
    const float AwayLookahead = 2f;    // 逃跑目標點 = 自身沿反方向外推這麼遠（純方向用，實速由 Actuator.MoveSpeed 決定）

    bool _inited;
    bool _fleeing;
    MonsterWeaponUser _weapon;

    void EnsureInit(in MonsterContext ctx)
    {
        if (_inited) return;
        _inited = true;
        if (ctx.Sensor != null) ctx.Sensor.DetectionRange = DetectionRange;
        _weapon = (ctx.Self != null) ? ctx.Self.WeaponUser : null;
    }

    public void Think(in MonsterContext ctx)
    {
        EnsureInit(ctx);

        MonsterActuator act = ctx.Actuator;
        Transform player = ctx.Player;

        // 召喚：玩家在場、冷卻好就召（不綁逃跑狀態）。冷卻/上限在 MonsterWeaponUser 內部結算。
        if (player != null && _weapon != null) _weapon.TryUse();

        if (player == null) { act.Stop(); return; }

        float dist = Vector2.Distance(act.transform.position, player.position);

        // 遲滯：開始逃後要拉開到 SafeRange 才停；還沒逃則要近到 FleeRange 才開始逃。
        if (_fleeing) { if (dist >= SafeRange) _fleeing = false; }
        else          { if (dist <= FleeRange) _fleeing = true; }

        if (_fleeing)
        {
            Vector2 away = (Vector2)act.transform.position - (Vector2)player.position;
            if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle.normalized;   // 重疊時隨機挑個方向
            Vector2 target = (Vector2)act.transform.position + away.normalized * AwayLookahead;
            act.MoveTowards(target);   // 往反方向逃（走 A* 會自動繞牆）；追得上與否靠 Speed 調慢
        }
        else
        {
            act.Stop();
        }
    }
}
