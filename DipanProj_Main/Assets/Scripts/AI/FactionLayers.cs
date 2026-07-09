using UnityEngine;

/// <summary>
/// 陣營用的 Layer 解析與碰撞設定（集中一處）。Ally 層(8)用來放玩家召喚的協戰怪，讓：
///  ‧ 玩家子彈(打 Enemy 層)天生打不到自己的召喚物；
///  ‧ 召喚物不會用物理去推玩家/敵怪（比照 Enemy 相處方式：只被 Environment 擋）。
///
/// 碰撞用 <see cref="Physics2D.IgnoreLayerCollision"/> 在進場前設定，**不必手改 DynamicsManager 的碰撞矩陣**。
/// 每次進 Play（含關 Domain Reload）都會由 RuntimeInitialize 重跑一次，idempotent；build 同樣正確。
/// 前置：Project Settings > Tags and Layers 的 user layer 8 = "Ally"（已加）。
/// </summary>
public static class FactionLayers
{
    public static int PlayerLayer { get; private set; } = -1;
    public static int EnemyLayer { get; private set; } = -1;
    public static int AllyLayer { get; private set; } = -1;
    public static int PlayerMask { get; private set; }
    public static int EnemyMask { get; private set; }
    public static int AllyMask { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        PlayerLayer = LayerMask.NameToLayer("Player");
        EnemyLayer = LayerMask.NameToLayer("Enemy");
        AllyLayer = LayerMask.NameToLayer("Ally");
        PlayerMask = PlayerLayer >= 0 ? (1 << PlayerLayer) : 0;
        EnemyMask = EnemyLayer >= 0 ? (1 << EnemyLayer) : 0;
        AllyMask = AllyLayer >= 0 ? (1 << AllyLayer) : 0;

        if (AllyLayer < 0)
        {
            Debug.LogWarning("[FactionLayers] 找不到 'Ally' 層。請在 Project Settings > Tags and Layers 把 user layer 8 設成 Ally，否則玩家召喚物會退回 Enemy 層（會被自己的子彈打到）。");
            return;
        }
        // Ally 穿過 Player/Enemy/Ally（比照 Enemy 的相處：互不推擠、傷害走幾何重疊），只被 Environment 擋。
        if (PlayerLayer >= 0) Physics2D.IgnoreLayerCollision(AllyLayer, PlayerLayer, true);
        if (EnemyLayer >= 0) Physics2D.IgnoreLayerCollision(AllyLayer, EnemyLayer, true);
        Physics2D.IgnoreLayerCollision(AllyLayer, AllyLayer, true);
    }
}
