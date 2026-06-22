using UnityEngine;

namespace Dipan.UI
{
    /// <summary>
    /// 自動建立 UIManager：在任何場景載入前跑一次，零手動接線。
    /// 因為 UIManager 是 DontDestroyOnLoad 的常駐單例，這裡只需在「整個遊戲啟動時」確保它存在一次。
    ///
    /// 這也讓 UI 系統天生支援多場景：不管第一個載入的是主選單還是遊戲場景，UIManager 都已就緒並跨場景存活。
    /// 若你偏好手動把 UIManager 放進場景也可以——UIManager.Awake 會處理重複實例（多的會自毀）。
    /// </summary>
    public static class UIBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (UIManager.Instance != null) return;
            var go = new GameObject("[UIManager]");
            go.AddComponent<UIManager>();   // Awake 內會設 Instance + DontDestroyOnLoad + 建分層 Canvas
        }
    }
}
