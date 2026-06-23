using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 通用道具格子元件（倉庫頁、以及任何用 IItemGrid 的格網）。實作 ISlotView，
    /// 拖放交給共用的 SlotDragController（跨面板互通），點擊回呼給所屬面板（送到對側容器）。
    /// 見 readme/STORAGE.md。
    /// </summary>
    public class ItemSlotWidget : MonoBehaviour, ISlotView,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        public IItemGrid Container { get; private set; }
        public int Index { get; private set; }

        StoragePanel _owner;
        Image _bg;
        Image _icon;
        Text _count;

        // 透明底：格線由面板底圖負責，格子本身不蓋色（仍 raycastTarget=true 可點/拖）。
        static readonly Color SlotBg = new Color(1f, 1f, 1f, 0f);

        // ── ISlotView ──
        public IItemGrid Grid => Container;
        public int GridIndex => Index;
        public bool IsEquip => false;
        public EquipSlot Equip => EquipSlot.None;
        public RectTransform Rt => (RectTransform)transform;
        public Sprite DragIcon() => _icon != null && _icon.enabled ? _icon.sprite : null;

        public static ItemSlotWidget Create(Transform parent, float size)
        {
            var go = UIBuilder.Create("Slot", parent);
            var rt = UIBuilder.Rect(go);
            rt.sizeDelta = new Vector2(size, size);

            var bg = go.AddComponent<Image>();
            bg.color = SlotBg;
            bg.raycastTarget = true;

            var w = go.AddComponent<ItemSlotWidget>();
            w._bg = bg;

            var icon = UIBuilder.Image(go.transform, "Icon", null, Color.white);
            UIBuilder.Stretch(icon.rectTransform, 6, 6, 6, 6);
            icon.raycastTarget = false; icon.preserveAspect = true; icon.enabled = false;
            w._icon = icon;

            var count = UIBuilder.Text(go.transform, "Count", "", 16, Color.white, TextAnchor.LowerRight);
            UIBuilder.Stretch(count.rectTransform, 2, 4, 2, 2);
            count.raycastTarget = false;
            w._count = count;

            return w;
        }

        public void Bind(StoragePanel owner, IItemGrid container, int index)
        {
            _owner = owner; Container = container; Index = index;
            Refresh();
        }

        public void Refresh()
        {
            var st = Container != null ? Container.GetAt(Index) : ItemStack.Empty;
            if (st.IsEmpty) { _icon.enabled = false; _count.text = ""; return; }
            var d = Container.GetData(st.ItemId);
            _icon.sprite = d != null ? d.Icon : null;
            _icon.enabled = _icon.sprite != null;
            _count.text = st.Count > 1 ? st.Count.ToString() : "";
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (_owner != null && !Container.GetAt(Index).IsEmpty) _owner.HandleClick(this);
        }

        public void OnPointerEnter(PointerEventData e) { if (_owner != null) _owner.HoverEnter(this); }
        public void OnPointerExit(PointerEventData e) { if (_owner != null) _owner.HoverExit(this); }

        public void OnBeginDrag(PointerEventData e) => SlotDragController.Begin(this, e);
        public void OnDrag(PointerEventData e) => SlotDragController.Drag(e);
        public void OnEndDrag(PointerEventData e) => SlotDragController.End(this);
        public void OnDrop(PointerEventData e) => SlotDragController.Drop(this, e);
    }
}
