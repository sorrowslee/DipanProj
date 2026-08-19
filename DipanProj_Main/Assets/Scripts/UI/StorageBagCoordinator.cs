using UnityEngine;

namespace Dipan.UI
{
    /// <summary>
    /// 倉庫／背包／鍛造開關與並排協調器（跨場景常駐，開場自動生成、零接線）。
    /// - K：開/關倉庫。B：開/關背包。Y：開/關鍛造（暫用熱鍵，之後改由鐵匠 NPC 互動開啟）。
    /// - 只開其中一個 → 該面板置中；與背包同時開 → 另一個面板靠左、背包靠右（並排），方便互搬／互拖。
    /// 見 readme/STORAGE.md、FORGING.md。
    /// </summary>
    public class StorageBagCoordinator : MonoBehaviour
    {
        public static StorageBagCoordinator Instance { get; private set; }

        public KeyCode storageKey = KeyCode.K;
        public KeyCode bagKey = KeyCode.B;
        public KeyCode forgeKey = KeyCode.Y;   // TODO(NPC)：鐵匠做好後改走互動點，這顆熱鍵可拿掉

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

            // 新手教學強制階段：鎖住背包/倉庫/鍛造快捷鍵，避免玩家亂開打斷引導。
            // 例外：佛燈教學的「按 B 開/關背包」步驟會放行 B 鍵（AllowBag），此時仍鎖倉庫 K 與鍛造 Y。
            //
            // 血統變身表演期間也要鎖（世界演出 + 立繪揭示面板兩段都算）：這三個面板會整片蓋在表演上面，
            // 而且是 Window 層、關掉時會把表演的視線打斷。IsPerforming 就是這兩段的單一真相。
            // ⚠ 這裡刻意**不用** IsGameplayInputBlocked：背包自己開著就會讓它為真，
            //    那樣一來按 B 就關不掉背包了。
            bool performing = Dipan.Gacha.BloodlineSystem.IsPerforming;
            bool hotkeysFree = !TutorialManager.HardLock && !performing;
            if (hotkeysFree && Input.GetKeyDown(storageKey)) ui.Toggle<StoragePanel>();
            if (hotkeysFree && Input.GetKeyDown(forgeKey)) ui.Toggle<ForgingPanel>();
            if ((hotkeysFree || (TutorialManager.AllowBag && !performing))
                && Input.GetKeyDown(bagKey)) ui.Toggle<InventoryPanel>();

            // 依當前開啟狀態套用版面（idempotent，每幀套無妨）
            var store = ui.Get<StoragePanel>();
            var bag = ui.Get<InventoryPanel>();
            var scripts = ui.Get<ScriptsPanel>();
            var forge = ui.Get<ForgingPanel>();
            bool storeOpen = store != null && store.IsOpen;
            bool bagOpen = bag != null && bag.IsOpen;
            bool scriptsOpen = scripts != null && scripts.IsOpen;
            bool forgeOpen = forge != null && forge.IsOpen;
            // 背包在「倉庫」「傳送門」「鍛造」任一開著時都靠右並排（讓左邊留給那個面板）。
            bool bagPaired = bagOpen && (storeOpen || scriptsOpen || forgeOpen);
            if (storeOpen) store.SetPairedLayout(storeOpen && bagOpen);
            if (forgeOpen) forge.SetPairedLayout(bagOpen);
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
