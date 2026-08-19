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

            // icon 改成固定尺寸（不是四邊拉伸）——IconFit 只處理固定尺寸的 icon，
            // 這裡設的是**內容框**，實際大小由 IconFit 依不透明內容反推。見 UI/IconFit.cs。
            var icon = UIBuilder.Image(go.transform, "Icon", null, Color.white);
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(size * 0.84f, size * 0.82f);
            icon.raycastTarget = false; icon.preserveAspect = true; icon.enabled = false;
            w._icon = icon;

            int countSize = Mathf.RoundToInt(Mathf.Clamp(size * 0.26f, 14f, 26f));
            var count = UIBuilder.Text(go.transform, "Count", "", countSize, Color.white, TextAnchor.LowerRight);
            UIBuilder.Stretch(count.rectTransform, 0, 4, 0, 3);
            count.raycastTarget = false;
            var sh = count.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sh.effectDistance = new Vector2(2f, -2f);
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
            // 珠子是「珠身＋能力符號」兩層，一律走 ItemIcons（見 readme/GEM_SOCKET.md）
            ItemIcons.Apply(_icon, st);
            _count.text = st.Count > 1 ? st.Count.ToString() : "";
        }

        // ⚠ **只收左鍵。** 全遊戲的約定是「左鍵＝搬移，右鍵＝使用」（見 InventoryPanel）。
        //    這裡的 HandleClick 是「一鍵送回背包」＝搬移，所以右鍵不該觸發它；
        //    而倉庫裡的東西刻意**不能直接使用**（要先拿回背包），所以右鍵在這裡就是沒有動作。
        //    原本這裡完全沒判斷按鍵，右鍵也會搬——那會讓「右鍵＝使用」這條規則在倉庫破功。
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
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
