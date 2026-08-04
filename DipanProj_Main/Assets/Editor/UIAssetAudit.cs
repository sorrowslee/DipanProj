using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Dipan.EditorTools
{
    /// <summary>
    /// UI 貼圖匯入設定的**檢查與批次修正**工具。
    ///
    /// 為什麼需要它：<see cref="UITextureImportSettings"/> 只在「第一次匯入」時套規則（刻意的，
    /// 否則你在 Inspector 的手動微調會被蓋掉），所以**既有的圖不會自動修正**。這支工具就是那個手動開關。
    ///
    /// 選單（Project Tools → 素材）：
    ///   ‧ **UI 貼圖設定：檢查** —— 只印報告，一個位元組都不改。先看這個。
    ///   ‧ **UI 貼圖設定：套用建議值** —— 實際改 Max Size 與壓縮設定並重新匯入（會先跳確認）。
    ///
    /// 規則本身在 <see cref="UIAssetRules"/>，要調數字改那裡。
    /// 見 readme/PERF_QUALITY_AUDIT.md「素材尺寸規範」。
    /// </summary>
    public static class UIAssetAudit
    {
        const string MenuCheck = "Project Tools/素材/UI 貼圖設定：檢查";
        const string MenuApply = "Project Tools/素材/UI 貼圖設定：套用建議值";

        class Row
        {
            public string path;
            public string category;
            public int longEdge;
            public int currentMax;
            public int recommendMax;
            public bool compressed;
            public bool mipmap;
            public bool oversizeSource;

            public bool NeedsFix => currentMax != recommendMax || compressed || mipmap;
            public long CurrentBytes => UIAssetRules.EstimateBytes(longEdge, currentMax);
            public long TargetBytes => UIAssetRules.EstimateBytes(longEdge, recommendMax);
        }

        [MenuItem(MenuCheck, false, 60)]
        static void Check() => Run(false);

        [MenuItem(MenuApply, false, 61)]
        static void Apply()
        {
            var rows = Scan();
            int fixes = 0;
            long save = 0;
            foreach (var r in rows) if (r.NeedsFix) { fixes++; save += r.CurrentBytes - r.TargetBytes; }

            if (fixes == 0)
            {
                EditorUtility.DisplayDialog("UI 貼圖設定", "全部都已經符合規則，沒有要改的。", "好");
                return;
            }

            bool ok = EditorUtility.DisplayDialog(
                "UI 貼圖設定：套用建議值",
                $"共 {rows.Count} 張 UI 貼圖，其中 {fixes} 張要調整。\n" +
                $"預估可省下約 {UIAssetRules.Human(save)} 記憶體。\n\n" +
                "會修改這些圖的匯入設定（Max Size / 不壓縮 / 關 Mipmap）並重新匯入。\n" +
                "原始 PNG 檔案不會被動到。要繼續嗎？",
                "套用", "取消");
            if (!ok) return;

            Run(true);
        }

        static void Run(bool apply)
        {
            var rows = Scan();
            var sb = new StringBuilder();
            int fixes = 0;
            long curTotal = 0, tgtTotal = 0;

            rows.Sort((a, b) => b.CurrentBytes.CompareTo(a.CurrentBytes));

            sb.AppendLine(apply ? "[UIAssetAudit] 套用建議值" : "[UIAssetAudit] 檢查報告（未修改任何東西）");
            sb.AppendLine($"規則：道具 icon → 256／滿版底圖(長邊≥1000) → 2048／其他面板素材 → 512（改 UIAssetRules）");
            sb.AppendLine();

            foreach (var r in rows)
            {
                curTotal += r.CurrentBytes;
                tgtTotal += r.TargetBytes;
                if (!r.NeedsFix) continue;
                fixes++;

                var flags = new List<string>();
                if (r.currentMax != r.recommendMax) flags.Add($"MaxSize {r.currentMax}→{r.recommendMax}");
                if (r.compressed) flags.Add("壓縮→不壓縮");
                if (r.mipmap) flags.Add("關 Mipmap");
                if (r.oversizeSource) flags.Add($"⚠原圖 {r.longEdge}px 遠大於需要，建議重出圖");

                sb.AppendLine($"  [{r.category}] {r.path}");
                sb.AppendLine($"      {string.Join("、", flags)}   ({UIAssetRules.Human(r.CurrentBytes)} → {UIAssetRules.Human(r.TargetBytes)})");

                if (apply) ApplyTo(r);
            }

            sb.AppendLine();
            sb.AppendLine($"共 {rows.Count} 張，{fixes} 張需要調整。");
            sb.AppendLine($"估算記憶體（未壓縮 RGBA）：{UIAssetRules.Human(curTotal)} → {UIAssetRules.Human(tgtTotal)}");
            if (!apply && fixes > 0)
                sb.AppendLine("要實際修改請跑：Project Tools → 素材 → UI 貼圖設定：套用建議值");

            Debug.Log(sb.ToString());

            if (apply)
            {
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("UI 貼圖設定",
                    $"已調整 {fixes} 張，估計省下 {UIAssetRules.Human(curTotal - tgtTotal)}。\n詳情見 Console。", "好");
            }
        }

        static List<Row> Scan()
        {
            var rows = new List<Row>();
            var guids = AssetDatabase.FindAssets("t:Texture2D");
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (!UIAssetRules.IsUIAsset(path)) continue;

                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                int longEdge = tex != null ? Mathf.Max(tex.width, tex.height) : 0;
                // 注意：tex.width/height 是**匯入後**的尺寸（已被 Max Size 夾過）。
                // 要看原圖尺寸得用 GetSourceTextureWidthAndHeight。
                ti.GetSourceTextureWidthAndHeight(out int srcW, out int srcH);
                if (srcW > 0) longEdge = Mathf.Max(srcW, srcH);

                int recommend = UIAssetRules.RecommendedMaxSize(path, longEdge);
                var s = ti.GetDefaultPlatformTextureSettings();

                rows.Add(new Row
                {
                    path = path,
                    longEdge = longEdge,
                    category = UIAssetRules.CategoryOf(path, longEdge),
                    currentMax = ti.maxTextureSize,
                    recommendMax = recommend,
                    compressed = s.textureCompression != TextureImporterCompression.Uncompressed,
                    mipmap = ti.mipmapEnabled,
                    oversizeSource = UIAssetRules.IsOversizeSource(longEdge, recommend),
                });
            }
            return rows;
        }

        static void ApplyTo(Row r)
        {
            var ti = AssetImporter.GetAtPath(r.path) as TextureImporter;
            if (ti == null) return;
            UIAssetRules.ApplySharedSettings(ti);
            ti.maxTextureSize = r.recommendMax;
            EditorUtility.SetDirty(ti);
            ti.SaveAndReimport();
        }
    }
}
