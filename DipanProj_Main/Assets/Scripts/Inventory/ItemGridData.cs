using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dipan.Inventory
{
    /// <summary>
    /// 可重用的「道具格網」純資料類別（非 MonoBehaviour），實作 IItemGrid。
    /// 倉庫的每一個分頁就是一個 ItemGridData；之後背包也可改用它收斂重複邏輯。
    /// 與背包/倉庫共用同一份 ItemDatabase。見 readme/STORAGE.md。
    /// </summary>
    public class ItemGridData : IItemGrid
    {
        public string DisplayName { get; private set; }
        public int Cols { get; private set; }
        public int Rows { get; private set; }
        public int Capacity => Cols * Rows;

        readonly ItemDatabase _db;
        ItemStack[] _grid;

        public event Action OnChanged;
        void Raise() => OnChanged?.Invoke();

        public ItemGridData(ItemDatabase db, int cols, int rows, string displayName)
        {
            _db = db;
            Cols = Mathf.Max(1, cols);
            Rows = Mathf.Max(1, rows);
            DisplayName = displayName;
            _grid = new ItemStack[Capacity];
        }

        public ItemData GetData(int itemId) => _db != null ? _db.Get(itemId) : null;
        public ItemStack GetAt(int index) => (index >= 0 && index < Capacity) ? _grid[index] : ItemStack.Empty;

        public void SetAt(int index, ItemStack stack)
        {
            if (index < 0 || index >= Capacity) return;
            _grid[index] = stack;
            Raise();
        }

        public int AddItem(int itemId, int count)
        {
            var d = GetData(itemId);
            if (d == null || count <= 0) return count;
            int max = Mathf.Max(1, d.MaxStack);

            if (max > 1)
                for (int i = 0; i < Capacity && count > 0; i++)
                    if (_grid[i].ItemId == itemId && _grid[i].Count < max)
                    {
                        int add = Mathf.Min(max - _grid[i].Count, count);
                        _grid[i].Count += add; count -= add;
                    }

            for (int i = 0; i < Capacity && count > 0; i++)
                if (_grid[i].IsEmpty)
                {
                    int add = Mathf.Min(max, count);
                    _grid[i] = new ItemStack { ItemId = itemId, Count = add };
                    count -= add;
                }

            Raise();
            return count;
        }

        public bool RemoveAt(int index, int count)
        {
            if (index < 0 || index >= Capacity || _grid[index].IsEmpty) return false;
            _grid[index].Count -= count;
            if (_grid[index].Count <= 0) _grid[index] = ItemStack.Empty;
            Raise();
            return true;
        }

        public bool MoveWithin(int from, int to)
        {
            if (from < 0 || to < 0 || from >= Capacity || to >= Capacity || from == to) return false;
            var tmp = _grid[from]; _grid[from] = _grid[to]; _grid[to] = tmp;
            Raise();
            return true;
        }

        public bool HasAnyItem()
        {
            for (int i = 0; i < Capacity; i++)
                if (!_grid[i].IsEmpty) return true;
            return false;
        }

        /// <summary>
        /// 預設排序（重整按鈕用）：合併同物品堆、依物品 ID 排序、往前壓實。
        /// 詳細排序規則之後再議；目前先給一個合理預設。
        /// </summary>
        public void Sort()
        {
            // 1) 蒐集所有物品數量
            var totals = new Dictionary<int, int>();
            var order = new List<int>();
            for (int i = 0; i < Capacity; i++)
            {
                if (_grid[i].IsEmpty) continue;
                int id = _grid[i].ItemId;
                if (!totals.ContainsKey(id)) { totals[id] = 0; order.Add(id); }
                totals[id] += _grid[i].Count;
            }
            order.Sort();   // 依 ID 由小到大

            // 2) 清空後依排序重新放（合併到 MaxStack 上限）
            _grid = new ItemStack[Capacity];
            int slot = 0;
            foreach (int id in order)
            {
                var d = GetData(id);
                int max = Mathf.Max(1, d != null ? d.MaxStack : 1);
                int remain = totals[id];
                while (remain > 0 && slot < Capacity)
                {
                    int put = Mathf.Min(max, remain);
                    _grid[slot++] = new ItemStack { ItemId = id, Count = put };
                    remain -= put;
                }
            }
            Raise();
        }

        // ── 存檔 ──
        public StorageDTO CaptureTo(string storageId)
        {
            var dto = new StorageDTO { storageId = storageId, rows = Rows, cols = Cols };
            for (int i = 0; i < Capacity; i++)
            {
                if (_grid[i].IsEmpty) continue;
                dto.grid.Add(new GridSlotDTO { slot = i, itemId = _grid[i].ItemId, count = _grid[i].Count });
            }
            return dto;
        }

        public void RestoreFrom(StorageDTO dto)
        {
            if (dto != null && dto.cols > 0 && dto.rows > 0) { Cols = dto.cols; Rows = dto.rows; }
            _grid = new ItemStack[Capacity];
            if (dto != null && dto.grid != null)
                foreach (var s in dto.grid)
                {
                    if (s == null || s.itemId <= 0 || s.count <= 0) continue;
                    var d = GetData(s.itemId);
                    if (d == null) { Debug.LogWarning($"[ItemGridData] 還原跳過未知物品 ID {s.itemId}"); continue; }
                    int count = Mathf.Min(s.count, Mathf.Max(1, d.MaxStack));
                    if (s.slot >= 0 && s.slot < Capacity && _grid[s.slot].IsEmpty)
                        _grid[s.slot] = new ItemStack { ItemId = s.itemId, Count = count };
                    else
                        AddItem(s.itemId, count);
                }
            Raise();
        }
    }
}
