using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 劇本同伴（2026-10-15，血狂之爭：選邊後「首領跟著玩家走」）。由觸發鏈動作 <c>companion</c> 呼叫。
///
/// 做法＝**重用玩家召喚物（PlayerAlly）那一整套**，只多一個 <see cref="MonsterController.IsCompanion"/> 標記：
/// ‧ 跟隨玩家、看到敵人就衝上去 ＝ <see cref="AllyBrain"/>（MonsterSpawner 對 PlayerAlly 自動掛）。
/// ‧ 跨圖跟著走 ＝ MapManager 本來就保留 PlayerAlly、並在新落點 RepositionPlayerAllies。
/// ‧ 沒有殺傷力 ＝ 同伴用的 MonsterData 列 ContactDamage 填 0（揮擊動畫照播，EnemyContactDamage 直接不結算）。
/// ‧ 不會受傷、不被任何怪當目標 ＝ IsCompanion（MonsterController.TakeDamage／FindNearestEnemy、EnemyContactDamage 會跳過）。
///
/// 生命週期＝**這一趟關卡**：換 module（回廣場、死亡、過關）時由 <see cref="TriggerChain.ClearLevelFlags"/> 呼叫 <see cref="DismissAll"/>；
/// 不收掉的話它會以 PlayerAlly 的身分一路跟到邪佛廣場。
/// </summary>
public static class CompanionService
{
    static readonly List<MonsterController> _list = new List<MonsterController>();

    /// <summary>目前場上的同伴數（給除錯／條件用）。</summary>
    public static int Count { get { _list.RemoveAll(m => m == null); return _list.Count; } }

    /// <summary>
    /// 召喚一位同伴。<paramref name="at"/> 有值＝生在該世界座標（例：劇情演員剛退場的位置，畫面才連得起來）；
    /// 沒值＝玩家身旁。同一個 monsterId 已在場就不重複生。回傳是否有生出來。
    /// </summary>
    public static bool Summon(int monsterId, Vector2? at)
    {
        _list.RemoveAll(m => m == null);
        foreach (var m in _list)
            if (m != null && m.DataId == monsterId)
            {
                Debug.Log($"[CompanionService] 同伴 {monsterId} 已在場，不重複生成。");
                return false;
            }

        var spawner = Object.FindObjectOfType<MonsterSpawner>();
        if (spawner == null) { Debug.LogWarning("[CompanionService] 場景找不到 MonsterSpawner，無法召喚同伴。"); return false; }

        Vector2 pos;
        if (at.HasValue) pos = at.Value;
        else
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            pos = p != null ? (Vector2)p.transform.position + new Vector2(-1.2f, 0f) : Vector2.zero;
        }

        var go = spawner.SpawnMonster(monsterId, pos, null, MonsterFaction.PlayerAlly, null, false);
        var mc = go != null ? go.GetComponent<MonsterController>() : null;
        if (mc == null) { Debug.LogWarning($"[CompanionService] 同伴 {monsterId} 生成失敗（MonsterData 沒有這一列？）。"); return false; }
        mc.IsCompanion = true;
        _list.Add(mc);
        Debug.Log($"[CompanionService] 同伴 {monsterId}「{mc.MonsterName}」加入。");
        return true;
    }

    /// <summary>解散全部同伴（立即銷毀，不播死亡、不掉寶）。</summary>
    public static void DismissAll()
    {
        for (int i = 0; i < _list.Count; i++)
            if (_list[i] != null) Object.Destroy(_list[i].gameObject);
        if (_list.Count > 0) Debug.Log($"[CompanionService] 解散 {_list.Count} 位同伴。");
        _list.Clear();
    }

    /// <summary>進 Play 模式歸零（已關 Domain Reload；由 PlayModeStaticReset 呼叫）。</summary>
    public static void ResetForPlayMode() => _list.Clear();
}
