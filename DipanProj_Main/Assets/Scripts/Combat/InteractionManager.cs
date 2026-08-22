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
    public Color switchMarkerColor = new Color(0.45f, 1f, 0.85f, 1f);   // 開關/機關：青綠星
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

    /// <summary>
    /// 一種「可按 F 的互動目標」的定義。
    ///
    /// <para>以前 pickup / drama / portal 三種目標的判斷散在三個地方（建點的 if 串、提示文字的三元運算子鏈、
    /// 按 F 的 if/else 鏈），加一種目標要同步改四處，漏改一處是靜默失敗（最典型：F 有效但提示不顯示）。
    /// 現在收成一張表：<b>加一種互動目標 = 在 <see cref="BuildKindRegistry"/> 加一筆</b>，其他地方一行都不用動。</para>
    /// </summary>
    class InteractKind
    {
        public string TypeId;                                   // 對應地圖編輯器的 trigger typeId
        public Color? MarkerColor;                              // null = 不放星星標示
        public System.Func<InteractPoint, TriggerRegion, bool> Setup;   // 讀參數；回 false = 這顆不建（參數無效/條件不符）
        public System.Func<InteractPoint, string> Tip;          // 靠近時的提示文字；回 null = 不顯示
        public System.Action<InteractPoint> Activate;           // 按 F 做什麼
    }

    // 重複規則（編輯器「重複規則」欄；決定同一個互動點多久能再觸發一次）。
    //   Visit（關卡單次，預設/空值）：進這張地圖只觸發一次（當次停留消耗，離圖重進復活）。
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
            case "關卡單次": case "每次進場": case "visit": return RepeatMode.Visit;   // 關卡單次＝進這張圖只觸發一次（舊值「每次進場」相容）
            default: return RepeatMode.Visit;   // 未知/空 → 預設（關卡單次）
        }
    }

    // Cycle/Life 用的自動旗標名（作者不用命名，系統以 trigger id 產生）。Life 加「永久:」前綴 → 存終身旗標。
    static string SeenKey(RepeatMode m, string id)
        => (m == RepeatMode.Life ? TriggerChain.LifePrefix : "") + "已觸發:" + id;

    // 開關「已經跑過一次鏈（setFlag/next）」的自動旗標。刻意用旗標而不是記憶體集合——
    // 記憶體集合換圖就清空，玩家「開→關→去隔壁房→回來→再開」會把整條鏈重播一次。
    // 前綴強制「關卡單次」範圍：這是程式產生的 key、作者沒辦法在旗標登記表登記它，
    // 不加前綴會落到預設的「周目」而寫進存檔 → 之後整個周目按幾次都不會再跑鏈（機關型開關就壞了）。
    // 加了之後每次進新 module 歸零＝「每趟關卡可以再跑一次鏈」，與 toggleFlag 建議的範圍一致。
    static string SwitchFiredKey(string id) => TriggerChain.LevelPrefix + "已開關:" + id;

    /// <summary>一個地圖編輯器放置的互動點（拾取點或劇情點）。</summary>
    class InteractPoint
    {
        public string id;
        public string kindId;           // = 來源 trigger 的 typeId（對應 InteractKind.TypeId）
        // pickup
        public int itemId;
        public int count;
        public bool toRealBag;   // true＝直接進真背包 InventorySystem（不走關卡臨時包）；給起始/教學道具用（如佛燈，撿了要能當場裝備、死亡也保留）
        public string name;
        // drama
        public int dramaId;
        public bool autoTrigger;        // Type 2（頭像對話）：碰到自動觸發、不顯示「按 F」提示
        // portal（傳送門互動）
        public string portalTeleport;   // 要開哪一個 teleport 區域（傳給 ScriptsPanel）
        // switch（開關/機關）
        public string toggleFlag;       // 按 F 要切換的旗標名（開→關→開…）
        public string tipOff, tipOn;    // 提示後綴：旗標未成立時 / 已成立時（例「開始」／「暫停」）
        public bool affectsOthers;      // 這張圖有別的 trigger/地上物在看這個旗標 → 切換後要重建互動點
        // openPanel（靠近按 F 開某個 UI；祭壇抽選用這個）
        public string panelId;          // 要開哪個面板（見 OpenPanelPoint 的分派）
        public string panelArg;         // 傳給面板的參數（祭壇＝抽選池代號）
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
    readonly HashSet<string> _switchWarned = new HashSet<string>();  // 開關的設定防呆警告：同一顆只印一次，別隨每次重建洗版
    Dictionary<string, InteractKind> _kinds;                     // typeId → 互動型別定義（BuildPoints 時建，每幀查）
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
        _switchWarned.Clear();
        _lastMap = map; _lastPickupT = pickupTypeId; _lastDramaT = dramaTypeId;
        BuildPoints();
    }

    /// <summary>重建互動點但**保留**本次停留的已消耗記錄（觸發鏈解鎖/旗標變動後由 MapManager.RefreshTriggers 呼叫）。</summary>
    public void RebuildPoints() => BuildPoints();

    /// <summary>
    /// 建出「有哪幾種可按 F 的目標」的登記表。每次 BuildPoints 重建（很便宜，就幾筆），
    /// 因為 pickup / drama 的 typeId 可由 MapLoader 的 Inspector 欄位覆寫。
    ///
    /// <b>要加一種新的互動目標，只要在這裡加一筆</b>——建點、提示、按 F 三件事都在同一個地方寫完。
    /// </summary>
    Dictionary<string, InteractKind> BuildKindRegistry(MapData map, string pickupT, string dramaT)
    {
        var inv = InventorySystem.Instance;
        var kinds = new List<InteractKind>
        {
            // ── 道具拾取點：撿 itemId × count ──
            new InteractKind
            {
                TypeId = pickupT,
                MarkerColor = pickupMarkerColor,
                Setup = (pt, r) =>
                {
                    if (!int.TryParse(r.GetString("itemId"), out int itemId) || itemId <= 0)
                    {
                        Debug.LogWarning($"[InteractionManager] 拾取點「{r.name}」itemId 無效（{r.GetString("itemId")}），略過。");
                        return false;
                    }
                    var data = inv != null ? inv.GetData(itemId) : null;
                    pt.itemId = itemId;
                    pt.count = Mathf.Max(1, r.GetInt("count", 1));
                    string toReal = r.GetString("toRealBag");
                    pt.toRealBag = toReal == "true" || toReal == "1";   // 直接進真背包（不走臨時包）；起始/教學道具用
                    pt.name = data != null ? data.Name : $"#{itemId}";
                    return true;
                },
                Tip = pt => $"按 {interactKey} 鍵拾取 {pt.name}",
                Activate = CollectPickup,
            },

            // ── 劇情觸發點：Type 1 按 F 開大圖、Type 2 碰到自動播頭像對話 ──
            new InteractKind
            {
                TypeId = dramaT,
                MarkerColor = dramaMarkerColor,
                Setup = (pt, r) =>
                {
                    int dramaId = r.GetInt("dramaId", 0);
                    if (dramaId <= 0)
                    {
                        Debug.LogWarning($"[InteractionManager] 劇情點「{r.name}」dramaId 無效（{r.GetString("dramaId")}），略過。");
                        return false;
                    }
                    pt.dramaId = dramaId;
                    var dd = DramaDatabase.Instance.Get(dramaId);
                    pt.autoTrigger = dd != null && dd.Type == 2;   // Type 2＝碰到自動觸發；Type 1＝按 F
                    return true;
                },
                Tip = _ => $"按 {interactKey} 鍵",   // 劇情點：只提示按鍵
                Activate = TriggerDrama,
            },

            // ── 傳送門互動：開 ScriptsPanel 放劇本 ──
            new InteractKind
            {
                TypeId = TriggerChain.TypePortal,
                MarkerColor = null,   // 傳送門是明顯的地上物、又有新手教學帶，不放星星（免得門裡浮一堆星星很怪）
                Setup = (pt, r) =>
                {
                    // 對應傳送點若已解鎖（門開過了）→ 這個互動點不再出現（不留殘影、不留「按 F」）。
                    var tp = FindRegion(map, r.GetString("linkTeleport"));
                    if (tp != null && !TriggerChain.IsDisabled(tp)) return false;
                    pt.portalTeleport = r.GetString("linkTeleport");
                    return true;
                },
                // 新手教學強制階段(HardLock)時不顯示世界「按 F」浮字（畫面上方已有教學提示，重複又詭異），F 仍可按。
                Tip = _ => Dipan.UI.TutorialManager.HardLock ? null : $"按 {interactKey} 鍵開啟傳送門",
                Activate = OpenPortal,
            },

            // ── 開啟 UI 面板：靠近按 F 開某個介面。祭壇抽選就是用這個（panelId=gacha、arg=池代號）──
            //    刻意做成通用型別而不是「altar 祭壇」專用，之後商店/鐵匠/圖鑑都能共用同一種 trigger。
            new InteractKind
            {
                TypeId = TriggerChain.TypeOpenPanel,
                MarkerColor = null,   // 祭壇/商店本身是明顯的地上物，不放星星
                Setup = (pt, r) =>
                {
                    pt.panelId = r.GetString("panelId").Trim();
                    pt.panelArg = r.GetString("arg").Trim();
                    if (pt.panelId.Length == 0)
                    {
                        Debug.LogWarning($"[InteractionManager] 開啟介面點「{r.name}」沒填 panelId，略過。");
                        return false;
                    }
                    pt.name = r.GetString("tipName").Trim();   // 選填：覆寫提示文字裡的名稱
                    return true;
                },
                Tip = pt => string.IsNullOrEmpty(pt.name) ? $"按 {interactKey} 鍵" : $"按 {interactKey} 鍵{pt.name}",
                Activate = OpenPanelPoint,
            },

            // ── 開關／機關：靠近按 F，切換一個旗標（開→關→開…），不開任何面板 ──
            // 誰在看這個旗標由對方決定：怪物出生點的「條件旗標」、地上物的 appearFlag／disappearFlag、
            // 其他 trigger 的條件旗標…都行。第一次開啟時還會跑自己的「完成寫旗標／接續觸發」（當一般機關用）。
            new InteractKind
            {
                TypeId = TriggerChain.TypeSwitch,
                MarkerColor = switchMarkerColor,
                Setup = (pt, r) =>
                {
                    pt.toggleFlag = r.GetString("toggleFlag").Trim();
                    if (pt.toggleFlag.Length == 0)
                    {
                        Debug.LogWarning($"[InteractionManager] 開關「{r.name}」沒填切換旗標，略過。");
                        return false;
                    }
                    pt.tipOff = r.GetString("tipOff").Trim();
                    pt.tipOn = r.GetString("tipOn").Trim();
                    if (pt.tipOff.Length == 0) pt.tipOff = "開始";
                    // 「已啟動時的提示」留空＝按下去就不能再關（一次性開關）；此時走 repeat 的一般消耗流程。
                    // 一次性開關而旗標已經成立（存檔帶回來、或被別的鏈設過）→ 不建點，
                    // 否則會留一顆沒有提示、按了也沒反應的死星星。
                    if (pt.tipOn.Length == 0 && TriggerChain.FlagTrue(pt.toggleFlag)) return false;

                    // 這張圖有沒有別的互動點在看這個旗標（別顆 trigger 的條件旗標）。
                    // 有才需要在按下去之後重建互動點——重建會把全圖星星砍掉重生（閃爍相位歸零），
                    // 而主用途「開關 → 怪物出生點」完全不需要（出生點是每幀自己輪詢旗標的）。
                    pt.affectsOthers = FlagWatchedByOthers(map, r.id, pt.toggleFlag);

                    // 防呆：切換旗標若同時被某顆「觀察旗標變動」監聽，取消再成立會被當成又一次首次成立 → 那條鏈會重跑。
                    if (map?.TriggerLayer?.regions != null)
                        foreach (var other in map.TriggerLayer.regions)
                            if (other != null && other.GetString("fireOnFlag").Trim() == pt.toggleFlag)
                            {
                                if (_switchWarned.Add(r.id))   // 每次重建都會重跑 Setup，同一顆只唸一次
                                    Debug.LogWarning($"[InteractionManager] 開關「{r.name}」的切換旗標「{pt.toggleFlag}」" +
                                                     $"同時被「{other.name}」(觀察旗標變動) 監聽——每次重新開啟都會再觸發它一次。" +
                                                     "請改用另一個旗標，見 readme/TRIGGER_CHAIN.md §3.6。");
                                break;
                            }
                    return true;
                },
                // 提示文字跟著旗標狀態走：未啟動顯示「按 F 鍵開始」、已啟動顯示「按 F 鍵暫停」。
                Tip = pt => TriggerChain.FlagTrue(pt.toggleFlag)
                            ? (string.IsNullOrEmpty(pt.tipOn) ? null : $"按 {interactKey} 鍵{pt.tipOn}")
                            : $"按 {interactKey} 鍵{pt.tipOff}",
                Activate = SwitchPoint,
            },
        };

        var map2 = new Dictionary<string, InteractKind>();
        foreach (var k in kinds)
        {
            if (string.IsNullOrEmpty(k.TypeId) || map2.ContainsKey(k.TypeId)) continue;
            map2[k.TypeId] = k;
        }
        return map2;
    }

    void BuildPoints()
    {
        ClearPoints();
        var map = _lastMap;
        if (map?.TriggerLayer?.regions == null) return;
        string pickupT = string.IsNullOrEmpty(_lastPickupT) ? "pickup" : _lastPickupT;
        string dramaT = string.IsNullOrEmpty(_lastDramaT) ? "drama" : _lastDramaT;
        _kinds = BuildKindRegistry(map, pickupT, dramaT);   // 快取起來：KindOf 每幀都要用，不能重建

        foreach (var r in map.TriggerLayer.regions)
        {
            if (r.cells == null || r.cells.Count == 0) continue;
            if (!_kinds.TryGetValue(r.typeId, out var kind)) continue;   // 不是可互動的型別
            if (_consumed.Contains(r.id)) continue;      // 本次停留已消耗 → 不復活
            // 關卡進度：本趟已取/已觸發過（跨換圖記憶）→ 重進地圖也不再出現。見 RunProgress。
            if (RunProgress.Exists && RunProgress.Instance.RunActive
                && RunProgress.Instance.IsTriggerConsumed(CurMapId, r.id)) continue;
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
                kindId = r.typeId,
                region = r,
                cellCenters = centers.ToArray(),
                center = center,
                repeat = repeat,
            };

            if (kind.Setup != null && !kind.Setup(pt, r)) continue;   // 參數無效/條件不符 → 這顆不建

            // 星星標示：型別有指定顏色才放。劇情點的 Type 2（碰到自動觸發）是隱形觸發點，也不放。
            if (kind.MarkerColor.HasValue && !pt.autoTrigger)
                pt.marker = CreateMarker(center, kind.MarkerColor.Value);

            _points.Add(pt);
        }
    }

    /// <summary>取某個互動點的型別定義（提示文字與按 F 行為都問它）。找不到回 null。</summary>
    /// <remarks>用 BuildPoints 建好的快取，不要在這裡重建——這支每幀都會被呼叫。</remarks>
    InteractKind KindOf(InteractPoint pt)
    {
        if (pt == null || _kinds == null) return null;
        return _kinds.TryGetValue(pt.kindId, out var k) ? k : null;
    }

    /// <summary>在 pos 放一個掉落物（通用入口，怪物掉落也用這個）。同點重疊會稍微散開避免疊死。
    /// 關卡 run 內會登記進 RunProgress（沒撿的換圖回來原地重放）。</summary>
    public GroundLoot DropLoot(int itemId, int count, Vector2 pos)
    {
        // 掉落＝憑空產生一件新東西 → 一律走工廠，孔數/珠子等級在**掉在地上的那一刻**就決定好。
        var st = ItemManager.Create(itemId, count);
        return DropStack(st, pos);
    }

    /// <summary>把一個「已經存在的」ItemStack 掉在 pos（背包滿溢出、拆解產物…都走這條，不重新骰實例）。</summary>
    public GroundLoot DropStack(ItemStack st, Vector2 pos)
    {
        if (st.IsEmpty) return null;
        var loot = CreateLoot(st.ItemId, st.Count, Scatter(pos), st.Inst);
        if (loot != null && RunProgress.Exists && RunProgress.Instance.RunActive)
        {
            int mapId = MapManager.Instance != null ? MapManager.Instance.CurrentMapId : -1;
            loot.RunDropId = RunProgress.Instance.RegisterGroundDrop(
                mapId, st.ItemId, st.Count, loot.transform.position, st.Inst);
        }
        return loot;
    }

    /// <summary>換圖回來時，依 RunProgress 記錄把本張地圖「還沒撿的掉落物」在原座標重放（不重新登記、沿用原 dropId）。</summary>
    public void RestoreGroundDrops(int mapId)
    {
        if (!RunProgress.Exists) return;
        var drops = RunProgress.Instance.GetGroundDrops(mapId);
        for (int i = 0; i < drops.Count; i++)
        {
            var d = drops[i];
            // 重放的是「同一件」——實例資料原封不動帶回去，孔數不會變。
            var loot = CreateLoot(d.itemId, d.count, new Vector2(d.x, d.y), d.inst);   // 用原座標、不散開
            if (loot != null) loot.RunDropId = d.id;
        }
    }

    /// <summary>純建立一個地上掉落物件（不散開、不登記進度）。DropLoot 與 RestoreGroundDrops 共用。</summary>
    GroundLoot CreateLoot(int itemId, int count, Vector2 spawnPos, ItemInstance inst = null)
    {
        if (count <= 0) return null;
        var inv = InventorySystem.Instance;
        var data = inv != null ? inv.GetData(itemId) : null;
        if (data == null)
        {
            Debug.LogWarning($"[InteractionManager] 掉落物 itemId={itemId} 在 ItemTable 找不到，略過。");
            return null;
        }

        var go = new GameObject($"GroundLoot_{data.Name}");
        go.transform.position = new Vector3(spawnPos.x, spawnPos.y, 0f);
        var loot = go.AddComponent<GroundLoot>();
        loot.Init(new ItemStack { ItemId = itemId, Count = count, Inst = inst },
                  data.Name, lootWorldSize, sortingLayerName, sortingOrder);
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

        // Shift：全域切換地上掉落物「名稱x數量」標籤（預設顯示；與「靠近才顯示的按 F 拾取」互不影響）。
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            GroundLoot.LabelsVisible = !GroundLoot.LabelsVisible;
            for (int i = 0; i < _loot.Count; i++)
                if (_loot[i] != null) _loot[i].SetLabelVisible(GroundLoot.LabelsVisible);
        }

        if (_loot.Count == 0 && _points.Count == 0) { HideTip(); return; }

        // 開 UI（背包/劇情等）時不互動、不顯示提示。
        // 例外：新手教學把玩家定住、逼他按 F 那一刻，雖然輸入被擋，但要放行「按 F 開傳送門」。
        if (UIManager.IsGameplayInputBlocked && !Dipan.UI.TutorialManager.AllowInteract) { HideTip(); return; }

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
                tipText = $"按 {interactKey} 鍵拾取";   // 名稱已由地上常駐標籤顯示，靠近提示只留「按 F 拾取」
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
                var k = KindOf(_points[i]);                     // 提示文字由型別定義決定（回 null = 不顯示）
                tipText = k?.Tip != null ? k.Tip(_points[i]) : $"按 {interactKey} 鍵";
            }
        }

        if (bestLoot == null && bestPoint == null) { HideTip(); return; }

        if (tipText != null)
        {
            if (_tip == null) _tip = PickupTipPanel.Ensure();
            if (_tip != null) _tip.ShowAt(tipPos + Vector3.up * tipHeight, tipText);
        }
        else HideTip();

        if (Input.GetKeyDown(interactKey))
        {
            if (bestLoot != null) { TryPickUpLoot(bestLoot); return; }

            var k = KindOf(bestPoint);                          // 按 F 的行為也由型別定義決定
            if (k?.Activate != null) k.Activate(bestPoint);
            else Debug.LogWarning($"[InteractionManager] 互動點「{bestPoint.id}」的型別「{bestPoint.kindId}」沒有登記按 F 的行為。");
        }
    }

    // 開啟傳送門 UI（不消耗此互動點：關掉 UI 後還能再按 F 重開）。
    void OpenPortal(InteractPoint pt)
    {
        if (pt == null) return;
        HideTip();
        Dipan.UI.ScriptsPanel.OpenFor(pt.portalTeleport);
    }

    // 開啟指定的 UI 面板（不消耗此互動點：關掉 UI 後還能再按 F 重開，祭壇本來就是可以一直回來抽的）。
    // panelId → 面板的對應寫在這裡；之後要接商店/鐵匠/圖鑑就在這個 switch 加一個 case。
    void OpenPanelPoint(InteractPoint pt)
    {
        if (pt == null) return;
        HideTip();
        switch (pt.panelId)
        {
            case "gacha":   // 祭壇抽選：arg = GachaPoolTable.csv 的 PoolId
                Dipan.UI.GachaPanel.OpenFor(pt.panelArg);
                break;
            default:
                Debug.LogWarning($"[InteractionManager] 開啟介面點「{pt.id}」的 panelId=「{pt.panelId}」沒有對應的面板。" +
                                 "可用值目前只有 gacha；要加新的請到 InteractionManager.OpenPanelPoint 補一個 case。");
                break;
        }
    }

    // 開關／機關：切換「切換旗標」的成立狀態。
    //   ‧ 第一次切成「開」時，順便跑一次自己的鏈（完成寫旗標／接續觸發），這樣它也能當一般機關用（開門、播對話…）。
    //   ‧ 之後的每一次切換只動旗標，不再重跑鏈——不然「暫停→恢復」會把後面的劇情重播一次。
    //   ‧ 有填「已啟動時的提示」＝可反覆切換的開關（互動點不消耗）；留空＝一次性開關，走 repeat 的一般消耗流程。
    void SwitchPoint(InteractPoint pt)
    {
        if (pt == null) return;

        bool on = TriggerChain.FlagTrue(pt.toggleFlag);
        if (on)
        {
            if (string.IsNullOrEmpty(pt.tipOn))
            {
                // 一次性開關而旗標已成立（在建點之後被別的鏈設起來的）→ 這顆已經沒用了，重建把它清掉。
                if (MapManager.Instance != null) MapManager.Instance.RefreshTriggers();
                return;
            }
            TriggerChain.ClearFlag(pt.toggleFlag);
        }
        else TriggerChain.SetFlag(pt.toggleFlag);

        // ⚠ 順序很重要：先消耗自己，再跑鏈。反過來的話 OnCompleted 內的 setFlag 會同步觸發 RefreshTriggers
        // → 互動點整批重建（此時本點還沒進 _consumed，於是被原樣建回來＋長出新星星），
        // 之後的 ConsumePoint 拿的是舊實例，新的那顆就變成一顆按不動的死星星（同 CollectPickup 的寫法）。
        if (string.IsNullOrEmpty(pt.tipOn))                    // 一次性開關：按完就收掉星星與提示
        {
            MarkRepeatSeen(pt);                                // 重複規則「每周目/永久」才有效的自動旗標
            ConsumePoint(pt);
        }
        else HideTip();                                        // 可切換：提示下一幀會依新狀態重新顯示

        // 第一次開啟才跑自己的鏈（setFlag/next）；之後的「暫停→恢復」只動旗標，不然後面的劇情會重播。
        string firedKey = SwitchFiredKey(pt.id);
        if (!on && !TriggerChain.FlagTrue(firedKey))
        {
            TriggerChain.SetFlag(firedKey);
            TriggerChain.OnCompleted(pt.region);
        }

        // 旗標變了 → 只有「這張圖真的有東西在看它」時才重建互動點（重建＝全圖星星砍掉重生，
        // 反覆按的開關會很明顯）。SetFlag/ClearFlag 本身不重建，OnCompleted 也只在填了 setFlag 時才重建。
        if (pt.affectsOthers && MapManager.Instance != null) MapManager.Instance.RefreshTriggers();
    }

    /// <summary>
    /// 這張圖有沒有「別的互動點」在看某個旗標（別顆 trigger 的條件旗標 `requireFlag`，可能帶 "!" 否定前綴）。
    /// 給開關判斷「切換之後要不要重建互動點」用——不用重建就別重建，重建會把全圖星星砍掉重生（閃爍相位歸零）。
    /// 刻意只看 `requireFlag`：`RefreshTriggers` 就只重建互動點，其他看旗標的東西都不靠它——
    /// 地上物的出現/消失是 `MapObjectRevealer` 走事件、出生點的條件是每幀輪詢 `TriggerChain.IsActive`、傳送點是每幀動態判定。
    /// </summary>
    static bool FlagWatchedByOthers(MapData map, string selfId, string flag)
    {
        if (map?.TriggerLayer?.regions == null || string.IsNullOrEmpty(flag)) return false;
        foreach (var r in map.TriggerLayer.regions)
        {
            if (r == null || r.id == selfId) continue;
            string req = r.GetString("requireFlag").Trim();
            if (req.StartsWith("!")) req = req.Substring(1).Trim();   // 「沒有這個旗標」也是在看它
            if (req == flag) return true;
        }
        return false;
    }

    // 依名字找同地圖的一個 trigger region（給傳送門互動點查它連動的傳送點狀態用）。
    static TriggerRegion FindRegion(MapData map, string name)
    {
        if (map?.TriggerLayer?.regions == null || string.IsNullOrEmpty(name)) return null;
        foreach (var r in map.TriggerLayer.regions)
            if (r.name == name) return r;
        return null;
    }

    // ── 新手教學用 ──
    /// <summary>當前地圖若有傳送門互動點，回傳其中心世界座標（給教學拉鏡頭用）。</summary>
    public bool TryGetPortalWorld(out Vector2 center)
    {
        for (int i = 0; i < _points.Count; i++)
            if (_points[i].kindId == TriggerChain.TypePortal) { center = _points[i].center; return true; }
        center = default; return false;
    }

    /// <summary>玩家是否已走到「可按 F 開傳送門」的範圍（給教學判斷何時進入強制按 F）。</summary>
    public bool PlayerNearPortal()
    {
        if (_player == null) _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (_player == null) return false;
        Vector2 p = _player.position;
        float r2 = pickupRadius * pickupRadius;
        for (int i = 0; i < _points.Count; i++)
            if (_points[i].kindId == TriggerChain.TypePortal && NearestCellSqr(_points[i], p) <= r2) return true;
        return false;
    }

    /// <summary>當前地圖若有某 itemId 的拾取點，回傳其中心世界座標（給教學手指指向佛燈用）。</summary>
    public bool TryGetPickupWorld(int itemId, out Vector2 center)
    {
        for (int i = 0; i < _points.Count; i++)
            if (_points[i].kindId == PickupTypeId && _points[i].itemId == itemId) { center = _points[i].center; return true; }
        center = default; return false;
    }

    /// <summary>玩家是否已走到某 itemId 拾取點「可按 F 拾取」的範圍（給教學判斷何時進入強制按 F）。</summary>
    public bool PlayerNearPickup(int itemId)
    {
        if (_player == null) _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (_player == null) return false;
        Vector2 p = _player.position;
        float r2 = pickupRadius * pickupRadius;
        for (int i = 0; i < _points.Count; i++)
            if (_points[i].kindId == PickupTypeId && _points[i].itemId == itemId && NearestCellSqr(_points[i], p) <= r2) return true;
        return false;
    }

    // 目前這張圖使用的 pickup typeId（可由 MapLoader 的 Inspector 欄位覆寫，預設 "pickup"）。
    string PickupTypeId => string.IsNullOrEmpty(_lastPickupT) ? TriggerChain.TypePickup : _lastPickupT;

    // 目前地圖 id（給關卡進度記錄用；沒有 MapManager 時 -1）。
    static int CurMapId => MapManager.Instance != null ? MapManager.Instance.CurrentMapId : -1;

    // 取得物品的統一入口：關卡內進臨時包（RunProgress，回 0）、廣場進真背包（回放不下的剩餘）。
    static int GiveToPlayer(int itemId, int count) => RunProgress.Instance.GiveItem(itemId, count);

    // 從地上撿（部分撿取：吃得下多少算多少，剩的留在地上）。關卡內進臨時包、廣場進真背包。
    void TryPickUpLoot(GroundLoot loot)
    {
        if (loot == null) return;

        // ⚠ 撿的是「地上那一件」，不是重新生一件——所以走 GiveStack 把實例原封不動帶走。
        int leftover = RunProgress.Instance.GiveStack(loot.ToStack());
        int added = loot.Count - leftover;

        if (added <= 0) { AlertPanel.Toast("背包已滿"); return; }

        AlertPanel.Toast(added > 1 ? $"獲得 {loot.DisplayName} ×{added}" : $"獲得 {loot.DisplayName}");

        if (leftover > 0)
        {
            loot.SetCount(leftover);
            if (RunProgress.Exists) RunProgress.Instance.UpdateGroundDropCount(CurMapId, loot.RunDropId, leftover);
        }
        else
        {
            if (RunProgress.Exists) RunProgress.Instance.RemoveGroundDrop(CurMapId, loot.RunDropId);
            _loot.Remove(loot); Destroy(loot.gameObject); HideTip();
        }
    }

    // 撿拾取點（一律消耗該點：吃得下的進背包/臨時包、剩的掉腳下變成地上掉落物）。
    void CollectPickup(InteractPoint pt)
    {
        if (pt == null) return;

        // toRealBag＝直接進真背包（起始/教學道具，如佛燈：撿了要能當場開背包裝備、死亡也不丟）；否則走既有規則（關卡內臨時包/廣場真背包）。
        // toRealBag 是給起始/教學道具用的「直接進真背包」捷徑，但金錢例外：
        // 金錢是獨立數字、不佔背包格，一律回到統一入口處理。
        // 拾取點＝憑空產生一件新東西 → 走工廠，需要孔位/等級的物品會在這裡骰好（每一件各自骰）。
        bool toReal = pt.toRealBag && pt.itemId != RunProgress.MoneyItemId;
        var inv0 = InventorySystem.Instance;
        var d0 = inv0 != null ? inv0.GetData(pt.itemId) : null;
        int leftover = 0;
        if (d0 != null && ItemManager.NeedsInstance(d0))
        {
            for (int i = 0; i < pt.count; i++)
            {
                var one = ItemManager.Create(pt.itemId, 1);
                leftover += toReal ? (inv0 != null ? inv0.AddStack(one) : 1)
                                   : RunProgress.Instance.GiveStack(one);
            }
        }
        else
        {
            leftover = toReal ? (inv0 != null ? inv0.AddItem(pt.itemId, pt.count) : pt.count)
                              : GiveToPlayer(pt.itemId, pt.count);
        }
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
            // 「可略過」欄（canSkip）：預設允許；只有一句的群組不會顯示 Skip。見 readme/DRAMA.md。
            DramaTalkController.Play(data.TalkGroup, pt.region == null || pt.region.GetBool("canSkip", true));
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
        // 關卡進度：本趟已取/已觸發（跨換圖記憶）→ 重進地圖也不再出現。非 run 期間由 RunProgress 內部忽略。
        if (RunProgress.Exists) RunProgress.Instance.MarkTriggerConsumed(CurMapId, pt.id);
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
