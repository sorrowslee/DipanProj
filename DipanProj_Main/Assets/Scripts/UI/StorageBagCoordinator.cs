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

            if (Input.GetKeyDown(storageKey)) ui.Toggle<StoragePanel>();
            if (Input.GetKeyDown(bagKey)) ui.Toggle<InventoryPanel>();

            // 依當前開啟狀態套用版面（idempotent，每幀套無妨）
            var store = ui.Get<StoragePanel>();
            var bag = ui.Get<InventoryPanel>();
            bool storeOpen = store != null && store.IsOpen;
            bool bagOpen = bag != null && bag.IsOpen;
            bool paired = storeOpen && bagOpen;
            if (storeOpen) store.SetPairedLayout(paired);
            if (bagOpen) bag.SetPairedLayout(paired);
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
