using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Dipan.Data;

namespace Dipan.Vfx
{
    /// <summary>靈獸的混合方式（2026-11-19）。</summary>
    public enum SpiritBlend
    {
        /// <summary>發光：純加色（電龍這類光做的生物；圖＝純黑底轉 alpha）。</summary>
        Additive = 0,
        /// <summary>遮擋：會蓋住地板的實體（黑龍這類暗色生物；圖＝白底反推 alpha）。發光部位另用 HeadGlow、GlowColor。</summary>
        Alpha = 1,
    }

    /// <summary>
    /// SpiritFxTable 的一列：一種「靈獸」長什麼樣、怎麼游（2026-11-19，DipanVfx；說明見 readme/DIPAN_VFX.md §4e）。
    /// 武器表 <c>SpiritFxID</c> 指向這裡。靈獸＝釋放後在畫面裡亂游的長條生物（電龍、之後的火虎、冰鳳…），
    /// 由「頭圖＋沿身體平鋪的身體貼圖＋掛在身上的爪圖＋纏身電弧＋尾巴電弧」組成。
    /// 游速、存活秒數、追蹤、一次幾隻、傷害節拍在配方（RecipeTable）；這裡只管長相與游法的手感。
    ///
    /// 所有長度都是世界單位，再乘呼叫端給的 scale（武器 BulletScale）。所有欄位留空＝預設。
    /// </summary>
    public sealed class SpiritFxData
    {
        public int ID;
        public string Name;
        public SpiritBlend Blend = SpiritBlend.Additive;

        // ── 頭 ──
        public Sprite Head; public string HeadPath = "";
        public float HeadWidth = 3.6f;        // 頭圖畫多寬（世界單位，含往後飄的鬃毛）
        public Vector2 HeadAnchor = new Vector2(0.58f, 0.55f);   // 頭圖上「接脖子」的點（從左、從上量的比例）
        public float HeadMaxTilt = 50f;       // 側面頭最多傾斜幾度（往正上／正下游時頭不跟著轉到垂直）
        public float HeadIntensity = 1.15f;
        public Sprite HeadGlow; public string HeadGlowPath = "";   // 疊在頭上的發光層（加色；跟頭圖同尺寸同裁切，例：黑龍的紅眼＋外緣紫光）
        public float HeadGlowIntensity = 1.3f;
        public float HeadGlowPulse = 0.25f;   // 發光層脈動幅度
        public float HeadGlowPulseSpeed = 7f;  // 弧度/秒

        // ── 身體 ──
        public Texture2D Body; public string BodyPath = "";
        public float BodyWidth = 1.25f;       // 頸部可見寬（世界單位）；也是傷害判定的粗細
        public float BodyLength = 7.5f;       // 身體長（不含頭）
        public float TaperStart = 0.5f;       // 從身長幾成開始收細
        public float TailWidth = 0.15f;       // 尾端寬＝頸寬 × 這個
        public float TailFade = 0.75f;        // 從身長幾成開始淡出（散成電弧）
        public float TileScale = 1f;          // 身體貼圖一節多長＝頸寬 × 貼圖寬高比 × 這個
        public float BodyIntensity = 1.15f;
        public Color GlowColor = new Color(0.31f, 0.66f, 1f);
        public float GlowAmount = 0.3f;       // 身體底下一層柔光（0＝沒有）
        public float GlowWidth = 1.5f;        // 柔光延伸到幾倍半寬（遮擋模式＝貼著身體外緣的一圈輪廓光）

        // ── 黑氣（遮擋模式最有用）──
        public float SmokeAmount = 0f;        // 身體外緣往尾巴流的黑氣濃度（0＝沒有）
        public Color SmokeColor = new Color(0.04f, 0.025f, 0.06f, 1f);
        public float SmokeSpeed = 1.6f;       // 黑氣往尾巴流的速度
        public float PuffRate = 0f;           // 身後留下的煙團：每秒幾團（0＝沒有）
        public float PuffSize = 1.2f;         // 煙團剛冒出時的直徑（×該處半寬）
        public float PuffGrow = 1.5f;         // 消散前長到幾倍（再加上 PuffSize）
        public float PuffLife = 0.9f;         // 煙團活幾秒（±30%）
        public float PuffAlpha = 0.7f;        // 煙團最濃時的不透明度
        public float PuffRise = 0.35f;        // 煙團往上飄的速度（世界單位/秒）

        // ── 爪 ──
        public Sprite Claw; public string ClawPath = "";
        public float ClawWidth = 0.62f;       // 爪圖寬＝頸寬 × 這個
        public Vector2 ClawAnchor = new Vector2(0.12f, 0.12f);   // 爪圖上「接身體」的點（從左、從上）
        public float[] ClawAt = { 0.16f, 0.52f }; public string ClawAtRaw = "";   // 掛在身長幾成處（每處一對）
        public float ClawSwing = 14f;         // 划動幅度（度）
        public float ClawSwingSpeed = 6f;     // 划動速度（弧度/秒）
        public float ClawFarDim = 0.55f;      // 遠側那隻的亮度

        // ── 游法 ──
        public float WaveAmp = 0.28f;         // 身體左右擺動幅度（世界單位）
        public float WaveLength = 2.6f;       // 擺動波長
        public float WaveSpeed = 7f;          // 擺動往尾巴傳的速度（弧度/秒）
        public float Wander = 1f;             // 亂游轉彎程度（0＝直直游）
        public float EdgeMargin = 2.2f;       // 離畫面邊緣多近就開始往中間轉

        // ── 纏身電弧、尾巴電弧（同色：共用一個材質）──
        public int ArcCount = 3;
        public Color ArcColor = new Color(0.56f, 0.82f, 1f);
        public float ArcIntensity = 1.1f;
        public float ArcWidth = 0.03f;        // 世界單位
        public float ArcLength = 1.4f;
        public float ArcTwist = 0.6f;         // 每世界單位繞幾圈
        public float ArcRate = 24f;           // 每秒重抽幾次鋸齒
        public int TailArcs = 3;
        public float TailArcLength = 0.9f;
        public float TailArcIntensity = 0.9f;

        public float FadeIn = 0.3f;
        public float FadeOut = 0.5f;
        public int SortingOrder = 21900;      // 畫在地上物與角色之上（光束 22000 再上面）
        public float Dim = 1f;
        public string Note = "";

        public bool IsUsable => Head != null || Body != null;
    }

    /// <summary>SpiritFxTable 載入與查詢。主遊戲由 VfxManager 載入；地圖編輯器的特效預覽器直接讀 DipanProj_Main 的 CSV。</summary>
    public static class SpiritFxDatabase
    {
        public const string TableName = "SpiritFxTable";

        static readonly Dictionary<int, SpiritFxData> _rows = new Dictionary<int, SpiritFxData>();
        static readonly HashSet<int> _warnedMissing = new HashSet<int>();

        public static void ResetForPlayMode() { _rows.Clear(); _warnedMissing.Clear(); }

        public static SpiritFxData Get(int id)
        {
            if (id <= 0) return null;
            if (_rows.TryGetValue(id, out var d)) return d;
            if (_warnedMissing.Add(id))
                Debug.LogWarning($"[SpiritFx] SpiritFxTable 沒有 ID {id}（或還沒載入）。主遊戲：VfxManager 的 Spirit Fx CSV 欄要拖 Assets/Data/SpiritFxTable.csv。");
            return null;
        }

        static Vector2 ParseVec2(string s, Vector2 fallback)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) return fallback;
            var p = s.Split('|');
            if (p.Length == 2
                && float.TryParse(p[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                && float.TryParse(p[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                return new Vector2(x, y);
            Debug.LogWarning($"[SpiritFx] 「{s}」看不懂（要 x|y），用預設。");
            return fallback;
        }

        public static void LoadFromText(string text)
        {
            _rows.Clear();
            _warnedMissing.Clear();
            var table = CsvTable.Parse(text ?? "", TableName);
            table.Require("ID", "Name", "Head", "Body");
            foreach (var err in table.Errors) Debug.LogError(err);
            foreach (var row in table.Rows)
            {
                int id = row.GetInt("ID", 0);
                if (id <= 0) continue;
                var d = new SpiritFxData { ID = id, Name = row.Get("Name", $"#{id}") };
                string blend = row.Get("Blend", "").Trim();
                if (blend.Length > 0)
                {
                    if (blend.Equals("Alpha", System.StringComparison.OrdinalIgnoreCase) || blend == "1" || blend == "遮擋") d.Blend = SpiritBlend.Alpha;
                    else if (blend.Equals("Additive", System.StringComparison.OrdinalIgnoreCase) || blend.Equals("Add", System.StringComparison.OrdinalIgnoreCase) || blend == "0" || blend == "發光") d.Blend = SpiritBlend.Additive;
                    else Debug.LogWarning($"[SpiritFx] {d.Name}：Blend「{blend}」看不懂（Additive／Alpha），用 Additive。");
                }

                d.HeadPath = row.Get("Head", "").Trim();
                d.Head = VfxAssets.LoadSprite(d.HeadPath);
                if (d.HeadPath.Length > 0 && d.Head == null) Debug.LogWarning($"[SpiritFx] {d.Name}：找不到頭圖 Resources/{d.HeadPath}");
                d.HeadWidth = Mathf.Max(0.05f, row.GetFloat("HeadWidth", d.HeadWidth));
                d.HeadAnchor = ParseVec2(row.Get("HeadAnchor", ""), d.HeadAnchor);
                d.HeadMaxTilt = Mathf.Clamp(row.GetFloat("HeadMaxTilt", d.HeadMaxTilt), 0f, 90f);
                d.HeadIntensity = Mathf.Max(0f, row.GetFloat("HeadIntensity", d.HeadIntensity));
                d.HeadGlowPath = row.Get("HeadGlow", "").Trim();
                d.HeadGlow = VfxAssets.LoadSprite(d.HeadGlowPath);
                if (d.HeadGlowPath.Length > 0 && d.HeadGlow == null) Debug.LogWarning($"[SpiritFx] {d.Name}：找不到頭部發光層 Resources/{d.HeadGlowPath}");
                d.HeadGlowIntensity = Mathf.Max(0f, row.GetFloat("HeadGlowIntensity", d.HeadGlowIntensity));
                d.HeadGlowPulse = Mathf.Clamp01(row.GetFloat("HeadGlowPulse", d.HeadGlowPulse));
                d.HeadGlowPulseSpeed = row.GetFloat("HeadGlowPulseSpeed", d.HeadGlowPulseSpeed);

                d.BodyPath = row.Get("Body", "").Trim();
                d.Body = VfxAssets.LoadTexture(d.BodyPath);
                if (d.BodyPath.Length > 0 && d.Body == null) Debug.LogWarning($"[SpiritFx] {d.Name}：找不到身體貼圖 Resources/{d.BodyPath}（要匯入成 Default 貼圖、Wrap U＝Repeat）");
                d.BodyWidth = Mathf.Max(0.05f, row.GetFloat("BodyWidth", d.BodyWidth));
                d.BodyLength = Mathf.Max(0.2f, row.GetFloat("BodyLength", d.BodyLength));
                d.TaperStart = Mathf.Clamp01(row.GetFloat("TaperStart", d.TaperStart));
                d.TailWidth = Mathf.Clamp(row.GetFloat("TailWidth", d.TailWidth), 0.01f, 1f);
                d.TailFade = Mathf.Clamp01(row.GetFloat("TailFade", d.TailFade));
                d.TileScale = Mathf.Max(0.05f, row.GetFloat("TileScale", d.TileScale));
                d.BodyIntensity = Mathf.Max(0f, row.GetFloat("BodyIntensity", d.BodyIntensity));
                d.GlowColor = VfxAssets.ParseColor(row.Get("GlowColor", ""), d.GlowColor);
                d.GlowAmount = Mathf.Max(0f, row.GetFloat("GlowAmount", d.GlowAmount));
                d.GlowWidth = Mathf.Clamp(row.GetFloat("GlowWidth", d.GlowWidth), 1f, 4f);
                d.SmokeAmount = Mathf.Clamp01(row.GetFloat("SmokeAmount", d.SmokeAmount));
                d.SmokeColor = VfxAssets.ParseColor(row.Get("SmokeColor", ""), d.SmokeColor);
                d.SmokeSpeed = row.GetFloat("SmokeSpeed", d.SmokeSpeed);
                d.PuffRate = Mathf.Clamp(row.GetFloat("PuffRate", d.PuffRate), 0f, 200f);
                d.PuffSize = Mathf.Max(0.05f, row.GetFloat("PuffSize", d.PuffSize));
                d.PuffGrow = Mathf.Max(0f, row.GetFloat("PuffGrow", d.PuffGrow));
                d.PuffLife = Mathf.Max(0.05f, row.GetFloat("PuffLife", d.PuffLife));
                d.PuffAlpha = Mathf.Clamp01(row.GetFloat("PuffAlpha", d.PuffAlpha));
                d.PuffRise = row.GetFloat("PuffRise", d.PuffRise);

                d.ClawPath = row.Get("Claw", "").Trim();
                d.Claw = VfxAssets.LoadSprite(d.ClawPath);
                if (d.ClawPath.Length > 0 && d.Claw == null) Debug.LogWarning($"[SpiritFx] {d.Name}：找不到爪圖 Resources/{d.ClawPath}");
                d.ClawWidth = Mathf.Max(0.01f, row.GetFloat("ClawWidth", d.ClawWidth));
                d.ClawAnchor = ParseVec2(row.Get("ClawAnchor", ""), d.ClawAnchor);
                d.ClawAtRaw = row.Get("ClawAt", "").Trim();
                if (d.ClawAtRaw.Length > 0)
                {
                    var list = new List<float>();
                    foreach (var part in d.ClawAtRaw.Split('|'))
                        if (float.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) list.Add(Mathf.Clamp01(f));
                    d.ClawAt = list.ToArray();
                }
                d.ClawSwing = row.GetFloat("ClawSwing", d.ClawSwing);
                d.ClawSwingSpeed = row.GetFloat("ClawSwingSpeed", d.ClawSwingSpeed);
                d.ClawFarDim = Mathf.Clamp01(row.GetFloat("ClawFarDim", d.ClawFarDim));

                d.WaveAmp = Mathf.Max(0f, row.GetFloat("WaveAmp", d.WaveAmp));
                d.WaveLength = Mathf.Max(0.1f, row.GetFloat("WaveLength", d.WaveLength));
                d.WaveSpeed = row.GetFloat("WaveSpeed", d.WaveSpeed);
                d.Wander = Mathf.Max(0f, row.GetFloat("Wander", d.Wander));
                d.EdgeMargin = Mathf.Max(0f, row.GetFloat("EdgeMargin", d.EdgeMargin));

                d.ArcCount = Mathf.Clamp(row.GetInt("ArcCount", d.ArcCount), 0, 12);
                d.ArcColor = VfxAssets.ParseColor(row.Get("ArcColor", ""), d.ArcColor);
                d.ArcIntensity = Mathf.Max(0f, row.GetFloat("ArcIntensity", d.ArcIntensity));
                d.ArcWidth = Mathf.Max(0.005f, row.GetFloat("ArcWidth", d.ArcWidth));
                d.ArcLength = Mathf.Max(0.1f, row.GetFloat("ArcLength", d.ArcLength));
                d.ArcTwist = row.GetFloat("ArcTwist", d.ArcTwist);
                d.ArcRate = Mathf.Max(0f, row.GetFloat("ArcRate", d.ArcRate));
                d.TailArcs = Mathf.Clamp(row.GetInt("TailArcs", d.TailArcs), 0, 8);
                d.TailArcLength = Mathf.Max(0.05f, row.GetFloat("TailArcLength", d.TailArcLength));
                d.TailArcIntensity = Mathf.Max(0f, row.GetFloat("TailArcIntensity", d.TailArcIntensity));

                d.FadeIn = Mathf.Max(0.01f, row.GetFloat("FadeIn", d.FadeIn));
                d.FadeOut = Mathf.Max(0.01f, row.GetFloat("FadeOut", d.FadeOut));
                d.SortingOrder = row.GetInt("SortingOrder", d.SortingOrder);
                d.Dim = Mathf.Max(0f, row.GetFloat("Dim", d.Dim));
                d.Note = row.Get("Note", "");
                if (!d.IsUsable) Debug.LogWarning($"[SpiritFx] {d.Name}（ID {id}）頭圖與身體貼圖都沒有，只會剩電弧。");
                _rows[id] = d;
            }
        }
    }
}
