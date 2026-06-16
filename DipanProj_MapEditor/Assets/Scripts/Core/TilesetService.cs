using System.Collections.Generic;
using UnityEngine;
using DipanMapEditor.Data;

namespace DipanMapEditor.Core
{
    /// <summary>一格可畫的地磚：來源素材 + 切格 index。tileId 格式 "catalogId#index"。</summary>
    public struct PaintableTile
    {
        public string tileId;        // 寫進 .dipanmap 的字串 ID
        public CatalogItem source;   // 來源地磚表
        public int index;            // 第幾格
        public string category;
    }

    /// <summary>
    /// 把 catalog 中「地磚表」（Tiles 分類 = 關卡的 Tiles/ 資料夾，放 Scenario 地磚 texture）
    /// 展開成可畫的 tile 清單。其他分類（Environment 的地上物、家具、角色、怪物）
    /// 不在此列，走 M3 物件擺放。
    /// </summary>
    public static class TilesetService
    {
        public static bool IsTilesetCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return false;
            return category.ToLowerInvariant() == "tiles";
        }

        /// <summary>只列出「Main + 指定 module」的地磚，避免跨 module 混用。</summary>
        public static List<PaintableTile> BuildTiles(Catalog catalog, string module)
        {
            var list = new List<PaintableTile>();
            if (catalog == null) return list;

            foreach (var item in catalog.items)
            {
                if (!IsTilesetCategory(item.category)) continue;
                if (item.module != "Main" && item.module != module) continue;
                int count = SpriteCache.GetTileCount(item);
                for (int i = 0; i < count; i++)
                {
                    list.Add(new PaintableTile
                    {
                        tileId = $"{item.id}#{i}",
                        source = item,
                        index = i,
                        category = item.category,
                    });
                }
            }
            return list;
        }

        /// <summary>解析 "catalogId#index" → (catalogId, index)。</summary>
        public static bool ParseTileId(string tileId, out string catalogId, out int index)
        {
            catalogId = null; index = 0;
            if (string.IsNullOrEmpty(tileId)) return false;
            int hash = tileId.LastIndexOf('#');
            if (hash < 0) { catalogId = tileId; index = 0; return true; }
            catalogId = tileId.Substring(0, hash);
            return int.TryParse(tileId.Substring(hash + 1), out index);
        }

        /// <summary>由 tileId 取得對應 sprite（透過 catalog + SpriteCache）。</summary>
        public static Sprite ResolveSprite(string tileId, Catalog catalog, float tileSize)
        {
            if (!ParseTileId(tileId, out string catId, out int index)) return null;
            var item = catalog.Find(catId);
            if (item == null) return null;
            return SpriteCache.GetTileSprite(item, index, tileSize);
        }
    }
}
