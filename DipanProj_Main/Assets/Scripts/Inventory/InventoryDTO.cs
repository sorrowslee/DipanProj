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
    }

    /// <summary>背包快照：稀疏的道具格 + 裝備欄（slot 列舉名 → 物品 ID）。</summary>
    public class InventoryDTO
    {
        public List<GridSlotDTO> grid = new List<GridSlotDTO>();
        public Dictionary<string, int> equipment = new Dictionary<string, int>();
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
