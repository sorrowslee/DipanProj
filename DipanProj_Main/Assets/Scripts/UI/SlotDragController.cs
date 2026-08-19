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

        /// <summary>
        /// 「這一格現在被別的介面借走了，不能拖出也不能放進」的查詢鉤子。
        /// 由需要鎖格子的面板在開啟時掛上、關閉時拆掉（目前唯一的來源是鍛造台：裝備放上鐵砧時
        /// 不搬移物品，而是把背包來源那一格鎖住，見 ForgeAnvilSlot / readme/FORGING.md）。
        /// null = 目前沒有任何鎖。
        ///
        /// ⚠ **鎖一定要擋在這裡，光在格子元件的 OnBeginDrag 裡 return 是擋不住的**——
        /// Unity 的 EventSystem 在滑鼠按下時就把 `eventData.pointerDrag` 設成那個格子了，
        /// 跟我們的 OnBeginDrag 有沒有做事無關；而 <see cref="Drop"/> 是從 pointerDrag 讀來源的，
        /// 所以「沒有懸浮圖示、但放到別格照樣搬走」。見 readme/PROBLEMS.md。
        /// </summary>
        public static System.Func<ISlotView, bool> IsSlotLocked;

        /// <summary>這一格是不是被鎖住了（沒掛鉤子就一律 false）。</summary>
        public static bool Locked(ISlotView v) => v != null && IsSlotLocked != null && IsSlotLocked(v);

        /// <summary>Play 模式 static 殘留保險（本專案關掉了 Domain Reload，見 PROBLEMS I3）。</summary>
        public static void ResetForPlayMode()
        {
            IsSlotLocked = null;
            _src = null;
            DraggingItemId = 0;
            if (_ghost != null) { Object.Destroy(_ghost); _ghost = null; }
        }

        public static void Begin(ISlotView view, PointerEventData e)
        {
            // ⚠ **只有左鍵能拖。** 全遊戲的約定是「左鍵＝搬移、右鍵＝使用」（見 InventoryPanel）。
            //    不擋的話右鍵按住稍微移動一下就變成搬移（Unity 的 ProcessDrag 不分按鍵），
            //    而右鍵原地放開卻是「使用」——同一個手勢差幾像素就是兩種結果，玩家無從預期。
            //    擋在這裡是因為五個格子元件（背包/倉庫/鍛造/劇本/藥水格）都經過這一支。
            if (e != null && e.button != PointerEventData.InputButton.Left) { _src = null; DraggingItemId = 0; return; }
            if (view == null || Locked(view) || !InventoryActions.HasItem(view)) { _src = null; DraggingItemId = 0; return; }
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
            // 沒有經過 Begin（來源被鎖住、或那一格根本沒東西）就不成立這次搬運。
            // 不能只信 e.pointerDrag——它是 EventSystem 在按下時就填好的，跟我們有沒有真的開始拖曳無關。
            if (_src == null) return;
            var src = e.pointerDrag != null ? e.pointerDrag.GetComponent<ISlotView>() : _src;
            if (src == null || target == null || src == target) return;
            if (Locked(src) || Locked(target)) return;   // 被借走的格子既不能拖出、也不能收進
            InventoryActions.Resolve(src, target);
        }
    }
}
