using System;

namespace Dipan.Inventory
{
    /// <summary>
    /// 傳送門 UI 的「放劇本方框」＝一個容量 1 的道具容器（實作 IItemGrid，直接吃現有拖放）。
    /// 只暫存玩家放進來的一件劇本；按下開啟＝消耗、關閉未開啟＝退回背包（由 ScriptsPanel 處理）。
    /// 「只收劇本」的把關放在 UI 端（ScriptSlotWidget.OnDrop）而非這裡的 SetAt——因為跨容器拖放
    /// 走的是 SetAt（會先塞目標再清來源），若在 SetAt 拒收會讓來源被清空造成物品消失。見 readme/STORAGE.md。
    /// </summary>
    public class ScriptSlotGrid : IItemGrid
    {
        ItemStack _cell = ItemStack.Empty;

        public string DisplayName => "傳送門";
        public int Capacity => 1;

        public ItemStack GetAt(int index) => index == 0 ? _cell : ItemStack.Empty;

        public void SetAt(int index, ItemStack stack)
        {
            if (index != 0) return;
            _cell = stack;
            OnChanged?.Invoke();
        }

        public int AddItem(int itemId, int count)
        {
            // 只有「劇本」且方框是空的才收得下 1 件；其餘全數退回。
            var d = GetData(itemId);
            if (d == null || !d.IsScript || !_cell.IsEmpty || count <= 0) return count;
            _cell = new ItemStack { ItemId = itemId, Count = 1 };
            OnChanged?.Invoke();
            return count - 1;
        }

        public bool RemoveAt(int index, int count)
        {
            if (index != 0 || _cell.IsEmpty) return false;
            _cell = ItemStack.Empty;
            OnChanged?.Invoke();
            return true;
        }

        public bool MoveWithin(int from, int to) => false;   // 只有一格，無同容器重排

        public ItemData GetData(int itemId) => InventorySystem.Instance != null ? InventorySystem.Instance.GetData(itemId) : null;

        public event Action OnChanged;

        // ── 傳送門專用 ──
        /// <summary>方框裡目前那件（空 = 無）。</summary>
        public ItemStack Current => _cell;
        /// <summary>方框裡是不是一件有效劇本。</summary>
        public bool HasScript { get { var d = GetData(_cell.ItemId); return !_cell.IsEmpty && d != null && d.IsScript; } }
        /// <summary>清空並回傳原本那件（給「退回背包」用）。</summary>
        public ItemStack TakeOut() { var s = _cell; _cell = ItemStack.Empty; OnChanged?.Invoke(); return s; }
    }
}
