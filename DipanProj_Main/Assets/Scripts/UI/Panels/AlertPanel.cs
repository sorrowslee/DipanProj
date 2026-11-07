using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Inventory;
using Dipan.Localization;

namespace Dipan.UI
{
    /// <summary>
    /// 中央短訊息提示（toast）。專門用來顯示「拾取到 XXX」這類短暫訊息。
    ///
    /// 特性：HUD 層、**不暫停遊戲、不擋輸入、不鋪遮罩、不入 ESC 堆疊**——純表演層。
    /// 每則訊息撐約 <see cref="HoldSeconds"/> 秒後淡出自毀；短時間內多則往上疊（最新在最上）。
    ///
    /// 用法（任何系統）：<c>AlertPanel.Toast("獲得 銅錢 ×5");</c>
    /// 風格對齊專案：全程式建構、零 prefab/Inspector 接線。見 readme/UI_SYSTEM.md。
    /// </summary>
    public class AlertPanel : UIPanel
    {
        // ── 系統訊息 toast：不暫停、不擋輸入、不遮罩、ESC 不關、不入堆疊 ──
        // ⚠ 層是 System 不是 HUD（2026-08-22 改）：掛在 HUD 時會被背包等 Window 面板整個蓋住，
        //   玩家看到的是「點了沒反應」，關掉背包才發現訊息早就跳完了（見 PROBLEMS E18）。
        //   System 是字面上的最上層，所以**這裡的每個 Graphic 都必須 raycastTarget = false**
        //   （底板與文字都已設好），否則會擋掉底下視窗的點擊。
        public override UILayer Layer => UILayer.System;
        public override bool PausesGame => false;
        public override bool BlocksGameplayInput => false;
        public override bool ShowBackdrop => false;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;
        public override bool KeepOpenOnSceneChange => true;   // HUD 常駐，換場景不關（避免快取引用失效）

        const float HoldSeconds = 1.6f;   // 完全顯示的時間
        const float FadeSeconds = 0.4f;   // 之後淡出的時間（合計約 2 秒）

        RectTransform _stack;   // 垂直堆疊容器（置中）

        // ── 道具獲得提示（2026-11-07）：文字上方多一個道具圖示；滑鼠停在圖示上＝顯示道具說明、不消失 ──
        const float ItemHoldSeconds = 3.0f;    // 比一般訊息久：作者「收到禮物的提示真的很不明顯」
        const float ItemIconSize = 96f;
        const float ItemHoverGrace = 0.6f;     // 滑鼠移開後再撐一下才開始淡出（剛看完說明就瞬間消失會很突兀）
        const int TxtObtained = 8002;          // 「獲得 {0} ×{1}」
        ItemTooltip _itemTip;
        RectTransform _itemTipOwner;           // 目前 tooltip 是哪一則的圖示叫出來的
        int _itemDismissSerial;                // DismissItemToasts 每呼叫一次 +1；還活著的道具提示看到數字變了就立刻收掉

        /// <summary>
        /// 立刻收掉所有道具獲得提示（含滑鼠正停在圖示上、說明開著的那則）。
        /// 道具提示在 hover 時會一直撐著不消失，所以「要離開這裡了」的流程一定要叫這個——
        /// 目前是 <c>GameFlowManager</c> 開始卍字離場時（過關／死亡／返回都會經過）。換圖由提示自己偵測（見 ItemLifeRoutine）。
        /// </summary>
        public static void DismissItemToasts()
        {
            var p = UIManager.Instance != null ? UIManager.Instance.Get<AlertPanel>() : null;
            if (p != null) p._itemDismissSerial++;
        }

        /// <summary>
        /// 道具獲得提示：圖示在上、「獲得 名稱 ×數量」在下。停 <see cref="ItemHoldSeconds"/> 秒後淡出；
        /// **滑鼠移到圖示上**＝跳出道具說明（全遊戲共用的 <see cref="ItemTooltip"/>），期間訊息不消失，移開後才繼續倒數。
        /// 找不到道具資料時退回一般文字 toast。
        /// </summary>
        public static void ToastItem(int itemId, int count)
        {
            if (UIManager.Instance == null || itemId <= 0) return;
            var p = UIManager.Instance.Open<AlertPanel>();
            if (p != null) p.PushItem(itemId, Mathf.Max(1, count));
        }

        /// <summary>確保 AlertPanel 已開啟並推一則訊息。任何系統都可呼叫。</summary>
        public static void Toast(string message)
        {
            if (UIManager.Instance == null || string.IsNullOrEmpty(message)) return;
            var p = UIManager.Instance.Open<AlertPanel>();
            if (p != null) p.Push(message);
        }

        protected override void OnBuild()
        {
            // 置中、由下往上長的垂直容器；ContentSizeFitter 讓它隨訊息數量自動撐高。
            var go = UIBuilder.Create("ToastStack", transform);
            _stack = UIBuilder.Rect(go);
            _stack.anchorMin = _stack.anchorMax = _stack.pivot = new Vector2(0.5f, 0.5f);
            _stack.anchoredPosition = new Vector2(0f, 220f);   // 略高於正中央，不擋準星

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 8f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>推一則訊息（最新顯示在最上）。</summary>
        public void Push(string message)
        {
            if (_stack == null) return;

            // 一則 toast = 半透明深色底 + 文字，外加 HorizontalLayoutGroup+Fitter 自動貼合文字寬度。
            var toast = UIBuilder.Create("Toast", _stack);
            var bg = toast.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.72f);
            bg.raycastTarget = false;

            var hlg = toast.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(22, 22, 10, 10);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            // 不加 ContentSizeFitter：toast 由父層 _stack 的 VerticalLayoutGroup（childControlWidth/Height）
            // 依本 HLG 回報的 preferred 尺寸排版，避免 Fitter 與父 LayoutGroup 衝突。

            var cg = toast.AddComponent<CanvasGroup>();

            var t = UIBuilder.Text(toast.transform, "Msg", message, 28, Color.white, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;   // 不換行，靠 fitter 撐寬
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 28f;

            toast.transform.SetAsFirstSibling();   // 最新在最上
            StartCoroutine(LifeRoutine(toast, cg));
        }

        /// <summary>推一則道具獲得提示（見 <see cref="ToastItem"/>）。</summary>
        public void PushItem(int itemId, int count)
        {
            if (_stack == null) return;
            var inv = InventorySystem.Instance;
            var data = inv != null ? inv.GetData(itemId) : null;
            string name = data != null ? data.Name : $"#{itemId}";
            string fmt = Language.Has(TxtObtained) ? Language.GetText(TxtObtained) : "{0} ×{1}";
            string msg;
            try { msg = string.Format(fmt, name, count); } catch (System.FormatException) { msg = $"{name} ×{count}"; }
            if (data == null) { Push(msg); return; }

            var toast = UIBuilder.Create("ItemToast", _stack);
            var bg = toast.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.72f);
            bg.raycastTarget = false;   // System 層：一律不擋底下的點擊（hover 用游標位置自己判斷，見 ItemLifeRoutine）

            var vlg = toast.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(26, 26, 14, 12);
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            var cg = toast.AddComponent<CanvasGroup>();

            // 圖示：放進只占版面的容器，圖本身固定大小置中（VerticalLayoutGroup 會把直接子物件撐寬）
            var boxGO = UIBuilder.Create("IconBox", toast.transform);
            var boxLe = boxGO.AddComponent<LayoutElement>();
            boxLe.preferredHeight = boxLe.minHeight = ItemIconSize;
            boxLe.preferredWidth = boxLe.minWidth = ItemIconSize;
            var icon = UIBuilder.Image(boxGO.transform, "Icon", null, Color.white);
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(ItemIconSize, ItemIconSize);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            ItemIcons.Apply(icon, itemId);

            var t = UIBuilder.Text(toast.transform, "Msg", msg, 28, Color.white, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 28f;

            toast.transform.SetAsFirstSibling();
            if (_itemTip == null) _itemTip = ItemTooltip.Create(transform, UILayer.System);
            StartCoroutine(ItemLifeRoutine(toast, cg, irt, itemId));
        }

        // 道具提示的壽命：停留 → 淡出；任何時候滑鼠在圖示上 ⇒ 回到完全顯示、顯示道具說明、淡出中斷。
        // 不靠 raycast（System 層的 Graphic 全部 raycastTarget=false，免得擋住底下視窗），每幀用游標座標判斷。
        IEnumerator ItemLifeRoutine(GameObject toast, CanvasGroup cg, RectTransform icon, int itemId)
        {
            float held = 0f, fade = 0f;
            bool wasHover = false;
            int serial = _itemDismissSerial;
            var mm = MapManager.Instance;
            int mapId = mm != null ? mm.CurrentMapId : -1;
            while (toast != null)
            {
                // 強制收掉（2026-11-07 作者實測：看著說明時倒數到了、被傳回廣場，提示一直掛在畫面上）：
                // ① 離場流程叫了 DismissItemToasts ② 開始換圖／已經換到別張圖。
                if (serial != _itemDismissSerial) break;
                if (mm != null && (mm.IsLoading || mm.CurrentMapId != mapId)) break;

                bool hover = icon != null && RectTransformUtility.RectangleContainsScreenPoint(icon, Input.mousePosition, null);
                if (hover)
                {
                    if (!wasHover || _itemTipOwner != icon) { _itemTip.Show(itemId); _itemTipOwner = icon; }
                    _itemTip.Follow();
                    cg.alpha = 1f;
                    fade = 0f;
                    held = Mathf.Min(held, ItemHoldSeconds - ItemHoverGrace);   // 移開後至少再撐 ItemHoverGrace 秒
                }
                else
                {
                    if (wasHover && _itemTipOwner == icon) { _itemTip.Hide(); _itemTipOwner = null; }
                    if (held < ItemHoldSeconds) held += Time.unscaledDeltaTime;
                    else
                    {
                        fade += Time.unscaledDeltaTime;
                        cg.alpha = Mathf.Lerp(1f, 0f, fade / FadeSeconds);
                        if (fade >= FadeSeconds) break;
                    }
                }
                wasHover = hover;
                yield return null;
            }
            if (_itemTipOwner == icon && _itemTip != null) { _itemTip.Hide(); _itemTipOwner = null; }
            if (toast != null) Destroy(toast);
        }

        IEnumerator LifeRoutine(GameObject toast, CanvasGroup cg)
        {
            cg.alpha = 1f;
            float t = 0f;
            while (t < HoldSeconds) { t += Time.unscaledDeltaTime; yield return null; }

            t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                if (cg != null) cg.alpha = Mathf.Lerp(1f, 0f, t / FadeSeconds);
                yield return null;
            }
            if (toast != null) Destroy(toast);
        }
    }
}
