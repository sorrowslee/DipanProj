using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 傳送門的「放劇本方框」格子。實作 ISlotView，重用共用拖放（SlotDragController / InventoryActions），
    /// 但**只收「劇本」道具**（OnDrop 先驗證來源是不是劇本，不是就退回）。也可把劇本再拖回背包。
    /// 與 ItemSlotWidget 不同：不綁 StoragePanel、沒有點擊快搬與 tooltip，單純收/顯示/拖出。
    /// </summary>
    public class ScriptSlotWidget : MonoBehaviour, ISlotView,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        public IItemGrid Container { get; private set; }
        public int Index { get; private set; }

        Image _icon;

        // ── ISlotView ──
        public IItemGrid Grid => Container;
        public int GridIndex => Index;
        public bool IsEquip => false;
        public EquipSlot Equip => EquipSlot.None;
        public RectTransform Rt => (RectTransform)transform;
        public Sprite DragIcon() => _icon != null && _icon.enabled ? _icon.sprite : null;

        public static ScriptSlotWidget Create(Transform parent, float size)
        {
            var go = UIBuilder.Create("ScriptSlot", parent);
            var rt = UIBuilder.Rect(go);
            rt.sizeDelta = new Vector2(size, size);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0f);   // 透明底：外框由方框底圖負責；此格仍可點/拖
            bg.raycastTarget = true;

            var w = go.AddComponent<ScriptSlotWidget>();
            var icon = UIBuilder.Image(go.transform, "Icon", null, Color.white);
            UIBuilder.Stretch(icon.rectTransform, 8, 8, 8, 8);
            icon.raycastTarget = false; icon.preserveAspect = true; icon.enabled = false;
            w._icon = icon;
            return w;
        }

        public void Bind(IItemGrid container, int index)
        {
            Container = container; Index = index;
            Refresh();
        }

        public void Refresh()
        {
            var st = Container != null ? Container.GetAt(Index) : ItemStack.Empty;
            if (st.IsEmpty) { _icon.enabled = false; return; }
            var d = Container.GetData(st.ItemId);
            _icon.sprite = d != null ? d.Icon : null;
            _icon.enabled = _icon.sprite != null;
        }

        // 拖出（把劇本拖回背包）——當作來源。
        public void OnBeginDrag(PointerEventData e) => SlotDragController.Begin(this, e);
        public void OnDrag(PointerEventData e) => SlotDragController.Drag(e);
        public void OnEndDrag(PointerEventData e) => SlotDragController.End(this);

        // 收下（從背包拖進來）——只收劇本，其餘忽略（來源會彈回原位）。
        public void OnDrop(PointerEventData e)
        {
            var src = e.pointerDrag != null ? e.pointerDrag.GetComponent<ISlotView>() : null;
            if (src == null) return;
            int id = InventoryActions.ItemIdOf(src);
            var d = InventorySystem.Instance != null ? InventorySystem.Instance.GetData(id) : null;
            if (d == null || !d.IsScript) return;   // 不是劇本 → 拒收
            SlotDragController.Drop(this, e);
        }
    }
}
