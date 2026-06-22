using UnityEngine;

namespace Dipan.UI
{
    /// <summary>
    /// 測試用：按一個鍵開關 UIDemoPanel，驗證 UI 底層。
    /// 把它掛到場景任一物件上、按 Play、按 U 試開關。驗證完可連同 UIDemoPanel 一起刪。
    /// </summary>
    public class UIDemoLauncher : MonoBehaviour
    {
        public KeyCode toggleKey = KeyCode.U;

        void Update()
        {
            if (Input.GetKeyDown(toggleKey) && UIManager.Instance != null)
                UIManager.Instance.Toggle<UIDemoPanel>();
        }
    }
}
