using System.Collections.Generic;
using UnityEngine;
using Dipan.Data;

namespace Dipan.Vfx
{
    /// <summary>
    /// FlameTable 的一列：一種「會燒的火」長什麼樣、怎麼動（2026-11-18，見 readme/DIPAN_VFX.md §3）。
    /// 武器表 <c>FlameStyleID</c>（子彈換成一團火）與 GroundFireTable 的 <c>FlameID</c>（地上燃燒的火舌）都指向這裡。
    /// 所有數值都是「留空＝預設」，預設值寫在欄位初始值。
    /// </summary>
    public sealed class FlameData
    {
        public int ID;
        public string Name;

        /// <summary>火焰圖（白色＋alpha＝熱度；隨機挑）。pivot＝火的根部。</summary>
        public Sprite[] Sprites;
        public string SpritesRaw = "";

        public float Size = 1f;              // 火的高度（世界單位；子彈會再乘呼叫端給的倍率）
        public float SizeJitter = 0.1f;      // 每團大小隨機 ±比例
        public float Anchor = 0f;            // 掛點在火的哪裡：0＝根部、0.5＝火的中間（子彈用 0.4 左右，火才會「包著」子彈）

        public Color ColorCore = new Color(1f, 0.95f, 0.75f);
        public Color ColorMid = new Color(1f, 0.55f, 0.12f);
        public Color ColorEdge = new Color(0.7f, 0.12f, 0.04f);
        public float Intensity = 1.35f;      // 亮度
        public float Additive = 0.55f;       // 0＝不透明火、1＝純發光

        public float ScrollSpeed = 1.4f;     // 往上捲動速度
        public float Distort = 0.1f;         // 扭曲強度
        public float Erode = 0.45f;          // 侵蝕（火舌斷裂）強度
        public float NoiseScale = 1.6f;      // 噪聲尺度（越大火舌越碎）
        public float Flicker = 0.08f;        // 大小忽大忽小的幅度

        public float LeanPerSpeed = 0.04f;   // 每 1 單位/秒的橫向速度倒多少（UV）
        public float LeanMax = 0.22f;        // 最多倒多少（UV；太大會被圖邊切掉）
        public float StretchPerSpeed = 0.03f;// 每 1 單位/秒的速度拉長多少
        public float StretchMax = 0.5f;      // 最多拉長多少（0.5＝1.5 倍高）

        public float IgniteTime = 0.15f;     // 從根部長出來的時間（秒）
        public float FadeTime = 0.25f;       // 熄滅時間（秒）

        public float TrailRate = 0f;         // 移動時每秒甩出幾團小火（0＝不甩）
        public float TrailLife = 0.35f;      // 甩出的小火燒多久（秒）
        public float TrailSize = 0.5f;       // 甩出的小火大小（相對本體）

        public float EmberRate = 0f;         // 每秒冒幾顆火星（0＝不冒）
        public Color EmberColor = new Color(1f, 0.7f, 0.25f);
        public float EmberSize = 0.06f;      // 火星大小（世界單位）

        public float Dim = 1f;               // 整體壓暗（1＝原樣）
        public int SortingOffset = 1;        // 掛在子彈上時排在子彈前面幾層
        public string Note = "";

        public bool HasSprites => Sprites != null && Sprites.Length > 0;
    }

    /// <summary>FlameTable 載入與查詢。主遊戲由 VfxManager 載入；地圖編輯器的特效預覽器直接讀 DipanProj_Main 的 CSV。</summary>
    public static class FlameDatabase
    {
        public const string TableName = "FlameTable";

        static readonly Dictionary<int, FlameData> _rows = new Dictionary<int, FlameData>();
        static readonly List<FlameData> _ordered = new List<FlameData>();
        static readonly HashSet<int> _warnedMissing = new HashSet<int>();

        public static bool Loaded { get; private set; }
        public static IReadOnlyList<FlameData> All => _ordered;

        public static void ResetForPlayMode() { _rows.Clear(); _ordered.Clear(); _warnedMissing.Clear(); Loaded = false; }

        public static FlameData Get(int id)
        {
            if (id <= 0) return null;
            if (_rows.TryGetValue(id, out var d)) return d;
            if (_warnedMissing.Add(id))
                Debug.LogWarning($"[Flame] FlameTable 沒有 ID {id}（或還沒載入）。主遊戲：VfxManager 的 Flame CSV 欄要拖 Assets/Data/FlameTable.csv。");
            return null;
        }

        public static void LoadFromText(string text)
        {
            _rows.Clear();
            _ordered.Clear();
            _warnedMissing.Clear();
            var table = CsvTable.Parse(text ?? "", TableName);
            table.Require("ID", "Name", "Sprites");
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

        public static FlameData FromRow(CsvRow row)
        {
            int id = row.GetInt("ID", 0);
            if (id <= 0) return null;
            var d = new FlameData { ID = id, Name = row.Get("Name", $"#{id}") };

            d.SpritesRaw = row.Get("Sprites", "");
            var list = new List<Sprite>();
            foreach (var raw in d.SpritesRaw.Split('|'))
            {
                string p = raw.Trim();
                if (p.Length == 0) continue;
                var sp = VfxAssets.LoadSprite(p);
                if (sp == null) { Debug.LogWarning($"[Flame] {d.Name}：找不到火焰圖 Resources/{p}"); continue; }
                list.Add(sp);
            }
            d.Sprites = list.ToArray();
            if (!d.HasSprites) Debug.LogWarning($"[Flame] {d.Name}（ID {id}）沒有任何可用的火焰圖，這列不會畫出東西。");

            d.Size = Mathf.Max(0.01f, row.GetFloat("Size", d.Size));
            d.SizeJitter = Mathf.Clamp01(row.GetFloat("SizeJitter", d.SizeJitter));
            d.Anchor = Mathf.Clamp01(row.GetFloat("Anchor", d.Anchor));
            d.ColorCore = VfxAssets.ParseColor(row.Get("ColorCore", ""), d.ColorCore);
            d.ColorMid = VfxAssets.ParseColor(row.Get("ColorMid", ""), d.ColorMid);
            d.ColorEdge = VfxAssets.ParseColor(row.Get("ColorEdge", ""), d.ColorEdge);
            d.Intensity = Mathf.Max(0f, row.GetFloat("Intensity", d.Intensity));
            d.Additive = Mathf.Clamp01(row.GetFloat("Additive", d.Additive));
            d.ScrollSpeed = row.GetFloat("ScrollSpeed", d.ScrollSpeed);
            d.Distort = Mathf.Max(0f, row.GetFloat("Distort", d.Distort));
            d.Erode = Mathf.Max(0f, row.GetFloat("Erode", d.Erode));
            d.NoiseScale = Mathf.Max(0.05f, row.GetFloat("NoiseScale", d.NoiseScale));
            d.Flicker = Mathf.Clamp01(row.GetFloat("Flicker", d.Flicker));
            d.LeanPerSpeed = Mathf.Max(0f, row.GetFloat("LeanPerSpeed", d.LeanPerSpeed));
            d.LeanMax = Mathf.Clamp(row.GetFloat("LeanMax", d.LeanMax), 0f, 0.45f);
            d.StretchPerSpeed = Mathf.Max(0f, row.GetFloat("StretchPerSpeed", d.StretchPerSpeed));
            d.StretchMax = Mathf.Max(0f, row.GetFloat("StretchMax", d.StretchMax));
            d.IgniteTime = Mathf.Max(0f, row.GetFloat("IgniteTime", d.IgniteTime));
            d.FadeTime = Mathf.Max(0.01f, row.GetFloat("FadeTime", d.FadeTime));
            d.TrailRate = Mathf.Max(0f, row.GetFloat("TrailRate", d.TrailRate));
            d.TrailLife = Mathf.Max(0.05f, row.GetFloat("TrailLife", d.TrailLife));
            d.TrailSize = Mathf.Max(0.05f, row.GetFloat("TrailSize", d.TrailSize));
            d.EmberRate = Mathf.Max(0f, row.GetFloat("EmberRate", d.EmberRate));
            d.EmberColor = VfxAssets.ParseColor(row.Get("EmberColor", ""), d.EmberColor);
            d.EmberSize = Mathf.Max(0.005f, row.GetFloat("EmberSize", d.EmberSize));
            d.Dim = Mathf.Max(0f, row.GetFloat("Dim", d.Dim));
            d.SortingOffset = row.GetInt("SortingOffset", d.SortingOffset);
            d.Note = row.Get("Note", "");
            return d;
        }
    }
}
