using UnityEngine;

namespace Dipan.Drama
{
    /// <summary>
    /// 提供 DramaTalkTable.csv 給頭像對話系統的小元件（與 <see cref="DramaTableProvider"/> 同套路）。
    /// 掛在場景的 GameManagers 上、把 <c>Assets/Data/DramaTalkTable.csv</c> 拖進 <see cref="talkCSV"/> 欄。
    /// 沒掛 / 沒拖時 DramaTalkDatabase 會退回讀 Resources（舊位置）並印錯誤指引。
    /// </summary>
    public class DramaTalkTableProvider : MonoBehaviour
    {
        [Tooltip("把 Assets/Data/DramaTalkTable.csv 拖進來")]
        public TextAsset talkCSV;
    }
}
