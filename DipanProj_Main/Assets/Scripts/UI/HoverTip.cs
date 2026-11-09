using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Dipan.Localization;

namespace Dipan.UI
{
    /// <summary>
    /// **通用文字小提示（hover tip）—— 全遊戲共用一份**（2026-11-09）。
    /// 滑鼠停在某個 UI 元件上一小段時間，元件上方浮出一行字（例：倉庫重整鈕的「重新整理」）。
    ///
    /// 用法（一行）：
    /// <code>
    ///   HoverTip.Attach(button.gameObject, 9001);          // 語言表 id（建議；切語言自動跟著換）
    ///   HoverTip.Attach(button.gameObject, () => "動態字");  // 內容會變的場合
    /// </code>
    ///
    /// 與 <see cref="ItemTooltip"/> 的分工：ItemTooltip 是**物品說明**（大圖＋名稱＋屬性、跟著游標）；
    /// 這個是**一行短字**、錨在元件正上方、不跟游標。要說明物品一律用 ItemTooltip。
    ///
    /// 行為：
    /// - 停留 <see cref="Delay"/> 秒才出現（用 unscaled time——背包會把 timeScale 設 0）。
    /// - 移開、按下、元件被停用／銷毀（面板關閉）⇒ 立刻收起。**面板不用自己在 OnClose 呼叫 Hide**。
    /// - 一律開在元件上方；上方超出畫面才改到下方；左右夾在畫面內。
    /// - 掛在 <see cref="UILayer.Popup"/> 層（同 ItemTooltip，理由見它的 Create 註解／PROBLEMS E34），本體 raycastTarget 全關，絕不擋 hover。
    ///
    /// 目標元件要能接 pointer 事件（本身或子物件有 raycastTarget=true 的 Graphic，Button 都有）。
    /// static 只存單一 UnityEngine.Object，停 Play 後 `== null` 會自動重建（關 Domain Reload 也安全，見 PROBLEMS I2/I8）。
    /// </summary>
    public static class HoverTip
    {
        // ══════════════════ 外觀（要調大小顏色改這一區）══════════════════

        public const float Delay = 0.3f;              // 停留多久才出現（秒）
        const int FontSize = 24;
        const int PadX = 16, PadY = 8;
        const float Gap = 10f;                        // 與元件的距離
        const float LineThickness = 2f;
        static readonly Color BgColor = new Color(0.07f, 0.055f, 0.04f, 0.95f);    // 深石面（UI 規範 #16130D 一帶）
        static readonly Color LineColor = new Color(0.69f, 0.54f, 0.33f, 1f);      // 細暗古金（#B08A55）
        static readonly Color TextColor = new Color(0.91f, 0.86f, 0.77f, 1f);      // 淡骨色

        // ══════════════════ 狀態（單一份）══════════════════

        static RectTransform _root;
        static RectTransform _layer;
        static Text _text;
        static HoverTipTrigger _owner;   // 目前顯示的是誰的（別人的 Hide 不能把它收掉）

        // ══════════════════ 對外 ══════════════════

        /// <summary>掛一個以語言表 id 取字的提示。重複呼叫會覆寫同一個元件上的設定。</summary>
        public static HoverTipTrigger Attach(GameObject target, int langId)
        {
            var t = Get(target);
            t.LangId = langId; t.TextFn = null;
            return t;
        }

        /// <summary>掛一個內容由呼叫端決定的提示（每次顯示時才呼叫，回傳空字串＝不顯示）。</summary>
        public static HoverTipTrigger Attach(GameObject target, Func<string> text)
        {
            var t = Get(target);
            t.LangId = 0; t.TextFn = text;
            return t;
        }

        /// <summary>強制收起（一般用不到；換場景之類的流程想保險可以叫）。</summary>
        public static void HideAll()
        {
            _owner = null;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        static HoverTipTrigger Get(GameObject target)
        {
            var t = target.GetComponent<HoverTipTrigger>();
            return t != null ? t : target.AddComponent<HoverTipTrigger>();
        }

        // ══════════════════ 給 trigger 用 ══════════════════

        internal static void Show(HoverTipTrigger owner, string text)
        {
            if (string.IsNullOrEmpty(text) || owner == null) return;
            if (!Ensure()) return;
            _owner = owner;
            _text.text = text;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_root);
            Place((RectTransform)owner.transform);
        }

        internal static void Hide(HoverTipTrigger owner)
        {
            if (_owner != owner) return;
            HideAll();
        }

        // ══════════════════ 內部 ══════════════════

        static bool Ensure()
        {
            if (_root != null) return true;
            var mgr = UIManager.Instance;
            var parent = mgr != null ? mgr.LayerRoot(UILayer.Popup) : null;
            if (parent == null) return false;
            _layer = (RectTransform)parent;

            var go = UIBuilder.Create("HoverTip", parent);
            _root = UIBuilder.Rect(go);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);

            var bg = go.AddComponent<Image>();
            bg.color = BgColor;
            bg.raycastTarget = false;

            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(PadX, PadX, PadY, PadY);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _text = UIBuilder.Text(go.transform, "Text", "", FontSize, TextColor, TextAnchor.MiddleCenter);
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;   // 一行字，寬度由字決定（要換行就在字串放 \n）

            var line = SlotOutline.Create(go.transform, "Line", LineColor, LineThickness);
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;   // 不參與排版，貼滿外框

            go.SetActive(false);
            return true;
        }

        static Camera CamOf(Component c)
        {
            var cv = c.GetComponentInParent<Canvas>();
            if (cv == null) return null;
            cv = cv.rootCanvas;
            return cv.renderMode == RenderMode.ScreenSpaceOverlay ? null : cv.worldCamera;
        }

        static void Place(RectTransform target)
        {
            var c = new Vector3[4];
            target.GetWorldCorners(c);                       // 0 左下、1 左上、2 右上、3 右下
            Camera tc = CamOf(target), lc = CamOf(_layer);
            Vector2 top = RectTransformUtility.WorldToScreenPoint(tc, (c[1] + c[2]) * 0.5f);
            Vector2 bot = RectTransformUtility.WorldToScreenPoint(tc, (c[0] + c[3]) * 0.5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, top, lc, out Vector2 lt);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, bot, lc, out Vector2 lb);

            var r = _layer.rect;
            float w = _root.rect.width, hgt = _root.rect.height;

            bool above = lt.y + Gap + hgt <= r.yMax;         // 上方放得下就放上方，否則放下方
            _root.pivot = new Vector2(0.5f, above ? 0f : 1f);
            float y = above ? lt.y + Gap : lb.y - Gap;
            float x = Mathf.Clamp(lt.x, r.xMin + w * 0.5f, r.xMax - w * 0.5f);
            _root.anchoredPosition = new Vector2(x, y);
        }
    }

    /// <summary>
    /// 掛在目標元件上的觸發器（由 <see cref="HoverTip.Attach(GameObject,int)"/> 自動加，不用手動掛）。
    /// </summary>
    public class HoverTipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        public int LangId;
        public Func<string> TextFn;

        bool _hover, _shown;
        float _t;

        public void OnPointerEnter(PointerEventData e) { _hover = true; _shown = false; _t = 0f; }
        public void OnPointerExit(PointerEventData e) { _hover = false; Close(); }
        public void OnPointerDown(PointerEventData e) { _hover = false; Close(); }   // 按下就收，直到滑鼠離開再回來

        void Update()
        {
            if (!_hover || _shown) return;
            _t += Time.unscaledDeltaTime;
            if (_t < HoverTip.Delay) return;
            _shown = true;
            string s = TextFn != null ? TextFn() : (LangId != 0 ? Language.GetText(LangId) : null);
            HoverTip.Show(this, s);
        }

        void OnDisable() { _hover = false; Close(); }

        void Close()
        {
            if (_shown) HoverTip.Hide(this);
            _shown = false;
        }
    }
}
