using System;

namespace Dipan.Inventory
{
    /// <summary>
    /// 鍛造介面「左三右三共 6 個鑲嵌孔」＝一個容量 6 的道具容器（實作 IItemGrid，直接吃現有的共用拖放）。
    ///
    /// ⚠ **它不自己存東西，而是直接讀寫「鐵砧上那件裝備」的實例資料**（<see cref="Bind"/> 綁定）。
    /// 因為 <see cref="ItemInstance"/> 是參照型別、而且就掛在背包／裝備欄那一格上，所以
    /// 珠子一拖進孔就已經鑲在那件裝備身上了——**沒有「提交」這個步驟，也不會因為關掉面板而消失**，
    /// 存檔時自然跟著那一件一起被寫出去。
    ///
    /// 孔位的鎖定狀態同樣來自實例：孔是在物品產生時隨機骰出來的，位置不連續（可能開的是第 1、4 孔）。
    /// 見 readme/GEM_SOCKET.md。
    /// </summary>
    public class ForgeSocketGrid : IItemGrid
    {
        ItemInstance _target;   // 目前綁定的那件裝備的實例；null = 鐵砧空著

        public string DisplayName => "鑲嵌孔";
        public int Capacity => ForgeSockets.MaxSockets;

        /// <summary>目前綁定的那件裝備的實例（鐵砧空著時為 null）。</summary>
        public ItemInstance Target => _target;

        /// <summary>把孔位面板綁到某一件裝備的實例上。傳 null = 解除綁定（全部上鎖）。</summary>
        public void Bind(ItemInstance inst)
        {
            if (ReferenceEquals(_target, inst)) return;
            _target = inst;
            OnChanged?.Invoke();
        }

        /// <summary>目前開啟的孔位數（0~6）。</summary>
        public int UnlockedCount => ForgeSockets.CountOf(_target);

        /// <summary>這一孔是不是上鎖的（介面用來決定要不要蓋鎖鏈圖、要不要收拖放）。</summary>
        public bool IsLocked(int index) => !ForgeSockets.IsUnlocked(_target, index);

        public ItemStack GetAt(int index)
        {
            if (_target == null || index < 0 || index >= Capacity) return ItemStack.Empty;
            return ItemManager.FromGemRef(_target.GemAt(index));
        }

        public void SetAt(int index, ItemStack stack)
        {
            if (_target == null || index < 0 || index >= Capacity) return;
            if (IsLocked(index)) return;   // 上鎖的孔不收（拖放的把關另在 UI 端，這裡只是保險）

            if (stack.IsEmpty) { _target.SetGem(index, null); Changed(); return; }

            var gem = ItemManager.ToGemRef(stack);
            if (gem == null) return;       // 不是珠子 → 不收（把關主要在 UI 端的 Accepts）
            _target.SetGem(index, gem);
            Changed();
        }

        public int AddItem(int itemId, int count)
        {
            // 只有實例（等級）齊全的珠子才鑲得上，所以這條路先讓工廠補一顆出來再走 AddStack。
            var d = GetData(itemId);
            if (d == null || !d.IsGem || count <= 0) return count;
            int left = count;
            while (left > 0)
            {
                if (AddStack(ItemManager.Create(itemId, 1)) > 0) break;
                left--;
            }
            return left;
        }

        public int AddStack(ItemStack stack)
        {
            if (_target == null || stack.IsEmpty) return stack.Count;
            var gem = ItemManager.ToGemRef(stack);
            if (gem == null) return stack.Count;

            for (int i = 0; i < Capacity; i++)
            {
                if (IsLocked(i) || _target.GemAt(i) != null) continue;
                _target.SetGem(i, gem);
                Changed();
                return 0;
            }
            return stack.Count;   // 沒有空孔
        }

        public bool RemoveAt(int index, int count)
        {
            if (_target == null || index < 0 || index >= Capacity) return false;
            if (_target.TakeGem(index) == null) return false;
            Changed();
            return true;
        }

        public bool MoveWithin(int from, int to)
        {
            if (_target == null) return false;
            if (from < 0 || from >= Capacity || to < 0 || to >= Capacity) return false;
            if (IsLocked(to) || IsLocked(from)) return false;
            var a = _target.GemAt(from);
            var b = _target.GemAt(to);
            _target.SetGem(to, a);
            _target.SetGem(from, b);
            Changed();
            return true;
        }

        public ItemData GetData(int itemId) =>
            InventorySystem.Instance != null ? InventorySystem.Instance.GetData(itemId) : null;

        public event Action OnChanged;

        /// <summary>取出全部珠子（孔位清空）並回傳，給「移除鑲嵌」用。呼叫前請先確認背包放得下。</summary>
        public ItemStack[] TakeAll()
        {
            if (_target == null) return new ItemStack[0];
            var list = new System.Collections.Generic.List<ItemStack>();
            bool any = false;
            for (int i = 0; i < Capacity; i++)
            {
                var g = _target.TakeGem(i);
                if (g == null) continue;
                list.Add(ItemManager.FromGemRef(g));
                any = true;
            }
            if (any) Changed();
            return list.ToArray();
        }

        /// <summary>目前鑲了幾顆。</summary>
        public int GemCount => _target != null ? _target.GemCount : 0;

        /// <summary>鑲嵌內容一改動，除了重繪自己，也要讓玩家的能力容器知道要重算。</summary>
        void Changed()
        {
            OnChanged?.Invoke();
            InventorySystem.Instance?.NotifyLoadoutChanged();
        }
    }
}
