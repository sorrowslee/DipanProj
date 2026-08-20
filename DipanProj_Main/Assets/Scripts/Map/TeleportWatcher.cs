using System.Collections.Generic;
using UnityEngine;
using Dipan.MapRuntime;

/// <summary>
/// 每幀判斷玩家有沒有踩到當前地圖的傳送點，踩到就呼叫 MapManager.GoToMap。
///
/// <para><b>兩種模式</b>（見 <see cref="TeleportAnchor"/>）：
/// ① <b>點模式</b>（有 markerX/markerY）——玩家位置進到錨點矩形就算踩到。
/// ② <b>格子模式</b>（沒設錨點的舊傳送點）——同以前，比對格子。
/// 兩種可同圖並存。</para>
///
/// <para><b>⚠ 判定點一律是 <c>transform.position</c>，不是腳底。</b>
/// 曾經改用 <c>PlayerController.FeetWorldPos</c>（理由：光盤畫在地上，腳踩上去才合語意），
/// 結果是**牆邊的傳送點結構上不可能被觸發**——玩家的碰撞圓在 <c>transform.position</c>（胸口高度），
/// 頂到牆時胸口離牆只剩一個半徑，而腳底還在胸口下方約一整格，
/// 所以**腳底永遠無法靠近牆壁一格以內，而門就在牆上**。
/// 物理、可走層、地圖擺放全都以 <c>transform.position</c> 為準，觸發判定換座標系就會跟整個系統差一格。
/// 見 readme/PROBLEMS.md **B13**。</para>
///
/// 落地防抖（必要）：傳送著陸後玩家就站在目標傳送點上，若立刻判定會被彈回。
/// 故每次 Setup 後「未武裝」，等玩家離開所有 teleport 格才武裝；之後再踩才觸發。
/// 由 MapManager 在每次換圖後 Setup（重建格表、重置防抖）。見 readme/MAP_SYSTEM.md。
///
/// <para><b>非自主位移不算「踩到」</b>：被怪物擊退推到傳送點上時不觸發，並且**解除武裝**
/// —— 要玩家自己走出去再走回來才算數。實際踩過：進客廳2 時怪物就站在落點旁邊，
/// 玩家還沒看清楚就被擊退回傳送點 → 立刻又被送回書房。見 readme/PROBLEMS.md **B11**。</para>
/// </summary>
public class TeleportWatcher : MonoBehaviour
{
    MapData _map;
    Transform _player;
    MapManager _manager;
    string _teleportTypeId = "teleport";

    // 舊的「格子模式」查表（只放沒設錨點的傳送點）
    readonly Dictionary<long, TriggerRegion> _cells = new Dictionary<long, TriggerRegion>();
    // 新的「點模式」清單（有 markerX/markerY 的）。數量是個位數，逐一比矩形比建空間索引划算得多。
    readonly List<TriggerRegion> _anchored = new List<TriggerRegion>();
    HitReactionHandler _playerHit;
    bool _armed;

    public void Setup(MapData map, string teleportTypeId, Transform player, MapManager manager)
    {
        _map = map;
        _teleportTypeId = teleportTypeId;
        _player = player;
        _manager = manager;
        _playerHit = player != null ? player.GetComponent<HitReactionHandler>() : null;

        _cells.Clear();
        _anchored.Clear();
        if (map?.TriggerLayer?.regions != null)
        {
            foreach (var r in map.TriggerLayer.regions)
            {
                if (r.typeId != _teleportTypeId) continue;
                // 有錨點 → 點模式（矩形），此時**刻意不建它的格表**：格子只剩「以前畫過」的殘留，
                // 兩套同時生效會讓同一個傳送點有兩個觸發區，而且其中一個看不見。
                if (TeleportAnchor.HasAnchor(r)) { _anchored.Add(r); continue; }
                if (r.cells == null) continue;
                foreach (var c in r.cells)
                    if (c != null && c.Length >= 2) _cells[Key(c[0], c[1])] = r;
            }
        }

        _armed = false;   // 著陸時人就站在傳送點上，離開後才武裝（防抖）
    }

    void Update()
    {
        if (_map == null || _player == null || _manager == null) return;
        if (_cells.Count == 0 && _anchored.Count == 0) return;

        var region = FindAt();
        bool onTeleport = region != null;

        // 觸發鏈：停用中（startDisabled 未解鎖）或 requireFlag 不成立的傳送點，踩到視同沒踩（不消耗武裝）。
        // 每幀動態判定 → 被鏈解鎖的瞬間即可生效，不必重建格表。見 TriggerChain。
        if (onTeleport && !TriggerChain.IsActive(region)) onTeleport = false;

        // 非自主位移（目前只有擊退）把人推到傳送點上：不觸發，而且**解除武裝**。
        // ⚠ 只是「跳過這一幀」是不夠的——擊退結束時玩家還站在那格上，下一幀照樣觸發，只是延後 0.x 秒。
        //   解除武裝＝要玩家自己走出去再走回來，與上面的「著陸防抖」是同一套語意，直接複用。
        if (onTeleport && IsPushedAround()) { _armed = false; return; }

        if (!_armed)
        {
            if (!onTeleport) _armed = true;   // 離開傳送點 → 武裝
            return;
        }

        if (!onTeleport) return;

        // 踩到傳送點：解除武裝並換圖。目的地優先讀「傳送門執行期覆寫」（劇本決定的關卡），否則用區域自身設定。
        _armed = false;
        int targetMapId; string targetEntrance;
        if (TriggerChain.TryGetTeleportOverride(region.id, out int ovMap, out string ovEntrance))
        {
            targetMapId = ovMap; targetEntrance = ovEntrance;
        }
        else
        {
            targetMapId = region.GetInt("targetMapId", -1);
            targetEntrance = region.GetString("targetEntrance");
        }
        if (targetMapId < 0)
        {
            Debug.LogWarning($"[TeleportWatcher] 傳送點「{region.name}」未設定 targetMapId，略過。");
            return;
        }
        _manager.GoToMap(targetMapId, targetEntrance);
    }

    /// <summary>
    /// 玩家現在踩在哪個傳送點上（沒有回 null）。點模式先比（那是現行做法），再退回格子模式。
    /// </summary>
    TriggerRegion FindAt()
    {
        Vector2 probe = _player.position;   // ⚠ 一律用 transform.position，別換成腳底（見類別註解）
        if (_anchored.Count > 0)
        {
            for (int i = 0; i < _anchored.Count; i++)
                if (TeleportAnchor.Contains(_anchored[i], probe)) return _anchored[i];
        }
        if (_cells.Count > 0)
        {
            Vector2Int cell = MapCoords.WorldToCell(probe, _map);
            if (_cells.TryGetValue(Key(cell.x, cell.y), out var r)) return r;
        }
        return null;
    }


    /// <summary>
    /// 玩家目前是不是「被推著動」而不是自己走。之後若加拉扯／吹飛／輸送帶之類的位移，一併加進來——
    /// 規則是「**玩家不是自己走過去的，就不該觸發位置型事件**」。
    /// </summary>
    bool IsPushedAround() => _playerHit != null && _playerHit.IsKnockedBack;

    static long Key(int x, int y) => ((long)(uint)x << 32) | (uint)y;
}
