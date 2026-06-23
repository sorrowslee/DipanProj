using UnityEngine;

namespace Dipan.Save
{
    /// <summary>
    /// 開場前自動生出 SaveManager（仿 UIBootstrap），零手動接線。
    /// SaveManager 是 DontDestroyOnLoad 常駐單例，只需在遊戲啟動時確保存在一次。
    /// 若你偏好手動把 SaveManager 放進場景也行——它的 Awake 會處理重複實例。
    /// </summary>
    public static class SaveBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (SaveManager.Instance != null) return;
            var go = new GameObject("[SaveManager]");
            go.AddComponent<SaveManager>();   // Awake：設 Instance + DontDestroyOnLoad + 載入名冊
        }
    }
}
