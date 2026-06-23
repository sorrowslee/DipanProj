using UnityEngine;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 所有「可拖放格子」的共同抽象——背包格(ItemSlotWidget / InventorySlotWidget 道具格)、
    /// 裝備欄(InventorySlotWidget 裝備格)、倉庫格(ItemSlotWidget) 都實作它，
    /// 讓「拖放搬運」(SlotDragController) 與「轉移規則」(InventoryActions) 一套程式吃遍所有容器。
    /// 見 readme/STORAGE.md、INVENTORY.md。
    /// </summary>
    public interface ISlotView
    {
        /// <summary>所屬格網（道具格/倉庫頁）；裝備欄為 null。</summary>
        IItemGrid Grid { get; }

        /// <summary>格網索引（Grid 非 null 時有效）。</summary>
        int GridIndex { get; }

        /// <summary>是否為裝備欄。</summary>
        bool IsEquip { get; }

        /// <summary>裝備欄種類（IsEquip 時有效）。</summary>
        EquipSlot Equip { get; }

        /// <summary>拖曳時的圖示（目前格內物品的 icon，沒有則 null）。</summary>
        Sprite DragIcon();

        /// <summary>此格的 RectTransform（拖曳 ghost 尺寸/父 canvas 用）。</summary>
        RectTransform Rt { get; }
    }
}
