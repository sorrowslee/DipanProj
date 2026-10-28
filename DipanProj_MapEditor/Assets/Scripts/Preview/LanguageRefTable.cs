using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DipanMapEditor.Preview
{
    /// <summary>
    /// 唯讀主專案的 <c>Assets/Data/LanguageTable.csv</c>（欄位 <c>id,cn,en</c>），
    /// 讓編輯器在「填語言表 id」的欄位旁邊直接顯示那句中文，作者一眼就知道有沒有填錯。
    /// 作法同 <see cref="ConditionRefTables"/>：直接讀 DipanProj_Main，不必同步。表改了按面板上的「重讀語言表」。
    ///
    /// <para>⚠ **只讀不寫**（作者 2026-10-28 拍板：步驟只填 id，文字一律在 LanguageTable.csv 維護）。</para>
    /// <para>⚠ 這張表的英文欄常含逗號（用雙引號包），所以**不能**像 ConditionRefTables 那樣單純 Split(',')，
    /// 要照 CSV 規則處理引號；<c>#</c> 開頭或第 0 欄不是整數的列跳過（同遊戲端 LanguageTableProvider）。</para>
    /// </summary>
    public static class LanguageRefTable
    {
        static Dictionary<int, string> _cn;

        /// <summary>丟掉快取，下次取用時重讀檔案。</summary>
        public static void Reload() => _cn = null;

        /// <summary>id → 中文；id≤0 或查無回 null。字面 <c>\n</c> 保留原樣（顯示端自己決定要不要換行）。</summary>
        public static string Get(int id)
        {
            if (id <= 0) return null;
            if (_cn == null) _cn = Load();
            return _cn.TryGetValue(id, out var s) ? s : null;
        }

        static string CsvPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;   // DipanProj_MapEditor
            string repoRoot = Directory.GetParent(projectRoot).FullName;               // DipanProj
            return Path.Combine(repoRoot, "DipanProj_Main", "Assets", "Data", "LanguageTable.csv");
        }

        static Dictionary<int, string> Load()
        {
            var d = new Dictionary<int, string>();
            string path = CsvPath();
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[LanguageRefTable] 找不到 {path}——語言表 id 旁不會顯示文字（填 id 本身不受影響）。");
                return d;
            }
            var lines = File.ReadAllText(path).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 1; i < lines.Length; i++)   // 第 0 列是表頭
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")) continue;
                var f = SplitFirstFields(line, 2);
                if (f.Count < 2 || !int.TryParse(f[0].Trim(), out int id)) continue;
                d[id] = f[1];
            }
            return d;
        }

        /// <summary>照 CSV 規則切出前 <paramref name="max"/> 欄（雙引號內的逗號不分欄、<c>""</c>＝一個雙引號）。</summary>
        static List<string> SplitFirstFields(string line, int max)
        {
            var r = new List<string>();
            var sb = new StringBuilder();
            bool q = false;
            for (int i = 0; i < line.Length && r.Count < max; i++)
            {
                char c = line[i];
                if (q)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else q = false;
                    }
                    else sb.Append(c);
                }
                else if (c == '"') q = true;
                else if (c == ',') { r.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            if (r.Count < max) r.Add(sb.ToString());
            return r;
        }
    }
}
