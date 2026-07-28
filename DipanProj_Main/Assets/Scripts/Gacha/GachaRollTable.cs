using System.Collections.Generic;
using UnityEngine;
using Dipan.Data;

namespace Dipan.Gacha
{
    /// <summary>基本表的一列：一個「一開始就抽得到」的東西。</summary>
    public class GachaRollEntry
    {
        public int ItemId;
        public int Weight = 1;
        public int MinCycle;         // 0/空 = 不限制
        public string RequireFlag;   // 空 = 不限制；前綴 ! 表否定
        public string Note;
    }

    /// <summary>
    /// 每個抽選池的「基本表」（BaseWeaponRoll.csv 之類）。表名由 <see cref="GachaPoolDef.BaseTable"/> 指定，
    /// 實際 TextAsset 由場景上的 <see cref="GachaTableProvider"/> 以陣列提供（用 TextAsset 的名字對應）。
    ///
    /// 注意：這張表只放「基本盤」。關卡解鎖進池的東西存在存檔裡（見 <see cref="Dipan.Save.SaveManager"/> 的
    /// 解鎖清單 API），實際抽選池 = 基本表（過濾條件通過的） ∪ 存檔解鎖清單。這樣解鎖不必改 CSV。
    /// </summary>
    public static class GachaRollTable
    {
        static Dictionary<string, List<GachaRollEntry>> _byTable;

        static bool _warned;

        /// <summary>進 Play 時清快取。由 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode() { _byTable = null; _warned = false; }

        /// <summary>取某張基本表的所有列（未過濾）。表不存在回空清單（不是 null）。</summary>
        public static List<GachaRollEntry> Get(string tableName)
        {
            EnsureLoaded();
            if (_byTable == null || string.IsNullOrEmpty(tableName)) return new List<GachaRollEntry>();
            return _byTable.TryGetValue(tableName.Trim(), out var rows) ? rows : new List<GachaRollEntry>();
        }

        static void EnsureLoaded()
        {
            if (_byTable != null) return;

            var assets = GachaTableProvider.RollTableCsvs;
            if (assets == null || assets.Length == 0)
            {
                // 不建快取就 return（理由同 GachaPoolTable）：provider 還沒就緒時建了空表會永久黏住。
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[GachaRollTable] 場景上的 GachaTableProvider 沒有掛任何基本表。" +
                                     "請把 Assets/Data/BaseXxxRoll.csv 逐張拖進「基本表」陣列。");
                }
                return;
            }

            _byTable = new Dictionary<string, List<GachaRollEntry>>();
            int total = 0;
            foreach (var ta in assets)
            {
                if (ta == null) continue;
                var rows = Parse(ta);
                if (_byTable.ContainsKey(ta.name))
                {
                    Debug.LogWarning($"[GachaRollTable] 基本表「{ta.name}」重複掛了兩次，後面那張被略過。");
                    continue;
                }
                _byTable[ta.name] = rows;
                total += rows.Count;
            }

            Debug.Log($"[GachaRollTable] 載入 {_byTable.Count} 張基本表、共 {total} 列。");
        }

        static List<GachaRollEntry> Parse(TextAsset ta)
        {
            var rows = new List<GachaRollEntry>();
            if (string.IsNullOrWhiteSpace(ta.text)) return rows;

            var lines = ta.text.Split('\n');
            for (int i = 1; i < lines.Length; i++)   // 第 0 行是表頭
            {
                string line = lines[i].TrimEnd('\r').Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                var v = CsvUtil.SplitLine(line);
                int itemId = CsvUtil.FieldInt(v, 0, 0);
                if (itemId <= 0) continue;

                rows.Add(new GachaRollEntry
                {
                    ItemId = itemId,
                    Weight = Mathf.Max(1, CsvUtil.FieldInt(v, 1, 1)),
                    MinCycle = CsvUtil.FieldInt(v, 2, 0),
                    RequireFlag = CsvUtil.Field(v, 3),
                    Note = CsvUtil.Field(v, 4),
                });
            }
            return rows;
        }
    }
}
