using UnityEngine;
using Dipan.MapRuntime;

/// <summary>
/// 依 .dipanmap 的 npcs 清單生一個 NPC（由 MapLoader.SpawnNpcs 呼叫）。
/// NPC 建立在怪物那套地基上（MonsterController／MonsterAnimator／MonsterActuator／BlobShadow／YSortByFeet 全沿用）：
///  ‧ 陣營 <see cref="MonsterFaction.Neutral"/>＋放 Ally 層 → 玩家子彈打不到、敵怪不打它、不推任何人。
///  ‧ ContactDamage=0 → 不傷人；DropsLoot=false、SpawnKey=null → 不掉寶、不記關卡進度。
///  ‧ 未來護送：HP／受擊反應天生就有，屆時只補 brain 模式與血條。
/// NPC 物件不掛 MapRoot（與怪物一致），換圖由 MapManager.ClearTransientGameplay 清掉（非 PlayerAlly 都會清）。
/// </summary>
public static class NpcSpawner
{
    public static GameObject Spawn(NpcInstance inst)
    {
        if (inst == null) return null;
        var data = NpcDatabase.Instance.Get(inst.npcId);
        if (data == null)
        {
            Debug.LogWarning($"[NpcSpawner] NPC 擺放「{inst.name}」的 npcId={inst.npcId} 在 NpcTable 找不到，略過。");
            return null;
        }

        var go = new GameObject("Npc_" + data.Name);
        go.transform.position = new Vector3(inst.x, inst.y, 0f);
        go.transform.localScale = Vector3.one * (data.Scale > 0f ? data.Scale : 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;   // 初始值；實際由 YSortByFeet 每幀依腳底 Y 覆寫（MonsterController.Start 會掛）

        // 放 Ally 層：玩家子彈（打 Enemy 層）打不到、也不與玩家/怪物做硬碰撞（FactionLayers 已設 ignore）。
        if (FactionLayers.AllyLayer >= 0) go.layer = FactionLayers.AllyLayer;

        var mc = go.AddComponent<MonsterController>();
        mc.Initialize(ToMonsterData(data, inst));
        mc.Faction = MonsterFaction.Neutral;   // 中立：不打人、不被打（見 FactionRelations）
        mc.FaceMovement = true;                // 走路面向移動方向；停下時玩家在感測範圍內才面向玩家
        mc.DropsLoot = false;

        // 行為：原地 / 來回。巡邏點 = [站位, 路徑點…]，乒乓來回。
        var mode = inst.behavior == NpcInstance.BehaviorPatrol ? NpcBrain.Mode.Patrol : NpcBrain.Mode.Idle;
        Vector2[] points = null;
        if (mode == NpcBrain.Mode.Patrol && inst.waypoints != null && inst.waypoints.Count > 0)
        {
            points = new Vector2[inst.waypoints.Count + 1];
            points[0] = new Vector2(inst.x, inst.y);
            for (int i = 0; i < inst.waypoints.Count; i++)
                points[i + 1] = new Vector2(inst.waypoints[i].x, inst.waypoints[i].y);
        }
        else if (mode == NpcBrain.Mode.Patrol)
        {
            Debug.LogWarning($"[NpcSpawner] NPC「{inst.name}」設了來回走動但沒有路徑點，改為原地不動。");
            mode = NpcBrain.Mode.Idle;
        }
        var brain = new NpcBrain(mode, points, inst.dwellSeconds);
        mc.SetBrain(brain);

        // 互動（按 F 交談 → 對話 → 開介面 → 接鏈）＋頭上對話泡泡標示。
        var agent = go.AddComponent<NpcAgent>();
        agent.Configure(mc, brain, inst, data);

        return go;
    }

    /// <summary>把 NpcTable 的一列＋這個擺放的覆寫，換成怪物地基吃的 MonsterData。</summary>
    static MonsterData ToMonsterData(NpcData d, NpcInstance inst)
    {
        return new MonsterData
        {
            ID = d.ID,
            Name = d.Name,                 // ＝序列圖資料夾名（Monsters/SequenceImage/<Name>/）
            HP = d.HP > 0f ? d.HP : 50f,
            BrainType = "Npc",             // Initialize 的 switch 沒這 case → 先掛 ChaseBrain，隨後 SetBrain 換成 NpcBrain
            Weapon = "",
            Scale = d.Scale > 0f ? d.Scale : 1f,
            PrefabPath = "",
            Speed = inst.speed > 0f ? inst.speed : (d.Speed > 0f ? d.Speed : 2f),   // 每個擺放可覆寫速度
            AnimFPS = d.AnimFPS > 0f ? d.AnimFPS : 8f,
            ContactDamage = 0f,            // NPC 不傷人（EnemyContactDamage 對 0 直接不運作）
            AttackInterval = 0.5f,
            DamageReduction = 0f,
            DetectionRange = 0f,           // 0＝NPC 平時**完全不看玩家**（走路面向移動方向、原地保持原朝向；
                                           // 只有按 F 對話那一刻由 NpcAgent 轉向玩家、對話完轉回）。
                                           // 2026-08-28 作者拍板：原本 2.6 格「靠近就轉頭」會和走路面向互搶（左右鬼打牆）。
            DisplayName = d.ShownName,
        };
    }
}
