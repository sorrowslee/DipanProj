using System.Collections.Generic;
using UnityEngine;
using Dipan.Data;
using Dipan.Gacha;
using Dipan.Localization;

namespace Dipan.Progress
{
    /// <summary>
    /// 劇本證明表（Assets/Data/ProofTable.csv，由場景上 <see cref="GachaTableProvider"/> 的「Proof Table CSV」欄提供）。
    /// 一列＝某劇本的某一級證明：達成後結算畫面顯示的**金色條件文字**（語言表 id）。
    ///
    /// 欄位：<c>Module,Tier,TextLangId,Note</c>
    /// - <b>Module</b>：MapsTable 的 Module（例 RedBridalGown）
    /// - <b>Tier</b>：B／A／S
    /// - <b>TextLangId</b>：語言表 id（文字放語言表＝可翻譯，也避免句子裡的逗號切壞 CSV）
    ///
    /// ⚠ **證明「什麼時候給」不寫在這張表**——那是地圖觸發鏈上 <c>grantProof</c> 的事（條件依劇本而異）。
    /// 這張表只管「給了之後顯示什麼」，以及「這個劇本有沒有證明」（沒有任何一列的 module＝結算畫面不顯示證明三行）。
    /// 見 readme/CORE_LOOP_DESIGN.md §8、readme/LEVEL_END_FLOW.md。
    /// </summary>
    public static class ProofTable
    {
        static Dictionary<string, int> _text;   // "module|B" → langId
        static HashSet<string> _modules;
        static bool _warned;

        /// <summary>進 Play 時清快取（Domain Reload 已關）。由 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode() { _text = null; _modules = null; _warned = false; }

        /// <summary>這個劇本有沒有登記證明（沒有＝結算畫面不顯示證明區）。</summary>
        public static bool HasModule(string module)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(module) && _modules != null && _modules.Contains(module);
        }

        /// <summary>達成後顯示的條件文字；表裡沒有回空字串。</summary>
        public static string ConditionText(string module, ProofTier tier)
        {
            EnsureLoaded();
            if (_text == null || string.IsNullOrEmpty(module)) return "";
            return _text.TryGetValue(module + "|" + ProofTierInfo.Key(tier), out int id) ? Language.GetText(id) : "";
        }

        static void EnsureLoaded()
        {
            if (_text != null) return;

            var ta = GachaTableProvider.ProofTableCsv;
            if (ta == null || string.IsNullOrWhiteSpace(ta.text))
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[ProofTable] 找不到 ProofTable.csv。請把 Assets/Data/ProofTable.csv 拖進場景上 GachaTableProvider 的「Proof Table CSV」欄。");
                }
                return;   // 不建快取：provider 晚一點就緒時還能再載
            }

            _text = new Dictionary<string, int>();
            _modules = new HashSet<string>();
            var lines = ta.text.Split('\n');
            for (int i = 1; i < lines.Length; i++)   // 第 0 行是表頭
            {
                string line = lines[i].TrimEnd('\r').Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var v = CsvUtil.SplitLine(line);
                string module = CsvUtil.Field(v, 0).Trim();
                if (string.IsNullOrEmpty(module)) continue;
                if (!ProofTierInfo.TryParse(CsvUtil.Field(v, 1), out var tier))
                {
                    Debug.LogWarning($"[ProofTable] 第 {i + 1} 行：看不懂的等級「{CsvUtil.Field(v, 1)}」（要填 B／A／S），略過。");
                    continue;
                }
                _modules.Add(module);
                _text[module + "|" + ProofTierInfo.Key(tier)] = CsvUtil.FieldInt(v, 2, 0);
            }
            Debug.Log($"[ProofTable] 載入 {_modules.Count} 個劇本的證明條件。");
        }
    }
}
