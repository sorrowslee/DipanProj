using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Dipan.MapRuntime
{
    /// <summary>
    /// runtime 從磁碟載 PNG → Texture2D（預設 Bilinear+mipmap，見 SceneFilterMode 註解）→ Sprite，並快取。
    /// 關鍵：一律以 PPU = 256/tileSize 建 sprite（= 編輯器尺寸），不依賴主專案 PNG 的匯入 PPU，
    /// 否則家具大小會與 .dipanmap 內的座標/scale 對不上。
    /// 另提供「依不透明像素貼合」的本地碰撞框（給家具 BoxCollider2D 用）。
    /// </summary>
    public class MapSpriteLoader
    {
        public const int TileNativePx = 256;
        const byte AlphaThreshold = 10;   // alpha 低於此值視為透明（去背邊）

        /// <summary>
        /// 場景貼圖濾波。
        /// Bilinear = 預設。素材 256px/格在 1080p 只顯示 ~108px/格（0.42x 縮小），
        ///            Point 縮小取樣會隨機丟棄過半像素 → 噪點與移動閃爍（見 PERF_QUALITY_AUDIT.md §2）。
        ///            搭配 mipmap（LoadImage 於 mipChain=true 時自動生成）縮小取樣才正確。
        /// Point    = 硬派像素邊緣（僅適合 >=1:1 放大顯示時，保留作 A/B 對比）。
        /// 執行期可用 PerfHud（按 P）的「場景濾波」按鈕、或按 F 即時切換（見 SetSceneFilterMode）。
        /// </summary>
        public static FilterMode SceneFilterMode = FilterMode.Bilinear;

        // 追蹤所有 runtime 載入的場景貼圖，供執行期切換濾波時即時套用（已被銷毀者於切換時清掉）。
        static readonly List<Texture2D> _liveTextures = new List<Texture2D>();

        /// <summary>
        /// 執行期設定場景貼圖濾波，並**即時**套到所有已載入的場景貼圖（否則只有之後新載入的才會變）。
        /// </summary>
        public static void SetSceneFilterMode(FilterMode mode)
        {
            SceneFilterMode = mode;
            for (int i = _liveTextures.Count - 1; i >= 0; i--)
            {
                var t = _liveTextures[i];
                if (t == null) { _liveTextures.RemoveAt(i); continue; } // 已銷毀
                t.filterMode = mode;
            }
        }

        /// <summary>在 Bilinear / Point 之間切換，回傳切換後的模式（給 PerfHud 按鈕用）。</summary>
        public static FilterMode ToggleSceneFilterMode()
        {
            SetSceneFilterMode(SceneFilterMode == FilterMode.Point ? FilterMode.Bilinear : FilterMode.Point);
            return SceneFilterMode;
        }

        readonly string _assetRoot;
        readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        readonly Dictionary<string, LocalBox> _alphaBoxes = new Dictionary<string, LocalBox>();
        readonly Dictionary<string, FootprintMask> _footprints = new Dictionary<string, FootprintMask>();

        /// <summary>
        /// 不透明像素的貼合框（世界單位）。<c>canvas</c> = 整張畫布的世界尺寸——
        /// 要算「可見內容佔畫布的比例」時**一定要除它**，不能假設畫布是 256px。
        /// </summary>
        public struct LocalBox { public bool ok; public Vector2 size; public Vector2 offset; public Vector2 canvas; }

        public MapSpriteLoader(string assetRoot) { _assetRoot = assetRoot; }

        public Texture2D GetTexture(CatalogItem item)
        {
            if (item == null) return null;
            if (_textures.TryGetValue(item.id, out var tex) && tex != null) return tex;

            string path = CatalogLoader.ResolveAssetPath(_assetRoot, item);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[MapSpriteLoader] 找不到圖檔：{path}");
                return null;
            }
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, true); // mipChain=true：LoadImage 會自動生成 mipmap（縮小取樣用，記憶體 +33%）
            tex.LoadImage(File.ReadAllBytes(path));    // 自動調整尺寸，CPU 可讀
            tex.filterMode = SceneFilterMode;
            tex.wrapMode = TextureWrapMode.Clamp;
            _liveTextures.Add(tex);
            _textures[item.id] = tex;
            return tex;
        }

        /// <summary>整張 sprite（單體物件 / 背景用），pivot 置中，PPU=256/tileSize。</summary>
        public Sprite GetWholeSprite(CatalogItem item, float tileSize)
        {
            if (item == null) return null;
            string key = $"{item.id}|whole|{tileSize}";
            if (_sprites.TryGetValue(key, out var sp) && sp != null) return sp;
            var tex = GetTexture(item);
            if (tex == null) return null;
            float ppu = TileNativePx / Mathf.Max(0.0001f, tileSize);
            sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
            _sprites[key] = sp;
            return sp;
        }

        // ---- 動畫地上物：依「幀的相對路徑」載入（一筆 catalog item 對多張幀圖）----

        /// <summary>載入（並快取）某幀 PNG 的貼圖，key = 相對路徑。失敗回 null。</summary>
        public Texture2D GetFrameTexture(string framePath)
        {
            if (string.IsNullOrEmpty(framePath)) return null;
            string key = $"frame|{framePath}";
            if (_textures.TryGetValue(key, out var tex) && tex != null) return tex;

            string path = Path.Combine(_assetRoot, framePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[MapSpriteLoader] 找不到動畫幀：{path}");
                return null;
            }
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, true); // mipChain=true：同 GetTexture，生成 mipmap
            tex.LoadImage(File.ReadAllBytes(path));
            tex.filterMode = SceneFilterMode;
            tex.wrapMode = TextureWrapMode.Clamp;
            _liveTextures.Add(tex);
            _textures[key] = tex;
            return tex;
        }

        /// <summary>某幀的整張 sprite（PPU 同 GetWholeSprite），key = 相對路徑 + tileSize。</summary>
        public Sprite GetFrameSprite(string framePath, float tileSize)
        {
            if (string.IsNullOrEmpty(framePath)) return null;
            string key = $"frame|{framePath}|{tileSize}";
            if (_sprites.TryGetValue(key, out var sp) && sp != null) return sp;
            var tex = GetFrameTexture(framePath);
            if (tex == null) return null;
            float ppu = TileNativePx / Mathf.Max(0.0001f, tileSize);
            sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
            _sprites[key] = sp;
            return sp;
        }

        /// <summary>把某動畫物件的所有幀載成 Sprite[]（依 frames 順序）。非動畫或任一幀失敗回 null。</summary>
        public Sprite[] GetAnimationFrames(CatalogItem item, float tileSize)
        {
            if (item == null || !item.IsAnimated) return null;
            var arr = new Sprite[item.frames.Count];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = GetFrameSprite(item.frames[i], tileSize);
                if (arr[i] == null) return null;
            }
            return arr;
        }

        /// <summary>地磚表第 index 格 sprite（256px 切格、top-left row-major）。</summary>
        public Sprite GetTileSprite(CatalogItem item, int index, float tileSize)
        {
            if (item == null) return null;
            string key = $"{item.id}|tile|{index}|{tileSize}";
            if (_sprites.TryGetValue(key, out var sp) && sp != null) return sp;
            var tex = GetTexture(item);
            if (tex == null) return null;

            int cell = TileNativePx;
            int cols = Mathf.Max(1, tex.width / cell);
            int rows = Mathf.Max(1, tex.height / cell);
            if (index < 0 || index >= cols * rows) return null;

            int col = index % cols;
            int row = index / cols;                       // row 0 = 最上排
            int rectX = col * cell;
            int rectY = tex.height - (row + 1) * cell;     // 紋理原點在左下
            float ppu = cell / Mathf.Max(0.0001f, tileSize);
            sp = Sprite.Create(tex, new Rect(rectX, rectY, cell, cell), new Vector2(0.5f, 0.5f), ppu);
            _sprites[key] = sp;
            return sp;
        }

        /// <summary>
        /// 由 tileId "catalogId#index" 解析 sprite。
        /// </summary>
        public Sprite ResolveTileSprite(string tileId, Catalog catalog, float tileSize)
        {
            if (string.IsNullOrEmpty(tileId)) return null;
            int hash = tileId.LastIndexOf('#');
            string catId = hash < 0 ? tileId : tileId.Substring(0, hash);
            int index = 0;
            if (hash >= 0) int.TryParse(tileId.Substring(hash + 1), out index);
            var item = catalog.Find(catId);
            return item == null ? null : GetTileSprite(item, index, tileSize);
        }

        /// <summary>
        /// 依不透明像素算出貼合的本地碰撞框（單位：世界單位 @ scale 1，相對 sprite 中心）。
        /// 只在載入時掃描一次並快取；runtime 真正參與碰撞的是一個普通 BoxCollider2D（最便宜）。
        /// </summary>
        public LocalBox GetAlphaLocalBox(CatalogItem item, float tileSize)
        {
            var none = new LocalBox { ok = false };
            if (item == null) return none;
            string key = $"{item.id}|alpha|{tileSize}";
            if (_alphaBoxes.TryGetValue(key, out var cached)) return cached;

            var tex = GetTexture(item);
            if (tex == null) { _alphaBoxes[key] = none; return none; }

            int w = tex.width, h = tex.height;
            Color32[] px = tex.GetPixels32();
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                int rowBase = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (px[rowBase + x].a > AlphaThreshold)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < 0) { _alphaBoxes[key] = none; return none; } // 全透明

            float ppu = TileNativePx / Mathf.Max(0.0001f, tileSize);
            float centerPxX = (minX + maxX + 1) * 0.5f;
            float centerPxY = (minY + maxY + 1) * 0.5f;
            var box = new LocalBox
            {
                ok = true,
                size = new Vector2((maxX - minX + 1) / ppu, (maxY - minY + 1) / ppu),
                offset = new Vector2((centerPxX - w * 0.5f) / ppu, (centerPxY - h * 0.5f) / ppu),
                canvas = new Vector2(w / ppu, h / ppu),
            };
            _alphaBoxes[key] = box;
            return box;
        }

        /// <summary>
        /// 取這個素材的「佔位遮罩」（給地上物碰撞貼合圖形用），解析度 = subdiv。取不到回 null。
        ///
        /// <para><b>永遠走同一條計算路徑</b>：先取得 <see cref="ObjectFootprint.BakeSubdiv"/> 解析度的遮罩
        /// —— catalog 烘好的直接用（<c>Project Tools → Sync Map Assets</c> 產生），沒有就**當場掃一次貼圖** ——
        /// 再降取樣到要求的解析度。烘與退路因此是<b>字面上相同的計算</b>，不會因為「這台機器有沒有烘過」
        /// 而得到不同的擋路範圍。</para>
        ///
        /// <para><b>不可以改成「拿不到烘焙就直接在目標解析度 Scan」</b>：`Downsample` 是 OR
        /// （4 顆子格有 1 顆實心就算擋），跟「整格算覆蓋率」的 `Scan` 結果差 10~38%。
        /// 混用會變成有烘/沒烘的機器擋路範圍不一樣，而且完全靜默——見 <see cref="ObjectFootprint.Downsample"/> 的警告。</para>
        ///
        /// <para>退路一定要留：catalog 有四個產生器，其中兩支 shell 版不會烘遮罩（見 <see cref="MapAssetCategories"/>），
        /// 沒有退路的話用 shell 同步過的專案會整批地上物不擋路——這種「靜默壞掉」正是本專案最貴的坑。</para>
        ///
        /// <para>結果（含 null）都會快取：null 代表「這張圖沒有可用遮罩」，不需要每個擺放重掃一次。</para>
        /// </summary>
        public FootprintMask GetFootprint(CatalogItem item, int subdiv)
        {
            if (item == null) return null;
            subdiv = ObjectFootprint.SnapSubdiv(subdiv);   // 收斂成 BakeSubdiv 的因數（1/2/4/8），見該函式說明
            string key = $"{item.id}|fp|{subdiv}";
            if (_footprints.TryGetValue(key, out var cached)) return cached;

            // ① 先拿到「烘焙解析度」的那一份（正典）。烘好的就用，沒有就當場掃。
            var full = item.footprint;
            if (full == null || !full.Ok || full.subdiv != ObjectFootprint.BakeSubdiv)
            {
                var tex = GetTexture(item);
                full = tex != null
                    ? ObjectFootprint.Scan(tex, ObjectFootprint.BakeSubdiv, ObjectFootprint.DefaultCoverage)
                    : null;
            }

            // ② 再降到要求的解析度（相同就原樣回）。
            var m = (full == null || full.subdiv == subdiv) ? full : ObjectFootprint.Downsample(full, subdiv);

            _footprints[key] = m;
            return m;
        }
    }
}
