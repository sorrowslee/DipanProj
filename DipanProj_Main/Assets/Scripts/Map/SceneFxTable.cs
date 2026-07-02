using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 場景特效外觀表（Resources/Data/SceneFxTable.csv）。fxId → 一組 <see cref="SceneFxEmitter.Look"/>（顏色/密度/大小/壽命/濃度…）。
/// 懶漢載入一次、快取。加新特效種類 = CSV 加一列，不動程式。
///
/// 欄位：Id,Name,R,G,B,EmitPerSecond,LifeMin,LifeMax,SizeStart,SizeEnd,PeakAlpha,Turbulence,SortingOrder
/// </summary>
public static class SceneFxTable
{
    static Dictionary<int, SceneFxEmitter.Look> _byId;

    public static SceneFxEmitter.Look Get(int fxId)
    {
        EnsureLoaded();
        if (_byId.TryGetValue(fxId, out var look)) return look;
        // 找不到就退回第一列或內建煙霧預設。
        if (_byId.Count > 0) foreach (var v in _byId.Values) return v;
        return Default();
    }

    static SceneFxEmitter.Look Default() => new SceneFxEmitter.Look
    {
        color = new Color(0.06f, 0.05f, 0.05f, 1f),
        emitPerSecond = 30f, lifeMin = 1.6f, lifeMax = 2.6f,
        sizeStart = 0.7f, sizeEnd = 1.9f, peakAlpha = 0.85f, turbulence = 0.12f, sortingOrder = 25000,
    };

    static void EnsureLoaded()
    {
        if (_byId != null) return;
        _byId = new Dictionary<int, SceneFxEmitter.Look>();

        var ta = Resources.Load<TextAsset>("Data/SceneFxTable");
        if (ta == null || string.IsNullOrWhiteSpace(ta.text))
        {
            Debug.LogWarning("[SceneFxTable] 找不到 Resources/Data/SceneFxTable.csv，改用內建煙霧預設。");
            _byId[1] = Default();
            return;
        }

        var lines = ta.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)   // 跳過表頭
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            var v = line.Split(',');
            if (v.Length < 13) continue;
            if (!int.TryParse(v[0].Trim(), out int id)) continue;

            // 第 14 欄 Kind（選填、向下相容）：portal = 傳送門矩形；其餘/缺 = stream 弧線粒子流。
            int kind = (v.Length >= 14 && v[13].Trim().ToLowerInvariant() == "portal") ? 1 : 0;

            _byId[id] = new SceneFxEmitter.Look
            {
                color = new Color(F(v[2]), F(v[3]), F(v[4]), 1f),
                emitPerSecond = F(v[5], 20f),
                lifeMin = F(v[6], 1.5f),
                lifeMax = F(v[7], 2.5f),
                sizeStart = F(v[8], 0.6f),
                sizeEnd = F(v[9], 1.6f),
                peakAlpha = F(v[10], 0.8f),
                turbulence = F(v[11], 0.12f),
                sortingOrder = (int)F(v[12], 25000f),
                kind = kind,
            };
        }
        Debug.Log($"[SceneFxTable] 載入 {_byId.Count} 種場景特效。");
    }

    static float F(string s, float fallback = 0f)
        => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ? r : fallback;
}
