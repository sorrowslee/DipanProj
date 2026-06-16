#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DipanMapEditor.Data;
using DipanMapEditor.IO;

namespace DipanMapEditor.EditorTools
{
    /// <summary>
    /// 選單 DipanMapEditor → 同步素材。
    /// 用 C# 重做 sync_assets.sh 的事：從主專案每個來源（Main + 所有 Modules）底下
    /// 只拿 Environment / Tiles 的 PNG，無條件覆蓋進 StreamingAssets/MapAssets，
    /// 並生成帶 module 標記的 catalog.json。按一下即可，不需開終端機。
    /// </summary>
    public static class AssetSyncTool
    {
        const int Ppu = 256;

        [MenuItem("DipanMapEditor/同步素材（全部 module）", false, 20)]
        public static void SyncAll()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;       // DipanProj_MapEditor
            string repoRoot = Directory.GetParent(projectRoot).FullName;                   // DipanProj
            string mainAssets = Path.Combine(repoRoot, "DipanProj_Main", "Assets", "GameAssets");
            string target = Path.Combine(Application.streamingAssetsPath, CatalogLoader.SubDir);

            if (!Directory.Exists(Path.Combine(mainAssets, "Main")))
            {
                EditorUtility.DisplayDialog("同步素材", $"找不到主專案素材：\n{mainAssets}\n\n請確認 DipanProj_Main 與本專案在同一層。", "OK");
                return;
            }

            // 一律以主專案為準：清空目標再重建
            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.CreateDirectory(target);

            var catalog = new Catalog();
            CopySource(Path.Combine(mainAssets, "Main"), "Main", "Main", catalog, target);

            string modulesDir = Path.Combine(mainAssets, "Modules");
            var modules = new List<string>();
            if (Directory.Exists(modulesDir))
            {
                foreach (var dir in Directory.GetDirectories(modulesDir))
                {
                    string m = Path.GetFileName(dir);
                    modules.Add(m);
                    CopySource(dir, "Modules/" + m, m, catalog, target);
                }
            }

            File.WriteAllText(Path.Combine(target, CatalogLoader.FileName), JsonConfig.Serialize(catalog));
            AssetDatabase.Refresh();

            // 執行中的話順手刷新調色盤
            if (Application.isPlaying && Core.MapSession.Instance != null)
            {
                Core.MapSession.Instance.ReloadCatalog();
                Core.SpriteCache.Clear();
            }

            string list = modules.Count > 0 ? string.Join("、", modules) : "（無 module）";
            Debug.Log($"[AssetSync] 已同步 {catalog.items.Count} 張 PNG，module：{list}");
            EditorUtility.DisplayDialog("同步素材完成",
                $"已同步 {catalog.items.Count} 張 PNG。\nmodule：{list}\n\n進 Play 時新建地圖即可選 module。", "OK");
        }

        // 只拿來源底下的 Environment / Tiles / Background
        static void CopySource(string baseDir, string prefix, string module, Catalog catalog, string target)
        {
            CopyFolder(Path.Combine(baseDir, "Environment"), prefix + "/Environment", module, catalog, target);
            CopyFolder(Path.Combine(baseDir, "Tiles"), prefix + "/Tiles", module, catalog, target);
            CopyFolder(Path.Combine(baseDir, "Background"), prefix + "/Background", module, catalog, target);
        }

        static void CopyFolder(string src, string prefixRel, string module, Catalog catalog, string target)
        {
            if (!Directory.Exists(src)) return;
            foreach (var f in Directory.GetFiles(src, "*.png", SearchOption.AllDirectories))
            {
                string rel = f.Substring(src.Length).TrimStart('/', '\\').Replace('\\', '/');
                string destRel = prefixRel + "/" + rel;
                string dest = Path.Combine(target, destRel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(f, dest, true);

                catalog.items.Add(new CatalogItem
                {
                    id = destRel.Substring(0, destRel.Length - 4), // 去 ".png"
                    path = destRel,
                    category = Path.GetFileName(Path.GetDirectoryName(f)),
                    module = module,
                    pixelSize = ReadPngWidth(f),
                    ppu = Ppu,
                });
            }
        }

        static int ReadPngWidth(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                var b = new byte[24];
                if (fs.Read(b, 0, 24) < 24) return 0;
                return (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19]; // IHDR width, big-endian
            }
            catch { return 0; }
        }
    }
}
#endif
