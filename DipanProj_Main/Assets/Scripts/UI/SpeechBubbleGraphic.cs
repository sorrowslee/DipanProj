using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 頭上對話框的三種形狀（作者 2026-10-28）：
    ///   <b>Square 方形</b>＝直接、官方言論（預設）；<b>Round 圓形</b>＝溫和、有感情；<b>Spike 尖刺</b>＝激動、憤怒。
    /// </summary>
    public enum SpeechBubbleShape { Square = 0, Round = 1, Spike = 2 }

    /// <summary>
    /// 形狀的幾何與標記解析（純資料，不碰 Unity 物件）。見 readme/MONSTER_SPEECH.md〈三種形狀〉。
    ///
    /// <para><b>全部從「方形」往外長</b>：方形＝文字最小容納範圍（文字＋內距）；圓形＝把方形撐成橢圓，
    /// 大小算到「每一行字的四角都在橢圓內」為止（台詞每行置中、長短不一，比直接外接整個方框小一圈）；
    /// 尖刺＝在圓形外圈長出鋸齒，刺的數量依周長自動增減。</para>
    ///
    /// <para><b>標準方向</b>：框往右長、尾巴在左下、y 向上，座標原點＝文字框中心。
    /// 其他方向由呼叫端鏡像（<see cref="Transform"/>）。尾巴是輪廓的一部分（不是另一張圖）。</para>
    /// </summary>
    public static class SpeechBubbleShapes
    {
        // ── 方形 ──
        const float Corner = 10f;              // 圓角半徑
        const float SqTailInset = 20f;         // 尾巴根部離主體左緣
        const float SqTailBase = 24f;          // 尾巴根部寬
        const float SqTailLen = 28f;           // 尾巴尖在主體下緣下方多遠
        const float SqTailLean = 4f;           // 尾巴尖往外（左）偏多少
        // ── 圓形 ──
        const float FitMarginX = 0.45f, FitMarginY = 0.25f;   // 文字四角再留多少內距（× PadX / PadY）才算「在橢圓內」
        const int EllipseSegs = 96;
        const float RoundTailAngle = 245f;     // 尾巴根部在橢圓上的角度（270＝正下方，245＝下方偏左）
        const float RoundTailHalf = 9f;        // 尾巴根部張開角（±度）
        const float RoundTailLen = 30f;        // 尾巴尖在根部下方多遠
        const float RoundTailLean = 6f;
        // ── 尖刺 ──
        const float SpikeGrowX = 1.04f, SpikeGrowY = 1.10f;   // 刺的根部所在橢圓＝圓形橢圓再放大一點（刺的凹處才不會壓到字）
        const float SpikeSpacing = 46f;        // 周長每多少 px 一根刺
        const int SpikeMin = 14;
        const float SpikeLenMin = 0.16f, SpikeLenMax = 0.28f; // 刺長（橢圓半徑的比例，同一句台詞固定）
        const float SpikeTailLen = 36f, SpikeTailLean = 8f;   // 當尾巴的那一根刺：在根部下方多遠、往左偏多少

        /// <summary>
        /// 產生輪廓（逆時針、封閉、不重複首點）。<paramref name="w"/>×<paramref name="h"/>＝文字框（含內距）；
        /// <paramref name="lineWidths"/>／<paramref name="lineHeight"/>＝各行實際寬與行高（圓形與尖刺用來貼合文字）。
        /// <paramref name="tip"/> 回傳尾巴尖（標準方向）。<paramref name="seed"/> 決定尖刺長短（同一句台詞每次一樣）。
        /// </summary>
        public static List<Vector2> Build(SpeechBubbleShape shape, float w, float h, IList<float> lineWidths, float lineHeight,
                                          float padX, float padY, int seed, out Vector2 tip)
        {
            switch (shape)
            {
                case SpeechBubbleShape.Round: return BuildRound(w, h, lineWidths, lineHeight, padX, padY, out tip);
                case SpeechBubbleShape.Spike: return BuildSpike(w, h, lineWidths, lineHeight, padX, padY, seed, out tip);
                default: return BuildSquare(w, h, out tip);
            }
        }

        static List<Vector2> BuildSquare(float w, float h, out Vector2 tip)
        {
            float a = w * 0.5f, b = h * 0.5f, r = Mathf.Min(Corner, a, b);
            float x1 = -a + Mathf.Max(SqTailInset, r), x2 = x1 + SqTailBase;
            tip = new Vector2(x1 - SqTailLean, -b - SqTailLen);
            var p = new List<Vector2> { tip, new Vector2(x2, -b) };
            Arc(p, new Vector2(a - r, -b + r), r, -90f, 0f);
            Arc(p, new Vector2(a - r, b - r), r, 0f, 90f);
            Arc(p, new Vector2(-a + r, b - r), r, 90f, 180f);
            Arc(p, new Vector2(-a + r, -b + r), r, 180f, 270f);
            p.Add(new Vector2(x1, -b));
            return p;
        }

        static void Arc(List<Vector2> p, Vector2 c, float r, float from, float to)
        {
            const int steps = 8;
            for (int i = 0; i <= steps; i++)
            {
                float t = Mathf.Deg2Rad * Mathf.Lerp(from, to, i / (float)steps);
                p.Add(c + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r);
            }
        }

        /// <summary>讓每一行文字（含一點內距）的四角都在橢圓內的最小橢圓半軸（長寬比與文字框相同）。</summary>
        static Vector2 FitEllipse(float w, float h, IList<float> lineWidths, float lineHeight, float padX, float padY)
        {
            float a0 = w * 0.5f, b0 = h * 0.5f, s = 1f;
            int n = lineWidths != null ? lineWidths.Count : 0;
            if (n == 0) return new Vector2(a0 * 1.414f, b0 * 1.414f);
            float top = n * lineHeight * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float x = lineWidths[i] * 0.5f + padX * FitMarginX;
                float y0 = top - i * lineHeight + padY * FitMarginY, y1 = top - (i + 1) * lineHeight - padY * FitMarginY;
                s = Mathf.Max(s, Mathf.Sqrt(Sq(x / a0) + Sq(y0 / b0)), Mathf.Sqrt(Sq(x / a0) + Sq(y1 / b0)));
            }
            return new Vector2(a0 * s, b0 * s);
        }

        static float Sq(float v) => v * v;
        static Vector2 OnEllipse(Vector2 ab, float deg) { float t = deg * Mathf.Deg2Rad; return new Vector2(ab.x * Mathf.Cos(t), ab.y * Mathf.Sin(t)); }

        static List<Vector2> BuildRound(float w, float h, IList<float> lw, float lh, float padX, float padY, out Vector2 tip)
        {
            var ab = FitEllipse(w, h, lw, lh, padX, padY);
            Vector2 baseC = OnEllipse(ab, RoundTailAngle);
            tip = new Vector2(baseC.x - RoundTailLean, baseC.y - RoundTailLen);
            var p = new List<Vector2> { tip };
            float start = RoundTailAngle + RoundTailHalf, end = RoundTailAngle - RoundTailHalf + 360f;
            int segs = Mathf.Max(24, Mathf.RoundToInt(EllipseSegs * (end - start) / 360f));
            for (int i = 0; i <= segs; i++) p.Add(OnEllipse(ab, Mathf.Lerp(start, end, i / (float)segs)));
            return p;
        }

        static List<Vector2> BuildSpike(float w, float h, IList<float> lw, float lh, float padX, float padY, int seed, out Vector2 tip)
        {
            var ab = FitEllipse(w, h, lw, lh, padX, padY);
            ab = new Vector2(ab.x * SpikeGrowX, ab.y * SpikeGrowY);
            float perim = Mathf.PI * (3f * (ab.x + ab.y) - Mathf.Sqrt((3f * ab.x + ab.y) * (ab.x + 3f * ab.y)));
            int n = Mathf.Max(SpikeMin, Mathf.RoundToInt(perim / SpikeSpacing));
            var rng = new System.Random(seed);

            // 哪一根刺當尾巴：外點角度最接近 RoundTailAngle 的那根
            int tailK = 0; float best = 999f;
            for (int k = 0; k < n; k++)
            {
                float deg = 360f * (k + 0.5f) / n;
                float d = Mathf.Abs(Mathf.DeltaAngle(deg, RoundTailAngle));
                if (d < best) { best = d; tailK = k; }
            }

            // 從尾巴那根開始排，尾巴尖就是第 0 點（呼叫端只看 tip，順序只是好讀）
            var p = new List<Vector2>(n * 2);
            tip = Vector2.zero;
            for (int j = 0; j < n; j++)
            {
                int k = (tailK + j) % n;
                float len = 1f + Mathf.Lerp(SpikeLenMin, SpikeLenMax, (float)rng.NextDouble());
                Vector2 outer;
                if (k == tailK)
                {
                    Vector2 root = OnEllipse(ab, 360f * (k + 0.5f) / n);
                    outer = new Vector2(root.x - SpikeTailLean, root.y - SpikeTailLen);
                    tip = outer;
                }
                else outer = OnEllipse(ab, 360f * (k + 0.5f) / n) * len;
                p.Add(outer);
                p.Add(OnEllipse(ab, 360f * (k + 1) / n));   // 下一個凹處
            }
            return p;
        }

        /// <summary>依方向鏡像（往左長＝左右翻、在腳下＝上下翻）；只翻一軸時反轉順序，維持逆時針。</summary>
        public static List<Vector2> Transform(List<Vector2> pts, bool flipX, bool flipY, ref Vector2 tip)
        {
            float sx = flipX ? -1f : 1f, sy = flipY ? -1f : 1f;
            var o = new List<Vector2>(pts.Count);
            foreach (var v in pts) o.Add(new Vector2(v.x * sx, v.y * sy));
            if (flipX != flipY) o.Reverse();
            tip = new Vector2(tip.x * sx, tip.y * sy);
            return o;
        }

        // ───────────── 台詞標記 ─────────────

        /// <summary>
        /// 讀台詞開頭的形狀標記並拿掉：<c>[刺]</c>／<c>[圓]</c>／<c>[方]</c>（全形【】也可、英文 spike/round/square 也可）。
        /// 沒有標記回 <paramref name="fallback"/>。給怪物 CSV 句子與任何走 <c>MonsterSpeechPanel.Speak</c> 的文字共用。
        /// </summary>
        public static SpeechBubbleShape ParseTag(ref string text, SpeechBubbleShape fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            string s = text.TrimStart();
            if (s.Length < 3) return fallback;
            char open = s[0];
            if (open != '[' && open != '【') return fallback;
            int close = s.IndexOf(open == '[' ? ']' : '】');
            if (close < 2 || close > 9) return fallback;
            if (!TryName(s.Substring(1, close - 1), out var shape)) return fallback;
            text = s.Substring(close + 1).TrimStart();
            return shape;
        }

        /// <summary>名字 → 形狀（劇情步驟欄位用）："" / square / 方 → 方形；round / 圓 → 圓形；spike / 刺 → 尖刺。</summary>
        public static SpeechBubbleShape FromName(string name)
            => TryName(name, out var s) ? s : SpeechBubbleShape.Square;

        static bool TryName(string name, out SpeechBubbleShape shape)
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "刺": case "spike": shape = SpeechBubbleShape.Spike; return true;
                case "圓": case "圆": case "round": shape = SpeechBubbleShape.Round; return true;
                case "方": case "square": shape = SpeechBubbleShape.Square; return true;
            }
            shape = SpeechBubbleShape.Square;
            return false;
        }

        /// <summary>穩定的字串雜湊（尖刺長短的種子；string.GetHashCode 不保證跨執行一致）。</summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                int h = (int)2166136261;
                if (s != null) foreach (char c in s) h = (h ^ c) * 16777619;
                return h & 0x7fffffff;
            }
        }
    }

    /// <summary>
    /// 把 <see cref="SpeechBubbleShapes"/> 的輪廓畫出來：白底＋黑框，內外各 1px 漸層抗鋸齒。
    /// 每次大小變動都依輪廓重建網格（不是拉伸圖片），所以線條粗細永遠一樣、多長的台詞都清楚。
    /// <para>填色用耳切法（ear clipping）三角化——尖刺與尾巴讓輪廓凹凸不規則，從中心扇形展開會畫到外面去。</para>
    /// <para>⚠ <b>一定要 <c>[RequireComponent(typeof(CanvasRenderer))]</c></b>：基底 <c>Graphic</c>／<c>MaskableGraphic</c> 不會自動補 CanvasRenderer
    /// （是 Image、Text 這些內建子類別自己宣告的）。少了它，Graphic 重建時看到 <c>canvasRenderer == null</c> 就直接跳過——
    /// 元件在、沒錯誤、尺寸正確，但什麼都不畫。2026-10-28 第一版就是這樣（只剩文字），見 PROBLEMS E44。</para>
    /// <para>三角形以順時針送出，跟 Unity 自己的四邊形一致（UI/Default 是 Cull Off，方向不影響顯示，只是慣例）。</para>
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class SpeechBubbleGraphic : MaskableGraphic
    {
        const float LineWidth = 3f;
        const float AA = 1f;           // 抗鋸齒漸層寬
        const float MiterLimit = 4f;   // 尖角的斜接長度上限（× 線寬），太尖的刺不會戳出一長條

        public Color FillColor = Color.white;
        public Color LineColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        List<Vector2> _pts;

        public void SetContour(List<Vector2> pts)
        {
            _pts = pts;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_pts == null || _pts.Count < 3) return;
            int n = _pts.Count;

            // 1) 填色（耳切）
            var tris = Triangulate(_pts);
            for (int i = 0; i < n; i++) vh.AddVert(_pts[i], FillColor, Vector2.zero);
            for (int i = 0; i + 2 < tris.Count; i += 3) vh.AddTriangle(tris[i], tris[i + 2], tris[i + 1]);   // 順時針（慣例，見類別說明）

            // 2) 框線：外漸層（輪廓往外 AA，透明→黑）＋線本體（輪廓→往內 LineWidth）＋內漸層（黑→透明，疊在白底上）
            var miter = new Vector2[n];
            for (int i = 0; i < n; i++) miter[i] = InwardMiter(_pts[(i - 1 + n) % n], _pts[i], _pts[(i + 1) % n]);
            Color clear = new Color(LineColor.r, LineColor.g, LineColor.b, 0f);
            Band(vh, miter, -AA, clear, 0f, LineColor);
            Band(vh, miter, 0f, LineColor, LineWidth, LineColor);
            Band(vh, miter, LineWidth, LineColor, LineWidth + AA, clear);
        }

        /// <summary>沿輪廓畫一圈帶狀（d0→d1 為往內偏移距離，負＝往外）。</summary>
        void Band(VertexHelper vh, Vector2[] miter, float d0, Color c0, float d1, Color c1)
        {
            int n = _pts.Count, start = vh.currentVertCount;
            for (int i = 0; i < n; i++)
            {
                vh.AddVert(_pts[i] + miter[i] * d0, c0, Vector2.zero);
                vh.AddVert(_pts[i] + miter[i] * d1, c1, Vector2.zero);
            }
            for (int i = 0; i < n; i++)
            {
                int a = start + i * 2, b = start + ((i + 1) % n) * 2;
                vh.AddTriangle(a, b + 1, b);       // 順時針（輪廓是逆時針，內側在左手邊 ⇒ 反著加）
                vh.AddTriangle(a, a + 1, b + 1);
            }
        }

        /// <summary>頂點的「往內」斜接方向（長度已含斜接放大，偏移 d 就是 pt + m*d）。輪廓為逆時針，左手邊＝內側。</summary>
        static Vector2 InwardMiter(Vector2 prev, Vector2 cur, Vector2 next)
        {
            Vector2 e0 = (cur - prev).normalized, e1 = (next - cur).normalized;
            Vector2 n0 = new Vector2(-e0.y, e0.x), n1 = new Vector2(-e1.y, e1.x);
            Vector2 m = n0 + n1;
            if (m.sqrMagnitude < 1e-6f) return n1;
            m.Normalize();
            float dot = Vector2.Dot(m, n1);
            float len = dot > 1e-3f ? 1f / dot : MiterLimit;
            return m * Mathf.Min(len, MiterLimit);
        }

        // ───────────── 耳切三角化（簡單多邊形、逆時針）─────────────
        static List<int> Triangulate(List<Vector2> p)
        {
            var res = new List<int>();
            int n = p.Count;
            var idx = new List<int>(n);
            for (int i = 0; i < n; i++) idx.Add(i);
            if (SignedArea(p) < 0f) idx.Reverse();   // 保險：確保逆時針

            int guard = n * n;
            while (idx.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i - 1 + idx.Count) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = p[ia], b = p[ib], c = p[ic];
                    if (Cross(b - a, c - b) <= 1e-5f) continue;   // 凹角不是耳朵
                    bool inside = false;
                    for (int j = 0; j < idx.Count; j++)
                    {
                        int k = idx[j];
                        if (k == ia || k == ib || k == ic) continue;
                        if (InTri(p[k], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    res.Add(ia); res.Add(ib); res.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;   // 退化輪廓：剩下的用扇形補（不會發生在正常形狀上）
            }
            for (int i = 1; i + 1 < idx.Count; i++) { res.Add(idx[0]); res.Add(idx[i]); res.Add(idx[i + 1]); }
            return res;
        }

        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
        static float SignedArea(List<Vector2> p)
        {
            float s = 0f;
            for (int i = 0; i < p.Count; i++) { var a = p[i]; var b = p[(i + 1) % p.Count]; s += a.x * b.y - b.x * a.y; }
            return s * 0.5f;
        }
        static bool InTri(Vector2 q, Vector2 a, Vector2 b, Vector2 c)
            => Cross(b - a, q - a) >= 0f && Cross(c - b, q - b) >= 0f && Cross(a - c, q - c) >= 0f;
    }
}
