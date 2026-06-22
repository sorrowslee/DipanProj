using System.IO;
using UnityEngine;
using DipanMapEditor.Data;

namespace DipanMapEditor.IO
{
    /// <summary>
    /// 載入由 sync_assets.sh 生成的 catalog.json。
    /// 路徑慣例：StreamingAssets/MapAssets/catalog.json。
    /// </summary>
    public static class CatalogLoader
    {
        public const string SubDir = "MapAssets";
        public const string FileName = "catalog.json";

        public static string DefaultDir => Path.Combine(Application.streamingAssetsPath, SubDir);
        public static string DefaultPath => Path.Combine(DefaultDir, FileName);

        public static Catalog Load(string path = null)
        {
            path ??= DefaultPath;
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CatalogLoader] 找不到 catalog：{path}（請先執行 sync_assets.sh）");
                return new Catalog();
            }
            string json = File.ReadAllText(path);
            var catalog = JsonConfig.Deserialize<Catalog>(json) ?? new Catalog();
            Debug.Log($"[CatalogLoader] 載入 {catalog.items.Count} 筆素材：{path}");
            return catalog;
        }

        /// <summary>取得某素材 PNG 在磁碟上的絕對路徑（StreamingAssets/MapAssets/ + item.path）。</summary>
        public static string ResolveAssetPath(CatalogItem item)
            => Path.Combine(DefaultDir, item.path);

        /// <summary>取得 catalog 內某相對路徑（如動畫物件的某一幀 frames[i]）在磁碟上的絕對路徑。</summary>
        public static string ResolveRelative(string relPath)
            => Path.Combine(DefaultDir, relPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
