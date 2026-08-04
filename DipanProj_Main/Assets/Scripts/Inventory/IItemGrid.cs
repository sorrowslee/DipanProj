using System;

namespace Dipan.Inventory
{
    /// <summary>
    /// 「道具格容器」統一介面——背包（InventorySystem）與倉庫（StorageSystem）都實作它，
    /// 讓「搬運」與「UI 格子渲染／拖放」共用同一套程式，不必為兩種容器各寫一份。
    /// 只涵蓋道具格（不含背包的裝備欄）。見 readme/STORAGE.md。
    /// </summary>
    public interface IItemGrid
    {
        /// <summary>給人看的容器名（UI 標題："背包" / "倉庫"）。</summary>
        string DisplayName { get; }

        /// <summary>格子總數。</summary>
        int Capacity { get; }

        /// <summary>取某格內容（越界回 Empty）。</summary>
        ItemStack GetAt(int index);

        /// <summary>直接設定某格（拖放交換用）。會觸發 OnChanged。</summary>
        void SetAt(int index, ItemStack stack);

        /// <summary>加入物品（先疊堆再放空格）。回傳放不下的剩餘數量。⚠ 不帶實例資料，需要孔位/等級的物品請走 ItemManager。</summary>
        int AddItem(int itemId, int count);

        /// <summary>
        /// 放入一個「已經存在的」ItemStack（實例資料原封不動帶著走）。回傳放不下的剩餘數量。
        /// 跨容器搬運、退回背包一律走這條，才不會把鑲嵌與珠子等級洗掉。
        /// </summary>
        int AddStack(ItemStack stack);

        /// <summary>移除某格的物品。</summary>
        bool RemoveAt(int index, int count);

        /// <summary>同容器內交換兩格（拖放重排用）。</summary>
        bool MoveWithin(int from, int to);

        /// <summary>查物品定義（icon／名稱／堆疊上限）。</summary>
        ItemData GetData(int itemId);

        /// <summary>內容變動事件，UI 訂閱重繪。</summary>
        event Action OnChanged;
    }
}
