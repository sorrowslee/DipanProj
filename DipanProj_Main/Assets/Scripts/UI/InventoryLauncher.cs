using UnityEngine;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 測試用：首次啟動若背包是空的就塞一批測試物品（讓你一開就看到 icon）。
    /// 開關鍵（B）已移到 StorageBagCoordinator（自動生成），這裡只負責種子。可掛到場景任一物件上。
    /// 正式有了撿道具/掉落系統後可刪除整支。
    /// </summary>
    public class InventoryLauncher : MonoBehaviour
    {
        public bool seedTestItems = true;

        void Start()
        {
            if (!seedTestItems) return;
            var inv = InventorySystem.Instance;

            // 測試武器白名單：既有 1~9、11~13，加上本輪保留的 21/22/24/25/27/28。
            // 原本「全空才塞」會踩到：SaveManager(執行序 -500)開場先載入角色、RestoreState 先清空再還原「存檔裡的舊背包」，
            // 舊角色存檔沒有新武器(如御靈水晶 13) → 背包非空 → 這裡跳過 → 測試武器一直不見/時有時無。
            // 本元件在 SaveManager 之後(Start，序 0)跑，改成「補齊」就能在還原後把缺的測試武器補回。
            // （純測試用；HasAnywhere 含裝備欄，避免已裝備的又被重複補一份。正式有撿道具系統後可刪整支。）
            int[] testWeaponIds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 13, 21, 22, 24, 25, 27, 28, 29 };
            foreach (int id in testWeaponIds)
            {
                if (!inv.HasAnywhere(id)) inv.AddItem(id);
            }

            // 雜物只在「完全沒有」時給一次（避免每次載入都補到滿）。
            if (!inv.Has(101)) inv.AddItem(101, 250); // 銅錢 x250
            if (!inv.Has(102)) inv.AddItem(102, 5);   // 卷軸 x5
            if (!inv.Has(103)) inv.AddItem(103, 12);  // 符紙 x12
            TopUp(inv, 201, 99);   // 小回血瓶 補到 x99（作弊；堆疊上限 99）
            TopUp(inv, 202, 99);   // 小回魔瓶 補到 x99（作弊）
        }

        // 作弊/測試：把某物品在背包裡補到指定數量（已達或超過就不動）。
        static void TopUp(InventorySystem inv, int itemId, int target)
        {
            int need = target - inv.CountOf(itemId);
            if (need > 0) inv.AddItem(itemId, need);
        }
    }
}
