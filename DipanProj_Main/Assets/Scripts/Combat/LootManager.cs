using System.Collections.Generic;
using UnityEngine;
using Dipan.Inventory;
using Dipan.MapRuntime;
using Dipan.UI;

/// <summary>
/// 拾取互動的大腦：常駐單例（仿 InventorySystem）。統一管理兩種「靠近按 F 撿取」的目標：
///   1. **拾取點 trigger**（地圖編輯器放置；星星特效標示）——撿取時把 itemId×count 放進背包。
///   2. **地上掉落物**（背包滿了溢出、未來怪物掉落）——用道具 icon 縮小放地上。
///
/// 兩者共用同一套互動：每幀找「最近且在 PickupRadius 內」的目標 → 在它上方顯示「按 F 鍵拾取 XXX」
/// （PickupTipPanel）→ 玩家按 F 撿取最近那一個。背包滿了，溢出的部分掉成地上掉落物
/// （拾取點按 F 也走同一條路：吃得下的進背包、剩的掉腳下，清完背包再按 F 撿地上那份）。
/// 開背包等視窗時（UIManager.IsGameplayInputBlocked）不觸發。
///
/// 生命週期：拾取點與掉落物都屬「當前地圖」，換地圖時 MapManager 呼叫 <see cref="ClearAll"/> 清掉、
/// 再 <see cref="SetupPickupPoints"/> 重建（= 當次停留記憶；永久化屬 readme/MAP_SYSTEM.md 的 Phase 2）。
///
/// 拾取點的星星標示特效由本類別管理（而非 MapLoader），因為「撿掉後要即時移除該特效」，
/// 由知道何時撿掉的這裡負責最自然（teleport 標記不會中途消失才放在 MapLoader）。
/// </summary>
public class LootManager : MonoBehaviour
{
    [Header("撿取")]
    public KeyCode pickupKey = KeyCode.F;
    public float pickupRadius = 1.2f;     // 玩家進入此半徑才顯示提示、可撿

    [Header("掉落物外觀")]
    public float lootWorldSize = 0.6f;    // 地上 icon 的世界大小（稍小於 1 格）
    public string sortingLayerName = "Default";
    public int sortingOrder = 5;          // 低於角色（角色在 10），畫在地上
    public float tipHeight = 0.6f;        // 提示框相對目標的上方偏移（世界單位）

    [Header("拾取點星星特效")]
    public int markerStarCount = 5;
    public Color markerColor = new Color(1f, 0.92f, 0.45f, 1f);   // 金黃星
    public int markerSortingOrder = 20;   // 高於角色，星星浮在空中

    static LootManager _instance;
    public static bool Exists => _instance != null;
    public static LootManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<LootManager>();
                if (_instance == null)
                {
                    var go = new GameObject("[LootManager]");
                    _instance = go.AddComponent<LootManager>();
                }
            }
            return _instance;
        }
    }

    /// <summary>一個地圖編輯器放置的拾取點。</summary>
    class PickupPoint
    {
        public string id;
        public int itemId;
        public int count;
        public string name;
        public Vector2[] cellCenters;   // 區域各格中心（世界座標）
        public Vector2 center;          // 區域中心（提示與星星位置）
        public GameObject marker;       // 星星特效物件
    }

    readonly List<GroundLoot> _loot = new List<GroundLoot>();
    readonly List<PickupPoint> _points = new List<PickupPoint>();
    Transform _player;
    PickupTipPanel _tip;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ───────────────────────── 對外 API ─────────────────────────

    /// <summary>讀當前地圖的 pickup trigger，建立拾取點 + 星星標示。每次換圖由 MapManager 呼叫（會先清舊的）。</summary>
    public void SetupPickupPoints(MapData map, string pickupTypeId)
    {
        ClearPickupPoints();
        if (map?.TriggerLayer?.regions == null) return;
        string typeId = string.IsNullOrEmpty(pickupTypeId) ? "pickup" : pickupTypeId;
        var inv = InventorySystem.Instance;

        foreach (var r in map.TriggerLayer.regions)
        {
            if (r.typeId != typeId || r.cells == null || r.cells.Count == 0) continue;

            if (!int.TryParse(r.GetString("itemId"), out int itemId) || itemId <= 0)
            {
                Debug.LogWarning($"[LootManager] 拾取點「{r.name}」itemId 無效（{r.GetString("itemId")}），略過。");
                continue;
            }
            int count = Mathf.Max(1, r.GetInt("count", 1));

            // 各格中心 + 區域中心。
            var centers = new List<Vector2>(r.cells.Count);
            Vector2 sum = Vector2.zero;
            foreach (var c in r.cells)
            {
                if (c == null || c.Length < 2) continue;
                Vector2 cc = MapCoords.CellCenter(c[0], c[1], map);
                centers.Add(cc);
                sum += cc;
            }
            if (centers.Count == 0) continue;
            Vector2 center = sum / centers.Count;

            var data = inv != null ? inv.GetData(itemId) : null;
            var pt = new PickupPoint
            {
                id = r.id,
                itemId = itemId,
                count = count,
                name = data != null ? data.Name : $"#{itemId}",
                cellCenters = centers.ToArray(),
                center = center,
                marker = CreateMarker(center),
            };
            _points.Add(pt);
        }
    }

    /// <summary>在 pos 放一個掉落物（通用入口，怪物掉落也用這個）。同點重疊會稍微散開避免疊死。</summary>
    public GroundLoot DropLoot(int itemId, int count, Vector2 pos)
    {
        if (count <= 0) return null;
        var inv = InventorySystem.Instance;
        var data = inv != null ? inv.GetData(itemId) : null;
        if (data == null)
        {
            Debug.LogWarning($"[LootManager] 掉落物 itemId={itemId} 在 ItemTable 找不到，略過。");
            return null;
        }

        Vector2 spawn = Scatter(pos);
        var go = new GameObject($"GroundLoot_{data.Name}");
        go.transform.position = new Vector3(spawn.x, spawn.y, 0f);
        var loot = go.AddComponent<GroundLoot>();
        loot.Init(itemId, count, data.Name, data.Icon, lootWorldSize, sortingLayerName, sortingOrder);
        _loot.Add(loot);
        return loot;
    }

    /// <summary>清掉所有地上掉落物與拾取點（換地圖時呼叫）。</summary>
    public void ClearAll()
    {
        for (int i = 0; i < _loot.Count; i++)
            if (_loot[i] != null) Destroy(_loot[i].gameObject);
        _loot.Clear();
        ClearPickupPoints();
        if (_tip != null) _tip.HideTip();
    }

    void ClearPickupPoints()
    {
        for (int i = 0; i < _points.Count; i++)
            if (_points[i]?.marker != null) Destroy(_points[i].marker);
        _points.Clear();
    }

    // ───────────────────────── 每幀：提示 + 撿取 ─────────────────────────

    void Update()
    {
        for (int i = _loot.Count - 1; i >= 0; i--)
            if (_loot[i] == null) _loot.RemoveAt(i);

        if (_loot.Count == 0 && _points.Count == 0) { HideTip(); return; }

        // 開 UI（背包等）時不撿、不顯示提示。
        if (UIManager.IsGameplayInputBlocked) { HideTip(); return; }

        if (_player == null) _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (_player == null) { HideTip(); return; }
        Vector2 p = _player.position;
        float r2 = pickupRadius * pickupRadius;

        // 找最近的可撿目標（掉落物 + 拾取點一起比）。
        float best = r2;
        GroundLoot bestLoot = null;
        PickupPoint bestPoint = null;
        Vector3 tipPos = default;
        string tipName = null;

        for (int i = 0; i < _loot.Count; i++)
        {
            float d = ((Vector2)_loot[i].transform.position - p).sqrMagnitude;
            if (d <= best)
            {
                best = d; bestLoot = _loot[i]; bestPoint = null;
                tipPos = _loot[i].transform.position; tipName = _loot[i].DisplayName;
            }
        }
        for (int i = 0; i < _points.Count; i++)
        {
            float d = NearestCellSqr(_points[i], p);
            if (d <= best)
            {
                best = d; bestPoint = _points[i]; bestLoot = null;
                tipPos = _points[i].center; tipName = _points[i].name;
            }
        }

        if (bestLoot == null && bestPoint == null) { HideTip(); return; }

        if (_tip == null) _tip = PickupTipPanel.Ensure();
        if (_tip != null)
            _tip.ShowAt(tipPos + Vector3.up * tipHeight, $"按 {pickupKey} 鍵拾取 {tipName}");

        if (Input.GetKeyDown(pickupKey))
        {
            if (bestLoot != null) TryPickUpLoot(bestLoot);
            else CollectPoint(bestPoint);
        }
    }

    // 從地上撿（部分撿取：吃得下多少算多少，剩的留在地上）。
    void TryPickUpLoot(GroundLoot loot)
    {
        var inv = InventorySystem.Instance;
        if (inv == null || loot == null) return;

        int leftover = inv.AddItem(loot.ItemId, loot.Count);
        int added = loot.Count - leftover;

        if (added <= 0) { AlertPanel.Toast("背包已滿"); return; }

        AlertPanel.Toast(added > 1 ? $"獲得 {loot.DisplayName} ×{added}" : $"獲得 {loot.DisplayName}");

        if (leftover > 0) loot.SetCount(leftover);
        else { _loot.Remove(loot); Destroy(loot.gameObject); HideTip(); }
    }

    // 撿拾取點（一律消耗該點：吃得下的進背包、剩的掉腳下變成地上掉落物）。
    void CollectPoint(PickupPoint pt)
    {
        var inv = InventorySystem.Instance;
        if (inv == null || pt == null) return;

        int leftover = inv.AddItem(pt.itemId, pt.count);
        int added = pt.count - leftover;

        if (added > 0)
            AlertPanel.Toast(added > 1 ? $"獲得 {pt.name} ×{added}" : $"獲得 {pt.name}");

        if (leftover > 0)
        {
            DropLoot(pt.itemId, leftover, _player != null ? (Vector2)_player.position : pt.center);
            AlertPanel.Toast(leftover > 1 ? $"背包已滿，{pt.name} ×{leftover} 掉落地上"
                                          : $"背包已滿，{pt.name} 掉落地上");
        }

        if (pt.marker != null) Destroy(pt.marker);
        _points.Remove(pt);
        HideTip();
    }

    static float NearestCellSqr(PickupPoint pt, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < pt.cellCenters.Length; i++)
        {
            float d = (pt.cellCenters[i] - p).sqrMagnitude;
            if (d < best) best = d;
        }
        return best;
    }

    GameObject CreateMarker(Vector2 pos)
    {
        var go = new GameObject("PickupMarker");
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        var m = go.AddComponent<PickupMarker>();
        m.Configure(markerStarCount, markerColor, sortingLayerName, markerSortingOrder);
        return go;
    }

    void HideTip()
    {
        if (_tip != null) _tip.HideTip();
    }

    Vector2 Scatter(Vector2 pos)
    {
        const float minDist = 0.35f;
        for (int i = 0; i < _loot.Count; i++)
        {
            if (_loot[i] == null) continue;
            if (((Vector2)_loot[i].transform.position - pos).sqrMagnitude < minDist * minDist)
                return pos + Random.insideUnitCircle * 0.45f;
        }
        return pos;
    }
}
