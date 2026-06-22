using UnityEngine;

namespace Dipan.Inventory
{
    /// <summary>裝備欄位種類。None = 不可裝備（一般道具）。對應背包左側 6 格。</summary>
    public enum EquipSlot
    {
        None = 0,
        Weapon,   // 武器
        Chest,    // 胸甲
        Boots,    // 鞋子
        Gloves,   // 手套
        Amulet,   // 護身符
        Ring,     // 戒指
    }

    /// <summary>單一物品的定義（由 ItemTable.csv 載入）。純資料，不含任何 UI。</summary>
    public class ItemData
    {
        public int ID;
        public string Name;
        public string Category;       // Weapon / Currency / Material / Consumable…（給人看/分類用）
        public EquipSlot EquipSlot;   // 可裝備到哪格；None = 不可裝備
        public string IconPath;       // 相對 Resources、不含副檔名
        public int MaxStack = 1;
        public string Description;    // 簡短說明（底部名稱列用，可留空）
        public string TipStats;       // tooltip 上半：功能/屬性（正楷）。之後可能改由屬性組字，現在純讀 CSV
        public string TipLore;        // tooltip 下半：劇情描述（斜體）
        public int WeaponID;          // 對應 WeaponTable 的武器 ID（裝備此武器 → 玩家切到該武器）；0 = 非武器
        public Sprite Icon;           // 由 ItemDatabase 從 Resources 載入

        public bool IsEquippable => EquipSlot != EquipSlot.None;
        public bool Stackable => MaxStack > 1;
    }
}
