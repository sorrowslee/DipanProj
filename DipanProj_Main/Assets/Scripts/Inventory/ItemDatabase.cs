using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Dipan.Inventory
{
    /// <summary>
    /// 物品定義表：從 Resources/Data/ItemTable.csv 載入所有 ItemData，並預載各自的 icon sprite。
    /// CSV 支援**雙引號包覆**的欄位（內含逗號的長文字請用 "..." 包起來，引號內的 "" 表示一個雙引號），
    /// 因此 tooltip 文字可自由使用逗號。欄位中的 \n 會被轉成換行。
    /// </summary>
    public class ItemDatabase
    {
        readonly Dictionary<int, ItemData> _items = new Dictionary<int, ItemData>();

        public IReadOnlyDictionary<int, ItemData> Items => _items;

        public ItemData Get(int id)
        {
            _items.TryGetValue(id, out var d);
            return d;
        }

        public void LoadFromResources(string path = "Data/ItemTable")
        {
            var csv = Resources.Load<TextAsset>(path);
            if (csv == null)
            {
                Debug.LogError($"[ItemDatabase] 找不到 CSV：Resources/{path}");
                return;
            }

            string[] lines = csv.text.Split('\n');
            for (int i = 1; i < lines.Length; i++)   // 第 0 行是表頭
            {
                string line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] v = ParseCsvLine(line);
                if (v.Length < 5) continue;   // ID, Name, Category, EquipSlot, IconPath 為必要欄位

                var d = new ItemData();
                d.ID = int.Parse(Field(v, 0));
                d.Name = Field(v, 1);
                d.Category = Field(v, 2);
                System.Enum.TryParse(Field(v, 3), true, out EquipSlot es);   // 解析失敗 = None
                d.EquipSlot = es;
                d.IconPath = Field(v, 4);
                string maxStr = Field(v, 5);
                d.MaxStack = !string.IsNullOrWhiteSpace(maxStr) ? int.Parse(maxStr) : 1;
                if (d.MaxStack < 1) d.MaxStack = 1;
                d.Description = Unescape(Field(v, 6));
                d.TipStats = Unescape(Field(v, 7));
                d.TipLore = Unescape(Field(v, 8));

                if (!string.IsNullOrEmpty(d.IconPath))
                {
                    d.Icon = Resources.Load<Sprite>(d.IconPath);
                    if (d.Icon == null)
                        Debug.LogWarning($"[ItemDatabase] icon 找不到：Resources/{d.IconPath}（item {d.ID} {d.Name}）");
                }

                _items[d.ID] = d;
            }

            Debug.Log($"[ItemDatabase] 載入 {_items.Count} 個物品。");
        }

        /// <summary>安全取欄位（超出範圍回空字串），並去頭尾空白。</summary>
        static string Field(string[] v, int i) => (i < v.Length && v[i] != null) ? v[i].Trim() : "";

        /// <summary>把字面 \n 轉成真正換行（讓 tooltip 文字可多行）。</summary>
        static string Unescape(string s) => string.IsNullOrEmpty(s) ? s : s.Replace("\\n", "\n");

        /// <summary>解析一行 CSV，支援雙引號包覆與引號內的 "" 轉義。</summary>
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
