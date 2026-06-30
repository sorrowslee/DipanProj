using System.Collections.Generic;

namespace DipanMapEditor.Data
{
    /// <summary>
    /// 素材目錄，由 sync_assets.sh 從主專案掃描生成 (catalog.json)。
    /// 編輯器右側調色盤、tile 筆刷、物件工具都讀這份來取得可放置素材。
    /// </summary>
    public class Catalog
    {
        public List<CatalogItem> items = new List<CatalogItem>();

        public CatalogItem Find(string id) => items.Find(i => i.id == id);

        /// <summary>
        /// 可建地圖的 module 清單：永遠含「Main」（主/共用場景，如初始山洞、邪佛廣場），
        /// 排第一個當預設；其餘為有素材的關卡 module，依名稱排序、去重。
        /// </summary>
        public System.Collections.Generic.List<string> EditableModules()
        {
            var set = new System.Collections.Generic.SortedSet<string>(System.StringComparer.Ordinal);
            foreach (var it in items)
                if (!string.IsNullOrEmpty(it.module) && it.module != "Main")
                    set.Add(it.module);
            var list = new System.Collections.Generic.List<string> { "Main" };   // Main 永遠可建
            list.AddRange(set);
            return list;
        }
    }

    public class CatalogItem
    {
        /// <summary>字串 ID = 相對路徑去副檔名（與主專案 / 未來 loader 共用）。</summary>
        public string id;
        /// <summary>StreamingAssets 內的相對路徑（含副檔名），供 runtime 載 PNG。</summary>
        public string path;
        /// <summary>分類 = 素材所在的上層資料夾名（Tiles / Environment / …）。</summary>
        public string category;
        /// <summary>所屬 module：「Main」= 共用，其餘 = 關卡名。用於依當前 module 過濾。</summary>
        public string module = "Main";
        /// <summary>原圖邊長（px），供顯示與世界尺寸換算。動畫物件 = 第一幀寬。</summary>
        public int pixelSize;
        /// <summary>Pixels Per Unit，預設 256（= 1 格）。</summary>
        public int ppu = 256;

        // ---- 動畫地上物（多張圖做成一個物件）----
        /// <summary>幀數；1（或缺欄）= 靜態單張。&gt;1 = 動畫物件。</summary>
        public int frameCount = 1;
        /// <summary>動畫各幀的相對路徑（依序，含第一幀）；靜態物件 = null。</summary>
        public List<string> frames;

        /// <summary>是否為動畫物件（多幀）。</summary>
        public bool IsAnimated => frameCount > 1 && frames != null && frames.Count > 1;
    }
}
