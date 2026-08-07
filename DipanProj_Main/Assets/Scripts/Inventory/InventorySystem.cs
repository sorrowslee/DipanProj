using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dipan.Inventory
{
    /// <summary>
    /// 背包格內容：物品 ID + 數量 + （可選的）這一件專屬的實例資料。
    ///
    /// <see cref="Inst"/> 是**參照型別**，所以搬運一律用「移動」語意（放到新位置後把舊位置清空），
    /// 絕對不要把同一個 ItemStack 同時留在兩個地方——那會讓兩格共用同一份鑲嵌。
    /// 有實例的物品一律 Count = 1、且不與任何東西合併堆疊。見 readme/GEM_SOCKET.md。
    /// </summary>
    public struct ItemStack
    {
        public int ItemId;
        public int Count;
        /// <summary>這一件專屬的資料（孔位/珠子等級…）；null = 一般可疊道具，沒有實例概念。</summary>
        public ItemInstance Inst;

        public bool IsEmpty => ItemId <= 0 || Count <= 0;
        /// <summary>有沒有實例資料（有的話就不能疊、不能合併）。</summary>
        public bool HasInst => Inst != null;
        public static ItemStack Empty => new ItemStack { ItemId = 0, Count = 0, Inst = null };

        /// <summary>複製一份（實例也深拷貝）。只有在「真的要多出一件」時才用，一般搬運不要用。</summary>
        public ItemStack DeepClone() => new ItemStack { ItemId = ItemId, Count = Count, Inst = Inst?.Clone() };
    }

    /// <summary>
    /// 背包分包。**分包規則只有一條：穿得上裝備欄的 → 裝備包，其餘全部 → 消耗品包。**
    /// 規則本體在 <see cref="InventorySystem.BagFor"/>，只有那一個地方判斷，不要在別處重寫一份。
    /// </summary>
    public enum BagKind
    {
        /// <summary>裝備包（武器/盔甲/手套/鞋子/護身符/戒指）。</summary>
        Equip = 0,
        /// <summary>消耗品包（藥水、材料、劇本、血統藥劑、能力珠…）。</summary>
        Item = 1,
    }

    /// <summary>
    /// 背包資料層（純資料、跨場景常駐單例）。**不含任何 UI**——UI 透過 OnChanged 事件訂閱重繪、操作時呼叫本類別 API。
    /// 這守住專案的解耦紀律：資料層與呈現層分離（同「彈道不算傷害」「GroundEffect 資料 vs 視覺」）。
    ///
    /// 【格子結構】道具格是**一條扁平陣列切成兩段**：前段 = 裝備包、後段 = 消耗品包。
    /// 之所以不做成兩個獨立容器，是因為「鍛造台鎖住哪一格」「存檔的格位」「新手教學找格子」
    /// 全都用同一個格子編號在對話，切段能讓那些地方一行都不用改。要加格改
    /// <see cref="EquipBagCount"/> / <see cref="ItemBagCount"/> 即可，介面會自動多分幾頁。
    ///
    /// 另外持有 6 個裝備欄與 2 個藥水格。玩家換地圖時物品延續（DontDestroyOnLoad）。
    /// 懶漢單例：第一次存取 Instance 就自動建立，零手動接線。見 readme/INVENTORY.md。
    /// </summary>
    public class InventorySystem : MonoBehaviour, IItemGrid
    {
        // ═══════════════ 容量（要加格就改這裡；兩包各自獨立，改一邊不影響另一邊）═══════════════

        /// <summary>裝備包格數。</summary>
        public const int EquipBagCount = 40;
        /// <summary>消耗品包格數。</summary>
        public const int ItemBagCount = 40;
        /// <summary>道具格總數（＝兩包相加）。存檔與鍛造鎖定用的格子編號都是這個範圍內的「扁平索引」。</summary>
        public const int GridCount = EquipBagCount + ItemBagCount;

        /// <summary>介面一頁顯示幾格（5x4）。放在資料層是為了讓「容量 → 幾頁」這件事只有一種算法。</summary>
        public const int PageSlots = 20;

        public const int PotionSlotCount = 2;   // 藥水格數（要加格改這裡）

        /// <summary>重整鈕在裝備包的排序：武器 → 盔甲 → 手套 → 鞋子 → 護身符 → 戒指。</summary>
        static readonly EquipSlot[] SortEquipOrder =
        {
            EquipSlot.Weapon, EquipSlot.Chest, EquipSlot.Gloves,
            EquipSlot.Boots,  EquipSlot.Amulet, EquipSlot.Ring,
        };

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
        Dictionary<EquipSlot, ItemStack> _equip;   // 裝備欄 → 那一件（含實例資料）
        int[] _potionSlots;                        // 藥水格綁定（長度 = PotionSlotCount）；跟背包一起存檔

        /// <summary>任何變動（加/減/移動/裝/卸）後觸發，UI 用來重繪。</summary>
        public event Action OnChanged;

        /// <summary>
        /// 「玩家身上的能力可能變了」的版本號：裝備欄變動、或任何裝備的鑲嵌被改動時 +1。
        /// 能力容器靠它判斷要不要重算——因為「換了珠子但沒換武器」時物品 ID 完全沒變，光比 ID 會漏掉。
        /// </summary>
        public int LoadoutVersion { get; private set; }

        /// <summary>鑲嵌被改動時由鍛造介面呼叫，讓能力容器知道要重算。</summary>
        public void NotifyLoadoutChanged()
        {
            LoadoutVersion++;
            Raise();
        }

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
            _equip = new Dictionary<EquipSlot, ItemStack>();
            _potionSlots = new int[PotionSlotCount];
        }

        void Raise() => OnChanged?.Invoke();

        // ═══════════════ 分包（唯一的分類規則在這裡）═══════════════

        /// <summary>某一包的第一個格子編號。</summary>
        public static int BagStart(BagKind bag) => bag == BagKind.Equip ? 0 : EquipBagCount;

        /// <summary>某一包有幾格。</summary>
        public static int BagCount(BagKind bag) => bag == BagKind.Equip ? EquipBagCount : ItemBagCount;

        /// <summary>這個格子編號屬於哪一包。</summary>
        public static BagKind BagOf(int gridIndex) => gridIndex < EquipBagCount ? BagKind.Equip : BagKind.Item;

        /// <summary>某一包要分成幾頁（依 <see cref="PageSlots"/>）。</summary>
        public static int PagesOf(BagKind bag) => Mathf.Max(1, Mathf.CeilToInt(BagCount(bag) / (float)PageSlots));

        /// <summary>
        /// **分包規則本體**：穿得上裝備欄的進裝備包，其餘（藥水/材料/劇本/血統藥劑/能力珠…）進消耗品包。
        /// 要改分類就只改這一個方法。
        /// </summary>
        public static BagKind BagFor(ItemData d) => (d != null && d.IsEquippable) ? BagKind.Equip : BagKind.Item;

        /// <summary>這個物品該待在哪一包（查不到物品定義時當消耗品）。</summary>
        public BagKind BagForItem(int itemId) => BagFor(GetData(itemId));

        // ═══════════════ 查詢 ═══════════════

        public ItemData GetData(int itemId) => Db != null ? Db.Get(itemId) : null;
        public ItemStack GetGrid(int index) => (index >= 0 && index < GridCount) ? _grid[index] : ItemStack.Empty;

        /// <summary>裝備欄裡那一件的物品 ID（0 = 空）。要連實例一起拿請用 <see cref="GetEquippedStack"/>。</summary>
        public int GetEquipped(EquipSlot slot) => GetEquippedStack(slot).ItemId;

        /// <summary>裝備欄裡那一件（含孔位/鑲嵌資料）。空欄回 Empty。</summary>
        public ItemStack GetEquippedStack(EquipSlot slot)
            => (_equip != null && _equip.TryGetValue(slot, out var st)) ? st : ItemStack.Empty;

        /// <summary>裝備欄裡那一件的實例資料（沒有就回 null）。</summary>
        public ItemInstance GetEquippedInstance(EquipSlot slot) => GetEquippedStack(slot).Inst;

        /// <summary>
        /// 直接設定某裝備欄的物品 ID（0 = 清空）。
        /// ⚠ 這條路**不帶實例資料**（等於一件沒有孔的裸裝），只給「憑空指定裝備」的舊呼叫端與測試用；
        /// 正常的裝/卸/交換請走 <see cref="SetEquippedStack"/>，否則玩家的鑲嵌會憑空消失。
        /// </summary>
        public void SetEquipped(EquipSlot slot, int itemId)
        {
            if (slot == EquipSlot.None) return;
            if (itemId <= 0) { SetEquippedStack(slot, ItemStack.Empty); return; }
            SetEquippedStack(slot, new ItemStack { ItemId = itemId, Count = 1, Inst = null });
        }

        /// <summary>設定某裝備欄的內容（含實例資料）。裝/卸/交換一律走這條。</summary>
        public void SetEquippedStack(EquipSlot slot, ItemStack st)
        {
            if (slot == EquipSlot.None) return;
            if (st.IsEmpty) _equip.Remove(slot);
            else { st.Count = 1; _equip[slot] = st; }
            LoadoutVersion++;
            Raise();
        }

        // ═══════════════ IItemGrid（讓背包與倉庫共用搬運/UI 程式）═══════════════
        //
        // 注意：本類別當 IItemGrid 用時，索引是「跨兩包的扁平索引」；
        // 而 AddItem / AddStack 會依物品分類自動丟進正確的那一包（倉庫點擊送過來就是走這條）。

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

        /// <summary>某物品的實際堆疊上限；有實例資料的一律 1（兩件的實例不同，疊在一起就分不出來了）。</summary>
        public int MaxStackOf(int itemId)
        {
            var d = GetData(itemId);
            if (d == null) return 1;
            if (Dipan.Inventory.ItemManager.NeedsInstance(d)) return 1;
            return Mathf.Max(1, d.MaxStack);
        }

        /// <summary>
        /// 整理**單一一包**（重整鈕用）：合併同物品堆、依規定順序排序、往前壓實（不動裝備欄、不動另一包）。
        /// 排序順序：裝備包＝武器/盔甲/手套/鞋子/護身符/戒指；消耗品包＝藥水/其他（同類再依物品 ID）。
        /// ⚠ 有實例資料的物品（裝備、能力珠）**一件一格搬過去、不加總合併**，否則鑲嵌與珠子等級會被洗掉。
        /// </summary>
        public void SortBag(BagKind bag)
        {
            int start = BagStart(bag), count = BagCount(bag);
            var totals = new Dictionary<int, int>();   // 可疊物：itemId → 總數
            var units = new List<ItemStack>();         // 最後要寫回去的「一格一筆」

            for (int i = start; i < start + count; i++)
            {
                var st = _grid[i];
                if (st.IsEmpty) continue;
                if (st.HasInst) { units.Add(st); continue; }   // 有實例的原封不動
                if (!totals.ContainsKey(st.ItemId)) totals[st.ItemId] = 0;
                totals[st.ItemId] += st.Count;
            }

            foreach (var kv in totals)
            {
                int max = MaxStackOf(kv.Key), remain = kv.Value;
                while (remain > 0)
                {
                    int put = Mathf.Min(max, remain);
                    units.Add(new ItemStack { ItemId = kv.Key, Count = put, Inst = null });
                    remain -= put;
                }
            }

            units.Sort((a, b) =>
            {
                int ra = SortRank(bag, a.ItemId), rb = SortRank(bag, b.ItemId);
                if (ra != rb) return ra.CompareTo(rb);
                if (a.ItemId != b.ItemId) return a.ItemId.CompareTo(b.ItemId);
                return b.Count.CompareTo(a.Count);   // 同一種：滿的那堆排前面
            });

            for (int i = start; i < start + count; i++) _grid[i] = ItemStack.Empty;
            for (int i = 0; i < units.Count && i < count; i++) _grid[start + i] = units[i];
            Raise();
        }

        /// <summary>排序分組編號（數字小的排前面）。</summary>
        int SortRank(BagKind bag, int itemId)
        {
            var d = GetData(itemId);
            if (bag == BagKind.Equip)
            {
                if (d == null) return 99;
                for (int i = 0; i < SortEquipOrder.Length; i++)
                    if (d.EquipSlot == SortEquipOrder[i]) return i;
                return 90;   // 不該出現在裝備包的東西一律排最後
            }
            return (d != null && d.IsPotion) ? 0 : 1;   // 消耗品包：藥水 → 其他
        }

        /// <summary>整理兩包（給不分包的舊呼叫端用；介面上的重整鈕只整理當前頁籤那一包）。</summary>
        public void SortGrid()
        {
            SortBag(BagKind.Equip);
            SortBag(BagKind.Item);
        }

        public bool HasAnyItem()
        {
            if (_grid != null)
                for (int i = 0; i < GridCount; i++)
                    if (!_grid[i].IsEmpty) return true;
            if (_equip != null)
                foreach (var kv in _equip)
                    if (kv.Value.ItemId > 0) return true;
            return false;
        }

        /// <summary>背包內某 itemId 的總數量（加總所有堆、兩包都算）。不含裝備欄。供觸發鏈條件（道具條件 requireItem）用。</summary>
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
                    if (kv.Value.ItemId == itemId) return true;
            return false;
        }

        /// <summary>指定那一包還有幾個空格。</summary>
        public int FreeSlotCount(BagKind bag)
        {
            if (_grid == null) return 0;
            int start = BagStart(bag), count = BagCount(bag), n = 0;
            for (int i = start; i < start + count; i++) if (_grid[i].IsEmpty) n++;
            return n;
        }

        /// <summary>兩包合計還有幾個空格。⚠ 要判斷「某類東西放不放得下」請用有分包參數的多載。</summary>
        public int FreeSlotCount() => FreeSlotCount(BagKind.Equip) + FreeSlotCount(BagKind.Item);

        // ═══════════════ 操作 ═══════════════

        /// <summary>
        /// 加入物品（自動丟進它該去的那一包：先疊到既有同物品堆、再放空格）。回傳「放不下的剩餘數量」（0 = 全放進去）。
        /// ⚠ 這條路**不會產生實例資料**——需要孔位/等級的物品請走
        /// <see cref="ItemManager.Give"/> 或 <see cref="ItemManager.Create"/> 再用 <see cref="AddStack"/>。
        /// </summary>
        public int AddItem(int itemId, int count = 1)
        {
            var d = GetData(itemId);
            if (d == null || count <= 0) return count;

            // 需要實例的物品從這條路進來 = 呼叫端漏走工廠。幫它補一份，並提醒。
            if (ItemManager.NeedsInstance(d))
            {
                int left = 0;
                for (int i = 0; i < count; i++)
                    left += AddStack(ItemManager.Create(itemId, 1));
                return left;
            }

            var bag = BagFor(d);
            int start = BagStart(bag), cap = BagCount(bag);
            int max = Mathf.Max(1, d.MaxStack);

            if (max > 1)
            {
                for (int i = start; i < start + cap && count > 0; i++)
                {
                    if (_grid[i].HasInst) continue;                       // 有實例的絕不合併
                    if (_grid[i].ItemId == itemId && _grid[i].Count < max)
                    {
                        int add = Mathf.Min(max - _grid[i].Count, count);
                        _grid[i].Count += add;
                        count -= add;
                    }
                }
            }

            for (int i = start; i < start + cap && count > 0; i++)
            {
                if (_grid[i].IsEmpty)
                {
                    int add = Mathf.Min(max, count);
                    _grid[i] = new ItemStack { ItemId = itemId, Count = add, Inst = null };
                    count -= add;
                }
            }

            Raise();
            return count;   // 剩餘（那一包滿了沒放完）
        }

        /// <summary>
        /// 把一個「已經存在的」ItemStack 放進背包（自動分包，實例資料原封不動帶著走）。
        /// 掉落物撿取、關卡結算落袋、倉庫搬回、鍛造退回背包都走這條。回傳放不下的剩餘數量。
        /// </summary>
        public int AddStack(ItemStack st)
        {
            if (st.IsEmpty) return 0;
            if (!st.HasInst) return AddItem(st.ItemId, st.Count);   // 沒有實例 → 照一般疊堆規則

            var bag = BagForItem(st.ItemId);
            int start = BagStart(bag), cap = BagCount(bag);
            for (int i = start; i < start + cap; i++)
            {
                if (!_grid[i].IsEmpty) continue;
                st.Count = 1;
                _grid[i] = st;
                Raise();
                return 0;
            }
            return st.Count;   // 那一包沒空格
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

        /// <summary>取走某格的整份內容（含實例），該格清空。給鑲嵌這類「把東西搬到別處」的操作用。</summary>
        public ItemStack TakeAt(int gridIndex)
        {
            if (gridIndex < 0 || gridIndex >= GridCount) return ItemStack.Empty;
            var st = _grid[gridIndex];
            if (st.IsEmpty) return ItemStack.Empty;
            _grid[gridIndex] = ItemStack.Empty;
            Raise();
            return st;
        }

        /// <summary>依物品 ID 移除 count 個（跨堆跨包扣除，供喝藥/消耗用）。回傳沒扣到的剩餘。</summary>
        public int RemoveItem(int itemId, int count = 1)
        {
            if (itemId <= 0 || count <= 0 || _grid == null) return count;
            for (int i = 0; i < GridCount && count > 0; i++)
            {
                if (_grid[i].ItemId == itemId && _grid[i].Count > 0)
                {
                    int take = Mathf.Min(_grid[i].Count, count);
                    _grid[i].Count -= take;
                    if (_grid[i].Count <= 0) _grid[i] = ItemStack.Empty;
                    count -= take;
                }
            }
            Raise();
            return count;
        }

        // ── HUD 藥水格綁定（跟背包一起存檔；HUD 讀/寫，見 readme/BOTTOM_HUD.md）──
        /// <summary>取藥水格綁定的藥劑物品 ID（i = 0/1）；0 = 空。</summary>
        public int GetPotionSlot(int i) => (_potionSlots != null && i >= 0 && i < _potionSlots.Length) ? _potionSlots[i] : 0;
        /// <summary>設定藥水格綁定（0 = 清空）。會觸發 OnChanged 讓 HUD 重繪。</summary>
        public void SetPotionSlot(int i, int itemId)
        {
            if (_potionSlots == null || i < 0 || i >= _potionSlots.Length) return;
            _potionSlots[i] = Mathf.Max(0, itemId);
            Raise();
        }

        /// <summary>把一個藥劑種類依規則自動放進藥水格：已綁定不動；否則放編號最小的空格；全滿取代 index 0。可延伸（格數=PotionSlotCount）。</summary>
        public void AutoPlacePotion(int itemId)
        {
            if (itemId <= 0 || _potionSlots == null) return;
            for (int i = 0; i < _potionSlots.Length; i++) if (_potionSlots[i] == itemId) return;
            for (int i = 0; i < _potionSlots.Length; i++) if (_potionSlots[i] == 0) { _potionSlots[i] = itemId; Raise(); return; }
            _potionSlots[0] = itemId; Raise();
        }

        /// <summary>
        /// 交換兩個道具格（拖放重排用）。
        /// ⚠ **跨包一律拒絕**——不然裝備會被搬進消耗品包，之後就再也排序不到正確位置。
        /// 介面上一次只看得到一包，所以正常操作不會走到這裡；真的跨包時由
        /// <see cref="InventoryActions"/> 改用「丟進正確的那一包」處理。
        /// </summary>
        public bool MoveGrid(int from, int to)
        {
            if (from < 0 || to < 0 || from >= GridCount || to >= GridCount || from == to) return false;
            if (BagOf(from) != BagOf(to)) return false;
            var tmp = _grid[from];
            _grid[from] = _grid[to];
            _grid[to] = tmp;
            Raise();
            return true;
        }

        /// <summary>把某道具格的可裝備物品裝到對應裝備欄（原本裝著的換回該格）。實例資料兩邊都帶著走。</summary>
        public bool EquipFromGrid(int gridIndex)
        {
            if (gridIndex < 0 || gridIndex >= GridCount) return false;
            var st = _grid[gridIndex];
            if (st.IsEmpty) return false;
            var d = GetData(st.ItemId);
            if (d == null || !d.IsEquippable) return false;

            var slot = d.EquipSlot;
            var prev = GetEquippedStack(slot);
            st.Count = 1;
            _equip[slot] = st;

            // 正常情況這一格就在裝備包裡，換下來的舊裝備直接放回原位；
            // 萬一來源格不在裝備包（例如手改過的存檔），就改丟進正確的那一包，別把裝備留在消耗品包。
            if (prev.IsEmpty || BagOf(gridIndex) == BagKind.Equip)
            {
                _grid[gridIndex] = prev.IsEmpty ? ItemStack.Empty : prev;
            }
            else
            {
                _grid[gridIndex] = ItemStack.Empty;
                if (AddStack(prev) > 0) _grid[gridIndex] = prev;   // 真的放不下就退回原位，別讓它消失
            }
            LoadoutVersion++;
            Raise();
            return true;
        }

        /// <summary>把裝備欄的物品卸回**裝備包**第一個空格（裝備包滿則失敗）。實例資料跟著回背包。</summary>
        public bool Unequip(EquipSlot slot)
        {
            var st = GetEquippedStack(slot);
            if (st.IsEmpty) return false;
            int start = BagStart(BagKind.Equip), cap = BagCount(BagKind.Equip);
            for (int i = start; i < start + cap; i++)
            {
                if (_grid[i].IsEmpty)
                {
                    _grid[i] = st;
                    _equip.Remove(slot);
                    LoadoutVersion++;
                    Raise();
                    return true;
                }
            }
            return false;   // 裝備包沒有空格
        }

        /// <summary>逐一走訪所有「裝備中」的欄位（能力容器重算時用）。</summary>
        public IEnumerable<KeyValuePair<EquipSlot, ItemStack>> EquippedItems()
        {
            if (_equip == null) yield break;
            foreach (var kv in _equip)
                if (kv.Value.ItemId > 0) yield return kv;
        }

        // ═══════════════ 存檔快照（純資料、不碰檔案）═══════════════
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
                    dto.grid.Add(new GridSlotDTO
                    {
                        slot = i,
                        itemId = _grid[i].ItemId,
                        count = _grid[i].Count,
                        inst = _grid[i].Inst,
                    });
                }
            if (_equip != null)
                foreach (var kv in _equip)
                {
                    if (kv.Value.ItemId <= 0) continue;
                    string key = kv.Key.ToString();
                    dto.equipment[key] = kv.Value.ItemId;          // 舊欄位：保留給只想知道「裝了什麼」的讀者（例如存檔選擇畫面）
                    if (kv.Value.Inst != null) dto.equipmentInst[key] = kv.Value.Inst;
                }
            dto.potionSlots = (int[])_potionSlots.Clone();
            return dto;
        }

        /// <summary>
        /// 用 DTO 還原背包/裝備。對找不到的物品 ID 跳過、count 夾到 MaxStack，最後 Raise 一次讓 UI 重繪。
        /// 跨改版安全：物品表移除某 ID 時，舊存檔的該格會被略過而不是整份炸掉。
        ///
        /// **分包遷移**：格子編號落在錯誤的那一包時（例如背包改成雙包之前的舊存檔、或改過容量），
        /// 不硬塞回原格號，改用 <see cref="AddStack"/> 丟進它該去的那一包——物品與鑲嵌都不會掉，
        /// 只是排列順序會被重排一次。
        /// </summary>
        public void RestoreState(InventoryDTO dto)
        {
            for (int i = 0; i < GridCount; i++) _grid[i] = ItemStack.Empty;
            _equip.Clear();
            for (int i = 0; i < _potionSlots.Length; i++) _potionSlots[i] = 0;

            if (dto != null)
            {
                int migrated = 0;
                if (dto.grid != null)
                    foreach (var s in dto.grid)
                    {
                        if (s == null || s.itemId <= 0 || s.count <= 0) continue;
                        var d = GetData(s.itemId);
                        if (d == null) { Debug.LogWarning($"[InventorySystem] 還原跳過未知物品 ID {s.itemId}"); continue; }

                        var inst = NormalizeInstance(d, s.inst);
                        int count = inst != null ? 1 : Mathf.Min(s.count, Mathf.Max(1, d.MaxStack));
                        var st = new ItemStack { ItemId = s.itemId, Count = count, Inst = inst };

                        bool slotOk = s.slot >= 0 && s.slot < GridCount
                                      && BagOf(s.slot) == BagFor(d)
                                      && _grid[s.slot].IsEmpty;
                        if (slotOk) _grid[s.slot] = st;
                        else { AddStack(st); migrated++; }   // 格號越界/被占/分錯包 → 找正確那一包的空位塞回
                    }
                if (migrated > 0)
                    Debug.Log($"[InventorySystem] 讀檔時有 {migrated} 件物品依分類重新分配到裝備包/消耗品包（內容與鑲嵌不變，只有排列順序改變）。");

                if (dto.equipment != null)
                    foreach (var kv in dto.equipment)
                    {
                        if (kv.Value <= 0) continue;
                        if (!System.Enum.TryParse(kv.Key, true, out EquipSlot slot) || slot == EquipSlot.None) continue;
                        var d = GetData(kv.Value);
                        if (d == null) { Debug.LogWarning($"[InventorySystem] 還原跳過未知裝備 ID {kv.Value}"); continue; }

                        ItemInstance inst = null;
                        if (dto.equipmentInst != null) dto.equipmentInst.TryGetValue(kv.Key, out inst);
                        _equip[slot] = new ItemStack { ItemId = kv.Value, Count = 1, Inst = NormalizeInstance(d, inst) };
                    }
            }

            for (int i = 0; i < _potionSlots.Length; i++)
            {
                int id = (dto != null && dto.potionSlots != null && i < dto.potionSlots.Length) ? dto.potionSlots[i] : 0;
                _potionSlots[i] = GetData(id) != null ? id : 0;   // 綁定的藥若已不在物品表 → 清空
            }

            LoadoutVersion++;
            Raise();
        }

        /// <summary>
        /// 把讀回來的實例資料修成「符合現在物品定義」的樣子：
        /// 需要實例卻缺 → 現場補一份（舊存檔/開發期改表的相容處理）；不需要實例卻有 → 丟掉。
        /// 孔位數量對不上（改過 SocketMax）也在這裡補齊。
        /// </summary>
        ItemInstance NormalizeInstance(ItemData d, ItemInstance inst)
        {
            if (d == null) return null;
            if (!ItemManager.NeedsInstance(d)) return null;

            if (inst == null) return ItemManager.CreateInstance(d);

            if (d.IsGem && inst.level <= 0) inst.level = 1;
            if (d.IsEquippable)
            {
                if (inst.sockets == null) inst.sockets = new List<SocketSlot>();
                while (inst.sockets.Count < ItemInstance.SocketMax) inst.sockets.Add(new SocketSlot());
                while (inst.sockets.Count > ItemInstance.SocketMax) inst.sockets.RemoveAt(inst.sockets.Count - 1);
                for (int i = 0; i < inst.sockets.Count; i++)
                    if (inst.sockets[i] == null) inst.sockets[i] = new SocketSlot();
            }
            return inst;
        }
    }
}
