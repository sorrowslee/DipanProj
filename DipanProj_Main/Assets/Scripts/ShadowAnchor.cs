using System.Collections.Generic;
using UnityEngine;
using Dipan.Data;

/// <summary>
/// 角色影子錨點（2026-09-03）：每個角色、每個動作一組「影子該放哪」的像素值，資料驅動。
///
/// ── 為什麼需要這張表 ──
/// AutoSprite 產的序列圖**沒有把腳錨在畫布的固定點**：主角 idle 的 25 幀腳都在畫布中心左邊 25px，
/// walk 卻兩腳跨在中心兩側；狼人 idle 的腳底離畫布底 27px、walk 卻是 46px。<see cref="BlobShadow"/> 以前一律用
/// <c>transform.position</c>（＝畫布中心）當 X、用 idle 第 0 幀量一次當 Y，所以「idle 偏、走路剛好準」。
/// 純程式猜腳在哪已證明有反例（披風、長袍、爪子；readme/PROBLEMS.md **E28**），所以改成：
/// **程式先算出八成正確的預設值 → 存成表 → 看拼圖不對的角色手改、標 manual、之後永不覆寫**。
///
/// ── 三個組件 ──
/// ‧ <see cref="ShadowAnchorPx"/>：一組錨點（像素，畫布座標）。X 相對畫布中心（+右，**未翻面**的來源圖方向）、
///   Y 從畫布底往上、Width 影子寬。都是像素，換算成世界單位是 BlobShadow 的事（要看當前 sprite 的 PPU/pivot/縮放/flipX）。
/// ‧ <see cref="ShadowAnchorMath"/>：**唯一的一條計算路徑**——編輯器工具（Project Tools → 角色 → 計算影子錨點）
///   與 runtime 退路（表裡沒有這個角色時當場算）都呼叫它，兩邊結果字面相同（B9 的教訓：烘焙版與退路版不能各算各的）。
/// ‧ <see cref="ShadowAnchorTable"/>：`Assets/Data/ShadowAnchorTable.csv` 的載入與查詢。
///   Key = <c>Characters/&lt;血統&gt;/&lt;動作&gt;</c> 或 <c>Monsters/&lt;怪名&gt;/&lt;動作&gt;</c>（不分大小寫）。
///
/// 使用端：<see cref="IShadowAnchorSource"/> 由 PlayerAnimator／MonsterAnimator 實作（回傳當前動作的錨點），
/// BlobShadow 每幀問一次。文件：readme/SHADOW.md。
/// </summary>
public struct ShadowAnchorPx
{
    public bool ok;
    public float xFromCenterPx;   // 影子中心 X：相對畫布中心（+右；未翻面的來源圖方向）
    public float yFromBottomPx;   // 影子中心 Y：從畫布底往上（＝可見腳底）
    public float widthPx;         // 影子寬（像素；BlobShadow 再乘 WidthFactor）
    public int canvasW, canvasH;  // 算這組值時的畫布尺寸（換畫布尺寸時提醒重算）
    public int frames;            // 算時的幀數（換圖後幀數不同＝該重算）
    public string source;         // "auto" / "manual"
}

/// <summary>角色動畫器實作：回傳**目前動作**的影子錨點；沒有就回 false，BlobShadow 走舊的自動量測。</summary>
public interface IShadowAnchorSource
{
    bool TryGetShadowAnchor(out ShadowAnchorPx anchor);
}

/// <summary>
/// 錨點演算法（唯一路徑）。規則（2026-09-03 二版）：
/// ‧ 在「最底一帶」（可見高度最底 15%）找**腳**：把帶內有不透明像素的欄連成一段段（gap ≤ 2px 算同一段）。
///   3/4 俯視的站姿兩腳一近一遠、一低一高，會落成左右兩段——
///   ‧ ≥2 段：取**最低的兩段**當兩隻腳（披風、破布條也會垂進帶內，但它們的底比腳高），X ＝ 兩段中心的中點、
///     Y ＝ 兩段各自最低列的平均（＝兩腳之間的地面接觸點）。
///   ‧ 1 段（走路兩腿交叉、衣襬連住兩腿、或遠腳高過帶子）：X ＝ 整體可見框中心（身體壓在自己中心上）、
///     Y ＝ 該段最低列再往上抬可見高 × 6%（＝兩腳之間，與兩段時的高度一致；見 SingleRunLiftFraction）。
/// ‧ 每幀算完取**全幀中位數**：一個動作一組固定值，不逐幀——逐幀會讓影子跟著跨步左右滑，比偏一點更怪。
/// ‧ Width ＝ max(兩腳外緣跨距, 可見框寬) 的中位數（只用腳的跨距時瘦長角色會縮成一小點；框寬＝舊版的寬，作者看慣的大小）。
/// 一版只取最底 6% 的水平平均，結果是影子壓在**近腳**上（遠腳比近腳高、根本不在帶內），作者實機看主角 idle 明顯偏——
/// 影子該在兩腳之間、不是在最低的那隻腳下。已知反例（披風、長袍、爪子垂到腳邊）就是表存在的理由：算出來不對就手改。
/// </summary>
public static class ShadowAnchorMath
{
    public const byte AlphaThreshold = 10;          // 與 MapSpriteLoader.AlphaThreshold 同值（去背邊當透明）
    public const float BottomBandFraction = 0.15f;  // 「最底一帶」佔可見高度的比例（要涵蓋較高的那隻遠腳）
    public const int RunGapPx = 2;                  // 欄與欄之間空 ≤ 這麼多像素仍算同一段（去背邊的破洞）
    /// <summary>
    /// 只找到一段（兩腿被衣襬連成一片、或遠腳高過帶子）時，影子中心要從該段最低列往上抬「可見高 × 這個比例」。
    /// 3/4 俯視兩腳一近一遠，地面接觸點在兩腳之間、比近腳的鞋底高；兩段時是直接取兩腳底的平均（主角約 6%），
    /// 一段時量不到遠腳，就用同樣的量抬——不抬的話影子中心壓在近腳鞋底、半顆橢圓吊在角色下面，看起來偏低偏外（殭屍、毛殭實機）。
    /// </summary>
    public const float SingleRunLiftFraction = 0.06f;
    public const float MinWidthOfBox = 1.0f;        // 影子寬至少是可見框寬的這個比例（0.75 時殭屍系列、狼人這種瘦長角色影子縮成一小顆，作者回報後改 1.0＝與舊版同寬）

    /// <summary>單幀量測結果（像素）。</summary>
    public struct FrameStat { public bool ok; public float anchorX; public float bottomPx; public int widthPx; public int runs; public int minX, maxX, minY, maxY; }

    /// <summary>量一張圖（<paramref name="px"/> 為 Unity 的 GetPixels32 排列：原點左下、row-major）。</summary>
    public static FrameStat MeasureFrame(Color32[] px, int w, int h)
    {
        var r = new FrameStat { ok = false };
        if (px == null || w <= 0 || h <= 0 || px.Length < w * h) return r;
        int minX = w, maxX = -1, minY = h, maxY = -1;
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                if (px[row + x].a > AlphaThreshold)
                {
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
            }
        }
        if (maxX < 0) return r;   // 全透明

        int visH = maxY - minY + 1;
        int band = Mathf.Max(2, Mathf.RoundToInt(visH * BottomBandFraction));
        int bandTop = Mathf.Min(maxY, minY + band - 1);   // 原點在左下：minY 是最底列

        // 帶內每一欄：有沒有像素、最低列在哪
        var colHas = new bool[w];
        var colBottom = new int[w];
        for (int x = minX; x <= maxX; x++)
        {
            colBottom[x] = -1;
            for (int y = minY; y <= bandTop; y++)
                if (px[y * w + x].a > AlphaThreshold) { colHas[x] = true; colBottom[x] = y; break; }   // 由下往上第一個 = 最低列
        }

        // 連成段（gap ≤ RunGapPx 視為同段）
        var runStart = new List<int>(); var runEnd = new List<int>(); var runBottom = new List<int>();
        int start = -1, last = -1, gap = 0, curBottom = h;
        for (int x = minX; x <= maxX + 1; x++)
        {
            bool has = x <= maxX && colHas[x];
            if (has)
            {
                if (start < 0) { start = x; curBottom = h; }
                last = x; gap = 0;
                if (colBottom[x] < curBottom) curBottom = colBottom[x];
            }
            else if (start >= 0)
            {
                gap++;
                if (gap > RunGapPx || x > maxX) { runStart.Add(start); runEnd.Add(last); runBottom.Add(curBottom); start = -1; }
            }
        }
        if (start >= 0) { runStart.Add(start); runEnd.Add(last); runBottom.Add(curBottom); }
        if (runStart.Count == 0) return r;

        r.ok = true;
        r.minX = minX; r.maxX = maxX; r.minY = minY; r.maxY = maxY;
        r.runs = runStart.Count;
        int boxW = maxX - minX + 1;
        if (runStart.Count >= 2)
        {
            // 取「最低的兩段」當兩隻腳（不是最左最右）：披風、破布條會在腳的兩側垂到帶內，
            // 但它們的底一定比腳高——覓血者 idle 一版取最左最右段，抓到兩條布條、影子整個往上飄（作者實機回報）。
            int a = -1, b = -1;
            for (int i = 0; i < runStart.Count; i++)
            {
                if (a < 0 || runBottom[i] < runBottom[a]) { b = a; a = i; }
                else if (b < 0 || runBottom[i] < runBottom[b]) b = i;
            }
            float ca = (runStart[a] + runEnd[a] + 1) * 0.5f, cb = (runStart[b] + runEnd[b] + 1) * 0.5f;
            r.anchorX = (ca + cb) * 0.5f;
            r.bottomPx = (runBottom[a] + runBottom[b]) * 0.5f;
            int spanL = Mathf.Min(runStart[a], runStart[b]), spanR = Mathf.Max(runEnd[a], runEnd[b]);
            r.widthPx = Mathf.Max(spanR - spanL + 1, Mathf.RoundToInt(boxW * MinWidthOfBox));
        }
        else
        {
            r.anchorX = (minX + maxX + 1) * 0.5f;   // 只找到一隻腳：身體壓在自己中心上
            r.bottomPx = runBottom[0] + visH * SingleRunLiftFraction;   // 往上抬到「兩腳之間」的高度（見 SingleRunLiftFraction）
            r.widthPx = Mathf.Max(runEnd[0] - runStart[0] + 1, Mathf.RoundToInt(boxW * MinWidthOfBox));
        }
        return r;
    }

    /// <summary>動作名是不是「躺著」的（dead）：影子改用整個剪影的中心，不找腳。</summary>
    public static bool IsLyingAction(string action)
        => !string.IsNullOrEmpty(action) && action.Trim().ToLowerInvariant() == "dead";

    /// <summary>躺姿只取最後這個比例的幀（dead 序列前段是倒下的過程、後段才是躺平定格，遊戲也是停在最後一幀）。</summary>
    public const float LyingTailFraction = 0.34f;

    /// <summary>
    /// 躺姿影子中心離剪影底緣多高（占剪影高度的比例）。0.5＝正中心——實機看起來影子「太往上」，因為上半被身體遮住、
    /// 只剩上緣露出來像飄在身後；放低到 0.25 讓影子從身體下緣露出來，才有壓在地上的感覺（作者實機回報後調的）。
    /// </summary>
    public const float LyingCenterFraction = 0.25f;

    /// <summary>
    /// 躺姿影子中心離底緣的高度上限：剪影寬 × 這個比例。躺平的剪影寬矮、用高度的 25% 剛好；
    /// 但跪坐／蜷曲那種**又高又窄**的死亡姿勢（該隱）用高度算會把影子推到半身高，改取兩者較小值——
    /// 影子橢圓高＝寬×0.5，中心在底緣往上「寬×0.15」等於露出下面約 1/3 圈，跟躺平那組看起來一致。
    /// </summary>
    public const float LyingCenterMaxOfWidth = 0.15f;

    /// <summary>
    /// 把一組幀的量測結果合成一組錨點（中位數）。
    /// <paramref name="lying"/>＝躺姿（dead）：整個剪影都貼在地上，沒有「腳」可找——X 用可見框中心、Y 在剪影底緣往上 LyingCenterFraction（0.25）的高度、寬＝可見框寬，
    /// 而且只取序列**最後 1/3** 的幀（前段是倒下過程，混進去會把影子拉到站姿與躺姿之間）。
    /// </summary>
    public static ShadowAnchorPx Combine(IList<FrameStat> stats, int canvasW, int canvasH, bool lying = false)
    {
        var a = new ShadowAnchorPx { ok = false, canvasW = canvasW, canvasH = canvasH, source = "auto" };
        if (stats == null || stats.Count == 0 || canvasW <= 0) return a;
        var xs = new List<float>(); var ys = new List<float>(); var ws = new List<float>();
        int from = lying ? Mathf.Max(0, stats.Count - Mathf.Max(1, Mathf.CeilToInt(stats.Count * LyingTailFraction))) : 0;
        for (int i = from; i < stats.Count; i++)
        {
            var s = stats[i];
            if (!s.ok) continue;
            if (lying)
            {
                xs.Add((s.minX + s.maxX + 1) * 0.5f - canvasW * 0.5f);
                ys.Add(s.minY + Mathf.Min((s.maxY - s.minY + 1) * LyingCenterFraction, (s.maxX - s.minX + 1) * LyingCenterMaxOfWidth));
                ws.Add(s.maxX - s.minX + 1);
                continue;
            }
            xs.Add(s.anchorX - canvasW * 0.5f);
            ys.Add(s.bottomPx);
            ws.Add(s.widthPx);
        }
        if (xs.Count == 0) return a;
        a.ok = true;
        a.frames = xs.Count;
        a.xFromCenterPx = Mathf.Round(Median(xs) * 10f) / 10f;
        a.yFromBottomPx = Mathf.Round(Median(ys) * 10f) / 10f;
        a.widthPx = Mathf.Round(Median(ws));
        return a;
    }

    /// <summary>從已載入的貼圖算（runtime 退路用；貼圖必須可讀＝MapSpriteLoader 的 LoadImage 貼圖）。</summary>
    public static ShadowAnchorPx ComputeFromTextures(IList<Texture2D> frames, bool lying = false)
    {
        var none = new ShadowAnchorPx { ok = false, source = "auto" };
        if (frames == null || frames.Count == 0) return none;
        var stats = new List<FrameStat>(frames.Count);
        int cw = 0, ch = 0;
        foreach (var t in frames)
        {
            if (t == null || !t.isReadable) continue;
            Color32[] px;
            try { px = t.GetPixels32(); } catch { continue; }
            if (cw == 0) { cw = t.width; ch = t.height; }
            stats.Add(MeasureFrame(px, t.width, t.height));
        }
        return Combine(stats, cw, ch, lying);
    }

    public static float Median(List<float> v)
    {
        if (v == null || v.Count == 0) return 0f;
        var a = new List<float>(v); a.Sort();
        int m = a.Count / 2;
        return (a.Count % 2 == 1) ? a[m] : 0.5f * (a[m - 1] + a[m]);
    }
}

/// <summary>
/// `Assets/Data/ShadowAnchorTable.csv` 的載入與查詢（懶漢單例；依表頭欄名取值）。
/// 來源：場景 GameManagers 上的 <see cref="ShadowAnchorTableProvider"/>（主）→ 編輯器下直接讀 Assets/Data 檔案（後備，只在 Editor）。
/// 表裡沒有的角色由 PlayerSpriteLibrary／MonsterSpriteLibrary 當場用 <see cref="ShadowAnchorMath"/> 算（同一條路徑）。
/// </summary>
public class ShadowAnchorTable
{
    public const string FileName = "ShadowAnchorTable.csv";
    public const string KindCharacters = "Characters";
    public const string KindMonsters = "Monsters";

    static ShadowAnchorTable _instance;
    public static ShadowAnchorTable Instance
    {
        get
        {
            if (_instance == null) { _instance = new ShadowAnchorTable(); _instance.Load(); }
            return _instance;
        }
    }

    /// <summary>進 Play 時丟掉單例（已關 Domain Reload；由 PlayModeStaticReset 呼叫）。</summary>
    public static void ResetForPlayMode() => _instance = null;

    readonly Dictionary<string, ShadowAnchorPx> _rows = new Dictionary<string, ShadowAnchorPx>();
    public int Count => _rows.Count;
    public bool Loaded { get; private set; }

    /// <summary>組 key：<c>Characters/base/idle</c>（小寫、去空白）。</summary>
    public static string MakeKey(string kind, string name, string state)
        => $"{(kind ?? "").Trim()}/{(name ?? "").Trim()}/{(state ?? "").Trim()}".ToLowerInvariant();

    public bool TryGet(string kind, string name, string state, out ShadowAnchorPx a)
        => _rows.TryGetValue(MakeKey(kind, name, state), out a);

    void Load()
    {
        _rows.Clear();
        string text = null;
        var provider = Object.FindObjectOfType<ShadowAnchorTableProvider>();
        if (provider != null && provider.shadowAnchorCSV != null) text = provider.shadowAnchorCSV.text;
#if UNITY_EDITOR
        if (text == null)
        {
            string p = System.IO.Path.Combine(Application.dataPath, "Data", FileName);
            if (System.IO.File.Exists(p))
            {
                text = System.IO.File.ReadAllText(p);
                Debug.LogWarning("[ShadowAnchorTable] 場景上沒有 ShadowAnchorTableProvider（或沒拖 CSV），編輯器下改直接讀 Assets/Data/" + FileName +
                                 "。打包前請在 GameManagers 掛 ShadowAnchorTableProvider 並把 CSV 拖進去，否則 build 裡影子會退回自動量測。");
            }
        }
#endif
        Loaded = text != null;
        if (text == null) return;   // 沒表：全部走自動計算（第一次跑工具前的正常狀態）

        var table = CsvTable.Parse(text, "ShadowAnchorTable");
        foreach (var e in table.Errors) Debug.LogWarning("[ShadowAnchorTable] " + e);
        foreach (var row in table.Rows)
        {
            string key = row.Get("Key");
            if (string.IsNullOrWhiteSpace(key)) continue;
            var a = new ShadowAnchorPx
            {
                ok = true,
                xFromCenterPx = row.GetFloat("AnchorX"),
                yFromBottomPx = row.GetFloat("AnchorY"),
                widthPx = row.GetFloat("WidthPx"),
                canvasW = row.GetInt("CanvasW"), canvasH = row.GetInt("CanvasH"),
                frames = row.GetInt("Frames"),
                source = row.Get("Source", "auto"),
            };
            _rows[key.Trim().ToLowerInvariant()] = a;
        }
        Debug.Log($"[ShadowAnchorTable] 載入 {_rows.Count} 組影子錨點。");
    }
}
