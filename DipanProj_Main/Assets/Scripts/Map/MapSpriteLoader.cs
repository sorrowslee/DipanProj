using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Dipan.MapRuntime
{
    /// <summary>
    /// runtime 從磁碟載 PNG → Texture2D（Point 濾鏡，像素風）→ Sprite，並快取。
    /// 關鍵：一律以 PPU = 256/tileSize 建 sprite（= 編輯器尺寸），不依賴主專案 PNG 的匯入 PPU，
    /// 否則家具大小會與 .dipanmap 內的座標/scale 對不上。
    /// 另提供「依不透明像素貼合」的本地碰撞框（給家具 BoxCollider2D 用）。
    /// </summary>
    public class MapSpriteLoader
    {
        public const int TileNativePx = 256;
        const byte AlphaThreshold = 10;   // alpha 低於此值視為透明（去背邊）

        readonly string _assetRoot;
        readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        readonly Dictionary<string, LocalBox> _alphaBoxes = new Dictionary<string, LocalBox>();

        public struct LocalBox { public bool ok; public Vector2 size; public Vector2 offset; }

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
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));    // 自動調整尺寸，CPU 可讀
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
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
            };
            _alphaBoxes[key] = box;
            return box;
        }
    }
}
