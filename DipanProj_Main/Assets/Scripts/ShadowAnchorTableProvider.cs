using UnityEngine;

/// <summary>
/// 提供 ShadowAnchorTable.csv 給影子系統的小元件（與 NpcTableProvider 同套路）。
/// 掛在場景的 GameManagers 上、把 <c>Assets/Data/ShadowAnchorTable.csv</c> 拖進 <see cref="shadowAnchorCSV"/> 欄。
/// 沒掛時：編輯器下 ShadowAnchorTable 會直接讀 Assets/Data 檔案並印提醒；build 裡則全部退回自動量測。
/// 表的產生：Project Tools → 角色 → 計算影子錨點（見 readme/SHADOW.md）。
/// </summary>
public class ShadowAnchorTableProvider : MonoBehaviour
{
    [Tooltip("把 Assets/Data/ShadowAnchorTable.csv 拖進來")]
    public TextAsset shadowAnchorCSV;
}
