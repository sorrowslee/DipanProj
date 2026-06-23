using UnityEngine;

namespace Dipan.Drama
{
    /// <summary>
    /// 提供 DramaTable.csv 給劇情系統的小元件（與 ItemTableProvider 同套路）。
    /// 掛在場景的 GameManagers 上、把 <c>Assets/Data/DramaTable.csv</c> 拖進 <see cref="dramaCSV"/> 欄。
    /// 沒掛 / 沒拖時 DramaDatabase 會退回讀 Resources（舊位置）並印錯誤指引。
    /// </summary>
    public class DramaTableProvider : MonoBehaviour
    {
        [Tooltip("把 Assets/Data/DramaTable.csv 拖進來")]
        public TextAsset dramaCSV;
    }
}
