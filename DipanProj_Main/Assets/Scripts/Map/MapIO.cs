using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

namespace Dipan.MapRuntime
{
    /// <summary>Newtonsoft 共用設定（與編輯器一致：略過 null、列舉轉字串）。</summary>
    public static class MapJsonConfig
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include,
        };

        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
    }

    /// <summary>.dipanmap 讀檔（runtime，主遊戲端不寫檔）。</summary>
    public static class MapSerializer
    {
        public const string Extension = ".dipanmap";

        public static MapData Load(string absolutePath)
        {
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException($"找不到地圖檔：{absolutePath}");
            var map = MapJsonConfig.Deserialize<MapData>(File.ReadAllText(absolutePath));
            if (map == null || map.format != "dipanmap")
                throw new InvalidDataException($"不是有效的 .dipanmap：{absolutePath}");
            map.NormalizeCutscenes();   // 舊檔的單一 cutscene 欄位搬進 cutscenes 清單
            return map;
        }
    }

    /// <summary>
    /// 載入 catalog.json，並把 catalog 的相對 path 解析成磁碟絕對路徑。
    /// 預設來源：StreamingAssets/MapAssets/（同步腳本產出，可打包）。
    /// </summary>
    public static class CatalogLoader
    {
        public const string SubDir = "MapAssets";
        public const string FileName = "catalog.json";

        public static string StreamingDir => Path.Combine(Application.streamingAssetsPath, SubDir);

        /// <summary>編輯器內的後備來源：直接讀 Assets/GameAssets（散檔仍在；打包後不存在）。</summary>
        public static string GameAssetsDir => Path.Combine(Application.dataPath, "GameAssets");

        /// <summary>
        /// 載入 catalog。先找 StreamingAssets/MapAssets/catalog.json；
        /// 找不到且在編輯器內時，後備掃描 GameAssets 即時生成一份 catalog（免同步測試用）。
        /// </summary>
        public static Catalog Load(out string assetRoot)
        {
            string streamingPath = Path.Combine(StreamingDir, FileName);
            if (File.Exists(streamingPath))
            {
                assetRoot = StreamingDir;
                var cat = MapJsonConfig.Deserialize<Catalog>(File.ReadAllText(streamingPath)) ?? new Catalog();
                Debug.Log($"[CatalogLoader] 載入 {cat.items.Count} 筆素材（StreamingAssets）");
                return cat;
            }

#if UNITY_EDITOR
            if (Directory.Exists(GameAssetsDir))
            {
                assetRoot = GameAssetsDir;
                var cat = BuildFromGameAssets(GameAssetsDir);
                Debug.LogWarning($"[CatalogLoader] 找不到 StreamingAssets/MapAssets/catalog.json，" +
                                 $"後備從 GameAssets 即時生成 {cat.items.Count} 筆（僅編輯器；打包前請執行 Tools/sync_map_assets.sh）");
                return cat;
            }
#endif
            assetRoot = StreamingDir;
            Debug.LogError("[CatalogLoader] 找不到 catalog（請先執行 Tools/sync_map_assets.sh）");
            return new Catalog();
        }

        /// <summary>取得某素材 PNG 的磁碟絕對路徑。</summary>
        public static string ResolveAssetPath(string assetRoot, CatalogItem item)
            => Path.Combine(assetRoot, item.path.Replace('/', Path.DirectorySeparatorChar));

#if UNITY_EDITOR
        // 分類白名單的單一來源在 MapAssetCategories（同 namespace，見該檔說明）。

        static Catalog BuildFromGameAssets(string root)
        {
            var cat = new Catalog();
            void Scan(string moduleName, string baseDir)
            {
                foreach (var c in MapAssetCategories.All)
                {
                    string cdir = Path.Combine(baseDir, c);
                    if (!Directory.Exists(cdir)) continue;

                    // 直接位於分類資料夾下的單張 PNG → 靜態素材。
                    // Talk 例外：允許每個 NPC 一個子資料夾（遞迴收，見 PROBLEMS.md C5）；
                    // 其餘類別維持只收第一層（Environment 子資料夾 = 動畫物件，不能混用）。
                    var opt = MapAssetCategories.IsRecursive(c) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    foreach (var png in Directory.GetFiles(cdir, "*.png", opt))
                    {
                        string rel = MakeRelative(root, png).Replace('\\', '/');
                        cat.items.Add(new CatalogItem
                        {
                            id = rel.Substring(0, rel.Length - 4),
                            path = rel,
                            category = c,
                            module = moduleName,
                            pixelSize = ReadPngWidth(png),
                            ppu = 256,
                        });
                    }

                    // Environment 子資料夾 = 一個動畫地上物（多幀收成一筆，依檔名排序＝播放順序）。
                    if (c == MapAssetCategories.Environment) ScanAnimated(cdir, moduleName);
                }

                // 逐格動畫素材（路線 B）：怪物 Monsters/SequenceImage/、玩家 Characters/SequenceImage/，
                // 每個動作葉資料夾（<名稱>/<state>/）收成一筆。
                ScanSequence(baseDir, "Monsters", moduleName);
                ScanSequence(baseDir, "Characters", moduleName);

                // 主角情緒立繪：Characters/Talk/<血統>/<情緒>.png，每張單圖收成一筆靜態素材（劇情頭像對話 Actor_<情緒> 用）。
                ScanCharacterTalk(baseDir, moduleName);
            }

            // Characters/Talk/<血統>/*.png：每張單圖 = 一筆靜態 catalog item，id = 相對路徑去副檔名
            // （例 Main/Characters/Talk/Base/angry）。供 DramaTalkDatabase 的 Actor_<情緒> 立繪解析。
            void ScanCharacterTalk(string baseDir, string moduleName)
            {
                string talkRoot = Path.Combine(baseDir, "Characters", "Talk");
                if (!Directory.Exists(talkRoot)) return;
                foreach (var png in Directory.GetFiles(talkRoot, "*.png", SearchOption.AllDirectories))
                {
                    string rel = MakeRelative(root, png).Replace('\\', '/');
                    cat.items.Add(new CatalogItem
                    {
                        id = rel.Substring(0, rel.Length - 4),
                        path = rel,
                        category = "Talk",
                        module = moduleName,
                        pixelSize = ReadPngWidth(png),
                        ppu = 256,
                    });
                }
            }

            // <categoryDir>/SequenceImage 下「直接含 PNG」的葉資料夾 = 一個動作（idle/walk/dead…）。
            void ScanSequence(string baseDir, string categoryDir, string moduleName)
            {
                string seqRoot = Path.Combine(baseDir, categoryDir, "SequenceImage");
                if (!Directory.Exists(seqRoot)) return;
                var dirs = new List<string>(Directory.GetDirectories(seqRoot, "*", SearchOption.AllDirectories));
                dirs.Sort(System.StringComparer.Ordinal);
                foreach (var d in dirs)
                {
                    var frameFiles = new List<string>(Directory.GetFiles(d, "*.png", SearchOption.TopDirectoryOnly));
                    if (frameFiles.Count == 0) continue;   // 非葉資料夾 → 跳過
                    frameFiles.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));

                    var framesRel = new List<string>(frameFiles.Count);
                    foreach (var fr in frameFiles) framesRel.Add(MakeRelative(root, fr).Replace('\\', '/'));

                    var item = new CatalogItem
                    {
                        id = MakeRelative(root, d).Replace('\\', '/'),
                        path = framesRel[0],
                        category = categoryDir,   // Monsters / Characters
                        module = moduleName,
                        pixelSize = ReadPngWidth(frameFiles[0]),
                        ppu = 256,
                    };
                    if (framesRel.Count >= 2) { item.frameCount = framesRel.Count; item.frames = framesRel; }
                    cat.items.Add(item);
                }
            }

            // Environment 底下每個子資料夾收成一筆動畫 catalog item（與同步工具一致；幀就地讀 GameAssets）。
            void ScanAnimated(string envDir, string moduleName)
            {
                var subDirs = new List<string>(Directory.GetDirectories(envDir));
                subDirs.Sort(System.StringComparer.Ordinal);
                foreach (var d in subDirs)
                {
                    var frameFiles = new List<string>(Directory.GetFiles(d, "*.png", SearchOption.TopDirectoryOnly));
                    frameFiles.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));
                    if (frameFiles.Count == 0) continue;

                    var framesRel = new List<string>(frameFiles.Count);
                    foreach (var fr in frameFiles) framesRel.Add(MakeRelative(root, fr).Replace('\\', '/'));

                    var item = new CatalogItem
                    {
                        id = MakeRelative(root, d).Replace('\\', '/'),
                        path = framesRel[0],
                        category = "Environment",
                        module = moduleName,
                        pixelSize = ReadPngWidth(frameFiles[0]),
                        ppu = 256,
                    };
                    if (framesRel.Count >= 2) { item.frameCount = framesRel.Count; item.frames = framesRel; }
                    cat.items.Add(item);
                }
            }

            string mainDir = Path.Combine(root, "Main");
            if (Directory.Exists(mainDir)) Scan("Main", mainDir);
            string modsDir = Path.Combine(root, "Modules");
            if (Directory.Exists(modsDir))
                foreach (var m in Directory.GetDirectories(modsDir))
                    Scan(Path.GetFileName(m), m);
            return cat;
        }

        static string MakeRelative(string root, string full)
            => full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, '/');

        static int ReadPngWidth(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                var b = new byte[24];
                if (fs.Read(b, 0, 24) < 24) return 0;
                return (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
            }
            catch { return 0; }
        }
#endif
    }
}
