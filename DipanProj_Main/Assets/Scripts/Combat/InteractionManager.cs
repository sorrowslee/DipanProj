using System.Collections.Generic;
using UnityEngine;
using Dipan.Inventory;
using Dipan.MapRuntime;
using Dipan.UI;
using Dipan.Drama;

/// <summary>
/// 「靠近按 F 互動」的大腦：常駐單例（仿 InventorySystem）。統一管理所有互動目標：
///   1. **拾取點 trigger**（地圖編輯器 pickup；星星標示）——撿取 itemId×count 進背包。
///   2. **劇情觸發點 trigger**（地圖編輯器 drama；星星標示）——開啟 DramaPanel(dramaId)。
///   3. **地上掉落物**（背包滿了溢出、未來怪物掉落）——道具 icon 縮小放地上。
///
/// 共用同一套互動：每幀找「最近且在 PickupRadius 內」的目標 → 在它上方顯示提示（PickupTipPanel）
/// → 玩家按 F 互動最近那一個。提示文字：拾取＝「按 F 鍵拾取 XXX」、劇情＝「按 F 鍵」。
/// 開背包/劇情等視窗時（UIManager.IsGameplayInputBlocked）不觸發。
///
/// 生命週期：互動點與掉落物都屬「當前地圖」，換地圖時 MapManager 呼叫 <see cref="ClearAll"/> 清掉、
/// 再 <see cref="SetupInteractPoints"/> 重建（= 當次停留記憶；永久化屬 readme/MAP_SYSTEM.md 的 Phase 2）。
/// 星星標示由本類別管理（撿掉 / 看完劇情要即時移除）。
/// </summary>
public class InteractionManager : MonoBehaviour
{
    [Header("互動")]
    public KeyCode interactKey = KeyCode.F;
    public float pickupRadius = 1.2f;     // 玩家進入此半徑才顯示提示、可互動
    public float dramaTouchRadius = 0.6f; // Type 2 劇情點：玩家進入此半徑就「碰到自動觸發」（不需按鍵）

    [Header("掉落物外觀")]
    public float lootWorldSize = 0.6f;    // 地上 icon 的世界大小（稍小於 1 格）
    public string sortingLayerName = "Default";
    public int sortingOrder = 5;          // 低於角色（角色在 10），畫在地上
    public float tipHeight = 0.6f;        // 提示框相對目標的上方偏移（世界單位）

    [Header("互動點星星特效")]
    public int markerStarCount = 5;
    public Color pickupMarkerColor = new Color(1f, 0.92f, 0.45f, 1f);   // 拾取點：金黃星
    public Color dramaMarkerColor = new Color(0.72f, 0.5f, 1f, 1f);     // 劇情點：紫星
    public int markerSortingOrder = 20;   // 高於角色，星星浮在空中

    static InteractionManager _instance;
    public static bool Exists => _instance != null;
    public static InteractionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<InteractionManager>();
                if (_instance == null)
                {
                    var go = new GameObject("[InteractionManager]");
                    _instance = go.AddComponent<InteractionManager>();
                }
            }
            return _instance;
        }
    }

    enum PointKind { Pickup, Drama }

    // 重複規則（編輯器「重複規則」欄；決定同一個互動點多久能再觸發一次）。
    //   Visit（預設，空值）：每次進這張地圖觸發一次（當次停留消耗，離圖重進復活）。
    //   Always：每次踩/按都觸發（不消耗；自動觸發型加「離開半徑才重新武裝」避免每幀洗版）。
    //   Cycle：每周目觸發一次（觸發後寫周目自動旗標，輪迴會清 → 下周目再觸發）。
    //   Life：整個角色一輩子一次（寫終身自動旗標，跨輪迴保存；開新角色才會再觸發）。
    enum RepeatMode { Visit, Always, Cycle, Life }

    static RepeatMode ParseRepeat(string s)
    {
        if (string.IsNullOrEmpty(s)) return RepeatMode.Visit;
        switch (s.Trim())
        {
            case "每次": case "always": return RepeatMode.Always;
            case "每周目": case "cycle": return RepeatMode.Cycle;
            case "永久": case "life": return RepeatMode.Life;
            default: return RepeatMode.Visit;   // 「每次進場」/未知 → 預設
        }
    }

    // Cycle/Life 用的自動旗標名（作者不用命名，系統以 trigger id 產生）。Life 加「永久:」前綴 → 存終身旗標。
    static string SeenKey(RepeatMode m, string id)
        => (m == RepeatMode.Life ? TriggerChain.LifePrefix : "") + "已觸發:" + id;

    /// <summary>一個地圖編輯器放置的互動點（拾取點或劇情點）。</summary>
    class InteractPoint
    {
        public string id;
        public PointKind kind;
        // pickup
        public int itemId;
        public int count;
        public string name;
        // drama
        public int dramaId;
        public bool autoTrigger;        // Type 2（頭像對話）：碰到自動觸發、不顯示「按 F」提示
        // 共用
        public TriggerRegion region;    // 來源 trigger（觸發鏈 next/setFlag 用，見 TriggerChain）
        public Vector2[] cellCenters;   // 區域各格中心（世界座標）
        public Vector2 center;          // 區域中心（提示與星星位置）
        public GameObject marker;       // 星星特效物件
        public RepeatMode repeat;       // 重複規則（見 RepeatMode）
        public bool armedForAlways = true;  // 僅 Always 自動觸發用：玩家離開半徑後重新武裝，避免每幀狂觸發
    }

    readonly List<GroundLoot> _loot = new List<GroundLoot>();
    readonly List<InteractPoint> _points = new List<InteractPoint>();
    readonly HashSet<string> _consumed = new HashSet<string>();  // 本次停留已消耗的點（重建互動點時不復活）
    MapData _lastMap; string _lastPickupT, _lastDramaT;          // RebuildPoints 用
    Transform _player;
    PickupTipPanel _tip;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ───────────────────────── 對外 API ─────────────────────────

    /// <summary>讀當前地圖的 pickup / drama trigger，建立互動點 + 星星標示。每次換圖由 MapManager 呼叫（會先清舊的）。</summary>
    public void SetupInteractPoints(MapData map, string pickupTypeId, string dramaTypeId)
    {
        _consumed.Clear();   // 換圖 = 新的一次停留（當次停留記憶重置；永久化屬 Phase 2）
        _lastMap = map; _lastPickupT = pickupTypeId; _lastDramaT = dramaTypeId;
        BuildPoints();
    }

    /// <summary>重建互動點但**保留**本次停留的已消耗記錄（觸發鏈解鎖/旗標變動後由 MapManager.RefreshTriggers 呼叫）。</summary>
    public void RebuildPoints() => BuildPoints();

    void BuildPoints()
    {
        ClearPoints();
        var map = _lastMap;
        if (map?.TriggerLayer?.regions == null) return;
        string pickupT = string.IsNullOrEmpty(_lastPickupT) ? "pickup" : _lastPickupT;
        string dramaT = string.IsNullOrEmpty(_lastDramaT) ? "drama" : _lastDramaT;
        var inv = InventorySystem.Instance;

        foreach (var r in map.TriggerLayer.regions)
        {
            if (r.cells == null || r.cells.Count == 0) continue;
            bool isPickup = r.typeId == pickupT;
            bool isDrama = r.typeId == dramaT;
            if (!isPickup && !isDrama) continue;
            if (_consumed.Contains(r.id)) continue;      // 本次停留已消耗 → 不復活
            if (!TriggerChain.IsActive(r)) continue;     // 停用中（startDisabled 未解鎖）或 requireFlag 不成立 → 隱形

            // 重複規則：每周目/永久 觸發過就不再現身（自動旗標依 scope 存周目/終身）。
            var repeat = ParseRepeat(r.GetString("repeat"));
            if ((repeat == RepeatMode.Cycle || repeat == RepeatMode.Life)
                && TriggerChain.FlagTrue(SeenKey(repeat, r.id))) continue;

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

            var pt = new InteractPoint
            {
                id = r.id,
                region = r,
                cellCenters = centers.ToArray(),
                center = center,
                repeat = repeat,
            };

            if (isPickup)
            {
                if (!int.TryParse(r.GetString("itemId"), out int itemId) || itemId <= 0)
                {
                    Debug.LogWarning($"[InteractionManager] 拾取點「{r.name}」itemId 無效（{r.GetString("itemId")}），略過。");
                    continue;
                }
                var data = inv != null ? inv.GetData(itemId) : null;
                pt.kind = PointKind.Pickup;
                pt.itemId = itemId;
                pt.count = Mathf.Max(1, r.GetInt("count", 1));
                pt.name = data != null ? data.Name : $"#{itemId}";
                pt.marker = CreateMarker(center, pickupMarkerColor);
            }
            else // drama
            {
                int dramaId = r.GetInt("dramaId", 0);
                if (dramaId <= 0)
                {
                    Debug.LogWarning($"[InteractionManager] 劇情點「{r.name}」dramaId 無效（{r.GetString("dramaId")}），略過。");
                    continue;
                }
                pt.kind = PointKind.Drama;
                pt.dramaId = dramaId;
                var dd = DramaDatabase.Instance.Get(dramaId);
                pt.autoTrigger = dd != null && dd.Type == 2;   // Type 2＝碰到自動觸發；Type 1＝按 F
                // Type 1 才放紫色星星提示；Type 2（碰到自動觸發）不顯示星星（純隱形觸發點）。
                if (!pt.autoTrigger) pt.marker = CreateMarker(center, dramaMarkerColor);
            }
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
            Debug.LogWarning($"[InteractionManager] 掉落物 itemId={itemId} 在 ItemTable 找不到，略過。");
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

    /// <summary>清掉所有地上掉落物與互動點（換地圖時呼叫）。</summary>
    public void ClearAll()
    {
        for (int i = 0; i < _loot.Count; i++)
            if (_loot[i] != null) Destroy(_loot[i].gameObject);
        _loot.Clear();
        ClearPoints();
        if (_tip != null) _tip.HideTip();
    }

    void ClearPoints()
    {
        for (int i = 0; i < _points.Count; i++)
            if (_points[i]?.marker != null) Destroy(_points[i].marker);
        _points.Clear();
    }

    // ───────────────────────── 每幀：提示 + 互動 ─────────────────────────

    void Update()
    {
        for (int i = _loot.Count - 1; i >= 0; i--)
            if (_loot[i] == null) _loot.RemoveAt(i);

        if (_loot.Count == 0 && _points.Count == 0) { HideTip(); return; }

        // 開 UI（背包/劇情等）時不互動、不顯示提示。
        if (UIManager.IsGameplayInputBlocked) { HideTip(); return; }

        if (_player == null) _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (_player == null) { HideTip(); return; }
        Vector2 p = _player.position;

        // 先處理「碰到自動觸發」的劇情點（Type 2）：踏進區域就觸發，不需按鍵、不顯示提示。
        float touchSqr = dramaTouchRadius * dramaTouchRadius;
        for (int i = 0; i < _points.Count; i++)
        {
            var ap = _points[i];
            if (!ap.autoTrigger) continue;
            bool inside = NearestCellSqr(ap, p) <= touchSqr;

            if (ap.repeat == RepeatMode.Always)
            {
                // 不消耗：只有「離開半徑後再踏進」才觸發一次，避免站在裡面每幀狂觸發。
                if (!inside) { ap.armedForAlways = true; continue; }
                if (!ap.armedForAlways) continue;
                ap.armedForAlways = false;
                HideTip();
                TriggerDrama(ap);
                return;
            }

            if (inside)
            {
                HideTip();
                TriggerDrama(ap);   // 內含 ConsumePoint（移除星星、當次不再觸發）
                return;             // 觸發後本幀就不再處理其他互動（開了對話會擋輸入）
            }
        }

        // 找最近的可互動目標（掉落物 + 互動點一起比）。
        float best = pickupRadius * pickupRadius;
        GroundLoot bestLoot = null;
        InteractPoint bestPoint = null;
        Vector3 tipPos = default;
        string tipText = null;

        for (int i = 0; i < _loot.Count; i++)
        {
            float d = ((Vector2)_loot[i].transform.position - p).sqrMagnitude;
            if (d <= best)
            {
                best = d; bestLoot = _loot[i]; bestPoint = null;
                tipPos = _loot[i].transform.position;
                tipText = $"按 {interactKey} 鍵拾取 {_loot[i].DisplayName}";
            }
        }
        for (int i = 0; i < _points.Count; i++)
        {
            if (_points[i].autoTrigger) continue;   // 自動觸發點不參與「按 F」提示/選取
            float d = NearestCellSqr(_points[i], p);
            if (d <= best)
            {
                best = d; bestPoint = _points[i]; bestLoot = null;
                tipPos = _points[i].center;
                tipText = _points[i].kind == PointKind.Pickup
                    ? $"按 {interactKey} 鍵拾取 {_points[i].name}"
                    : $"按 {interactKey} 鍵";   // 劇情點：只提示按鍵
            }
        }

        if (bestLoot == null && bestPoint == null) { HideTip(); return; }

        if (_tip == null) _tip = PickupTipPanel.Ensure();
        if (_tip != null) _tip.ShowAt(tipPos + Vector3.up * tipHeight, tipText);

        if (Input.GetKeyDown(interactKey))
        {
            if (bestLoot != null) TryPickUpLoot(bestLoot);
            else if (bestPoint.kind == PointKind.Pickup) CollectPickup(bestPoint);
            else TriggerDrama(bestPoint);
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
    void CollectPickup(InteractPoint pt)
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

        MarkRepeatSeen(pt);
        ConsumePoint(pt);
        TriggerChain.OnCompleted(pt.region);   // 觸發鏈：撿完 = 完成 → setFlag + 接 next
    }

    // 觸發劇情點（依 DramaTable 的 Type 分支，並消耗該點 = 當次停留不再觸發；離開地圖會重建）。
    void TriggerDrama(InteractPoint pt)
    {
        if (pt == null) return;

        TriggerChain.CompleteAfterDrama(pt.region);     // 觸發鏈：等對話面板關閉才算完成（面板 OnClose 通知）

        var data = DramaDatabase.Instance.Get(pt.dramaId);
        if (data != null && data.Type == 2)
            DramaTalkController.Play(data.TalkGroup);   // Type 2：頭像對話（開 TalkPanel）
        else
            DramaPanel.Show(pt.dramaId);                // Type 1（或找不到資料）：大圖 + 文字（現有）

        MarkRepeatSeen(pt);
        ConsumePoint(pt);
    }

    // 每周目/永久：觸發後寫自動旗標，讓之後（跨停留、依 scope 跨輪迴）不再現身。
    void MarkRepeatSeen(InteractPoint pt)
    {
        if (pt.repeat == RepeatMode.Cycle || pt.repeat == RepeatMode.Life)
            TriggerChain.SetFlag(SeenKey(pt.repeat, pt.id));
    }

    void ConsumePoint(InteractPoint pt)
    {
        // Always：不消耗（靠「離開半徑重新武裝」控制節奏），星星與互動點保留。
        if (pt.repeat == RepeatMode.Always) { HideTip(); return; }
        _consumed.Add(pt.id);   // 當次停留不復活（RebuildPoints 會跳過）
        if (pt.marker != null) Destroy(pt.marker);
        _points.Remove(pt);
        HideTip();
    }

    static float NearestCellSqr(InteractPoint pt, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < pt.cellCenters.Length; i++)
        {
            float d = (pt.cellCenters[i] - p).sqrMagnitude;
            if (d < best) best = d;
        }
        return best;
    }

    GameObject CreateMarker(Vector2 pos, Color color)
    {
        var go = new GameObject("InteractMarker");
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        var m = go.AddComponent<InteractMarker>();
        m.Configure(markerStarCount, color, sortingLayerName, markerSortingOrder);
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
