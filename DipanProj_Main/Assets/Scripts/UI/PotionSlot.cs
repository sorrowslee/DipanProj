using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 背包介面裡的「藥水格」（左＝數字鍵1、右＝數字鍵2）。綁定的是藥劑**種類**、不是某一瓶：
    /// 從背包格拖一種藥劑上來 → 記住那個物品 ID、顯示 icon ＋背包剩餘數量；那個種類在背包歸零 → 自動清空。
    /// 綁定存進 <see cref="InventorySystem"/>（跟背包一起存檔）。使用（喝）由 <see cref="PotionHotkeys"/> 在遊戲中按 1/2 觸發。
    ///
    /// 因為藥水格與背包同在 Window canvas，投放用標準 uGUI <see cref="OnDrop"/> 即可（沒有遮罩擋事件的問題）。
    /// 綁定＝只記種類、不動背包內容，所以不走 InventoryActions.Resolve（那是搬移）。往格外拖＝解綁清空。
    /// 見 readme/BOTTOM_HUD.md、INVENTORY.md。
    /// </summary>
    [DisallowMultipleComponent]
    public class PotionSlot : MonoBehaviour,
        IDropHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        public int index;         // 0 = 左（鍵1）、1 = 右（鍵2）
        public Image icon;
        public Text count;
        public Image dropHi;      // 拖曳藥劑時「可放這格」黃色高亮

        /// <summary>移入時回呼（傳綁定的藥劑 ID，供面板顯示 tooltip）。</summary>
        public System.Action<int> Entered;
        public System.Action Exited;

        RectTransform _rt;
        bool _dragging;
        GameObject _ghost;

        void Awake() { _rt = (RectTransform)transform; }

        int BoundId => InventorySystem.Instance.GetPotionSlot(index);

        /// <summary>依綁定＋背包數量刷新 icon 與數量；種類消失/歸零則自動清空。</summary>
        public void Refresh()
        {
            int id = BoundId;
            var d = id > 0 ? InventorySystem.Instance.GetData(id) : null;
            int have = (id > 0 && d != null) ? InventorySystem.Instance.CountOf(id) : 0;
            if (id > 0 && (d == null || have <= 0))   // 綁定失效或用完 → 清空
            {
                InventorySystem.Instance.SetPotionSlot(index, 0);
                id = 0; d = null; have = 0;
            }
            if (icon != null)
            {
                icon.sprite = d != null ? d.Icon : null;
                icon.enabled = d != null && d.Icon != null;
            }
            if (count != null) count.text = have > 0 ? have.ToString() : "";
        }

        // ── 收「從背包格拖來」的藥劑 → 綁定種類（不消耗背包）──
        public void OnDrop(PointerEventData e)
        {
            var src = e.pointerDrag != null ? e.pointerDrag.GetComponent<ISlotView>() : null;
            if (src == null || src.Grid == null) return;                 // 只收有格網的來源（道具格）
            var st = src.Grid.GetAt(src.GridIndex);
            if (st.IsEmpty) return;
            var d = InventorySystem.Instance.GetData(st.ItemId);
            if (d == null) return;
            if (d.IsPotion) { InventorySystem.Instance.SetPotionSlot(index, st.ItemId); return; }   // 藥水 → 綁到這格
            if (d.IsEquippable) InventoryActions.EquipToCorrectSlot(src);                            // 武器等丟到藥水格 → 自動裝備
        }

        // ── 往格外拖 = 解綁清空 ──
        public void OnBeginDrag(PointerEventData e)
        {
            int id = BoundId;
            _dragging = id != 0;
            if (!_dragging) return;
            var d = InventorySystem.Instance.GetData(id);
            if (d != null && d.Icon != null)
            {
                var canvas = _rt.GetComponentInParent<Canvas>();
                _ghost = new GameObject("PotionDragGhost", typeof(RectTransform));
                _ghost.transform.SetParent(canvas != null ? canvas.rootCanvas.transform : _rt.root, false);
                var img = _ghost.AddComponent<Image>();
                img.sprite = d.Icon; img.raycastTarget = false; img.preserveAspect = true;
                img.color = new Color(1f, 1f, 1f, 0.85f);
                ((RectTransform)_ghost.transform).sizeDelta = _rt.rect.size;
                _ghost.transform.SetAsLastSibling();
                _ghost.transform.position = e.position;
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (_ghost != null) _ghost.transform.position = e.position;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_ghost != null) { Destroy(_ghost); _ghost = null; }
            if (!_dragging) return;
            _dragging = false;
            if (!RectTransformUtility.RectangleContainsScreenPoint(_rt, e.position, null))
                InventorySystem.Instance.SetPotionSlot(index, 0);   // 拖出格子 → 解綁
        }

        public void OnPointerEnter(PointerEventData e) => Entered?.Invoke(BoundId);
        public void OnPointerExit(PointerEventData e) => Exited?.Invoke();
    }
}
