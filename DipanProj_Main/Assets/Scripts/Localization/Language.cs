using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Dipan.Localization
{
    /// <summary>目前語言。預設中文；之後畫面設定可切換英文。</summary>
    public enum Lang { CN, EN }

    /// <summary>
    /// 全遊戲字串的「唯一取用入口」（多語系）。字串放 <c>Assets/Data/LanguageTable.csv</c>（欄位 id,cn,en），
    /// 由場景上的 <see cref="LanguageTableProvider"/> 提供；懶漢載入一次、快取。
    ///
    /// 用法：<c>Language.GetText(1001)</c> 取當前語言的字串。切語言：<c>Language.SetLanguage(Lang.EN)</c>
    /// （GetText 每次即時讀 <see cref="Current"/>，切換立即生效、免重載）。英文欄留空時自動退回中文。
    ///
    /// id 分段慣例（方便擴充、避免撞號）：1001–1099＝新手教學：柴房佛燈。之後每個功能自己一個百/千位段。
    /// 找不到 id 會回傳可辨識的佔位字串 <c>[lang:id]</c>，方便抓漏。
    /// </summary>
    public static class Language
    {
        /// <summary>
        /// **開機／進 Play 的預設語言。要臨時測英文版就改這一行。**
        ///
        /// ⚠ 不要只改下面 <see cref="Current"/> 的初始值——<see cref="ResetForPlayMode"/> 每次進 Play
        /// 都會把 Current 設回這個常數，只改初始值等於白改（2026-08-19 實際踩到）。
        /// 兩邊都讀同一個常數就不會再發生。
        ///
        /// 🔻 **正式版要是 Lang.CN**。現在是 EN 是為了驗英文素材，測完記得改回來。
        /// </summary>
        public const Lang DefaultLanguage = Lang.CN;

        /// <summary>目前語言。切換：SetLanguage。GetText 即時讀取。</summary>
        public static Lang Current = DefaultLanguage;

        /// <summary>
        /// 語言變了。**圖片型文字的面板一定要訂這個**——字串是每次 GetText 即時讀所以自動跟著變，
        /// 但「畫成圖的字」是在面板 OnBuild 時載一次就存住的，不重建就會停在舊語言
        /// （見 <see cref="LocalizedArt"/> 與 UIManager 的訂閱）。
        /// </summary>
        public static event System.Action OnLanguageChanged;

        public static void SetLanguage(Lang lang)
        {
            if (Current == lang) return;
            Current = lang;
            OnLanguageChanged?.Invoke();
        }
        public static bool IsEnglish => Current == Lang.EN;

        struct Row { public string cn; public string en; }
        static Dictionary<int, Row> _rows;

        /// <summary>取 id 對應「當前語言」的字串（英文缺→退回中文；找不到→[lang:id] 佔位）。</summary>
        public static string GetText(int id)
        {
            EnsureLoaded();
            if (_rows.TryGetValue(id, out var r))
                return (Current == Lang.EN && !string.IsNullOrEmpty(r.en)) ? r.en : r.cn;
            return $"[lang:{id}]";
        }

        /// <summary>有這個 id 的字串嗎（不含佔位）。</summary>
        public static bool Has(int id) { EnsureLoaded(); return _rows.ContainsKey(id); }

        /// <summary>重載語言表（改了 CSV 或測試用；立即重讀）。</summary>
        public static void Reload() { _rows = null; EnsureLoaded(); }

        /// <summary>
        /// 進 Play 時清快取（**只清、不立即載**）。因為已關 Domain Reload，static 快取跨 Play 殘留——
        /// 若曾在「provider 還沒接好」時載過一次（快取成空表），之後接好也不會重載 → 字串全變 [lang:id]。
        /// 這裡把 _rows 設回 null，下次 GetText（遊戲進行中、provider 已在場）再懶漢重載乾淨的表。
        /// 由 PlayModeStaticReset 在每次進 Play 最早期呼叫。
        /// </summary>
        public static void ResetForPlayMode()
        {
            _rows = null;
            // ⚠ 連 Current 一起還原。Domain Reload 已關 ⇒ 只要有任何一次切成英文（設定面板、除錯鍵、測試），
            //    之後每一次 Play 都會從英文開始，而且**唯一的徵兆是美術換成英文版**，很難聯想到是殘留。
            //    真的要記住玩家選的語言，應該由存檔在開場時明確設回來，而不是靠 static 殘留。
            //    ⚠ 還原成 DefaultLanguage 而不是寫死 Lang.CN——想測別的語言只要改那個常數一處。
            Current = DefaultLanguage;
            OnLanguageChanged = null;   // 上一輪 Play 的訂閱者（已銷毀的 UIManager）不要留著
        }

        static void EnsureLoaded()
        {
            if (_rows != null) return;
            _rows = new Dictionary<int, Row>();

            // 正典：場景上的 LanguageTableProvider（Assets/Data/LanguageTable.csv）；找不到才退回 Resources（一般沒有）。
            var provider = Object.FindObjectOfType<LanguageTableProvider>();
            var ta = (provider != null && provider.languageCSV != null)
                ? provider.languageCSV
                : Resources.Load<TextAsset>("Data/LanguageTable");
            if (ta == null || string.IsNullOrWhiteSpace(ta.text))
            {
                Debug.LogWarning("[Language] 找不到 LanguageTable.csv。請把 Assets/Data/LanguageTable.csv 拖進場景上 " +
                                 "LanguageTableProvider 元件。字串會以 [lang:id] 佔位顯示。");
                return;
            }

            var lines = ta.text.Replace("\r", "").Split('\n');
            for (int i = 1; i < lines.Length; i++)   // 第 0 列是表頭
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                var v = ParseCsvLine(line);
                // 第 0 欄非整數 id（含「# 註解」列、空列）一律跳過。
                if (v.Count < 1 || !int.TryParse(v[0].Trim(), out int id)) continue;
                string cn = v.Count > 1 ? Unescape(v[1]) : "";
                string en = v.Count > 2 ? Unescape(v[2]) : "";
                _rows[id] = new Row { cn = cn, en = en };
            }
            Debug.Log($"[Language] 載入 {_rows.Count} 筆字串（目前語言：{Current}）。");
        }

        // 逗號分隔、支援雙引號包覆（引號內的逗號不分欄；引號內 "" = 一個雙引號）。與 ItemDatabase 同慣例，
        // 所以英文（常含逗號）也能直接寫，只要用 "..." 包起來。
        static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                    else sb.Append(c);
                }
            }
            fields.Add(sb.ToString());
            return fields;
        }

        // 支援用字面 \n 在字串裡換行（與 ItemTable 慣例一致）。
        static string Unescape(string s) => string.IsNullOrEmpty(s) ? s : s.Replace("\\n", "\n");
    }
}
