using System;
using UnityEditor;
using UnityEngine;

namespace Dipan.EditorTools
{
    /// <summary>
    /// UI 貼圖匯入自動化：凡是放進 <c>Resources/UI/</c> 底下的貼圖，
    /// 「第一次匯入」時自動套用「UI 銳利」預設——不壓縮、關 Mipmap、Sprite 類型、Max Size 夠大。
    ///
    /// 目的：解決「UI（背包/面板）在大螢幕看起來糊糊的」——根因是貼圖被 Compressed（BC/DXT）
    /// 加 Bilinear 放大後露出塊狀髒點。詳見 readme/PROBLEMS.md G2。
    ///
    /// 重要：只在 <see cref="TextureImporter.importSettingsMissing"/>（即尚無 .meta、剛丟進來的新圖）時套用，
    /// 之後你在 Inspector 手動微調（例如把某張像素風 UI 改 Point）不會被蓋掉。
    /// 所以此腳本不會動到既有已設定好的 UI 圖，只影響「未來新丟進 Resources/UI 的圖」。
    /// </summary>
    public class UITextureImportSettings : AssetPostprocessor
    {
        // 命中此路徑片段的貼圖才套 UI 預設（未來若有其他 UI 目錄，加進這個陣列即可）
        static readonly string[] UIFolders = { "/Resources/UI/" };

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');

            bool isUI = false;
            foreach (var f in UIFolders)
            {
                if (path.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) { isUI = true; break; }
            }
            if (!isUI) return;

            var ti = (TextureImporter)assetImporter;

            // 只在第一次匯入（尚無設定）時套預設，避免蓋掉之後的手動調整。
            if (!ti.importSettingsMissing) return;

            ti.textureType   = TextureImporterType.Sprite;
            ti.mipmapEnabled = false;                 // UI 是螢幕空間，開 mipmap 反而會挑到模糊的低階 mip
            ti.filterMode    = FilterMode.Bilinear;   // 平滑 UI 用；像素風 UI 之後可自行在 Inspector 改 Point
            ti.wrapMode      = TextureWrapMode.Clamp;
            if (ti.maxTextureSize < 2048) ti.maxTextureSize = 2048;

            // 去糊關鍵：預設平台改「不壓縮」。Standalone 未 override 會沿用此預設。
            var s = ti.GetDefaultPlatformTextureSettings();
            s.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SetPlatformTextureSettings(s);

            Debug.Log($"[UITextureImportSettings] 已套用 UI 銳利預設（不壓縮/關 Mipmap）：{assetPath}");
        }
    }
}
