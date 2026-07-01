using System.Collections.Generic;
using UnityEngine;
using Dipan.MapRuntime;

/// <summary>
/// 每幀看玩家是否踩在「鏡頭區（camZone）」trigger 上：踩進就讓相機拉遠/位移（該區參數），
/// 離開所有鏡頭區就還原。仿 TeleportWatcher 由 MapManager 在每次換圖後 Setup。
///
/// 參數（camZone 區域）：
///   zoom    = 縮放倍率（在本地圖正常縮放上再乘；1=不變、>1 拉遠看更廣、<1 拉近）。留空=1。
///   offsetX = 鏡頭往右(+)/左(-)位移（世界單位）。留空=0。
///   offsetY = 鏡頭往上(+)/下(-)位移（世界單位）。留空=0。
/// 實際縮放/位移與平滑過渡由 MapCameraController 處理。見 readme/MAP_SYSTEM.md。
/// </summary>
public class CameraZoneWatcher : MonoBehaviour
{
    MapData _map;
    Transform _player;
    MapCameraController _camCtl;
    string _typeId = "camZone";

    readonly Dictionary<long, TriggerRegion> _cells = new Dictionary<long, TriggerRegion>();
    TriggerRegion _active;   // 玩家目前所在的鏡頭區（null = 不在任何鏡頭區）

    public void Setup(MapData map, string camZoneTypeId, Transform player, MapCameraController camCtl)
    {
        _map = map;
        _typeId = camZoneTypeId;
        _player = player;
        _camCtl = camCtl;

        _cells.Clear();
        if (map?.TriggerLayer?.regions != null)
        {
            foreach (var r in map.TriggerLayer.regions)
            {
                if (r.typeId != _typeId || r.cells == null) continue;
                foreach (var c in r.cells)
                    if (c != null && c.Length >= 2) _cells[Key(c[0], c[1])] = r;
            }
        }

        _active = null;
        if (_camCtl != null) _camCtl.ClearCameraZone();   // 換圖先還原
    }

    void Update()
    {
        if (_map == null || _player == null || _camCtl == null) return;

        Vector2Int cell = MapCoords.WorldToCell(_player.position, _map);
        _cells.TryGetValue(Key(cell.x, cell.y), out var region);   // 不在任何鏡頭區時 region = null

        if (region == _active) return;   // 狀態沒變

        _active = region;
        if (region != null)
        {
            float zoom = region.GetFloat("zoom", 1f);
            float ox = region.GetFloat("offsetX", 0f);
            float oy = region.GetFloat("offsetY", 0f);
            _camCtl.SetCameraZone(zoom, new Vector2(ox, oy));
        }
        else
        {
            _camCtl.ClearCameraZone();
        }
    }

    static long Key(int x, int y) => ((long)(uint)x << 32) | (uint)y;
}
