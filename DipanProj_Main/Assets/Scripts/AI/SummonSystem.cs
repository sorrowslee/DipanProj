using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 召喚的共用核心（擁有者無關）：玩家與怪物(boss)共用同一套「表驅動生怪」邏輯。
/// 從配方的 ID 池隨機抽 <c>SummonCount</c> 隻，在 <paramref name="originPos"/> 周圍 <c>SummonRadius</c> 環上生成；
/// 受 <c>SummonMaxAlive</c> 同時上限限制（呼叫端各自持一份 <paramref name="aliveTracker"/>，死掉的分身會被 Unity 判成 null 而清掉）。
///
/// 冷卻不在這裡管（呼叫端各自用 FireInterval 節流：玩家用 _fireTimer、怪物用 MonsterWeaponUser 的冷卻）。
/// 回傳「這次是否真的生了怪」。
///
/// 注意：召喚出的怪目前一律走**敵人** AI（MonsterSpawner 生的是 Enemy 層、ChaseBrain 追玩家）。
/// 玩家召喚的「友軍 faction」（改追敵人、不傷玩家）尚未實作，見 readme/BOSS_MODULE.md §3。
/// </summary>
public static class SummonSystem
{
    public static bool Cast(GameObject owner, Vector3 originPos, RecipeEntry recipe, List<GameObject> aliveTracker, MonsterFaction faction)
    {
        if (recipe == null || !recipe.IsSummon || aliveTracker == null) return false;

        aliveTracker.RemoveAll(go => go == null);                       // 清掉已死分身
        if (recipe.SummonIds == null || recipe.SummonIds.Length == 0) return false;
        if (aliveTracker.Count >= recipe.SummonMaxAlive) return false;  // 已達同時上限

        // 不 static 快取 spawner：Domain Reload 關閉下 static 會殘留成已銷毀參考；召喚不頻繁，每次找一次即可。
        var spawner = Object.FindObjectOfType<MonsterSpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("[SummonSystem] 場景找不到 MonsterSpawner，召喚略過。");
            return false;
        }

        int want = Mathf.Max(1, recipe.SummonCount);
        int room = recipe.SummonMaxAlive - aliveTracker.Count;
        int n = Mathf.Min(want, room);

        int spawned = 0;
        for (int k = 0; k < n; k++)
        {
            int id = recipe.SummonIds[Random.Range(0, recipe.SummonIds.Length)];
            Vector2 ring = Random.insideUnitCircle.normalized;
            if (ring.sqrMagnitude < 0.0001f) ring = Vector2.right;
            Vector2 pos = (Vector2)originPos + ring * recipe.SummonRadius + Random.insideUnitCircle * 0.3f;

            // 分身不帶 deathFlag：殺家人旗標綁編輯器擺放的家人怪，召喚分身不算（見 readme/TRIGGER_CHAIN.md §7）。
            GameObject go = spawner.SpawnMonster(id, pos, null, faction);   // 玩家召喚=PlayerAlly、怪物召喚=Enemy
            if (go != null) { aliveTracker.Add(go); spawned++; }
        }
        return spawned > 0;
    }
}
