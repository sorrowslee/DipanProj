using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Dipan.MapRuntime;   // 立繪走地圖素材管線（catalog + StreamingAssets），與劇情大圖同套

namespace Dipan.Drama
{
    /// <summary>
    /// 頭像對話表：載入 DramaTalkTable.csv，依「群組編號」分組、組內依流水號由小到大排序。
    /// 表來源 = 場景上 <see cref="DramaTalkTableProvider"/> 提供的 TextAsset（CSV 在 Assets/Data/DramaTalkTable.csv）。
    ///
    /// 懶漢快取：第一次存取 <see cref="Instance"/> 自動建立並載入。<see cref="GetGroup"/>(group) 取一串對話。
    /// CSV 解析與 DramaDatabase / ItemDatabase 同套（支援雙引號包覆、欄位內 \n 轉換行）。
    ///
    /// 立繪載入：每句左右兩個立繪路徑（catalog id 或 Actor_&lt;情緒&gt;）。因為 Actor_ 要依「目前血統」決定圖，
    /// 立繪在「播放當下」由 <see cref="ResolveGroupAvatars"/>(group, bloodline) 解析（讀目前血統），不在表載入時就定死。
    /// </summary>
    public class DramaTalkDatabase
    {
        // 主角情緒立繪的前綴與資料夾慣例：Actor_<情緒> → Main/Characters/Talk/<血統>/<情緒>
        public const string ActorPrefix = "Actor_";
        const string CharacterTalkRoot = "Main/Characters/Talk";   // catalog id 前綴（與三處同步工具一致）

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

        // 立繪載入（懶漢）：catalog + loader 只建一次，sprite 依「解析後的 catalog id」快取。
        Catalog _catalog;
        MapSpriteLoader _loader;
        readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

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

            // 表頭：ID,Group,Name,LeftAvatarPath,RightAvatarPath,SpotlightSide,Text
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
                    LeftAvatarPath = Field(v, 3),
                    RightAvatarPath = Field(v, 4),
                    SpotlightSide = (Field(v, 5) == "2") ? 2 : 1,   // 2 = 聚光右；其餘（含留空）= 聚光左
                    Text = Unescape(Field(v, 6)),
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

            Debug.Log($"[DramaTalkDatabase] 載入 {all.Count} 句對話、{_groups.Count} 個群組。");
        }

        /// <summary>
        /// 播放前解析某群組所有句子的左右立繪 sprite（填進 <see cref="DramaTalkData.LeftAvatar"/> / <see cref="DramaTalkData.RightAvatar"/>）。
        /// 這裡才解析（而非表載入時）是因為 <c>Actor_&lt;情緒&gt;</c> 要依「目前血統」決定圖；血統可能在遊戲過程中改變。
        /// </summary>
        public void ResolveGroupAvatars(List<DramaTalkData> lines, string bloodline)
        {
            if (lines == null) return;
            foreach (var d in lines)
            {
                d.LeftAvatar = ResolvePortrait(d.LeftAvatarPath, bloodline);
                d.RightAvatar = ResolvePortrait(d.RightAvatarPath, bloodline);
            }
        }

        /// <summary>
        /// 把一個立繪路徑字串解析成 Sprite：
        ///   - 留空 → null（那一側不顯示）。
        ///   - <c>Actor_&lt;情緒&gt;</c> → 主角情緒立繪：catalog id = Main/Characters/Talk/&lt;血統&gt;/&lt;情緒小寫&gt;。
        ///   - 其餘 → 直接當 catalog id（沿用既有立繪管線）。
        /// 找不到圖回 null（TalkPanel 自動隱藏，方便人工抓 bug）。依解析後的 catalog id 快取。
        /// </summary>
        public Sprite ResolvePortrait(string rawPath, string bloodline)
        {
            if (string.IsNullOrEmpty(rawPath)) return null;

            string catalogId;
            if (rawPath.StartsWith(ActorPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                string emotion = rawPath.Substring(ActorPrefix.Length).Trim().ToLowerInvariant();   // angry / cry / ...
                if (string.IsNullOrEmpty(emotion)) return null;
                string blood = string.IsNullOrEmpty(bloodline) ? "Base" : bloodline.Trim();
                catalogId = $"{CharacterTalkRoot}/{blood}/{emotion}";
            }
            else
            {
                catalogId = rawPath;
            }

            if (_spriteCache.TryGetValue(catalogId, out var cached)) return cached;

            EnsureLoader();
            var item = _catalog?.Find(catalogId);
            Sprite sp = item != null ? _loader.GetWholeSprite(item, 1f) : null;
            if (sp == null)
                Debug.LogWarning($"[DramaTalkDatabase] 找不到立繪（catalog id：{catalogId}）。" +
                    (rawPath.StartsWith(ActorPrefix, System.StringComparison.OrdinalIgnoreCase)
                        ? "確認 GameAssets/Main/Characters/Talk/<血統>/<情緒>.png 存在，且已執行 Project Tools → Sync Map Assets。"
                        : "確認圖放在 GameAssets/Modules/<module>/Talk/ 下，且已執行 Project Tools → Sync Map Assets。"));
            _spriteCache[catalogId] = sp;
            return sp;
        }

        void EnsureLoader()
        {
            if (_catalog != null) return;
            _catalog = CatalogLoader.Load(out string assetRoot);
            _loader = new MapSpriteLoader(assetRoot);
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
