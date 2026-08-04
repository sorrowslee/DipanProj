using System;
using UnityEditor;
using UnityEngine;

namespace Dipan.EditorTools
{
    /// <summary>
    /// **UI 貼圖匯入設定的唯一規則來源。**
    ///
    /// 兩個地方共用這一份，避免「新圖自動套的規則」與「工具檢查用的規則」各說各話：
    ///   ‧ <see cref="UITextureImportSettings"/>：新圖第一次匯入時自動套
    ///   ‧ <see cref="UIAssetAudit"/>：選單工具，可套用到**既有**檔案（既有檔案的 .meta 已存在，
    ///     postprocessor 刻意不會動它們，所以要靠工具）
    ///
    /// 【為什麼要管 Max Size】
    /// UI 貼圖一律「不壓縮」（避免 BC 壓縮在大螢幕露出塊狀髒點，見 readme/PROBLEMS.md G2），
    /// 所以尺寸直接等於記憶體：一張 500×500 未壓縮 RGBA ≈ 1MB、256×256 ≈ 256KB。
    /// 而 icon 在畫面上最多只顯示 120px（裝備欄），原圖留 500px 是純浪費。
    /// 規範見 readme/PERF_QUALITY_AUDIT.md「素材尺寸規範」。
    ///
    /// 【要調就改下面這一區】
    /// </summary>
    public static class UIAssetRules
    {
        // ══════════════════════════ 調整區 ══════════════════════════

        /// <summary>套用這套規則的路徑（新增其他 UI 目錄就加進來）。</summary>
        public static readonly string[] Folders = { "/Resources/UI/", "/Resources/Loading/" };

        /// <summary>道具 icon 目錄——顯示尺寸最大 120px（裝備欄），4K 下 UI 放大 2× ＝ 240px。</summary>
        const string IconFolder = "/Resources/UI/Icons/";
        const int IconMaxSize = 256;

        /// <summary>
        /// 「原圖長邊 ≥ 這個值」就視為滿版／大型底圖（面板背景、載入圖），本來就該大，不縮。
        /// 例：inventoryPanelBG 1126×1397、ForgingPanel_Bg 1536×1024、SelectSavePanel_Bg 1672×941。
        /// </summary>
        const int LargeArtLongEdge = 1000;
        const int LargeArtMaxSize = 2048;

        /// <summary>其餘 UI 素材（面板框、按鈕、提示圖…）：顯示通常在 200~450px 之間。</summary>
        const int DefaultMaxSize = 512;

        /// <summary>原圖長邊超過建議值這麼多倍時，額外提醒「這張圖畫太大了，考慮重出圖」。</summary>
        const float OversizeWarnRatio = 3f;

        // ══════════════════════════ 規則本體 ══════════════════════════

        /// <summary>這個資產路徑要不要套 UI 規則。</summary>
        public static bool IsUIAsset(string assetPath)
        {
            string p = (assetPath ?? "").Replace('\\', '/');
            foreach (var f in Folders)
                if (p.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>
        /// 這張圖建議的 Max Size。
        /// longEdge = 原圖長邊像素（&lt;= 0 表示還讀不到，例如 postprocessor 階段沒有尺寸可查）。
        /// </summary>
        public static int RecommendedMaxSize(string assetPath, int longEdge)
        {
            string p = (assetPath ?? "").Replace('\\', '/');

            if (p.IndexOf(IconFolder, StringComparison.OrdinalIgnoreCase) >= 0) return IconMaxSize;
            if (longEdge >= LargeArtLongEdge) return LargeArtMaxSize;
            if (longEdge <= 0) return LargeArtMaxSize;   // 尺寸未知時保守不縮，交給工具事後修正
            return DefaultMaxSize;
        }

        /// <summary>用途說明（報告用）。</summary>
        public static string CategoryOf(string assetPath, int longEdge)
        {
            string p = (assetPath ?? "").Replace('\\', '/');
            if (p.IndexOf(IconFolder, StringComparison.OrdinalIgnoreCase) >= 0) return "道具 icon";
            if (longEdge >= LargeArtLongEdge) return "滿版/大型底圖";
            return "面板素材";
        }

        /// <summary>原圖是不是畫得比需要的還大很多（建議重出圖，而不是靠 Max Size 硬縮）。</summary>
        public static bool IsOversizeSource(int longEdge, int recommended)
            => longEdge > 0 && recommended > 0 && longEdge > recommended * OversizeWarnRatio;

        /// <summary>
        /// 把「UI 銳利」的共同設定套上去（不含 Max Size，因為那要看原圖尺寸）。
        /// 見 readme/PROBLEMS.md G2：UI 糊掉的根因是被 BC 壓縮 + Bilinear 放大。
        /// </summary>
        public static void ApplySharedSettings(TextureImporter ti)
        {
            ti.textureType   = TextureImporterType.Sprite;
            ti.mipmapEnabled = false;                 // UI 是螢幕空間，開 mipmap 反而會挑到模糊的低階 mip
            ti.filterMode    = FilterMode.Bilinear;   // 平滑 UI 用；像素風 UI 可自行在 Inspector 改 Point
            ti.wrapMode      = TextureWrapMode.Clamp;

            var s = ti.GetDefaultPlatformTextureSettings();
            s.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SetPlatformTextureSettings(s);
        }

        /// <summary>估算未壓縮 RGBA 的記憶體（bytes）。報告用。</summary>
        public static long EstimateBytes(int longEdge, int maxSize)
        {
            int side = Mathf.Min(longEdge > 0 ? longEdge : maxSize, maxSize);
            return (long)side * side * 4;
        }

        public static string Human(long bytes)
            => bytes >= 1024 * 1024 ? $"{bytes / 1024f / 1024f:0.##} MB" : $"{bytes / 1024f:0.#} KB";
    }
}
