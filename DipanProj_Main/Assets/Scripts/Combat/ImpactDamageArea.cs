using UnityEngine;

/// <summary>
/// **一次性傷害圈**：在指定位置開一個極短命的圓形 trigger，碰到的敵對目標各扣一次血，然後自毀。
/// 給「一瞬間的打擊」用——跳躍踐踏的落地（<see cref="LeapSlamBrain"/>）、近戰揮武器的命中幀
/// （<see cref="MeleeChaseBrain"/>）都走這支。
///
/// <para><b>零新傷害程式</b>——傷害整個交給既有的 <see cref="EnemyContactDamage"/>：
/// 誰能傷誰查 <c>FactionRelations</c>（所以狼人砍到吸血鬼也會痛、砍到同族不會）、
/// 對玩家走玩家自己的無敵幀、傷害統一進中央 <c>CombatSystem</c>。
/// 這是榕樹妖地刺（<see cref="BossSpike"/>）用了很久的同一套做法。</para>
///
/// <para><b>刻意不放 Enemy 層</b>（BossSpike 放是因為地刺要能被玩家打壞）：
/// 這東西是一瞬間的傷害判定、不是實體，放進 Enemy 層會被玩家的子彈與搜敵邏輯當成目標，
/// 平白吃掉一發子彈、連鎖閃電還會往它跳。留在預設層即可——
/// <c>EnemyContactDamage</c> 走 <c>Physics2D.Distance</c> 直接算兩個 collider 的距離，本來就不看 layer。</para>
///
/// <para>⚠ **這個圈的半徑不等於實際殺傷範圍**：<c>EnemyContactDamage</c> 判的是**兩個碰撞框的邊緣距離**，
/// 所以實際打得到的距離 ＝ 本圈半徑 ＋ 目標碰撞框的半徑（玩家約 0.5）。訂數字時記得加上去。</para>
///
/// 見 readme/BOSS_MODULE.md §9.4（踐踏）與 §10.4（揮擊）。
/// </summary>
[DisallowMultipleComponent]
public class ImpactDamageArea : MonoBehaviour
{
    // 存活時間：只要夠長到讓 EnemyContactDamage 跑到一次 Update 就好。
    // 太長會變成「站在裡面持續被打」，太短（< 兩幀）在低幀率時可能一次都沒結算到。
    const float LifeSeconds = 0.12f;

    // 重擊間隔給一個遠大於存活時間的值 ⇒ 同一個目標在這一次打擊裡**只會被打一次**。
    const float HitInterval = 99f;

    /// <summary>在 <paramref name="pos"/> 開一次傷害判定。<paramref name="faction"/> 傳攻擊者的陣營。</summary>
    public static void Spawn(Vector2 pos, float radius, float damage, MonsterFaction faction)
    {
        if (radius <= 0.01f || damage <= 0f) return;

        var go = new GameObject("ImpactDamageArea");
        go.transform.position = pos;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;          // 不擋路、不推人
        col.radius = radius;

        var dmg = go.AddComponent<EnemyContactDamage>();
        dmg.Configure(damage, faction, HitInterval);

        Object.Destroy(go, LifeSeconds);
    }
}
