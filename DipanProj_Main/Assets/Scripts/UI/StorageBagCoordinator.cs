using UnityEngine;

namespace Dipan.UI
{
    /// <summary>
    /// 倉庫／背包開關與並排協調器（跨場景常駐，開場自動生成、零接線）。
    /// - K：開/關倉庫。B：開/關背包。
    /// - 只開其中一個 → 該面板置中；兩個都開 → 倉庫左、背包右（並排），方便互搬。
    /// 見 readme/STORAGE.md。
    /// </summary>
    public class StorageBagCoordinator : MonoBehaviour
    {
        public static StorageBagCoordinator Instance { get; private set; }

        public KeyCode storageKey = KeyCode.K;
        public KeyCode bagKey = KeyCode.B;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            var ui = UIManager.Instance;
            if (ui == null) return;

            // 新手教學強制階段：鎖住背包/倉庫快捷鍵，避免玩家亂開打斷引導。
            // 例外：佛燈教學的「按 B 開/關背包」步驟會放行 B 鍵（AllowBag），此時仍鎖倉庫 K。
            bool hotkeysFree = !TutorialManager.HardLock;
            if (hotkeysFree && Input.GetKeyDown(storageKey)) ui.Toggle<StoragePanel>();
            if ((hotkeysFree || TutorialManager.AllowBag) && Input.GetKeyDown(bagKey)) ui.Toggle<InventoryPanel>();

            // 依當前開啟狀態套用版面（idempotent，每幀套無妨）
            var store = ui.Get<StoragePanel>();
            var bag = ui.Get<InventoryPanel>();
            var scripts = ui.Get<ScriptsPanel>();
            bool storeOpen = store != null && store.IsOpen;
            bool bagOpen = bag != null && bag.IsOpen;
            bool scriptsOpen = scripts != null && scripts.IsOpen;
            // 背包在「倉庫」或「傳送門」任一開著時都靠右並排（讓左邊留給倉庫/傳送門）。
            bool bagPaired = bagOpen && (storeOpen || scriptsOpen);
            if (storeOpen) store.SetPairedLayout(storeOpen && bagOpen);
            if (bagOpen) bag.SetPairedLayout(bagPaired);
        }
    }

    /// <summary>開場前自動生出協調器（仿 UIBootstrap），不必手動掛。</summary>
    public static class StorageBagBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (StorageBagCoordinator.Instance != null) return;
            var go = new GameObject("[StorageBagCoordinator]");
            go.AddComponent<StorageBagCoordinator>();
        }
    }
}
