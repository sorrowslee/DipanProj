using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DipanMapEditor.Preview
{
    /// <summary>
    /// 載入劇情演員的逐格動畫幀（idle/walk），直接讀主專案 GameAssets 來源 PNG
    /// （Main 與當前 module 底下的 Monsters/SequenceImage/&lt;folder&gt;/&lt;state&gt;/）。
    /// 尺寸正規化與遊戲端一致：依 idle(取不到用 walk) 首幀「不透明像素高度」把角色縮放到 CharacterWorldHeight(1.95) 世界高，
    /// 所以編輯器預覽的角色大小 ≈ 遊戲。
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


        // 量首幀不透明像素的高度（px），供 walk 對齊 idle 用。
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
            var arr = new Sprite[files.Count];
            for (int i = 0; i < files.Count; i++)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                tex.LoadImage(File.ReadAllBytes(files[i]));
                arr[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
            }
            return arr;
        }

        public static ActorFrames Load(string folder, string module, float tileSize)
        {
            if (string.IsNullOrEmpty(folder)) return null;
            string key = folder + "|" + (module ?? "") + "|" + tileSize;
            if (_cache.TryGetValue(key, out var c)) return c;

            var idleFiles = FrameFiles(folder, "idle", module);
            var walkFiles = FrameFiles(folder, "walk", module);

            // idle 用 PPU = 256/tileSize（同遊戲）；walk 逐動作正規化：把 walk 首幀可見高度對齊 idle，
            // 消除 AI 各動作大小落差（走路變大、停下變小）。與遊戲端 MonsterAnimator 同公式。
            float walkTile = tileSize;
            int idleVisPx = (idleFiles != null && idleFiles.Count > 0) ? VisiblePx(idleFiles[0]) : 0;
            int walkVisPx = (walkFiles != null && walkFiles.Count > 0) ? VisiblePx(walkFiles[0]) : 0;
            if (idleVisPx > 0 && walkVisPx > 0)
                walkTile = Mathf.Clamp(tileSize * ((float)idleVisPx / walkVisPx), 0.1f, 30f);

            var res = new ActorFrames
            {
                idle = idleFiles != null ? MakeSprites(idleFiles, TileNativePx / Mathf.Max(0.0001f, tileSize)) : null,
                walk = walkFiles != null ? MakeSprites(walkFiles, TileNativePx / Mathf.Max(0.0001f, walkTile)) : null,
            };
            if (res.idle == null && res.walk != null) res.idle = res.walk;   // 沒 idle 用 walk 當待機（同遊戲）
            if (res.idle == null && res.walk == null)
                Debug.LogWarning($"[CutscenePreview] 找不到演員「{folder}」的 idle/walk 圖（Main 或 Modules/{module} 底下的 Monsters/SequenceImage/{folder}/）。");
            _cache[key] = res;
            return res;
        }
    }
}
