using System.Collections.Generic;
using UnityEngine;
using Dipan.Data;

/// <summary>
/// GroundSpikeTable 的一列：一種「從地底刺出來的尖刺」長什麼樣、怎麼動（2026-11-17，見 readme/GROUND_SPIKE.md）。
/// 武器表 <c>TrailSpikeID</c> 指向這裡；子彈每飛 TrailStep 就在腳下長一組（<see cref="GroundSpikeBurst"/>）。
/// 所有數值都是「留空＝預設」，預設值就寫在欄位初始值。
/// </summary>
public sealed class GroundSpikeData
{
    public int ID;
    public string Name;

    /// <summary>尖刺圖（隨機挑；一組裡盡量不重複）。圖的**底邊＝地平線**、尖端朝上、往右彎（往左飛時自動左右翻）。</summary>
    public Sprite[] Spikes;
    /// <summary>這組圖裡最高那張的世界高度（用來換算縮放：<see cref="Height"/> 指的是最高那根）。</summary>
    public float TallestSpriteHeight = 1f;

    public int CountMin = 2, CountMax = 3;   // 每個點長幾根（填 1/1 ＝ 單根）
    public float Height = 1.4f;              // 最高那根的高度（世界單位，還會乘武器的 CastVisualScale）
    public float HeightJitter = 0.2f;        // 每根高度隨機 ±比例
    public float Spread = 0.35f;             // 一叢裡各根離中心的散開距離（世界單位）
    public float LeanAngle = 12f;            // 往前進方向傾斜幾度（左右飛時最明顯；上下飛時自動變小）
    public float LeanJitter = 8f;            // 傾斜隨機 ±度
    public float StaggerTime = 0.06f;        // 一叢裡各根錯開冒出的最大延遲（秒）

    public float RiseTime = 0.08f;           // 從地底刺出到全長的時間（秒）
    public float Overshoot = 0.08f;          // 刺出瞬間縱向拉長多少再彈回（0.08 ＝ 拉長 8%）
    public float HoldTime = 0.25f;           // 全長後停留幾秒
    public bool RetractIntoGround = true;    // true＝縮回地底；false＝原地淡出（表格 RetractMode：Retract／Fade）
    public float RetractTime = 0.15f;        // 收尾時間（秒）

    public Sprite Crack;  public float CrackSize = 1.2f;    // 地面裂口（寬度，世界單位；留空圖＝不畫）
    public Sprite Dust;   public float DustSize = 1.1f;     // 破土揚塵（寬度；留空圖＝不畫）
    public float DustTime = 0.4f;                           // 揚塵散掉的時間（秒）

    public bool HasSortingOrder; public int SortingOrder;   // 留空＝用 VfxManager 的全域排序
    public float Dim = 1f;                                  // 整體壓暗（1＝原樣）

    /// <summary>一組從頭到尾要多久（給物件池判斷何時回收）。</summary>
    public float TotalTime => StaggerTime + RiseTime + HoldTime + RetractTime + 0.05f;
}

/// <summary>GroundSpikeTable 載入與查詢。由 <see cref="VfxManager"/> 在 Awake 時載入。</summary>
public static class GroundSpikeDatabase
{
    static readonly Dictionary<int, GroundSpikeData> _rows = new Dictionary<int, GroundSpikeData>();
    static readonly HashSet<int> _warnedMissing = new HashSet<int>();

    public static bool Loaded { get; private set; }

    /// <summary>關掉 Domain Reload 後 static 不歸零；進 Play 時由 PlayModeStaticReset 呼叫。</summary>
    public static void ResetForPlayMode() { _rows.Clear(); _warnedMissing.Clear(); Loaded = false; }

    public static GroundSpikeData Get(int id)
    {
        if (_rows.TryGetValue(id, out var d)) return d;
        if (_warnedMissing.Add(id))
            Debug.LogWarning($"[GroundSpike] GroundSpikeTable 沒有 ID {id}（或還沒載入）。請確認 VfxManager 的 Ground Spike CSV 欄有拖 Assets/Data/GroundSpikeTable.csv。");
        return null;
    }

    public static void LoadFromText(string text)
    {
        _rows.Clear();
        _warnedMissing.Clear();
        var table = CsvTable.Parse(text ?? "", "GroundSpikeTable");
        table.Require("ID", "Name", "Sprites");
        foreach (var err in table.Errors) Debug.LogError(err);

        foreach (var row in table.Rows)
        {
            int id = row.GetInt("ID", 0);
            if (id <= 0) continue;
            var d = new GroundSpikeData { ID = id, Name = row.Get("Name", $"#{id}") };

            // 尖刺圖：用 | 分隔的 Resources 路徑
            var list = new List<Sprite>();
            foreach (var raw in row.Get("Sprites", "").Split('|'))
            {
                string p = raw.Trim();
                if (p.Length == 0) continue;
                var sp = Resources.Load<Sprite>(p);
                if (sp == null) { Debug.LogWarning($"[GroundSpike] {d.Name}：找不到尖刺圖 Resources/{p}"); continue; }
                list.Add(sp);
            }
            d.Spikes = list.ToArray();
            if (d.Spikes.Length == 0) { Debug.LogWarning($"[GroundSpike] {d.Name}（ID {id}）沒有任何可用的尖刺圖，略過。"); continue; }
            float tallest = 0f;
            foreach (var sp in d.Spikes) tallest = Mathf.Max(tallest, sp.bounds.size.y);
            d.TallestSpriteHeight = Mathf.Max(0.0001f, tallest);

            d.CountMin = Mathf.Max(1, row.GetInt("CountMin", d.CountMin));
            d.CountMax = Mathf.Max(d.CountMin, row.GetInt("CountMax", d.CountMax));
            d.Height = row.GetFloat("Height", d.Height);
            d.HeightJitter = Mathf.Clamp01(row.GetFloat("HeightJitter", d.HeightJitter));
            d.Spread = Mathf.Max(0f, row.GetFloat("Spread", d.Spread));
            d.LeanAngle = row.GetFloat("LeanAngle", d.LeanAngle);
            d.LeanJitter = Mathf.Max(0f, row.GetFloat("LeanJitter", d.LeanJitter));
            d.StaggerTime = Mathf.Max(0f, row.GetFloat("StaggerTime", d.StaggerTime));
            d.RiseTime = Mathf.Max(0.01f, row.GetFloat("RiseTime", d.RiseTime));
            d.Overshoot = Mathf.Max(0f, row.GetFloat("Overshoot", d.Overshoot));
            d.HoldTime = Mathf.Max(0f, row.GetFloat("HoldTime", d.HoldTime));
            string mode = row.Get("RetractMode", "Retract").Trim();
            d.RetractIntoGround = !mode.Equals("Fade", System.StringComparison.OrdinalIgnoreCase) && mode != "淡出";
            d.RetractTime = Mathf.Max(0.01f, row.GetFloat("RetractTime", d.RetractTime));

            d.Crack = LoadOptional(row.Get("CrackSprite", ""), d.Name);
            d.CrackSize = row.GetFloat("CrackSize", d.CrackSize);
            d.Dust = LoadOptional(row.Get("DustSprite", ""), d.Name);
            d.DustSize = row.GetFloat("DustSize", d.DustSize);
            d.DustTime = Mathf.Max(0.05f, row.GetFloat("DustTime", d.DustTime));

            if (row.Has("SortingOrder")) { d.HasSortingOrder = true; d.SortingOrder = row.GetInt("SortingOrder", 0); }
            d.Dim = row.GetFloat("Dim", d.Dim);

            _rows[id] = d;
        }
        Loaded = true;
    }

    static Sprite LoadOptional(string path, string owner)
    {
        path = (path ?? "").Trim();
        if (path.Length == 0) return null;
        var sp = Resources.Load<Sprite>(path);
        if (sp == null) Debug.LogWarning($"[GroundSpike] {owner}：找不到圖 Resources/{path}");
        return sp;
    }
}
