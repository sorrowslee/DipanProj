using UnityEngine;

/// <summary>
/// 提供 `DropTable.csv` 給掉落系統的小元件（與 SceneFxTableProvider／ItemTableProvider 同套路）。
/// 掛在場景的 GameManagers 上，把 <c>Assets/Data/DropTable.csv</c> 拖進 <see cref="dropCSV"/> 欄。
///
/// <para>為什麼需要：<see cref="DropTable"/> 是靜態類別、場景上沒有可拖檔的物件，
/// 所以由這個被動 provider 持有 CSV 參照，DropTable 載入時 FindObjectOfType 取用。
/// 沒掛／沒拖時 DropTable 會印一則 warning 並讓所有怪不掉寶（不會靜靜壞掉）。</para>
/// </summary>
public class DropTableProvider : MonoBehaviour
{
    [Tooltip("把 Assets/Data/DropTable.csv 拖進來")]
    public TextAsset dropCSV;
}
