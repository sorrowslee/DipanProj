using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 5×5（或任意 N×M）序列圖切割工具。
///
/// 用途：AutoSprite 之類的工具會把一個動作輸出成「一張大序列圖」（每格 256×256、整張是 256 的整數倍），
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
/// 共同切割規格（依與使用者確認過的規格）：
///   1. 每格固定 256×256，格數 = 圖寬÷256 × 圖高÷256（自動推算；非 256 整數倍 → 單張模式中止並提示、批次模式跳過該檔並列入報告）。
///   2. 順序 row-major（左→右、上→下）。
///   3. 命名 = 來源檔名前綴 + 兩位數、從 _01 起（例：walk.png → walk_01.png、walk_02.png…），與 CHARACTER_SETUP 一致。
///   4. 全透明的空格自動跳過、不輸出（序號在保留的幀上連續編，不留洞）。
///   5. 不翻轉、不縮放：維持原朝向（朝右）與原生 256 解析度。朝向交給遊戲端 flipX 處理。
///   6. 切割成功後刪掉原始序列圖（連同 .meta）。
///   7. 不清理資料夾裡既有的舊幀（同名覆寫、不同名保留）。
///
/// ⚠ 批次模式（2、3）額外守衛：<b>剛好 256×256（1×1 格）的 PNG 視為「已是單張幀」直接跳過</b>——
/// 否則「整包就地切割」跑第二次時，會把上次切出來的每一張幀再「切割」一次（改名 + 刪原檔），整包被靜默重排。
/// 單張模式沒有這個守衛（使用者親手選那張檔，視為明確意圖，維持舊行為）。
///
/// 純 Editor + 純 C#（Texture2D 切格 → EncodeToPNG），無外部依賴。
/// </summary>
public static class SpriteSheetSplitter
{
    private const int CellSize = 256;

    // ─────────────────────────────────────────────
    //  入口 1：單張（原始模式，行為不變）
    // ─────────────────────────────────────────────

    [MenuItem("Project Tools/Split Sprite Sheet", false, 40)]
    public static void SplitSelectedSheet()
    {
        string path = EditorUtility.OpenFilePanel("選擇要切割的序列圖（每格 256×256）", Application.dataPath, "png");
        if (string.IsNullOrEmpty(path)) return;   // 取消

        if (!IsUnderAssets(path))
        {
            EditorUtility.DisplayDialog("無法處理",
                "請選擇位於本專案 Assets 資料夾內的序列圖。", "OK");
            return;
        }

        var r = SplitOne(path, Path.GetDirectoryName(path), allowSingleCell: true);
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
            modeName: "切到檔名子資料夾");
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
            modeName: "整包就地切割");
    }

    // ─────────────────────────────────────────────
    //  批次共同流程
    // ─────────────────────────────────────────────

    private static void RunBatch(string title, SearchOption searchOption,
                                 System.Func<string, string> outputFolderFor, string modeName)
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
                "將逐張切割（剛好 256×256 的視為已是單張幀、自動跳過；\n" +
                "非 256 整數倍的跳過並列入報告）。\n" +
                "切割成功的序列圖會被刪除（幀已寫出）。\n\n確定開始？",
                "開始", "取消"))
            return;

        int okCount = 0, skipFrame = 0;
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

                var r = SplitOne(p, outputFolderFor(p), allowSingleCell: false);
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
        sb.AppendLine($"切割 {okCount} 張序列圖；跳過 {skipFrame} 張已是單張幀的 PNG。");
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
        public bool isSingleCellSkip;   // 批次守衛：剛好 1×1 格（已是單張幀）
        public string errorTitle, error;
        public int written, skipped, cols, rows;
        public string skippedCells;
    }

    /// <summary>
    /// 把一張序列圖切成單張幀寫進 <paramref name="outFolder"/>（不存在會建立），成功後刪掉原圖（含 .meta）。
    /// <paramref name="allowSingleCell"/>：單張模式為 true（256×256 也照切，維持舊行為）；批次模式為 false（視為已是幀、跳過）。
    /// </summary>
    private static SplitResult SplitOne(string path, string outFolder, bool allowSingleCell)
    {
        var res = new SplitResult();

        byte[] bytes = File.ReadAllBytes(path);
        var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!sheet.LoadImage(bytes))
        {
            Object.DestroyImmediate(sheet);
            res.errorTitle = "讀取失敗"; res.error = "這張 PNG 無法載入。";
            return res;
        }

        int sw = sheet.width, sh = sheet.height;

        if (sw % CellSize != 0 || sh % CellSize != 0)
        {
            Object.DestroyImmediate(sheet);
            res.errorTitle = "尺寸不符";
            res.error = $"圖片尺寸為 {sw}×{sh}，必須是 {CellSize} 的整數倍（例如 1280×1280 = 5×5）。\n" +
                        "請確認來源序列圖每格為 256×256、且無格間留白。";
            return res;
        }

        res.cols = sw / CellSize;
        res.rows = sh / CellSize;

        if (!allowSingleCell && res.cols * res.rows <= 1)
        {
            Object.DestroyImmediate(sheet);
            res.isSingleCellSkip = true;
            res.error = "剛好 256×256（已是單張幀），跳過。";
            return res;
        }

        string prefix = Path.GetFileNameWithoutExtension(path);
        Directory.CreateDirectory(outFolder);

        // 切格（row-major）。Unity 貼圖原點在左下，故第 r 列（由上往下）對應 y = sh - (r+1)*CellSize。
        var skippedCells = new StringBuilder();
        for (int r = 0; r < res.rows; r++)
        {
            for (int c = 0; c < res.cols; c++)
            {
                int x = c * CellSize;
                int y = sh - (r + 1) * CellSize;
                Color[] cell = sheet.GetPixels(x, y, CellSize, CellSize);

                if (IsFullyTransparent(cell))   // 全透明 → 跳過
                {
                    res.skipped++;
                    skippedCells.Append($"({r},{c}) ");
                    continue;
                }

                var frame = new Texture2D(CellSize, CellSize, TextureFormat.RGBA32, false);
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
