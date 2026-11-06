using System.Collections.Generic;
using UnityEngine;
using Dipan.Inventory;
using Dipan.Save;

/// <summary>
/// 「一趟關卡」的進度（純記憶體、跨換圖常駐單例；離開關卡/進新關卡即清）＋「取得物品的統一入口」。
///
/// 對應設計：readme/CORE_LOOP_DESIGN.md §6 ＋ readme/MAP_SYSTEM.md 的 Phase 2
/// （地圖狀態持久化：怪清掉不復生、掉落物/血球/已取事件保留）。
///
/// ⚠ **2026-11-06 起沒有臨時包了**（作者拍板）：關卡內撿到的東西**直接進真背包**，死亡也保留。
///    原本的「通關才落袋、死亡歸零」整套拿掉；見 readme/RUN_PROGRESS.md。
///
/// 職責：
///   1. **取得物品的統一入口** <see cref="GiveItem"/>／<see cref="GiveStack"/>：一律進真背包（銅錢轉金錢數字）。
///   2. **關卡進度（per-map、限本趟）**：記「哪些出生點的怪已清」「哪些 trigger 已取/已觸發」「哪些掉落物／血球還沒撿（留原地）」，
///      換地圖回來時 MapLoader / InteractionManager 據此跳過重生、原地重放。
///   3. **工程介面**：按 F8 開/關一個除錯疊層，看目前 run 狀態。
///
/// 「在關卡內」＝ <see cref="RunActive"/>：由 MapManager 進入非廣場 module 時 <see cref="BeginRun"/>、
/// 結束關卡（過關/死亡/返回）時 <see cref="EndRun"/>。
/// </summary>
public class RunProgress : MonoBehaviour
{
    /// <summary>
    /// 金錢＝銅錢道具（ItemTable ID 101）。掉落時仍然「當成道具」處理（才能沿用掉落物、撿取那一整套），
    /// 但**取得時會轉成獨立的金錢數字**，不佔背包格——見 <see cref="GiveStack"/>。
    /// ItemTable 的這一列仍然要保留（圖示與名稱還要用）。
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

    /// <summary>目前是否在一趟關卡內（true = 記關卡進度：怪不復生、掉落物/血球保留）。廣場/教學為 false。</summary>
    public bool RunActive { get; private set; }
    /// <summary>本趟關卡的 module（BeginRun 時記下；用來判斷「同一趟」不重置進度）。</summary>
    public string RunModule { get; private set; }

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
        public readonly Dictionary<int, Vector2> orbs = new Dictionary<int, Vector2>();         // 未吸收的血球（id → 世界座標）
        public bool hadLootKill;        // 本趟在這張圖殺過 DropTableId>0 的怪（清場血球的資格）
        public readonly HashSet<string> battlesGiven = new HashSet<string>();   // 本趟已噴過清場血球的戰鬥鏈 key（見 MapClearReward）
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

    /// <summary>
    /// 跨 module 換圖時由 MapManager 呼叫：依 module 判斷要不要開新的一趟（換了關卡才重置進度）。
    /// 回傳 true ＝這次**開了新的一趟**（目前沒有呼叫端用到，留著給之後「邪佛關卡倒數」之類的掛點）。
    /// </summary>
    public bool OnEnterModule(string module)
    {
        bool isLevel = IsLevelModule(module);
        if (isLevel)
        {
            if (!RunActive || RunModule != module) { BeginRun(module); return true; }
        }
        else
        {
            // 進入廣場/教學：理論上結束關卡時已把 run 收掉，這裡保險再清一次殘留。
            if (RunActive) EndRun();
        }
        return false;
    }

    /// <summary>module 是不是「關卡」（＝非廣場/教學）。廣場與教學洞窟同屬 Hub module，取得的東西直接進真背包。</summary>
    public static bool IsLevelModule(string module)
        => !string.IsNullOrEmpty(module) && module != SaveConstants.HubModule;

    /// <summary>開始新的一趟關卡：清空所有進度記錄（已清怪、已取物、掉落物、血球）。</summary>
    public void BeginRun(string module)
    {
        _maps.Clear();
        _nextDropId = 1;
        RunActive = true;
        RunModule = module;
        Debug.Log($"[RunProgress] 開始一趟關卡：{module}（進度已重置）。");
    }

    /// <summary>
    /// 結束這趟關卡（過關／死亡／返回一律同一支）：清掉本趟進度。
    /// 撿到的東西早就在真背包裡了，這裡**不動背包**——沒有臨時包可以結算或丟棄（2026-11-06 起）。
    /// </summary>
    public void EndRun()
    {
        _maps.Clear();
        RunActive = false;
        RunModule = null;
    }

    // ───────────────────────── 取得物品（一律進真背包）─────────────────────────

    /// <summary>
    /// 給玩家 itemId×count：一律進真背包，回傳放不下的剩餘（呼叫端自己決定要不要掉在地上）。
    /// giveItem trigger、拾取點、地上掉落物撿取、抽選發獎、作弊面板都走這裡。
    ///
    /// 需要實例資料的物品（裝備、能力珠）會在這裡先經 <see cref="ItemManager"/> 產生完整的一件——
    /// 所以孔數與珠子等級在「取得的當下」就決定好了。
    ///
    /// <paramref name="toRealBag"/>：**已無作用**（2026-11-06 拿掉臨時包後，所有來源本來就直接進真背包）。
    /// 參數保留只是為了不動到既有呼叫端與編輯器的拾取點 toRealBag 欄。
    /// </summary>
    public int GiveItem(int itemId, int count, bool toRealBag = false)
    {
        if (count <= 0) return 0;

        var inv0 = InventorySystem.Instance;
        var d = inv0 != null ? inv0.GetData(itemId) : null;
        if (d != null && ItemManager.NeedsInstance(d))
        {
            int left = 0;
            for (int i = 0; i < count; i++) left += GiveStack(ItemManager.Create(itemId, 1), toRealBag);
            return left;
        }

        return GiveStack(new ItemStack { ItemId = itemId, Count = count, Inst = null }, toRealBag);
    }

    /// <summary>
    /// 把一個「已經存在的」ItemStack 交給玩家（不重新產生實例）。
    /// 地上掉落物撿取走這條——地上那一件的孔數是掉落當下就決定的，撿起來不能重骰。
    /// <paramref name="toRealBag"/> 已無作用（見 <see cref="GiveItem"/>）。
    /// </summary>
    public int GiveStack(ItemStack st, bool toRealBag = false)
    {
        if (st.IsEmpty) return 0;

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

    // ───────────────────────── 血球（沒吸的留原地）─────────────────────────

    /// <summary>登記一顆地上血球，回傳 id（≥1）。非 run 期間回 0（不登記）。見 <see cref="BloodOrb"/>。</summary>
    public int RegisterBloodOrb(int mapId, Vector2 pos)
    {
        if (!RunActive) return 0;
        int id = _nextDropId++;   // 跟掉落物共用流水號，本趟唯一即可
        Rec(mapId).orbs[id] = pos;
        return id;
    }

    /// <summary>血球被吸收後移除記錄。</summary>
    public void RemoveBloodOrb(int mapId, int orbId)
    {
        if (orbId <= 0 || !_maps.TryGetValue(mapId, out var m)) return;
        m.orbs.Remove(orbId);
    }

    /// <summary>取某張地圖目前還沒被吸收的血球（id, 座標），換圖回來重放用。</summary>
    public List<KeyValuePair<int, Vector2>> GetBloodOrbs(int mapId)
    {
        var list = new List<KeyValuePair<int, Vector2>>();
        if (_maps.TryGetValue(mapId, out var m)) list.AddRange(m.orbs);
        return list;
    }

    // ───────────────────────── 清場血球（見 MapClearReward）─────────────────────────

    public void MarkLootKill(int mapId) { if (RunActive) Rec(mapId).hadLootKill = true; }
    public bool HadLootKill(int mapId) => _maps.TryGetValue(mapId, out var m) && m.hadLootKill;
    public bool IsBattleGiven(int mapId, string battleKey) => _maps.TryGetValue(mapId, out var m) && m.battlesGiven.Contains(battleKey);
    public void MarkBattleGiven(int mapId, string battleKey) { if (RunActive) Rec(mapId).battlesGiven.Add(battleKey); }

    // ───────────────────────── 工程介面（F8 除錯疊層）─────────────────────────

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8)) _showDebug = !_showDebug;
    }

    void OnGUI()
    {
        if (!_showDebug) return;

        const float w = 360f;
        float h = 64f + _maps.Count * 22f;
        var rect = new Rect(12f, 12f, w, h);
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
        float y = 18f;
        GUI.Label(new Rect(24f, y, w - 24f, 22f), "<b>[F8] 關卡進度（工程介面）</b>", style); y += 24f;
        GUI.Label(new Rect(24f, y, w - 24f, 22f),
            RunActive ? $"關卡中：{RunModule}" : "不在關卡內", style); y += 22f;

        foreach (var kv in _maps)
        {
            var m = kv.Value;
            GUI.Label(new Rect(24f, y, w - 24f, 22f),
                $"地圖 #{kv.Key}：清怪 {m.killedSpawns.Count}／掉落物 {m.drops.Count}／血球 {m.orbs.Count}{(m.battlesGiven.Count > 0 ? $"／已結束戰鬥鏈 {m.battlesGiven.Count}" : "")}", style);
            y += 22f;
        }
    }
}
