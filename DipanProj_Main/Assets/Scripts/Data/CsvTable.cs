using System;
using System.Collections.Generic;
using System.Globalization;

namespace Dipan.Data
{
    /// <summary>
    /// **依表頭名稱取值**的 CSV 表格解析器（2026-08-26 起所有玩法表統一走這裡）。
    ///
    /// <para><b>為什麼要有這支</b>：以前各張表都是 <c>v[28]</c>、<c>v[42]</c> 這種「第幾欄」寫死在程式裡，
    /// 欄位不能重排、中間不能插欄、加欄還要人工數索引，而且排錯了會<b>靜默讀到隔壁欄</b>不報錯。
    /// 改成先讀第一列表頭建「欄名 → 位置」對照，之後一律 <c>row.GetFloat("Range")</c> 取值——
    /// 欄位可以任意分群重排、表頭打錯字會立刻報「找不到欄位」。見 readme/RECIPE_AND_WEAPON.md。</para>
    ///
    /// <para><b>表頭規則</b>：欄名取<b>括號前</b>的部分（半形 <c>(</c> 或全形 <c>（</c> 都算），括號內是給人看的說明，
    /// 可以隨意改。例：<c>Speed(飛行速度每秒/建議5~100)</c> → 欄名 <c>Speed</c>。欄名比對<b>不分大小寫</b>、會去頭尾空白。</para>
    ///
    /// <para><b>列規則</b>：<c>#</c> 開頭的整列是註解（可放在表頭之後任何位置，用來寫每一組欄位的填寫提示）；空行跳過；
    /// 欄位值支援雙引號包覆（走 <see cref="CsvUtil.SplitLine"/>），所以值裡可以有逗號。</para>
    ///
    /// <para><b>空白＝預設值</b>：所有 <c>GetXxx</c> 在欄位不存在或值為空時回 fallback，不丟例外。
    /// 真正的錯誤（表頭缺必要欄、數值格式壞掉）收集在 <see cref="Errors"/>，由呼叫端決定怎麼印——
    /// 這支刻意<b>不依賴 UnityEngine</b>，才能在 Unity 外用純 C# 編譯器跑單元測試。</para>
    /// </summary>
    public sealed class CsvTable
    {
        readonly List<string> _columns = new List<string>();
        readonly Dictionary<string, int> _index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        readonly List<CsvRow> _rows = new List<CsvRow>();
        readonly List<string> _errors = new List<string>();

        /// <summary>表名（只用在錯誤訊息裡）。</summary>
        public string Name { get; private set; }
        /// <summary>欄名清單（括號前的名字，依表頭順序）。</summary>
        public IReadOnlyList<string> Columns => _columns;
        /// <summary>資料列（已跳過註解與空行）。</summary>
        public IReadOnlyList<CsvRow> Rows => _rows;
        /// <summary>解析過程收集到的錯誤（表頭重複、缺必要欄…）。空 = 乾淨。</summary>
        public IReadOnlyList<string> Errors => _errors;

        public bool HasColumn(string name) => !string.IsNullOrEmpty(name) && _index.ContainsKey(name.Trim());

        /// <summary>取欄位在表頭的位置；沒有回 -1。</summary>
        public int IndexOf(string name) => (!string.IsNullOrEmpty(name) && _index.TryGetValue(name.Trim(), out int i)) ? i : -1;

        /// <summary>解析整份 CSV 文字。永遠回傳物件（即使表頭壞掉），錯誤看 <see cref="Errors"/>。</summary>
        public static CsvTable Parse(string text, string tableName = "CSV")
        {
            var t = new CsvTable { Name = tableName ?? "CSV" };
            if (text == null) { t._errors.Add($"[{t.Name}] 內容為 null。"); return t; }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            bool headerDone = false;
            for (int li = 0; li < lines.Length; li++)
            {
                string raw = lines[li];
                if (li == 0) raw = StripBom(raw);
                if (string.IsNullOrWhiteSpace(raw)) continue;
                if (raw.TrimStart().StartsWith("#")) continue;   // 註解列

                string[] cells = CsvUtil.SplitLine(raw);

                if (!headerDone)
                {
                    for (int c = 0; c < cells.Length; c++)
                    {
                        string col = NormalizeHeader(cells[c]);
                        if (col.Length == 0) col = $"__col{c}";   // 空表頭：給個不會撞的名字，之後取不到就等於沒這欄
                        if (t._index.ContainsKey(col))
                            t._errors.Add($"[{t.Name}] 表頭第 {c + 1} 欄「{col}」重複出現（第 {t._index[col] + 1} 欄已有）。");
                        else
                            t._index[col] = c;
                        t._columns.Add(col);
                    }
                    headerDone = true;
                    continue;
                }

                // 整列都是空白（例如只有一排逗號）也跳過
                bool any = false;
                for (int c = 0; c < cells.Length; c++) if (!string.IsNullOrWhiteSpace(cells[c])) { any = true; break; }
                if (!any) continue;

                t._rows.Add(new CsvRow(t, cells, li + 1));
            }
            if (!headerDone) t._errors.Add($"[{t.Name}] 找不到表頭列（整份都是空行或註解）。");
            return t;
        }

        /// <summary>檢查必要欄是否都在表頭；缺的記進 <see cref="Errors"/> 並回 false。</summary>
        public bool Require(params string[] columns)
        {
            bool ok = true;
            foreach (var c in columns)
            {
                if (HasColumn(c)) continue;
                _errors.Add($"[{Name}] 表頭缺少必要欄位「{c}」。目前的欄位：{string.Join(", ", _columns)}");
                ok = false;
            }
            return ok;
        }

        /// <summary>表頭裡有、但 <paramref name="known"/> 清單裡沒有的欄名（拿來抓打錯字的表頭）。</summary>
        public List<string> UnknownColumns(IEnumerable<string> known)
        {
            var set = new HashSet<string>(known, StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();
            foreach (var c in _columns) if (!c.StartsWith("__col") && !set.Contains(c)) list.Add(c);
            return list;
        }

        /// <summary>把表頭「Speed(飛行速度…)」變成「Speed」：去 BOM、去頭尾空白、切掉第一個括號之後的一切。</summary>
        public static string NormalizeHeader(string cell)
        {
            if (cell == null) return "";
            string s = StripBom(cell).Trim();
            int cut = -1;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '(' || ch == '（' || ch == '[' || ch == '【') { cut = i; break; }
            }
            if (cut >= 0) s = s.Substring(0, cut);
            return s.Trim();
        }

        static string StripBom(string s)
            => (!string.IsNullOrEmpty(s) && s[0] == '﻿') ? s.Substring(1) : s;
    }

    /// <summary>
    /// CSV 的一列。所有取值都是「欄名 → 值」，欄不存在或空白一律回 fallback。
    /// 也可以用 <see cref="ToDictionary"/> 轉成純字典交給 <c>RecipeEntry.FromFields</c> 這類建構函式，
    /// 讓「從 CSV 建」和「從 UI 面板建」走同一條路（武器效果模擬系統靠這個）。
    /// </summary>
    public sealed class CsvRow
    {
        readonly CsvTable _table;
        readonly string[] _cells;

        /// <summary>原始檔案的行號（1 起算，含註解與空行），給錯誤訊息用。</summary>
        public int Line { get; }

        internal CsvRow(CsvTable table, string[] cells, int line)
        {
            _table = table; _cells = cells; Line = line;
        }

        /// <summary>這一欄有沒有填東西（欄存在且非空白）。</summary>
        public bool Has(string column)
        {
            int i = _table.IndexOf(column);
            return i >= 0 && i < _cells.Length && !string.IsNullOrWhiteSpace(_cells[i]);
        }

        /// <summary>取字串（去頭尾空白）；欄不存在或空白回 fallback。</summary>
        public string Get(string column, string fallback = "")
        {
            int i = _table.IndexOf(column);
            if (i < 0 || i >= _cells.Length) return fallback;
            string s = _cells[i];
            if (s == null) return fallback;
            s = s.Trim();
            return s.Length == 0 ? fallback : s;
        }

        public int GetInt(string column, int fallback = 0)
        {
            string s = Get(column, null);
            if (s == null) return fallback;
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r)) return r;
            // 「1.0」這種也認
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return (int)Math.Round(f);
            return fallback;
        }

        public float GetFloat(string column, float fallback = 0f)
        {
            string s = Get(column, null);
            if (s == null) return fallback;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ? r : fallback;
        }

        /// <summary>接受 1/0、true/false、yes/no（不分大小寫）；空白或無法辨識回 fallback。</summary>
        public bool GetBool(string column, bool fallback = false)
        {
            string s = Get(column, null);
            if (s == null) return fallback;
            return CsvFieldParse.Bool(s, fallback);
        }

        /// <summary>百分比欄：「30%」→ 30、「-20%」→ -20、「0.3」→ 0.3（不帶 % 就照原數字）。</summary>
        public float GetPercent(string column, float fallback = 0f)
        {
            string s = Get(column, null);
            if (s == null) return fallback;
            return CsvFieldParse.Percent(s, fallback);
        }

        /// <summary>用 <paramref name="separator"/> 分隔的整數清單（例：召喚名單「12|13|15」）。空 = 空陣列。</summary>
        public int[] GetIntList(string column, char separator = '|')
        {
            string s = Get(column, null);
            if (s == null) return new int[0];
            return CsvFieldParse.IntList(s, separator);
        }

        /// <summary>整列轉成「欄名 → 原始字串」字典（只含有填值的欄）。</summary>
        public Dictionary<string, string> ToDictionary()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var cols = _table.Columns;
            for (int i = 0; i < cols.Count && i < _cells.Length; i++)
            {
                if (cols[i].StartsWith("__col")) continue;
                string s = _cells[i];
                if (string.IsNullOrWhiteSpace(s)) continue;
                d[cols[i]] = s.Trim();
            }
            return d;
        }
    }

    /// <summary>
    /// 單一欄位值的解析（純函式，給 <see cref="CsvRow"/> 與「從字典建資料」的建構函式共用）。
    /// </summary>
    public static class CsvFieldParse
    {
        public static bool Bool(string s, bool fallback = false)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            s = s.Trim();
            if (s == "1") return true;
            if (s == "0") return false;
            if (bool.TryParse(s, out bool b)) return b;
            if (s.Equals("yes", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Equals("no", StringComparison.OrdinalIgnoreCase)) return false;
            if (int.TryParse(s, out int n)) return n != 0;
            return fallback;
        }

        public static int Int(string s, int fallback = 0)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            s = s.Trim();
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r)) return r;
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return (int)Math.Round(f);
            return fallback;
        }

        public static float Float(string s, float fallback = 0f)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ? r : fallback;
        }

        /// <summary>「30%」→ 30；「-20%」→ -20；不帶 % 就照原數字。</summary>
        public static float Percent(string s, float fallback = 0f)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            s = s.Trim();
            if (s.EndsWith("%")) s = s.Substring(0, s.Length - 1).Trim();
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ? r : fallback;
        }

        public static int[] IntList(string s, char separator = '|')
        {
            if (string.IsNullOrWhiteSpace(s)) return new int[0];
            var list = new List<int>();
            foreach (var p in s.Split(separator))
                if (int.TryParse(p.Trim(), out int id)) list.Add(id);
            return list.ToArray();
        }
    }

    /// <summary>
    /// CSV 寫出（給武器工坊存回 RecipeTable／WeaponTable 用；一般表由人手編輯，不會走這裡）。
    /// 表頭與欄順序由呼叫端給；值含逗號／引號／換行時自動用雙引號包（讀回時 <see cref="CsvUtil.SplitLine"/> 認得）。
    /// </summary>
    public static class CsvWriter
    {
        /// <summary>單格跳脫：需要時用雙引號包、內部引號寫成 ""。</summary>
        public static string Escape(string v)
        {
            if (v == null) return "";
            bool need = v.IndexOf(',') >= 0 || v.IndexOf('"') >= 0 || v.IndexOf('\n') >= 0 || v.IndexOf('\r') >= 0
                        || (v.Length > 0 && (char.IsWhiteSpace(v[0]) || char.IsWhiteSpace(v[v.Length - 1])));
            if (!need) return v;
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>一列：依 <paramref name="columns"/> 順序從字典取值（沒有的欄留空）。</summary>
        public static string Row(IReadOnlyList<string> columns, IReadOnlyDictionary<string, string> fields)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < columns.Count; i++)
            {
                if (i > 0) sb.Append(',');
                string v;
                if (fields != null && fields.TryGetValue(columns[i], out v)) sb.Append(Escape(v));
            }
            return sb.ToString();
        }

        /// <summary>整列註解（第一格以 # 開頭，其餘補空欄，讓 Excel 類工具也對得齊）。</summary>
        public static string Comment(int columnCount, string text)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(Escape("# " + (text ?? "")));
            for (int i = 1; i < columnCount; i++) sb.Append(',');
            return sb.ToString();
        }

        /// <summary>組整份檔案：表頭 → 註解列 → 資料列。換行用 \n（與專案現有 CSV 一致）。</summary>
        public static string Build(IReadOnlyList<string> headerCells, IReadOnlyList<string> columns,
                                   IReadOnlyList<string> commentLines, IEnumerable<IReadOnlyDictionary<string, string>> rows)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < headerCells.Count; i++) { if (i > 0) sb.Append(','); sb.Append(Escape(headerCells[i])); }
            sb.Append('\n');
            if (commentLines != null)
                foreach (var c in commentLines) sb.Append(Comment(columns.Count, c)).Append('\n');
            foreach (var r in rows) sb.Append(Row(columns, r)).Append('\n');
            return sb.ToString();
        }
    }
}
