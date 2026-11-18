using System.Collections.Generic;
using System.Text;

namespace Dipan.Data
{
    /// <summary>
    /// CSV 解析共用工具。
    ///
    /// <para><b>為什麼有這支</b>：專案裡各張表原本都各自 <c>line.Split(',')</c>，
    /// 那種寫法<b>不支援「欄位內含逗號」</b>——所以像 MonsterData 的怪物台詞、DramaTalkTable 的對話
    /// 都被迫規定「不可以用半形逗號」（見 readme/MONSTER_SPEECH.md）。
    /// 這支把 <c>ItemDatabase</c> 已驗證過的引號解析抽出來共用，讓之後需要的表可以直接換過來。</para>
    ///
    /// <para><b>目前的使用狀態（2026-07-27）</b>：這是<b>純新增</b>的工具，
    /// <b>既有的表一行都沒有改</b>（避免無法編譯驗證的大規模重構風險）。
    /// 之後<b>遇到「這一欄需要能打逗號」時，再把那張表逐個換成 <see cref="SplitLine"/></b> 即可。
    /// 已支援引號的兩處（<c>ItemDatabase</c>、<c>Language</c>）維持原樣，之後順手時可改成呼叫這裡。</para>
    ///
    /// <para><b>換過來的方法</b>：把 <c>string[] v = line.Split(',');</c>
    /// 改成 <c>string[] v = CsvUtil.SplitLine(line);</c>，欄位取用改走 <see cref="Field"/> 就同時有防呆與 Trim。
    /// 注意換完之後，該表的 CSV 若原本有裸露的雙引號會被當成引號語法，需一併檢查資料。</para>
    /// </summary>
    public static class CsvUtil
    {
        /// <summary>
        /// 解析一行 CSV，支援雙引號包覆與引號內的 "" 轉義。
        /// 行為與 <c>ItemDatabase.ParseCsvLine</c> 完全一致（原樣搬移）。
        /// </summary>
        /// <remarks>不做 Trim；要去頭尾空白請用 <see cref="Field"/> 取欄位。</remarks>
        public static string[] SplitLine(string line)
        {
            // null／空行回空陣列（同檔的 Field 系列都是 null-safe，這裡保持一致；
            // 呼叫端漏了「跳過空行」也不會炸）。
            if (string.IsNullOrEmpty(line)) return new string[0];

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

        /// <summary>
        /// 安全取欄位：索引超出範圍或為 null 都回空字串，並去頭尾空白。
        /// <para><b>一律用這支取欄位</b>——CSV 值常帶前導空白，不 Trim 會讓字串比對（例如 BrainType 的 switch）
        /// 靜默對不上而掉回預設值（readme/PROBLEMS.md F4 就是這樣來的）。</para>
        /// </summary>
        public static string Field(string[] v, int i)
            => (v != null && i >= 0 && i < v.Length && v[i] != null) ? v[i].Trim() : "";

        /// <summary>取欄位並轉 int，空白／格式錯誤回 <paramref name="fallback"/>（不丟例外）。</summary>
        public static int FieldInt(string[] v, int i, int fallback = 0)
            => int.TryParse(Field(v, i), out int r) ? r : fallback;

        /// <summary>取欄位並轉 float，空白／格式錯誤回 <paramref name="fallback"/>（不丟例外）。</summary>
        public static float FieldFloat(string[] v, int i, float fallback = 0f)
            => float.TryParse(Field(v, i), out float r) ? r : fallback;

        /// <summary>
        /// 取欄位並轉 bool。接受 true/false（不分大小寫）與 1/0；空白或無法辨識回 <paramref name="fallback"/>。
        /// </summary>
        public static bool FieldBool(string[] v, int i, bool fallback = false)
        {
            string s = Field(v, i);
            if (s.Length == 0) return fallback;
            if (bool.TryParse(s, out bool b)) return b;
            if (int.TryParse(s, out int n)) return n != 0;
            return fallback;
        }

        /// <summary>把字面 \n 轉成真正換行（多行 tooltip／台詞用）。</summary>
        public static string Unescape(string s)
            => string.IsNullOrEmpty(s) ? s : s.Replace("\\n", "\n");
    }
}
