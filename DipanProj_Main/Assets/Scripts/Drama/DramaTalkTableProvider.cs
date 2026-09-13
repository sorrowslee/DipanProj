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

        // 立繪固定微調表（選填）：沒拖就是每張立繪都走自動對齊、不做額外偏移。見 PortraitTable。
        [Tooltip("把 Assets/Data/PortraitTable.csv 拖進來（選填：每張立繪的固定縮放/位移）")]
        public TextAsset portraitCSV;
    }
}
