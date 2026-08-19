using System;
using System.Collections.Generic;
using UnityEngine;
using Dipan.MapRuntime;
using Dipan.Cutscene;

/// <summary>
/// 每幀比對玩家所在格與當前地圖的 cutscene 觸發格；踩到就播一段過場表演，**一次性**。
/// 仿 <see cref="TeleportWatcher"/>，但不是直接換圖，而是：
///   觸發 → 穿隧道表演（TunnelWalkController）→ （之後：播邪佛影片）→ MapManager.GoToMap(目標圖)。
///
/// 由 MapManager 在每次換圖後 Setup（重建格表、重置）。目前 cutsceneId 任意值都跑「穿隧道」；
/// 之後要支援多種過場，再依 cutsceneId 分派。
/// </summary>
public class CutsceneWatcher : MonoBehaviour
{
    MapData _map;
    Transform _player;
    MapManager _manager;
    TunnelWalkController _tunnel;
    VideoPlayerOverlay _video;
    string _typeId = "cutscene";

    readonly Dictionary<long, TriggerRegion> _cells = new Dictionary<long, TriggerRegion>();
    HitReactionHandler _playerHit;
    bool _armed;
    bool _fired;                 // 一次性：觸發過就不再觸發（即使走回去）
    bool _running;               // 表演進行中

    int _pendingMapId = -1;
    string _pendingEntrance;
    string _pendingVideo;
    Action _onTunnelDone;

    public void Setup(MapData map, string cutsceneTypeId, Transform player, MapManager manager,
                      TunnelWalkController tunnel, VideoPlayerOverlay video)
    {
        _map = map;
        _typeId = string.IsNullOrEmpty(cutsceneTypeId) ? "cutscene" : cutsceneTypeId;
        _player = player;
        _manager = manager;
        _tunnel = tunnel;
        _video = video;
        _playerHit = player != null ? player.GetComponent<HitReactionHandler>() : null;

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

        _armed = false;
        _fired = false;
        _running = false;
    }

    void Update()
    {
        if (_fired || _running || _map == null || _player == null || _manager == null || _cells.Count == 0) return;

        Vector2Int cell = MapCoords.WorldToCell(_player.position, _map);
        bool on = _cells.TryGetValue(Key(cell.x, cell.y), out var region);

        // 觸發鏈：停用中或 requireFlag 不成立的過場點，踩到視同沒踩（每幀動態判定）。見 TriggerChain。
        if (on && !TriggerChain.IsActive(region)) on = false;

        // 非自主位移（目前只有擊退）把人推到過場點上：不觸發，而且**解除武裝**（同 TeleportWatcher 的處理）。
        // 過場是一次性的（_fired），被擊退誤觸的代價比傳送更高——白白播掉一段只能看一次的演出。
        if (on && _playerHit != null && _playerHit.IsKnockedBack) { _armed = false; return; }

        if (!_armed)
        {
            if (!on) _armed = true;   // 著陸/出生時若剛好站在觸發點上，離開後才武裝
            return;
        }
        if (!on) return;

        Fire(region);
    }

    void Fire(TriggerRegion region)
    {
        _fired = true;
        _running = true;
        _pendingMapId = region.GetInt("targetMapId", -1);
        _pendingEntrance = region.GetString("targetEntrance");
        _pendingVideo = region.GetString("video");

        var tunnel = _tunnel != null ? _tunnel : FindObjectOfType<TunnelWalkController>();
        if (tunnel == null)
        {
            Debug.LogError("[CutsceneWatcher] 找不到 TunnelWalkController（場景上掛一個、或拖進 MapManager.tunnelWalk）。直接換圖。");
            AfterPerformance();
            return;
        }

        _tunnel = tunnel;
        // 穿隧道表演 → 完成 → AfterPerformance（之後在那裡插影片）→ GoToMap。
        _onTunnelDone = OnTunnelDone;
        tunnel.OnComplete += _onTunnelDone;
        tunnel.Play();
        Debug.Log($"[CutsceneWatcher] 觸發過場「{region.name}」→ 穿隧道；完成後 → 地圖 #{_pendingMapId}");
    }

    void OnTunnelDone()
    {
        if (_tunnel != null && _onTunnelDone != null) _tunnel.OnComplete -= _onTunnelDone;
        _onTunnelDone = null;
        AfterPerformance();
    }

    void AfterPerformance()
    {
        // 穿隧道完 → 若有設 video 就播（邪佛動畫），播完/跳過再 GoNext；否則直接 GoNext。
        if (!string.IsNullOrEmpty(_pendingVideo))
        {
            var v = _video != null ? _video : FindObjectOfType<VideoPlayerOverlay>();
            if (v != null) { _video = v; v.Play(_pendingVideo, GoNext); return; }
            Debug.LogWarning("[CutsceneWatcher] 有設 video 但場景沒有 VideoPlayerOverlay，略過影片。");
        }
        GoNext();
    }

    void GoNext()
    {
        _running = false;
        if (_pendingMapId < 0)
        {
            Debug.LogWarning("[CutsceneWatcher] cutscene 未設 targetMapId，表演完停在原地。");
            return;
        }
        _manager.GoToMap(_pendingMapId, _pendingEntrance);
    }

    static long Key(int x, int y) => ((long)(uint)x << 32) | (uint)y;
}
