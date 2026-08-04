using UnityEditor;
using UnityEngine;

namespace Dipan.EditorTools
{
    /// <summary>
    /// UI 貼圖匯入自動化：凡是放進 <c>Resources/UI/</c>（與 <c>Resources/Loading/</c>）底下的貼圖，
    /// 「第一次匯入」時自動套用「UI 銳利」預設——不壓縮、關 Mipmap、Sprite 類型、依用途設 Max Size。
    ///
    /// 目的：解決「UI（背包/面板）在大螢幕看起來糊糊的」——根因是貼圖被 Compressed（BC/DXT）
    /// 加 Bilinear 放大後露出塊狀髒點。詳見 readme/PROBLEMS.md G2。
    ///
    /// **規則本身在 <see cref="UIAssetRules"/>，這裡只負責「新圖進來時自動套」。**
    /// 既有檔案（.meta 已存在）刻意不動——要批次修正請用選單
    /// <c>Project Tools → 素材 → UI 貼圖設定：檢查／套用建議值</c>（<see cref="UIAssetAudit"/>）。
    ///
    /// 重要：只在 <see cref="TextureImporter.importSettingsMissing"/>（即尚無 .meta、剛丟進來的新圖）時套用，
    /// 之後你在 Inspector 手動微調（例如把某張像素風 UI 改 Point）不會被蓋掉。
    /// </summary>
    public class UITextureImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!UIAssetRules.IsUIAsset(assetPath)) return;

            var ti = (TextureImporter)assetImporter;

            // 只在第一次匯入（尚無設定）時套預設，避免蓋掉之後的手動調整。
            if (!ti.importSettingsMissing) return;

            UIAssetRules.ApplySharedSettings(ti);

            // 這個階段還讀不到原圖尺寸（貼圖尚未產生），所以只能依路徑判斷：
            //   icon 目錄 → 直接給 icon 的上限；其他 → 先保守不縮，之後跑一次選單工具修正。
            ti.maxTextureSize = UIAssetRules.RecommendedMaxSize(assetPath, 0);

            Debug.Log($"[UITextureImportSettings] 已套用 UI 銳利預設（不壓縮/關 Mipmap，Max Size {ti.maxTextureSize}）：{assetPath}\n" +
                      $"　新圖建議跑一次 Project Tools → 素材 → UI 貼圖設定：檢查，確認尺寸是否需要再縮。");
        }
    }
}
