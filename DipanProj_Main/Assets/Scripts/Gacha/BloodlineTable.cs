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
        public string SpriteFolder;   // 外型資料夾名，同時決定序列圖與對話立繪的資料夾

        /// <summary>
        /// 體型倍率（以人類 Base 為 1）。**純視覺**：只改角色圖畫多大，不動碰撞框、不動任何數值。
        /// 引擎本來就會把各血統的 idle 可見高度正規化成同一個世界高度，但正規化只看高度不看體積與姿勢，
        /// 瘦長挺立的圖看起來就是比橫向壯碩的圖小一號——這一欄是拿來用眼睛校正那個落差的。
        /// </summary>
        public float BodyScale = 1f;

        // ── 五個角色屬性 ──
        // ⚠ 目前「只存數值、完全不套用」：遊戲裡還沒有角色屬性系統，沒有任何一處會讀它們。
        //   換血統在體感上只有外型與立繪會變，戰力完全不變，這是預期行為。
        //   等屬性系統做好，再決定每個屬性換算成什麼效果（見 readme/BLOODLINE.md）。
        public float WalkSpeed;       // 行走速度
        public float Strength;        // 力量
        public float Agility;         // 敏捷
        public float Magic;           // 魔力
        public float Vitality;        // 體力

        public int SkillId;           // 預留：技能系統還不存在
        public string Note;
    }

    /// <summary>
    /// 血統表（表B，Assets/Data/BloodlineTable.csv，由場景上的 <see cref="GachaTableProvider"/> 提供）。
    ///
    /// 血統藥劑是 ItemTable 的一列（Category=Bloodline），用 <c>BloodlineID</c> 欄指到這裡的 Id——
    /// 與「武器道具 ItemTable.WeaponID → WeaponTable」完全同一個模式。
    ///
    /// 「這個血統屬於哪個系列、在第幾階」不在這裡，查 <see cref="BloodlineSeriesTable"/>（表A）。
    /// 兩張表都寫就會對不上，所以隸屬關係只有表A 一個真相。
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

        /// <summary>依 Id 取顯示名；找不到回退字串。</summary>
        public static string NameOf(int id, string fallback = "人類")
        {
            var d = Get(id);
            return d != null ? d.DisplayName : fallback;
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
                    BodyScale = CsvUtil.FieldFloat(v, 4, 1f),
                    WalkSpeed = CsvUtil.FieldFloat(v, 5, 0f),
                    Strength = CsvUtil.FieldFloat(v, 6, 0f),
                    Agility = CsvUtil.FieldFloat(v, 7, 0f),
                    Magic = CsvUtil.FieldFloat(v, 8, 0f),
                    Vitality = CsvUtil.FieldFloat(v, 9, 0f),
                    SkillId = CsvUtil.FieldInt(v, 10, 0),
                    Note = CsvUtil.Field(v, 11),
                };
                if (d.DisplayName.Length == 0) d.DisplayName = d.Key.Length > 0 ? d.Key : $"#{id}";
                // 留空/0/負數一律當 1；上限擋在 5 倍，填錯一個 0 不會讓角色大到蓋滿整個畫面。
                d.BodyScale = d.BodyScale <= 0.01f ? 1f : Mathf.Clamp(d.BodyScale, 0.2f, 5f);

                _byId[id] = d;
            }

            Debug.Log($"[BloodlineTable] 載入 {_byId.Count} 種血統。");
        }
    }
}
