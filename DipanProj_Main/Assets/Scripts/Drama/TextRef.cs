using Dipan.Localization;

namespace Dipan.Drama
{
    /// <summary>
    /// 資料表裡「玩家可見字串」欄位的共用解析（2026-11-06）。給沒有寫程式的地方（CSV 欄位）引用語言表用：
    /// <list type="bullet">
    /// <item><c>lang:&lt;id&gt;</c> → <see cref="Language.GetText(int)"/>（當前語言）。</item>
    /// <item><c>reveal:&lt;旗標&gt;:&lt;id&gt;</c> → 旗標成立＝語言表該 id；不成立＝<see cref="UnknownNameLangId"/>（「???」）。
    ///   用在「玩家還不知道這個角色叫什麼」：例紅嫁衣 boss 看過家書才顯示真名芙蓉。旗標可帶 <c>永久:</c>／<c>關卡:</c> 前綴（取最後一個冒號後當 id）。</item>
    /// <item>其他 → 原樣當明文（舊資料向下相容）；前綴後面格式不對也原樣顯示，寫錯一眼看得出來。</item>
    /// </list>
    /// 每次呼叫即時查表與旗標 ⇒ 切語言、旗標變了都立即反映，不必重載資料表。
    /// 目前用在：DramaTalkTable 的 <c>Name</c>／<c>Text</c>、MonsterData 的 <c>DisplayName</c>（boss 開戰名牌）。
    /// </summary>
    public static class TextRef
    {
        /// <summary>「還不知道名字」時顯示的字串（LanguageTable 8001 ＝ ???）。</summary>
        public const int UnknownNameLangId = 8001;

        public static string Resolve(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw ?? "";
            string t = raw.Trim();

            if (StartsWith(t, "lang:"))
            {
                return int.TryParse(t.Substring(5).Trim(), out int id) ? Language.GetText(id) : raw;
            }

            if (StartsWith(t, "reveal:"))
            {
                string rest = t.Substring(7);
                int c = rest.LastIndexOf(':');
                if (c > 0 && int.TryParse(rest.Substring(c + 1).Trim(), out int id))
                {
                    string flag = rest.Substring(0, c).Trim();
                    return TriggerChain.FlagTrue(flag) ? Language.GetText(id) : Language.GetText(UnknownNameLangId);
                }
                return raw;
            }

            return raw;
        }

        static bool StartsWith(string s, string prefix)
            => s.Length > prefix.Length && s.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase);
    }
}
