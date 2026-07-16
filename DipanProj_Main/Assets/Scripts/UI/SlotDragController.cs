using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Dipan.UI
{
    /// <summary>
    /// 全域拖放控制器：被任何 ISlotView（背包格/裝備欄/倉庫格）的拖曳事件呼叫，
    /// 維護「拖曳來源 + 懸浮 ghost」，放開時交給 InventoryActions.Resolve 執行搬運。
    /// 因為 ghost 掛在共用的 root canvas、放開時讀 eventData.pointerDrag 上的 ISlotView，
    /// **跨面板（倉庫↔背包）的拖放天生互通**。見 readme/STORAGE.md。
    /// </summary>
    public static class SlotDragController
    {
        static ISlotView _src;
        static GameObject _ghost;

        /// <summary>目前正在拖曳的物品 ID（0=沒拖）。面板輪詢它來「拖起某類物品時亮出可放的空欄位」（不用 static 事件，避免關 Domain Reload 殘留）。</summary>
        public static int DraggingItemId { get; private set; }

        public static void Begin(ISlotView view, PointerEventData e)
        {
            if (view == null || !InventoryActions.HasItem(view)) { _src = null; DraggingItemId = 0; return; }
            _src = view;
            DraggingItemId = InventoryActions.ItemIdOf(view);

            // 讓來源格在拖曳期間不擋 raycast，drop 才能命中底下的目標格
            var cg = view.Rt.GetComponent<CanvasGroup>();
            if (cg == null) cg = view.Rt.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;

            var sprite = view.DragIcon();
            if (sprite != null)
            {
                var canvas = view.Rt.GetComponentInParent<Canvas>();
                _ghost = new GameObject("DragGhost", typeof(RectTransform));
                _ghost.transform.SetParent(canvas != null ? canvas.rootCanvas.transform : view.Rt.root, false);
                var img = _ghost.AddComponent<Image>();
                img.sprite = sprite; img.raycastTarget = false; img.preserveAspect = true;
                img.color = new Color(1, 1, 1, 0.85f);
                var grt = (RectTransform)_ghost.transform;
                grt.sizeDelta = view.Rt.rect.size;   // 原生格子尺寸（簡單夠用；不精算縮放）
                _ghost.transform.SetAsLastSibling();
                _ghost.transform.position = e.position;
            }
        }

        public static void Drag(PointerEventData e)
        {
            if (_ghost != null) _ghost.transform.position = e.position;
        }

        public static void End(ISlotView view)
        {
            DraggingItemId = 0;
            var cg = view != null ? view.Rt.GetComponent<CanvasGroup>() : null;
            if (cg != null) cg.blocksRaycasts = true;
            if (_ghost != null) { Object.Destroy(_ghost); _ghost = null; }
            _src = null;
        }

        public static void Drop(ISlotView target, PointerEventData e)
        {
            var src = e.pointerDrag != null ? e.pointerDrag.GetComponent<ISlotView>() : _src;
            if (src == null || target == null || src == target) return;
            InventoryActions.Resolve(src, target);
        }
    }
}
