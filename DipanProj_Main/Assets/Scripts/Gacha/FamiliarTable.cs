using System.Collections.Generic;
using UnityEngine;
using Dipan.Data;

/// <summary>隨從（<see cref="BloodlineFamiliar"/>）一種樣式的參數，一列 = FamiliarTable.csv 的一列。欄位說明見 CSV 檔頭註解。</summary>
public class FamiliarDef
{
    public string Key;
    public string Folder;          // SequenceImage 底下的相對路徑，例 Companions/MoziEagle
    public float Size = 0.18f;     // 停棲時可見高 ÷ 宿主角色高
    public float Fps = 12f;
    public float ScaleTakeoff = 1f, ScaleFly = 1f, ScaleGlide = 1f, ScaleLand = 1f;
    public Vector2 FootIdle;       // 來源圖像素：X 相對畫布中心、Y 從畫布底
    public Vector2[] FootTakeoff;  // 每幀一組
    public int TakeoffLift = 1;    // 第幾幀離開肩膀（0 起算，解析時已減 1）
    public int TakeoffEnd = -1;    // 播到第幾幀換 fly（0 起算；-1＝播完）
    public Vector2[] FootLand;
    public int LandTouch = 0;      // 第幾幀爪子碰到肩膀（0 起算）
    public float WaitMin = 6f, WaitMax = 10f;
    public float LapTime = 2.8f;
    public float LapRadiusX = 0.75f, LapRadiusY = 0.22f, LapHeight = 0.95f;
    public float GlideFrom = 0.3f, GlideTo = 0.65f;
    public float FlyAwayTime = 1.2f;
    public float PerchOffsetX, PerchOffsetY;
}

/// <summary>宿主某個動作的逐幀停棲點（來源圖像素，座標慣例同 <see cref="ShadowAnchorPx"/>）。</summary>
public class FamiliarAnchors
{
    public Vector2[] Points;
    public int CanvasW, CanvasH;
}

/// <summary>
/// `Assets/Data/FamiliarTable.csv` 與 `FamiliarAnchorTable.csv` 的載入與查詢（懶漢單例，2026-11-24）。
/// 來源：場景上的 <see cref="FamiliarTableProvider"/>（主）→ 編輯器下直接讀 Assets/Data（後備，只在 Editor）。
/// </summary>
public class FamiliarTable
{
    public const string FileName = "FamiliarTable.csv";
    public const string AnchorFileName = "FamiliarAnchorTable.csv";

    static FamiliarTable _instance;
    public static FamiliarTable Instance
    {
        get
        {
            if (_instance == null) { _instance = new FamiliarTable(); _instance.Load(); }
            return _instance;
        }
    }

    /// <summary>進 Play 時丟掉單例（已關 Domain Reload；由 PlayModeStaticReset 呼叫）。</summary>
    public static void ResetForPlayMode() => _instance = null;

    readonly Dictionary<string, FamiliarDef> _defs = new Dictionary<string, FamiliarDef>();
    readonly Dictionary<string, FamiliarAnchors> _anchors = new Dictionary<string, FamiliarAnchors>();

    public bool TryGetDef(string key, out FamiliarDef def)
        => _defs.TryGetValue((key ?? "").Trim().ToLowerInvariant(), out def);

    /// <summary>key = characters/&lt;SpriteFolder&gt;/&lt;動作&gt;（不分大小寫）。</summary>
    public bool TryGetAnchors(string spriteFolder, string state, out FamiliarAnchors a)
        => _anchors.TryGetValue($"characters/{(spriteFolder ?? "").Trim()}/{(state ?? "").Trim()}".ToLowerInvariant(), out a);

    static string ReadText(TextAsset asset, string fileName)
    {
        if (asset != null) return asset.text;
#if UNITY_EDITOR
        string p = System.IO.Path.Combine(Application.dataPath, "Data", fileName);
        if (System.IO.File.Exists(p))
        {
            Debug.LogWarning($"[FamiliarTable] 場景上沒有 FamiliarTableProvider（或沒拖 {fileName}），編輯器下改直接讀 Assets/Data/{fileName}。" +
                             "打包前請在 GameManagers 掛 FamiliarTableProvider 並把兩張 CSV 拖進去，否則 build 裡隨從不會出現。");
            return System.IO.File.ReadAllText(p);
        }
#endif
        return null;
    }

    void Load()
    {
        var provider = Object.FindObjectOfType<FamiliarTableProvider>();
        string defText = ReadText(provider != null ? provider.familiarCSV : null, FileName);
        string ancText = ReadText(provider != null ? provider.familiarAnchorCSV : null, AnchorFileName);

        if (defText != null)
        {
            var t = CsvTable.Parse(defText, "FamiliarTable");
            foreach (var e in t.Errors) Debug.LogWarning("[FamiliarTable] " + e);
            foreach (var r in t.Rows)
            {
                string key = r.Get("Key").Trim();
                if (key.Length == 0) continue;
                var d = new FamiliarDef
                {
                    Key = key,
                    Folder = r.Get("Folder").Trim(),
                    Size = r.GetFloat("Size", 0.18f),
                    Fps = r.GetFloat("Fps", 12f),
                    ScaleTakeoff = r.GetFloat("ScaleTakeoff", 1f),
                    ScaleFly = r.GetFloat("ScaleFly", 1f),
                    ScaleGlide = r.GetFloat("ScaleGlide", 1f),
                    ScaleLand = r.GetFloat("ScaleLand", 1f),
                    FootTakeoff = ParsePoints(r.Get("FootTakeoff"), key),
                    TakeoffLift = Mathf.Max(0, r.GetInt("TakeoffLift", 1) - 1),
                    TakeoffEnd = r.GetInt("TakeoffEnd", 0) - 1,
                    FootLand = ParsePoints(r.Get("FootLand"), key),
                    LandTouch = Mathf.Max(0, r.GetInt("LandTouch", 1) - 1),
                    WaitMin = r.GetFloat("WaitMin", 6f),
                    WaitMax = r.GetFloat("WaitMax", 10f),
                    LapTime = Mathf.Max(0.5f, r.GetFloat("LapTime", 2.8f)),
                    LapRadiusX = r.GetFloat("LapRadiusX", 0.75f),
                    LapRadiusY = r.GetFloat("LapRadiusY", 0.22f),
                    LapHeight = r.GetFloat("LapHeight", 0.95f),
                    GlideFrom = r.GetFloat("GlideFrom", 0.3f),
                    GlideTo = r.GetFloat("GlideTo", 0.65f),
                    FlyAwayTime = Mathf.Max(0.1f, r.GetFloat("FlyAwayTime", 1.2f)),
                    PerchOffsetX = r.GetFloat("PerchOffsetX", 0f),
                    PerchOffsetY = r.GetFloat("PerchOffsetY", 0f),
                };
                var fi = ParsePoints(r.Get("FootIdle"), key);
                d.FootIdle = fi != null && fi.Length > 0 ? fi[0] : Vector2.zero;
                if (d.Size <= 0.001f) d.Size = 0.18f;
                if (d.Fps <= 0.1f) d.Fps = 12f;
                _defs[key.ToLowerInvariant()] = d;
            }
        }

        if (ancText != null)
        {
            var t = CsvTable.Parse(ancText, "FamiliarAnchorTable");
            foreach (var e in t.Errors) Debug.LogWarning("[FamiliarAnchorTable] " + e);
            foreach (var r in t.Rows)
            {
                string key = r.Get("Key").Trim();
                if (key.Length == 0) continue;
                var pts = ParsePoints(r.Get("Points"), key);
                if (pts == null || pts.Length == 0) continue;
                _anchors[key.ToLowerInvariant()] = new FamiliarAnchors
                {
                    Points = pts,
                    CanvasW = r.GetInt("CanvasW", 256),
                    CanvasH = r.GetInt("CanvasH", 256),
                };
            }
        }
        Debug.Log($"[FamiliarTable] 載入 {_defs.Count} 種隨從、{_anchors.Count} 組停棲點。");
    }

    /// <summary>「x:y;x:y;…」→ 點陣列。空字串回 null；格式錯的那一組記警告、用 (0,0)。</summary>
    public static Vector2[] ParsePoints(string s, string owner)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var parts = s.Split(';');
        var list = new List<Vector2>(parts.Length);
        foreach (var p in parts)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            var xy = p.Split(':');
            float x = 0f, y = 0f;
            if (xy.Length != 2
                || !float.TryParse(xy[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x)
                || !float.TryParse(xy[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y))
                Debug.LogWarning($"[FamiliarTable] {owner} 的座標「{p}」看不懂（要寫成 X:Y），先當 0:0。");
            list.Add(new Vector2(x, y));
        }
        return list.ToArray();
    }
}
