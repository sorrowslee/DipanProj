using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DipanMapEditor.Data;
using DipanMapEditor.IO;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 從 StreamingAssets/MapAssets 載 PNG → Texture2D（Point 濾鏡，像素風用），
    /// 並依 256px 切出子 sprite（地磚表）或整張 sprite（單體物件），全部快取。
    /// </summary>
    public static class SpriteCache
    {
        public const int TileNativePx = 256; // 1 格的原生像素（依主專案 PPU 256）

        static readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

        public static void Clear()
        {
            _textures.Clear();
            _sprites.Clear();
        }

        /// <summary>載入（並快取）某 catalog 素材的整張貼圖。失敗回 null。</summary>
        public static Texture2D GetTexture(CatalogItem item)
        {
            if (item == null) return null;
            if (_textures.TryGetValue(item.id, out var tex) && tex != null) return tex;

            string path = CatalogLoader.ResolveAssetPath(item);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SpriteCache] 找不到圖檔：{path}");
                return null;
            }
            byte[] bytes = File.ReadAllBytes(path);
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes);              // 自動調整尺寸
            tex.filterMode = FilterMode.Point; // 像素風：不模糊
            tex.wrapMode = TextureWrapMode.Clamp;
            _textures[item.id] = tex;
            return tex;
        }

        /// <summary>
        /// 取得「整張」sprite（單體物件用）。PPU 讓 sprite 世界尺寸 = pixelSize/256 * tileSize 的等比。
        /// </summary>
        public static Sprite GetWholeSprite(CatalogItem item, float tileSize)
        {
            string key = $"{item.id}|whole|{tileSize}";
            if (_sprites.TryGetValue(key, out var sp) && sp != null) return sp;
            var tex = GetTexture(item);
            if (tex == null) return null;
            float ppu = TileNativePx / Mathf.Max(0.0001f, tileSize);
            sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                               new Vector2(0.5f, 0.5f), ppu);
            _sprites[key] = sp;
            return sp;
        }

        /// <summary>
        /// 取得地磚表中第 index 格的 sprite（256px 切格、top-left row-major）。
        /// PPU = 256 / tileSize → 每格世界尺寸恰好 = tileSize（貼合 Tilemap cell）。
        /// </summary>
        public static Sprite GetTileSprite(CatalogItem item, int index, float tileSize)
        {
            string key = $"{item.id}|tile|{index}|{tileSize}";
            if (_sprites.TryGetValue(key, out var sp) && sp != null) return sp;

            var tex = GetTexture(item);
            if (tex == null) return null;

            int cell = TileNativePx;
            int cols = Mathf.Max(1, tex.width / cell);
            int rows = Mathf.Max(1, tex.height / cell);
            int count = cols * rows;
            if (index < 0 || index >= count) return null;

            int col = index % cols;
            int row = index / cols;                 // row 0 = 最上排
            int rectX = col * cell;
            int rectY = tex.height - (row + 1) * cell;  // 紋理原點在左下

            float ppu = cell / Mathf.Max(0.0001f, tileSize);
            sp = Sprite.Create(tex, new Rect(rectX, rectY, cell, cell),
                               new Vector2(0.5f, 0.5f), ppu);
            _sprites[key] = sp;
            return sp;
        }

        /// <summary>地磚表可切出的格數（cols×rows）。</summary>
        public static int GetTileCount(CatalogItem item)
        {
            var tex = GetTexture(item);
            if (tex == null) return 0;
            int cell = TileNativePx;
            return Mathf.Max(1, tex.width / cell) * Mathf.Max(1, tex.height / cell);
        }
    }
}
