using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 一個背包格（道具格或裝備欄）的互動元件。掛在格子的透明命中區上，
    /// 接收點擊/移入/移出事件並回呼給 InventoryPanel。持有自己的 icon / 數量文字參照供面板更新。
    /// </summary>
    public class InventorySlotWidget : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public enum Kind { Grid, Equip }

        public Kind kind;
        public int index;             // Grid 用：0..62
        public EquipSlot equipSlot;   // Equip 用

        public Image icon;
        public Text count;

        public Action<InventorySlotWidget> Clicked;
        public Action<InventorySlotWidget> Entered;
        public Action<InventorySlotWidget> Exited;

        public void OnPointerClick(PointerEventData e) => Clicked?.Invoke(this);
        public void OnPointerEnter(PointerEventData e) => Entered?.Invoke(this);
        public void OnPointerExit(PointerEventData e) => Exited?.Invoke(this);
    }
}
