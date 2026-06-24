using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Dipan.MapRuntime;   // 頭像走地圖素材管線（catalog + StreamingAssets），與劇情大圖同套

namespace Dipan.Drama
{
    /// <summary>
    /// 頭像對話表：載入 DramaTalkTable.csv，依「群組編號」分組、組內依流水號由小到大排序。
    /// 表來源 = 場景上 <see cref="DramaTalkTableProvider"/> 提供的 TextAsset（CSV 在 Assets/Data/DramaTalkTable.csv）。
    ///
    /// 懶漢快取：第一次存取 <see cref="Instance"/> 自動建立並載入。<see cref="GetGroup"/>(group) 取一串對話。
    /// CSV 解析與 DramaDatabase / ItemDatabase 同套（支援雙引號包覆、欄位內 \n 轉換行）。
    /// 頭像圖的「載入方式」留待對話介面 UI 決定，本資料層只保存路徑字串。
    /// </summary>
    public class DramaTalkDatabase
    {
        static DramaTalkDatabase _instance;
        public static DramaTalkDatabase Instance
        {
            get
            {
                if (_instance == null) { _instance = new DramaTalkDatabase(); _instance.Load(); }
                return _instance;
            }
        }

        // 群組編號 → 該群組的對話（已依流水號由小到大排序）
        readonly Dictionary<int, List<DramaTalkData>> _groups = new Dictionary<int, List<DramaTalkData>>();

        /// <summary>取某群組的整串對話（已排序）；該群組不存在回 null。</summary>
        public List<DramaTalkData> GetGroup(int group)
        {
            _groups.TryGetValue(group, out var list);
            return list;
        }

        void Load()
        {
            // 主要來源：場景上 DramaTalkTableProvider 提供的 TextAsset。
            var provider = Object.FindObjectOfType<DramaTalkTableProvider>();
            if (provider != null && provider.talkCSV != null) { LoadFromText(provider.talkCSV.text); return; }

            // 後備：Resources（舊位置）。
            var csv = Resources.Load<TextAsset>("Data/DramaTalkTable");
            if (csv == null)
            {
                Debug.LogError("[DramaTalkDatabase] 找不到 DramaTalkTable。請把 Assets/Data/DramaTalkTable.csv 拖進場景上 " +
                               "DramaTalkTableProvider 元件的 Talk CSV 欄（見 readme/DRAMA.md）。");
                return;
            }
            LoadFromText(csv.text);
        }

        void LoadFromText(string text)
        {
            _groups.Clear();

            var all = new List<DramaTalkData>();
            string[] lines = (text ?? "").Split('\n');
            for (int i = 1; i < lines.Length; i++)   // 第 0 行是表頭
            {
                string line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] v = ParseCsvLine(line);
                if (!int.TryParse(Field(v, 0), out int id)) continue;
                int.TryParse(Field(v, 1), out int group);

                var d = new DramaTalkData
                {
                    Id = id,
                    Group = group,
                    Name = Field(v, 2),
                    AvatarPath = Field(v, 3),
                    Side = (Field(v, 4) == "2") ? 2 : 1,   // 2 = 置右；其餘（含留空）= 置左
                    Text = Unescape(Field(v, 5)),
                };
                all.Add(d);
            }

            // 先依「群組、再流水號」排序，再分組——確保每組內由小到大、播放順序正確。
            all.Sort((a, b) => a.Group != b.Group ? a.Group.CompareTo(b.Group) : a.Id.CompareTo(b.Id));
            foreach (var d in all)
            {
                if (!_groups.TryGetValue(d.Group, out var list))
                {
                    list = new List<DramaTalkData>();
                    _groups[d.Group] = list;
                }
                list.Add(d);
            }

            ResolveAvatars();
            Debug.Log($"[DramaTalkDatabase] 載入 {all.Count} 句對話、{_groups.Count} 個群組。");
        }

        /// <summary>
        /// 頭像走「地圖素材管線」（每關專屬、與共用 Resources 分開），與劇情大圖一致：
        /// 圖放 GameAssets/Modules/&lt;module&gt;/Talk/，由 Sync Map Assets 收進 catalog ＋ StreamingAssets。
        /// AvatarPath = catalog id（相對 GameAssets 的路徑、不含副檔名，例 Modules/RedBridalGown/Talk/redBridalGown）。
        /// 同一張頭像被多句共用時只載一次（path 快取）。
        /// </summary>
        void ResolveAvatars()
        {
            bool any = false;
            foreach (var list in _groups.Values)
                foreach (var d in list)
                    if (!string.IsNullOrEmpty(d.AvatarPath)) { any = true; break; }
            if (!any) return;

            var catalog = CatalogLoader.Load(out string assetRoot);
            var loader = new MapSpriteLoader(assetRoot);
            var cache = new Dictionary<string, Sprite>();

            foreach (var list in _groups.Values)
            {
                foreach (var d in list)
                {
                    if (string.IsNullOrEmpty(d.AvatarPath)) continue;
                    if (!cache.TryGetValue(d.AvatarPath, out var sp))
                    {
                        var item = catalog.Find(d.AvatarPath);
                        sp = item != null ? loader.GetWholeSprite(item, 1f) : null;
                        cache[d.AvatarPath] = sp;
                        if (sp == null)
                            Debug.LogWarning($"[DramaTalkDatabase] 找不到頭像（catalog id：{d.AvatarPath}）。" +
                                "確認圖放在 GameAssets/Modules/<module>/Talk/ 下，且已執行 Project Tools → Sync Map Assets。");
                    }
                    d.Avatar = sp;
                }
            }
        }

        static string Field(string[] v, int i) => (i < v.Length && v[i] != null) ? v[i].Trim() : "";
        static string Unescape(string s) => string.IsNullOrEmpty(s) ? s : s.Replace("\\n", "\n");

        static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(ch);
                }
                else
                {
                    if (ch == '"') inQuotes = true;
                    else if (ch == ',') { result.Add(sb.ToString()); sb.Clear(); }
                    else sb.Append(ch);
                }
            }
            result.Add(sb.ToString());
            return result.ToArray();
        }
    }
}
