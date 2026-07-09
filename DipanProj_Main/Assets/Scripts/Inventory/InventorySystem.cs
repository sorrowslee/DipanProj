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
    public class InventorySystem : MonoBehaviour, IItemGrid
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
            // 主要來源：場景上 ItemTableProvider 提供的 TextAsset（CSV 在 Assets/Data，與其他表同位置）。
            // 找不到 provider / 未指定時退回 Resources（舊位置）——一般會印錯誤指引使用者去拖 CSV。
            var provider = FindObjectOfType<ItemTableProvider>();
            if (provider != null && provider.itemCSV != null) Db.LoadFromTextAsset(provider.itemCSV);
            else Db.LoadFromResources();
            _grid = new ItemStack[GridCount];
            _equip = new Dictionary<EquipSlot, int>();
        }

        void Raise() => OnChanged?.Invoke();

        // ───────────── 查詢 ─────────────

        public ItemData GetData(int itemId) => Db != null ? Db.Get(itemId) : null;
        public ItemStack GetGrid(int index) => (index >= 0 && index < GridCount) ? _grid[index] : ItemStack.Empty;
        public int GetEquipped(EquipSlot slot) => (_equip != null && _equip.TryGetValue(slot, out var id)) ? id : 0;

        /// <summary>直接設定某裝備欄的物品 ID（0 = 清空）。拖放/跨容器裝備用；會觸發 OnChanged（裝備↔武器連動靠它）。</summary>
        public void SetEquipped(EquipSlot slot, int itemId)
        {
            if (slot == EquipSlot.None) return;
            _equip[slot] = Mathf.Max(0, itemId);
            Raise();
        }

        // ───────────── IItemGrid（讓背包與倉庫共用搬運/UI 程式）─────────────
        public string DisplayName => "背包";
        public int Capacity => GridCount;
        public ItemStack GetAt(int index) => GetGrid(index);
        public void SetAt(int index, ItemStack stack)
        {
            if (index < 0 || index >= GridCount) return;
            _grid[index] = stack;
            Raise();
        }
        public bool MoveWithin(int from, int to) => MoveGrid(from, to);

        /// <summary>整理道具格（重整鈕用）：合併同物品堆、依物品 ID 排序、往前壓實（不動裝備欄）。</summary>
        public void SortGrid()
        {
            var totals = new Dictionary<int, int>();
            var order = new List<int>();
            for (int i = 0; i < GridCount; i++)
            {
                if (_grid[i].IsEmpty) continue;
                int id = _grid[i].ItemId;
                if (!totals.ContainsKey(id)) { totals[id] = 0; order.Add(id); }
                totals[id] += _grid[i].Count;
            }
            order.Sort();

            for (int i = 0; i < GridCount; i++) _grid[i] = ItemStack.Empty;
            int slot = 0;
            foreach (int id in order)
            {
                var d = GetData(id);
                int max = Mathf.Max(1, d != null ? d.MaxStack : 1);
                int remain = totals[id];
                while (remain > 0 && slot < GridCount)
                {
                    int put = Mathf.Min(max, remain);
                    _grid[slot++] = new ItemStack { ItemId = id, Count = put };
                    remain -= put;
                }
            }
            Raise();
        }

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

        /// <summary>背包內某 itemId 的總數量（加總所有堆）。只算背包格，不含裝備欄。供觸發鏈條件（道具條件 requireItem）用。</summary>
        public int CountOf(int itemId)
        {
            if (itemId <= 0 || _grid == null) return 0;
            int total = 0;
            for (int i = 0; i < GridCount; i++)
                if (_grid[i].ItemId == itemId) total += _grid[i].Count;
            return total;
        }

        /// <summary>背包是否含有此 itemId（至少 1 個）。只算背包格，不含裝備欄。</summary>
        public bool Has(int itemId) => CountOf(itemId) > 0;

        /// <summary>背包格「或」任一裝備欄是否含有此 itemId（給測試補齊用，避免已裝備的又被重複補一份）。</summary>
        public bool HasAnywhere(int itemId)
        {
            if (Has(itemId)) return true;
            if (_equip != null)
                foreach (var kv in _equip)
                    if (kv.Value == itemId) return true;
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

        // ───────────── 存檔快照（純資料、不碰檔案）─────────────
        // SaveManager 在存檔時呼叫 CaptureState、載入角色時呼叫 RestoreState。
        // 本類別完全不知道有「檔案」，維持資料層與持久化層的解耦（見 readme/SAVE_SYSTEM.md §6.1）。

        /// <summary>把目前背包/裝備打包成可序列化 DTO（稀疏：只收非空格與有裝備的欄）。</summary>
        public InventoryDTO CaptureState()
        {
            var dto = new InventoryDTO();
            if (_grid != null)
                for (int i = 0; i < GridCount; i++)
                {
                    if (_grid[i].IsEmpty) continue;
                    dto.grid.Add(new GridSlotDTO { slot = i, itemId = _grid[i].ItemId, count = _grid[i].Count });
                }
            if (_equip != null)
                foreach (var kv in _equip)
                    if (kv.Value > 0) dto.equipment[kv.Key.ToString()] = kv.Value;
            return dto;
        }

        /// <summary>
        /// 用 DTO 還原背包/裝備。對找不到的物品 ID 跳過、count 夾到 MaxStack，最後 Raise 一次讓 UI 重繪。
        /// 跨改版安全：物品表移除某 ID 時，舊存檔的該格會被略過而不是整份炸掉。
        /// </summary>
        public void RestoreState(InventoryDTO dto)
        {
            for (int i = 0; i < GridCount; i++) _grid[i] = ItemStack.Empty;
            _equip.Clear();

            if (dto != null)
            {
                if (dto.grid != null)
                    foreach (var s in dto.grid)
                    {
                        if (s == null || s.itemId <= 0 || s.count <= 0) continue;
                        var d = GetData(s.itemId);
                        if (d == null) { Debug.LogWarning($"[InventorySystem] 還原跳過未知物品 ID {s.itemId}"); continue; }
                        int count = Mathf.Min(s.count, Mathf.Max(1, d.MaxStack));
                        if (s.slot >= 0 && s.slot < GridCount && _grid[s.slot].IsEmpty)
                            _grid[s.slot] = new ItemStack { ItemId = s.itemId, Count = count };
                        else
                            AddItem(s.itemId, count);   // 格子越界/被占（例如改過背包尺寸）→ 找空位塞回
                    }

                if (dto.equipment != null)
                    foreach (var kv in dto.equipment)
                    {
                        if (kv.Value <= 0) continue;
                        if (!System.Enum.TryParse(kv.Key, true, out EquipSlot slot) || slot == EquipSlot.None) continue;
                        if (GetData(kv.Value) == null) { Debug.LogWarning($"[InventorySystem] 還原跳過未知裝備 ID {kv.Value}"); continue; }
                        _equip[slot] = kv.Value;
                    }
            }

            Raise();
        }
    }
}
