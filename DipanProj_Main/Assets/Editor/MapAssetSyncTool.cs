using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using Dipan.MapRuntime;

/// <summary>
/// 把地圖素材同步進 StreamingAssets,供 runtime MapLoader 載入(可打包)。
/// 等同 Tools/sync_map_assets.sh,但純 C#、跨平台、免終端機,跑完自動 Refresh。
///
/// 動作:從 Assets/GameAssets/{Main, Modules/*} 底下,只拿 Environment/ Tiles/ Background/ Drama/ Talk/
/// 的 PNG,依原相對路徑複製進 Assets/StreamingAssets/MapAssets/(覆蓋),生成 catalog.json,
/// 並複製各 module 的 Maps/*.dipanmap。id = 相對 GameAssets 的路徑去副檔名,與 .dipanmap 一致。
/// </summary>
public static class MapAssetSyncTool
{
    const int PPU = 256;
    static readonly string[] Cats = { "Environment", "Tiles", "Background", "Drama", "Talk" };

    // 編輯器 Maps 資料夾(相對 DipanProj_Main/Assets):結構為 Maps/<模組名>/*.dipanmap。
    const string EditorMapsRelative = "../../DipanProj_MapEditor/Maps";

    // priority 20:與 Build and Deploy(0)差 > 10 → 上方出現分隔線；檔案處理類功能由此往下延伸。
    [MenuItem("Project Tools/Sync Map Assets", false, 20)]
    public static void SyncMapAssets()
    {
        string srcRoot = Path.Combine(Application.dataPath, "GameAssets");
        string dstRoot = Path.Combine(Application.dataPath, "StreamingAssets", "MapAssets");

        if (!Directory.Exists(srcRoot))
        {
            Debug.LogError($"[SyncMapAssets] 找不到來源:{srcRoot}");
            return;
        }
        Directory.CreateDirectory(dstRoot);

        // 0) 先從地圖編輯器把編好的地圖拉進 GameAssets/Modules/<模組>/Maps/。
        int pulled = PullMapsFromEditor(Path.Combine(srcRoot, "Modules"));

        var catalog = new Catalog();
        int mapCount = 0;

        foreach (var (module, baseDir) in SourceDirs(srcRoot))
        {
            // 1) 素材
            foreach (var cat in Cats)
            {
                string cdir = Path.Combine(baseDir, cat);
                if (!Directory.Exists(cdir)) continue;

                // 直接位於該分類資料夾下的單張 PNG → 靜態素材（不遞迴）。
                foreach (var png in Directory.GetFiles(cdir, "*.png", SearchOption.TopDirectoryOnly))
                {
                    string rel = Rel(srcRoot, png);
                    CopyOverwrite(png, Path.Combine(dstRoot, rel));
                    catalog.items.Add(new CatalogItem
                    {
                        id = StripExt(rel),
                        path = rel,
                        category = cat,
                        module = module,
                        pixelSize = ReadPngWidth(png),
                        ppu = PPU,
                    });
                }

                // Environment 子資料夾 = 一個動畫地上物（多幀收成一筆，與編輯器同步邏輯一致）。
                if (cat == "Environment")
                    AddAnimatedObjects(cdir, srcRoot, dstRoot, module, catalog);
            }

            // 2) 地圖檔
            string mdir = Path.Combine(baseDir, "Maps");
            if (Directory.Exists(mdir))
                foreach (var map in Directory.GetFiles(mdir, "*.dipanmap"))
                {
                    CopyOverwrite(map, Path.Combine(dstRoot, Rel(srcRoot, map)));
                    mapCount++;
                }
        }

        File.WriteAllText(Path.Combine(dstRoot, "catalog.json"),
            JsonConvert.SerializeObject(catalog, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,   // 靜態物件的 frames=null 不寫出
            }));

        AssetDatabase.Refresh();
        Debug.Log($"✅ [SyncMapAssets] 從編輯器拉入 {pulled} 張地圖;" +
                  $"推送 {catalog.items.Count} 筆素材、{mapCount} 張地圖 → StreamingAssets/MapAssets");
    }

    /// <summary>
    /// 從地圖編輯器 Maps/&lt;模組&gt;/*.dipanmap 拉進遊戲端 GameAssets/Modules/&lt;模組&gt;/Maps/。
    /// 模組名 = 編輯器 Maps 下的子資料夾名,必須對應 GameAssets/Modules 內既有的模組(否則略過並警告)。
    /// </summary>
    static int PullMapsFromEditor(string gameModulesRoot)
    {
        string editorMaps = Path.GetFullPath(Path.Combine(Application.dataPath, EditorMapsRelative));
        if (!Directory.Exists(editorMaps))
        {
            Debug.LogWarning($"[SyncMapAssets] 找不到編輯器 Maps 資料夾,略過拉地圖:{editorMaps}");
            return 0;
        }

        int count = 0;
        foreach (var moduleDir in Directory.GetDirectories(editorMaps))
        {
            string module = Path.GetFileName(moduleDir);
            string targetModule = Path.Combine(gameModulesRoot, module);
            if (!Directory.Exists(targetModule))
            {
                Debug.LogWarning($"[SyncMapAssets] 編輯器有「{module}」的地圖,但 GameAssets/Modules 沒有對應模組,略過。" +
                                 $"(若是新關卡,請先在 GameAssets/Modules 建立「{module}」資料夾)");
                continue;
            }

            string targetMaps = Path.Combine(targetModule, "Maps");
            Directory.CreateDirectory(targetMaps);
            foreach (var map in Directory.GetFiles(moduleDir, "*.dipanmap", SearchOption.AllDirectories))
            {
                File.Copy(map, Path.Combine(targetMaps, Path.GetFileName(map)), true);
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Environment 底下的每個子資料夾 = 一個動畫地上物：複製各幀、收成「一筆」catalog item
    /// （category 仍是 Environment、id = 資料夾相對路徑，含 frameCount/frames，依檔名排序＝播放順序）。
    /// 只有一張的子資料夾退回靜態。與 DipanProj_MapEditor 的 sync_assets.sh / AssetSyncTool.cs 一致。
    /// </summary>
    static void AddAnimatedObjects(string envDir, string srcRoot, string dstRoot, string module, Catalog catalog)
    {
        var subDirs = new List<string>(Directory.GetDirectories(envDir));
        subDirs.Sort(System.StringComparer.Ordinal);
        foreach (var d in subDirs)
        {
            var frameFiles = new List<string>(Directory.GetFiles(d, "*.png", SearchOption.TopDirectoryOnly));
            frameFiles.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));
            if (frameFiles.Count == 0) continue;

            var framesRel = new List<string>(frameFiles.Count);
            foreach (var fr in frameFiles)
            {
                string frel = Rel(srcRoot, fr);
                CopyOverwrite(fr, Path.Combine(dstRoot, frel));
                framesRel.Add(frel);
            }

            string id = Rel(srcRoot, d);   // 資料夾相對路徑（無副檔名）
            var item = new CatalogItem
            {
                id = id,
                path = framesRel[0],        // 第一幀 = 預覽/whole sprite/碰撞框來源
                category = "Environment",
                module = module,
                pixelSize = ReadPngWidth(frameFiles[0]),
                ppu = PPU,
            };
            if (framesRel.Count >= 2)
            {
                item.frameCount = framesRel.Count;
                item.frames = framesRel;
            }
            catalog.items.Add(item);
        }
    }

    // Main(共用)+ Modules/<關卡>
    static IEnumerable<(string module, string dir)> SourceDirs(string root)
    {
        string main = Path.Combine(root, "Main");
        if (Directory.Exists(main)) yield return ("Main", main);
        string mods = Path.Combine(root, "Modules");
        if (Directory.Exists(mods))
        {
            var list = new List<string>(Directory.GetDirectories(mods));
            list.Sort(System.StringComparer.Ordinal);
            foreach (var d in list) yield return (Path.GetFileName(d), d);
        }
    }

    static void CopyOverwrite(string src, string dst)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dst));
        File.Copy(src, dst, true);
    }

    static string Rel(string root, string full)
        => full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, '/').Replace('\\', '/');

    static string StripExt(string rel)
    {
        int dot = rel.LastIndexOf('.');
        return dot < 0 ? rel : rel.Substring(0, dot);
    }

    static int ReadPngWidth(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var b = new byte[24];
            if (fs.Read(b, 0, 24) < 24) return 0;
            // PNG IHDR width 為 offset 16 的大端 uint
            return (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
        }
        catch { return 0; }
    }
}
