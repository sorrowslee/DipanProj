using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 一個背包格（道具格或裝備欄）的互動元件。點擊/移入/移出回呼給 InventoryPanel（裝/卸、tooltip、高亮）。
    /// 同時實作 ISlotView，拖放交給共用 SlotDragController，因此能與倉庫（ItemSlotWidget）互拖。
    /// 持有自己的 icon / 數量文字參照供面板更新。見 readme/INVENTORY.md、STORAGE.md。
    /// </summary>
    public class InventorySlotWidget : MonoBehaviour, ISlotView,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        public enum Kind { Grid, Equip }

        public Kind kind;
        public int index;             // Grid 用：0..62
        public EquipSlot equipSlot;   // Equip 用

        public Image icon;
        public Text count;
        public Image dropHi;   // 拖曳時「可放這格」黃色高亮（裝備欄用）

        public Action<InventorySlotWidget> Clicked;
        public Action<InventorySlotWidget> RightClicked;
        public Action<InventorySlotWidget> Entered;
        public Action<InventorySlotWidget> Exited;

        // ── ISlotView：道具格 → 背包格網；裝備欄 → Grid 為 null ──
        public IItemGrid Grid => kind == Kind.Grid ? InventorySystem.Instance : null;
        public int GridIndex => index;
        public bool IsEquip => kind == Kind.Equip;
        public EquipSlot Equip => equipSlot;
        public RectTransform Rt => (RectTransform)transform;
        public Sprite DragIcon() => (icon != null && icon.enabled) ? icon.sprite : null;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) RightClicked?.Invoke(this);
            else Clicked?.Invoke(this);
        }
        public void OnPointerEnter(PointerEventData e) => Entered?.Invoke(this);
        public void OnPointerExit(PointerEventData e) => Exited?.Invoke(this);

        public void OnBeginDrag(PointerEventData e) => SlotDragController.Begin(this, e);
        public void OnDrag(PointerEventData e) => SlotDragController.Drag(e);
        public void OnEndDrag(PointerEventData e) => SlotDragController.End(this);
        public void OnDrop(PointerEventData e) => SlotDragController.Drop(this, e);
    }
}
