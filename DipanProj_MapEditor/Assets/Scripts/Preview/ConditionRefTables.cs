using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DipanMapEditor.Preview
{
    /// <summary>
    /// 「偵測條件」要挑 id 時用的參照表：血統系列／血統／背包道具。
    /// 直接讀主專案 <c>Assets/Data/*.csv</c>（同 <see cref="NpcTableEditor"/> 的作法，不必同步素材），
    /// 讓編輯器的條件列可以**選名字**而不是手打 id。表改了按條件列旁的「重讀」即可。
    ///
    /// ⚠ 只讀 id 與顯示名兩欄，且都在各表的前三欄之內——所以用最單純的 split(',')
    ///   就不會被後面「描述」那類含逗號的欄位影響。<c>#</c> 開頭的註解列自動跳過。
    /// </summary>
    public static class ConditionRefTables
    {
        public class Row
        {
            public int Id;
            public string Name = "";
            public string Label => $"{Id}　{Name}";
        }

        static List<Row> _series;      // 血統系列（BloodlineSeriesTable.csv：SeriesId / DisplayName）
        static List<Row> _bloodlines;  // 血統（BloodlineTable.csv：Id / DisplayName）
        static List<Row> _items;       // 背包道具（ItemTable.csv：ID / Name）

        public static List<Row> Series      { get { if (_series == null)     _series     = Load("BloodlineSeriesTable.csv", "seriesid", "displayname"); return _series; } }
        public static List<Row> Bloodlines  { get { if (_bloodlines == null) _bloodlines = Load("BloodlineTable.csv",       "id",       "displayname"); return _bloodlines; } }
        public static List<Row> Items       { get { if (_items == null)      _items      = Load("ItemTable.csv",            "id",       "name");        return _items; } }

        public static void Reload() { _series = null; _bloodlines = null; _items = null; }

        /// <summary>某個 kind 的參照清單；不需要挑清單的 kind（旗標走旗標登記表）回 null。</summary>
        public static List<Row> ListFor(string kind)
        {
            switch (kind)
            {
                case "series":    return Series;
                case "bloodline": return Bloodlines;
                case "item":      return Items;
            }
            return null;
        }

        /// <summary>把 id 翻成名字給面板顯示；查不到回空字串（作者就知道這個 id 不存在）。</summary>
        public static string NameOf(string kind, string value)
        {
            var list = ListFor(kind);
            if (list == null || !int.TryParse((value ?? "").Trim(), out int id)) return "";
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return list[i].Name;
            return "";
        }

        static string CsvPath(string fileName)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;   // DipanProj_MapEditor
            string repoRoot = Directory.GetParent(projectRoot).FullName;               // DipanProj
            return Path.Combine(repoRoot, "DipanProj_Main", "Assets", "Data", fileName);
        }

        static List<Row> Load(string fileName, string idKey, string nameKey)
        {
            var rows = new List<Row>();
            string path = CsvPath(fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[ConditionRefTables] 找不到 {path}——條件列的「選」清單會是空的，仍可手打 id。");
                return rows;
            }

            var lines = File.ReadAllText(path).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length < 2) return rows;

            var header = lines[0].Split(',');
            int idCol = -1, nameCol = -1;
            for (int i = 0; i < header.Length; i++)
            {
                string k = header[i].Trim().ToLowerInvariant();
                if (idCol < 0 && k == idKey) idCol = i;
                if (nameCol < 0 && k == nameKey) nameCol = i;
            }
            if (idCol < 0) return rows;

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.TrimStart().StartsWith("#")) continue;         // 註解列（這幾張表的檔頭說明很長）
                var v = line.Split(',');
                if (idCol >= v.Length || !int.TryParse(v[idCol].Trim(), out int id) || id <= 0) continue;
                rows.Add(new Row
                {
                    Id = id,
                    Name = (nameCol >= 0 && nameCol < v.Length) ? v[nameCol].Trim() : "",
                });
            }
            return rows;
        }
    }
}
