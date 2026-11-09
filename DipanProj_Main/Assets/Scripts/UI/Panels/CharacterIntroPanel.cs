using UnityEngine;
using UnityEngine.UI;
using Dipan.Drama;

namespace Dipan.UI
{
    /// <summary>
    /// 角色介紹面板（劇情演出 <c>intro</c> 步驟專用，2026-10-28；見 readme/CUTSCENE_DIRECTOR.md §10）。
    ///
    /// 版面（作者 2026-10-28 示意圖）：壓黑背景 → 上下裝飾線（同一張圖、水平置中）→ 立繪（一側、放右邊時鏡像）
    /// → 另一側的文字欄：姓名版（稱號＋名字）＋說明上下裝飾線之間的置中說明文字。
    ///
    /// <para><b>不是 <see cref="UIPanel"/></b>：同劇情的置中漫畫／Skip，自建一張 ScreenSpaceOverlay Canvas、
    /// 由 <c>CutsceneDirector</c> 開關並掛在 director 底下（演出被硬銷毀時一起消失，不會殘留在畫面上）。
    /// 不暫停遊戲、不吃滑鼠（沒有 GraphicRaycaster），所以右上角的演出 Skip 照樣按得到。</para>
    ///
    /// <para><b>本身沒有進出場動畫</b>（劇情 intro：作者先暫定沒有）。但開了兩個給呼叫端驅動的把手
    /// （2026-11-09，Boss 開戰資訊改用本版面時加的）：<see cref="Alpha"/>＝整張淡入淡出、
    /// <see cref="SetNameReveal"/>＝名字扭曲浮現（<see cref="NameWarpEffect"/>）。不呼叫就跟原本一模一樣。
    /// 呼叫端一律用 unscaled 時間驅動。</para>
    ///
    /// <para><b>兩個使用者</b>：劇情 <c>intro</c>（CutsceneDirector，壓黑背景、立繪可左可右）、
    /// Boss 開戰資訊（<see cref="BossIntroPanel"/>，**不壓黑**——底下的黑霧就是背景；立繪固定在左）。</para>
    ///
    /// <para>⚠ 所有版面數字都是「參考解析度 1920×1080」下的像素，從示意圖（1672×941）×1.148 換算；
    /// 要調整只改下面的常數。背景透明度在 Linear 色彩空間下會比直覺重（PROBLEMS E11），要進遊戲看。</para>
    /// </summary>
    public class CharacterIntroPanel : MonoBehaviour
    {
        // ── 層級 ──
        const int SortingOrder = 5050;   // 置中漫畫(5000)之上、演出 Skip(5100)之下：介紹期間仍按得到 Skip

        // ── 素材 ──
        const string ArtRoot = "UI/CharacterIntro/";
        const string OrnamentArt = ArtRoot + "CharacterIntro_top_bottom_ornament";   // 上下裝飾線共用一張
        const string NameplateArt = ArtRoot + "CharacterIntro_nameplate";
        const string DescTopArt = ArtRoot + "CharacterIntro_description_top";
        const string DescBottomArt = ArtRoot + "CharacterIntro_description_bottom";
        const string FontRegular = "Fonts/NotoSerifTC/NotoSerifTC-Regular";   // 思源宋體（SIL OFL，可商用）
        const string FontBold = "Fonts/NotoSerifTC/NotoSerifTC-Bold";

        // ── 背景與文字顏色 ──
        // 壓黑後場景「看起來」剩幾成亮度（人眼看到的，0＝全黑）。
        // ⚠ 不要直接填 alpha：專案是 Linear 色彩空間（PROBLEMS E11），黑色疊 α 的實際效果是亮度剩 (1−α)^(1/2.2)——
        //   2026-10-28 首版填 α 0.85，看起來只壓到 42%，作者回報文字看不清楚。這裡填目標亮度，由 BackdropAlpha() 換算。
        const float BackdropBrightness = 0.17f;   // ≈ Linear 下 α 0.98
        static readonly Color TextColor = new Color(0.94f, 0.90f, 0.82f, 1f);   // 米白（示意圖）

        // ── 上下裝飾線（水平置中、不隨左右換邊）──
        const float OrnamentTopY = 37f;      // 中心距畫面頂
        const float OrnamentBottomY = 60f;   // 中心距畫面底
        const float OrnamentScale = 1f;      // 原圖 1100×57

        // ── 文字欄（姓名版＋說明）：水平中心＝畫面寬的這個比例（立繪在左時；立繪在右時取 1 − 它）──
        const float TextColumnX = 0.74f;

        // 姓名版（原圖 808×309；內框上緣約 y=92、分隔線約 y=146、下緣約 y=280，皆從圖檔頂端量）
        const float NameplateY = 360f;       // 姓名版中心距畫面頂
        const float NameplateScale = 1f;
        const float TitleYInPlate = 118f;    // 稱號中心（圖檔像素，從頂端量）
        const int TitleFontSize = 32;
        const string TitleGap = "  ";        // 稱號字距（中日韓字之間插的空白；英文不插）
        const float NameYInPlate = 217f;     // 名字中心
        const int NameFontSize = 84;
        const int NameMinFontSize = 40;      // 名字太長時自動縮到最小這麼大（單行）
        const string NameGap = " ";
        const float PlateTextWidth = 600f;   // 稱號／名字可用寬度（圖檔像素）

        // 說明欄（兩條裝飾線之間放置中文字）
        const float DescTopLineY = 534f;     // 上裝飾線中心距畫面頂
        const float DescBottomLineY = 878f;  // 下裝飾線中心距畫面頂
        const float DescWidth = 640f;
        const float DescPadding = 30f;       // 文字框上下各離裝飾線多遠
        const int DescFontSize = 32;
        const float DescLineSpacing = 1.45f;

        // ── 立繪：量的是「人物」（不透明內容框，PortraitFit），不是圖檔外框 ──
        const float PortraitX = 0.30f;       // 人物水平中心＝畫面寬的比例（立繪在左；在右取 1 − 它）；會被下面的可用區夾住
        const float PortraitTopY = 70f;      // 人物頂端距畫面頂
        const float PortraitHeight = 1040f;  // 人物高度（直幅立繪：超過畫面底的部分會被裁掉，示意圖就是這樣）
        // 寬幅立繪（2026-10-28 夏綠蒂重製後是 1536×1024、人物寬高比 1.36~1.47）照 PortraitHeight 放會寬到 1400 多。
        // 作者 2026-10-28：**跟其他角色同比例（同人物高度）優先**，身體跑出畫面外側一點沒關係 ⇒
        //   內側緣絕不壓到文字欄；外側緣可以超出畫面，最多人物寬的 PortraitMaxOffscreen；還放不下才等比縮小。
        const float PortraitEdgeMargin = 24f;      // 人物外側緣距畫面邊的留白（窄立繪用；寬立繪會超出去）
        const float PortraitTextGap = 24f;         // 人物內側緣距文字欄（姓名版／說明的外框）至少多少
        const float PortraitMaxOffscreen = 0.4f;   // 寬幅立繪的人物寬，最多這個比例可以在畫面外側（夏綠蒂四張都能維持同高）
        const float PortraitBottomMargin = 20f;    // 還是得縮小時：人物底緣距畫面底

        static Font _regular, _bold;

        CanvasGroup _group;          // 整張淡入淡出（Alpha）
        Text _nameText;              // 名字（SetNameReveal 用）
        NameWarpEffect _warp;        // 名字扭曲（Strength=0 時零開銷；劇情 intro 不會去動它）

        /// <summary>整張面板的不透明度（0~1）。預設 1。</summary>
        public float Alpha
        {
            get => _group != null ? _group.alpha : 1f;
            set { if (_group != null) _group.alpha = Mathf.Clamp01(value); }
        }

        /// <summary>
        /// 名字浮現進度：<paramref name="e"/>＝0 全透明＋滿扭曲、1 完全浮現且字形歸位。
        /// 扭曲要「動」得每幀呼叫（本方法會 SetVerticesDirty）。<paramref name="warpAmount"/> 是 e=0 時的扭曲強度（參考解析度像素）。
        /// </summary>
        public void SetNameReveal(float e, float warpAmount, float speed)
        {
            if (_nameText == null) return;
            e = Mathf.Clamp01(e);
            var c = _nameText.color; c.a = e; _nameText.color = c;
            if (_warp != null)
            {
                _warp.Strength = (1f - e) * Mathf.Max(0f, warpAmount);
                _warp.Speed = speed;
            }
            _nameText.SetVerticesDirty();
        }

        /// <summary>
        /// 建一張介紹面板並顯示。<paramref name="portraitOnRight"/>＝立繪在右（自動鏡像），文字欄在左。
        /// <paramref name="fit"/> 只用它的內容框（人物在圖檔裡的位置大小）；<c>PortraitTable</c> 的縮放／位移
        /// 是 TalkPanel 版面調出來的，這裡**刻意不套**（作者 2026-10-28：先照統一規則擺，看結果再決定要不要另開欄位）。
        /// </summary>
        public static CharacterIntroPanel Show(Transform parent, Sprite portrait, PortraitFit fit, bool portraitOnRight,
                                               string title, string name, string desc)
            => Show(parent, portrait, fit, portraitOnRight, title, name, desc, true);

        /// <summary>
        /// 同上，多一個 <paramref name="backdrop"/>：false＝不鋪壓黑背景（Boss 開戰資訊：底下已經是黑霧）。
        /// ⚠ <paramref name="parent"/> 不要給另一張 Canvas 底下的物件——巢狀 Canvas 會忽略本面板自己的
        /// ScreenSpaceOverlay／sortingOrder／CanvasScaler。沒有合適的父物件就給 null（掛在場景根）。
        /// </summary>
        public static CharacterIntroPanel Show(Transform parent, Sprite portrait, PortraitFit fit, bool portraitOnRight,
                                               string title, string name, string desc, bool backdrop)
        {
            if (_regular == null) _regular = UIBuilder.LoadFont(FontRegular);
            if (_bold == null) _bold = UIBuilder.LoadFont(FontBold);

            var go = new GameObject("[CharacterIntro]");
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;   // 同 UIManager／SkipButton

            var panel = go.AddComponent<CharacterIntroPanel>();
            panel._group = go.AddComponent<CanvasGroup>();
            panel._group.interactable = false;
            panel._group.blocksRaycasts = false;
            panel.Build(go.transform, portrait, fit, portraitOnRight, title, name, desc, backdrop);
            return panel;
        }

        void Build(Transform root, Sprite portrait, PortraitFit fit, bool portraitOnRight, string title, string name, string desc, bool backdrop)
        {
            // 1) 壓黑背景（Boss 開戰資訊不鋪：底下的黑霧就是背景）
            if (backdrop)
            {
                var bg = NewRect("Backdrop", root);
                bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one;
                bg.offsetMin = bg.offsetMax = Vector2.zero;
                var bgImg = bg.gameObject.AddComponent<Image>();
                bgImg.color = new Color(0f, 0f, 0f, BackdropAlpha());
                bgImg.raycastTarget = false;
            }

            // 2) 上下裝飾線（立繪會蓋在它們上面，示意圖如此）
            var orn = Resources.Load<Sprite>(OrnamentArt);
            ArtImage("OrnamentTop", root, orn, new Vector2(0.5f, 1f), new Vector2(0f, -OrnamentTopY), OrnamentScale);
            ArtImage("OrnamentBottom", root, orn, new Vector2(0.5f, 0f), new Vector2(0f, OrnamentBottomY), OrnamentScale);

            // 3) 立繪（要先知道文字欄佔多寬，才能算立繪可用區）
            float colX = portraitOnRight ? 1f - TextColumnX : TextColumnX;
            var plateSp = Resources.Load<Sprite>(NameplateArt);
            float textHalf = Mathf.Max(plateSp != null ? plateSp.rect.width * NameplateScale * 0.5f : 404f, DescWidth * 0.5f);
            if (portrait != null) BuildPortrait(root, portrait, fit, portraitOnRight, colX, textHalf);

            // 4) 文字欄
            var plate = ArtImage("Nameplate", root, plateSp,
                                 new Vector2(colX, 1f), new Vector2(0f, -NameplateY), NameplateScale);
            if (plate != null)
            {
                var pr = (RectTransform)plate.transform;
                Label("Title", pr, Spaced(title, TitleGap), _regular, TitleFontSize, 0,
                      new Vector2(0f, -TitleYInPlate * NameplateScale), new Vector2(PlateTextWidth * NameplateScale, TitleFontSize * 1.6f), false);
                var nm = Label("Name", pr, Spaced(name, NameGap), _bold, NameFontSize, NameMinFontSize,
                      new Vector2(0f, -NameYInPlate * NameplateScale), new Vector2(PlateTextWidth * NameplateScale, NameFontSize * 1.25f), false);
                _nameText = nm;
                // ⚠ 扭曲一定要比 Shadow 先掛：mesh effect 照元件順序跑，先扭再複製影子，影子才會跟著字一起扭
                //   （反過來的話影子的頂點也被當成「字」各自亂扭，字和影子會分家）。
                _warp = nm.gameObject.AddComponent<NameWarpEffect>();
                var sh = nm.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.8f);
                sh.effectDistance = new Vector2(2f, -3f);
            }

            ArtImage("DescTop", root, Resources.Load<Sprite>(DescTopArt), new Vector2(colX, 1f), new Vector2(0f, -DescTopLineY), 1f);
            ArtImage("DescBottom", root, Resources.Load<Sprite>(DescBottomArt), new Vector2(colX, 1f), new Vector2(0f, -DescBottomLineY), 1f);

            var descRt = NewRect("Desc", root);
            descRt.anchorMin = descRt.anchorMax = new Vector2(colX, 1f);
            descRt.pivot = new Vector2(0.5f, 0.5f);
            descRt.anchoredPosition = new Vector2(0f, -(DescTopLineY + DescBottomLineY) * 0.5f);
            descRt.sizeDelta = new Vector2(DescWidth, DescBottomLineY - DescTopLineY - DescPadding * 2f);
            var dt = descRt.gameObject.AddComponent<Text>();
            SetupText(dt, (desc ?? "").Replace("\\n", "\n"), _regular, DescFontSize);
            dt.lineSpacing = DescLineSpacing;
            dt.horizontalOverflow = HorizontalWrapMode.Wrap;
            dt.verticalOverflow = VerticalWrapMode.Overflow;
        }

        /// <summary>
        /// 立繪排版：人物（不透明內容框）頂端距畫面頂 <see cref="PortraitTopY"/>、高 <see cref="PortraitHeight"/>、
        /// 水平中心在畫面寬 <see cref="PortraitX"/>（右側取 1 − 它）。圖檔的留白多寡、畫布比例都被吸收掉
        /// （同 TalkPanel〈立繪自動對齊〉，readme/DRAMA.md）。
        /// <para><b>寬幅立繪</b>：人物寬度不得超出「立繪可用區」＝畫面外側邊（<see cref="PortraitEdgeMargin"/>）到
        /// 文字欄外框（<see cref="PortraitTextGap"/>）之間；超過就等比縮小、腳底改貼近畫面底，水平中心也夾在可用區內。
        /// 直幅立繪（艾爾文那種）寬度本來就放得下，行為不變。</para>
        /// 右側一律水平鏡像（立繪原圖臉朝右；放右邊翻成朝左，兩側都面向畫面中央的文字）。
        /// </summary>
        void BuildPortrait(Transform root, Sprite sp, PortraitFit fit, bool onRight, float colX, float textHalf)
        {
            Vector2 px = sp.rect.size;   // 圖檔像素
            // 內容框佔圖檔的比例、內容中心相對圖檔中心的位移比例（fit 的單位不重要，只用比值）
            Vector2 frac = Vector2.one, centerFrac = Vector2.zero;
            if (fit.ok && fit.canvas.x > 0f && fit.canvas.y > 0f && fit.content.y > 0f)
            {
                frac = new Vector2(fit.content.x / fit.canvas.x, fit.content.y / fit.canvas.y);
                centerFrac = new Vector2(fit.center.x / fit.canvas.x, fit.center.y / fit.canvas.y);
            }
            // 畫布（CanvasScaler 參考單位）的實際寬高：match 0.5 時不是固定 1920×1080，寬螢幕會更寬
            Vector2 cv = CanvasSize();
            float colPx = colX * cv.x;
            // 可用區：內側到文字欄外框為止（硬限制）；外側先以畫面邊留白為準，寬立繪再往畫面外延伸
            float lo = onRight ? colPx + textHalf + PortraitTextGap : PortraitEdgeMargin;
            float hi = onRight ? cv.x - PortraitEdgeMargin : colPx - textHalf - PortraitTextGap;
            float off = Mathf.Clamp(PortraitMaxOffscreen, 0f, 0.9f);
            float maxW = Mathf.Max(1f, hi - lo) / (1f - off);       // 人物寬 w 時外側可多出 off×w ⇒ w(1−off) ≤ 可用寬

            float aspect = (frac.x * px.x) / Mathf.Max(1f, frac.y * px.y);   // 人物寬高比
            float personH = PortraitHeight, personW = personH * aspect;
            if (personW > maxW) { personW = maxW; personH = personW / aspect; }   // 連超出畫面都放不下：等比縮小
            if (onRight) hi += off * personW; else lo -= off * personW;          // 外側允許超出畫面
            float top = Mathf.Max(PortraitTopY, cv.y - PortraitBottomMargin - personH);   // 縮小了就讓腳站回下方
            float cx = Mathf.Clamp((onRight ? 1f - PortraitX : PortraitX) * cv.x, lo + personW * 0.5f, hi - personW * 0.5f);

            float dispH = personH / frac.y;                        // 整張圖檔要顯示多高，人物才會是 personH
            var size = new Vector2(dispH * px.x / Mathf.Max(1f, px.y), dispH);
            Vector2 contentOff = new Vector2(centerFrac.x * size.x, centerFrac.y * size.y);
            if (onRight) contentOff.x = -contentOff.x;              // 鏡像後內容中心也跟著翻

            var rt = NewRect("Portrait", root);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);     // 左上角為原點，x 用上面算好的畫布座標
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            // 人物中心要落在 (cx, −(top + H/2))；圖檔中心＝人物中心 − 內容位移
            rt.anchoredPosition = new Vector2(cx, -(top + personH * 0.5f)) - contentOff;
            if (onRight) rt.localScale = new Vector3(-1f, 1f, 1f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sp;
            img.raycastTarget = false;
        }

        // ───────────── 小工具 ─────────────

        /// <summary>
        /// 黑幕要填的 alpha：讓場景「看起來」剩 <see cref="BackdropBrightness"/> 的亮度。
        /// Linear 色彩空間的混色在線性光上做，黑色疊 α ⇒ 線性亮度 ×(1−α) ⇒ 人眼亮度 ×(1−α)^(1/2.2)（PROBLEMS E11）。
        /// </summary>
        static float BackdropAlpha()
        {
            float b = Mathf.Clamp01(BackdropBrightness);
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? 1f - Mathf.Pow(b, 2.2f) : 1f - b;
        }

        /// <summary>畫布在參考單位下的寬高（同 CanvasScaler：ScaleWithScreenSize、1920×1080、match 0.5 ＝ 寬高比例取幾何平均）。</summary>
        static Vector2 CanvasSize()
        {
            float sw = Screen.width, sh = Screen.height;
            if (sw <= 0f || sh <= 0f) return new Vector2(1920f, 1080f);
            float s = Mathf.Sqrt((sw / 1920f) * (sh / 1080f));
            return new Vector2(sw / s, sh / s);
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>原尺寸（×scale）擺一張美術圖；圖載不到印警告並回 null（版面其他部分照常）。</summary>
        static Image ArtImage(string name, Transform parent, Sprite sp, Vector2 anchor, Vector2 pos, float scale)
        {
            if (sp == null)
            {
                Debug.LogWarning($"[CharacterIntroPanel] 找不到美術元件「{name}」（Resources/{ArtRoot}…）。");
                return null;
            }
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sp.rect.size * scale;
            rt.anchoredPosition = pos;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sp;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>在姓名版裡放一行置中字（座標以姓名版頂端中央為原點）。minSize＞0＝太長時自動縮字（單行）。</summary>
        static Text Label(string name, RectTransform parent, string text, Font font, int size, int minSize,
                          Vector2 pos, Vector2 box, bool wrap)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = rt.gameObject.AddComponent<Text>();
            SetupText(t, text, font, size);
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (minSize > 0)
            {
                // bestFit 需要 Wrap＋框高只容一行，才會「縮字」而不是「換行」
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Truncate;
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = minSize;
                t.resizeTextMaxSize = size;
            }
            return t;
        }

        static void SetupText(Text t, string text, Font font, int size)
        {
            t.text = text ?? "";
            t.font = font != null ? font : UIBuilder.DefaultFont;
            t.fontSize = size;
            t.color = TextColor;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            t.supportRichText = false;
        }

        /// <summary>
        /// 稱號／名字加字距（示意圖是字字分開的排法）：只在「沒有英文字母」時於每個字之間插 <paramref name="gap"/>；
        /// 英文（之後的 en 欄）原樣不動，免得變成 C h a r l o t t e。
        /// </summary>
        static string Spaced(string s, string gap)
        {
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(gap)) return s ?? "";
            foreach (char c in s) if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) return s;
            var sb = new System.Text.StringBuilder(s.Length * (1 + gap.Length));
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && !char.IsWhiteSpace(s[i]) && !char.IsWhiteSpace(s[i - 1])) sb.Append(gap);
                sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}
