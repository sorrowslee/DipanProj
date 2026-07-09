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
            if (inv.HasAnyItem()) return;   // 已有東西就不重複塞（換場景/換圖也不會重塞）

            // 武器（ItemTable ID 1~13，對應 WeaponTable；13=御靈水晶召喚，測試用）
            for (int id = 1; id <= 13; id++) inv.AddItem(id);
            // 雜物
            inv.AddItem(101, 250); // 銅錢 x250
            inv.AddItem(102, 5);   // 卷軸 x5
            inv.AddItem(103, 12);  // 符紙 x12
        }
    }
}
