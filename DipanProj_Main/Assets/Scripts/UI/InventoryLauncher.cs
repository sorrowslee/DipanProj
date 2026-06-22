using UnityEngine;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 測試用：按 B 開關背包；首次啟動若背包是空的就塞一批測試物品（讓你一開就看到 icon）。
    /// 把它掛到場景任一物件上、按 Play。正式有了撿道具/掉落系統後可刪除種子物品那段。
    /// </summary>
    public class InventoryLauncher : MonoBehaviour
    {
        public KeyCode toggleKey = KeyCode.B;
        public bool seedTestItems = true;

        void Start()
        {
            if (!seedTestItems) return;
            var inv = InventorySystem.Instance;
            if (inv.HasAnyItem()) return;   // 已有東西就不重複塞（換場景/換圖也不會重塞）

            // 12 把武器（ItemTable ID 1~12，對應 WeaponTable 1~12）
            for (int id = 1; id <= 12; id++) inv.AddItem(id);
            // 雜物
            inv.AddItem(101, 250); // 銅錢 x250
            inv.AddItem(102, 5);   // 卷軸 x5
            inv.AddItem(103, 12);  // 符紙 x12
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey) && UIManager.Instance != null)
                UIManager.Instance.Toggle<InventoryPanel>();
        }
    }
}
