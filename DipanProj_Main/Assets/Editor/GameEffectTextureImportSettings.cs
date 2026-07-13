using System;
using UnityEditor;
using UnityEngine;

namespace Dipan.EditorTools
{
    /// <summary>
    /// 新匯入的武器序列圖與 VFX 統一套用 Sprite／Point／無壓縮設定。
    /// 只處理尚未有 .meta 的圖片，不覆蓋作者之後在 Inspector 做的個別調整。
    /// </summary>
    public sealed class GameEffectTextureImportSettings : AssetPostprocessor
    {
        static readonly string[] TargetFolders =
        {
            "/Resources/Weapon/",
            "/Resources/VfxEffects/",
            "/Resources/GroundEffect/"
        };

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            bool matched = false;
            foreach (string folder in TargetFolders)
            {
                if (path.IndexOf(folder, StringComparison.OrdinalIgnoreCase) < 0) continue;
                matched = true;
                break;
            }

            if (!matched) return;

            var importer = (TextureImporter)assetImporter;
            if (!importer.importSettingsMissing) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            if (importer.maxTextureSize < 2048) importer.maxTextureSize = 2048;

            TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
