using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 頭上對話框（覆蓋層、不擋輸入、不暫停）。**通用元件**：可同時掛多個氣泡，各自跟著一個目標的頭頂跑。
    ///
    /// 兩個呼叫端（同一套美術與定位邏輯）：
    ///   ‧ **怪物**：<see cref="MonsterSpeech"/>（掛在怪物上）呼叫 <see cref="Speak(MonsterController, string, float)"/>；
    ///     顯示 duration 秒後淡出，怪物死亡（IsDead / 物件被銷毀）立即移除。
    ///   ‧ **劇情演員**：劇情演出的 <c>bubble</c> 步驟呼叫 <see cref="Speak(Transform, string, float)"/>，
    ///     讓話直接冒在演員頭上、不跳對話視窗（見 readme/CUTSCENE_DIRECTOR.md）。
    ///
    /// 所以內部**不綁 MonsterController**：氣泡只記一個 <c>Transform</c> 目標 ＋ 一個「還在不在」的判斷委派。
    ///
    /// ── 2026-10-28 改版（作者：「NPC 跟怪物在講啥我基本上都看不清楚」）──
    /// 舊版是固定大小的水墨泡泡圖（寬 336、能放字的奶油區只有 200×83），字級被框卡住：超過 7 個字就降到 28、
    /// 9 個字以上只剩 22px，再加上內建字體＋假粗體，幾乎讀不到。現在改成：
    ///   ‧ **框跟著文字長**：外框由程式依文字大小即時畫（<see cref="SpeechBubbleGraphic"/>，白底黑線、尾巴是輪廓的一部分），
    ///     不再拉伸圖片，所以線條粗細永遠一樣、多長的台詞都清楚。
    ///   ‧ **三種形狀**（<see cref="SpeechBubbleShape"/>）：方形＝直接／官方（預設）、圓形＝溫和／有感情、尖刺＝激動／憤怒。
    ///     全部從「方形＝文字最小容納範圍」往外長。台詞開頭寫 <c>[刺]</c>／<c>[圓]</c>／<c>[方]</c> 指定，或由呼叫端傳入。
    ///   ‧ **字級固定**（<see cref="FontSize"/>）：每句話都一樣大，太長就換行、框長高，不再縮字。
    ///   ‧ **自己斷行**（<see cref="WrapText"/>）：uGUI 內建換行不懂中文禁則，會把「。」斷到行首；
    ///     這裡優先在標點後斷（不拆詞），沒標點可斷才平均分行（最後一行不會只剩一兩個字）。
    ///   ‧ 字型改思源宋體 Bold（同角色介紹）。
    /// 舊的 <c>UI/InGame/InGame_TalkBg1/2</c> 與第一版的 <c>UI/SpeechBubble/*.png</c> 都沒刪，程式已不再使用
    /// （<c>tools/ui/speech_bubble/*.svg</c> 留作外觀設計稿）。
    ///
    /// 定位（螢幕座標，沿用 PlayerHintPanel 的 WorldToScreen 做法）：**尾巴尖**對到目標頭頂（或腳下），
    /// 方向在開口那一刻決定、之後固定：靠畫面左緣→框往右長、靠右緣→往左長（尾巴左右翻）、中間隨機；
    /// 靠上緣→改到腳下（尾巴上下翻、框在下方）。
    /// 見 readme/MONSTER_SPEECH.md。
    /// </summary>
    public class MonsterSpeechPanel : UIPanel
    {
        public override UILayer Layer => UILayer.HUD;       // 常駐抬頭層：壓在背包/設定等視窗之下，合理
        public override bool BlocksGameplayInput => false;
        public override bool PausesGame => false;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;

        // ── 版面常數（1920x1080 基準；要調氣泡大小改這裡；形狀本身的尺寸在 SpeechBubbleShapes）──
        const int FontSize = 34;              // 字級（固定，所有台詞一樣大）
        const float MaxTextWidth = 340f;      // 一行最寬（約 10 個中文字），超過就換行
        const float PadX = 22f, PadY = 14f;   // 文字到方框線的內距（＝方形的「最小容納範圍」；圓形／尖刺從這裡往外長）
        const float MinBoxWidth = 64f;        // 方框最窄（一兩個字的台詞，尾巴才放得下）
        const float LineSpacing = 1.0f;       // uGUI Text 的行距倍率（思源宋體本身行高偏寬，1.0 已經夠鬆）
        static readonly Color TextColor = new Color(0.08f, 0.08f, 0.08f, 1f);   // 近黑（純白底上對比最高）
        const string FontPath = "Fonts/NotoSerifTC/NotoSerifTC-Bold";
        const float HeadGap = 6f;             // 尾巴尖離頭頂（或腳下）再留一點
        const float FadeIn = 0.12f, FadeOut = 0.3f;

        // 邊緣判定門檻（viewport 0~1）：太靠左/右 → 對話翻到另一側；太靠上 → 改到腳下。
        const float LeftEdge = 0.25f, RightEdge = 0.75f, TopEdge = 0.80f;

        static Font _font;
        static Font SpeechFont
        {
            get { if (_font == null) _font = UIBuilder.LoadFont(FontPath); return _font; }
        }

        class Bubble
        {
            public RectTransform Root;
            public CanvasGroup Cg;
            public Transform Target;          // 氣泡跟著誰（怪物 / 劇情演員 / 玩家）
            public System.Func<bool> Alive;   // 還要不要顯示（怪物＝沒死；null＝只要 Target 還在就顯示）
            public float Spawn;
            public float Duration;
            public bool Below;       // 錨在腳下（true）或頭頂（false）
            public float OffsetX, OffsetY;
        }

        readonly List<Bubble> _bubbles = new List<Bubble>();
        Camera _cam;

        protected override void OnBuild() { /* 氣泡動態生成，這裡不建固定版面 */ }

        // ───────────────────────── 對外 API ─────────────────────────

        /// <summary>
        /// 在某隻怪物頭上顯示一句話（duration 秒後淡出；怪物死亡立即消失）。
        /// 形狀：台詞開頭有 <c>[刺]</c>／<c>[圓]</c>／<c>[方]</c> 就照標記，否則用 <paramref name="shape"/>。
        /// </summary>
        public static void Speak(MonsterController mc, string text, float duration, SpeechBubbleShape shape = SpeechBubbleShape.Square)
        {
            if (mc == null || string.IsNullOrEmpty(text)) return;
            var p = UIManager.Instance?.Open<MonsterSpeechPanel>();
            p?.AddBubble(mc.transform, () => mc != null && !mc.IsDead, text, duration, shape);
        }

        /// <summary>
        /// 在**任何** Transform 頭上顯示一句話（劇情演員、玩家…）。目標被銷毀就自動移除。
        /// 頭頂/腳下位置的抓法見 <see cref="HeadWorld"/>：玩家走 PlayerController 的可見身體幾何，
        /// 其他人優先用 Collider2D，都沒有才退回 SpriteRenderer 的 bounds。
        /// </summary>
        public static void Speak(Transform target, string text, float duration, SpeechBubbleShape shape = SpeechBubbleShape.Square)
        {
            if (target == null || string.IsNullOrEmpty(text)) return;
            var p = UIManager.Instance?.Open<MonsterSpeechPanel>();
            // 目標被停用（劇情演員 despawn／演出被略過時快轉成消失）也要立刻收掉氣泡——
            // 只看「沒被銷毀」的話，despawn 只是 SetActive(false)，氣泡會留在空中（2026-10-15 作者回報）。
            p?.AddBubble(target, () => target != null && target.gameObject.activeInHierarchy, text, duration, shape);
        }

        void AddBubble(Transform target, System.Func<bool> alive, string text, float duration, SpeechBubbleShape shape)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            // 台詞開頭的形狀標記優先（怪物 CSV 句子、語言表文字都能寫），並從顯示文字拿掉
            text = (text ?? "").Replace("\\n", "\n");
            shape = SpeechBubbleShapes.ParseTag(ref text, shape);
            if (string.IsNullOrEmpty(text)) return;

            // 同一個目標若已有氣泡 → 先移除舊的（避免疊字）。
            for (int i = _bubbles.Count - 1; i >= 0; i--)
                if (_bubbles[i].Target == target) Remove(i);

            // 方向（開口這一刻決定）：靠左緣→往右長、靠右緣→往左長、中間隨機；靠上緣→改腳下。
            Vector3 head = HeadWorld(target);
            Vector3 vp = _cam.WorldToViewportPoint(head);
            bool rightSide = vp.x < LeftEdge ? true : (vp.x > RightEdge ? false : (Random.value < 0.5f));
            bool below = vp.y > TopEdge;

            var font = SpeechFont;
            string wrapped = WrapText(font, text, FontSize, MaxTextWidth);
            string[] lines = wrapped.Split('\n');

            // 容器：原點＝尾巴尖（大小 0），文字框與外框都相對它擺
            var rootGo = UIBuilder.Create("SpeechBubble", transform);
            var root = (RectTransform)rootGo.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;

            // 文字框（pivot 在中心＝形狀座標的原點）。文字先建，量出實際大小才知道框多大。
            var body = UIBuilder.Create("Body", root);
            var brt = (RectTransform)body.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);

            var shapeGo = UIBuilder.Create("Shape", brt);   // 先建＝畫在文字後面
            var txt = UIBuilder.Text(brt, "Msg", wrapped, FontSize, TextColor, TextAnchor.MiddleCenter);
            txt.font = font;
            txt.fontSize = FontSize;
            txt.fontStyle = FontStyle.Normal;   // 字檔本身就是 Bold，不要再疊假粗體
            txt.lineSpacing = LineSpacing;
            txt.raycastTarget = false;
            txt.supportRichText = false;
            txt.resizeTextForBestFit = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;   // 已經自己斷好行，不讓 uGUI 再折
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            float tw = Mathf.Ceil(txt.preferredWidth), th = Mathf.Ceil(txt.preferredHeight);

            float bw = Mathf.Max(tw + PadX * 2f, MinBoxWidth);
            float bh = th + PadY * 2f;
            brt.sizeDelta = new Vector2(bw, bh);
            var trt = txt.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(PadX, PadY); trt.offsetMax = new Vector2(-PadX, -PadY);

            // 外框：從方框（文字最小容納範圍）往外長出該形狀，再依方向鏡像
            var widths = new List<float>(lines.Length);
            foreach (var l in lines) widths.Add(Width(font, l, FontSize));
            float lh = lines.Length > 0 ? th / lines.Length : th;
            var pts = SpeechBubbleShapes.Build(shape, bw, bh, widths, lh, PadX, PadY, SpeechBubbleShapes.StableHash(text), out Vector2 tip);
            pts = SpeechBubbleShapes.Transform(pts, !rightSide, below, ref tip);

            var srt = (RectTransform)shapeGo.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = Vector2.zero;
            float ex = 0f, ey = 0f;
            foreach (var v in pts) { ex = Mathf.Max(ex, Mathf.Abs(v.x)); ey = Mathf.Max(ey, Mathf.Abs(v.y)); }
            srt.sizeDelta = new Vector2(ex * 2f + 4f, ey * 2f + 4f);   // 只給裁切用，畫的位置看頂點
            var g = shapeGo.AddComponent<SpeechBubbleGraphic>();
            g.raycastTarget = false;
            g.SetContour(pts);

            // 文字框中心放在「尾巴尖 → 中心」的位置，尾巴尖就落在容器原點
            brt.anchoredPosition = -tip;

            var cg = rootGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            var b = new Bubble
            {
                Root = root, Cg = cg, Target = target, Alive = alive,
                Spawn = Time.time, Duration = Mathf.Max(0.3f, duration),
                Below = below,
                OffsetX = 0f,
                OffsetY = below ? -HeadGap : HeadGap,
            };
            _bubbles.Add(b);
            Reposition(b);   // 立刻擺對位置，避免第一幀出現在畫面中央
        }

        // ───────────────────────── 斷行：自己做，uGUI 的不懂中文 ─────────────────────────

        // 不能放在行首的字（句讀、收尾括號、波浪號…）——斷行時讓它留在上一行（允許略超出最大寬度）
        const string NoLineStart = "，。、！？；：」』）】〉》…—～~,.!?;:)]}%·";
        // 不能放在行尾的字（開頭括號）——斷行時把它帶到下一行
        const string NoLineEnd = "「『（【〈《([{";

        // 斷句標點：優先在這些字「之後」斷行（中文台詞幾乎都有標點，斷在這裡最自然、不會把詞拆開）
        const string BreakAfter = "，。、！？；：…—～~,.!?;";

        /// <summary>
        /// 依實際字寬把文字斷成多行（插 <c>\n</c>）。作者手動寫的 <c>\n</c> 一律保留。一段超過一行時，兩種斷法都算、取行數少的：
        /// <para>① <b>標點優先</b>（<see cref="PunctLines"/>）：先照斷句標點切成短句，再把短句組成行；行尾的標點可以超出最大寬度
        /// （例：「公爵大人最討厭被拒絕。／上一個拒絕他的，／血到現在還沒流乾。」，不會變成「…被拒／絕。…」）。</para>
        /// <para>② <b>平均分行</b>（<see cref="Balanced"/>）：沒標點可斷或標點版多出行數時用——每行約總寬 ÷ 行數，最後一行不會只剩一兩個字。</para>
        /// <para>行數一樣時用①。兩種都遵守禁則（句讀不放行首、開括號不放行尾）、英文單字不從中間切。</para>
        /// 字寬用 <c>Font.GetCharacterInfo</c> 量（動態字型要先 RequestCharactersInTexture）。
        /// </summary>
        static string WrapText(Font font, string text, int size, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || font == null) return text ?? "";
            font.RequestCharactersInTexture(text, size, FontStyle.Normal);
            var sb = new System.Text.StringBuilder();
            string[] segs = text.Split('\n');
            for (int s = 0; s < segs.Length; s++)
            {
                if (s > 0) sb.Append('\n');
                string seg = segs[s];
                if (Width(font, seg, size) <= maxWidth) { sb.Append(seg); continue; }
                var a = Balanced(font, seg, size, maxWidth);
                var b = PunctLines(font, seg, size, maxWidth);
                sb.Append(string.Join("\n", b.Count <= a.Count ? b : a));
            }
            return sb.ToString();
        }

        static List<string> Balanced(Font font, string seg, int size, float maxWidth)
        {
            float total = Width(font, seg, size);
            int n = Mathf.Max(1, Mathf.CeilToInt(total / maxWidth));
            var lines = Greedy(font, seg, size, total / n, maxWidth);
            return lines.Count > n ? Greedy(font, seg, size, maxWidth, maxWidth) : lines;
        }

        /// <summary>標點優先：短句組行；單一短句本身就超過一行才在句中平均切，切剩的最後一行可以再接下一句。</summary>
        static List<string> PunctLines(Font font, string seg, int size, float maxWidth)
        {
            var lines = new List<string>();
            string cur = ""; float w = 0f;
            foreach (var ch in SplitChunks(seg))
            {
                float cw = Width(font, ch, size);
                float slack = TailSlack(font, ch, size);
                if (cur.Length > 0 && w + cw <= maxWidth + slack) { cur += ch; w += cw; continue; }
                if (cur.Length > 0) lines.Add(cur);
                if (cw <= maxWidth + slack) { cur = ch; w = cw; }
                else
                {
                    var l = Balanced(font, ch, size, maxWidth);
                    for (int i = 0; i < l.Count - 1; i++) lines.Add(l[i]);
                    cur = l[l.Count - 1]; w = Width(font, cur, size);
                }
            }
            if (cur.Length > 0) lines.Add(cur);
            return lines;
        }

        /// <summary>照斷句標點切短句（連續的標點／收尾字跟著前一句）；整片只有標點的（例：句首「……」）併進下一句。</summary>
        static List<string> SplitChunks(string seg)
        {
            var raw = new List<string>();
            var cur = new System.Text.StringBuilder();
            for (int i = 0; i < seg.Length; i++)
            {
                cur.Append(seg[i]);
                if (BreakAfter.IndexOf(seg[i]) >= 0)
                {
                    while (i + 1 < seg.Length && (BreakAfter.IndexOf(seg[i + 1]) >= 0 || NoLineStart.IndexOf(seg[i + 1]) >= 0))
                        cur.Append(seg[++i]);
                    raw.Add(cur.ToString()); cur.Clear();
                }
            }
            if (cur.Length > 0) raw.Add(cur.ToString());

            var outp = new List<string>();
            string pend = "";
            foreach (var c in raw)
            {
                if (IsAllPunct(c)) { pend += c; continue; }
                outp.Add(pend + c); pend = "";
            }
            if (pend.Length > 0) { if (outp.Count > 0) outp[outp.Count - 1] += pend; else outp.Add(pend); }
            return outp;
        }

        static bool IsAllPunct(string s)
        {
            foreach (char c in s) if (BreakAfter.IndexOf(c) < 0 && NoLineStart.IndexOf(c) < 0) return false;
            return true;
        }

        /// <summary>句尾連續標點的寬度＝這一句允許超出最大寬度多少（標點掛在行尾不算擠）。</summary>
        static float TailSlack(Font font, string s, int size)
        {
            float k = 0f;
            for (int i = s.Length - 1; i >= 0 && (BreakAfter.IndexOf(s[i]) >= 0 || NoLineStart.IndexOf(s[i]) >= 0); i--)
                k += Advance(font, s[i], size);
            return k;
        }

        static float Width(Font font, string s, int size)
        {
            float w = 0f;
            foreach (char c in s) w += Advance(font, c, size);
            return w;
        }

        static List<string> Greedy(Font font, string seg, int size, float target, float maxWidth)
        {
            var lines = new List<string>();
            var cur = new System.Text.StringBuilder();
            float w = 0f;
            for (int i = 0; i < seg.Length; i++)
            {
                char c = seg[i];
                float a = Advance(font, c, size);
                if (cur.Length > 0 && w + a > target + size * 0.3f)
                {
                    // 句讀不放行首：留在這一行（只要沒超出最大寬度一個字以上）
                    if (NoLineStart.IndexOf(c) >= 0 && w + a <= maxWidth + size) { cur.Append(c); w += a; continue; }

                    string carry = "";
                    // 英文單字不切：往回找空白，空白之後那段帶到下一行
                    if (IsLatin(c) && IsLatin(cur[cur.Length - 1]))
                    {
                        string cs = cur.ToString();
                        int sp = cs.LastIndexOf(' ');
                        if (sp > 0) { carry = cs.Substring(sp + 1); cur.Length = sp; }
                    }
                    // 開括號不放行尾：帶到下一行
                    else if (cur.Length > 1 && NoLineEnd.IndexOf(cur[cur.Length - 1]) >= 0)
                    {
                        carry = cur[cur.Length - 1].ToString(); cur.Length -= 1;
                    }
                    lines.Add(cur.ToString().TrimEnd());
                    cur.Clear(); cur.Append(carry); cur.Append(c);
                    w = 0f; foreach (char cc in cur.ToString()) w += Advance(font, cc, size);
                }
                else { cur.Append(c); w += a; }
            }
            if (cur.Length > 0) lines.Add(cur.ToString());
            return lines;
        }

        static bool IsLatin(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '\'';

        static float Advance(Font font, char c, int size)
        {
            if (font.GetCharacterInfo(c, out CharacterInfo ci, size, FontStyle.Normal)) return ci.advance;
            return size * (c < 0x2E80 ? 0.55f : 1f);   // 量不到時的概估
        }

        // ───────────────────────── 每幀更新 ─────────────────────────

        void Update()
        {
            if (!IsOpen || _bubbles.Count == 0) return;
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

            float now = Time.time;
            for (int i = _bubbles.Count - 1; i >= 0; i--)
            {
                var b = _bubbles[i];

                // 目標消失（怪物死亡 / 演員被銷毀）→ 立即移除氣泡
                if (b.Target == null || (b.Alive != null && !b.Alive())) { Remove(i); continue; }

                float elapsed = now - b.Spawn;
                if (elapsed >= b.Duration) { Remove(i); continue; }

                Reposition(b);

                float a = 1f;
                if (elapsed < FadeIn) a = elapsed / FadeIn;
                else if (elapsed > b.Duration - FadeOut) a = Mathf.Clamp01((b.Duration - elapsed) / FadeOut);
                b.Cg.alpha = a;
            }
        }

        void Reposition(Bubble b)
        {
            Vector3 anchorWorld = b.Below ? FeetWorld(b.Target) : HeadWorld(b.Target);
            Vector2 screen = _cam.WorldToScreenPoint(anchorWorld);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screen, null, out Vector2 local))
                return;
            b.Root.anchoredPosition = local + new Vector2(b.OffsetX, b.OffsetY);
        }

        void Remove(int i)
        {
            var b = _bubbles[i];
            if (b.Root != null) Destroy(b.Root.gameObject);
            _bubbles.RemoveAt(i);
        }

        protected override void OnClose()
        {
            for (int i = _bubbles.Count - 1; i >= 0; i--) Remove(i);
        }

        // 頭頂 / 腳下世界座標。三段優先序（劇情演員沒有 Collider2D，所以一定要有第三段）：
        //   ① 玩家 → PlayerController 的可見身體幾何（唯一正確來源，見 readme/PROBLEMS.md E14）
        //   ② 有 Collider2D → 碰撞框上下緣（怪物走這條，行為與改版前完全一致）
        //   ③ 只有 SpriteRenderer → 用 bounds（劇情 npc 演員；氣泡錨點對精度要求不高，可接受）
        static Vector3 HeadWorld(Transform t)
        {
            var pc = t.GetComponent<PlayerController>();
            if (pc != null)
            {
                Vector2 feet = pc.FeetWorldPos;
                return new Vector3(feet.x, feet.y + pc.VisibleBodyHeight, t.position.z);
            }
            var col = t.GetComponent<Collider2D>();
            if (col != null) return new Vector3(col.bounds.center.x, col.bounds.max.y, t.position.z);
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null) return new Vector3(sr.bounds.center.x, sr.bounds.max.y, t.position.z);
            return t.position + Vector3.up * 1.2f;
        }

        static Vector3 FeetWorld(Transform t)
        {
            var pc = t.GetComponent<PlayerController>();
            if (pc != null) { Vector2 feet = pc.FeetWorldPos; return new Vector3(feet.x, feet.y, t.position.z); }
            var col = t.GetComponent<Collider2D>();
            if (col != null) return new Vector3(col.bounds.center.x, col.bounds.min.y, t.position.z);
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null) return new Vector3(sr.bounds.center.x, sr.bounds.min.y, t.position.z);
            return t.position;
        }
    }
}
