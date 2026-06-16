using System.Collections.Generic;
using UnityEngine;
using DipanMapEditor.Data;

namespace DipanMapEditor.Core
{
    /// <summary>可放置的地上物（來源素材）。assetId = catalog id。</summary>
    public struct PlaceableObject
    {
        public string assetId;
        public CatalogItem source;
        public string category;
    }

    /// <summary>
    /// 把 catalog 中「地上物」（Environment 分類 = 關卡的 Environment/ 資料夾）
    /// 列成可放置清單，依 Main + 當前 module 過濾。
    /// </summary>
    public static class ObjectService
    {
        public static bool IsObjectCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return false;
            return category.ToLowerInvariant() == "environment";
        }

        public static List<PlaceableObject> BuildObjects(Catalog catalog, string module)
        {
            var list = new List<PlaceableObject>();
            if (catalog == null) return list;
            foreach (var item in catalog.items)
            {
                if (!IsObjectCategory(item.category)) continue;
                if (item.module != "Main" && item.module != module) continue;
                list.Add(new PlaceableObject { assetId = item.id, source = item, category = item.category });
            }
            return list;
        }
    }
}
