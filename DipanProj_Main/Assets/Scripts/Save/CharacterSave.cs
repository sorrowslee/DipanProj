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

        // 曾達到過的最高「完成關卡數」（跨輪迴保存，同 lifetimeFlags 放頂層＝ReincarnateInPlace 不重置）。
        // 給地上物「出現條件＝永久（lifetime）」判定用：只要某周目曾完成 N 關，該物件就永久出現。缺欄＝0。
        public int lifetimeMaxClears = 0;
    }

    /// <summary>角色屬性（待屬性系統，先佔位）。</summary>
    /// <remarks>HP/MP 刻意不存檔——玩家每次進遊戲都滿血滿魔（方便測試），見 readme/COMBAT.md §7。</remarks>
    public class StatsDTO
    {
        public int currency = 0;            // 金錢（存錢抽關卡用）
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

        // ── 其他劇情/狀態旗標（彈性擴充，免改結構）──
        public Dictionary<string, string> flags = new Dictionary<string, string>();
    }
}
