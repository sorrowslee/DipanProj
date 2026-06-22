using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dipan.Inventory
{
    /// <summary>背包格內容：物品 ID + 數量。ItemId &lt;= 0 視為空格。</summary>
    public struct ItemStack
    {
        public int ItemId;
        public int Count;
        public bool IsEmpty => ItemId <= 0 || Count <= 0;
        public static ItemStack Empty => new ItemStack { ItemId = 0, Count = 0 };
    }

    /// <summary>
    /// 背包資料層（純資料、跨場景常駐單例）。**不含任何 UI**——UI 透過 OnChanged 事件訂閱重繪、操作時呼叫本類別 API。
    /// 這守住專案的解耦紀律：資料層與呈現層分離（同「彈道不算傷害」「GroundEffect 資料 vs 視覺」）。
    ///
    /// 持有：7x9=63 個道具格 + 6 個裝備欄。玩家換地圖時物品延續（DontDestroyOnLoad）。
    /// 懶漢單例：第一次存取 Instance 就自動建立，零手動接線。
    /// </summary>
    public class InventorySystem : MonoBehaviour
    {
        public const int Columns = 7;
        public const int Rows = 9;
        public const int GridCount = Columns * Rows;

        static InventorySystem _instance;
        public static InventorySystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<InventorySystem>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[InventorySystem]");
                        _instance = go.AddComponent<InventorySystem>();
                    }
                }
                return _instance;
            }
        }

        public ItemDatabase Db { get; private set; }

        ItemStack[] _grid;
        Dictionary<EquipSlot, int> _equip;   // 裝備欄 → 物品 ID（0 = 空）

        /// <summary>任何變動（加/減/移動/裝/卸）後觸發，UI 用來重繪。</summary>
        public event Action OnChanged;

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Init();
        }

        void Init()
        {
            Db = new ItemDatabase();
            Db.LoadFromResources();
            _grid = new ItemStack[GridCount];
            _equip = new Dictionary<EquipSlot, int>();
        }

        void Raise() => OnChanged?.Invoke();

        // ───────────── 查詢 ─────────────

        public ItemData GetData(int itemId) => Db != null ? Db.Get(itemId) : null;
        public ItemStack GetGrid(int index) => (index >= 0 && index < GridCount) ? _grid[index] : ItemStack.Empty;
        public int GetEquipped(EquipSlot slot) => (_equip != null && _equip.TryGetValue(slot, out var id)) ? id : 0;

        public bool HasAnyItem()
        {
            if (_grid != null)
                for (int i = 0; i < GridCount; i++)
                    if (!_grid[i].IsEmpty) return true;
            if (_equip != null)
                foreach (var kv in _equip)
                    if (kv.Value > 0) return true;
            return false;
        }

        // ───────────── 操作 ─────────────

        /// <summary>加入物品（先疊到既有同物品堆、再放空格）。回傳「放不下的剩餘數量」（0 = 全放進去）。</summary>
        public int AddItem(int itemId, int count = 1)
        {
            var d = GetData(itemId);
            if (d == null || count <= 0) return count;
            int max = Mathf.Max(1, d.MaxStack);

            if (max > 1)
            {
                for (int i = 0; i < GridCount && count > 0; i++)
                {
                    if (_grid[i].ItemId == itemId && _grid[i].Count < max)
                    {
                        int add = Mathf.Min(max - _grid[i].Count, count);
                        _grid[i].Count += add;
                        count -= add;
                    }
                }
            }

            for (int i = 0; i < GridCount && count > 0; i++)
            {
                if (_grid[i].IsEmpty)
                {
                    int add = Mathf.Min(max, count);
                    _grid[i] = new ItemStack { ItemId = itemId, Count = add };
                    count -= add;
                }
            }

            Raise();
            return count;   // 剩餘（背包滿了沒放完）
        }

        /// <summary>移除某格的物品。</summary>
        public bool RemoveAt(int gridIndex, int count = 1)
        {
            if (gridIndex < 0 || gridIndex >= GridCount || _grid[gridIndex].IsEmpty) return false;
            _grid[gridIndex].Count -= count;
            if (_grid[gridIndex].Count <= 0) _grid[gridIndex] = ItemStack.Empty;
            Raise();
            return true;
        }

        /// <summary>交換兩個道具格（拖放重排用）。</summary>
        public bool MoveGrid(int from, int to)
        {
            if (from < 0 || to < 0 || from >= GridCount || to >= GridCount || from == to) return false;
            var tmp = _grid[from];
            _grid[from] = _grid[to];
            _grid[to] = tmp;
            Raise();
            return true;
        }

        /// <summary>把某道具格的可裝備物品裝到對應裝備欄（原本裝著的換回該格）。</summary>
        public bool EquipFromGrid(int gridIndex)
        {
            if (gridIndex < 0 || gridIndex >= GridCount) return false;
            var st = _grid[gridIndex];
            if (st.IsEmpty) return false;
            var d = GetData(st.ItemId);
            if (d == null || !d.IsEquippable) return false;

            var slot = d.EquipSlot;
            int prev = GetEquipped(slot);
            _equip[slot] = st.ItemId;
            _grid[gridIndex] = (prev > 0) ? new ItemStack { ItemId = prev, Count = 1 } : ItemStack.Empty;
            Raise();
            return true;
        }

        /// <summary>把裝備欄的物品卸回第一個空道具格（背包滿則失敗）。</summary>
        public bool Unequip(EquipSlot slot)
        {
            int id = GetEquipped(slot);
            if (id <= 0) return false;
            for (int i = 0; i < GridCount; i++)
            {
                if (_grid[i].IsEmpty)
                {
                    _grid[i] = new ItemStack { ItemId = id, Count = 1 };
                    _equip[slot] = 0;
                    Raise();
                    return true;
                }
            }
            return false;   // 沒有空格
        }
    }
}
