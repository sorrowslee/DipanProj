namespace Dipan.Inventory
{
    /// <summary>
    /// 鑲嵌孔的查詢工具。
    ///
    /// 【重要觀念】孔位**不是**寫在 ItemTable 裡的欄位，而是「這一件」自己的資料：
    /// 同一把武器 ID 掉落兩次，可能一把 2 孔、一把 5 孔，而且開的位置還不一樣
    /// （**隨機位置，不是前 N 個**，例如 2 孔武器可能開的是第 1、4 孔）。
    /// 所以孔數一律從 <see cref="ItemInstance"/> 讀，不是從 <see cref="ItemData"/> 讀。
    /// 孔位是在物品產生的那一刻由 <see cref="ItemManager"/> ＋ <see cref="Dipan.Rules.RandomRules"/> 骰出來的。
    ///
    /// 見 readme/GEM_SOCKET.md。
    /// </summary>
    public static class ForgeSockets
    {
        /// <summary>鍛造介面實際擺出來的孔位上限（左三右三）。</summary>
        public const int MaxSockets = ItemInstance.SocketMax;

        /// <summary>
        /// 開發測試用：≥0 時無視這一件本身的孔位佈局，強制當作前 N 個孔都開著。-1 = 關閉（正常讀實例）。
        /// 在 Console 或作弊面板設 <c>Dipan.Inventory.ForgeSockets.DebugCount = 4;</c> 即可。
        /// </summary>
        public static int DebugCount = -1;

        /// <summary>關掉 Domain Reload 後 static 不會歸零，進 Play 時由 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode() => DebugCount = -1;

        /// <summary>這一件開了幾個孔。</summary>
        public static int CountOf(ItemInstance inst)
        {
            if (DebugCount >= 0) return UnityEngine.Mathf.Clamp(DebugCount, 0, MaxSockets);
            return inst != null ? inst.UnlockedCount : 0;
        }

        /// <summary>這一件的第 i 個孔是不是開著的。</summary>
        public static bool IsUnlocked(ItemInstance inst, int i)
        {
            if (i < 0 || i >= MaxSockets) return false;
            if (DebugCount >= 0) return i < UnityEngine.Mathf.Clamp(DebugCount, 0, MaxSockets);
            return inst != null && inst.IsUnlocked(i);
        }
    }
}
