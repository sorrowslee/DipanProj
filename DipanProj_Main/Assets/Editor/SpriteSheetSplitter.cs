using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 5×5（或任意 N×M）序列圖切割工具。
///
/// 用途：AutoSprite 之類的工具會把一個動作輸出成「一張大序列圖」（每格 256×256、整張是 256 的整數倍），
/// 但本專案的 route B 程式逐格動畫吃的是「資料夾裡的單張幀」。這支工具讓你：
///   Project Tools → Split Sprite Sheet → 選那張序列圖 → 自動切成單張幀、寫回同資料夾、刪掉原圖。
///
/// 行為（依與使用者確認過的規格）：
///   1. 每格固定 256×256，格數 = 圖寬÷256 × 圖高÷256（自動推算；非 256 整數倍則中止並提示）。
///   2. 順序 row-major（左→右、上→下）。
///   3. 命名 = 來源檔名前綴 + 兩位數、從 _01 起（例：walk.png → walk_01.png、walk_02.png…），與 CHARACTER_SETUP 一致。
///   4. 全透明的空格自動跳過、不輸出（序號在保留的幀上連續編，不留洞）。
///   5. 不翻轉、不縮放：維持原朝向（朝右）與原生 256 解析度。朝向交給遊戲端 flipX 處理。
///   6. 切割成功後刪掉原始序列圖（連同 .meta）。
///   7. 不清理資料夾裡既有的舊幀（同名覆寫、不同名保留）。
///
/// 純 Editor + 純 C#（Texture2D 切格 → EncodeToPNG），無外部依賴。
/// </summary>
public static class SpriteSheetSplitter
{
    private const int CellSize = 256;

    [MenuItem("Project Tools/Split Sprite Sheet", false, 40)]
    public static void SplitSelectedSheet()
    {
        // 1) 選檔（預設從 Assets 開始）
        string startDir = Application.dataPath;
        string path = EditorUtility.OpenFilePanel("選擇要切割的序列圖（每格 256×256）", startDir, "png");
        if (string.IsNullOrEmpty(path)) return;   // 取消

        // 2) 必須在本專案 Assets 底下（切出來的幀要寫回專案、原圖也要用 AssetDatabase 刪）
        string dataPath = Application.dataPath.Replace('\\', '/');
        string normPath = path.Replace('\\', '/');
        if (!normPath.StartsWith(dataPath))
        {
            EditorUtility.DisplayDialog("無法處理",
                "請選擇位於本專案 Assets 資料夾內的序列圖。", "OK");
            return;
        }

        // 3) 讀圖
        byte[] bytes = File.ReadAllBytes(path);
        var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!sheet.LoadImage(bytes))
        {
            Object.DestroyImmediate(sheet);
            EditorUtility.DisplayDialog("讀取失敗", "這張 PNG 無法載入。", "OK");
            return;
        }

        int sw = sheet.width, sh = sheet.height;

        // 4) 驗證是 256 的整數倍
        if (sw % CellSize != 0 || sh % CellSize != 0)
        {
            Object.DestroyImmediate(sheet);
            EditorUtility.DisplayDialog("尺寸不符",
                $"圖片尺寸為 {sw}×{sh}，必須是 {CellSize} 的整數倍（例如 1280×1280 = 5×5）。\n" +
                "請確認來源序列圖每格為 256×256、且無格間留白。", "OK");
            return;
        }

        int cols = sw / CellSize;
        int rows = sh / CellSize;

        string folder = Path.GetDirectoryName(path);
        string prefix = Path.GetFileNameWithoutExtension(path);

        // 5) 切格（row-major）。Unity 貼圖原點在左下，故第 r 列（由上往下）對應 y = sh - (r+1)*CellSize。
        int written = 0;
        int skipped = 0;
        var skippedCells = new StringBuilder();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int x = c * CellSize;
                int y = sh - (r + 1) * CellSize;
                Color[] cell = sheet.GetPixels(x, y, CellSize, CellSize);

                // 全透明 → 跳過
                if (IsFullyTransparent(cell))
                {
                    skipped++;
                    skippedCells.Append($"({r},{c}) ");
                    continue;
                }

                var frame = new Texture2D(CellSize, CellSize, TextureFormat.RGBA32, false);
                frame.SetPixels(cell);
                frame.Apply();

                written++;
                string outName = $"{prefix}_{written:D2}.png";   // 兩位數、從 _01 起、連續編號
                string outPath = Path.Combine(folder, outName);
                File.WriteAllBytes(outPath, frame.EncodeToPNG());
                Object.DestroyImmediate(frame);
            }
        }

        Object.DestroyImmediate(sheet);

        // 6) 刪掉原始序列圖（用 AssetDatabase 才會一併清掉 .meta）
        string assetRelPath = "Assets" + normPath.Substring(dataPath.Length);
        AssetDatabase.DeleteAsset(assetRelPath);

        // 7) 讓 Unity 看到新檔
        AssetDatabase.Refresh();

        string msg = $"切割完成：寫出 {written} 張幀（{prefix}_01 ~ {prefix}_{written:D2}）。\n" +
                     $"格數 {cols}×{rows}，跳過 {skipped} 個空格。\n" +
                     $"已刪除原始序列圖。";
        Debug.Log($"[SpriteSheetSplitter] {msg}" +
                  (skipped > 0 ? $"\n跳過的格子(列,欄)：{skippedCells}" : ""));
        EditorUtility.DisplayDialog("Split Sprite Sheet", msg, "OK");
    }

    /// <summary>整格 alpha 皆為 0 視為空格。</summary>
    private static bool IsFullyTransparent(Color[] pixels)
    {
        for (int i = 0; i < pixels.Length; i++)
            if (pixels[i].a > 0f) return false;
        return true;
    }
}
