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
    /// 只拿 Environment / Tiles / Background 的 PNG，無條件覆蓋進 StreamingAssets/MapAssets，
    /// 並生成帶 module 標記的 catalog.json。按一下即可，不需開終端機。
    ///
    /// 動畫地上物：Environment/ 底下的「子資料夾」= 一個動畫物件，多幀收成一筆 catalog item
    /// （category 仍是 Environment、id = 資料夾相對路徑，含 frameCount / frames，依檔名排序＝播放順序）。
    /// 與 Tools/sync_assets.sh 行為一致。
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

            // 順手把遊戲的底部操控列 HUD 圖搬進編輯器（供「顯示底部ui」參考層用；無條件覆蓋，以防日後換 UI）。
            bool bottomUiOk = CopyBottomUi(repoRoot);

            AssetDatabase.Refresh();

            // 執行中的話順手刷新調色盤
            if (Application.isPlaying && Core.MapSession.Instance != null)
            {
                Core.MapSession.Instance.ReloadCatalog();
                Core.SpriteCache.Clear();
            }

            string list = modules.Count > 0 ? string.Join("、", modules) : "（無 module）";
            Debug.Log($"[AssetSync] 已同步 {catalog.items.Count} 張 PNG，module：{list}");
            string bottomUiLine = bottomUiOk ? "\n底部 UI 圖：已更新" : "\n底部 UI 圖：⚠ 找不到來源，未更新";
            EditorUtility.DisplayDialog("同步素材完成",
                $"已同步 {catalog.items.Count} 張 PNG。\nmodule：{list}{bottomUiLine}\n\n進 Play 時新建地圖即可選 module。", "OK");
        }

        // 底部操控列 HUD 圖：遊戲端在 Resources/UI/BottomControlPanel/，同步到編輯器 StreamingAssets/EditorUI/。
        // 對應 Core.BottomUiOverlay.SubDir / FileName（保持一致）。
        const string BottomUiSubDir = "EditorUI";
        const string BottomUiFileName = "BottomControlPanel_Bg.png";

        /// <summary>把遊戲底部 UI 框圖無條件覆蓋進編輯器 StreamingAssets/EditorUI/。找不到來源回 false。</summary>
        static bool CopyBottomUi(string repoRoot)
        {
            string src = Path.Combine(repoRoot, "DipanProj_Main", "Assets", "Resources", "UI",
                                      "BottomControlPanel", BottomUiFileName);
            if (!File.Exists(src))
            {
                Debug.LogWarning($"[AssetSync] 找不到底部 UI 圖來源：{src}（未更新，「顯示底部ui」會沿用舊圖或不可用）");
                return false;
            }
            string destDir = Path.Combine(Application.streamingAssetsPath, BottomUiSubDir);
            Directory.CreateDirectory(destDir);
            File.Copy(src, Path.Combine(destDir, BottomUiFileName), true);   // 無條件覆蓋
            Debug.Log("[AssetSync] 已更新底部 UI 圖 → StreamingAssets/EditorUI/" + BottomUiFileName);
            return true;
        }

        // 只拿來源底下的 Environment / Tiles / Background
        static void CopySource(string baseDir, string prefix, string module, Catalog catalog, string target)
        {
            CopyEnvironment(Path.Combine(baseDir, "Environment"), prefix + "/Environment", module, catalog, target);
            CopyFlat(Path.Combine(baseDir, "Tiles"), prefix + "/Tiles", module, "Tiles", catalog, target);
            CopyFlat(Path.Combine(baseDir, "Background"), prefix + "/Background", module, "Background", catalog, target);
        }

        /// <summary>拷貝「直接位於 src 底下」的 PNG（不遞迴），每張登記成靜態素材。</summary>
        static void CopyFlat(string src, string prefixRel, string module, string category, Catalog catalog, string target)
        {
            if (!Directory.Exists(src)) return;
            foreach (var f in Directory.GetFiles(src, "*.png", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(f);
                string destRel = prefixRel + "/" + fileName;
                CopyFile(f, destRel, target);
                catalog.items.Add(new CatalogItem
                {
                    id = destRel.Substring(0, destRel.Length - 4), // 去 ".png"
                    path = destRel,
                    category = category,
                    module = module,
                    pixelSize = ReadPngWidth(f),
                    ppu = Ppu,
                });
            }
        }

        /// <summary>
        /// Environment：直接擺的單張 = 靜態物件；每個子資料夾 = 一個動畫地上物（多幀收成一筆，
        /// 依檔名排序＝播放順序）。與 Tools/sync_assets.sh 行為一致。
        /// </summary>
        static void CopyEnvironment(string env, string prefixRel, string module, Catalog catalog, string target)
        {
            if (!Directory.Exists(env)) return;

            // 1) 直接位於 Environment/ 的單張 PNG → 靜態物件。
            CopyFlat(env, prefixRel, module, "Environment", catalog, target);

            // 2) Environment/ 底下的每個子資料夾 → 一個動畫物件。
            var subDirs = new List<string>(Directory.GetDirectories(env));
            subDirs.Sort(System.StringComparer.Ordinal);
            foreach (var d in subDirs)
            {
                string name = Path.GetFileName(d);

                var frameFiles = new List<string>(Directory.GetFiles(d, "*.png", SearchOption.TopDirectoryOnly));
                // 依檔名（非完整路徑）排序＝播放順序，建議補零命名（_01.._NN）。
                frameFiles.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));
                if (frameFiles.Count == 0) continue;

                var framesRel = new List<string>(frameFiles.Count);
                foreach (var fr in frameFiles)
                {
                    string frel = prefixRel + "/" + name + "/" + Path.GetFileName(fr);
                    CopyFile(fr, frel, target);
                    framesRel.Add(frel);
                }

                if (framesRel.Count < 2)
                {
                    // 只有一張 → 當靜態（避免「單張資料夾」變成意義不大的動畫）。
                    catalog.items.Add(new CatalogItem
                    {
                        id = prefixRel + "/" + name,
                        path = framesRel[0],
                        category = "Environment",
                        module = module,
                        pixelSize = ReadPngWidth(frameFiles[0]),
                        ppu = Ppu,
                    });
                    continue;
                }

                catalog.items.Add(new CatalogItem
                {
                    id = prefixRel + "/" + name,        // 動畫物件 id = 資料夾相對路徑
                    path = framesRel[0],                 // 第一幀 = 預覽/whole sprite/碰撞框來源
                    category = "Environment",
                    module = module,
                    pixelSize = ReadPngWidth(frameFiles[0]),
                    ppu = Ppu,
                    frameCount = framesRel.Count,
                    frames = framesRel,
                });
            }
        }

        static void CopyFile(string src, string destRel, string target)
        {
            string dest = Path.Combine(target, destRel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(src, dest, true);
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
