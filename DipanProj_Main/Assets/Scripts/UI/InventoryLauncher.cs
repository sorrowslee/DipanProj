using UnityEngine;

namespace Dipan.UI
{
    /// <summary>
    /// （已停用）早期測試用：首次啟動若背包空的就塞一批測試物品（讓你一開就看到 icon）。
    ///
    /// 現在新角色初始背包保持「**完全空**」——不再自動塞任何武器/雜物/藥水。
    /// 正式的撿道具/掉落/劇本系統已就緒，故移除種子邏輯。此元件留著不做事（可從場景移除或整支刪除）。
    ///
    /// 若日後要臨時塞測試裝備：git 記錄裡有舊版種子碼可還原，或在 <see cref="Start"/> 內自行 <c>InventorySystem.Instance.AddItem(id)</c>。
    /// </summary>
    public class InventoryLauncher : MonoBehaviour
    {
        void Start()
        {
            // no-op：新角色初始背包完全空，不再自動塞測試物品。
        }
    }
}
