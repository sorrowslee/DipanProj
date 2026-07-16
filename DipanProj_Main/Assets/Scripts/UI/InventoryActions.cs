using UnityEngine;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 物品搬運規則（與 UI 無關的純邏輯）：給「來源格」與「目標格」就執行正確的搬運/裝卸。
    /// 涵蓋 格↔格（同/跨容器：放入/合併/交換）、格↔裝備欄（裝備/卸下/交換）。
    /// 所有操作走各容器 / InventorySystem 的 API，自動觸發 OnChanged 讓 UI 重繪。見 readme/STORAGE.md。
    /// </summary>
    public static class InventoryActions
    {
        public static int ItemIdOf(ISlotView v)
        {
            if (v == null) return 0;
            if (v.IsEquip) return InventorySystem.Instance.GetEquipped(v.Equip);
            return v.Grid != null ? v.Grid.GetAt(v.GridIndex).ItemId : 0;
        }

        public static bool HasItem(ISlotView v) => ItemIdOf(v) > 0;

        static ItemStack One(int id) => new ItemStack { ItemId = id, Count = 1 };

        /// <summary>拖放：把 src 放到 dst（依兩者是「格」或「裝備欄」決定行為）。</summary>
        public static void Resolve(ISlotView src, ISlotView dst)
        {
            if (src == null || dst == null || src == dst) return;
            if (!HasItem(src)) return;

            if (!src.IsEquip && !dst.IsEquip) { GridToGrid(src, dst); return; }
            if (!src.IsEquip && dst.IsEquip) { GridToEquip(src, dst); return; }
            if (src.IsEquip && !dst.IsEquip) { EquipToGrid(src, dst); return; }
            // 裝備欄→裝備欄：忽略
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
            if (b.ItemId == a.ItemId)   // 合併
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
            // 不同物品 → 交換
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
            int prev = inv.GetEquipped(target);
            inv.SetEquipped(target, st.ItemId);
            src.Grid.SetAt(src.GridIndex, prev > 0 ? One(prev) : ItemStack.Empty);
        }

        // ── 裝備欄 → 格（卸下到該格；該格有相容裝備則交換，否則只在空格放下）──
        static void EquipToGrid(ISlotView src, ISlotView dst)
        {
            var inv = InventorySystem.Instance;
            int id = inv.GetEquipped(src.Equip);
            if (id <= 0) return;

            var b = dst.Grid.GetAt(dst.GridIndex);
            if (b.IsEmpty)
            {
                dst.Grid.SetAt(dst.GridIndex, One(id));
                inv.SetEquipped(src.Equip, 0);
                return;
            }
            // 目標格是相容裝備 → 交換穿戴
            if (b.Count == 1)
            {
                var bd = dst.Grid.GetData(b.ItemId);
                if (bd != null && bd.EquipSlot == src.Equip)
                {
                    inv.SetEquipped(src.Equip, b.ItemId);
                    dst.Grid.SetAt(dst.GridIndex, One(id));
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
            int remaining = other.AddItem(st.ItemId, st.Count);
            int moved = st.Count - remaining;
            if (moved > 0) slot.Grid.RemoveAt(slot.GridIndex, moved);
            return moved > 0;
        }
    }
}
