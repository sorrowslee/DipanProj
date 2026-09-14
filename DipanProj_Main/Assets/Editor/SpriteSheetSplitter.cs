using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// AutoSprite 序列圖切割工具（格線由每張圖自己推測，排版／解析度都不必固定）。
///
/// 用途：AutoSprite 會把一個動作輸出成「一張大序列圖」（常見是 5×5＝25 格；它推薦的 perfect loop
/// 張數較少時就變成 5×3、4×3、5×2…；素材拿重繪工具高清化之後整張圖還會放大 2~4 倍），
/// 但本專案的 route B 程式逐格動畫吃的是「資料夾裡的單張幀」。三個選單入口（Project Tools）：
///
///   1. <b>Split Sprite Sheet</b>：選一張序列圖 → 切成單張幀、寫回<b>同資料夾</b>、刪掉原圖（原始單張模式，行為不變）。
///   2. <b>Split Sprite Sheets（切到檔名子資料夾）</b>：選一個資料夾，掃它<b>第一層</b>的 PNG，
///      每張 <c>B.png</c> 切到 <c>該資料夾/B/</c> 子資料夾（幀名 B_01.png…）。
///      適合「一包 idle.png / walk.png / dead.png 丟在角色資料夾」的情境——切完直接就是 route B 的資料夾結構。
///   3. <b>Split Sprite Sheets（整包就地切割）</b>：選一個資料夾，<b>遞迴</b>掃所有子資料夾裡的 PNG，
///      每張序列圖<b>就地</b>切成幀（幀留在 sheet 原本所在的資料夾）。
///      適合「sheet 已經各自放進 idle/walk/attack/dead 資料夾」的角色包，選角色資料夾一鍵全切。
///
/// 共同切割規格：
///   1. <b>格線由每張圖自己推測，不寫死任何排版</b>（見下「怎麼推測格線」）。所以序列圖<b>解析度放大 2~4 倍</b>、
///      或 AutoSprite 的 perfect loop 讓<b>張數不滿 25</b>，都不必改任何設定。
///   2. 順序 row-major（左→右、上→下）。
///   3. 命名 = 來源檔名前綴 + 兩位數、從 _01 起（例：walk.png → walk_01.png、walk_02.png…），與 CHARACTER_SETUP 一致。
///   4. 全透明的空格自動跳過、不輸出（序號在保留的幀上連續編，不留洞）。
///   5. 不翻轉、不縮放：維持原朝向（朝右）與合圖本身的解析度。朝向交給遊戲端 flipX 處理。
///   6. 切割成功後刪掉原始序列圖（連同 .meta）。
///   7. 不清理資料夾裡既有的舊幀（同名覆寫、不同名保留）。
///
/// <b>怎麼推測格線（<see cref="GuessGrid"/>）</b>——關鍵觀察：<b>一格放一幀，所以正確的格線一定落在
/// 幀與幀的交界上</b>，線兩側不會同時是角色的實體像素；切錯則幾乎每條線都把角色攔腰切開。演算法三步：
///   ① 候選格邊長 ＝ <b>圖寬與圖高的公因數</b>（格子是正方形），再濾掉格邊長 &lt; <see cref="MinCell"/>、
///      格數 &lt; 2 或 &gt; <see cref="MaxCells"/> 的（1 格＝單張圖、太多格＝一定切錯）。
///   ② 對每個候選算「<b>跨線率</b>」＝內部格線兩側同時是實體像素的點數 ÷ 內部格線總長度，取最低者；
///      同分取<b>較小的 cell</b>（擋掉「cell 剛好是正解倍數」那種也零跨線的粗排版，例如把 4×4 看成 2×2）。
///      alpha &lt; <see cref="SolidAlpha"/> 的抗鋸齒羽化不算實體，否則正解會出現零星偽跨線。
///   ③ 最低跨線率 &gt; <see cref="MaxCrossRatio"/>，或切出來有內容的格子少於 <see cref="MinNonEmptyCells"/>
///      ＝ 認不出格線 → 不切、附上候選一覽說明原因。
/// <b>⚠ 這三步沒有一處用到欄數、列數或圖片尺寸當基準</b>——AutoSprite 的排版（5×5／5×3／4×3／5×2…）
/// 與高清重繪後的放大倍率都是變數，只有「格子是正方形」與「線不該切穿角色」兩個前提是不變的。
/// 實測分離度極高、中間沒有模糊地帶（2026-09-14 量）：正解（蟲皇 dead 5×5）<b>0.00%</b>；
/// 同一張圖的其他排版 8×8 21%／4×4 29%／2×2 49%；單張立繪 `ZhaYu/idle` 500×500 最低 39%、
/// `Ghost_GrandMa` 408×612 最低 19%、已切好的 256×256 幀最低 24%，全部落在門檻外被擋下。
///
/// <b>為什麼不是「貼邊率」</b>（2026-09-14 改，見 readme/PROBLEMS.md <b>C14</b>）：初版判的是
/// 「內容有沒有<b>碰到</b>格線」，前提是「AutoSprite 每一格四周都留白」。倒下／展翅這類橫向撐滿整格的
/// 動作會打破那個前提——蟲皇 dead 的第 7~11 幀翅膀張到 212~256px（格子就是 256），
/// 貼邊率 20% 超過當時 10% 的門檻，<b>正確的 5×5 被判成切錯而整張拒切</b>。
/// 貼邊不等於切穿；真正要防的是切穿，所以改成直接量它。
///
/// ⚠ 批次模式（2、3）的兩道守衛：
///   ① <b>檔名結尾是「_兩位以上數字」的 PNG 視為「已是切好的幀」直接跳過</b>（例 <c>walk_01.png</c>）。
///   ② <b>「整包就地切割」限定</b>：<b>同一資料夾內 PNG 超過一張 ＝ 已經切好了，跳過</b>
///      （作者的製程：要切的動作資料夾裡只會放一張合圖；要重切就把資料夾清空只留合圖）。
///      「切到檔名子資料夾」<b>不</b>套用這條——那個模式的來源資料夾本來就會放好幾張 sheet（idle.png/walk.png/…）。
///   第三道是推測本身：認不出格線就不切（上面實測的 31%~76% 那組就是靠這道擋下的）。
///   單張模式沒有前兩道守衛（使用者親手選那張檔，視為明確意圖）。
///
/// 純 Editor + 純 C#（Texture2D 切格 → EncodeToPNG），無外部依賴。
/// </summary>
public static class SpriteSheetSplitter
{
    // ── 格線推測參數（見檔頭「怎麼推測格線」）──
    // ⚠ 這四個都是「與排版、解析度無關」的量：沒有任何一個是格數或圖片尺寸。
    private const int MinCell = 32;             // 格邊長下限：再小就不像一幀角色圖了
    private const int MaxCells = 64;            // 格數上限：擋掉「切成上百張」（不是 AutoSprite 的張數上限）
    private const float MaxCrossRatio = 0.02f;  // 跨線率門檻：低於這個才算切對（實測正解 0%、切錯 19% 以上）
    private const byte SolidAlpha = 16;         // 低於此 alpha 視為抗鋸齒羽化，不算「實體像素」
    private const int MinNonEmptyCells = 2;     // 切出來至少要有兩格有內容，才像一張合圖

    // ─────────────────────────────────────────────
    //  入口 1：單張（原始模式，行為不變）
    // ─────────────────────────────────────────────

    [MenuItem("Project Tools/Split Sprite Sheet", false, 40)]
    public static void SplitSelectedSheet()
    {
        string path = EditorUtility.OpenFilePanel("選擇要切割的序列圖（5 欄，列數自動判斷）", Application.dataPath, "png");
        if (string.IsNullOrEmpty(path)) return;   // 取消

        if (!IsUnderAssets(path))
        {
            EditorUtility.DisplayDialog("無法處理",
                "請選擇位於本專案 Assets 資料夾內的序列圖。", "OK");
            return;
        }

        var r = SplitOne(path, Path.GetDirectoryName(path), isBatch: false);
        if (!r.ok)
        {
            EditorUtility.DisplayDialog(r.errorTitle, r.error, "OK");
            return;
        }

        AssetDatabase.Refresh();

        string prefix = Path.GetFileNameWithoutExtension(path);
        string msg = $"切割完成：寫出 {r.written} 張幀（{prefix}_01 ~ {prefix}_{r.written:D2}）。\n" +
                     $"格數 {r.cols}×{r.rows}，跳過 {r.skipped} 個空格。\n" +
                     $"已刪除原始序列圖。";
        Debug.Log($"[SpriteSheetSplitter] {msg}" +
                  (r.skipped > 0 ? $"\n跳過的格子(列,欄)：{r.skippedCells}" : ""));
        EditorUtility.DisplayDialog("Split Sprite Sheet", msg, "OK");
    }

    // ─────────────────────────────────────────────
    //  入口 2：資料夾第一層 → 各切到「檔名」子資料夾
    // ─────────────────────────────────────────────

    [MenuItem("Project Tools/Split Sprite Sheets（切到檔名子資料夾）", false, 41)]
    public static void SplitFolderIntoNamedSubfolders()
    {
        RunBatch(
            title: "選擇裝著序列圖的資料夾（只掃第一層 PNG）",
            searchOption: SearchOption.TopDirectoryOnly,
            outputFolderFor: sheetPath =>
                Path.Combine(Path.GetDirectoryName(sheetPath), Path.GetFileNameWithoutExtension(sheetPath)),
            modeName: "切到檔名子資料夾",
            loneFileGuard: false);   // 這個模式的來源資料夾本來就會放好幾張 sheet，不能用「只有一張」當守衛
    }

    // ─────────────────────────────────────────────
    //  入口 3：整包遞迴 → 各自就地切割（幀留在 sheet 原資料夾）
    // ─────────────────────────────────────────────

    [MenuItem("Project Tools/Split Sprite Sheets（整包就地切割）", false, 42)]
    public static void SplitFolderInPlaceRecursive()
    {
        RunBatch(
            title: "選擇要整包切割的資料夾（遞迴掃所有子資料夾的 PNG，就地切成幀）",
            searchOption: SearchOption.AllDirectories,
            outputFolderFor: Path.GetDirectoryName,
            modeName: "整包就地切割",
            loneFileGuard: true);    // 作者的製程：要切的動作資料夾裡只會放一張合圖；多於一張＝已經切好了
    }

    // ─────────────────────────────────────────────
    //  批次共同流程
    // ─────────────────────────────────────────────

    private static void RunBatch(string title, SearchOption searchOption,
                                 System.Func<string, string> outputFolderFor, string modeName,
                                 bool loneFileGuard)
    {
        string folder = EditorUtility.OpenFolderPanel(title, Application.dataPath, "");
        if (string.IsNullOrEmpty(folder)) return;   // 取消

        if (!IsUnderAssets(folder))
        {
            EditorUtility.DisplayDialog("無法處理",
                "請選擇位於本專案 Assets 資料夾內的資料夾。", "OK");
            return;
        }

        // Directory.GetFiles 的 "*.png" 在部分平台會同時撈到 .PNG，統一自己過濾一次（.meta 天然不會命中）。
        string[] pngs = Directory.GetFiles(folder, "*.*", searchOption)
            .Where(p => p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, System.StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (pngs.Length == 0)
        {
            EditorUtility.DisplayDialog("沒有東西可切",
                $"「{Path.GetFileName(folder)}」{(searchOption == SearchOption.AllDirectories ? "（含子資料夾）" : "第一層")}找不到任何 PNG。", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog($"Split Sprite Sheets（{modeName}）",
                $"在「{Path.GetFileName(folder)}」找到 {pngs.Length} 張 PNG。\n\n" +
                "格線由每張圖自己推測（解析度與格數都不必固定）。\n" +
                (loneFileGuard ? "同一資料夾內 PNG 超過一張 → 視為已經切好，跳過。\n" : "") +
                "檔名結尾是 _數字的視為已切好的幀、自動跳過；認不出格線的跳過並列入報告。\n" +
                "切割成功的序列圖會被刪除（幀已寫出）。\n\n確定開始？",
                "開始", "取消"))
            return;

        int okCount = 0, skipFrame = 0, skipMulti = 0;
        var pngCountCache = new Dictionary<string, int>();   // 資料夾 → 其中的 PNG 張數（同一資料夾只數一次）
        var failures = new List<string>();   // 「檔名：原因」
        var okLines  = new List<string>();   // 「相對路徑 → N 幀」
        string dataPath = Application.dataPath.Replace('\\', '/');

        try
        {
            for (int i = 0; i < pngs.Length; i++)
            {
                string p = pngs[i];
                string rel = p.Replace('\\', '/').Substring(dataPath.Length).TrimStart('/');
                EditorUtility.DisplayProgressBar($"Split Sprite Sheets（{modeName}）",
                    $"{i + 1}/{pngs.Length}　{rel}", (float)(i + 1) / pngs.Length);

                // 守衛：作者的製程是「要切的動作資料夾裡只放一張合圖」，所以同資料夾 PNG 超過一張＝已經切好了。
                if (loneFileGuard && CountPngInFolder(Path.GetDirectoryName(p), pngCountCache) > 1)
                {
                    skipMulti++;
                    continue;
                }

                var r = SplitOne(p, outputFolderFor(p), isBatch: true);
                if (r.ok)
                {
                    okCount++;
                    okLines.Add($"{rel} → {r.written} 幀（{r.cols}×{r.rows}，空格 {r.skipped}）");
                }
                else if (r.isSingleCellSkip)
                {
                    skipFrame++;   // 已是單張幀，安靜跳過（只計數）
                }
                else
                {
                    failures.Add($"{rel}：{r.error}");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();

        var sb = new StringBuilder();
        sb.AppendLine($"切割 {okCount} 張序列圖（格線各自推測）；跳過 {skipFrame} 張檔名像單張幀的 PNG" +
                      (loneFileGuard ? $"、{skipMulti} 張所在資料夾已有多張 PNG（＝已切好）" : "") + "。");
        if (failures.Count > 0)
        {
            sb.AppendLine($"\n⚠ {failures.Count} 張無法處理（原檔保留未動）：");
            foreach (var f in failures) sb.AppendLine("・" + f);
        }
        Debug.Log($"[SpriteSheetSplitter]（{modeName}）\n{sb}\n" + string.Join("\n", okLines));
        EditorUtility.DisplayDialog($"Split Sprite Sheets（{modeName}）", sb.ToString(), "OK");
    }

    // ─────────────────────────────────────────────
    //  單張核心（單張／批次共用）
    // ─────────────────────────────────────────────

    private struct SplitResult
    {
        public bool ok;
        public bool isSingleCellSkip;   // 批次守衛：檔名結尾 _數字（已是切好的單張幀）
        public string errorTitle, error;
        public int written, skipped, cols, rows;
        public string skippedCells;
    }

    // ─────────────────────────────────────────────
    //  格線推測（不靠寫死的排版，從圖本身算出來）
    // ─────────────────────────────────────────────

    private struct GridGuess
    {
        public bool ok;
        public int cols, rows, cell;
        public float crossRatio;    // 採用的候選的跨線率（越低越確定切對；0 ＝沒有任何一幀被切穿）
        public string detail;       // 給錯誤訊息用的候選一覽
    }

    /// <summary>
    /// 從圖本身推測格線。原理見檔頭：<b>切對時沒有任何一幀會被格線切穿</b>。
    /// 候選限縮成「圖寬與圖高的公因數」（格子是正方形），再用<b>跨線率</b>挑出唯一解；
    /// 同分時取較小的 cell（擋掉「cell 剛好是正解的倍數」那種也零跨線的粗排版，例如把 4×4 看成 2×2）。
    /// <b>全程不假設欄數、列數或圖片尺寸</b>——5×5／5×3／4×3／5×2 與高清放大過的圖都走同一條路。
    /// </summary>
    private static GridGuess GuessGrid(Texture2D sheet)
    {
        var g = new GridGuess();
        int w = sheet.width, h = sheet.height;
        Color32[] px = sheet.GetPixels32();   // 一次讀全圖：索引 [y * w + x]，y=0 是最下面一列
        var sb = new StringBuilder();
        float bestRatio = 2f;
        bool found = false;

        int limit = Mathf.Min(w, h);
        for (int size = MinCell; size <= limit; size++)
        {
            if (w % size != 0 || h % size != 0) continue;          // 格子必須是正方形
            int cols = w / size, rows = h / size, n = cols * rows;
            if (n < 2 || n > MaxCells) continue;                   // 1 格＝單張圖、太多格＝一定切錯

            float ratio = CrossRatio(px, w, h, size, cols, rows);
            int nonEmpty = CountNonEmptyCells(px, w, size, cols, rows);
            sb.Append($"{cols}×{rows}:{ratio * 100f:0.##}%(有內容 {nonEmpty} 格) ");

            // 嚴格小於 ＋ size 由小到大 ⇒ 同分自動取較小的 cell。
            if (ratio < bestRatio && nonEmpty >= MinNonEmptyCells)
            {
                bestRatio = ratio;
                g.cols = cols; g.rows = rows; g.cell = size; g.crossRatio = ratio;
                found = true;
            }
        }

        g.detail = sb.Length > 0 ? sb.ToString().TrimEnd() : "（沒有任何候選：圖寬與圖高沒有合適的公因數）";
        g.ok = found && bestRatio <= MaxCrossRatio;
        return g;
    }

    /// <summary>
    /// 這組格線的<b>跨線率</b>＝「格線兩側同時是實體像素」的點數 ÷ 所有內部格線的總長度。
    /// 切對時是 0（線正好落在幀與幀的交界）；切錯時角色被攔腰切開，這個值會是兩位數的百分比。
    /// <b>只數內部格線，不數圖的外框</b>——外框不是切下去的位置，內容貼著外框是正常的
    /// （倒下、展翅這類橫向撐滿整格的動作就會貼滿，見 readme/PROBLEMS.md <b>C14</b>）。
    /// <paramref name="px"/> 的 alpha 低於 <see cref="SolidAlpha"/> 一律不算，否則抗鋸齒羽化
    /// 會讓正解出現零星的偽跨線（蟲皇 dead 實測：兩個點，alpha 各為 2 與 5）。
    /// </summary>
    private static float CrossRatio(Color32[] px, int w, int h, int size, int cols, int rows)
    {
        int cross = 0, total = 0;

        for (int c = 1; c < cols; c++)          // 縱線：x = c * size
        {
            int x = c * size;
            for (int y = 0; y < h; y++)
                if (px[y * w + x - 1].a >= SolidAlpha && px[y * w + x].a >= SolidAlpha) cross++;
            total += h;
        }

        for (int r = 1; r < rows; r++)          // 橫線：y = r * size
        {
            int y = r * size;
            for (int x = 0; x < w; x++)
                if (px[(y - 1) * w + x].a >= SolidAlpha && px[y * w + x].a >= SolidAlpha) cross++;
            total += w;
        }

        return total > 0 ? (float)cross / total : 1f;   // 沒有任何內部格線 ⇒ 當成切錯
    }

    /// <summary>這組格線切出來有幾格「不是全透明」（＝真的會寫出一張幀）。</summary>
    private static int CountNonEmptyCells(Color32[] px, int texW, int size, int cols, int rows)
    {
        int n = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (!CellIsEmpty(px, texW, c * size, r * size, size)) n++;
        return n;
    }

    /// <summary>這一格是不是全透明（判準與寫出階段的 <see cref="IsFullyTransparent"/> 一致：alpha 全為 0）。</summary>
    private static bool CellIsEmpty(Color32[] px, int texW, int x0, int y0, int size)
    {
        for (int y = y0; y < y0 + size; y++)
            for (int x = x0; x < x0 + size; x++)
                if (px[y * texW + x].a > 0) return false;
        return true;
    }

    /// <summary>
    /// 把一張序列圖依 <see cref="GuessGrid"/> 推測出的格線切成單張幀寫進 <paramref name="outFolder"/>
    /// （不存在會建立），成功後刪掉原圖（含 .meta）。
    /// <paramref name="isBatch"/>：批次模式為 true（檔名像自己的輸出就跳過）；單張模式為 false（親手選檔＝明確意圖，照切）。
    /// </summary>
    private static SplitResult SplitOne(string path, string outFolder, bool isBatch)
    {
        var res = new SplitResult();

        // 批次守衛：檔名結尾是 _數字 → 這是上一次切出來的幀，安靜跳過（冪等性，見檔頭 ⚠）。
        if (isBatch && LooksLikeSplitFrame(path))
        {
            res.isSingleCellSkip = true;
            res.error = "檔名結尾是 _數字（已是切好的單張幀），跳過。";
            return res;
        }

        byte[] bytes = File.ReadAllBytes(path);
        var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!sheet.LoadImage(bytes))
        {
            Object.DestroyImmediate(sheet);
            res.errorTitle = "讀取失敗"; res.error = "這張 PNG 無法載入。";
            return res;
        }

        int sw = sheet.width, sh = sheet.height;

        // 從圖本身推測格線（不再假設任何固定排版）——解析度放大幾倍、張數滿不滿 25 都不影響。
        var guess = GuessGrid(sheet);
        if (!guess.ok)
        {
            Object.DestroyImmediate(sheet);
            res.errorTitle = "認不出格線";
            res.error = $"圖片尺寸為 {sw}×{sh}，找不到「切下去不會切穿角色」的格線。\n\n" +
                        $"各候選排版的跨線率（格線兩側同時是角色實體像素的比例，越低越可能是正確答案）：\n{guess.detail}\n\n" +
                        $"門檻是 {MaxCrossRatio * 100f:0.##}%。真正的合圖是 0%（線落在幀與幀的交界）；" +
                        "都高於門檻代表這張多半不是合圖（單張立繪／已經切好的幀），或者它被裁切過、格線不再對齊。";
            return res;
        }

        res.cols = guess.cols;
        res.rows = guess.rows;
        int cellW = guess.cell;
        int cellH = guess.cell;

        string prefix = Path.GetFileNameWithoutExtension(path);
        Directory.CreateDirectory(outFolder);

        // 切格（row-major）。Unity 貼圖原點在左下，故第 r 列（由上往下）對應 y = sh - (r+1)*cellH。
        var skippedCells = new StringBuilder();
        for (int r = 0; r < res.rows; r++)
        {
            for (int c = 0; c < res.cols; c++)
            {
                int x = c * cellW;
                int y = sh - (r + 1) * cellH;
                Color[] cell = sheet.GetPixels(x, y, cellW, cellH);

                if (IsFullyTransparent(cell))   // 全透明 → 跳過
                {
                    res.skipped++;
                    skippedCells.Append($"({r},{c}) ");
                    continue;
                }

                var frame = new Texture2D(cellW, cellH, TextureFormat.RGBA32, false);
                frame.SetPixels(cell);
                frame.Apply();

                res.written++;
                string outName = $"{prefix}_{res.written:D2}.png";   // 兩位數、從 _01 起、連續編號
                File.WriteAllBytes(Path.Combine(outFolder, outName), frame.EncodeToPNG());
                Object.DestroyImmediate(frame);
            }
        }

        Object.DestroyImmediate(sheet);
        res.skippedCells = skippedCells.ToString();

        if (res.written == 0)
        {
            // 整張全透明：什麼都沒寫出，原圖保留（刪了等於資料憑空消失）。
            res.errorTitle = "整張全透明";
            res.error = "所有格子都是全透明，沒有寫出任何幀；原始序列圖保留未動。";
            return res;
        }

        // 刪掉原始序列圖（用 AssetDatabase 才會一併清掉 .meta）
        string dataPath = Application.dataPath.Replace('\\', '/');
        string assetRelPath = "Assets" + path.Replace('\\', '/').Substring(dataPath.Length);
        AssetDatabase.DeleteAsset(assetRelPath);

        res.ok = true;
        return res;
    }

    /// <summary>
    /// 檔名是否長得像「這支工具自己切出來的幀」（結尾 <c>_</c> 加兩位以上數字，例 <c>walk_01</c>）。
    /// 批次模式用它跳過自己的輸出，維持「跑幾次結果都一樣」。
    /// </summary>
    private static bool LooksLikeSplitFrame(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        int i = name.Length;
        while (i > 0 && name[i - 1] >= '0' && name[i - 1] <= '9') i--;
        int digits = name.Length - i;
        return digits >= 2 && i >= 2 && name[i - 1] == '_';
    }

    /// <summary>數這個資料夾第一層有幾張 PNG（結果快取，同一資料夾只數一次）。</summary>
    private static int CountPngInFolder(string folder, Dictionary<string, int> cache)
    {
        if (string.IsNullOrEmpty(folder)) return 0;
        if (cache.TryGetValue(folder, out int n)) return n;
        n = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
            .Count(f => f.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase));
        cache[folder] = n;
        return n;
    }

    private static bool IsUnderAssets(string absPath)
    {
        string dataPath = Application.dataPath.Replace('\\', '/');
        return absPath.Replace('\\', '/').StartsWith(dataPath);
    }

    /// <summary>整格 alpha 皆為 0 視為空格。</summary>
    private static bool IsFullyTransparent(Color[] pixels)
    {
        for (int i = 0; i < pixels.Length; i++)
            if (pixels[i].a > 0f) return false;
        return true;
    }
}
