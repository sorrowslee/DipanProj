using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Dipan.MapRuntime
{
    /// <summary>
    /// 地上物的「佔位遮罩」：把一張素材圖切成子格，逐格記錄「這格有沒有畫東西」。
    ///
    /// <para><b>為什麼需要它</b>：舊做法是拿「整張圖不透明像素的<b>外接矩形</b>」當碰撞框
    /// （<see cref="MapSpriteLoader.GetAlphaLocalBox"/>）。外接矩形只由最外圍那一個像素決定，
    /// 所以<b>只能縮框、不能挖洞</b>——斜擺的屏風、燈籠的細桿、椅腳之間的空隙，圖上明明是透明的
    /// 卻照樣擋路，而且把素材的邊切掉也救不了。改成逐格遮罩後，透明處就是可以走。
    /// 見 readme/PROBLEMS.md「地上物碰撞」與 readme/MAP_LOADER_SETUP.md。</para>
    ///
    /// <para><b>座標慣例</b>：<see cref="cells"/>[0] = 圖的<b>最上面</b>一列（與可走層 <c>blocked</c> 相同的慣例），
    /// 字元 '1' = 擋、'0' = 不擋。列字串長度 = <see cref="cols"/>。</para>
    ///
    /// <para><b>為什麼遮罩可以預先烘焙（與地圖無關）</b>：素材一律以 <c>PPU = 256/tileSize</c> 載入
    /// （見 <see cref="MapSpriteLoader"/>），所以一張 w 像素寬的圖在世界上就是 <c>w/256</c> 「格」寬——
    /// <b>與該地圖的 tileSize 無關</b>。子格大小定義成「1 格 / subdiv」，因此同一份遮罩可以跨地圖共用，
    /// 也就能在 Project Tools → Sync Map Assets 時算好、寫進 catalog.json。</para>
    ///
    /// <para><b>兩條產生路徑共用 <see cref="ObjectFootprint.Scan"/></b>：
    /// ① 烘焙（<c>MapAssetSyncTool</c>，寫進 catalog）；② runtime 退路（catalog 沒有遮罩時當場掃）。
    /// 共用同一段程式碼是刻意的——這個專案的 catalog 有四個產生器（兩支 C#、兩支 shell，見
    /// <see cref="MapAssetCategories"/>），shell 版不會烘遮罩，所以退路一定會被用到；
    /// 兩條路走鐘的話會變成「同一個物件在不同機器上擋路範圍不一樣」這種極難查的坑。</para>
    /// </summary>
    public class FootprintMask
    {
        /// <summary>每「格」切成幾份（子格大小 = tileSize / subdiv）。</summary>
        public int subdiv = ObjectFootprint.BakeSubdiv;
        /// <summary>子格欄數（= ceil(圖寬px / 256 * subdiv)）。</summary>
        public int cols;
        /// <summary>子格列數（= ceil(圖高px / 256 * subdiv)）。</summary>
        public int rows;
        /// <summary>每列一字串，'1'=擋 '0'=不擋；cells[0] = 圖的最上面一列。</summary>
        public List<string> cells;

        [JsonIgnore]
        public bool Ok => subdiv > 0 && cols > 0 && rows > 0 && cells != null && cells.Count == rows;

        /// <summary>某子格是否擋路。超出範圍一律回 false（＝不擋，寧可漏擋也不要憑空多擋）。</summary>
        public bool At(int cx, int cy)
        {
            if (cx < 0 || cy < 0 || cy >= rows || cells == null || cy >= cells.Count) return false;
            string row = cells[cy];
            return row != null && cx < row.Length && row[cx] == '1';
        }

        /// <summary>擋路子格總數。</summary>
        [JsonIgnore]
        public int SolidCount
        {
            get
            {
                if (!Ok) return 0;
                int n = 0;
                for (int y = 0; y < rows; y++)
                {
                    string row = cells[y];
                    if (row == null) continue;
                    for (int x = 0; x < row.Length; x++) if (row[x] == '1') n++;
                }
                return n;
            }
        }
    }

    /// <summary>
    /// 佔位遮罩的產生／降取樣工具。烘焙端（Editor）與 runtime 退路共用，見 <see cref="FootprintMask"/> 說明。
    /// </summary>
    public static class ObjectFootprint
    {
        /// <summary>alpha 低於此值視為透明（與 <see cref="MapSpriteLoader"/> 的去背邊門檻一致）。</summary>
        public const byte AlphaThreshold = 10;

        /// <summary>烘焙解析度：一律烘在較細的 8，runtime 想用 4 再降取樣（改解析度不必重跑同步）。</summary>
        public const int BakeSubdiv = 8;

        /// <summary>子格解析度的合理上限（再細下去碰撞條數暴增、對手感沒有幫助）。</summary>
        public const int MaxSubdiv = 16;

        /// <summary>
        /// 一個子格要有多少比例的像素不透明才算「擋」。
        /// <para>用 0 = 「只要有一個不透明像素就擋」＝最保守，但 AI 去背素材的邊緣有一圈半透明反鋸齒，
        /// 會讓每個物件的四周都多出一圈約 0.125 格的假邊。實測 0.25 能吃掉那圈假邊又不會挖穿實體
        /// （屏風 114→103 格、燈籠 62→41 格）。</para>
        /// </summary>
        public const float DefaultCoverage = 0.25f;

        /// <summary>
        /// 掃描一張貼圖產生佔位遮罩。全透明（或貼圖無效）回 null——呼叫端應退回「不生碰撞」或舊的單框做法。
        /// </summary>
        /// <param name="tex">素材貼圖（動畫地上物 = 第一幀，與 sprite/舊碰撞框的取樣來源一致）。</param>
        /// <param name="subdiv">每格切幾份。</param>
        /// <param name="coverage">一格要多少比例不透明才算擋，見 <see cref="DefaultCoverage"/>。</param>
        public static FootprintMask Scan(Texture2D tex, int subdiv, float coverage)
        {
            if (tex == null) return null;
            subdiv = Mathf.Clamp(subdiv, 1, MaxSubdiv);

            // 一個子格 = 幾個像素。素材以 256px = 1 格載入，故子格邊長 = 256/subdiv 像素。
            int cell = Mathf.Max(1, MapSpriteLoader.TileNativePx / subdiv);
            int w = tex.width, h = tex.height;
            if (w <= 0 || h <= 0) return null;

            int cols = Mathf.CeilToInt(w / (float)cell);
            int rows = Mathf.CeilToInt(h / (float)cell);

            Color32[] px;
            try { px = tex.GetPixels32(); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ObjectFootprint] 貼圖不可讀，無法掃描佔位遮罩：{e.Message}");
                return null;
            }

            // 門檻用「完整子格」的面積算：邊界上被截短的格子等於要求更高的比例才算擋，
            // 這正好是我們要的——圖的最外圈通常只是去背殘留。
            int need = Mathf.Max(1, Mathf.RoundToInt(cell * cell * Mathf.Clamp01(coverage)));

            var list = new List<string>(rows);
            var sb = new System.Text.StringBuilder(cols);
            int solid = 0;

            for (int cy = 0; cy < rows; cy++)
            {
                sb.Length = 0;
                // cy 是「從上數來第幾列」；貼圖列是由下往上，故要翻轉。
                int topY0 = cy * cell;
                int topY1 = Mathf.Min(h, topY0 + cell);
                int ty0 = h - topY1, ty1 = h - topY0;

                for (int cx = 0; cx < cols; cx++)
                {
                    int x0 = cx * cell, x1 = Mathf.Min(w, x0 + cell);
                    int n = 0;
                    bool on = false;
                    for (int y = ty0; y < ty1 && !on; y++)
                    {
                        int rowBase = y * w;
                        for (int x = x0; x < x1; x++)
                        {
                            if (px[rowBase + x].a <= AlphaThreshold) continue;
                            if (++n >= need) { on = true; break; }
                        }
                    }
                    if (on) solid++;
                    sb.Append(on ? '1' : '0');
                }
                list.Add(sb.ToString());
            }

            if (solid == 0) return null;   // 整張透明：當作沒有遮罩
            return new FootprintMask { subdiv = subdiv, cols = cols, rows = rows, cells = list };
        }

        /// <summary>
        /// 把要求的解析度收斂到 <see cref="BakeSubdiv"/> 的因數（目前 = 1/2/4/8）。
        ///
        /// <para>兩個理由，缺一不可：
        /// ① <see cref="Downsample"/> 只支援整除，填 3/5/6/7 會拿不到降取樣、每張圖每次載入都要重掃（烘焙白做）；
        /// ② <see cref="Scan"/> 的 <c>cell = 256/subdiv</c> 是<b>整數除法</b>，而呼叫端算世界尺寸用的是
        /// <c>tileSize/subdiv</c>。subdiv=6 時 cell=42px，但 42/256 ≠ 1/6，每一欄會累積 1.6% 的誤差、
        /// 整個形狀往右下漂。1/2/4/8 才會剛好整除（256/128/64/32）。</para>
        /// </summary>
        public static int SnapSubdiv(int subdiv)
        {
            if (subdiv >= BakeSubdiv) return BakeSubdiv;
            int best = 1;
            for (int d = 2; d <= BakeSubdiv; d++)
                if (BakeSubdiv % d == 0 && d <= subdiv && d > best) best = d;
            return best;
        }

        /// <summary>
        /// 把遮罩降到較粗的解析度（例：烘焙的 8 → 遊戲用的 4）。目標格只要涵蓋到任一擋路子格就算擋
        /// （寧可多擋也不要把實體挖穿）。無法整除或參數不合法時回 null。
        ///
        /// <para>⚠ <b>降取樣是較粗解析度的「正典定義」，不要拿它跟「直接在該解析度 Scan」混用。</b>
        /// 兩者結果<b>不一樣</b>：這裡是 OR（4 顆子格有 1 顆實心就算擋），等效門檻只有
        /// <see cref="DefaultCoverage"/> 的 1/k²；直接 Scan 則是整格算覆蓋率。實測同一張圖 subdiv=4，
        /// 兩種算法的實心格數差 10~38%（Cultist 18 vs 13）。混用的後果是「有烘過的機器和沒烘過的機器，
        /// 同一個物件擋路範圍不一樣」，而且完全靜默——所以 <see cref="MapSpriteLoader.GetFootprint"/>
        /// 一律「先取得 BakeSubdiv 的遮罩（烘好的或當場掃的），再降取樣」，兩條路的計算完全相同。</para>
        /// </summary>
        public static FootprintMask Downsample(FootprintMask src, int targetSubdiv)
        {
            if (src == null || !src.Ok) return null;
            if (targetSubdiv <= 0 || targetSubdiv > src.subdiv) return null;
            if (targetSubdiv == src.subdiv) return src;
            if (src.subdiv % targetSubdiv != 0) return null;   // 只支援整除（8→4/2/1），其餘交給重掃

            int k = src.subdiv / targetSubdiv;
            int cols = Mathf.CeilToInt(src.cols / (float)k);
            int rows = Mathf.CeilToInt(src.rows / (float)k);

            var list = new List<string>(rows);
            var sb = new System.Text.StringBuilder(cols);
            int solid = 0;

            for (int cy = 0; cy < rows; cy++)
            {
                sb.Length = 0;
                for (int cx = 0; cx < cols; cx++)
                {
                    bool on = false;
                    for (int sy = cy * k; sy < (cy + 1) * k && !on; sy++)
                        for (int sx = cx * k; sx < (cx + 1) * k; sx++)
                            if (src.At(sx, sy)) { on = true; break; }
                    if (on) solid++;
                    sb.Append(on ? '1' : '0');
                }
                list.Add(sb.ToString());
            }

            if (solid == 0) return null;
            return new FootprintMask { subdiv = targetSubdiv, cols = cols, rows = rows, cells = list };
        }

        /// <summary>
        /// 擋路子格佔「其外接矩形」的比例（0~1）。接近 1 = 這張圖本來就是實心方塊，
        /// 用單一方框跟逐格貼合幾乎沒有差別，可以省掉一顆 CompositeCollider2D。
        /// 沒有遮罩回 1（＝當作實心，走單框那條路）。
        /// </summary>
        public static float FillRatio(FootprintMask m)
        {
            if (m == null || !m.Ok) return 1f;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1, solid = 0;
            for (int y = 0; y < m.rows; y++)
            {
                string row = m.cells[y];
                if (row == null) continue;
                for (int x = 0; x < row.Length; x++)
                {
                    if (row[x] != '1') continue;
                    solid++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0) return 1f;
            int area = (maxX - minX + 1) * (maxY - minY + 1);
            return area <= 0 ? 1f : solid / (float)area;
        }
    }
}
