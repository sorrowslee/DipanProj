using System.Collections.Generic;
using UnityEngine;
using Dipan.Data;
using Dipan.Gacha;
using Dipan.Inventory;

namespace Dipan.Exchange
{
    /// <summary>
    /// 兌換表（Assets/Data/ExchangeTable.csv，由場景上的 <see cref="GachaTableProvider"/> 的「兌換表」欄提供）。
    ///
    /// 欄位：<c>ExchangeId,ItemId,Category,Spec,Cost,SortOrder,Note</c>（說明見 readme/EXCHANGE.md §3）
    /// - <b>Cost</b>：<c>貨幣:數量</c>，多種貨幣用分號分隔（例 <c>proofB:1;merit:100</c>）。只寫數字＝功德值。
    /// - <b>Category</b> 留空 ＝ 依 ItemTable 推斷。
    /// - <b>Spec</b>（2026-11-17 取代原本的 Rarity 欄，同一個位置）：生成規格 <c>q=4</c> 這類（語法見 <see cref="ItemSpec"/>），
    ///   **兌換那一刻才照它產生**——效果與孔數每次兌換各骰各的。留空＝全隨機。
    ///   品質不再寫在兌換表：裝備看 Spec、非裝備看 ItemTable 的 Rarity 欄（readme/EQUIPMENT.md §6.3）。
    ///
    /// 懶漢載入一次、快取（同 GachaPoolTable 的套路；provider 還沒就緒時不建快取，避免黏住空表）。
    /// </summary>
    public static class ExchangeTable
    {
        static List<ExchangeEntry> _all;
        static Dictionary<int, ExchangeEntry> _byId;
        static bool _warned;

        /// <summary>進 Play 時清快取（Domain Reload 已關）。由 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode() { _all = null; _byId = null; _warned = false; }

        /// <summary>表裡所有可兌換的品項（CSV 順序）。</summary>
        public static IReadOnlyList<ExchangeEntry> All
        {
            get { EnsureLoaded(); return _all ?? (IReadOnlyList<ExchangeEntry>)System.Array.Empty<ExchangeEntry>(); }
        }

        public static ExchangeEntry Get(int exchangeId)
        {
            EnsureLoaded();
            return _byId != null && _byId.TryGetValue(exchangeId, out var e) ? e : null;
        }

        static void EnsureLoaded()
        {
            if (_all != null) return;

            var ta = GachaTableProvider.ExchangeTableCsv;
            var inv = InventorySystem.Instance;
            if (ta == null || string.IsNullOrWhiteSpace(ta.text) || inv == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning(ta == null
                        ? "[ExchangeTable] 找不到 ExchangeTable.csv。請把 Assets/Data/ExchangeTable.csv 拖進場景上 GachaTableProvider 的「Exchange Table CSV」欄。"
                        : "[ExchangeTable] 背包系統還沒就緒，晚點再載入兌換表。");
                }
                return;   // 刻意不建快取（見 GachaPoolTable.EnsureLoaded 的說明）
            }

            _all = new List<ExchangeEntry>();
            _byId = new Dictionary<int, ExchangeEntry>();

            var lines = ta.text.Split('\n');
            for (int i = 1; i < lines.Length; i++)   // 第 0 行是表頭
            {
                string line = lines[i].TrimEnd('\r').Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                var v = CsvUtil.SplitLine(line);
                int exId = CsvUtil.FieldInt(v, 0, 0);
                int itemId = CsvUtil.FieldInt(v, 1, 0);
                if (exId <= 0 || itemId <= 0) continue;

                var d = inv.GetData(itemId);
                if (d == null)
                {
                    Debug.LogWarning($"[ExchangeTable] 第 {i + 1} 行：ItemId {itemId} 不在 ItemTable 裡，略過。");
                    continue;
                }
                if (_byId.ContainsKey(exId))
                {
                    Debug.LogWarning($"[ExchangeTable] ExchangeId {exId} 重複，第 {i + 1} 行被略過。");
                    continue;
                }

                string specText = CsvUtil.Field(v, 3);
                var spec = ItemSpec.Parse(specText, $"兌換表第 {i + 1} 行（{d.Name}）");
                if (spec != null && !ItemManager.NeedsInstance(d))
                {
                    Debug.LogWarning($"[ExchangeTable] 第 {i + 1} 行（{d.Name}）不是裝備或能力珠，Spec「{specText}」沒有作用。");
                    spec = null;
                }

                var e = new ExchangeEntry
                {
                    ExchangeId = exId,
                    ItemId = itemId,
                    Item = d,
                    Category = ExchangeCategoryInfo.Parse(CsvUtil.Field(v, 2), d),
                    Spec = spec,
                    SortOrder = CsvUtil.FieldInt(v, 5, exId * 10),
                };
                e.ResolveRarity();
                if (d.IsEquippable && e.FixedQuality > 0)
                {
                    int max = ItemQuality.MaxQuality(d);
                    if (e.FixedQuality > max)
                        Debug.LogWarning($"[ExchangeTable] 第 {i + 1} 行（{d.Name}）寫死品質 {e.FixedQuality}，但這件能長的效果最多只到品質 {max}——兌換出來會被降級。");
                }
                ParseCost(CsvUtil.Field(v, 4), e.Cost, i + 1);
                if (e.Cost.Count == 0)
                {
                    Debug.LogWarning($"[ExchangeTable] 第 {i + 1} 行（{d.Name}）沒有填價格，略過。格式：merit:500");
                    continue;
                }

                _byId[exId] = e;
                _all.Add(e);
            }

            Debug.Log($"[ExchangeTable] 載入 {_all.Count} 個兌換品項。");
        }

        /// <summary>解析 <c>merit:500</c> 或 <c>proofB:1;merit:100</c>（別名：copper＝merit、B／A／S＝proofB／A／S）。只寫數字 ＝ 功德值。</summary>
        static void ParseCost(string s, List<CostPart> into, int lineNo)
        {
            if (string.IsNullOrWhiteSpace(s)) return;
            foreach (var raw in s.Split(';'))
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                string key = ExchangeCurrencies.Merit, num = part;
                int colon = part.IndexOf(':');
                if (colon >= 0) { key = ExchangeCurrencies.Normalize(part.Substring(0, colon)); num = part.Substring(colon + 1).Trim(); }

                if (!int.TryParse(num, out int amount) || amount < 0)
                {
                    Debug.LogWarning($"[ExchangeTable] 第 {lineNo} 行：價格「{part}」看不懂，這一段略過。");
                    continue;
                }
                if (ExchangeCurrencies.Get(key) == null)
                    Debug.LogWarning($"[ExchangeTable] 第 {lineNo} 行：未登記的貨幣「{key}」（可用 merit／proofB／proofA／proofS，或 B／A／S），這個品項會顯示成尚未開放。");
                into.Add(new CostPart { CurrencyKey = key, Amount = amount });
            }
        }
    }
}
