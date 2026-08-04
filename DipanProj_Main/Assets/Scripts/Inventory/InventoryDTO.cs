using System.Collections.Generic;

namespace Dipan.Inventory
{
    /// <summary>
    /// 背包存檔用的純資料 DTO（可序列化）。刻意放在 Dipan.Inventory 命名空間：
    /// 存檔系統（Dipan.Save）依賴背包、背包不反向依賴存檔——維持「資料層不碰檔案」的解耦邊界。
    /// 見 readme/SAVE_SYSTEM.md。
    /// </summary>
    public class GridSlotDTO
    {
        public int slot;     // 0..GridCount-1
        public int itemId;
        public int count;
        /// <summary>
        /// 這一件專屬的資料（孔位/鑲的珠子/珠子等級…）；null = 一般可疊道具。
        /// 之後裝備要多屬性就是在 <see cref="ItemInstance"/> 加欄位，這裡不用動。
        /// </summary>
        public ItemInstance inst;
    }

    /// <summary>背包快照：稀疏的道具格 + 裝備欄（slot 列舉名 → 物品 ID）。</summary>
    public class InventoryDTO
    {
        public List<GridSlotDTO> grid = new List<GridSlotDTO>();
        /// <summary>裝備欄：slot 列舉名 → 物品 ID。</summary>
        public Dictionary<string, int> equipment = new Dictionary<string, int>();
        /// <summary>
        /// 裝備欄的實例資料：slot 列舉名 → 那一件的孔位/鑲嵌。
        /// 刻意與 <see cref="equipment"/> 分開兩個欄位而不是改它的型別——這樣既有的讀取端
        /// （例如存檔選擇畫面只想知道「裝了哪把武器」好畫 icon）完全不用改。
        /// </summary>
        public Dictionary<string, ItemInstance> equipmentInst = new Dictionary<string, ItemInstance>();
        public int[] potionSlots;   // 藥水格綁定（長度 = PotionSlotCount）；跟背包一起存檔
    }

    /// <summary>
    /// 倉庫快照（每個倉庫一筆、各有獨立格子）。放在 Dipan.Inventory（同 InventoryDTO 的理由：
    /// 存檔依賴資料層、資料層不依賴存檔）。一份 CharacterSave 可有多個倉庫（storages[]）。
    /// </summary>
    public class StorageDTO
    {
        public string storageId;
        public int rows;
        public int cols;
        public List<GridSlotDTO> grid = new List<GridSlotDTO>();
    }
}
