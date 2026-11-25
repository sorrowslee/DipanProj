using UnityEngine;

/// <summary>
/// 提供隨從的兩張表給 <see cref="FamiliarTable"/>（與 ShadowAnchorTableProvider 同套路，2026-11-24）。
/// 掛在場景的 GameManagers 上，把 <c>Assets/Data/FamiliarTable.csv</c>、<c>Assets/Data/FamiliarAnchorTable.csv</c> 拖進來。
/// 沒掛時：編輯器下直接讀 Assets/Data 檔案並印提醒；build 裡隨從整層不出現。見 readme/BLOODLINE.md §5b〈`FamiliarStyle`：隨從〉。
/// </summary>
public class FamiliarTableProvider : MonoBehaviour
{
    [Tooltip("把 Assets/Data/FamiliarTable.csv 拖進來（隨從外觀與行為參數）")]
    public TextAsset familiarCSV;
    [Tooltip("把 Assets/Data/FamiliarAnchorTable.csv 拖進來（宿主每個動作每一幀的停棲點）")]
    public TextAsset familiarAnchorCSV;
}
