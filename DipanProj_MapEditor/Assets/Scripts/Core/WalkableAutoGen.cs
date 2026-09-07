using System.Collections.Generic;
using UnityEngine;
using DipanMapEditor.Data;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 從背景圖自動生成可走區域（可走面板的「自動生成」）。
    ///
    /// <para><b>做法</b>：以你點的那格地板為基準做區域生長 —
    /// ① 把背景圖降取樣到子格解析度 → ② 轉「色度」(去掉亮度、只留色相比例) →
    /// ③ 與種子色度距離 &lt; 容差的算候選 → ④ <b>侵蝕→取與種子連通的一塊→膨脹回來</b> →
    /// ⑤ 閉運算填掉小洞。</para>
    ///
    /// <para><b>為什麼是這幾步（2026-09-07 拿全庫 22 張手塗的可走層實測出來的）</b>：</para>
    /// <list type="bullet">
    /// <item>用「亮度門檻」平均 IoU 只有 57%，而且有 5 張<b>完全失敗</b> —— 因為「地板比較亮」這個假設
    ///       在室內場景不成立（有些圖地板比牆暗）。改成「以種子顏色為基準」才不會賭錯方向。</item>
    /// <item>用<b>色度</b>而不是 RGB：同一片地板在光照下和陰影下 RGB 差很多、色度幾乎不變。
    ///       平均 IoU 從 73% → 76%。</item>
    /// <item>第 ④ 步是整件事的關鍵：誤判的區域（例如山壁被火光照亮的面，亮度色相都跟地板一樣）
    ///       都是<b>零碎不連通</b>的，先侵蝕切斷它們與主地板的細連接、取連通塊就能一次濾掉。
    ///       邪佛廣場實測 IoU 66% → 91%。</item>
    /// <item>多種子點<b>反而更差</b>（IoU 58%）：取「與最近種子的距離」等於放寬條件，會洩漏到牆上。
    ///       所以這裡只吃單一種子，要補不同色的地板請生成後用筆刷補。</item>
    /// </list>
    ///
    /// <para><b>⚠ 有天花板，不要期待 100%</b>：背景圖裡沒有「可走」這個資訊，只有顏色。
    /// 兩類東西演算法永遠猜不到 —— (a) 牆的受光面跟地板同色同亮；(b) 你<b>刻意</b>讓某塊深色地面
    /// 可走／某塊看起來像地板的地方不可走（那是設計意圖，不在圖裡）。
    /// 實測全庫平均 IoU 76%，但<b>錯誤集中在少數大塊</b>（最大 3 塊佔 73%），
    /// 生成後平均只要再補 6 筆左右（最多 14 筆），所以定位是「一鍵鋪好底稿」不是「一鍵完成」。</para>
    /// </summary>
    public static class WalkableAutoGen
    {
        /// <summary>
        /// 容差滑桿的範圍與預設（色度空間的歐氏距離）。
        /// 預設 35 是拿邪佛廣場實測出來的最佳點（容差 30→IoU 83.6%、35→89.9%、40→89.9%、50→87.5%）；
        /// 各地圖的最佳值不同，所以做成滑桿讓作者當場拖 —— 拖的時候有即時預覽，比猜快得多。
        /// </summary>
        public const float MinTolerance = 2f;
        public const float MaxTolerance = 60f;
        public const float DefaultTolerance = 35f;

        // ---- 降取樣快取：拖容差滑桿時只重跑後面幾步，不再重讀整張背景圖 ----
        // key = 背景 id + 子格尺寸；背景圖動輒 5792×4344（2500 萬像素），重讀一次要好幾百毫秒。
        static string _cacheKey;
        static float[] _cacheChroma;   // 長度 fw*fh*3，已經是色度（去亮度）

        /// <summary>丟掉降取樣快取（換背景圖／改畫布尺寸／按「刷新素材」時呼叫）。</summary>
        public static void ClearCache() { _cacheKey = null; _cacheChroma = null; }

        /// <summary>
        /// 生成可走遮罩。回傳長度 = FineWidth×FineHeight 的陣列，true = 可走。
        /// 失敗（沒背景圖／種子在圖外）回 null，原因寫在 <paramref name="report"/>。
        /// </summary>
        public static bool[] Generate(MapData map, Texture2D bg, Vector2Int seed, float tolerance, out string report)
        {
            report = "";
            if (map == null) { report = "沒有地圖"; return null; }
            if (bg == null) { report = "這張地圖沒有背景圖，自動生成無從判斷（請先設背景，或用筆刷手塗）"; return null; }

            int fw = map.FineWidth, fh = map.FineHeight;
            if (fw <= 0 || fh <= 0) { report = "可走層尺寸不正確"; return null; }
            if (seed.x < 0 || seed.y < 0 || seed.x >= fw || seed.y >= fh) { report = "種子點在地圖外"; return null; }

            float[] chroma = GetChroma(map, bg, fw, fh);
            if (chroma == null) { report = "背景圖讀取失敗"; return null; }

            int si = seed.y * fw + seed.x;
            float sr = chroma[si * 3], sg = chroma[si * 3 + 1], sb = chroma[si * 3 + 2];

            // ③ 與種子色度距離 < 容差
            var raw = new bool[fw * fh];
            for (int i = 0; i < raw.Length; i++)
            {
                float dr = chroma[i * 3] - sr, dg = chroma[i * 3 + 1] - sg, db = chroma[i * 3 + 2] - sb;
                raw[i] = (dr * dr + dg * dg + db * db) < tolerance * tolerance;
            }

            // ④ 侵蝕 → 取與種子連通的一塊 → 膨脹回來（再與 raw 取交集，避免膨脹越過候選邊界）
            int r = ErodeRadius(fw, fh);
            var eroded = Erode(raw, fw, fh, r);
            var island = Connected(eroded, fw, fh, si);
            var grown = Dilate(island, fw, fh, r);
            for (int i = 0; i < grown.Length; i++) grown[i] = grown[i] && raw[i];

            // ⑤ 閉運算：填掉地板上零星的小洞（一兩格的雜訊），不影響大形狀
            var closed = Erode(Dilate(grown, fw, fh, 1), fw, fh, 1);

            int n = 0;
            for (int i = 0; i < closed.Length; i++) if (closed[i]) n++;
            if (n == 0)
            {
                report = "生成結果是空的 —— 種子點可能落在牆上，或容差太小；請點在地板中央、或把容差調大";
                return closed;
            }
            report = $"可走 {n} 子格（佔 {n * 100f / closed.Length:0.#}%）；侵蝕半徑 {r}";
            return closed;
        }

        /// <summary>
        /// 侵蝕半徑：用來切斷「誤判區與主地板」的細連接。地圖越小要越保守，
        /// 否則會把整條走廊侵蝕掉、連通塊擴不出去。
        /// </summary>
        static int ErodeRadius(int fw, int fh) => Mathf.Max(1, Mathf.Min(3, Mathf.Min(fw, fh) / 40));

        // ---- 背景圖 → 每子格色度 ----

        static float[] GetChroma(MapData map, Texture2D bg, int fw, int fh)
        {
            string key = $"{map.backgroundId}|{bg.width}x{bg.height}|{fw}x{fh}";
            if (key == _cacheKey && _cacheChroma != null) return _cacheChroma;

            var sum = new float[fw * fh * 3];
            var cnt = new int[fw * fh];

            // 逐「子格列」讀取：背景圖可能有 2500 萬像素，一次 GetPixels 全張會配置上百 MB。
            // 一次只讀一條（寬 × 約 30px），峰值不到 1MB。
            // ⚠ Texture2D 的 y 軸由下往上，子格 fy 由上往下 —— 這裡要翻過來。
            for (int fy = 0; fy < fh; fy++)
            {
                int py0 = (int)((long)fy * bg.height / fh);
                int py1 = (int)((long)(fy + 1) * bg.height / fh);
                if (py1 <= py0) py1 = py0 + 1;
                if (py1 > bg.height) py1 = bg.height;

                int ty0 = bg.height - py1;          // 翻轉後的下界（含）
                int rows = py1 - py0;
                if (ty0 < 0) { ty0 = 0; }
                if (ty0 + rows > bg.height) rows = bg.height - ty0;
                if (rows <= 0) continue;

                Color[] pix;
                try { pix = bg.GetPixels(0, ty0, bg.width, rows); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[WalkableAutoGen] 讀背景圖像素失敗：{e.Message}");
                    return null;
                }

                for (int py = 0; py < rows; py++)
                {
                    int rowBase = py * bg.width;
                    for (int px = 0; px < bg.width; px++)
                    {
                        int fx = (int)((long)px * fw / bg.width);
                        if (fx >= fw) fx = fw - 1;
                        int idx = fy * fw + fx;
                        Color c = pix[rowBase + px];
                        sum[idx * 3] += c.r; sum[idx * 3 + 1] += c.g; sum[idx * 3 + 2] += c.b;
                        cnt[idx]++;
                    }
                }
            }

            // 平均 → 色度（除以 r+g+b，去掉亮度；陰影下的同一材質色度幾乎不變）
            var chroma = new float[fw * fh * 3];
            for (int i = 0; i < fw * fh; i++)
            {
                int n = Mathf.Max(1, cnt[i]);
                float r = sum[i * 3] / n, g = sum[i * 3 + 1] / n, b = sum[i * 3 + 2] / n;
                float s = Mathf.Max(0.0001f, r + g + b);
                chroma[i * 3] = r / s * 255f;
                chroma[i * 3 + 1] = g / s * 255f;
                chroma[i * 3 + 2] = b / s * 255f;
            }

            _cacheKey = key;
            _cacheChroma = chroma;
            return chroma;
        }

        // ---- 形態學與連通（方形結構元素；不依賴任何外部套件）----

        /// <summary>
        /// 方形結構元素的膨脹。**拆成橫、縱兩次一維掃描**（方形是可分離的）：
        /// O(n·2r) 而不是 O(n·r²)，r=2 時快 6 倍 —— 拖容差滑桿要即時重算，這裡是熱點。
        /// </summary>
        static bool[] Dilate(bool[] m, int w, int h, int r)
        {
            var tmp = new bool[m.Length];
            for (int y = 0; y < h; y++)
            {
                int rowBase = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (!m[rowBase + x]) continue;
                    int x0 = Mathf.Max(0, x - r), x1 = Mathf.Min(w - 1, x + r);
                    for (int xx = x0; xx <= x1; xx++) tmp[rowBase + xx] = true;
                }
            }
            var o = new bool[m.Length];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (!tmp[y * w + x]) continue;
                    int y0 = Mathf.Max(0, y - r), y1 = Mathf.Min(h - 1, y + r);
                    for (int yy = y0; yy <= y1; yy++) o[yy * w + x] = true;
                }
            }
            return o;
        }

        /// <summary>侵蝕＝對補集做膨脹再取反。邊界視為「外面」，所以貼邊的區域會被侵蝕掉一圈（正確：地圖外就是牆）。</summary>
        static bool[] Erode(bool[] m, int w, int h, int r)
        {
            var inv = new bool[m.Length];
            for (int i = 0; i < m.Length; i++) inv[i] = !m[i];
            var d = Dilate(inv, w, h, r);
            var o = new bool[m.Length];
            for (int i = 0; i < o.Length; i++) o[i] = !d[i];
            // 邊界一圈：補集膨脹碰不到圖外，手動把貼邊的 r 圈也侵蝕掉
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (x < r || y < r || x >= w - r || y >= h - r) o[y * w + x] = false;
            return o;
        }

        /// <summary>取與 seed 連通的那一塊（4 連通 BFS）。seed 不在遮罩內就回全 false。</summary>
        static bool[] Connected(bool[] m, int w, int h, int seed)
        {
            var o = new bool[m.Length];
            if (seed < 0 || seed >= m.Length || !m[seed]) return o;
            var st = new Stack<int>();
            st.Push(seed); o[seed] = true;
            while (st.Count > 0)
            {
                int i = st.Pop();
                int x = i % w, y = i / w;
                if (x > 0     && m[i - 1] && !o[i - 1]) { o[i - 1] = true; st.Push(i - 1); }
                if (x < w - 1 && m[i + 1] && !o[i + 1]) { o[i + 1] = true; st.Push(i + 1); }
                if (y > 0     && m[i - w] && !o[i - w]) { o[i - w] = true; st.Push(i - w); }
                if (y < h - 1 && m[i + w] && !o[i + w]) { o[i + w] = true; st.Push(i + w); }
            }
            return o;
        }
    }
}
