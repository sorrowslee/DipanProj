using System;
using UnityEngine;
using Sorrows.Ballistics;

/// <summary>
/// **不綁擁有者的武器發射服務**（Phase 2 第一階段：Normal 直飛彈）。
///
/// 在這之前，「發射一發子彈」這件事只存在於 <c>PlayerController</c> 裡，整段綁死玩家：
/// 瞄準方向來自滑鼠、出手點來自血統體型、命中層寫死 <c>EnemyLayer</c>、傷害來源寫死 <c>gameObject</c>。
/// 所以怪物想用同一把武器射同一種子彈時無路可走（<see cref="MonsterWeaponUser"/> 舊版只好吐
/// 「投射型武器供怪物使用為 Phase 2」的 warning）。
///
/// 這支把「發射」本身抽出來：**誰射的、從哪射、往哪射、會打到哪一層、命中要做什麼**全部變成參數
/// （<see cref="CastContext"/>），玩家與怪物共用同一份彈道生成程式。
/// 於是「子彈怎麼飛」（分裂／反彈／追蹤／平行／穿透／軌跡）只有一份實作，改一次兩邊同時生效。
///
/// ⚠ **刻意留在呼叫端、沒有一起抽進來的**（它們本來就屬於「誰在射」而不是「怎麼射」）：
///   ‧ 瞄準方向與出手點——玩家是滑鼠＋`MuzzleWorldPos`，怪物是「我面向目標」；
///   ‧ 耗魔、集氣、連擊、能力珠覆寫——這些是玩家的資源系統，怪物沒有；
///   ‧ **命中之後要做什麼**——玩家要結算地面特效／子武器迸發／擊中特效（`HandleBulletHit`），
///     怪物只要對玩家結算傷害。所以 `OnHit` 是 callback 而不是寫死在這裡。
///     這是刻意的 seam：硬把玩家的命中鏈搬進來，會把 `TryTriggerSubWeapon`／`TryTriggerGroundEffect`
///     整串玩家專屬狀態一起拖過來，反而讓這支服務變成第二個 PlayerController。
///
/// **目前只支援 <see cref="WeaponMode.Normal"/>**（直飛彈，含分裂／反彈／追蹤／平行／穿透／軌跡）。
/// Laser／Parabolic／SkyStrike／Chain／Orbital／Melee／Dash／GroundCast 仍住在 `PlayerController`，
/// 之後一種一種搬進來；搬的時候照同一個原則：幾何與彈道進來，資源與命中鏈留在呼叫端。
/// </summary>
public static class WeaponCastService
{
    /// <summary>一次扣扳機最多生幾顆子彈（平行 × 分裂）；超過就砍平行道數，遊戲不能因為珠子疊太多而卡死。</summary>
    public const int MaxBulletsPerTrigger = 128;

    /// <summary>平行彈從出生點散開到各自車道要幾秒（<c>LaneBehavior</c> 的側向速度衰減時間）。</summary>
    public const float ParallelFanOutSeconds = 0.15f;

    // 平行道數被砍的 warning 只印一次（原本是 PlayerController 的實例欄位；服務是靜態的，所以改成靜態旗標）。
    private static bool _parallelCapWarned;

    /// <summary>
    /// 一次發射所需的「誰、從哪、往哪、打得到誰」。刻意做成 struct：每發射一次填一份，不留狀態。
    /// </summary>
    public struct CastContext
    {
        /// <summary>誰射的。當成傷害來源（<c>CombatSystem.Apply</c> 的 source）與「不要打到自己」的判斷依據。</summary>
        public GameObject Owner;

        /// <summary>出手點（世界座標）。玩家＝`MuzzleWorldPos`，怪物＝自己的身體中心偏目標方向。</summary>
        public Vector2 Origin;

        /// <summary>射向（不需先正規化）。</summary>
        public Vector2 Direction;

        /// <summary>擁有者的體型倍率，乘進子彈大小。玩家＝`PlayerScale`；怪物填 1 即可（怪物體型不該放大牠的箭）。</summary>
        public float OwnerScale;

        /// <summary>
        /// 這發子彈「會打到」的目標層。玩家射＝怪物層；怪物射＝玩家層。
        /// ⚠ 這一欄就是「怪物的箭不會射到自己人、玩家的劍不會打到自己」的全部秘密——
        /// 不要在命中 callback 裡補判斷，讓 layer 先擋掉才不會每發子彈都做無謂的計算。
        /// </summary>
        public LayerMask TargetLayers;

        /// <summary>環境（牆／障礙物）層。子彈撞牆、以及 `BlockedByEnvironment` 的穿透判定都用它。</summary>
        public LayerMask EnvLayer;

        /// <summary>命中任何東西時的 callback（`BulletInstance`／命中物／`RaycastHit2D`）。</summary>
        public Action<BulletInstance, GameObject, RaycastHit2D> OnHit;

        /// <summary>沿飛行路徑每隔 `TrailStep` 距離觸發一次（地刺武器用；沒有就傳 null）。</summary>
        public Action<BulletInstance, Vector2> OnTrailPoint;
    }

    /// <summary>
    /// 發射一次 Normal 直飛彈（依配方可能是好幾道平行、每道再分裂成好幾顆）。
    /// 幾何與行為全部由 <paramref name="weapon"/>／<paramref name="recipe"/> 決定，與誰射的無關。
    /// </summary>
    /// <returns>
    /// **這次有沒有真的生出子彈**。呼叫端（尤其是怪物）要據此決定要不要進冷卻／播出手動畫——
    /// 回 false 卻照樣進冷卻、照樣播動畫，畫面上就是「動作演了、東西沒出去」的空砲
    /// （見 readme/PROBLEMS.md **F25**）。
    /// </returns>
    public static bool FireNormal(WeaponData weapon, ProjectileData recipe, in CastContext ctx)
    {
        if (weapon == null || recipe == null || weapon.BulletPrefab == null) return false;

        Vector2 dir = ctx.Direction.sqrMagnitude > 0.0001f ? ctx.Direction.normalized : Vector2.right;

        LayerMask collisionMask = ctx.EnvLayer | ctx.TargetLayers;
        LayerMask pierceableLayers = ResolvePierceableLayers(weapon.Recipe, ctx.TargetLayers, ctx.EnvLayer);
        LayerMask nonBounceLayers = ResolveNonBounceLayers(weapon.Recipe != null ? weapon.Recipe.BounceTarget : BounceTarget.None,
                                                          ctx.TargetLayers, ctx.EnvLayer);

        float ownerScale = ctx.OwnerScale > 0.0001f ? ctx.OwnerScale : 1f;
        Vector3 bulletScale = weapon.BulletPrefab.transform.localScale * ownerScale * weapon.BulletScale;

        // 平行彈：每一道都從同一個出生點出生（不會生在牆裡），掛 LaneBehavior 讓它飛出去
        // ParallelFanOutSeconds 秒內散開到自己的車道再拉直。
        // 工廠每顆子彈給一個新實例；OnSpawn 分裂出的子彈會由 BallisticsEngine 繼承同一個工廠（整排一起散開）。
        int perLane = (recipe.HasSplit && recipe.Timing == SplitTiming.OnSpawn) ? Mathf.Max(1, recipe.SplitCount) : 1;
        float[] lanes = ParallelOffsets(weapon.Recipe, perLane);
        Vector2 perp = new Vector2(-dir.y, dir.x);

        int spawned = 0;
        for (int i = 0; i < lanes.Length; i++)
        {
            Vector2 lateral = perp * lanes[i];
            Func<IBulletBehavior> lane = lanes[i] != 0f
                ? () => new LaneBehavior(lateral, ParallelFanOutSeconds)
                : (Func<IBulletBehavior>)null;

            BulletInstance b = BallisticsEngine.Spawn(recipe, weapon.BulletPrefab, ctx.Origin, dir,
                collisionMask, pierceableLayers, nonBounceLayers,
                ctx.OnHit,
                weapon.WeaponSprite, weapon.SpriteAngleOffset, bulletScale, weapon.WeaponSprites, weapon.AnimFPS,
                ctx.OnTrailPoint, lane);
            if (b != null) spawned++;
        }
        return spawned > 0;
    }

    /// <summary>
    /// 可穿透的層：目標層永遠可穿（穿透數由 `PierceCount` 決定），
    /// 配方沒勾 `BlockedByEnvironment` 時連牆也一起穿。
    /// </summary>
    public static LayerMask ResolvePierceableLayers(RecipeEntry recipe, LayerMask targetLayers, LayerMask envLayer)
    {
        LayerMask layers = targetLayers;
        if (recipe != null && !recipe.BlockedByEnvironment)
            layers |= envLayer;
        return layers;
    }

    /// <summary>反彈對象以外的層＝碰到就停（不反彈）。</summary>
    public static LayerMask ResolveNonBounceLayers(BounceTarget bounceTarget, LayerMask targetLayers, LayerMask envLayer)
    {
        return bounceTarget switch
        {
            BounceTarget.Environment => targetLayers,
            BounceTarget.Enemy => envLayer,
            _ => envLayer | targetLayers
        };
    }

    /// <summary>
    /// 每一道的側向偏移（世界單位、垂直於射向、置中對稱）。
    /// 總寬超過 `ParallelMaxWidth` 就壓縮間距；道數 × 每道顆數超過上限就砍道數。
    /// </summary>
    public static float[] ParallelOffsets(RecipeEntry r, int bulletsPerLane)
    {
        if (r == null) return new[] { 0f };

        int lanes = Mathf.Clamp(r.ParallelCount, 1, 16);
        int maxLanes = Mathf.Max(1, MaxBulletsPerTrigger / Mathf.Max(1, bulletsPerLane));
        if (lanes > maxLanes)
        {
            if (!_parallelCapWarned)
            {
                _parallelCapWarned = true;
                Debug.LogWarning($"[平行彈] {r.Name}：{lanes} 道 × 每道 {bulletsPerLane} 顆超過一次扣扳機上限 {MaxBulletsPerTrigger}，砍成 {maxLanes} 道。");
            }
            lanes = maxLanes;
        }
        if (lanes <= 1) return new[] { 0f };

        float spacing = r.ParallelSpacing;
        float width = spacing * (lanes - 1);
        if (width > r.ParallelMaxWidth) { width = r.ParallelMaxWidth; spacing = width / (lanes - 1); }

        var offsets = new float[lanes];
        for (int i = 0; i < lanes; i++) offsets[i] = -width * 0.5f + spacing * i;
        return offsets;
    }
}
