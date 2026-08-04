using System;

namespace Dipan.Inventory
{
    /// <summary>
    /// 鍛造介面「鐵砧中央那一格」。
    ///
    /// ⚠ **它不持有物品，只記住「這件東西現在放在背包的哪一格」**——這是它與倉庫／傳送門方框最大的不同：
    /// 倉庫是真的把物品搬過去，鐵砧只是「借放」。物品自始至終留在背包原位不動、不會換位置也不會被排序打亂，
    /// 只是來源那一格會被鎖起來（壓黑、不能拖也不能點），從鐵砧取下才解鎖。
    /// 這樣玩家把裝備放上鐵砧、再關掉面板，也不會有任何東西跑掉或改變位置。
    ///
    /// 仍然實作 <see cref="IItemGrid"/> 是為了讓 UI 端（ForgeSlotWidget）沿用同一套「讀格子畫 icon」的程式；
    /// 但**寫入類的方法刻意都不做事**（<see cref="SetAt"/>／<see cref="AddItem"/>），因為放上鐵砧不是一次搬運——
    /// 真正的入口是 <see cref="PlaceFromGrid"/> / <see cref="PlaceFromEquip"/>。
    ///
    /// 見 readme/FORGING.md。
    /// </summary>
    public class ForgeAnvilSlot : IItemGrid
    {
        /// <summary>台面上那件東西是從哪裡來的。</summary>
        public enum SourceKind { None, Grid, Equip }

        SourceKind _kind = SourceKind.None;
        int _gridIndex = -1;
        EquipSlot _equip = EquipSlot.None;
        int _itemId;
        ItemInstance _inst;   // 放上去那一刻的實例參照，用來判斷「來源那一格還是不是同一件」

        public SourceKind Kind => _kind;
        /// <summary>來源是背包道具格時的索引（其餘為 -1）。</summary>
        public int SourceGridIndex => _kind == SourceKind.Grid ? _gridIndex : -1;
        /// <summary>來源是裝備欄時的欄位（其餘為 None）。</summary>
        public EquipSlot SourceEquip => _kind == SourceKind.Equip ? _equip : EquipSlot.None;

        public bool IsEmpty => _kind == SourceKind.None;
        public int ItemId => IsEmpty ? 0 : _itemId;
        public ItemData Data => GetData(ItemId);

        /// <summary>
        /// 台面上那一件的實例資料（孔位/鑲嵌）。
        /// ⚠ 這是**背包／裝備欄那一格上的同一個物件參照**，不是複本——所以鑲嵌孔面板直接改它
        /// 就等於改到本體，不需要另外「提交」，存檔時自然跟著那一件寫出去。
        /// </summary>
        public ItemInstance Instance
        {
            get
            {
                var inv = InventorySystem.Instance;
                if (inv == null || IsEmpty) return null;
                return _kind == SourceKind.Grid ? inv.GetGrid(_gridIndex).Inst : inv.GetEquippedStack(_equip).Inst;
            }
        }

        /// <summary>台面上這件的孔位數（沒東西＝0）。</summary>
        public int SocketCount => IsEmpty ? 0 : ForgeSockets.CountOf(Instance);

        public event Action OnChanged;

        // ───────────────────────── 放上／取下 ─────────────────────────

        /// <summary>把背包道具格 index 那件東西放上鐵砧（只記來源，不搬動物品）。回傳是否成功。</summary>
        public bool PlaceFromGrid(int index)
        {
            var inv = InventorySystem.Instance;
            if (inv == null) return false;
            var st = inv.GetGrid(index);
            var d = GetData(st.ItemId);
            if (d == null || !d.IsEquippable) return false;
            if (_kind == SourceKind.Grid && _gridIndex == index) return false;   // 已經是這一格
            _kind = SourceKind.Grid; _gridIndex = index; _equip = EquipSlot.None; _itemId = st.ItemId; _inst = st.Inst;
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>把身上某個裝備欄的東西放上鐵砧（一樣不卸下、只記來源）。回傳是否成功。</summary>
        public bool PlaceFromEquip(EquipSlot slot)
        {
            var inv = InventorySystem.Instance;
            if (inv == null || slot == EquipSlot.None) return false;
            var st = inv.GetEquippedStack(slot);
            var d = GetData(st.ItemId);
            if (d == null || !d.IsEquippable) return false;
            if (_kind == SourceKind.Equip && _equip == slot) return false;
            _kind = SourceKind.Equip; _equip = slot; _gridIndex = -1; _itemId = st.ItemId; _inst = st.Inst;
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>從鐵砧取下（來源那一格解鎖）。回傳原本有沒有東西。</summary>
        public bool Clear()
        {
            if (IsEmpty) return false;
            _kind = SourceKind.None; _gridIndex = -1; _equip = EquipSlot.None; _itemId = 0; _inst = null;
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 檢查來源是否還是原來那件東西——被排序搬走、被消耗掉、被換裝了都算不是。
        /// 不是就自動取下（東西沒有丟失，只是鐵砧不再指著它）。每次背包內容變動時呼叫。
        /// </summary>
        public void Validate()
        {
            if (IsEmpty) return;
            var inv = InventorySystem.Instance;
            if (inv == null) { Clear(); return; }
            var now = _kind == SourceKind.Grid ? inv.GetGrid(_gridIndex) : inv.GetEquippedStack(_equip);
            // 比物品 ID **也比實例參照**：同一個 ID 的另一件（不同孔數/不同鑲嵌）被搬到這一格時，
            // 光比 ID 會誤判成「還是原來那件」，孔位面板就會綁到錯的裝備上。
            if (now.ItemId != _itemId || (_inst != null && !ReferenceEquals(now.Inst, _inst))) Clear();
        }

        /// <summary>某個背包道具格是不是正被鐵砧借走（背包用它決定要不要鎖住／壓黑那一格）。</summary>
        public bool HoldsGrid(int index) => _kind == SourceKind.Grid && _gridIndex == index;

        /// <summary>某個裝備欄是不是正被鐵砧借走。</summary>
        public bool HoldsEquip(EquipSlot slot) => _kind == SourceKind.Equip && slot != EquipSlot.None && _equip == slot;

        // ───────────────────────── IItemGrid（唯讀，給 UI 畫 icon 用）─────────────────────────

        public string DisplayName => "鍛造台";
        public int Capacity => 1;

        public ItemStack GetAt(int index) =>
            (index == 0 && !IsEmpty) ? new ItemStack { ItemId = _itemId, Count = 1, Inst = Instance } : ItemStack.Empty;

        /// <summary>刻意不做事：鐵砧不持有物品。放上鐵砧請走 PlaceFromGrid / PlaceFromEquip。</summary>
        public void SetAt(int index, ItemStack stack) { }

        /// <summary>刻意不做事，理由同 <see cref="SetAt"/>。回傳 count ＝ 一件都收不下。</summary>
        public int AddItem(int itemId, int count) => count;

        /// <summary>刻意不做事，理由同 <see cref="SetAt"/>。回傳 count ＝ 一件都收不下。</summary>
        public int AddStack(ItemStack stack) => stack.Count;

        public bool RemoveAt(int index, int count) => index == 0 && Clear();

        public bool MoveWithin(int from, int to) => false;

        public ItemData GetData(int itemId) =>
            (itemId > 0 && InventorySystem.Instance != null) ? InventorySystem.Instance.GetData(itemId) : null;
    }
}
