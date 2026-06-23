using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace Dipan.UI
{
    /// <summary>
    /// Code-driven uGUI 建構助手。把「new GameObject + AddComponent&lt;Image/Text/Button&gt; + 設 RectTransform」
    /// 這類重複樣板收成一組靜態方法，讓面板的 OnBuild() 能用很短的程式把版面拼出來。
    ///
    /// 設計呼應本專案既有風格：VfxManager / LaserBeam 都是「全程式建構、不需要 prefab、不需要 Inspector 接線」，
    /// UI 也比照辦理。所有圖檔走 Resources（與 WeaponSpritePath 等同套慣例）。
    /// </summary>
    public static class UIBuilder
    {
        static Font _defaultFont;

        /// <summary>內建字型（uGUI Text 必須有 Font）。用 Unity 內建的 LegacyRuntime.ttf，免額外匯入 TMP。</summary>
        public static Font DefaultFont
        {
            get
            {
                if (_defaultFont == null)
                {
                    // Unity 2022+：內建字型改名為 LegacyRuntime.ttf；舊名 Arial.ttf 作為後備。
                    _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_defaultFont == null) _defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return _defaultFont;
            }
        }

        // ───────────────────────── 基礎物件 ─────────────────────────

        /// <summary>建一個空的 UI 物件（帶 RectTransform）並掛到 parent 底下。</summary>
        public static GameObject Create(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>取 RectTransform（UI 物件的 transform 一定是 RectTransform）。</summary>
        public static RectTransform Rect(Component c) => (RectTransform)c.transform;

        /// <summary>取 RectTransform（GameObject 版）。</summary>
        public static RectTransform Rect(GameObject go) => (RectTransform)go.transform;

        // ───────────────────────── RectTransform 錨點助手 ─────────────────────────

        /// <summary>四邊拉伸貼齊父物件（可給內縮邊距）。常用於整頁背景、遮罩。</summary>
        public static RectTransform Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>置中、給定固定尺寸。常用於對話框、視窗主體。</summary>
        public static RectTransform Center(RectTransform rt, float width, float height, Vector2 offset = default)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        /// <summary>
        /// 通用錨點設定。anchor 用 0~1 的相對座標（(0,0)=左下、(1,1)=右上、(0.5,0.5)=中）。
        /// anchoredPos 是相對錨點的位移，size 是寬高（當 anchorMin==anchorMax 時才有意義）。
        /// </summary>
        public static RectTransform Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
                                           Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        // ───────────────────────── 圖像 ─────────────────────────

        /// <summary>建一個 Image（可帶 sprite 與顏色）。預設置中、原圖尺寸由呼叫端再設。</summary>
        public static Image Image(Transform parent, string name, Sprite sprite = null, Color? color = null)
        {
            var go = Create(name, parent);
            var img = go.AddComponent<Image>();
            if (sprite != null) img.sprite = sprite;
            img.color = color ?? Color.white;
            return img;
        }

        /// <summary>建一個純色面板（一張無 sprite 的 Image），預設四邊拉伸貼齊父物件。</summary>
        public static Image SolidPanel(Transform parent, string name, Color color)
        {
            var img = Image(parent, name, null, color);
            Stretch(img.rectTransform);
            return img;
        }

        // ───────────────────────── 文字 ─────────────────────────

        /// <summary>建一段文字。</summary>
        public static Text Text(Transform parent, string name, string content, int fontSize = 24,
                                Color? color = null, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = Create(name, parent);
            var t = go.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = content;
            t.fontSize = fontSize;
            t.color = color ?? Color.white;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        // ───────────────────────── 按鈕 ─────────────────────────

        /// <summary>建一個帶文字標籤的按鈕，並接好 onClick。回傳 Button（標籤可從 GetComponentInChildren&lt;Text&gt; 取得）。</summary>
        public static Button Button(Transform parent, string name, string label, UnityAction onClick,
                                    Color? bgColor = null, Sprite bgSprite = null)
        {
            var go = Create(name, parent);
            var img = go.AddComponent<Image>();
            img.color = bgColor ?? new Color(0.25f, 0.25f, 0.3f, 1f);
            if (bgSprite != null) img.sprite = bgSprite;

            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(onClick);

            if (!string.IsNullOrEmpty(label))
            {
                var t = Text(go.transform, "Label", label, 22, Color.white, TextAnchor.MiddleCenter);
                Stretch(t.rectTransform);
            }
            return btn;
        }

        // ───────────────────────── 輸入框 ─────────────────────────

        /// <summary>
        /// 建一個 legacy uGUI InputField（含背景、文字、placeholder），不依賴 TMP。
        /// 單行；characterLimit &gt; 0 時限制字數。回傳 InputField（讀取用 .text）。
        /// </summary>
        public static UnityEngine.UI.InputField InputField(Transform parent, string name, string placeholder,
                                            int fontSize = 22, int characterLimit = 16, Color? bgColor = null)
        {
            var go = Create(name, parent);
            var img = go.AddComponent<Image>();
            img.color = bgColor ?? new Color(1f, 1f, 1f, 0.10f);

            // 注意：本方法名與型別 InputField 同名，方法內存取型別成員必須用完整命名空間，否則簡單名會被當成「方法」(CS0119)。
            var input = go.AddComponent<UnityEngine.UI.InputField>();
            input.lineType = UnityEngine.UI.InputField.LineType.SingleLine;
            input.characterLimit = Mathf.Max(0, characterLimit);

            var text = Text(go.transform, "Text", "", fontSize, Color.white, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform, 12, 12, 4, 4);
            text.raycastTarget = true;
            text.supportRichText = false;

            var ph = Text(go.transform, "Placeholder", placeholder, fontSize,
                          new Color(1f, 1f, 1f, 0.4f), TextAnchor.MiddleLeft);
            Stretch(ph.rectTransform, 12, 12, 4, 4);
            ph.fontStyle = FontStyle.Italic;

            input.textComponent = text;
            input.placeholder = ph;
            input.text = "";
            return input;
        }

        // ───────────────────────── 雜項 ─────────────────────────

        /// <summary>從 Resources 載一張 Sprite（路徑不含副檔名）。找不到會印 Warning。</summary>
        public static Sprite LoadSprite(string resourcesPath)
        {
            if (string.IsNullOrEmpty(resourcesPath)) return null;
            var s = Resources.Load<Sprite>(resourcesPath);
            if (s == null) Debug.LogWarning($"[UIBuilder] 找不到 Sprite：Resources/{resourcesPath}");
            return s;
        }
    }
}
