using UnityEngine;

/// <summary>
/// 角色環境融合（前身：2026-09-02 的 CharacterEnvPoc；2026-09-03 作者拍板轉正並改成**自動算**）。
///
/// ── 要解決什麼 ──
/// 全螢幕後處理對角色與場景一視同仁，永遠不會改變「角色暗部比場景暗多少」，而那個相對差就是
/// 「角色像貼在背景上」的來源之一（亮場景最明顯：大廳地板 linear 中位 0.36、角色暗部 0.02，差 18 倍）。
/// 只能在角色自己的 sprite 上動：<b>黑階抬升</b>（把角色最暗處抬到場景中位的 1/N）為主、環境色為輔。
/// 作者實機拍板（大廳、Atmosphere 17）：抬升量 = 場景中位 ÷ 14 剛好、×2 太過。
///
/// ── 為什麼自動算、不做每種氛圍一組參數 ──
/// 會隨場景變的只有兩個數：場景的 linear 中位亮度（決定抬多少）與場景暗部／亮部的顏色（決定抬成什麼色）。
/// 19 種氛圍 × 每張圖的底圖都不同，人工量會變成「每加一種氛圍就欠一次校準」。所以改成**進圖後量最終畫面**：
/// <see cref="AtmosphereBlit"/> 在後處理之後把整個畫面縮成 32×18 讀回 CPU（一張圖一次、AsyncGPUReadback 不卡幀），
/// 算中位亮度、最暗 15% 的平均色、最亮 15% 的平均色，餵進 <see cref="SetSceneStats"/>。
/// 這正是 PROBLEMS E26「量最終畫面、不要照 shader 常數推」的原則，只是把手動量截圖變成程式自己量。
/// 結果：大廳算出 0.026 左右（≈作者拍板的 0.031）、紅嫁衣暗房算出 0.001（≈關）——場景越亮抬越多、暗到像紅嫁衣就等於不抬。
///
/// ── 怎麼用 ──
/// 平常不用管：換圖自動重量。遊戲中 <b>P → G</b> 循環 0 原狀／1 自動／2 自動×2（A/B 對比用），面板顯示量到的場景數據。
/// 要手動覆寫某張圖：MapsTable 加欄 `CharLift`（尚未加；需要時再加，空＝自動——同 AtmoTint 慣例）。
///
/// ── 掛點 ──
/// ‧ <c>AtmosphereController.ApplyMapAtmosphere</c> → <see cref="OnAtmosphereChanged"/>（換圖排一次量測）。
/// ‧ <c>AtmosphereBlit.OnRenderImage</c> → <see cref="WantsProbe"/>／<see cref="ProbeFrom"/>（後處理之後抓畫面）。
/// ‧ <c>HitReactionHandler</c>：比對 <see cref="Version"/>，變了就把參數寫進角色的 MaterialPropertyBlock（與白閃同一個 block）。
/// ‧ shader：<c>Custom/SpriteFlash</c> 的 _Env* 參數，全部 0 時整段跳過＝逐位元等於沒這功能。
///
/// ── ⚠⚠ 所有數值都是 Linear 空間（PROBLEMS E11／E26）──
/// </summary>
public static class CharacterEnvFusion
{
    public enum Mode
    {
        Original = 0,   // 完全現狀（shader 整段跳過）
        Auto,           // 自動（依量到的場景數據）
        AutoStrong      // 自動 ×2（A/B 用：確認方向與有沒有在作用）
    }
    const int ModeCount = 3;

    public static Mode Current { get; private set; } = Mode.Auto;

    /// <summary>每次「參數會變」+1（切模式、量到新場景數據）。HitReactionHandler 比對後重寫 MPB。</summary>
    public static int Version { get; private set; } = 1;

    public static void Cycle() => SetMode((Mode)(((int)Current + 1) % ModeCount));

    public static void SetMode(Mode m)
    {
        if (Current == m) return;
        Current = m;
        Version++;
    }

    public static string ModeName()
    {
        switch (Current)
        {
            case Mode.Auto:       return "1 自動";
            case Mode.AutoStrong: return "2 自動×2";
            default:              return "0 原狀";
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  場景量測（由 AtmosphereBlit 餵）
    // ══════════════════════════════════════════════════════════════

    /// <summary>量到的場景 linear 中位亮度；0 = 還沒量（此時 shader 整段跳過）。</summary>
    public static float SceneLuma { get; private set; }
    /// <summary>最暗 15% 像素的平均色（歸一化：三通道平均 = 1）。</summary>
    public static Vector4 SceneDarkTint { get; private set; } = Vector4.one;
    /// <summary>最亮 15% 像素的平均色（歸一化）。</summary>
    public static Vector4 SceneLitTint { get; private set; } = Vector4.one;
    /// <summary>量測次數（面板顯示用）。</summary>
    public static int ProbeCount { get; private set; }

    /// <summary>換圖後等這麼多幀再量：讓地圖建完、相機到位、載入頁（UI 層，不在相機畫面裡）期間的半成品畫面不算數。</summary>
    public const int ProbeDelayFrames = 20;
    /// <summary>量兩次：第一次在 ProbeDelayFrames、第二次再過這麼多幀（地圖分幀載入、光源淡入都到位後）。之後不再量，除非再換圖。</summary>
    public const int ProbeSecondPassFrames = 90;

    static int _probeAt = -1;        // 排程：Time.frameCount 到這個值就量；-1 = 沒排
    static bool _secondPassPending;

    /// <summary>換圖時由 AtmosphereController 呼叫：排一次量測（不改模式）。</summary>
    public static void OnAtmosphereChanged(int atmosphereType)
    {
        RequestProbe();
    }

    /// <summary>排一次量測（PerfHud 也可手動叫）。</summary>
    public static void RequestProbe()
    {
        _probeAt = Time.frameCount + ProbeDelayFrames;
        _secondPassPending = true;
    }

    /// <summary>這一幀要不要抓畫面（AtmosphereBlit 每幀問，只是一個 int 比較）。</summary>
    public static bool WantsProbe => _probeAt >= 0 && Time.frameCount >= _probeAt;

    /// <summary>
    /// 由 AtmosphereBlit 在後處理**之後**呼叫：把最終畫面縮到 32×18 讀回來算統計。
    /// 用 AsyncGPUReadback（不 stall GPU），下一兩幀回呼才寫進 SceneLuma。
    /// </summary>
    public static void ProbeFrom(RenderTexture finalImage)
    {
        _probeAt = -1;
        if (finalImage == null) return;

        const int W = 32, H = 18;
        // 逐級 2x 縮（同 bloom 的理由：一步跳太多倍會 aliasing；這裡只是統計、但便宜就順手做對）。
        RenderTexture cur = finalImage;
        var temps = new System.Collections.Generic.List<RenderTexture>();
        while (cur.width / 2 >= W * 2 && cur.height / 2 >= H * 2)
        {
            var t = RenderTexture.GetTemporary(cur.width / 2, cur.height / 2, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            t.filterMode = FilterMode.Bilinear;
            Graphics.Blit(cur, t);
            temps.Add(t); cur = t;
        }
        var tiny = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        tiny.filterMode = FilterMode.Bilinear;
        Graphics.Blit(cur, tiny);
        foreach (var t in temps) RenderTexture.ReleaseTemporary(t);

        UnityEngine.Rendering.AsyncGPUReadback.Request(tiny, 0, TextureFormat.RGBAHalf, req =>
        {
            RenderTexture.ReleaseTemporary(tiny);
            if (req.hasError) { Debug.LogWarning("[CharacterEnvFusion] 場景量測讀回失敗（AsyncGPUReadback error）。"); return; }
            var data = req.GetData<ushort>();
            Accumulate(data, W * H);
        });

        if (_secondPassPending) { _secondPassPending = false; _probeAt = Time.frameCount + ProbeSecondPassFrames; }
    }

    // 從 RGBAHalf 資料算統計（linear 值）
    static void Accumulate(Unity.Collections.NativeArray<ushort> px, int n)
    {
        if (px.Length < n * 4) return;
        var lum = new float[n];
        var rgb = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float r = Mathf.HalfToFloat(px[i * 4]), g = Mathf.HalfToFloat(px[i * 4 + 1]), b = Mathf.HalfToFloat(px[i * 4 + 2]);
            rgb[i] = new Vector3(r, g, b);
            lum[i] = 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        System.Array.Sort(order, (a, b) => lum[a].CompareTo(lum[b]));

        int k = Mathf.Max(1, n * 15 / 100);
        Vector3 dark = Vector3.zero, lit = Vector3.zero;
        for (int i = 0; i < k; i++) { dark += rgb[order[i]]; lit += rgb[order[n - 1 - i]]; }
        dark /= k; lit /= k;

        SceneLuma = Mathf.Max(0f, lum[order[n / 2]]);
        SceneDarkTint = Normalize(dark);
        SceneLitTint = Normalize(lit);
        ProbeCount++;
        Version++;
    }

    static Vector4 Normalize(Vector3 c)
    {
        float m = (c.x + c.y + c.z) / 3f;
        if (m <= 1e-5f) return Vector4.one;
        return new Vector4(c.x / m, c.y / m, c.z / m, 1f);
    }

    // ══════════════════════════════════════════════════════════════
    //  參數（不隨場景變的常數）
    // ══════════════════════════════════════════════════════════════

    /// <summary>目標落差倍數：角色最暗處至少抬到場景中位的 1/N。作者實機拍板 14（大廳 ×1 剛好、×2 太過）。</summary>
    public const float TargetDarkRatio = 14f;
    /// <summary>抬升量上限（linear），保險：超亮場景不要把角色黑衣服洗成灰。</summary>
    public const float MaxBlackLift = 0.06f;
    /// <summary>環境色強度（角色暗側／亮側各乘場景暗部／亮部的歸一色）。場景後處理已對整個畫面套過一次色，這裡是再補一層，所以要輕。</summary>
    const float EnvMix = 0.35f;
    /// <summary>角色**自己貼圖**亮度上「翻成亮側」的分界與過渡帶半寬（linear；角色空間、不是場景空間）。</summary>
    const float EnvPivot = 0.140f, EnvSplit = 0.050f;
    const float SatDelta = -0.04f;
    const float LumBoost = 0.10f;

    static float Strength => Current == Mode.AutoStrong ? 2f : 1f;

    /// <summary>目前實際抬升量（linear）。面板顯示用。</summary>
    public static float BlackLift => Mathf.Min(MaxBlackLift, SceneLuma / TargetDarkRatio) * Strength;

    /// <summary>色彩處理有沒有在作用（模式非原狀、且已量到場景）。</summary>
    public static bool ColorEnabled => Current != Mode.Original && SceneLuma > 0f;

    /// <summary>
    /// 把參數寫進 <paramref name="mpb"/>。⚠ 呼叫端必須與自己的其他屬性（白閃）寫在同一個 block：SetPropertyBlock 是整包覆蓋的。
    /// </summary>
    public static void FillPropertyBlock(MaterialPropertyBlock mpb)
    {
        if (mpb == null) return;
        if (!ColorEnabled) { mpb.SetFloat("_EnvOn", 0f); return; }

        float k = Strength;
        mpb.SetFloat("_EnvOn", 1f);
        mpb.SetVector("_EnvBase", SceneDarkTint);     // 乘法係數不是顏色：走 SetVector（Linear 專案下 SetColor 會做 gamma→linear）
        mpb.SetVector("_EnvLit", SceneLitTint);
        mpb.SetFloat("_EnvPivot", EnvPivot);
        mpb.SetFloat("_EnvSplit", EnvSplit);
        mpb.SetFloat("_EnvMix", Mathf.Min(1f, EnvMix * k));
        mpb.SetFloat("_BlackLift", BlackLift);
        mpb.SetVector("_LiftTint", SceneDarkTint);    // 抬升量帶場景暗部的色，不然角色暗部會被拉得比場景冷（E26 那次量到的）
        mpb.SetFloat("_Sat", SatDelta * k);
        mpb.SetFloat("_LumBoost", LumBoost * k);
    }

    /// <summary>進 Play 時重置（已關 Domain Reload；由 PlayModeStaticReset 呼叫）。</summary>
    public static void ResetForPlayMode()
    {
        Current = Mode.Auto; Version = 1;
        SceneLuma = 0f; SceneDarkTint = Vector4.one; SceneLitTint = Vector4.one; ProbeCount = 0;
        _probeAt = -1; _secondPassPending = false;
    }
}
