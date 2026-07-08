using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DipanMapEditor.Core
{
    /// <summary>特效「暗黑風換色」：逐像素把原幀轉成暗色調的預設，供預覽器即時套用，
    /// 並可把套色後的幀「烤」成 PNG 匯出（這樣才能真的複製進遊戲用）。
    /// 全部保留 alpha（透明處不動，避免暗邊光暈）。</summary>
    public static class EffectRecolor
    {
        public enum Mode
        {
            None,       // 原色
            Darken,     // 壓暗（保留色相，整體變暗）
            DarkRed,    // 暗紅（去飽和後染暗紅，冥婚/血色）
            ColdBlue,   // 冷藍（去飽和後染冷藍，陰森）
            DesatDark,  // 去飽和壓暗（褪色 + 變暗，鬼氣）
        }

        public static readonly Mode[] AllModes =
            { Mode.None, Mode.Darken, Mode.DarkRed, Mode.ColdBlue, Mode.DesatDark };

        public static string DisplayName(Mode m)
        {
            switch (m)
            {
                case Mode.Darken: return "壓暗";
                case Mode.DarkRed: return "暗紅";
                case Mode.ColdBlue: return "冷藍";
                case Mode.DesatDark: return "去飽和壓暗";
                default: return "原色";
            }
        }

        static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        /// <summary>取得（並快取）某幀套色後的貼圖。Mode.None 直接回原幀。</summary>
        public static Texture2D GetRecolored(string absPath, Mode mode)
        {
            if (mode == Mode.None) return EffectLibrary.GetFrame(absPath);
            string key = absPath + "|" + mode;
            if (_cache.TryGetValue(key, out var t) && t != null) return t;

            var baseTex = EffectLibrary.GetFrame(absPath);
            if (baseTex == null) return null;

            var px = baseTex.GetPixels32();
            Apply(px, mode);

            t = new Texture2D(baseTex.width, baseTex.height, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(px);
            t.Apply(false);
            _cache[key] = t;
            return t;
        }

        static void Apply(Color32[] px, Mode mode)
        {
            for (int i = 0; i < px.Length; i++)
            {
                Color32 c = px[i];
                if (c.a == 0) continue; // 全透明不動

                float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
                float lum = 0.299f * r + 0.587f * g + 0.114f * b;

                switch (mode)
                {
                    case Mode.Darken:
                        r *= 0.5f; g *= 0.5f; b *= 0.5f;
                        break;
                    case Mode.DarkRed:      // 去飽和 → 染暗紅（亮部偏橙、暗部偏深紅）
                        r = lum * 1.00f * 0.95f;
                        g = lum * 0.30f * 0.95f;
                        b = lum * 0.24f * 0.95f;
                        break;
                    case Mode.ColdBlue:     // 去飽和 → 染冷藍
                        r = lum * 0.42f * 0.95f;
                        g = lum * 0.60f * 0.95f;
                        b = lum * 1.00f * 0.95f;
                        break;
                    case Mode.DesatDark:    // 往灰靠 0.6 + 壓暗 0.6
                        r = Mathf.Lerp(r, lum, 0.6f) * 0.6f;
                        g = Mathf.Lerp(g, lum, 0.6f) * 0.6f;
                        b = Mathf.Lerp(b, lum, 0.6f) * 0.6f;
                        break;
                }

                px[i] = new Color32(To255(r), To255(g), To255(b), c.a);
            }
        }

        static byte To255(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        /// <summary>把某效果的某顏色整串幀，套色後匯出成 PNG。
        /// 輸出到 StreamingAssets/Effects_Recolored/&lt;類別&gt;/&lt;效果&gt;/&lt;顏色&gt;_&lt;模式&gt;/&lt;效果&gt;_001.png。
        /// 回傳寫出的幀數；outDir 由 out 參數帶回實際輸出資料夾。</summary>
        public static int ExportColorSet(EffectLibrary.Entry entry, EffectLibrary.ColorSet color, Mode mode, out string outDir)
        {
            outDir = null;
            if (entry == null || color == null || color.frames.Count == 0) return 0;

            string modeTag = mode == Mode.None ? "orig" : DisplayNameAscii(mode);
            outDir = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Effects_Recolored",
                                  entry.category, entry.name, color.name + "_" + modeTag);
            Directory.CreateDirectory(outDir);

            int written = 0;
            for (int i = 0; i < color.frames.Count; i++)
            {
                var tex = GetRecolored(color.frames[i], mode);
                if (tex == null) continue;
                byte[] bytes = tex.EncodeToPNG();
                string name = string.Format("{0}_{1:000}.png", entry.name, i + 1);
                File.WriteAllBytes(Path.Combine(outDir, name), bytes);
                written++;
            }
            return written;
        }

        // 匯出資料夾名用 ASCII 標籤（避免跨平台檔名編碼問題）
        static string DisplayNameAscii(Mode m)
        {
            switch (m)
            {
                case Mode.Darken: return "darken";
                case Mode.DarkRed: return "darkred";
                case Mode.ColdBlue: return "coldblue";
                case Mode.DesatDark: return "desatdark";
                default: return "orig";
            }
        }
    }
}
