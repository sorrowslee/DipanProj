using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 鍛造介面用的單格元件（鐵砧中央那一格、以及六個鑲嵌孔共用同一個類別）。
    /// 實作 ISlotView，重用共用拖放（SlotDragController / InventoryActions），跨面板與背包天生互通。
    ///
    /// 與 ScriptSlotWidget 的差別：
    /// - 接受條件做成可注入的 <see cref="Accepts"/>（鐵砧＝可裝備物；鑲嵌孔＝將來的寶石），不寫死。
    /// - 多一個**上鎖**狀態：上鎖時蓋一張鎖鏈圖、既不收拖放也不能把東西拖出去。
    ///
    /// 見 readme/FORGING.md。
    /// </summary>
    public class ForgeSlotWidget : MonoBehaviour, ISlotView,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        public IItemGrid Container { get; private set; }
        public int Index { get; private set; }

        Image _icon;
        Image _dropHi;       // 拖曳中「這格可以放」的黃色高亮
        bool _locked;

        /// <summary>
        /// 上鎖／未上鎖時要顯示的外框圖（由面板建好後掛進來，兩張都放在這格「後方」當同層兄弟）。
        /// 鎖鏈不再是疊在格子上的獨立圖層，而是**外框圖本身有兩個版本**（ForgingPanel_ItemFrame /
        /// ForgingPanel_ItemFrameWithChain），切換時只是把兩張圖互相 enable/disable。
        /// 兩張都可以是 null（例如鐵砧中央那格永遠不會上鎖，就只掛 frameUnlocked）。
        /// </summary>
        Image _frameUnlocked, _frameLocked;

        /// <summary>這一格收不收某個物品。null = 全收。由建立者注入（鐵砧＝可裝備物）。</summary>
        public Func<ItemData, bool> Accepts;

        /// <summary>滑鼠移入／移出（給面板顯示 tooltip 用；傳目前格內的物品 ID，空格傳 0）。</summary>
        public Action<int> Entered, Exited;

        /// <summary>拖了不該放的東西進來時呼叫（給面板跳提示；null = 靜靜忽略）。</summary>
        public Action Rejected;

        /// <summary>左鍵點這一格（給鐵砧做「取下」；null = 點了沒事）。</summary>
        public Action Clicked;

        /// <summary>
        /// 收下時改由呼叫端自己處理（null = 走預設的共用搬運，真的把物品搬進本格的容器）。
        /// 鐵砧設了這個 → 改成「只記住來源、完全不搬動物品」，見 <see cref="ForgeAnvilSlot"/>。
        /// </summary>
        public Action<ISlotView> CustomDrop;

        /// <summary>
        /// 能不能從這一格把東西拖出去。**鐵砧要設成 false**——它只是借放、沒有真的持有物品，
        /// 讓它當拖曳來源會被共用搬運當成「搬出去」，結果是把物品憑空複製一份到背包。
        /// </summary>
        public bool CanDragOut = true;

        // ── ISlotView ──
        public IItemGrid Grid => Container;
        public int GridIndex => Index;
        public bool IsEquip => false;
        public EquipSlot Equip => EquipSlot.None;
        public RectTransform Rt => (RectTransform)transform;
        public Sprite DragIcon() => (!_locked && _icon != null && _icon.enabled) ? _icon.sprite : null;

        /// <summary>上鎖 = 換成有鎖鏈的外框、不收也不放。</summary>
        public bool Locked
        {
            get => _locked;
            set
            {
                _locked = value;
                ApplyFrameArt();
                Refresh();
            }
        }

        /// <summary>
        /// 掛上這一格的兩張外框圖（未上鎖／上鎖）。由面板建好、擺好位置後呼叫；傳 null 代表沒有那個狀態的圖。
        /// </summary>
        public void SetFrameArt(Image unlocked, Image locked)
        {
            _frameUnlocked = unlocked;
            _frameLocked = locked;
            ApplyFrameArt();
        }

        void ApplyFrameArt()
        {
            if (_frameUnlocked != null) _frameUnlocked.enabled = !_locked && _frameUnlocked.sprite != null;
            if (_frameLocked != null) _frameLocked.enabled = _locked && _frameLocked.sprite != null;
        }

        /// <summary>建一格。size = 格子命中範圍（＝外框方框大小），iconSize = 物品圖顯示大小。</summary>
        public static ForgeSlotWidget Create(Transform parent, string name, float size, float iconSize)
        {
            var go = UIBuilder.Create(name, parent);
            var rt = UIBuilder.Rect(go);
            rt.sizeDelta = new Vector2(size, size);

            var hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);   // 透明命中區：外框由底圖／框圖負責，這格只收點擊與拖放
            hit.raycastTarget = true;

            var w = go.AddComponent<ForgeSlotWidget>();

            var dropHi = UIBuilder.Image(go.transform, "DropHi", null, new Color(1f, 0.82f, 0.3f, 0.28f));
            UIBuilder.Stretch(dropHi.rectTransform, 6, 6, 6, 6);
            dropHi.raycastTarget = false;
            dropHi.enabled = false;
            w._dropHi = dropHi;

            var icon = UIBuilder.Image(go.transform, "Icon", null, Color.white);
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(iconSize, iconSize);
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
            if (_locked || st.IsEmpty)
            {
                if (_icon != null) _icon.enabled = false;
                return;
            }
            // 珠子是「珠身＋能力符號」兩層，一律走 ItemIcons（見 readme/GEM_SOCKET.md）
            ItemIcons.Apply(_icon, st);
        }

        /// <summary>拖曳中把「這格收得下」亮起來（面板統一呼叫；itemId=0 代表沒在拖，關掉高亮）。</summary>
        public void SetDropHighlight(int itemId)
        {
            if (_dropHi == null) return;
            if (_locked || itemId <= 0 || Container == null || !Container.GetAt(Index).IsEmpty)
            { _dropHi.enabled = false; return; }
            _dropHi.enabled = CanTake(itemId);
        }

        bool CanTake(int itemId)
        {
            if (_locked) return false;
            var d = InventorySystem.Instance != null ? InventorySystem.Instance.GetData(itemId) : null;
            if (d == null) return false;
            return Accepts == null || Accepts(d);
        }

        // ── hover（給面板做 tooltip）──
        public void OnPointerEnter(PointerEventData e) =>
            Entered?.Invoke(_locked ? 0 : (Container != null ? Container.GetAt(Index).ItemId : 0));

        public void OnPointerExit(PointerEventData e) => Exited?.Invoke(0);

        // ── 左鍵點擊（鐵砧＝取下）──
        public void OnPointerClick(PointerEventData e)
        {
            if (_locked || e.button != PointerEventData.InputButton.Left) return;
            Clicked?.Invoke();
        }

        // ── 拖出（把東西拖回背包）──CanDragOut=false 的格子完全不當拖曳來源。
        public void OnBeginDrag(PointerEventData e)
        {
            if (_locked || !CanDragOut) return;
            SlotDragController.Begin(this, e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_locked || !CanDragOut) return;
            SlotDragController.Drag(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_locked || !CanDragOut) return;
            SlotDragController.End(this);
        }

        // ── 收下（從背包拖進來）──先驗證再交給共用搬運，不合就直接忽略（來源會留在原位，不會消失）。
        public void OnDrop(PointerEventData e)
        {
            if (_locked) return;
            var src = e.pointerDrag != null ? e.pointerDrag.GetComponent<ISlotView>() : null;
            if (src == null) return;
            int id = InventoryActions.ItemIdOf(src);
            if (!CanTake(id))
            {
                if (id > 0) Rejected?.Invoke();
                return;
            }
            if (CustomDrop != null) CustomDrop(src);          // 鐵砧：只記來源，不搬物品
            else SlotDragController.Drop(this, e);            // 一般格：走共用搬運
        }
    }
}
