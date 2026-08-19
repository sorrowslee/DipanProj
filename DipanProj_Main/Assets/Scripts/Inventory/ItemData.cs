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
        public int TargetMapId;       // 劇本類道具：放進傳送門後要去的關卡（MapsTable ID）；0 = 非劇本
        public string TargetEntrance; // 目的地落點名（空 = 目標圖預設出生點）
        public int HealHp;            // 藥劑：喝下回復的生命（0 = 不回血）
        public int HealMp;            // 藥劑：喝下回復的魔力（0 = 不回魔）
        public float LightRadius;     // 發光半徑（世界單位）：>0＝此裝備「裝在身上」時發光照亮周遭；0/空＝不發光。取所有裝備欄最大值畫光圈。
        public int BloodlineID;       // 系列起始藥劑：對應 BloodlineTable 的 Id（慣例是某系列第一階）；0 = 不是起始藥劑
        public int GemID;             // 能力珠：對應 GemTable 的 GemID（決定它給的是哪個能力）；0 = 不是能力珠
        public int BloodlineUpgrade;  // 血統進階藥劑：目標階數（2 = 中階、3 = 高階）。全系列通用；0 = 不是進階藥劑
        public Sprite Icon;           // 由 ItemDatabase 從 Resources 載入

        public bool IsEquippable => EquipSlot != EquipSlot.None;
        public bool Stackable => MaxStack > 1;
        /// <summary>是不是「劇本」（可放進傳送門選關卡的道具）＝有指定目的地關卡。</summary>
        public bool IsScript => TargetMapId > 0;
        /// <summary>
        /// 是不是「藥劑」（可拖到 HUD 藥水格、按數字鍵 1／2 喝）。
        /// 在背包裡：**左鍵＝綁定到快捷格（不消耗）、右鍵＝當場喝掉**。
        /// </summary>
        public bool IsPotion => Category == "Potion";
        /// <summary>
        /// 是不是「系列起始藥劑」（決定本世走哪一個血統系列，本世只能喝一次、不可逆）。
        /// BloodlineID 指到 BloodlineTable 的某一列，慣例上是該系列的第一階。
        /// </summary>
        public bool IsBloodlineStarter => BloodlineID > 0;
        /// <summary>
        /// 是不是「血統進階藥劑」（把目前系列往上推一階）。**全系列通用**，所以它不指定血統，
        /// 只指定目標階數（2 = 中階、3 = 高階）；實際會變成哪一種血統由 BloodlineSeriesTable 決定。
        /// </summary>
        public bool IsBloodlineUpgrade => BloodlineUpgrade > 0;
        /// <summary>
        /// 是不是「血統藥劑」（起始或進階都算）。一次性消耗道具，在背包裡對它**按右鍵**喝掉
        /// （會先跳確認視窗，因為本世不可逆）。**左鍵不會喝**——左鍵一律只做不消耗的事，
        /// 見 <c>InventoryPanel.OnSlotClicked</c> 與 <c>ItemUse</c>。
        /// 刻意不可裝備（EquipSlot=None）也不算 Potion——它不進 HUD 藥水格。
        /// </summary>
        public bool IsBloodline => IsBloodlineStarter || IsBloodlineUpgrade;
        /// <summary>
        /// 是不是「能力珠」（可鑲進裝備孔位、給該角色一項配方能力）。
        /// 刻意不可裝備——它不占裝備欄，而是鑲進裝備的孔裡。每一顆有自己的等級（1~3），所以不可疊。
        /// 見 readme/GEM_SOCKET.md。
        /// </summary>
        public bool IsGem => GemID > 0;
    }
}
