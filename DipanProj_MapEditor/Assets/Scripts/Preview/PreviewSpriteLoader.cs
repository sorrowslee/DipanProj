using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DipanMapEditor.Preview
{
    /// <summary>
    /// 載入角色的逐格動畫幀（idle/walk），直接讀主專案 GameAssets 來源 PNG
    /// （Main 與當前 module 底下的 Monsters/SequenceImage/&lt;folder&gt;/&lt;state&gt;/）。
    ///
    /// **兩種尺寸模式**（呼叫端用 <paramref name="normalizeHeight"/> 決定，2026-09-15 補）：
    ///  ‧ <c>normalizeHeight = 0</c>（預設，劇情演出預覽沿用）：PPU = 256/tileSize，
    ///    即「原生 256px 畫布 ＝ 一格」——角色大小**取決於原圖畫布像素數**。
    ///  ‧ <c>normalizeHeight &gt; 0</c>（NPC 預覽用 1.95）：與遊戲端 <c>MonsterController</c> 同一套——
    ///    量 idle（取不到用 walk）首幀的**不透明像素高度**，把角色縮放成固定的世界高度，
    ///    **與原圖畫布大小無關**。這樣編輯器看到的大小＝遊戲看到的大小（再各自乘 CSV 的 Scale）。
    ///
    /// ⚠ 兩邊算法不同就會出「編輯器裡小、遊戲裡對」的錯覺，害人把 CSV Scale 填大（見 PROBLEMS C15）。
    /// </summary>
    public static class PreviewSpriteLoader
    {
        const int TileNativePx = 256;

        public class ActorFrames { public Sprite[] idle; public Sprite[] walk; }
        static readonly Dictionary<string, ActorFrames> _cache = new Dictionary<string, ActorFrames>();

        public static void Clear() => _cache.Clear();

        static string GameAssetsRoot()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;   // DipanProj_MapEditor
            string repoRoot = Directory.GetParent(projectRoot).FullName;               // DipanProj
            return Path.Combine(repoRoot, "DipanProj_Main", "Assets", "GameAssets");
        }

        static List<string> FrameFiles(string folder, string state, string module)
        {
            string ga = GameAssetsRoot();
            var bases = new List<string>();
            if (!string.IsNullOrEmpty(module) && module != "Main") bases.Add(Path.Combine(ga, "Modules", module));
            bases.Add(Path.Combine(ga, "Main"));
            foreach (var b in bases)
            {
                string dir = Path.Combine(b, "Monsters", "SequenceImage", folder, state);
                if (!Directory.Exists(dir)) continue;
                var files = new List<string>(Directory.GetFiles(dir, "*.png"));
                if (files.Count == 0) continue;
                files.Sort(System.StringComparer.Ordinal);
                return files;
            }
            return null;
        }


        // 量首幀不透明像素的高度（px），供 walk 對齊 idle／世界高正規化用。
        static int VisiblePx(string framePath)
        {
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(framePath));
                var px = tex.GetPixels32(); int w = tex.width, h = tex.height;
                int top = -1, bottom = -1;
                for (int y = 0; y < h; y++)
                {
                    bool any = false; int rb = y * w;
                    for (int x = 0; x < w; x++) if (px[rb + x].a > 10) { any = true; break; }
                    if (any) { if (top < 0) top = y; bottom = y; }
                }
                return top < 0 ? 0 : (bottom - top + 1);
            }
            catch { return 0; }
        }

        static Sprite[] MakeSprites(List<string> files, float ppu)
        {
            ppu = Mathf.Clamp(ppu, 1f, 4096f);
            var arr = new Sprite[files.Count];
            for (int i = 0; i < files.Count; i++)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                tex.LoadImage(File.ReadAllBytes(files[i]));
                arr[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
            }
            return arr;
        }

        /// <param name="tileSize">一格的世界大小（舊模式用它換 PPU）。</param>
        /// <param name="normalizeHeight">
        /// &gt;0＝把角色「可見像素高度」正規化成這麼多世界單位（遊戲端 CharacterWorldHeight，預設 1.95）；
        /// 0＝舊行為（256px 畫布＝一格）。
        /// </param>
        public static ActorFrames Load(string folder, string module, float tileSize, float normalizeHeight = 0f)
        {
            if (string.IsNullOrEmpty(folder)) return null;
            string key = folder + "|" + (module ?? "") + "|" + tileSize + "|" + normalizeHeight;
            if (_cache.TryGetValue(key, out var c)) return c;

            var idleFiles = FrameFiles(folder, "idle", module);
            var walkFiles = FrameFiles(folder, "walk", module);

            int idleVisPx = (idleFiles != null && idleFiles.Count > 0) ? VisiblePx(idleFiles[0]) : 0;
            int walkVisPx = (walkFiles != null && walkFiles.Count > 0) ? VisiblePx(walkFiles[0]) : 0;

            float basePpu = TileNativePx / Mathf.Max(0.0001f, tileSize);
            float idlePpu = basePpu, walkPpu = basePpu;

            if (normalizeHeight > 0f)
            {
                // 遊戲端同公式：可見高度 → 固定世界高（與畫布像素數無關）。
                // idle 取不到就用 walk 當基準（同 MonsterController）。
                int fallbackPx = idleVisPx > 0 ? idleVisPx : walkVisPx;
                if (fallbackPx > 0)
                {
                    idlePpu = (idleVisPx > 0 ? idleVisPx : fallbackPx) / normalizeHeight;
                    walkPpu = (walkVisPx > 0 ? walkVisPx : fallbackPx) / normalizeHeight;
                }
            }
            else if (idleVisPx > 0 && walkVisPx > 0)
            {
                // 舊模式的 walk 逐動作正規化：把 walk 首幀可見高度對齊 idle，
                // 消除各動作大小落差（走路變大、停下變小）。
                float walkTile = Mathf.Clamp(tileSize * ((float)idleVisPx / walkVisPx), 0.1f, 30f);
                walkPpu = TileNativePx / Mathf.Max(0.0001f, walkTile);
            }

            var res = new ActorFrames
            {
                idle = idleFiles != null ? MakeSprites(idleFiles, idlePpu) : null,
                walk = walkFiles != null ? MakeSprites(walkFiles, walkPpu) : null,
            };
            if (res.idle == null && res.walk != null) res.idle = res.walk;   // 沒 idle 用 walk 當待機（同遊戲）
            if (res.idle == null && res.walk == null)
                Debug.LogWarning($"[CutscenePreview] 找不到演員「{folder}」的 idle/walk 圖（Main 或 Modules/{module} 底下的 Monsters/SequenceImage/{folder}/）。");
            _cache[key] = res;
            return res;
        }
    }
}
