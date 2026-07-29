namespace Dipan.Inventory
{
    /// <summary>
    /// 「一件裝備有幾個鑲嵌孔」的唯一查詢入口（seam）。
    ///
    /// 現況：裝備還沒有「孔位數」這個屬性，所以一律回傳 0 ＝ 鍛造介面的六個孔全部上鎖（掛鎖鏈）。
    ///
    /// 【將來要接上時只要動這裡】
    ///   1. ItemTable.csv 加一欄 SocketCount（0~6）。
    ///   2. ItemData 加 `public int SocketCount;`，ItemDatabase 解析那一欄。
    ///   3. 把下面 Of() 的內容換成 `return Mathf.Clamp(d.SocketCount, 0, MaxSockets);`
    /// 介面端（ForgingPanel / ForgeSocketGrid）完全不必改——它們只問這個函式。
    ///
    /// 見 readme/FORGING.md。
    /// </summary>
    public static class ForgeSockets
    {
        /// <summary>鍛造介面實際擺出來的孔位上限（左三右三）。</summary>
        public const int MaxSockets = 6;

        /// <summary>
        /// 開發測試用：≥0 時無視裝備本身的孔位數，強制開這麼多孔。-1 = 關閉（正常走 Of()）。
        /// 想在還沒有 SocketCount 欄位時先看解鎖效果，就在 Console 或作弊面板設
        /// <c>Dipan.Inventory.ForgeSockets.DebugCount = 4;</c>
        /// </summary>
        public static int DebugCount = -1;

        /// <summary>這件裝備有幾個鑲嵌孔（0~MaxSockets）。非裝備、查不到都回 0。</summary>
        public static int Of(ItemData d)
        {
            if (DebugCount >= 0) return UnityEngine.Mathf.Clamp(DebugCount, 0, MaxSockets);
            if (d == null || !d.IsEquippable) return 0;

            // TODO(孔位)：等 ItemTable 加了 SocketCount 欄位就改成讀它。現在一律 0 ＝ 全部上鎖。
            return 0;
        }

        /// <summary>依物品 ID 查孔位數（查不到回 0）。</summary>
        public static int Of(int itemId)
        {
            if (itemId <= 0 || InventorySystem.Instance == null) return 0;
            return Of(InventorySystem.Instance.GetData(itemId));
        }
    }
}
