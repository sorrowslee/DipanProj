using UnityEngine;

namespace Dipan.Localization
{
    /// <summary>
    /// 提供 LanguageTable.csv 給多語系系統的小元件（與 ItemTableProvider / DramaTableProvider 同套路）。
    /// 掛在場景的 GameManagers 上，把 <c>Assets/Data/LanguageTable.csv</c> 拖進 <see cref="languageCSV"/> 欄。
    ///
    /// 為什麼需要：<see cref="Language"/> 是靜態類別、場景上沒有可拖檔的物件，
    /// 所以由這個被動 provider 持有 CSV 參照，Language 載入時 FindObjectOfType 取用。
    /// 沒掛 / 沒拖時字串會以 [lang:id] 佔位顯示（並印警告指引）。
    /// </summary>
    public class LanguageTableProvider : MonoBehaviour
    {
        [Tooltip("把 Assets/Data/LanguageTable.csv 拖進來")]
        public TextAsset languageCSV;
    }
}
