using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Dipan.Inventory
{
    /// <summary>珠子的能力要套到哪一層。</summary>
    public enum GemTarget
    {
        /// <summary>配方（RecipeTable / ProjectileData 的欄位）：反彈、穿透、速度、分裂…</summary>
        Recipe = 0,
        /// <summary>武器（WeaponTable 的欄位）：傷害、子彈大小、耗魔…</summary>
        Weapon = 1,
        /// <summary>角色屬性：最大生命、減傷、移動速度…（預留，效果尚未接）</summary>
        Player = 2,
    }

    /// <summary>
    /// 一種能力珠的定義（GemTable.csv 一列 = 一種珠子，三個等級共用同一列）。
    ///
    /// <see cref="Field"/> 刻意**原文照抄 RecipeTable / WeaponTable 的欄位名**——
    /// 這樣「反彈」這個功能在兩邊叫同一個名字，不會出現同一件事兩種命名的情況。
    /// 見 readme/GEM_SOCKET.md。
    /// </summary>
    public class GemData
    {
        public int GemID;
        public string Name;
        /// <summary>要改的欄位名，原文照抄目標表的欄位（例：MaxBounces / PierceCount / Speed / Damage）。</summary>
        public string Field;
        public GemTarget Target = GemTarget.Recipe;
        /// <summary>Lv1~Lv3 的數值。表裡填 30% 就是百分比（見 <see cref="IsPercent"/>）。</summary>
        public readonly float[] Levels = new float[3];
        /// <summary>這顆珠子是不是百分比加成（表裡的值帶 %）。百分比會累加後乘上基礎值，不是直接相加。</summary>
        public bool IsPercent;

        /// <summary>疊在珠身上的能力符號檔名後半段（<c>gemIcon_&lt;這裡&gt;</c>）。空 = 不疊符號。</summary>
        public string Icon;

        /// <summary>
        /// 珠身顏色（<c>gemBase_&lt;這裡&gt;_lv1~3</c>）。
        /// CSV 留空時由 <see cref="Target"/> 自動推導：Recipe/Weapon → red（技能珠）、Player → blue（屬性珠）。
        /// 想把屬性珠再細分（藍=生命、黃=防禦）才需要在表裡明寫。
        /// </summary>
        public string BaseColor;

        public string Note;

        /// <summary>取某等級的數值（等級夾在 1~3）。</summary>
        public float ValueAt(int level)
        {
            int i = Mathf.Clamp(level, 1, 3) - 1;
            return Levels[i];
        }
    }

    /// <summary>
    /// 能力珠定義表（Assets/Data/GemTable.csv）。
    /// 由場景上的 <see cref="GemTableProvider"/> 把 CSV 拖進來提供，與其他資料表同慣例。
    /// </summary>
    public class GemDatabase
    {
        readonly Dictionary<int, GemData> _gems = new Dictionary<int, GemData>();

        public IReadOnlyDictionary<int, GemData> Gems => _gems;

        public GemData Get(int gemId)
        {
            _gems.TryGetValue(gemId, out var g);
            return g;
        }

        public void LoadFromTextAsset(TextAsset csv)
        {
            if (csv == null) { Debug.LogError("[GemDatabase] 傳入的 GemTable TextAsset 為 null。"); return; }
            LoadFromText(csv.text);
        }

        /// <summary>找不到直接的 TextAsset 時：先找場景上的 provider，再退回 Resources。</summary>
        public void LoadAuto(string resourcesPath = "Data/GemTable")
        {
            var provider = Object.FindObjectOfType<GemTableProvider>();
            if (provider != null && provider.gemCSV != null) { LoadFromText(provider.gemCSV.text); return; }

            var csv = Resources.Load<TextAsset>(resourcesPath);
            if (csv == null)
            {
                Debug.LogWarning("[GemDatabase] 找不到 GemTable。請把 Assets/Data/GemTable.csv 拖進場景上 " +
                                 "GemTableProvider 元件的 Gem CSV 欄（見 readme/GEM_SOCKET.md）。");
                return;
            }
            LoadFromText(csv.text);
        }

        void LoadFromText(string text)
        {
            _gems.Clear();
            string[] lines = (text ?? "").Split('\n');
            for (int i = 1; i < lines.Length; i++)   // 第 0 行是表頭
            {
                string line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.TrimStart().StartsWith("#")) continue;   // # 開頭 = 註解列

                string[] v = line.Split(',');
                if (v.Length < 7) continue;
                if (!int.TryParse(Field(v, 0), out int id)) continue;

                var g = new GemData();
                g.GemID = id;
                g.Name = Field(v, 1);
                g.Field = Field(v, 2);
                g.Target = ParseTarget(Field(v, 3));
                for (int lv = 0; lv < 3; lv++)
                {
                    g.Levels[lv] = ParseValue(Field(v, 4 + lv), out bool pct);
                    if (pct) g.IsPercent = true;
                }
                g.Icon = Field(v, 7);
                g.BaseColor = Field(v, 8);
                if (string.IsNullOrEmpty(g.BaseColor)) g.BaseColor = DefaultColorFor(g.Target);
                g.Note = Field(v, 9);

                if (string.IsNullOrEmpty(g.Field))
                {
                    Debug.LogWarning($"[GemDatabase] 珠子 {g.GemID} '{g.Name}' 沒填 Field（要改哪個欄位），已略過。");
                    continue;
                }
                _gems[g.GemID] = g;
            }
            Debug.Log($"[GemDatabase] 載入 {_gems.Count} 種能力珠。");
        }

        /// <summary>沒填 BaseColor 時的預設珠身顏色：技能珠（配方/武器欄位）紅、屬性珠藍。</summary>
        static string DefaultColorFor(GemTarget t) => t == GemTarget.Player ? "blue" : "red";

        static GemTarget ParseTarget(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return GemTarget.Recipe;
            switch (s.Trim().ToLowerInvariant())
            {
                case "weapon": return GemTarget.Weapon;
                case "player": return GemTarget.Player;
                default: return GemTarget.Recipe;
            }
        }

        /// <summary>解析數值；支援尾端百分比（"30%" → 0.30 且 isPercent = true）。</summary>
        static float ParseValue(string s, out bool isPercent)
        {
            isPercent = false;
            if (string.IsNullOrWhiteSpace(s)) return 0f;
            s = s.Trim();
            if (s.EndsWith("%"))
            {
                isPercent = true;
                s = s.Substring(0, s.Length - 1).Trim();
                return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float p) ? p / 100f : 0f;
            }
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;
        }

        static string Field(string[] v, int i) => (i < v.Length && v[i] != null) ? v[i].Trim() : "";
    }
}
