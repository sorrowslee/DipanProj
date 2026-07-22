using UnityEngine;

/// <summary>
/// 提供 ScreenFxTable.csv 給全螢幕過場特效系統的小元件（與 ItemTableProvider / DramaTableProvider 同套路）。
/// 掛在場景的 GameManagers 上，把 <c>Assets/Data/ScreenFxTable.csv</c> 拖進 <see cref="screenFxCSV"/> 欄。
///
/// 為什麼需要：<see cref="ScreenFxTable"/> 是靜態類別、場景上沒有可拖檔的物件，
/// 所以由這個被動 provider 持有 CSV 參照，ScreenFxTable 載入時 FindObjectOfType 取用。
/// 沒掛 / 沒拖時 ScreenFxTable 會退回讀 Resources（舊位置），再不行才用控制器預設。
/// </summary>
public class ScreenFxTableProvider : MonoBehaviour
{
    [Tooltip("把 Assets/Data/ScreenFxTable.csv 拖進來")]
    public TextAsset screenFxCSV;
}
