using System.Collections.Generic;
using UnityEngine;
using Dipan.Data;

namespace Dipan.Vfx
{
    /// <summary>
    /// GroundFireTable 的一列：一種「地上燃燒」長什麼樣（2026-11-18，DipanVfx；說明見 readme/DIPAN_VFX.md §4）。
    /// GroundEffectTable 的 <c>FireStyleID</c> 指向這裡。範圍（半徑）與燒多久由 GroundEffect 那一列決定，這裡只管外觀。
    /// 所有數值都是「留空＝預設」。
    /// </summary>
    public sealed class GroundFireData
    {
        public int ID;
        public string Name;

        public int FlameID;                  // 火舌用 FlameTable 哪一列（0＝不長火舌，只有焦土）
        public FlameData Flame => FlameDatabase.Get(FlameID);

        public Sprite Scorch;   public string ScorchRaw = "";    // 焦土地面圖（RGB＋alpha；留空＝不畫）
        public Sprite Glow;     public string GlowRaw = "";      // 裂紋餘燼遮罩（alpha；留空＝沒有餘燼）
        public float ScorchSize = 1.25f;     // 焦土直徑＝範圍直徑 × 這個倍率（火會燒到範圍邊上，焦痕要稍大一點）
        public Color GlowColor = new Color(1f, 0.45f, 0.1f);
        public float GlowIntensity = 2f;
        public float ScorchLinger = 0.8f;    // 火滅後焦痕再留幾秒才淡掉

        public int CountMin = 5, CountMax = 8;   // 同時燒著的火舌數（範圍半徑 1 時；範圍越大按面積多長）
        public float FlameSize = 0.9f;       // 火舌高度（世界單位；中心的大、外圈的小）
        public float Fill = 0.8f;            // 火舌散布到範圍半徑的多少（0～1）
        public float TongueLife = 1.2f;      // 每條火舌燒多久就換到別處重燃（0＝整段時間都同一條）
        public float StaggerTime = 0.25f;    // 起火時各條火舌錯開冒出的最大延遲（秒）
        public float IgniteTime = 0.3f;      // 焦土從中心燒開的時間（秒）
        public float FadeTime = 0.5f;        // 結束時火舌熄滅、餘燼冷卻的時間（秒；落在燒的時間最後這段）

        public float EmberRate = 6f;         // 每秒冒幾顆火星（範圍半徑 1 時；按面積放大）
        public float Dim = 1f;               // 整體壓暗（1＝原樣）
        public string Note = "";
    }

    /// <summary>GroundFireTable 載入與查詢。主遊戲由 VfxManager 載入；地圖編輯器的特效預覽器直接讀 DipanProj_Main 的 CSV。</summary>
    public static class GroundFireDatabase
    {
        public const string TableName = "GroundFireTable";

        static readonly Dictionary<int, GroundFireData> _rows = new Dictionary<int, GroundFireData>();
        static readonly List<GroundFireData> _ordered = new List<GroundFireData>();
        static readonly HashSet<int> _warnedMissing = new HashSet<int>();

        public static bool Loaded { get; private set; }
        public static IReadOnlyList<GroundFireData> All => _ordered;

        public static void ResetForPlayMode() { _rows.Clear(); _ordered.Clear(); _warnedMissing.Clear(); Loaded = false; }

        public static GroundFireData Get(int id)
        {
            if (id <= 0) return null;
            if (_rows.TryGetValue(id, out var d)) return d;
            if (_warnedMissing.Add(id))
                Debug.LogWarning($"[GroundFire] GroundFireTable 沒有 ID {id}（或還沒載入）。主遊戲：VfxManager 的 Ground Fire CSV 欄要拖 Assets/Data/GroundFireTable.csv。");
            return null;
        }

        public static void LoadFromText(string text)
        {
            _rows.Clear();
            _ordered.Clear();
            _warnedMissing.Clear();
            var table = CsvTable.Parse(text ?? "", TableName);
            table.Require("ID", "Name");
            foreach (var err in table.Errors) Debug.LogError(err);
            foreach (var row in table.Rows)
            {
                var d = FromRow(row);
                if (d == null) continue;
                _rows[d.ID] = d;
                _ordered.Add(d);
            }
            Loaded = true;
        }

        public static GroundFireData FromRow(CsvRow row)
        {
            int id = row.GetInt("ID", 0);
            if (id <= 0) return null;
            var d = new GroundFireData { ID = id, Name = row.Get("Name", $"#{id}") };
            d.FlameID = row.GetInt("FlameID", 0);
            d.ScorchRaw = row.Get("ScorchSprite", "");
            d.Scorch = LoadOptional(d.ScorchRaw, d.Name);
            d.GlowRaw = row.Get("GlowSprite", "");
            d.Glow = LoadOptional(d.GlowRaw, d.Name);
            d.ScorchSize = Mathf.Max(0.05f, row.GetFloat("ScorchSize", d.ScorchSize));
            d.GlowColor = VfxAssets.ParseColor(row.Get("GlowColor", ""), d.GlowColor);
            d.GlowIntensity = Mathf.Max(0f, row.GetFloat("GlowIntensity", d.GlowIntensity));
            d.ScorchLinger = Mathf.Max(0f, row.GetFloat("ScorchLinger", d.ScorchLinger));
            d.CountMin = Mathf.Max(0, row.GetInt("CountMin", d.CountMin));
            d.CountMax = Mathf.Max(d.CountMin, row.GetInt("CountMax", d.CountMax));
            d.FlameSize = Mathf.Max(0.01f, row.GetFloat("FlameSize", d.FlameSize));
            d.Fill = Mathf.Clamp01(row.GetFloat("Fill", d.Fill));
            d.TongueLife = Mathf.Max(0f, row.GetFloat("TongueLife", d.TongueLife));
            d.StaggerTime = Mathf.Max(0f, row.GetFloat("StaggerTime", d.StaggerTime));
            d.IgniteTime = Mathf.Max(0.01f, row.GetFloat("IgniteTime", d.IgniteTime));
            d.FadeTime = Mathf.Max(0.05f, row.GetFloat("FadeTime", d.FadeTime));
            d.EmberRate = Mathf.Max(0f, row.GetFloat("EmberRate", d.EmberRate));
            d.Dim = Mathf.Max(0f, row.GetFloat("Dim", d.Dim));
            d.Note = row.Get("Note", "");
            return d;
        }

        static Sprite LoadOptional(string path, string owner)
        {
            path = (path ?? "").Trim();
            if (path.Length == 0) return null;
            var sp = VfxAssets.LoadSprite(path);
            if (sp == null) Debug.LogWarning($"[GroundFire] {owner}：找不到圖 Resources/{path}");
            return sp;
        }
    }
}
