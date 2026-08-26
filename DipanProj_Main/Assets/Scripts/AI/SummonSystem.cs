using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 召喚的共用核心（擁有者無關）：玩家與怪物(boss)共用同一套「表驅動生怪」邏輯。
/// 從配方 ID 池隨機抽 <c>SummonCount</c> 隻，在 <paramref name="originPos"/> 周圍 <c>SummonRadius</c> 環上生成，
/// 但**生怪點會避開牆/水（不可走區）**，避免怪出生在牆裡走不出來（見 FindSpawnPos）。
/// 受 <c>SummonMaxAlive</c> 同時上限限制（呼叫端各持一份 <paramref name="aliveTracker"/>，死掉分身被 Unity 判 null 清掉）。
///
/// <para><b>召喚特效</b>：<paramref name="summonVfxId"/> &gt; 0 時，在每個生怪點播一次 VfxTable 特效並**同一幀生怪**
/// （邊播邊出現）；特效會依怪的可見高度縮放（見 VfxManager.SpawnSizedToHeight）。找不到 VfxManager 就只生怪。</para>
///
/// 冷卻由呼叫端各自管（玩家 _fireTimer、boss MonsterWeaponUser）。陣營：玩家=PlayerAlly、怪物/boss=Enemy。
/// </summary>
public static class SummonSystem
{
    /// <summary>目前是否還有空位可召喚（未達 SummonMaxAlive）。呼叫端可在扣魔/進冷卻前先問，避免扣了魔卻沒生怪。</summary>
    public static bool HasRoom(RecipeEntry recipe, List<GameObject> aliveTracker)
    {
        if (recipe == null || recipe.Mode != WeaponMode.Summon || aliveTracker == null) return false;
        if (recipe.SummonIds == null || recipe.SummonIds.Length == 0) return false;
        aliveTracker.RemoveAll(go => go == null);
        return aliveTracker.Count < recipe.SummonMaxAlive;
    }

    public static bool Cast(GameObject owner, Vector3 originPos, RecipeEntry recipe, List<GameObject> aliveTracker, MonsterFaction faction, int summonVfxId = 0)
    {
        if (recipe == null || recipe.Mode != WeaponMode.Summon || aliveTracker == null) return false;

        aliveTracker.RemoveAll(go => go == null);
        if (recipe.SummonIds == null || recipe.SummonIds.Length == 0) return false;
        if (aliveTracker.Count >= recipe.SummonMaxAlive) return false;

        var spawner = Object.FindObjectOfType<MonsterSpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("[SummonSystem] 場景找不到 MonsterSpawner，召喚略過。");
            return false;
        }

        VfxManager vfx = (summonVfxId > 0) ? Object.FindObjectOfType<VfxManager>() : null;

        int want = Mathf.Max(1, recipe.SummonCount);
        int room = recipe.SummonMaxAlive - aliveTracker.Count;
        int n = Mathf.Min(want, room);

        int spawned = 0;
        for (int k = 0; k < n; k++)
        {
            int id = recipe.SummonIds[Random.Range(0, recipe.SummonIds.Length)];
            Vector2 pos = FindSpawnPos((Vector2)originPos, recipe.SummonRadius);   // 避開牆/水的落點

            // 分身不帶 deathFlag（見 readme/TRIGGER_CHAIN.md §7）。玩家召喚=PlayerAlly、怪物召喚=Enemy。
            GameObject go = spawner.SpawnMonster(id, pos, null, faction);
            if (go == null) continue;
            aliveTracker.Add(go); spawned++;

            // 召喚特效：同一幀在生怪點播，並依這隻怪的可見大小縮放（大怪大特效、小怪小特效）。
            if (vfx != null)
            {
                float targetH = MonsterVisibleHeight(go);
                if (targetH > 0f) vfx.SpawnSizedToHeight(summonVfxId, pos, targetH);
                else vfx.Spawn(summonVfxId, pos, 0f, go.transform.localScale.x);
            }
        }
        return spawned > 0;
    }

    // 在施放者周圍找一個「不在牆/水裡」的生怪點：試多個角度、由外往內縮，挑第一個空的；都不行退回施放者腳下（一定可走）。
    private static Vector2 FindSpawnPos(Vector2 origin, float radius)
    {
        int mask = LayerMask.GetMask("Environment", "Water");
        Vector2 jitter = Random.insideUnitCircle * 0.3f;
        if (mask == 0) return origin + Vector2.ClampMagnitude(new Vector2(radius, 0f), radius) + jitter;  // 沒障礙層可查（保險）

        const float foot = 0.35f;   // 生怪點要空出的半徑（約怪的腳），這圈內不能有牆/水
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float r = radius * (1f - attempt * 0.07f);            // 逐次往內縮，靠近施放者（施放者站的地方通常可走）
            if (r < 0f) r = 0f;
            Vector2 p = origin + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            if (Physics2D.OverlapCircle(p, foot, mask) == null) return p;   // 沒撞牆/水＝可用
        }
        return origin;   // 都找不到：生在施放者腳下
    }

    // 怪物在畫面上的「可見高度」（世界單位）：route B 怪一律正規化到 CharacterWorldHeight × transform.Scale。取不到回 0。
    private static float MonsterVisibleHeight(GameObject go)
    {
        if (go == null) return 0f;
        var mc = go.GetComponent<MonsterController>();
        float s = go.transform.localScale.x;
        if (mc != null && mc.CharacterWorldHeight > 0f) return mc.CharacterWorldHeight * s;
        return 0f;
    }
}
