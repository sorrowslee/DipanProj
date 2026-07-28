using System.Collections.Generic;
using UnityEngine;
using Dipan.Data;

namespace Dipan.Gacha
{
    /// <summary>一座祭壇 = 一個抽選池的定義（GachaPoolTable.csv 的一列）。</summary>
    public class GachaPoolDef
    {
        public string PoolId;        // 池代號（openPanel 觸發的 arg 用它指定）
        public string DisplayName;   // 面板標題
        public string BaseTable;     // 基本表檔名（不含 .csv）
        public string SlateSprite;   // 石板圖檔名（給編輯器擺圖對照，程式不讀）
        public int CostSingle = 100;
        public int CostMulti = 0;    // 0 = 不開放連抽
        public int MultiCount = 10;
        public int CostItemId = GachaConstants.DefaultCostItemId;
        public string Note;

        public bool AllowsMulti => CostMulti > 0 && MultiCount > 1;
    }

    /// <summary>抽選系統的共用常數。</summary>
    public static class GachaConstants
    {
        /// <summary>預設拿來當錢的道具：銅錢（與 RunProgress.MoneyItemId 同一個）。</summary>
        public const int DefaultCostItemId = 101;

        /// <summary>
        /// 「本世血統」存在周目旗標裡的 key，值 = BloodlineTable 的 Id 字串。
        /// 刻意用 progress.flags（周目層）而不是 lifetimeFlags——輪迴時 ReincarnateInPlace 會整個換掉
        /// progress，所以下一世自動回到未定型狀態，不需要任何額外的重置程式碼。
        /// </summary>
        public const string BloodlineFlagKey = "血統";
    }

    /// <summary>
    /// 抽選池登記表（Assets/Data/GachaPoolTable.csv，由場景上的 <see cref="GachaTableProvider"/> 提供）。
    ///
    /// 這張表是整套抽選系統的「大項清單」：新增一種可抽的大類 = 加一列；刪除一種 = 刪一列。
    /// 程式碼裡沒有任何地方寫死「武器 / 裝備 / 血統 / 道具」這些字，所以大項增刪不用改程式。
    /// 懶漢載入一次、快取（同 SceneFxTable / ScreenFxTable 的套路）。
    /// </summary>
    public static class GachaPoolTable
    {
        static Dictionary<string, GachaPoolDef> _byId;
        static List<GachaPoolDef> _ordered;

        /// <summary>進 Play 時清快取（Domain Reload 已關；provider 接好前若載過會殘留空表）。由 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode() { _byId = null; _ordered = null; _warned = false; }

        /// <summary>依 poolId 取池定義；找不到回 null（呼叫端要自己印警告）。</summary>
        public static GachaPoolDef Get(string poolId)
        {
            EnsureLoaded();
            if (_byId == null || string.IsNullOrEmpty(poolId)) return null;
            return _byId.TryGetValue(poolId.Trim(), out var def) ? def : null;
        }

        /// <summary>表裡所有池（依 CSV 順序）。給除錯/作弊面板列清單用。</summary>
        public static IReadOnlyList<GachaPoolDef> All
        {
            get { EnsureLoaded(); return _ordered ?? (IReadOnlyList<GachaPoolDef>)System.Array.Empty<GachaPoolDef>(); }
        }

        static bool _warned;

        static void EnsureLoaded()
        {
            if (_byId != null) return;

            var ta = GachaTableProvider.PoolTableCsv;
            if (ta == null || string.IsNullOrWhiteSpace(ta.text))
            {
                // ⚠ 這裡刻意「不建快取」就 return——建了空表就等於永久黏住：
                //   provider 用 FindObjectOfType，在 GameManagers 還沒就緒時（標題畫面、跨場景載入中）會找不到，
                //   若這時把 _byId 設成空字典，之後 provider 接好也不會重載，整場遊戲的祭壇都是空的。
                //   Editor 有 PlayModeStaticReset 兜底，但 build 沒有。
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[GachaPoolTable] 找不到 GachaPoolTable.csv。" +
                                     "請把 Assets/Data/GachaPoolTable.csv 拖進場景上 GachaTableProvider 的「池登記表」欄。");
                }
                return;
            }

            _byId = new Dictionary<string, GachaPoolDef>();
            _ordered = new List<GachaPoolDef>();

            var lines = ta.text.Split('\n');
            for (int i = 1; i < lines.Length; i++)   // 第 0 行是表頭
            {
                string line = lines[i].TrimEnd('\r').Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;   // 空行與 # 註解列

                var v = CsvUtil.SplitLine(line);
                string poolId = CsvUtil.Field(v, 0);
                if (poolId.Length == 0) continue;

                var def = new GachaPoolDef
                {
                    PoolId = poolId,
                    DisplayName = CsvUtil.Field(v, 1),
                    BaseTable = CsvUtil.Field(v, 2),
                    SlateSprite = CsvUtil.Field(v, 3),
                    CostSingle = CsvUtil.FieldInt(v, 4, 100),
                    CostMulti = CsvUtil.FieldInt(v, 5, 0),
                    MultiCount = CsvUtil.FieldInt(v, 6, 10),
                    CostItemId = CsvUtil.FieldInt(v, 7, GachaConstants.DefaultCostItemId),
                    Note = CsvUtil.Field(v, 8),
                };
                if (def.DisplayName.Length == 0) def.DisplayName = def.PoolId;
                if (def.CostItemId <= 0) def.CostItemId = GachaConstants.DefaultCostItemId;

                if (_byId.ContainsKey(def.PoolId))
                {
                    Debug.LogWarning($"[GachaPoolTable] PoolId「{def.PoolId}」重複，後面那列被略過。");
                    continue;
                }
                _byId[def.PoolId] = def;
                _ordered.Add(def);
            }

            Debug.Log($"[GachaPoolTable] 載入 {_ordered.Count} 個抽選池。");
        }
    }
}
