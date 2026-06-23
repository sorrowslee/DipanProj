using UnityEngine;

namespace Dipan.Inventory
{
    /// <summary>
    /// 提供 ItemTable.csv 給背包系統的小元件。把它掛在場景的 GameManagers 上、
    /// 把 <c>Assets/Data/ItemTable.csv</c> 拖進 <see cref="itemCSV"/> 欄即可
    /// ——與 WeaponManager / MapTable / VfxManager 等「Assets/Data + 拖 TextAsset」的慣例一致。
    ///
    /// 為什麼需要這個元件：<see cref="InventorySystem"/> 是自動生成的常駐單例、場景上沒有可拖檔的物件，
    /// 所以由這個被動的 provider 持有 CSV 參照，InventorySystem 載入時 FindObjectOfType 取用。
    /// 沒掛 / 沒拖時 InventorySystem 會印清楚錯誤指引（見 ItemDatabase）。
    /// </summary>
    public class ItemTableProvider : MonoBehaviour
    {
        [Tooltip("把 Assets/Data/ItemTable.csv 拖進來")]
        public TextAsset itemCSV;
    }
}
