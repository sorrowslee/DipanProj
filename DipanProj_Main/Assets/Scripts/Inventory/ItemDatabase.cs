using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Dipan.Inventory
{
    /// <summary>
    /// 物品定義表：載入所有 ItemData，並預載各自的 icon sprite。
    /// 表來源 = 一個 TextAsset（CSV 放在 Assets/Data/ItemTable.csv，與其他表同位置，由場景上的
    /// <see cref="ItemTableProvider"/> 把它拖進 Inspector 提供；見 readme/INVENTORY.md）。
    /// （icon 仍走 Resources/UI/Icons，與表的位置無關。）
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

        /// <summary>從指定 TextAsset 載入（主要路徑：由 ItemTableProvider 提供、CSV 在 Assets/Data）。</summary>
        public void LoadFromTextAsset(TextAsset csv)
        {
            if (csv == null) { Debug.LogError("[ItemDatabase] 傳入的 ItemTable TextAsset 為 null。"); return; }
            LoadFromText(csv.text);
        }

        /// <summary>
        /// 沒有直接 TextAsset 時的通用載入（給 StorageSystem，以及 InventorySystem 找不到 provider 時用）。
        /// 一律以「正典來源」為先：先找場景上的 <see cref="ItemTableProvider"/>（CSV 在 Assets/Data），
        /// 找不到才退回 Resources（舊位置，已淘汰）。這樣所有呼叫端都吃同一份 Assets/Data/ItemTable.csv，
        /// 不會有人讀到 Resources 裡的舊表。
        /// </summary>
        public void LoadFromResources(string path = "Data/ItemTable")
        {
            // 正典：場景上的 ItemTableProvider（Assets/Data/ItemTable.csv）。
            var provider = Object.FindObjectOfType<ItemTableProvider>();
            if (provider != null && provider.itemCSV != null) { LoadFromText(provider.itemCSV.text); return; }

            // 後備：Resources（舊位置）。
            var csv = Resources.Load<TextAsset>(path);
            if (csv == null)
            {
                Debug.LogError($"[ItemDatabase] 找不到 ItemTable。請把 Assets/Data/ItemTable.csv 拖進場景上 " +
                               $"ItemTableProvider 元件的 Item CSV 欄（見 readme/INVENTORY.md）。");
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
                string weaponStr = Field(v, 9);
                d.WeaponID = !string.IsNullOrWhiteSpace(weaponStr) ? int.Parse(weaponStr) : 0;
                string targetStr = Field(v, 10);   // 劇本目的地（選填欄；舊表沒有這欄 → Field 回 "" → 0）
                d.TargetMapId = !string.IsNullOrWhiteSpace(targetStr) ? int.Parse(targetStr) : 0;
                d.TargetEntrance = Field(v, 11);
                string hpStr = Field(v, 12);
                d.HealHp = !string.IsNullOrWhiteSpace(hpStr) ? int.Parse(hpStr) : 0;
                string mpStr = Field(v, 13);
                d.HealMp = !string.IsNullOrWhiteSpace(mpStr) ? int.Parse(mpStr) : 0;
                string lightStr = Field(v, 14);   // 發光半徑（選填；舊表沒這欄 → "" → 0）。用 InvariantCulture 避免逗號小數點地區設定問題。
                d.LightRadius = (!string.IsNullOrWhiteSpace(lightStr)
                    && float.TryParse(lightStr, System.Globalization.NumberStyles.Float,
                                      System.Globalization.CultureInfo.InvariantCulture, out float lr)) ? lr : 0f;

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
