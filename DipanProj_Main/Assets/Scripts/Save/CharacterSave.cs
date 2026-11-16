using System.Collections.Generic;
using Dipan.Inventory;

namespace Dipan.Save
{
    /// <summary>
    /// 統一角色存檔（一個角色一份，序列化成 character.json）。
    /// 這次先填 inventory；storages / stats / progress / mapStates 先佔位（預設/空），
    /// 未來接儲藏箱、角色屬性、地圖狀態持久化時是「加上去」而非重構。見 readme/SAVE_SYSTEM.md。
    /// </summary>
    public class CharacterSave
    {
        public int schemaVersion = SaveConstants.CurrentSchemaVersion;
        public string characterId;
        public string name;
        public int slotIndex = -1;          // 屬於哪個存檔欄位（0..SlotCount-1）
        public int generation = 1;          // 轉生世代（= 周目）
        public string createdAtUtc;
        public string lastPlayedUtc;
        public long playTimeSeconds;

        // ── 核心：物品 ──
        public InventoryDTO inventory = new InventoryDTO();
        public List<StorageDTO> storages = new List<StorageDTO>();   // 儲藏箱（將來），現在通常為空

        // ── 預留（這次不填內容）──
        public StatsDTO stats = new StatsDTO();
        public ProgressDTO progress = new ProgressDTO();
        public Dictionary<string, object> mapStates = new Dictionary<string, object>();   // 對接 MAP_SYSTEM Phase 2

        // ── 終身旗標（跨輪迴保存；只有開新角色才空）──
        // 與 progress.flags（周目旗標，輪迴會清）分開。觸發鏈以旗標名前綴「永久:」路由到這裡。
        // 放在頂層＝ReincarnateInPlace 只換 stats/progress，不會動到它。見 readme/TRIGGER_CHAIN.md。
        public Dictionary<string, string> lifetimeFlags = new Dictionary<string, string>();

        // ── 抽選池的永久解鎖清單（跨輪迴保存；key = GachaPoolTable.csv 的 PoolId，value = 解鎖的物品 id）──
        // 「打贏紅嫁衣 → 血統池 +幽靈」這類解鎖寫在這裡，而不是改 CSV：
        // 實際抽選池 = 基本表（BaseXxxRoll.csv 過濾後） ∪ 這份清單。由觸發鏈動作 unlockRoll 寫入。
        // 放頂層（與 lifetimeFlags 同級）＝ReincarnateInPlace 只換 stats/progress，不會被輪迴清掉。
        // 缺欄（舊存檔）= 空字典，不需要遷移。見 readme/GACHA_SYSTEM.md。
        public Dictionary<string, List<int>> unlockedRollEntries = new Dictionary<string, List<int>>();

        // 曾達到過的最高「完成關卡數」（跨輪迴保存，同 lifetimeFlags 放頂層＝ReincarnateInPlace 不重置）。
        // 給地上物「出現條件＝永久（lifetime）」判定用：只要某周目曾完成 N 關，該物件就永久出現。缺欄＝0。
        public int lifetimeMaxClears = 0;

        // ── 劇本紀錄（角色紀錄層，2026-11-16；跨輪迴保存，同 lifetimeFlags 放頂層＝ReincarnateInPlace 不重置）──
        // key = 劇本 module（MapsTable 的 Module）。目前只記「哪幾級證明領過、第幾世領的」。
        // ⚠ 一律透過 Dipan.Progress.ScriptRecords 讀寫，不要直接碰這個欄位（之後的成就系統、通關筆記也讀它）。
        // 缺欄（舊存檔）= 空字典，不需要遷移。架構見 readme/SAVE_SYSTEM.md §15。
        public Dictionary<string, ScriptRecordDTO> scriptRecords = new Dictionary<string, ScriptRecordDTO>();
    }

    /// <summary>一個劇本的紀錄（見 readme/SAVE_SYSTEM.md §15.3）。之後要加欄位（進入次數…）直接加，不用遷移。</summary>
    public class ScriptRecordDTO
    {
        /// <summary>領過的證明：key = "B"／"A"／"S"。沒有這個 key ＝ 沒領過。</summary>
        public Dictionary<string, ProofClaimDTO> proofs = new Dictionary<string, ProofClaimDTO>();
    }

    /// <summary>一次證明領取的紀錄：第幾世（周目）、什麼時間（UTC，ISO 8601）。</summary>
    public class ProofClaimDTO
    {
        public int gen;
        public string utc;
    }

    /// <summary>角色屬性（待屬性系統，先佔位）。</summary>
    /// <remarks>HP/MP 刻意不存檔——玩家每次進遊戲都滿血滿魔（方便測試），見 readme/COMBAT.md §7。</remarks>
    public class StatsDTO
    {
        public int currency = 0;            // 功德值（2026-11-16 起銅錢改名功德值；欄位名沿用以免遷移）
        // 手上的劇本證明張數（貨幣，2026-11-16）。輪迴會清空，除非選了「全部證明」當帶物。
        // 「領過沒」是另一回事，記在 CharacterSave.scriptRecords（輪迴不清）。見 readme/SAVE_SYSTEM.md §15.4。
        public int proofB = 0;
        public int proofA = 0;
        public int proofS = 0;
    }

    /// <summary>
    /// 進度與轉生紀錄。「關卡」= MapsTable 的一個 Module（如 RedBridalGown、Main）。
    /// 大進度「周目」用 CharacterSave.generation；小進度「完成關卡數」= clearedModules 去重後的數量。
    /// 見 readme/SAVE_SYSTEM.md。
    /// </summary>
    public class ProgressDTO
    {
        // ── 輪迴繼承 ──
        public int inheritedItemId = 0;                              // 舊：單一繼承物品（保留相容；新流程改用 inheritedItems）
        public List<int> inheritedItems = new List<int>();          // 本代從上一代帶入的物品 id（min(周目,7) 件；預留給轉生流程）

        // ── 關卡進度 ──
        public List<string> unlockedModules = new List<string>();   // 已解鎖（抽到）的關卡 module
        public List<string> clearedModules = new List<string>();    // 已通關的關卡 module（去重集合；完成關卡數 = Count）

        // ── 場景旗標 ──
        public bool hubIntroSpawnDone = false;                      // 是否已由開場鏈首次抵達邪佛廣場（決定出生點：洞穴出口/中央）

        // ── 上次所在位置（「繼續遊戲」要回到哪）──
        // ⚠ 只記 HubModule（Main：開場山道 13/14、初始洞窟 11、邪佛廣場 12）的地圖，**關卡一律不記**。
        // 理由：關卡進度（RunProgress：怪清了沒、掉落物）純記憶體，重開遊戲就沒了；而且劇本在傳送門開門時已消耗，
        // 中途離開＝這趟放棄（2026-11-06 起）。記了關卡位置會回到一個進度全失的關卡裡。
        // 所以這兩欄的值永遠是「最後一次待在 Main 的位置」；在關卡中離開＝回到進關卡前的廣場。
        // 缺欄/0（schema v2 以前的舊存檔）= 沒有記錄 → 退回邪佛廣場中央，行為與改動前一致。
        public int lastMapId = 0;
        public string lastEntrance = null;

        // ── 新手夢境教學（2026-09-23，schema v4）──
        // true ＝ 新建角色後還沒走完夢境（還沒進過任何 Main 地圖）。「繼續遊戲」看到它就**重做一次夢**，而不是看 lastMapId。
        // 新建時設 true、第一次進 Main 地圖（醒來到山道 13）時清掉並立刻存檔（見 MapManager 的 Main 檢查點）。
        // 夢境本身不存任何進度：夢裡的血統／武器都是暫時覆寫、夢境地圖不是 Main 不會記位置 ⇒ 重做就是從頭開始。
        // 缺欄（v3 以前的舊存檔）＝ false ＝ 當作做完了，行為與改動前一致。
        public bool dreamTutorialPending = false;

        // ── 其他劇情/狀態旗標（彈性擴充，免改結構）──
        public Dictionary<string, string> flags = new Dictionary<string, string>();
    }

    /// <summary>
    /// 輪迴要帶走的東西（2026-11-16）：物品（一疊算 1 件）＋功德值整筆（1 件）＋全部證明（1 件）。
    /// 交給 <see cref="SaveManager.ReincarnateInPlace(ReincarnateCarry)"/>。見 readme/CORE_LOOP_DESIGN.md §8.6。
    /// </summary>
    public class ReincarnateCarry
    {
        public List<ItemStack> items = new List<ItemStack>();
        public bool keepMerit;    // 功德值整筆
        public bool keepProofs;   // 全部證明（B＋A＋S）

        /// <summary>目前選了幾件（給選物畫面顯示「已選 N／上限」）。</summary>
        public int Count => items.Count + (keepMerit ? 1 : 0) + (keepProofs ? 1 : 0);
    }
}
