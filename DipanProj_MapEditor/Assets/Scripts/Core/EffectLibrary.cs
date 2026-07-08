using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DipanMapEditor.Core
{
    /// <summary>整理好的特效庫：掃描 StreamingAssets/Effects（由 Tools/organize_effects.py 產生），
    /// 提供「類別 → 效果 → 顏色 → 幀」的資料模型與幀貼圖快取。
    /// 讀圖走 File IO（同 SpriteCache），不需要 Unity import，所以放 StreamingAssets 即可。</summary>
    public static class EffectLibrary
    {
        /// <summary>一個效果的某個顏色（一串幀）。</summary>
        public class ColorSet
        {
            public string name;                 // blue / red / ...
            public List<string> frames = new List<string>(); // 各幀的絕對路徑，依檔名排序＝播放順序
        }

        /// <summary>一個效果（含多種顏色）。</summary>
        public class Entry
        {
            public string category;             // Explosions / Impacts / ...
            public string name;                 // epic_explosion_001 / ...
            public List<ColorSet> colors = new List<ColorSet>();
        }

        public static string Root => Path.Combine(Application.streamingAssetsPath, "Effects");

        static List<Entry> _entries;
        static readonly Dictionary<string, Texture2D> _texCache = new Dictionary<string, Texture2D>();

        /// <summary>所有效果（首次存取時掃描一次）。</summary>
        public static IReadOnlyList<Entry> Entries
        {
            get { if (_entries == null) Scan(); return _entries; }
        }

        /// <summary>重新掃描資料夾並清空貼圖快取（換了整理內容後呼叫）。</summary>
        public static void Refresh() { _entries = null; _texCache.Clear(); }

        static void Scan()
        {
            _entries = new List<Entry>();
            if (!Directory.Exists(Root)) return;

            foreach (var catDir in SortedDirs(Root))
            {
                string category = Path.GetFileName(catDir);
                foreach (var effDir in SortedDirs(catDir))
                {
                    var entry = new Entry { category = category, name = Path.GetFileName(effDir) };
                    foreach (var colorDir in SortedDirs(effDir))
                    {
                        var pngs = new List<string>(Directory.GetFiles(colorDir, "*.png"));
                        pngs.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));
                        if (pngs.Count == 0) continue;
                        entry.colors.Add(new ColorSet { name = Path.GetFileName(colorDir), frames = pngs });
                    }
                    if (entry.colors.Count > 0) _entries.Add(entry);
                }
            }
        }

        static IEnumerable<string> SortedDirs(string dir)
        {
            var list = new List<string>(Directory.GetDirectories(dir));
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }

        /// <summary>載入（並快取）某幀 PNG 的整張貼圖（Point 濾鏡，像素風）。失敗回 null。</summary>
        public static Texture2D GetFrame(string absPath)
        {
            if (string.IsNullOrEmpty(absPath)) return null;
            if (_texCache.TryGetValue(absPath, out var tex) && tex != null) return tex;
            if (!File.Exists(absPath)) return null;

            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(absPath));   // 自動調整尺寸
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            _texCache[absPath] = tex;
            return tex;
        }
    }
}
