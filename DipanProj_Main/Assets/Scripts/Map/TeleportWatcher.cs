using System.Collections.Generic;
using UnityEngine;
using Dipan.MapRuntime;

/// <summary>
/// 每幀把玩家所在格與當前地圖的 teleport 格比對，踩到就呼叫 MapManager.GoToMap。
///
/// 落地防抖（必要）：傳送著陸後玩家就站在目標傳送點上，若立刻判定會被彈回。
/// 故每次 Setup 後「未武裝」，等玩家離開所有 teleport 格才武裝；之後再踩才觸發。
/// 由 MapManager 在每次換圖後 Setup（重建格表、重置防抖）。見 readme/MAP_SYSTEM.md。
/// </summary>
public class TeleportWatcher : MonoBehaviour
{
    MapData _map;
    Transform _player;
    MapManager _manager;
    string _teleportTypeId = "teleport";

    readonly Dictionary<long, TriggerRegion> _cells = new Dictionary<long, TriggerRegion>();
    bool _armed;

    public void Setup(MapData map, string teleportTypeId, Transform player, MapManager manager)
    {
        _map = map;
        _teleportTypeId = teleportTypeId;
        _player = player;
        _manager = manager;

        _cells.Clear();
        if (map?.TriggerLayer?.regions != null)
        {
            foreach (var r in map.TriggerLayer.regions)
            {
                if (r.typeId != _teleportTypeId || r.cells == null) continue;
                foreach (var c in r.cells)
                    if (c != null && c.Length >= 2) _cells[Key(c[0], c[1])] = r;
            }
        }

        _armed = false;   // 著陸時人就站在傳送點上，離開後才武裝（防抖）
    }

    void Update()
    {
        if (_map == null || _player == null || _manager == null || _cells.Count == 0) return;

        Vector2Int cell = MapCoords.WorldToCell(_player.position, _map);
        bool onTeleport = _cells.TryGetValue(Key(cell.x, cell.y), out var region);

        // 觸發鏈：停用中（startDisabled 未解鎖）或 requireFlag 不成立的傳送點，踩到視同沒踩（不消耗武裝）。
        // 每幀動態判定 → 被鏈解鎖的瞬間即可生效，不必重建格表。見 TriggerChain。
        if (onTeleport && !TriggerChain.IsActive(region)) onTeleport = false;

        if (!_armed)
        {
            if (!onTeleport) _armed = true;   // 離開傳送點 → 武裝
            return;
        }

        if (!onTeleport) return;

        // 踩到傳送點：解除武裝並換圖。
        _armed = false;
        int targetMapId = region.GetInt("targetMapId", -1);
        string targetEntrance = region.GetString("targetEntrance");
        if (targetMapId < 0)
        {
            Debug.LogWarning($"[TeleportWatcher] 傳送點「{region.name}」未設定 targetMapId，略過。");
            return;
        }
        _manager.GoToMap(targetMapId, targetEntrance);
    }

    static long Key(int x, int y) => ((long)(uint)x << 32) | (uint)y;
}
