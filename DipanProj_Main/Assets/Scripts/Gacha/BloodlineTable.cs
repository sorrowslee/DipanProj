using System.Collections.Generic;
using UnityEngine;
using Dipan.Data;

namespace Dipan.Gacha
{
    /// <summary>一種血統的定義（BloodlineTable.csv 的一列）。</summary>
    public class BloodlineDef
    {
        public int Id;
        public string Key;            // 英文代號（除錯訊息用）
        public string DisplayName;
        public string SpriteFolder;   // GameAssets/Main/Characters/SequenceImage/<這一欄>/
        public float MaxHpAdd;
        public float MoveSpeedMul = 1f;
        public float OutgoingDamageBonusPercent;
        public int SkillId;           // 預留：技能系統還不存在
        public string Note;
    }

    /// <summary>
    /// 血統表（Assets/Data/BloodlineTable.csv，由場景上的 <see cref="GachaTableProvider"/> 提供）。
    ///
    /// 血統藥劑是 ItemTable 的一列（Category=Bloodline），用 <c>BloodlineID</c> 欄指到這裡的 Id——
    /// 與「武器道具 ItemTable.WeaponID → WeaponTable」完全同一個模式。
    /// 血統的數值／技能全部放這張表，不污染 ItemTable。
    /// </summary>
    public static class BloodlineTable
    {
        /// <summary>初始外型（人類）的血統 Id。輪迴後回到這個狀態。</summary>
        public const int HumanId = 1;

        static Dictionary<int, BloodlineDef> _byId;

        static bool _warned;

        /// <summary>進 Play 時清快取。由 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode() { _byId = null; _warned = false; }

        /// <summary>依 Id 取血統定義；找不到回 null。</summary>
        public static BloodlineDef Get(int id)
        {
            EnsureLoaded();
            if (_byId == null) return null;
            return _byId.TryGetValue(id, out var d) ? d : null;
        }

        static void EnsureLoaded()
        {
            if (_byId != null) return;

            var ta = GachaTableProvider.BloodlineTableCsv;
            if (ta == null || string.IsNullOrWhiteSpace(ta.text))
            {
                // 不建快取就 return（理由同 GachaPoolTable）：provider 還沒就緒時建了空表會永久黏住。
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[BloodlineTable] 找不到 BloodlineTable.csv。" +
                                     "請把 Assets/Data/BloodlineTable.csv 拖進場景上 GachaTableProvider 的「血統表」欄。");
                }
                return;
            }

            _byId = new Dictionary<int, BloodlineDef>();

            var lines = ta.text.Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r').Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                var v = CsvUtil.SplitLine(line);
                int id = CsvUtil.FieldInt(v, 0, 0);
                if (id <= 0) continue;

                var d = new BloodlineDef
                {
                    Id = id,
                    Key = CsvUtil.Field(v, 1),
                    DisplayName = CsvUtil.Field(v, 2),
                    SpriteFolder = CsvUtil.Field(v, 3),
                    MaxHpAdd = CsvUtil.FieldFloat(v, 4, 0f),
                    MoveSpeedMul = CsvUtil.FieldFloat(v, 5, 1f),
                    OutgoingDamageBonusPercent = CsvUtil.FieldFloat(v, 6, 0f),
                    SkillId = CsvUtil.FieldInt(v, 7, 0),
                    Note = CsvUtil.Field(v, 8),
                };
                if (d.DisplayName.Length == 0) d.DisplayName = d.Key.Length > 0 ? d.Key : $"#{id}";
                if (d.MoveSpeedMul <= 0.01f) d.MoveSpeedMul = 1f;

                _byId[id] = d;
            }

            Debug.Log($"[BloodlineTable] 載入 {_byId.Count} 種血統。");
        }
    }
}
