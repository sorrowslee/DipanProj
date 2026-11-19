using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dipan.Vfx
{
    /// <summary>
    /// DipanVfx 套件的「外部接口」與共用資源（2026-11-18，見 readme/DIPAN_VFX.md）。
    ///
    /// 套件被主遊戲與地圖編輯器兩個專案共用，但兩邊取圖、排序的方式不同，所以把差異收在這裡：
    /// <list type="bullet">
    /// <item><see cref="SpriteLoader"/>：主遊戲留空＝<c>Resources.Load&lt;Sprite&gt;</c>；
    ///   地圖編輯器在開預覽器時改成「直接讀 DipanProj_Main/Assets/Resources 底下的 png」。</item>
    /// <item><see cref="SortOrderForY"/>：主遊戲填 <c>MapDepthSort.Order(y, 0)</c>（立著的火跟角色一起 Y 排序）；
    ///   留空時呼叫端給的排序值直接用。</item>
    /// </list>
    /// 另外提供程序產生的共用貼圖（噪聲、圓點），不必放圖檔。
    /// </summary>
    public static class VfxAssets
    {
        /// <summary>取圖（路徑同 Resources.Load：相對 Resources、不含副檔名）。null＝用 Resources.Load。</summary>
        public static Func<string, Sprite> SpriteLoader;

        /// <summary>取一般貼圖（路徑同 Resources.Load：相對 Resources、不含副檔名）。null＝用 Resources.Load&lt;Texture2D&gt;。
        /// 靈獸的身體平鋪貼圖用（2026-11-19）；地圖編輯器換成直接讀主遊戲的 png。</summary>
        public static Func<string, Texture2D> TextureLoader;

        /// <summary>世界 Y → sortingOrder（立著的東西用）。null＝不做 Y 排序。</summary>
        public static Func<float, int> SortOrderForY;

        public static Sprite LoadSprite(string path)
        {
            path = (path ?? "").Trim();
            if (path.Length == 0) return null;
            return SpriteLoader != null ? SpriteLoader(path) : Resources.Load<Sprite>(path);
        }

        public static Texture2D LoadTexture(string path)
        {
            path = (path ?? "").Trim();
            if (path.Length == 0) return null;
            return TextureLoader != null ? TextureLoader(path) : Resources.Load<Texture2D>(path);
        }

        /// <summary>立著的東西在世界 Y 時該用的排序；沒有 Y 排序規則就回傳 fallback。</summary>
        public static int OrderForY(float worldY, int fallback)
            => SortOrderForY != null ? SortOrderForY(worldY) : fallback;

        // ───────────────────────── 共用材質 ─────────────────────────

        static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();
        static readonly HashSet<string> _warned = new HashSet<string>();

        /// <summary>依 shader 名稱取一顆共用材質（個別差異一律走 MaterialPropertyBlock）。找不到 shader 回 null 並警告一次。</summary>
        public static Material SharedMaterial(string shaderName)
        {
            if (_mats.TryGetValue(shaderName, out var m) && m != null) return m;
            var sh = Shader.Find(shaderName);
            if (sh == null)
            {
                if (_warned.Add(shaderName))
                    Debug.LogWarning($"[DipanVfx] 找不到 shader {shaderName}（應在 DipanVfx/Runtime/Resources/Shaders/）。");
                return null;
            }
            m = new Material(sh) { name = shaderName + " (DipanVfx)" };
            if (m.HasProperty(IdNoiseTex)) m.SetTexture(IdNoiseTex, NoiseTexture);
            _mats[shaderName] = m;
            return m;
        }

        public static readonly int IdNoiseTex = Shader.PropertyToID("_NoiseTex");

        // ───────────────────────── 程序貼圖 ─────────────────────────

        static Texture2D _noise;
        static Sprite _dot;

        /// <summary>
        /// 可無縫平鋪的噪聲（128×128、線性、Repeat）：R、G 是兩組不同的 fBm，B 是較細的一組。
        /// 火焰 shader 用它往上捲動扭曲、侵蝕火舌；焦土用它做不規則的邊緣與脈動。
        /// </summary>
        public static Texture2D NoiseTexture
        {
            get
            {
                if (_noise != null) return _noise;
                const int N = 128;
                _noise = new Texture2D(N, N, TextureFormat.RGBA32, false, true)
                {
                    name = "DipanVfx Noise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float r = Fbm(x, y, N, 4, 11);
                    float g = Fbm(x, y, N, 4, 47);
                    float b = Fbm(x, y, N, 8, 83);
                    px[y * N + x] = new Color32((byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f), 255);
                }
                _noise.SetPixels32(px);
                _noise.Apply(false, false);
                return _noise;
            }
        }

        /// <summary>柔邊白圓點（火星、光點用）。</summary>
        public static Sprite DotSprite
        {
            get
            {
                if (_dot != null) return _dot;
                const int N = 32;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
                {
                    name = "DipanVfx Dot", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
                tex.SetPixels32(px);
                tex.Apply(false, false);
                _dot = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);   // 1 世界單位寬
                _dot.name = "DipanVfx Dot";
                _dot.hideFlags = HideFlags.DontSave;
                return _dot;
            }
        }

        static Sprite _smoke;

        /// <summary>
        /// 煙團（64×64、白色＋alpha，1 世界單位寬；2026-11-19 靈獸身後的黑煙用）：圓形淡出＋兩層值噪聲侵蝕出不規則的邊，
        /// 呼叫端用頂點色上色（煙色）、用旋轉避免每團長一樣。
        /// </summary>
        public static Sprite SmokeSprite
        {
            get
            {
                if (_smoke != null) return _smoke;
                const int N = 64;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
                {
                    name = "DipanVfx Smoke", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                    float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                    float n = ValueNoise(u * 5f, v * 5f, 11) * 0.6f + ValueNoise(u * 11f, v * 11f, 29) * 0.4f;
                    float fall = Mathf.Pow(Mathf.Clamp01(1f - r), 1.5f);
                    float a = fall * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.75f, n + (1f - r) * 0.35f));
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
                tex.SetPixels32(px);
                tex.Apply(false, false);
                _smoke = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
                _smoke.name = "DipanVfx Smoke";
                _smoke.hideFlags = HideFlags.DontSave;
                return _smoke;
            }
        }

        static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)seed * 0xcb1ab31fu;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        static float ValueNoise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Hash01(xi, yi, seed), b = Hash01(xi + 1, yi, seed), c = Hash01(xi, yi + 1, seed), d = Hash01(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static Sprite _square;

        /// <summary>全白正方形（Full Rect、1 世界單位寬；程序 shader 的畫布，例如衝擊波圈）。</summary>
        public static Sprite SquareSprite
        {
            get
            {
                if (_square != null) return _square;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                {
                    name = "DipanVfx Square", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                var px = new Color32[16];
                for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply(false, false);
                _square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4, 0, SpriteMeshType.FullRect);
                _square.name = "DipanVfx Square";
                _square.hideFlags = HideFlags.DontSave;
                return _square;
            }
        }

        // 可平鋪的 value noise fBm：每個八度的格子數都是 N 的因數，所以邊界接得起來。
        static float Fbm(int x, int y, int n, int baseCells, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int cells = baseCells;
            for (int o = 0; o < 4; o++)
            {
                sum += amp * ValueNoise(x * cells / (float)n, y * cells / (float)n, cells, seed + o * 131);
                norm += amp;
                amp *= 0.5f;
                cells *= 2;
            }
            return Mathf.Clamp01(sum / norm);
        }

        static float ValueNoise(float fx, float fy, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Hash(x0, y0, period, seed), b = Hash(x0 + 1, y0, period, seed);
            float c = Hash(x0, y0 + 1, period, seed), d = Hash(x0 + 1, y0 + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static float Hash(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        // ───────────────────────── 小工具 ─────────────────────────

        /// <summary>解析 "#RRGGBB" / "#RRGGBBAA"；空或解析失敗回 fallback。</summary>
        public static Color ParseColor(string s, Color fallback)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) return fallback;
            if (!s.StartsWith("#")) s = "#" + s;
            return ColorUtility.TryParseHtmlString(s, out var c) ? c : fallback;
        }

        /// <summary>關掉 Domain Reload 後 static 不歸零；進 Play 時由主遊戲 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode()
        {
            _mats.Clear();
            _warned.Clear();
            // 程序貼圖（DontSave）跨 Play 保留即可；SpriteLoader／SortOrderForY 由各專案啟動時重新指定。
        }
    }
}
