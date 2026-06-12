using UnityEngine;
using Sorrows.Ballistics;

/// <summary>
/// 雷射「外型」的定義庫：把 WeaponTable 的 BeamStyle 編號 → 一組 shader 參數(BeamStyle)，
/// 以及 BeamColor 編號 → 顏色。使用者只在 CSV 填編號，實際數字集中在這裡。
///
/// 新增第 11 種雷射：在 Get() 多一個 case（複製最接近的那組改數字）即可，不需要產圖、不動其他程式。
/// 每個參數的視覺意義見 BeamStyle 結構註解與 README「雷射外型系統」。
/// </summary>
public static class BeamStyleLibrary
{
    /// <summary>BeamStyle 編號 → 參數組。未知編號退回 2（標準雷射）。</summary>
    public static BeamStyle Get(int id)
    {
        switch (id)
        {
            // 編號 : 名稱         I     edge  coreW coreWhite flow bandF bandD bandSharp noise nSpd flick fSpd
            case 1:  return Make(1.10f, 0.62f, 0.50f, 0.15f,  0.0f, 0.0f, 0.00f,  1.0f,   0.0f, 0.0f, 0.00f,  0f);   // 鏡光（古鏡）：平滑均勻、無波無脈動
            case 2:  return Make(1.30f, 0.50f, 0.40f, 0.70f,  1.5f, 0.8f, 0.45f,  2.5f,   0.0f, 0.0f, 0.07f, 11f);   // 標準雷射：能量波＋白核＋輕脈動
            case 3:  return Make(1.40f, 0.50f, 0.35f, 0.80f,  3.0f, 0.6f, 0.80f,  6.0f,   0.0f, 0.0f, 0.18f, 16f);   // 脈衝砲：能量包、強脈動快流動
            case 4:  return Make(1.60f, 0.72f, 0.60f, 0.95f,  0.5f, 0.3f, 0.12f,  1.5f,   0.0f, 0.0f, 0.04f,  9f);   // 離子/實心：極亮實心、近白
            case 5:  return Make(1.30f, 0.45f, 0.45f, 0.50f,  2.0f, 0.5f, 0.50f,  2.0f,   0.5f, 3.0f, 0.10f, 14f);   // 電漿：紊流雜訊churn
            case 6:  return Make(1.30f, 0.55f, 0.40f, 0.60f,  2.5f, 0.7f, 1.00f, 16.0f,   0.0f, 0.0f, 0.05f, 10f);   // 虛線/節段：跑馬燈
            case 7:  return Make(1.40f, 0.50f, 0.35f, 0.85f,  4.0f, 1.0f, 0.60f,  3.0f,   0.8f, 8.0f, 0.30f, 22f);   // 閃電/不穩：高雜訊強閃
            case 8:  return Make(1.50f, 0.22f, 0.18f, 0.90f,  1.0f, 0.5f, 0.30f,  2.0f,   0.0f, 0.0f, 0.05f, 10f);   // 針狀/狙擊：細亮核＋寬柔暈
            case 9:  return Make(1.35f, 0.60f, 0.50f, 0.60f,  0.8f, 0.35f,0.60f,  3.0f,   0.15f,1.5f, 0.12f,  6f);   // 能量洪流：厚大慢波
            case 10: return Make(0.85f, 0.35f, 0.50f, 0.30f,  0.6f, 0.4f, 0.25f,  1.5f,   0.0f, 0.0f, 0.05f,  8f);   // 微光/柔：低調柔和
            default: return Get(2);
        }
    }

    private static BeamStyle Make(float intensity, float edge, float coreW, float coreWhite,
                                  float flow, float bandFreq, float bandDepth, float bandSharp,
                                  float noise, float noiseSpd, float flick, float flickSpd)
    {
        return new BeamStyle
        {
            Intensity = intensity,
            EdgeStart = edge,
            CoreWidth = coreW,
            CoreWhiteness = coreWhite,
            FlowSpeed = flow,
            BandFreq = bandFreq,
            BandDepth = bandDepth,
            BandSharp = bandSharp,
            NoiseAmt = noise,
            NoiseSpeed = noiseSpd,
            FlickerStrength = flick,
            FlickerSpeed = flickSpd,
        };
    }

    // ── 顏色調色盤（編號 1~10；index 0 留空讓編號對齊）。新增顏色 = 加一筆 hex。──
    private static readonly string[] Palette =
    {
        null,        // 0（不使用）
        "FF2A2A",    // 1 紅
        "FF7A18",    // 2 橙
        "FFD21E",    // 3 黃
        "33FF55",    // 4 綠
        "14FFFF",    // 5 青
        "2E6BFF",    // 6 藍
        "9A42FF",    // 7 紫
        "FF3FB6",    // 8 洋紅
        "FFFFFF",    // 9 白
        "FFB347",    // 10 琥珀金
    };

    /// <summary>BeamColor 編號 → 顏色。未知編號退回白色。</summary>
    public static Color GetColor(int id)
    {
        if (id >= 1 && id < Palette.Length && Palette[id] != null
            && ColorUtility.TryParseHtmlString("#" + Palette[id], out Color c))
            return c;
        return Color.white;
    }
}
