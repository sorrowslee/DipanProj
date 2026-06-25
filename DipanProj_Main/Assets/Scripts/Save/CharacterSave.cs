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
        public int generation = 1;          // 轉生世代（第幾代）
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
    }

    /// <summary>角色屬性（待屬性系統，先佔位）。</summary>
    /// <remarks>HP/MP 刻意不存檔——玩家每次進遊戲都滿血滿魔（方便測試），見 readme/COMBAT.md §7。</remarks>
    public class StatsDTO
    {
        public int currency = 0;
    }

    /// <summary>進度與轉生紀錄（待進度系統，先佔位）。</summary>
    public class ProgressDTO
    {
        public int inheritedItemId = 0;     // 本代從上一代繼承來的物品（0 = 無）
        public List<string> unlockedModules = new List<string>();
        public Dictionary<string, string> flags = new Dictionary<string, string>();
    }
}
