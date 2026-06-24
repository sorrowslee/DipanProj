using UnityEngine;

namespace Dipan.UI
{
    /// <summary>
    /// 設定面板的開啟入口。開場自動生成、零接線（仿 SaveBootstrap）。
    ///
    /// 主要：向 UIManager 註冊「沒有任何視窗開著時，按 <b>ESC</b> 就開設定面板」。
    /// ESC 由 UIManager 在同一個分支統一處理（有視窗→關最上層、沒視窗→開設定），所以設定開著時 ESC 會關它、
    /// 不會發生「關掉又重開」。另保留 <b>O</b> 作為備用快捷鍵（直接 Toggle）。
    /// 之後若有「暫停選單 / 主選單」按鈕，也可直接呼叫 UIManager.Instance.Open&lt;SettingsPanel&gt;()。
    /// </summary>
    public class SettingsLauncher : MonoBehaviour
    {
        public KeyCode openKey = KeyCode.O;   // 備用快捷鍵（ESC 由 UIManager 統一處理）

        bool _registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            var go = new GameObject("[SettingsLauncher]");
            go.AddComponent<SettingsLauncher>();
            DontDestroyOnLoad(go);
        }

        void Update()
        {
            var ui = UIManager.Instance;
            if (ui == null) return;

            // 一次性註冊 ESC 根面板（UIManager 可能比本元件晚一點才就緒，故在 Update 裡確保註冊到）。
            if (!_registered)
            {
                ui.SetEscapeRootPanel<SettingsPanel>();
                _registered = true;
            }

            if (Input.GetKeyDown(openKey))
                ui.Toggle<SettingsPanel>();
        }
    }
}
