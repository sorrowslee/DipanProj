using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dipan.Save;
using Dipan.UI;

namespace Dipan.Flow
{
    /// <summary>
    /// 遊戲總流程指揮（跨場景常駐單例，全程式建構、零手動接線，仿 SaveManager / UIManager）。
    ///
    /// 開機不再一進場就跳進關卡，而是：**標題畫面 → 三欄存讀檔畫面 → 玩家選擇**。
    /// - 新建遊戲：在該欄建角 →（有開場場景就播開場鏈，最後接到邪佛廣場；沒有就直接進廣場）。
    /// - 繼續遊戲：載入該欄存檔 → 直接進邪佛廣場（中央出生）。
    /// - 覆蓋 / 刪除：見 SaveSlotPanel（呼叫 SaveManager 的槽位 API）。
    ///
    /// 開機時（見 GameFlowBootstrap）先把 SaveManager.SuppressAutoLoad 與 MapManager.SuppressAutoStart 設 true，
    /// 讓「載哪個進度、進哪張圖」完全由本流程依玩家選擇驅動。
    /// 見 readme/TITLE_AND_SAVE_UI.md。
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        /// <summary>是否啟用「標題→存讀檔」流程。預設開。設 false（在任何 BeforeSceneLoad 前）＝維持舊的「直接進關卡」測試流程。</summary>
        public static bool TitleFlowEnabled = true;

        [Tooltip("等 MapManager 就緒的逾時秒數（換場景後）。")]
        public float mapManagerWaitTimeout = 8f;

        /// <summary>是否已進入遊戲（false = 還在標題/存讀檔畫面）。</summary>
        public bool InGame { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (TitleFlowEnabled) OpenTitle();
        }

        // ───────────── 畫面 ─────────────

        public void OpenTitle()
        {
            if (UIManager.Instance != null) UIManager.Instance.Open<TitlePanel>();
        }

        public void OpenSlotSelect()
        {
            if (UIManager.Instance != null) UIManager.Instance.Open<SaveSlotPanel>();
        }

        void CloseMenus()
        {
            if (UIManager.Instance == null) return;
            UIManager.Instance.Close<SaveSlotPanel>();
            UIManager.Instance.Close<TitlePanel>();
        }

        // ───────────── 流程：新建 / 繼續 / 刪除 ─────────────

        /// <summary>在某欄新建遊戲（若該欄有檔＝覆蓋，覆蓋確認由 UI 先問）。建角後播開場鏈或直接進廣場。</summary>
        public void StartNewGame(int slot, string name)
        {
            var sm = SaveManager.Instance;
            if (sm == null) { Debug.LogError("[GameFlow] 沒有 SaveManager，無法新建遊戲。"); return; }

            sm.StartNewGameInSlot(slot, name);   // 建立並設為活躍（覆蓋時會先刪舊角）
            CloseMenus();
            InGame = true;

            if (CanPlayIntro())
            {
                // 交還給既有開場鏈：解除抑制，讓 Intro→MainScene 後 MapManager 照舊自動進 Main_Cave，
                // 之後踩到 cutscene 過場到邪佛廣場（MapManager 進廣場會用洞穴出口出生 + 自動存）。
                MapManager.SuppressAutoStart = false;
                Debug.Log("[GameFlow] 新建遊戲 → 播開場鏈（交還自動進關卡給開場流程）。");
                StartCoroutine(LoadSceneRoutine(SaveConstants.IntroSceneName));
            }
            else
            {
                // 無開場：由本流程明確帶進廣場，保持抑制避免 MapManager 自動進 Main_Cave 打架。
                MapManager.SuppressAutoStart = true;
                Debug.Log("[GameFlow] 無開場場景（或未加入 Build Settings）→ 直接進邪佛廣場。");
                StartCoroutine(GoToHubRoutine(SaveConstants.HubEntranceCaveExit));
            }
        }

        /// <summary>載入某欄存檔並直接進邪佛廣場（中央出生）。</summary>
        public void ContinueGame(int slot)
        {
            var sm = SaveManager.Instance;
            if (sm == null) { Debug.LogError("[GameFlow] 沒有 SaveManager，無法繼續遊戲。"); return; }

            if (!sm.LoadSlot(slot))
            {
                Debug.LogWarning($"[GameFlow] 欄位 {slot} 沒有可載入的存檔。");
                return;
            }
            CloseMenus();
            InGame = true;
            MapManager.SuppressAutoStart = true;   // 由本流程明確帶進廣場，避免自動進 Main_Cave 打架
            StartCoroutine(GoToHubRoutine(SaveConstants.HubEntranceCenter));
        }

        /// <summary>刪除某欄存檔（測試用）。</summary>
        public void DeleteSlotForTest(int slot)
        {
            if (SaveManager.Instance != null) SaveManager.Instance.DeleteSlot(slot);
        }

        // ───────────── 場景協調 ─────────────

        bool CanPlayIntro()
            => !string.IsNullOrEmpty(SaveConstants.IntroSceneName)
               && Application.CanStreamedLevelBeLoaded(SaveConstants.IntroSceneName);

        IEnumerator LoadSceneRoutine(string sceneName)
        {
            if (SceneManager.GetActiveScene().name != sceneName)
            {
                var op = SceneManager.LoadSceneAsync(sceneName);
                while (op != null && !op.isDone) yield return null;
            }
        }

        /// <summary>確保在 MainScene → 等 MapManager 就緒 → 進邪佛廣場。</summary>
        IEnumerator GoToHubRoutine(string entrance)
        {
            if (SceneManager.GetActiveScene().name != SaveConstants.MainSceneName)
                yield return StartCoroutine(LoadSceneRoutine(SaveConstants.MainSceneName));

            float t = 0f;
            while (MapManager.Instance == null && t < mapManagerWaitTimeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (MapManager.Instance != null)
                MapManager.Instance.GoToMap(SaveConstants.HubMapId, entrance);
            else
                Debug.LogError("[GameFlow] 等不到 MapManager，無法進邪佛廣場（確認 MainScene 裡有 MapManager）。");
        }
    }

    /// <summary>
    /// 開機前自動生出 GameFlowManager，並把 SaveManager / MapManager 切到「由流程驅動」模式。
    /// 靜態旗標在 BeforeSceneLoad 設定，早於任何場景物件的 Start，確保它們不會自動載入/自動進關卡。
    /// </summary>
    public static class GameFlowBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (!GameFlowManager.TitleFlowEnabled) return;   // 關閉時維持舊測試流程

            SaveManager.SuppressAutoLoad = true;   // 不自動載/建 test001，改由存讀檔畫面決定
            MapManager.SuppressAutoStart = true;   // 不自動進 startModule，改由流程 GoToMap/StartLevel

            if (GameFlowManager.Instance == null)
            {
                var go = new GameObject("[GameFlowManager]");
                go.AddComponent<GameFlowManager>();
            }
        }
    }
}
