using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Dipan.Data;

/// <summary>
/// 「計算影子錨點」（2026-09-03）：遞迴掃 <c>Assets/GameAssets/**/(Characters|Monsters)/SequenceImage/&lt;角色&gt;/&lt;動作&gt;/*.png</c>，
/// 對每個「角色/動作」用 <see cref="ShadowAnchorMath"/>（與 runtime 退路**同一條路徑**）算一組影子錨點，寫進
/// <c>Assets/Data/ShadowAnchorTable.csv</c>，並在專案根 <c>TempImage/ShadowAnchors/</c>（gitignored）輸出每個角色一張檢視用拼圖
/// —— 每列一個動作（順序 idle / walk / attack / dead / 其他）、每列取 4 幀，畫上影子與紅十字（錨點），不用進遊戲就能一眼看全部角色。
///
/// 規則：
/// ‧ **只算新的**：表裡已有的 key 一律不動（不管 auto/manual）；例外是 auto 列的幀數變了（＝換過圖）會重算並在 Note 註記。
/// ‧ **重算所有 auto**：auto 列全部重算，<b>Source=manual 的列永不覆寫</b>——看拼圖覺得哪個角色錯，直接改數字、把 Source 改成 manual 即可。
/// ‧ 新資料夾不必先 Sync Map Assets（這裡掃的是 GameAssets 原檔）；但進遊戲要看到圖仍然要 Sync。
/// 文件：readme/SHADOW.md。
/// </summary>
public static class ShadowAnchorTool
{
    const string MenuNew   = "Project Tools/角色/計算影子錨點（只算新的、出檢視圖）";
    const string MenuRedo  = "Project Tools/角色/重算所有 auto 影子錨點（manual 不動）";
    const string MenuSheet = "Project Tools/角色/影子錨點檢視圖（只出圖、不改表）";

    static readonly string[] Columns = { "Key", "AnchorX", "AnchorY", "WidthPx", "Source", "Frames", "CanvasW", "CanvasH", "Note" };
    static readonly string[] HeaderCells =
    {
        "Key(角色種類/角色/動作: Characters/<血統>/<動作> 或 Monsters/<怪名>/<動作>, 不分大小寫)",
        "AnchorX(影子中心X px/相對畫布中心/+右/未翻面方向)",
        "AnchorY(影子中心Y px/從畫布底往上/=可見腳底)",
        "WidthPx(影子寬 px/BlobShadow 再乘 WidthFactor)",
        "Source(auto=工具算的會被重算/manual=手改永不覆寫)",
        "Frames(算時幀數/幀數變了只算新的也會重算 auto 列)",
        "CanvasW(畫布寬 px)",
        "CanvasH(畫布高 px)",
        "Note(備註)",
    };
    static readonly string[] CommentLines =
    {
        "由 Project Tools → 角色 → 計算影子錨點 產生。看 TempImage/ShadowAnchors/ 的拼圖，錯的角色直接改 AnchorX/AnchorY/WidthPx、Source 改 manual。",
        "演算法（ShadowAnchorMath）：在最底 15% 帶內找腳（連成段）；兩段以上=取最低兩段當腳、中點/兩腳底平均、一段=可見框中心/該段底；全幀取中位數；Width=max(兩腳跨距, 框寬)。dead=躺姿：只取最後 1/3 幀、X/Y 用剪影中心、寬=剪影寬。披風/長袍/爪子垂到腳邊會算歪，這就是 manual 存在的理由。",
        "遊戲端：BlobShadow 依當前動作取這組像素，用當前 sprite 的 PPU/pivot/縮放/flipX 換算成世界位置；表裡沒有的角色 runtime 用同一條演算法當場算。",
    };

    static readonly string[] ActionOrder = { "idle", "walk", "attack", "dead" };

    [MenuItem(MenuNew, false, 61)]   static void RunNew()   => Run(onlyNew: true,  writeTable: true);
    [MenuItem(MenuRedo, false, 62)]  static void RunRedo()  => Run(onlyNew: false, writeTable: true);
    [MenuItem(MenuSheet, false, 63)] static void RunSheet() => Run(onlyNew: true,  writeTable: false);

    // ── 一個「角色/動作」資料夾 ──
    class Seq
    {
        public string kind, name, action, key;
        public List<string> files;
    }

    static string TablePath => Path.Combine(Application.dataPath, "Data", ShadowAnchorTable.FileName);
    static string SheetDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TempImage", "ShadowAnchors"));

    static void Run(bool onlyNew, bool writeTable)
    {
        try
        {
            var seqs = Scan();
            if (seqs.Count == 0) { Debug.LogWarning("[影子錨點] GameAssets 下找不到任何 Characters/Monsters 的 SequenceImage 資料夾。"); return; }

            // 既有表（保留列順序與所有欄）
            var existing = new Dictionary<string, Dictionary<string, string>>();
            var order = new List<string>();
            if (File.Exists(TablePath))
            {
                var t = CsvTable.Parse(File.ReadAllText(TablePath), "ShadowAnchorTable");
                foreach (var e in t.Errors) Debug.LogWarning("[影子錨點] 既有表：" + e);
                foreach (var row in t.Rows)
                {
                    string k = row.Get("Key").Trim().ToLowerInvariant();
                    if (k.Length == 0 || existing.ContainsKey(k)) continue;
                    existing[k] = row.ToDictionary();
                    order.Add(k);
                }
            }

            var results = new Dictionary<string, ShadowAnchorPx>();   // key → 這次要顯示/寫入的錨點
            int computed = 0, kept = 0, manual = 0, redone = 0;
            var log = new StringBuilder();
            log.AppendLine($"[影子錨點] 掃到 {seqs.Count} 個角色/動作（{(onlyNew ? "只算新的" : "重算所有 auto")}{(writeTable ? "" : "、不改表")}）");

            for (int i = 0; i < seqs.Count; i++)
            {
                var s = seqs[i];
                EditorUtility.DisplayProgressBar("計算影子錨點", s.key, (float)i / seqs.Count);

                existing.TryGetValue(s.key, out var row);
                bool isManual = row != null && row.TryGetValue("Source", out var src) && src.Trim().ToLowerInvariant() == "manual";
                int oldFrames = row != null && row.TryGetValue("Frames", out var fr) && int.TryParse(fr, out int fv) ? fv : -1;
                bool framesChanged = row != null && oldFrames >= 0 && oldFrames != s.files.Count;

                bool needCompute = row == null || (!isManual && (!onlyNew || framesChanged));
                if (!writeTable) needCompute = row == null;   // 只出圖：有列的用表裡的值畫，沒列的當場算（不寫）

                ShadowAnchorPx a;
                if (needCompute)
                {
                    a = Compute(s);
                    if (row != null && framesChanged) { redone++; a.source = "auto"; }
                    else computed++;
                    if (writeTable)
                    {
                        var d = row ?? new Dictionary<string, string>();
                        string note = (row != null && framesChanged) ? $"幀數 {oldFrames}→{s.files.Count} 已重算" : (row != null ? "" : "");
                        FillRow(d, s.key, a, note);
                        existing[s.key] = d;
                        if (row == null) order.Add(s.key);
                    }
                }
                else
                {
                    a = FromRow(row);
                    if (isManual) manual++; else kept++;
                }
                results[s.key] = a;
            }
            EditorUtility.ClearProgressBar();

            if (writeTable)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TablePath));
                var rows = order.Select(k => (IReadOnlyDictionary<string, string>)existing[k]).ToList();
                File.WriteAllText(TablePath, CsvWriter.Build(HeaderCells, Columns, CommentLines, rows), new UTF8Encoding(false));
                AssetDatabase.ImportAsset("Assets/Data/" + ShadowAnchorTable.FileName);
                ShadowAnchorTable.ResetForPlayMode();
                log.AppendLine($"  表：{TablePath}（新算 {computed}、換圖重算 {redone}、沿用 {kept}、manual {manual}）");
            }

            // 檢視圖：每個角色一張
            int sheets = DrawSheets(seqs, results);
            log.AppendLine($"  檢視圖：{sheets} 張 → {SheetDir}");
            log.AppendLine("  每列一個動作（idle / walk / attack / dead / 其他，依資料夾實際有的）、每列 4 幀；灰橢圓＝影子、紅十字＝錨點、淡線＝畫布中心。");
            log.AppendLine("  看到不對的：改 CSV 那列的 AnchorX / AnchorY / WidthPx，Source 改成 manual，再跑一次「檢視圖」確認。");
            Debug.Log(log.ToString());
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("[影子錨點] 失敗：" + e.Message + "\n" + e.StackTrace);
        }
    }

    // ── 掃資料夾 ──
    static List<Seq> Scan()
    {
        var list = new List<Seq>();
        string root = Path.Combine(Application.dataPath, "GameAssets");
        if (!Directory.Exists(root)) return list;
        foreach (var seqDir in Directory.GetDirectories(root, "SequenceImage", SearchOption.AllDirectories))
        {
            string kind = Path.GetFileName(Path.GetDirectoryName(seqDir));   // Characters / Monsters
            if (kind != ShadowAnchorTable.KindCharacters && kind != ShadowAnchorTable.KindMonsters) continue;
            var nameDirs = new List<string>(Directory.GetDirectories(seqDir)); nameDirs.Sort(System.StringComparer.Ordinal);
            foreach (var nd in nameDirs)
            {
                var actDirs = new List<string>(Directory.GetDirectories(nd)); actDirs.Sort(System.StringComparer.Ordinal);
                foreach (var ad in actDirs)
                {
                    var files = new List<string>(Directory.GetFiles(ad, "*.png", SearchOption.TopDirectoryOnly));
                    files.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));   // 與 Sync 工具同序
                    if (files.Count == 0) continue;
                    string name = Path.GetFileName(nd), action = Path.GetFileName(ad);
                    list.Add(new Seq { kind = kind, name = name, action = action, key = ShadowAnchorTable.MakeKey(kind, name, action), files = files });
                }
            }
        }
        list.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
        return list;
    }

    static Texture2D LoadPng(string path)
    {
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!t.LoadImage(File.ReadAllBytes(path))) { Object.DestroyImmediate(t); return null; }
        return t;
    }

    static ShadowAnchorPx Compute(Seq s)
    {
        var stats = new List<ShadowAnchorMath.FrameStat>(s.files.Count);
        int cw = 0, ch = 0;
        foreach (var f in s.files)
        {
            var t = LoadPng(f);
            if (t == null) continue;
            if (cw == 0) { cw = t.width; ch = t.height; }
            stats.Add(ShadowAnchorMath.MeasureFrame(t.GetPixels32(), t.width, t.height));
            Object.DestroyImmediate(t);
        }
        return ShadowAnchorMath.Combine(stats, cw, ch, ShadowAnchorMath.IsLyingAction(s.action));
    }

    static void FillRow(Dictionary<string, string> d, string key, ShadowAnchorPx a, string note)
    {
        d["Key"] = key;
        d["AnchorX"] = a.xFromCenterPx.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        d["AnchorY"] = a.yFromBottomPx.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        d["WidthPx"] = a.widthPx.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        d["Source"] = "auto";
        d["Frames"] = a.frames.ToString();
        d["CanvasW"] = a.canvasW.ToString();
        d["CanvasH"] = a.canvasH.ToString();
        if (!string.IsNullOrEmpty(note) || !d.ContainsKey("Note")) d["Note"] = note ?? "";
    }

    static ShadowAnchorPx FromRow(Dictionary<string, string> row)
    {
        float F(string k) => row.TryGetValue(k, out var v) ? CsvFieldParse.Float(v) : 0f;
        int I(string k) => row.TryGetValue(k, out var v) ? CsvFieldParse.Int(v) : 0;
        return new ShadowAnchorPx
        {
            ok = true,
            xFromCenterPx = F("AnchorX"), yFromBottomPx = F("AnchorY"), widthPx = F("WidthPx"),
            canvasW = I("CanvasW"), canvasH = I("CanvasH"), frames = I("Frames"),
            source = row.TryGetValue("Source", out var s) ? s : "auto",
        };
    }

    // ── 檢視圖 ──
    const int CellPx = 192;            // 每格輸出尺寸（幀縮到這麼大）
    const int SamplesPerAction = 4;
    static readonly Color32 BgColor = new Color32(0xB9, 0xAB, 0x93, 255);   // 接近石材地板的中間調
    const float ShadowAlpha = 0.45f;   // 比遊戲的 0.3 深一點，看得清楚

    static int DrawSheets(List<Seq> seqs, Dictionary<string, ShadowAnchorPx> results)
    {
        Directory.CreateDirectory(SheetDir);
        int n = 0;
        foreach (var g in seqs.GroupBy(q => q.kind + "/" + q.name))
        {
            var acts = g.OrderBy(q => { int i = System.Array.IndexOf(ActionOrder, q.action.ToLowerInvariant()); return i < 0 ? 99 : i; })
                        .ThenBy(q => q.action, System.StringComparer.Ordinal).ToList();
            int rows = acts.Count;
            var sheet = new Texture2D(CellPx * SamplesPerAction, CellPx * rows, TextureFormat.RGBA32, false);
            var px = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < px.Length; i++) px[i] = BgColor;

            for (int r = 0; r < rows; r++)
            {
                var s = acts[r];
                results.TryGetValue(s.key, out var a);
                int rowY0 = (rows - 1 - r) * CellPx;   // 第 0 列畫在最上面（貼圖原點左下）
                for (int c = 0; c < SamplesPerAction; c++)
                {
                    int fi = s.files.Count == 1 ? 0 : Mathf.Clamp(Mathf.RoundToInt((float)c * (s.files.Count - 1) / (SamplesPerAction - 1)), 0, s.files.Count - 1);
                    var t = LoadPng(s.files[fi]);
                    if (t == null) continue;
                    DrawCell(px, sheet.width, c * CellPx, rowY0, t, a);
                    Object.DestroyImmediate(t);
                }
                // 列分隔線
                if (r > 0) for (int x = 0; x < sheet.width; x++) px[(rowY0 + CellPx - 1) * sheet.width + x] = new Color32(60, 50, 40, 255);
            }
            sheet.SetPixels32(px); sheet.Apply();
            string file = Path.Combine(SheetDir, $"{g.Key.Replace('/', '_')}.png");
            File.WriteAllBytes(file, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            n++;
        }
        return n;
    }

    /// <summary>把一幀（含影子、錨點十字）畫進拼圖的一格。幀等比縮到 CellPx。</summary>
    static void DrawCell(Color32[] dst, int dstW, int ox, int oy, Texture2D frame, ShadowAnchorPx a)
    {
        var src = frame.GetPixels32();
        int fw = frame.width, fh = frame.height;
        float scale = (float)CellPx / Mathf.Max(fw, fh);
        int dw = Mathf.RoundToInt(fw * scale), dh = Mathf.RoundToInt(fh * scale);
        int cx0 = ox + (CellPx - dw) / 2, cy0 = oy + (CellPx - dh) / 2;   // 幀在格內置中

        // 畫布中心淡線
        int centerX = cx0 + dw / 2;
        for (int y = 0; y < CellPx; y++) Blend(dst, dstW, centerX, oy + y, new Color32(255, 255, 255, 70));

        if (a.ok)
        {
            // 影子：橢圓中心 = 錨點；寬 = WidthPx × 1.0（BlobShadow.WidthFactor 預設）、高 = 寬 × 0.5
            float sx = a.canvasW > 0 ? (float)fw / a.canvasW : 1f, sy = a.canvasH > 0 ? (float)fh / a.canvasH : 1f;
            float ax = (a.xFromCenterPx * sx + fw * 0.5f) * scale + cx0;
            float ay = (a.yFromBottomPx * sy) * scale + cy0;
            float rw = Mathf.Max(2f, a.widthPx * sx * scale * 0.5f), rh = rw * 0.5f;
            int x0 = Mathf.FloorToInt(ax - rw), x1 = Mathf.CeilToInt(ax + rw), y0 = Mathf.FloorToInt(ay - rh), y1 = Mathf.CeilToInt(ay + rh);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - ax) / rw, dy = (y + 0.5f - ay) / rh;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d >= 1f) continue;
                    float edge = Mathf.InverseLerp(0.72f, 1f, d);
                    float alpha = (1f - Mathf.SmoothStep(0f, 1f, edge)) * ShadowAlpha;
                    Blend(dst, dstW, x, y, new Color32(0, 0, 0, (byte)(alpha * 255f)));
                }
        }

        // 幀（最近鄰縮小就夠看）
        for (int y = 0; y < dh; y++)
        {
            int syy = Mathf.Min(fh - 1, Mathf.FloorToInt(y / scale));
            for (int x = 0; x < dw; x++)
            {
                int sxx = Mathf.Min(fw - 1, Mathf.FloorToInt(x / scale));
                var c = src[syy * fw + sxx];
                if (c.a == 0) continue;
                Blend(dst, dstW, cx0 + x, cy0 + y, c);
            }
        }

        if (a.ok)
        {
            // 錨點紅十字（畫在幀之上，確保看得到）
            float sx = a.canvasW > 0 ? (float)fw / a.canvasW : 1f, sy = a.canvasH > 0 ? (float)fh / a.canvasH : 1f;
            int ax = Mathf.RoundToInt((a.xFromCenterPx * sx + fw * 0.5f) * scale + cx0);
            int ay = Mathf.RoundToInt((a.yFromBottomPx * sy) * scale + cy0);
            var red = new Color32(255, 40, 40, 255);
            for (int i = -6; i <= 6; i++) { Blend(dst, dstW, ax + i, ay, red); Blend(dst, dstW, ax, ay + i, red); }
        }
    }

    static void Blend(Color32[] dst, int w, int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= w) return;
        int i = y * w + x;
        if (i < 0 || i >= dst.Length) return;
        float a = c.a / 255f;
        var d = dst[i];
        dst[i] = new Color32(
            (byte)Mathf.RoundToInt(d.r + (c.r - d.r) * a),
            (byte)Mathf.RoundToInt(d.g + (c.g - d.g) * a),
            (byte)Mathf.RoundToInt(d.b + (c.b - d.b) * a),
            255);
    }
}
