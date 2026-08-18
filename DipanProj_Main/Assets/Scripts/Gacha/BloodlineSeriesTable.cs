using System.Collections.Generic;
using UnityEngine;
using Dipan.Data;

namespace Dipan.Gacha
{
    /// <summary>一個血統系列的定義（BloodlineSeriesTable.csv 的一列）＝三個階段。</summary>
    public class BloodlineSeriesDef
    {
        public int SeriesId;
        public string Key;            // 英文代號（除錯訊息用）
        public string DisplayName;    // 系列名（例：殭屍）
        public string Note;

        /// <summary>三個階段的血統 Id，index 0 = 第一階。0 = 這一階沒填（系列可以只有 1~2 階）。</summary>
        public readonly int[] StageIds = new int[BloodlineSeriesTable.MaxStage];

        /// <summary>這個系列實際有幾階（從第一階往後數到第一個空的為止）。</summary>
        public int StageCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < StageIds.Length; i++)
                {
                    if (StageIds[i] <= 0) break;
                    n++;
                }
                return n;
            }
        }

        /// <summary>取第 stage 階（1-based）的血統 Id；超出範圍或沒填回 0。</summary>
        public int IdOfStage(int stage)
        {
            if (stage < 1 || stage > StageIds.Length) return 0;
            return StageIds[stage - 1];
        }
    }

    /// <summary>
    /// 血統系列表（Assets/Data/BloodlineSeriesTable.csv，由場景上的 <see cref="GachaTableProvider"/> 提供）。
    ///
    /// 這張表是「系列 ↔ 階段 ↔ 血統 Id」的唯一真相。<see cref="BloodlineTable"/>（表B）刻意不存
    /// SeriesId / Stage——兩張表都寫就會對不上，所以隸屬關係只寫在這裡，反查索引由本表在載入時建。
    ///
    /// 玩法規則（拍板）：
    ///   ・系列起始藥劑 → 得到 Stage1 的血統（本世只能選一個系列，之後不能改吃別的系列）。
    ///   ・血統進階藥劑（中階 = 目標第 2 階、高階 = 目標第 3 階）→ 沿同一系列往上走，**全系列通用**。
    ///   ・必須逐階：第 1 階的人不能直接喝高階藥劑。
    /// </summary>
    public static class BloodlineSeriesTable
    {
        /// <summary>一個系列最多幾階（表格欄位數也是照這個開的）。</summary>
        public const int MaxStage = 3;

        static Dictionary<int, BloodlineSeriesDef> _bySeries;
        /// <summary>血統 Id → 它在哪個系列的第幾階。人類（沒有系列）不會進這張索引。</summary>
        static Dictionary<int, Locator> _byBloodlineId;
        static bool _warned;

        class Locator
        {
            public BloodlineSeriesDef Series;
            public int Stage;   // 1-based
        }

        /// <summary>進 Play 時清快取。由 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode() { _bySeries = null; _byBloodlineId = null; _warned = false; }

        /// <summary>依系列 Id 取定義；找不到回 null。</summary>
        public static BloodlineSeriesDef Get(int seriesId)
        {
            EnsureLoaded();
            if (_bySeries == null) return null;
            return _bySeries.TryGetValue(seriesId, out var d) ? d : null;
        }

        /// <summary>
        /// 反查某個血統 Id 屬於哪個系列、在第幾階（1-based）。
        /// 人類、或表A 沒登記的血統 → 回 false（series = null、stage = 0）。
        /// </summary>
        public static bool TryLocate(int bloodlineId, out BloodlineSeriesDef series, out int stage)
        {
            EnsureLoaded();
            series = null;
            stage = 0;
            if (_byBloodlineId == null || bloodlineId <= 0) return false;
            if (!_byBloodlineId.TryGetValue(bloodlineId, out var loc)) return false;
            series = loc.Series;
            stage = loc.Stage;
            return true;
        }

        /// <summary>全部系列（給抽選／除錯／未來的角色面板列清單用）。</summary>
        public static IEnumerable<BloodlineSeriesDef> All
        {
            get
            {
                EnsureLoaded();
                return _bySeries != null ? (IEnumerable<BloodlineSeriesDef>)_bySeries.Values
                                         : new BloodlineSeriesDef[0];
            }
        }

        static void EnsureLoaded()
        {
            if (_bySeries != null) return;

            var ta = GachaTableProvider.BloodlineSeriesTableCsv;
            if (ta == null || string.IsNullOrWhiteSpace(ta.text))
            {
                // 不建快取就 return（理由同 GachaPoolTable / BloodlineTable）：
                // provider 還沒就緒時建了空表會永久黏住，之後接好也不重載。
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[BloodlineSeriesTable] 找不到 BloodlineSeriesTable.csv。" +
                                     "請把 Assets/Data/BloodlineSeriesTable.csv 拖進場景上 GachaTableProvider 的「血統系列表」欄。");
                }
                return;
            }

            var bySeries = new Dictionary<int, BloodlineSeriesDef>();
            var byBloodline = new Dictionary<int, Locator>();

            var lines = ta.text.Split('\n');
            for (int i = 1; i < lines.Length; i++)   // 第 0 行是表頭
            {
                string line = lines[i].TrimEnd('\r').Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                var v = CsvUtil.SplitLine(line);
                int sid = CsvUtil.FieldInt(v, 0, 0);
                if (sid <= 0) continue;

                var d = new BloodlineSeriesDef
                {
                    SeriesId = sid,
                    Key = CsvUtil.Field(v, 1),
                    DisplayName = CsvUtil.Field(v, 2),
                };
                for (int s = 0; s < MaxStage; s++)
                    d.StageIds[s] = CsvUtil.FieldInt(v, 3 + s, 0);
                d.Note = CsvUtil.Field(v, 3 + MaxStage);

                if (d.DisplayName.Length == 0) d.DisplayName = d.Key.Length > 0 ? d.Key : $"#{sid}";

                bySeries[sid] = d;

                // 階段必須從第一階起連續。填成 Stage1=10, Stage2=空, Stage3=12 的話 StageCount 只會算到 1，
                // 第三階那個血統雖然進得了反查索引，卻永遠升不上去——而且完全不會報錯。這裡先喊出來。
                for (int s = 1; s < MaxStage; s++)
                {
                    if (d.StageIds[s] > 0 && d.StageIds[s - 1] <= 0)
                    {
                        Debug.LogWarning($"[BloodlineSeriesTable] 系列「{d.DisplayName}」第 {s} 階是空的、" +
                                         $"第 {s + 1} 階卻填了 {d.StageIds[s]}。階段必須從第一階起連續填，" +
                                         "中間跳過的話後面那幾階永遠升不上去。");
                        break;
                    }
                }

                for (int s = 0; s < MaxStage; s++)
                {
                    int bid = d.StageIds[s];
                    if (bid <= 0) continue;
                    if (byBloodline.ContainsKey(bid))
                    {
                        Debug.LogWarning($"[BloodlineSeriesTable] 血統 id {bid} 同時出現在系列 " +
                                         $"{byBloodline[bid].Series.DisplayName} 與 {d.DisplayName}。" +
                                         "一個血統只能屬於一個系列，後面這筆被忽略。");
                        continue;
                    }
                    byBloodline[bid] = new Locator { Series = d, Stage = s + 1 };
                }
            }

            _bySeries = bySeries;
            _byBloodlineId = byBloodline;

            Debug.Log($"[BloodlineSeriesTable] 載入 {_bySeries.Count} 個血統系列（共 {_byBloodlineId.Count} 種血統）。");
        }
    }
}
