using UnityEngine;

/// <summary>
/// 提供 NpcTable.csv 給 NPC 系統的小元件（與 DramaTableProvider 同套路）。
/// 掛在場景的 GameManagers 上、把 <c>Assets/Data/NpcTable.csv</c> 拖進 <see cref="npcCSV"/> 欄。
/// 沒掛 / 沒拖時 NpcDatabase 會退回讀 Resources 並印錯誤指引。
/// </summary>
public class NpcTableProvider : MonoBehaviour
{
    [Tooltip("把 Assets/Data/NpcTable.csv 拖進來")]
    public TextAsset npcCSV;
}
