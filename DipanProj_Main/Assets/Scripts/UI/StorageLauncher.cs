using UnityEngine;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 測試用：首次啟動若倉庫是空的就塞幾個測試物品，方便看搬運。
    /// 開關鍵（K）已移到 StorageBagCoordinator（自動生成），這裡只負責種子。可掛到場景任一物件上。
    /// 正式有了「靠近倉庫才開」的互動後可刪除整支。
    /// </summary>
    public class StorageLauncher : MonoBehaviour
    {
        public bool seedTestItems = true;

        void Start()
        {
            if (!seedTestItems) return;
            var st = StorageSystem.Instance;
            if (st.HasAnyItem()) return;
            var p0 = st.Page(0);    // 種到第 1 頁
            p0.AddItem(101, 500);   // 銅錢
            p0.AddItem(102, 30);    // 卷軸
            p0.AddItem(103, 20);    // 符紙
            st.Page(1).AddItem(1, 1);   // 第 2 頁放一把武器，方便驗證切頁
        }
    }
}
