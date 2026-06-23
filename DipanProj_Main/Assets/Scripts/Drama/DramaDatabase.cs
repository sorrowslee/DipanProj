using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Dipan.Drama
{
    /// <summary>
    /// 劇情定義表：載入所有 DramaData，並預載各自的圖。表來源 = 一個 TextAsset
    /// （CSV 在 Assets/Data/DramaTable.csv，由場景上的 <see cref="DramaTableProvider"/> 提供）。
    /// 圖走 Resources（與 icon 慣例相同，路徑不含副檔名）。
    ///
    /// 懶漢快取：第一次存取 <see cref="Instance"/> 自動建立並載入；DramaPanel 用它查 dramaId。
    /// CSV 支援雙引號包覆、欄位內 \n 轉換行（與 ItemDatabase 同套解析）。
    /// </summary>
    public class DramaDatabase
    {
        static DramaDatabase _instance;
        public static DramaDatabase Instance
        {
            get
            {
                if (_instance == null) { _instance = new DramaDatabase(); _instance.Load(); }
                return _instance;
            }
        }

        readonly Dictionary<int, DramaData> _items = new Dictionary<int, DramaData>();

        public DramaData Get(int id)
        {
            _items.TryGetValue(id, out var d);
            return d;
        }

        void Load()
        {
            // 主要來源：場景上 DramaTableProvider 提供的 TextAsset（CSV 在 Assets/Data）。
            var provider = Object.FindObjectOfType<DramaTableProvider>();
            if (provider != null && provider.dramaCSV != null) { LoadFromText(provider.dramaCSV.text); return; }

            // 後備：Resources（舊位置）。一般會印錯誤指引使用者去拖 CSV。
            var csv = Resources.Load<TextAsset>("Data/DramaTable");
            if (csv == null)
            {
                Debug.LogError("[DramaDatabase] 找不到 DramaTable。請把 Assets/Data/DramaTable.csv 拖進場景上 " +
                               "DramaTableProvider 元件的 Drama CSV 欄（見 readme/DRAMA.md）。");
                return;
            }
            LoadFromText(csv.text);
        }

        void LoadFromText(string text)
        {
            _items.Clear();
            string[] lines = (text ?? "").Split('\n');
            for (int i = 1; i < lines.Length; i++)   // 第 0 行是表頭
            {
                string line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] v = ParseCsvLine(line);
                if (v.Length < 1) continue;
                if (!int.TryParse(Field(v, 0), out int id)) continue;

                var d = new DramaData
                {
                    ID = id,
                    ImagePath = Field(v, 1),
                    Text = Unescape(Field(v, 2)),
                };
                if (!string.IsNullOrEmpty(d.ImagePath))
                {
                    d.Image = Resources.Load<Sprite>(d.ImagePath);
                    if (d.Image == null)
                        Debug.LogWarning($"[DramaDatabase] 圖找不到：Resources/{d.ImagePath}（drama {d.ID}）");
                }
                _items[d.ID] = d;
            }
            Debug.Log($"[DramaDatabase] 載入 {_items.Count} 段劇情。");
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
