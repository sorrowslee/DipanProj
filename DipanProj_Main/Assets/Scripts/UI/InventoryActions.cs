using UnityEngine;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 物品搬運規則（與 UI 無關的純邏輯）：給「來源格」與「目標格」就執行正確的搬運/裝卸。
    /// 涵蓋 格↔格（同/跨容器：放入/合併/交換）、格↔裝備欄（裝備/卸下/交換）。
    /// 所有操作走各容器 / InventorySystem 的 API，自動觸發 OnChanged 讓 UI 重繪。見 readme/STORAGE.md。
    ///
    /// ⚠ 這裡的搬運一律是**移動**（整份 ItemStack 含實例資料搬過去、來源清空），
    /// 絕對不要只搬 itemId——那會讓裝備的鑲嵌與珠子的等級憑空消失。見 readme/GEM_SOCKET.md。
    /// </summary>
    public static class InventoryActions
    {
        public static int ItemIdOf(ISlotView v) => StackOf(v).ItemId;

        /// <summary>取某個格子/裝備欄的完整內容（含實例資料）。</summary>
        public static ItemStack StackOf(ISlotView v)
        {
            if (v == null) return ItemStack.Empty;
            if (v.IsEquip) return InventorySystem.Instance.GetEquippedStack(v.Equip);
            return v.Grid != null ? v.Grid.GetAt(v.GridIndex) : ItemStack.Empty;
        }

        public static bool HasItem(ISlotView v) => ItemIdOf(v) > 0;

        /// <summary>拖放：把 src 放到 dst（依兩者是「格」或「裝備欄」決定行為）。</summary>
        public static void Resolve(ISlotView src, ISlotView dst)
        {
            if (src == null || dst == null || src == dst) return;
            if (!HasItem(src)) return;

            // ★ 目標是背包格，但這件東西該待的那一包不是目標那一格所在的包
            //   （例：背包停在「消耗品」頁時，從倉庫拖一把劍進來）。
            //   不硬塞——硬塞會讓裝備混進消耗品包、之後永遠排序不到正確位置；
            //   改成「丟進它自己該去的那一包」，玩家切過去就看得到。
            if (!dst.IsEquip && dst.Grid is InventorySystem invDst
                && (src.IsEquip || !ReferenceEquals(src.Grid, dst.Grid)))
            {
                var incoming = StackOf(src);
                if (invDst.BagForItem(incoming.ItemId) != InventorySystem.BagOf(dst.GridIndex))
                {
                    DropIntoInventory(src, invDst);
                    return;
                }
            }

            if (!src.IsEquip && !dst.IsEquip) { GridToGrid(src, dst); return; }
            if (!src.IsEquip && dst.IsEquip) { GridToEquip(src, dst); return; }
            if (src.IsEquip && !dst.IsEquip) { EquipToGrid(src, dst); return; }
            // 裝備欄→裝備欄：忽略
        }

        /// <summary>
        /// 把來源那一件丟進背包（由 <see cref="InventorySystem.AddStack"/> 決定進哪一包）。
        /// 放不下就整個不動——寧可玩家覺得「怎麼放不進去」，也不要東西不見。
        /// </summary>
        static void DropIntoInventory(ISlotView src, InventorySystem inv)
        {
            if (src.IsEquip)
            {
                var cur = inv.GetEquippedStack(src.Equip);
                if (cur.IsEmpty) return;
                if (inv.AddStack(cur) == 0) inv.SetEquippedStack(src.Equip, ItemStack.Empty);
                return;
            }
            QuickMoveGrid(src, inv);
        }

        // ── 格 ↔ 格（含跨容器）──
        static void GridToGrid(ISlotView src, ISlotView dst)
        {
            if (src.Grid == dst.Grid) { src.Grid.MoveWithin(src.GridIndex, dst.GridIndex); return; }

            var a = src.Grid.GetAt(src.GridIndex);
            var b = dst.Grid.GetAt(dst.GridIndex);
            if (a.IsEmpty) return;

            if (b.IsEmpty)
            {
                dst.Grid.SetAt(dst.GridIndex, a);
                src.Grid.SetAt(src.GridIndex, ItemStack.Empty);
                return;
            }
            // 合併：只有「兩邊都沒有實例資料」的一般可疊道具才合併。
            // 有實例的（裝備、能力珠）就算 ID 相同也是不同的兩件，合併會弄丟其中一件的內容。
            if (b.ItemId == a.ItemId && !a.HasInst && !b.HasInst)
            {
                var d = dst.Grid.GetData(b.ItemId);
                int max = Mathf.Max(1, d != null ? d.MaxStack : 1);
                int move = Mathf.Clamp(a.Count, 0, max - b.Count);
                if (move > 0)
                {
                    b.Count += move; a.Count -= move;
                    dst.Grid.SetAt(dst.GridIndex, b);
                    src.Grid.SetAt(src.GridIndex, a.Count > 0 ? a : ItemStack.Empty);
                }
                return;
            }
            // 不同物品（或任一邊有實例）→ 交換
            dst.Grid.SetAt(dst.GridIndex, a);
            src.Grid.SetAt(src.GridIndex, b);
        }

        // ── 格 → 裝備欄（放錯也自動歸位）：可裝備物裝到「它自己該去的欄位」；藥水丟到裝備欄 → 自動進藥水格 ──
        static void GridToEquip(ISlotView src, ISlotView dst)
        {
            var st = src.Grid.GetAt(src.GridIndex);
            var d = src.Grid.GetData(st.ItemId);
            if (d == null) return;
            if (d.IsPotion) { InventorySystem.Instance.AutoPlacePotion(st.ItemId); return; }
            if (d.IsEquippable) EquipToCorrectSlot(src);
        }

        /// <summary>把來源格的可裝備物裝到「它自己該去的裝備欄」（不管拖到哪個欄位）；原本裝著的換回來源格。放錯自動歸位共用。</summary>
        public static void EquipToCorrectSlot(ISlotView src)
        {
            if (src == null || src.Grid == null) return;
            var st = src.Grid.GetAt(src.GridIndex);
            var d = src.Grid.GetData(st.ItemId);
            if (d == null || !d.IsEquippable) return;
            var inv = InventorySystem.Instance;
            var target = d.EquipSlot;
            var prev = inv.GetEquippedStack(target);
            inv.SetEquippedStack(target, st);
            src.Grid.SetAt(src.GridIndex, prev.IsEmpty ? ItemStack.Empty : prev);
        }

        // ── 裝備欄 → 格（卸下到該格；該格有相容裝備則交換，否則只在空格放下）──
        static void EquipToGrid(ISlotView src, ISlotView dst)
        {
            var inv = InventorySystem.Instance;
            var cur = inv.GetEquippedStack(src.Equip);
            if (cur.IsEmpty) return;

            var b = dst.Grid.GetAt(dst.GridIndex);
            if (b.IsEmpty)
            {
                dst.Grid.SetAt(dst.GridIndex, cur);
                inv.SetEquippedStack(src.Equip, ItemStack.Empty);
                return;
            }
            // 目標格是相容裝備 → 交換穿戴
            if (b.Count == 1)
            {
                var bd = dst.Grid.GetData(b.ItemId);
                if (bd != null && bd.EquipSlot == src.Equip)
                {
                    inv.SetEquippedStack(src.Equip, b);
                    dst.Grid.SetAt(dst.GridIndex, cur);
                }
            }
            // 其他情形（被不可裝備物品占住）→ 忽略
        }

        /// <summary>點擊快速搬：把 slot 的整堆送到 other 容器（背包↔倉庫）。回傳是否搬了東西。</summary>
        public static bool QuickMoveGrid(ISlotView slot, IItemGrid other)
        {
            if (slot == null || slot.Grid == null || other == null) return false;
            var st = slot.Grid.GetAt(slot.GridIndex);
            if (st.IsEmpty) return false;

            // 有實例的物品：整份搬過去（不能拆、不能只搬 id），成功才清來源。
            if (st.HasInst)
            {
                if (other.AddStack(st) > 0) return false;
                slot.Grid.SetAt(slot.GridIndex, ItemStack.Empty);
                return true;
            }

            int remaining = other.AddItem(st.ItemId, st.Count);
            int moved = st.Count - remaining;
            if (moved > 0) slot.Grid.RemoveAt(slot.GridIndex, moved);
            return moved > 0;
        }
    }
}
