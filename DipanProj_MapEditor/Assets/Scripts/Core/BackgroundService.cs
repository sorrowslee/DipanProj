using System.Collections.Generic;
using DipanMapEditor.Data;

namespace DipanMapEditor.Core
{
    /// <summary>列出 catalog 中「背景圖」（Background 分類），依 Main + 當前 module 過濾。</summary>
    public static class BackgroundService
    {
        public static bool IsBackgroundCategory(string category)
            => !string.IsNullOrEmpty(category) && category.ToLowerInvariant() == "background";

        public static List<CatalogItem> BuildBackgrounds(Catalog catalog, string module)
        {
            var list = new List<CatalogItem>();
            if (catalog == null) return list;
            foreach (var it in catalog.items)
                if (IsBackgroundCategory(it.category) && (it.module == "Main" || it.module == module))
                    list.Add(it);
            return list;
        }
    }
}
