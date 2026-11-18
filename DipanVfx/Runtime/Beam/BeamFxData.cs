using System.Collections.Generic;
using UnityEngine;
using Dipan.Data;

namespace Dipan.Vfx
{
    /// <summary>電弧的走法。</summary>
    public enum BeamArcMode
    {
        /// <summary>螺旋：一小段一小段的電弧繞著光柱轉、往前竄（氣功砲的「雷光環繞」）。</summary>
        Helix = 0,
        /// <summary>沿路徑：整條鋸齒電弧從頭連到尾、高速重抽形狀（電光／連鎖閃電）。</summary>
        Path = 1,
    }

    /// <summary>
    /// BeamFxTable 的一列：一種光束長什麼樣（2026-11-18，DipanVfx；說明見 readme/DIPAN_VFX.md §4d）。
    /// 武器表 <c>BeamFxID</c> 指向這裡。光束的路徑、命中寬度由遊戲端決定（雷射＝LaserBeam 的折線、
    /// 連鎖＝目標連線、掃射＝源點到落點），這裡只管「畫成什麼樣」。所有數值都是「留空＝預設」。
    ///
    /// 寬度單位：<b>可見寬</b>＝呼叫端給的寬度（＝武器 BeamWidth，也就是命中寬度）× <see cref="WidthMul"/>。
    /// 以下凡是寫「×半寬」的欄位都乘可見寬的一半。
    /// </summary>
    public sealed class BeamFxData
    {
        public int ID;
        public string Name;

        // ── 光柱本體 ──
        public float WidthMul = 1.2f;        // 可見寬＝命中寬 × 這個
        public float BodyIntensity = 1.2f;   // 光柱亮度；0＝不畫光柱（只剩電弧）
        public Color ColorCore = new Color(1f, 1f, 1f);
        public Color ColorMid = new Color(0.36f, 0.78f, 1f);
        public Color ColorEdge = new Color(0.1f, 0.35f, 1f);
        public float CoreWidth = 0.3f;       // 白熱核心寬（×半寬）
        public float Solid = 0.6f;           // 實心程度：0＝只有核心和外暈、1＝整條飽滿的光柱
        public float GlowWidth = 1.6f;       // 外暈延伸到幾倍半寬（≥1）
        public float FlowSpeed = 8f;         // 流紋往前推的速度（世界單位/秒）
        public float StreakAmt = 0.3f;       // 流紋明暗量（0＝均勻）
        public float PulseSpacing = 0f;      // 能量脈衝間距（世界單位；0＝沒有脈衝）
        public float PulseAmt = 0.15f;       // 脈衝經過時光柱鼓起多少（×半寬）
        public float PulseSpeed = 12f;       // 脈衝往前推的速度（世界單位/秒）
        public float EdgeNoise = 0.25f;      // 外鞘翻騰（0＝邊緣乾淨）
        public float Chroma = 0f;            // 邊緣七彩色散（鏡光）
        public float Shimmer = 0f;           // 沿光束滑過的亮紋（鏡光）
        public float ShimmerSpacing = 2.5f;  // 亮紋間距（世界單位）
        public float ShimmerSpeed = 6f;      // 亮紋滑動速度（世界單位/秒）
        public float Flicker = 0.08f;        // 整體閃爍幅度
        public float GrowSpeed = 0f;         // 剛發射時光束往前伸長的速度（世界單位/秒；0＝瞬間全長）

        // ── 電弧 ──
        public int ArcCount = 0;             // 幾條電弧（0＝沒有）
        public BeamArcMode ArcMode = BeamArcMode.Helix;
        public Color ArcColor = new Color(0.75f, 0.9f, 1f);
        public float ArcIntensity = 1.6f;
        public float ArcWidth = 0.12f;       // 電弧粗細（×半寬）
        public float ArcAmp = 1.1f;          // 螺旋＝繞光柱的半徑；沿路徑＝左右亂竄的幅度（×半寬）
        public float ArcJitter = 0.35f;      // 鋸齒程度（×ArcAmp）
        public float ArcSegment = 0.22f;     // 每一折的長度（世界單位）
        public float ArcRate = 24f;          // 每秒重抽幾次形狀
        public float ArcTwist = 0.7f;        // 螺旋：每世界單位繞幾圈
        public float ArcLength = 2.2f;       // 螺旋：每一小段電弧多長（世界單位）
        public float ArcTravel = 10f;        // 螺旋：電弧往前竄的速度（世界單位/秒）
        public float ArcLife = 0.22f;        // 螺旋：每一小段電弧活多久（秒）
        public float ArcBranch = 0f;         // 沿路徑：每個折點長出分岔的機率（0~1）

        // ── 砲口／命中光球、轉折光點、火花 ──
        public float MuzzleSize = 1.8f;      // 砲口光球直徑（×可見寬；0＝不畫）
        public float ImpactSize = 2.0f;      // 命中光球直徑（×可見寬；0＝不畫）
        public float JointSize = 0f;         // 急轉折（反彈點）光點直徑（×可見寬；0＝不畫）
        public int OrbRays = 6;              // 光球放射光芒數（0＝沒有）
        public float OrbStar = 0f;           // 十字星芒強度（鏡光）
        public float OrbPulse = 0.12f;       // 光球呼吸幅度
        public float SparkRate = 14f;        // 命中點每秒噴幾顆火花（0＝不噴）
        public Color SparkColor = new Color(0.7f, 0.9f, 1f);
        public float SparkSize = 0.06f;      // 火花大小（世界單位）
        public float SparkSpeed = 2.2f;      // 火花噴出速度

        public float FadeTime = 0.12f;       // 放開後淡出秒數
        public float Dim = 1f;               // 整體壓暗（1＝原樣）
        public string Note = "";

        public bool HasBody => BodyIntensity > 0.0001f;
        public bool HasArcs => ArcCount > 0 && ArcIntensity > 0.0001f;
    }

    /// <summary>BeamFxTable 載入與查詢。主遊戲由 VfxManager 載入；地圖編輯器的特效預覽器直接讀 DipanProj_Main 的 CSV。</summary>
    public static class BeamFxDatabase
    {
        public const string TableName = "BeamFxTable";

        /// <summary>武器沒填 BeamFxID 時用的光束（一般雷射）。</summary>
        public const int DefaultId = 2;

        static readonly Dictionary<int, BeamFxData> _rows = new Dictionary<int, BeamFxData>();
        static readonly HashSet<int> _warnedMissing = new HashSet<int>();
        static BeamFxData _fallback;

        public static void ResetForPlayMode() { _rows.Clear(); _warnedMissing.Clear(); }

        public static int Count => _rows.Count;

        /// <summary>取一列；找不到時回傳 <see cref="DefaultId"/> 那列，再不行就是全預設值（保證不回 null，光束一定畫得出來）。</summary>
        public static BeamFxData Get(int id)
        {
            if (id <= 0) id = DefaultId;
            if (_rows.TryGetValue(id, out var d)) return d;
            if (_warnedMissing.Add(id))
                Debug.LogWarning($"[BeamFx] BeamFxTable 沒有 ID {id}（或還沒載入）。主遊戲：VfxManager 的 Beam Fx CSV 欄要拖 Assets/Data/BeamFxTable.csv。");
            if (id != DefaultId && _rows.TryGetValue(DefaultId, out d)) return d;
            return _fallback ?? (_fallback = new BeamFxData { ID = 0, Name = "（預設）" });
        }

        public static bool Has(int id) => _rows.ContainsKey(id);

        public static void LoadFromText(string text)
        {
            _rows.Clear();
            _warnedMissing.Clear();
            var table = CsvTable.Parse(text ?? "", TableName);
            table.Require("ID", "Name");
            foreach (var err in table.Errors) Debug.LogError(err);
            foreach (var row in table.Rows)
            {
                int id = row.GetInt("ID", 0);
                if (id <= 0) continue;
                var d = new BeamFxData { ID = id, Name = row.Get("Name", $"#{id}") };

                d.WidthMul = Mathf.Max(0.05f, row.GetFloat("WidthMul", d.WidthMul));
                d.BodyIntensity = Mathf.Max(0f, row.GetFloat("BodyIntensity", d.BodyIntensity));
                d.ColorCore = VfxAssets.ParseColor(row.Get("ColorCore", ""), d.ColorCore);
                d.ColorMid = VfxAssets.ParseColor(row.Get("ColorMid", ""), d.ColorMid);
                d.ColorEdge = VfxAssets.ParseColor(row.Get("ColorEdge", ""), d.ColorEdge);
                d.CoreWidth = Mathf.Clamp(row.GetFloat("CoreWidth", d.CoreWidth), 0.02f, 1f);
                d.Solid = Mathf.Clamp01(row.GetFloat("Solid", d.Solid));
                d.GlowWidth = Mathf.Clamp(row.GetFloat("GlowWidth", d.GlowWidth), 1f, 4f);
                d.FlowSpeed = row.GetFloat("FlowSpeed", d.FlowSpeed);
                d.StreakAmt = Mathf.Clamp01(row.GetFloat("StreakAmt", d.StreakAmt));
                d.PulseSpacing = Mathf.Max(0f, row.GetFloat("PulseSpacing", d.PulseSpacing));
                d.PulseAmt = Mathf.Clamp(row.GetFloat("PulseAmt", d.PulseAmt), 0f, 1f);
                d.PulseSpeed = row.GetFloat("PulseSpeed", d.PulseSpeed);
                d.EdgeNoise = Mathf.Clamp01(row.GetFloat("EdgeNoise", d.EdgeNoise));
                d.Chroma = Mathf.Max(0f, row.GetFloat("Chroma", d.Chroma));
                d.Shimmer = Mathf.Max(0f, row.GetFloat("Shimmer", d.Shimmer));
                d.ShimmerSpacing = Mathf.Max(0.1f, row.GetFloat("ShimmerSpacing", d.ShimmerSpacing));
                d.ShimmerSpeed = row.GetFloat("ShimmerSpeed", d.ShimmerSpeed);
                d.Flicker = Mathf.Clamp01(row.GetFloat("Flicker", d.Flicker));
                d.GrowSpeed = Mathf.Max(0f, row.GetFloat("GrowSpeed", d.GrowSpeed));

                d.ArcCount = Mathf.Clamp(row.GetInt("ArcCount", d.ArcCount), 0, 12);
                string mode = row.Get("ArcMode", "").Trim();
                if (mode.Length > 0)
                {
                    if (mode == "1" || mode.Equals("Path", System.StringComparison.OrdinalIgnoreCase)) d.ArcMode = BeamArcMode.Path;
                    else if (mode == "0" || mode.Equals("Helix", System.StringComparison.OrdinalIgnoreCase)) d.ArcMode = BeamArcMode.Helix;
                    else Debug.LogWarning($"[BeamFx] {d.Name}：ArcMode「{mode}」看不懂（Helix / Path），用 Helix。");
                }
                d.ArcColor = VfxAssets.ParseColor(row.Get("ArcColor", ""), d.ArcColor);
                d.ArcIntensity = Mathf.Max(0f, row.GetFloat("ArcIntensity", d.ArcIntensity));
                d.ArcWidth = Mathf.Max(0.01f, row.GetFloat("ArcWidth", d.ArcWidth));
                d.ArcAmp = Mathf.Max(0f, row.GetFloat("ArcAmp", d.ArcAmp));
                d.ArcJitter = Mathf.Max(0f, row.GetFloat("ArcJitter", d.ArcJitter));
                d.ArcSegment = Mathf.Max(0.03f, row.GetFloat("ArcSegment", d.ArcSegment));
                d.ArcRate = Mathf.Max(0f, row.GetFloat("ArcRate", d.ArcRate));
                d.ArcTwist = row.GetFloat("ArcTwist", d.ArcTwist);
                d.ArcLength = Mathf.Max(0.1f, row.GetFloat("ArcLength", d.ArcLength));
                d.ArcTravel = row.GetFloat("ArcTravel", d.ArcTravel);
                d.ArcLife = Mathf.Max(0.02f, row.GetFloat("ArcLife", d.ArcLife));
                d.ArcBranch = Mathf.Clamp01(row.GetFloat("ArcBranch", d.ArcBranch));

                d.MuzzleSize = Mathf.Max(0f, row.GetFloat("MuzzleSize", d.MuzzleSize));
                d.ImpactSize = Mathf.Max(0f, row.GetFloat("ImpactSize", d.ImpactSize));
                d.JointSize = Mathf.Max(0f, row.GetFloat("JointSize", d.JointSize));
                d.OrbRays = Mathf.Clamp(row.GetInt("OrbRays", d.OrbRays), 0, 16);
                d.OrbStar = Mathf.Max(0f, row.GetFloat("OrbStar", d.OrbStar));
                d.OrbPulse = Mathf.Clamp01(row.GetFloat("OrbPulse", d.OrbPulse));
                d.SparkRate = Mathf.Max(0f, row.GetFloat("SparkRate", d.SparkRate));
                d.SparkColor = VfxAssets.ParseColor(row.Get("SparkColor", ""), d.SparkColor);
                d.SparkSize = Mathf.Max(0.005f, row.GetFloat("SparkSize", d.SparkSize));
                d.SparkSpeed = Mathf.Max(0f, row.GetFloat("SparkSpeed", d.SparkSpeed));

                d.FadeTime = Mathf.Max(0.01f, row.GetFloat("FadeTime", d.FadeTime));
                d.Dim = Mathf.Max(0f, row.GetFloat("Dim", d.Dim));
                d.Note = row.Get("Note", "");
                if (!d.HasBody && !d.HasArcs)
                    Debug.LogWarning($"[BeamFx] {d.Name}（ID {id}）BodyIntensity=0 又沒有電弧，只會剩光球。");
                _rows[id] = d;
            }
        }
    }
}
