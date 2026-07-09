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

    // 編輯器旗標登記表(相對 DipanProj_Main/Assets):編輯器把 flags.json 存在自己專案根目錄(Assets 上一層)。
    const string EditorFlagsRelative = "../../DipanProj_MapEditor/flags.json";

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

        // 0) 先從地圖編輯器把編好的地圖拉進來：Main → GameAssets/Main/Maps，其它 → GameAssets/Modules/<模組>/Maps。
        int pulled = PullMapsFromEditor(srcRoot);

        // 0.5) 旗標登記表 flags.json：從編輯器根目錄複製進 StreamingAssets/MapAssets。
        //      遊戲端 FlagRegistry 讀這份決定每個旗標的範圍(周目/永久/關卡單次)——沒帶過來就會把新旗標當周目、讀錯存檔。
        int flagsPulled = PullFlagsFromEditor(dstRoot);

        var catalog = new Catalog();
        int mapCount = 0;

        foreach (var (module, baseDir) in SourceDirs(srcRoot))
        {
            // 1) 素材
            foreach (var cat in Cats)
            {
                string cdir = Path.Combine(baseDir, cat);
                if (!Directory.Exists(cdir)) continue;

                // 直接位於該分類資料夾下的單張 PNG → 靜態素材。
                // Talk 例外：允許「每個 NPC 一個子資料夾」（例 Talk/Buddha/Buddha_normal.png），遞迴收所有 PNG、
                // 各成一筆靜態素材（見 PROBLEMS.md C5）；其餘類別維持只收第一層——Environment 的子資料夾
                // 另有「動畫物件」語意，不能混用。
                var searchOpt = cat == "Talk" ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (var png in Directory.GetFiles(cdir, "*.png", searchOpt))
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

            // 1.5) 角色逐格動畫素材（路線 B）：怪物 Monsters/SequenceImage/<怪名>/<state>/、
            //      玩家 Characters/SequenceImage/<血統>/<state>/，每個動作葉資料夾收成一筆。
            AddSequenceAnimations(baseDir, "Monsters", srcRoot, dstRoot, module, catalog);
            AddSequenceAnimations(baseDir, "Characters", srcRoot, dstRoot, module, catalog);

            // 1.6) 主角情緒立繪：Characters/Talk/<血統>/<情緒>.png，每張單圖收成一筆靜態素材（劇情頭像對話 Actor_<情緒> 用）。
            AddCharacterTalk(baseDir, srcRoot, dstRoot, module, catalog);

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
        Debug.Log($"✅ [SyncMapAssets] 從編輯器拉入 {pulled} 張地圖、{flagsPulled} 份旗標登記表;" +
                  $"推送 {catalog.items.Count} 筆素材、{mapCount} 張地圖 → StreamingAssets/MapAssets");
    }

    /// <summary>
    /// 從地圖編輯器根目錄把 flags.json 複製進遊戲端 StreamingAssets/MapAssets/flags.json。
    /// 遊戲端 <see cref="Dipan.MapRuntime.FlagRegistry"/> 讀這份決定每個旗標的範圍(周目/永久/關卡單次)。
    /// 找不到就略過並警告(不擋整個同步)。回傳複製了幾份(0 或 1)。
    /// </summary>
    static int PullFlagsFromEditor(string dstRoot)
    {
        string editorFlags = Path.GetFullPath(Path.Combine(Application.dataPath, EditorFlagsRelative));
        if (!File.Exists(editorFlags))
        {
            Debug.LogWarning($"[SyncMapAssets] 找不到編輯器旗標登記表,略過 flags.json(遊戲端旗標範圍不會更新):{editorFlags}");
            return 0;
        }
        CopyOverwrite(editorFlags, Path.Combine(dstRoot, "flags.json"));
        return 1;
    }

    /// <summary>
    /// 從地圖編輯器 Maps/&lt;模組&gt;/*.dipanmap 拉進遊戲端：
    /// 模組名 = "Main" → GameAssets/Main/Maps/（主/共用場景，如初始山洞、邪佛廣場）；
    /// 其它模組 → GameAssets/Modules/&lt;模組&gt;/Maps/。目的地必須已存在(否則略過並警告)。
    /// </summary>
    static int PullMapsFromEditor(string gameAssetsRoot)
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
            // Main 模組 → GameAssets/Main；其它 → GameAssets/Modules/<模組>
            string targetModule = module == "Main"
                ? Path.Combine(gameAssetsRoot, "Main")
                : Path.Combine(gameAssetsRoot, "Modules", module);
            if (!Directory.Exists(targetModule))
            {
                Debug.LogWarning($"[SyncMapAssets] 編輯器有「{module}」的地圖,但 GameAssets 沒有對應目錄（{targetModule}），略過。" +
                                 (module == "Main"
                                    ? "（請確認 GameAssets/Main 存在）"
                                    : $"（若是新關卡,請先在 GameAssets/Modules 建立「{module}」資料夾）"));
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

    /// <summary>
    /// 主角情緒立繪：Characters/Talk/&lt;血統&gt;/&lt;情緒&gt;.png（單張），複製進 StreamingAssets、各收成一筆靜態 catalog item
    /// （category=Talk、id=相對路徑去副檔名，例 Main/Characters/Talk/Base/angry）。
    /// 供 DramaTalkDatabase 的 Actor_&lt;情緒&gt; 立繪解析（依目前血統定位）。
    /// </summary>
    static void AddCharacterTalk(string baseDir, string srcRoot, string dstRoot, string module, Catalog catalog)
    {
        string talkRoot = Path.Combine(baseDir, "Characters", "Talk");
        if (!Directory.Exists(talkRoot)) return;

        var pngs = new List<string>(Directory.GetFiles(talkRoot, "*.png", SearchOption.AllDirectories));
        pngs.Sort(System.StringComparer.Ordinal);
        foreach (var png in pngs)
        {
            string rel = Rel(srcRoot, png);
            CopyOverwrite(png, Path.Combine(dstRoot, rel));
            catalog.items.Add(new CatalogItem
            {
                id = StripExt(rel),
                path = rel,
                category = "Talk",
                module = module,
                pixelSize = ReadPngWidth(png),
                ppu = PPU,
            });
        }
    }

    /// <summary>
    /// 逐格動畫素材（路線 B）：掃 &lt;categoryDir&gt;/SequenceImage/ 下「直接含 PNG」的葉資料夾
    /// （= 一個動作，如 &lt;名稱&gt;/idle、&lt;名稱&gt;/walk…），各收成一筆 catalog item
    /// （category = categoryDir、id = 資料夾相對路徑，≥2 幀帶 frameCount/frames，依檔名排序＝播放順序）。
    /// categoryDir = "Monsters"（怪物，MonsterSpriteLibrary 用）或 "Characters"（玩家，PlayerSpriteLibrary 用）。
    /// </summary>
    static void AddSequenceAnimations(string baseDir, string categoryDir, string srcRoot, string dstRoot, string module, Catalog catalog)
    {
        string seqRoot = Path.Combine(baseDir, categoryDir, "SequenceImage");
        if (!Directory.Exists(seqRoot)) return;

        var dirs = new List<string>(Directory.GetDirectories(seqRoot, "*", SearchOption.AllDirectories));
        dirs.Sort(System.StringComparer.Ordinal);
        foreach (var d in dirs)
        {
            var frameFiles = new List<string>(Directory.GetFiles(d, "*.png", SearchOption.TopDirectoryOnly));
            if (frameFiles.Count == 0) continue;   // 非葉資料夾（只含子資料夾）→ 跳過
            frameFiles.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));

            var framesRel = new List<string>(frameFiles.Count);
            foreach (var fr in frameFiles)
            {
                string frel = Rel(srcRoot, fr);
                CopyOverwrite(fr, Path.Combine(dstRoot, frel));
                framesRel.Add(frel);
            }

            var item = new CatalogItem
            {
                id = Rel(srcRoot, d),       // 例：Main/Monsters/SequenceImage/ZhaYu/walk、Main/Characters/SequenceImage/Base/walk
                path = framesRel[0],
                category = categoryDir,     // Monsters / Characters
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
