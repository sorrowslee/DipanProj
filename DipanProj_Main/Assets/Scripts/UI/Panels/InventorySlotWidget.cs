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

        /// <summary>
        /// 這一格暫時被別的介面「借走」了 → 壓黑。**這個旗標只管外觀**，由 InventoryPanel.Redraw 重畫時設定。
        /// 目前唯一的來源是鍛造台：裝備放上鐵砧時**不搬移物品**，而是把背包這一格鎖起來，取下才解鎖
        /// （見 ForgeAnvilSlot、readme/FORGING.md）。
        ///
        /// ⚠ **行為上的擋不要只靠這個旗標**——它是重畫時才更新的、會慢一拍。
        /// 真正的把關在 <see cref="SlotDragController.IsSlotLocked"/>（即時查詢），見 <see cref="Blocked"/>。
        /// </summary>
        public bool locked;

        /// <summary>這一格現在是不是不能操作（即時查詢，不只看慢一拍的 <see cref="locked"/> 旗標）。</summary>
        bool Blocked => locked || SlotDragController.Locked(this);

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

        // 被借走的格子只保留 hover（還是看得到 tooltip，玩家才知道那是什麼），點擊/拖曳/收放全部擋掉。
        // 拖曳的最終把關在 SlotDragController（只有那裡擋得住 e.pointerDrag），這裡只是提早收工。
        public void OnPointerClick(PointerEventData e)
        {
            if (Blocked) return;
            if (e.button == PointerEventData.InputButton.Right) RightClicked?.Invoke(this);
            else Clicked?.Invoke(this);
        }
        public void OnPointerEnter(PointerEventData e) => Entered?.Invoke(this);
        public void OnPointerExit(PointerEventData e) => Exited?.Invoke(this);

        public void OnBeginDrag(PointerEventData e) { if (!Blocked) SlotDragController.Begin(this, e); }
        public void OnDrag(PointerEventData e) { if (!Blocked) SlotDragController.Drag(e); }
        public void OnEndDrag(PointerEventData e) => SlotDragController.End(this);   // 收尾一律要跑，免得 ghost / raycast 狀態殘留
        public void OnDrop(PointerEventData e) { if (!Blocked) SlotDragController.Drop(this, e); }
    }
}
