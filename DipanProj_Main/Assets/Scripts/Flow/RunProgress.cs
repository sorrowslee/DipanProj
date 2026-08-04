using System.Collections.Generic;
using UnityEngine;
using Dipan.Inventory;
using Dipan.Save;

/// <summary>
/// 「一趟關卡」的進度與臨時包（純記憶體、跨換圖常駐單例；死亡/離開/進新關卡即清）。
///
/// 對應設計：readme/CORE_LOOP_DESIGN.md §6（關卡內收穫暫存、通關才落袋、死亡/離開歸零）
/// ＋ readme/MAP_SYSTEM.md 的 Phase 2（地圖狀態持久化：怪清掉不復生、掉落物/已取事件保留）。
///
/// 三大職責：
///   1. **臨時包**：關卡內取得的道具/金錢（金錢＝銅錢道具 <see cref="MoneyItemId"/>）先進這裡，
///      通關才 <see cref="SettleIntoBag"/> 併進真背包（InventorySystem），死亡/返回則 <see cref="EndRunDiscard"/> 丟棄。
///   2. **關卡進度（per-map、限本趟）**：記「哪些出生點的怪已清」「哪些 trigger 已取/已觸發」「哪些掉落物還沒撿（留原地）」，
///      換地圖回來時 MapLoader / InteractionManager 據此跳過重生、原地重放掉落物。
///   3. **工程介面**：按 F8 開/關一個除錯疊層，直接看臨時包內容（正式背包 UI 之前頂著用）。
///
/// 「在關卡內」＝ <see cref="RunActive"/>：由 MapManager 進入非廣場 module 時 <see cref="BeginRun"/>、
/// 結束關卡時結算/丟棄。廣場（Hub module）取得的東西照舊直接進真背包，不走臨時包。
/// </summary>
public class RunProgress : MonoBehaviour
{
    /// <summary>
    /// 金錢＝銅錢道具（ItemTable ID 101）。掉落與關卡內臨時包仍然「當成道具」處理（才能沿用掉落物、
    /// 撿取、結算那一整套），但**落袋時會轉成獨立的金錢數字**，不佔背包格——見 <see cref="GiveItem"/>
    /// 與 <see cref="SettleIntoBag"/>。ItemTable 的這一列仍然要保留（圖示與名稱還要用）。
    /// </summary>
    public const int MoneyItemId = 101;

    static RunProgress _instance;
    public static bool Exists => _instance != null;
    public static RunProgress Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<RunProgress>();
                if (_instance == null)
                {
                    var go = new GameObject("[RunProgress]");
                    _instance = go.AddComponent<RunProgress>();
                }
            }
            return _instance;
        }
    }

    /// <summary>目前是否在一趟關卡內（true = 取得的東西進臨時包）。廣場/教學為 false。</summary>
    public bool RunActive { get; private set; }
    /// <summary>本趟關卡的 module（BeginRun 時記下；用來判斷「同一趟」不重置進度）。</summary>
    public string RunModule { get; private set; }

    // 臨時包：itemId → 數量（金錢也是道具 101）。無容量上限（併入真背包時才受背包格數限制）。
    // ⚠ 只裝「可疊的一般道具」——裝備與能力珠每一件都有自己的孔位/等級，不能用「ID + 數量」表示，
    //    它們放在下面的 _tempInstanced，一件一筆。見 readme/GEM_SOCKET.md。
    readonly Dictionary<int, int> _temp = new Dictionary<int, int>();

    // 臨時包（有實例的部分）：裝備、能力珠…一件一筆，含孔位與珠子等級。
    // 這樣玩家在關卡中途按 F8 或打開結算就看得到「這把是 6 孔的」——掉落當下就已經決定好了。
    readonly List<ItemStack> _tempInstanced = new List<ItemStack>();

    /// <summary>一筆躺在地上、還沒被撿走的掉落物（換圖回來要在原座標重放）。</summary>
    public class GroundDrop
    {
        public int id;      // 本趟唯一（RunProgress 發號）
        public int itemId;
        public int count;
        public float x, y;  // 世界座標（已散開後的實際落點）
        /// <summary>這一件專屬的資料（孔位/珠子等級）；null = 一般可疊道具。掉在地上時就已經決定了。</summary>
        public ItemInstance inst;
    }

    // 每張地圖一筆記錄（只在本趟有效）。
    class MapRec
    {
        public readonly HashSet<string> killedSpawns = new HashSet<string>();   // 已清的怪出生點 key
        public readonly HashSet<string> consumedTriggers = new HashSet<string>(); // 已取/已觸發的 trigger id
        public readonly HashSet<string> destroyedObjects = new HashSet<string>(); // 已破壞的地上物 key
        public readonly Dictionary<int, GroundDrop> drops = new Dictionary<int, GroundDrop>(); // 未撿的掉落物
    }
    readonly Dictionary<int, MapRec> _maps = new Dictionary<int, MapRec>();
    int _nextDropId = 1;

    bool _showDebug;   // F8 除錯疊層

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    MapRec Rec(int mapId)
    {
        if (!_maps.TryGetValue(mapId, out var m)) { m = new MapRec(); _maps[mapId] = m; }
        return m;
    }

    // ───────────────────────── 一趟關卡的生命週期 ─────────────────────────

    /// <summary>進入非廣場 module 時由 MapManager 呼叫：依 module 判斷要不要開新的一趟（換了關卡才重置進度）。</summary>
    public void OnEnterModule(string module)
    {
        bool isLevel = IsLevelModule(module);
        if (isLevel)
        {
            if (!RunActive || RunModule != module) BeginRun(module);
        }
        else
        {
            // 進入廣場/教學：理論上結算已把 run 收掉，這裡保險再清一次殘留（避免臨時包髒資料被帶回）。
            if (RunActive) EndRunDiscard();
        }
    }

    /// <summary>module 是不是「關卡」（＝非廣場/教學）。廣場與教學洞窟同屬 Hub module，取得的東西直接進真背包。</summary>
    public static bool IsLevelModule(string module)
        => !string.IsNullOrEmpty(module) && module != SaveConstants.HubModule;

    /// <summary>開始新的一趟關卡：清空臨時包與所有進度記錄。</summary>
    public void BeginRun(string module)
    {
        _temp.Clear();
        _tempInstanced.Clear();
        _maps.Clear();
        _nextDropId = 1;
        RunActive = true;
        RunModule = module;
        Debug.Log($"[RunProgress] 開始一趟關卡：{module}（臨時包已清空）。");
    }

    /// <summary>死亡/主動返回：臨時包與進度全部丟棄（這趟零收穫）。</summary>
    public void EndRunDiscard()
    {
        int lost = _temp.Count + _tempInstanced.Count;
        _temp.Clear();
        _tempInstanced.Clear();
        _maps.Clear();
        RunActive = false;
        RunModule = null;
        if (lost > 0) Debug.Log("[RunProgress] 未過關 → 臨時包內容全部丟棄。");
    }

    /// <summary>
    /// 過關結算：把臨時包整包併入真背包（InventorySystem），清空臨時包與進度，結束這趟。
    /// 回傳併入前的內容快照供結算畫面顯示（含每一件的孔位/鑲嵌，所以結算畫面能標「6 孔」）。
    /// </summary>
    public List<ItemStack> SettleIntoBag()
    {
        var snapshot = TempSnapshot();

        var inv = InventorySystem.Instance;
        var sm = Dipan.Save.SaveManager.Instance;
        foreach (var st in snapshot)
        {
            // 金錢走錢包（獨立數字），不佔背包格、也不會因為背包滿而掉。
            if (st.ItemId == MoneyItemId)
            {
                if (sm != null) sm.AddCurrency(st.Count);
                else Debug.LogWarning($"[RunProgress] 沒有 SaveManager，結算的金錢 {st.Count} 無處可放。");
                continue;
            }
            if (inv == null) continue;
            int leftover = inv.AddStack(st);
            if (leftover > 0)
                Debug.LogWarning($"[RunProgress] 結算時背包已滿，道具 {st.ItemId} ×{leftover} 無法放入（暫時捨棄）。");
        }

        _temp.Clear();
        _tempInstanced.Clear();
        _maps.Clear();
        RunActive = false;
        RunModule = null;
        return snapshot;
    }

    // ───────────────────────── 取得物品（關卡內→臨時包；廣場→真背包）─────────────────────────

    /// <summary>
    /// 給玩家 itemId×count：關卡內進臨時包（無上限，恆回 0）；廣場走真背包（回傳放不下的剩餘）。
    /// giveItem trigger、拾取點、地上掉落物撿取都走這裡，統一「在關卡內就進臨時包」的規則。
    ///
    /// 需要實例資料的物品（裝備、能力珠）會在這裡先經 <see cref="ItemManager"/> 產生完整的一件——
    /// 所以孔數與珠子等級在「取得的當下」就決定好了，不是等到結算才骰。
    /// </summary>
    public int GiveItem(int itemId, int count)
    {
        if (count <= 0) return 0;

        var inv0 = InventorySystem.Instance;
        var d = inv0 != null ? inv0.GetData(itemId) : null;
        if (d != null && ItemManager.NeedsInstance(d))
        {
            int left = 0;
            for (int i = 0; i < count; i++) left += GiveStack(ItemManager.Create(itemId, 1));
            return left;
        }

        return GiveStack(new ItemStack { ItemId = itemId, Count = count, Inst = null });
    }

    /// <summary>
    /// 把一個「已經存在的」ItemStack 交給玩家（不重新產生實例）。
    /// 地上掉落物撿取走這條——地上那一件的孔數是掉落當下就決定的，撿起來不能重骰。
    /// </summary>
    public int GiveStack(ItemStack st)
    {
        if (st.IsEmpty) return 0;

        if (RunActive)
        {
            if (st.HasInst) _tempInstanced.Add(st);
            else
            {
                _temp.TryGetValue(st.ItemId, out int c);
                _temp[st.ItemId] = c + st.Count;
            }
            return 0;
        }

        // 金錢不進背包（改成獨立數字顯示在背包下方）。這裡是「取得物品的統一入口」，
        // 所以攔在這一層，掉落物、觸發鏈 giveItem、抽選發獎…全部自動適用。
        if (st.ItemId == MoneyItemId)
        {
            var sm = Dipan.Save.SaveManager.Instance;
            if (sm != null) { sm.AddCurrency(st.Count); return 0; }
            Debug.LogWarning("[RunProgress] 沒有 SaveManager，金錢無處可放（本次遊玩不會保存）。");
            return st.Count;
        }

        var inv = InventorySystem.Instance;
        return inv != null ? inv.AddStack(st) : st.Count;
    }

    /// <summary>臨時包目前內容的快照（可疊的合併成一筆、有實例的一件一筆，依物品 ID 排序）。</summary>
    public List<ItemStack> TempSnapshot()
    {
        var list = new List<ItemStack>();
        foreach (var kv in _temp) list.Add(new ItemStack { ItemId = kv.Key, Count = kv.Value, Inst = null });
        list.AddRange(_tempInstanced);
        list.Sort((a, b) => a.ItemId.CompareTo(b.ItemId));
        return list;
    }

    /// <summary>臨時包裡可疊道具的部分（唯讀）。完整內容請用 <see cref="TempSnapshot"/>。</summary>
    public IReadOnlyDictionary<int, int> TempItems => _temp;

    /// <summary>臨時包目前有幾筆（可疊的算一筆、有實例的一件一筆）。</summary>
    public int TempCount => _temp.Count + _tempInstanced.Count;

    // ───────────────────────── 怪物：已清出生點 ─────────────────────────

    public bool IsSpawnKilled(int mapId, string spawnKey)
        => !string.IsNullOrEmpty(spawnKey) && _maps.TryGetValue(mapId, out var m) && m.killedSpawns.Contains(spawnKey);

    /// <summary>記某出生點的怪已清（本趟不再重生）。非 run 期間忽略。</summary>
    public void MarkSpawnKilled(int mapId, string spawnKey)
    {
        if (!RunActive || string.IsNullOrEmpty(spawnKey)) return;
        Rec(mapId).killedSpawns.Add(spawnKey);
    }

    // ───────────────────────── 地上物：已破壞 ─────────────────────────

    public bool IsObjectDestroyed(int mapId, string objKey)
        => !string.IsNullOrEmpty(objKey) && _maps.TryGetValue(mapId, out var m) && m.destroyedObjects.Contains(objKey);

    /// <summary>記某地上物本趟已破壞（換圖回來不再重建）。非 run 期間忽略。</summary>
    public void MarkObjectDestroyed(int mapId, string objKey)
    {
        if (!RunActive || string.IsNullOrEmpty(objKey)) return;
        Rec(mapId).destroyedObjects.Add(objKey);
    }

    // ───────────────────────── trigger：已取/已觸發 ─────────────────────────

    public bool IsTriggerConsumed(int mapId, string triggerId)
        => !string.IsNullOrEmpty(triggerId) && _maps.TryGetValue(mapId, out var m) && m.consumedTriggers.Contains(triggerId);

    /// <summary>記某 trigger 本趟已取/已觸發（給物品、拾取點、劇情點；重進地圖不再出現）。非 run 期間忽略。</summary>
    public void MarkTriggerConsumed(int mapId, string triggerId)
    {
        if (!RunActive || string.IsNullOrEmpty(triggerId)) return;
        Rec(mapId).consumedTriggers.Add(triggerId);
    }

    // ───────────────────────── 地上掉落物（沒撿的留原地）─────────────────────────

    /// <summary>
    /// 登記一筆地上掉落物，回傳 dropId（≥1）。非 run 期間回 0（不登記，例如廣場背包溢出的掉落）。
    /// inst = 這一件的實例資料（孔位/珠子等級），換圖回來重放時原封不動放回去。
    /// </summary>
    public int RegisterGroundDrop(int mapId, int itemId, int count, Vector2 pos, ItemInstance inst = null)
    {
        if (!RunActive || count <= 0) return 0;
        int id = _nextDropId++;
        Rec(mapId).drops[id] = new GroundDrop { id = id, itemId = itemId, count = count, x = pos.x, y = pos.y, inst = inst };
        return id;
    }

    /// <summary>部分撿取後更新剩餘數量（≤0 = 移除該筆）。</summary>
    public void UpdateGroundDropCount(int mapId, int dropId, int newCount)
    {
        if (dropId <= 0 || !_maps.TryGetValue(mapId, out var m)) return;
        if (m.drops.TryGetValue(dropId, out var d))
        {
            if (newCount <= 0) m.drops.Remove(dropId);
            else d.count = newCount;
        }
    }

    /// <summary>撿光後移除該筆掉落物記錄。</summary>
    public void RemoveGroundDrop(int mapId, int dropId)
    {
        if (dropId <= 0 || !_maps.TryGetValue(mapId, out var m)) return;
        m.drops.Remove(dropId);
    }

    /// <summary>取某張地圖目前未撿的掉落物（換圖回來重放用）。</summary>
    public List<GroundDrop> GetGroundDrops(int mapId)
    {
        var list = new List<GroundDrop>();
        if (_maps.TryGetValue(mapId, out var m)) list.AddRange(m.drops.Values);
        return list;
    }

    // ───────────────────────── 工程介面（F8 除錯疊層）─────────────────────────

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8)) _showDebug = !_showDebug;
    }

    void OnGUI()
    {
        if (!_showDebug) return;

        const float w = 320f;
        float h = 96f + TempCount * 22f;
        var rect = new Rect(12f, 12f, w, h);
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
        float y = 18f;
        GUI.Label(new Rect(24f, y, w - 24f, 22f), "<b>[F8] 臨時包（工程介面）</b>", style); y += 24f;
        GUI.Label(new Rect(24f, y, w - 24f, 22f),
            RunActive ? $"關卡中：{RunModule}" : "不在關卡內（進真背包）", style); y += 22f;

        var inv = InventorySystem.Instance;
        var snap = TempSnapshot();
        if (snap.Count == 0)
        {
            GUI.Label(new Rect(24f, y, w - 24f, 22f), "（空）", style); y += 22f;
        }
        else
        {
            foreach (var st in snap)
            {
                var d = inv != null ? inv.GetData(st.ItemId) : null;
                string nm = d != null ? d.Name : $"#{st.ItemId}";
                // 有孔的裝備順便標出「幾孔／鑲了幾顆」，中途按 F8 就看得到這趟打到什麼好貨。
                string extra = "";
                if (st.Inst != null)
                {
                    if (st.Inst.HasSockets && st.Inst.UnlockedCount > 0)
                        extra = $"  <color=#FFD479>({st.Inst.UnlockedCount}孔 / 鑲{st.Inst.GemCount})</color>";
                    else if (st.Inst.level > 0)
                        extra = $"  <color=#8FD3FF>Lv{st.Inst.level}</color>";
                }
                GUI.Label(new Rect(24f, y, w - 24f, 22f), $"{nm} ×{st.Count}{extra}", style);
                y += 22f;
            }
        }
    }
}
