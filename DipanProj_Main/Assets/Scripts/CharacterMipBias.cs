using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【過渡期功能】角色取樣密度對齊背景（mipMapBias），2026-09-03。
///
/// ── 要解決什麼 ──
/// 整張背景底圖目前全是 1448×1086（ChatGPT 輸出尺寸）拉伸貼齊地圖：標準 18×10 房只有 80 px/格、
/// 血狂之爭 45~63 px/格；1080p 跟隨模式一格是 108 螢幕 px ⇒ **背景被放大 1.35~2.4 倍顯示、變軟**。
/// 角色序列圖約 124 px/格、地上物 250~500 px/格，都是縮小顯示、銳利。
/// 「銳利的角色貼在一張軟掉的背景上」就是貼紙感的主因之一，任何色彩處理都救不了它
/// （量化過程見 readme/PROGRESS.md 2026-09-03、readme/PROBLEMS.md **E29**）。
///
/// ── 做法 ──
/// 正解是把背景解析度提上來（建議尺寸見 readme/PERF_QUALITY_AUDIT.md §素材尺寸規範「整張背景圖」）。
/// 在那之前，先讓角色的取樣密度**降到跟背景一樣**：對角色貼圖設
///     mipMapBias = log2( 角色貼圖密度 ÷ 背景密度 )
/// 密度單位都是「貼圖像素 / 世界單位」——角色 = PPU ÷ lossyScale，背景 = 貼圖寬 ÷ 地圖世界寬。
/// 兩者的比值與相機、螢幕解析度、整圖/跟隨模式全都無關，**每張地圖一個常數**（大廳約 +1.0、標準房約 +0.6）。
/// GPU 因此改抽較低一階的 mipmap，角色與背景在螢幕上一樣「軟」。永遠只往軟的方向（bias ≥ 0），不做銳化。
///
/// ── 怎麼關 ──
/// 背景解析度提上來之後把 <see cref="DefaultEnabled"/> 改 false（或乾脆刪本檔與三處掛點），bias 全歸 0
/// ＝逐位元回到加這功能之前。遊戲中 PerfHud（P → M）可即時開關 A/B 比對。
///
/// ── 掛點 ──
/// ‧ MapLoader.BuildBackground → <see cref="SetSceneDensity"/>；Teardown → <see cref="ClearSceneDensity"/>。
/// ‧ PlayerAnimator.Setup / MonsterAnimator.Setup → <see cref="Register"/>（把該角色所有幀的貼圖登記進來）。
/// ⚠ 貼圖是跨角色共用的（MapSpriteLoader 快取），同一張貼圖若被兩隻不同 PPU 的角色用到，後登記者的密度生效；
///   實務上同名角色 tileSize 相同、差異可忽略。
/// </summary>
public static class CharacterMipBias
{
    /// <summary>總開關預設值。背景解析度提上來之後改 false（PerfHud 的 M 鍵只改執行期的 <see cref="Enabled"/>）。</summary>
    public const bool DefaultEnabled = true;

    /// <summary>執行期開關（PerfHud M 鍵切換）。改它請走 <see cref="SetEnabled"/>，否則已登記的貼圖不會重套。</summary>
    public static bool Enabled { get; private set; } = DefaultEnabled;

    /// <summary>目前場景背景的貼圖密度（像素/世界單位）；0 = 這張圖沒有背景圖，bias 全歸 0。</summary>
    public static float SceneDensity { get; private set; }

    struct Entry
    {
        public Texture2D tex;
        public float ppu;        // 該 sprite 的 pixelsPerUnit（= 256 / tileSize）
        public Transform owner;  // 讀 lossyScale 用（怪物 CSV Scale、主角 0.8 縮放）；銷毀後這筆自動剔除
    }
    static readonly List<Entry> _entries = new List<Entry>();

    /// <summary>進 Play 時清掉（已關 Domain Reload；由 PlayModeStaticReset 呼叫）。</summary>
    public static void ResetForPlayMode()
    {
        _entries.Clear();
        SceneDensity = 0f;
        Enabled = DefaultEnabled;
    }

    public static void SetEnabled(bool on)
    {
        if (Enabled == on) return;
        Enabled = on;
        ApplyAll();
    }

    /// <summary>切換開關並回傳新狀態（給 PerfHud 按鈕用）。</summary>
    public static bool Toggle() { SetEnabled(!Enabled); return Enabled; }

    /// <summary>
    /// 由 MapLoader 在建背景時呼叫：<paramref name="textureWidthPx"/> = 背景貼圖寬、
    /// <paramref name="worldWidth"/> = 地圖世界寬（格數 × tileSize）。
    /// </summary>
    public static void SetSceneDensity(float textureWidthPx, float worldWidth)
    {
        SceneDensity = (textureWidthPx > 0f && worldWidth > 0.0001f) ? textureWidthPx / worldWidth : 0f;
        ApplyAll();
    }

    /// <summary>拆圖時呼叫：沒背景 ⇒ bias 全歸 0。</summary>
    public static void ClearSceneDensity() => SetSceneDensity(0f, 0f);

    /// <summary>
    /// 登記一組幀（PlayerAnimator / MonsterAnimator 在 Setup 時呼叫，每個動作各叫一次）。
    /// 同一張貼圖重複登記會覆蓋成最新的 PPU / owner。登記後立刻套用目前場景的 bias。
    /// </summary>
    public static void Register(Sprite[] frames, Transform owner)
    {
        if (frames == null) return;
        for (int i = 0; i < frames.Length; i++)
        {
            var sp = frames[i];
            if (sp == null || sp.texture == null) continue;
            var e = new Entry { tex = sp.texture, ppu = sp.pixelsPerUnit, owner = owner };
            int at = IndexOf(e.tex);
            if (at >= 0) _entries[at] = e; else _entries.Add(e);
            Apply(e);
        }
    }

    /// <summary>目前這個角色會拿到的 bias（給 PerfHud 顯示用）。</summary>
    public static float BiasFor(float ppu, float lossyScale)
    {
        if (!Enabled || SceneDensity <= 0f || ppu <= 0f) return 0f;
        float charDensity = ppu / Mathf.Max(0.0001f, Mathf.Abs(lossyScale));
        return Mathf.Max(0f, Mathf.Log(charDensity / SceneDensity, 2f));
    }

    static int IndexOf(Texture2D tex)
    {
        for (int i = 0; i < _entries.Count; i++) if (_entries[i].tex == tex) return i;
        return -1;
    }

    static void ApplyAll()
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            if (e.tex == null || e.owner == null) { _entries.RemoveAt(i); continue; }   // 貼圖或角色已銷毀
            Apply(e);
        }
    }

    static void Apply(Entry e)
    {
        if (e.tex == null) return;
        float scale = e.owner != null ? e.owner.lossyScale.x : 1f;
        e.tex.mipMapBias = BiasFor(e.ppu, scale);
    }
}
