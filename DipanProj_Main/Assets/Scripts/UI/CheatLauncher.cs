using UnityEngine;

namespace Dipan.UI
{
    /// <summary>
    /// 作弊面板的開關入口（測試用）。開場自動生成、零接線（仿 SettingsLauncher / SaveBootstrap）。
    ///
    /// 按 <b>L</b> 開/關 <see cref="CheatPanel"/>。選 L 是因為 O 已被設定面板當備用鍵、P 已被效能面板占用，
    /// 且 L 好按（不像功能鍵在 Mac 上不順手）。要改鍵改 <see cref="openKey"/> 即可。
    ///
    /// 注意：這是作弊/除錯工具。若正式出貨要隱藏，可把本檔移到 Assets/Editor（就不會進 build），
    /// 或在 Update 外面包 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`。
    /// </summary>
    public class CheatLauncher : MonoBehaviour
    {
        public KeyCode openKey = KeyCode.L;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            var go = new GameObject("[CheatLauncher]");
            go.AddComponent<CheatLauncher>();
            DontDestroyOnLoad(go);
        }

        void Update()
        {
            var ui = UIManager.Instance;
            if (ui == null) return;
            if (Input.GetKeyDown(openKey))
                ui.Toggle<CheatPanel>();
        }
    }
}
