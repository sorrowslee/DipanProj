using System.Collections;
using System.Collections.Generic;
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
            InGame = true;

            if (CanPlayIntro())
            {
                // 交還給既有開場鏈：解除抑制，讓 Intro→MainScene 後 MapManager 照舊自動進 Main_Cave，
                // 之後踩到 cutscene 過場到邪佛廣場（MapManager 進廣場會用洞穴出口出生 + 自動存）。
                MapManager.SuppressAutoStart = false;
                Debug.Log("[GameFlow] 新建遊戲 → 播開場鏈（交還自動進關卡給開場流程）。");
                StartCoroutine(NewGameIntroRoutine());   // 先蓋黑再關選單→載入開場，避免露出標題面板
            }
            else
            {
                // 無開場：由本流程明確帶進廣場，保持抑制避免 MapManager 自動進 Main_Cave 打架。
                CloseMenus();
                MapManager.SuppressAutoStart = true;
                Debug.Log("[GameFlow] 無開場場景（或未加入 Build Settings）→ 直接進邪佛廣場。");
                StartCoroutine(GoToHubRoutine(SaveConstants.HubEntranceCaveExit));
            }
        }

        /// <summary>新建有開場：先蓋黑幕遮住標題/存讀檔面板，再關選單、載入 Intro，最後淡出露出開場。避免「標題閃一下才進漫畫」。</summary>
        IEnumerator NewGameIntroRoutine()
        {
            var fader = ScreenFader.Ensure();
            yield return fader.FadeTo(1f, 0.25f);   // 蓋黑（暫停中，用 unscaledTime 仍會動）
            CloseMenus();                            // 在黑幕下關掉標題/存讀檔，玩家看不到淡出過程
            yield return LoadSceneRoutine(SaveConstants.IntroSceneName);
            yield return null;                       // 等一幀讓 Intro 場景初始化
            yield return fader.FadeTo(0f, 0.35f);    // 淡出，露出開場漫畫
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

        // ───────────── 流程：過關 / 死亡 → 卍字離場 → 結算 → 返回廣場 ─────────────

        // 關卡 module → 顯示名（結算畫面「關卡：xxx」用）。新增關卡在這加一行即可；查無＝直接顯示 module id。
        static readonly Dictionary<string, string> ModuleDisplayNames = new Dictionary<string, string>
        {
            { "RedBridalGown", "紅嫁衣" },
            { "BanyanTree", "榕樹妖" },
        };
        static string DisplayNameOf(string module)
            => (!string.IsNullOrEmpty(module) && ModuleDisplayNames.TryGetValue(module, out var n)) ? n : module;

        bool _endingLevel;              // 結束流程進行中（防重入：boss 死＋玩家死同幀只跑一次）
        public bool IsEndingLevel => _endingLevel;   // 給 clearLevel 判斷「已在結束流程中就別重跑對話」
        Transform _endPlayer;          // 被卍字吞走的玩家（回廣場前還原縮放/復活用）
        Vector3 _endPlayerScale;       // 玩家原始縮放
        string _endModule;             // 這關的 module（結算底圖/關卡名/記過關用）

        /// <summary>結束本關的三種情形。</summary>
        public enum LevelEndKind
        {
            Clear,   // 過關：記進度、標題「通關結算」
            Death,   // 死亡：不記進度、標題「死亡結算」、死亡定格多停一下
            Return,  // 主動返回廣場（設定面板）：不記進度、不顯示標題
        }

        /// <summary>
        /// 結束本關：播卍字離場特效 → 開結算畫面。
        /// Clear＝過關（記進度、通關標題）／Death＝死亡（死亡標題）／Return＝主動返回（無標題、不記進度）。
        /// 由 clearLevel 觸發動作、PlayerController.Die、設定面板「返回廣場」呼叫；同幀多次呼叫只生效一次。
        /// </summary>
        // 死亡的延時（固定）：讓死亡動畫演一下再吞。過關的延時走 clearLevel 觸發的「延時觸發」欄位（見 delaySeconds 參數）。
        const float DeathDelaySeconds = 2f;

        /// <param name="delaySeconds">Clear 專用：觸發後等這麼久才播離場（讓 boss 死前對話/表演演完）。&lt;0＝用預設 2 秒。其他 kind 忽略此值。</param>
        public void EndLevel(LevelEndKind kind, float delaySeconds = -1f)
        {
            if (_endingLevel) return;
            // 必須「已載入一張關卡地圖」才觸發（標題畫面 CurrentMapId <= 0）。用地圖狀態判斷而非 InGame，
            // 這樣走 DevQuickStart（直接進關卡、沒跑標題流程、InGame=false）也能正常結束關卡。
            if (MapManager.Instance == null || MapManager.Instance.CurrentMapId <= 0) return;
            _endingLevel = true;
            StartCoroutine(EndLevelRoutine(kind, delaySeconds));
        }

        IEnumerator EndLevelRoutine(LevelEndKind kind, float delaySeconds)
        {
            bool win = kind == LevelEndKind.Clear;
            bool showTitle = kind != LevelEndKind.Return;

            _endModule = MapManager.Instance != null ? MapManager.Instance.CurrentModule : null;

            var playerGo = GameObject.FindGameObjectWithTag("Player");
            _endPlayer = playerGo != null ? playerGo.transform : null;
            _endPlayerScale = _endPlayer != null ? _endPlayer.localScale : Vector3.one;

            // 延時秒數：過關＝觸發欄位（<0 用 2）；死亡＝固定 2；返回＝短。
            float delay = kind == LevelEndKind.Clear ? (delaySeconds >= 0f ? delaySeconds : 2f)
                        : kind == LevelEndKind.Death ? DeathDelaySeconds
                        : 0.25f;

            // ── 階段一：等待 ──
            if (kind == LevelEndKind.Clear)
            {
                // 過關：**不擋操作、不暫停** → 玩家可自由移動撿戰利品；boss 對話（clearLevel 的 next，由 TriggerChain 觸發）
                // 自己會暫停。倒數用 scaled time（對話暫停時自動凍住），並在上方顯示「X 秒後即將進入結算」。
                yield return ClearCountdown(delay);
            }
            else
            {
                // 死亡/返回：擋操作但不暫停（死亡動畫仍會演），等固定秒數。
                if (UIManager.Instance != null) UIManager.Instance.SetExternalHold(true, false);
                if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            }

            // 階段二：暫停（怪物/子彈定住）＋播卍字離場特效（吞玩家、飛上天；特效用 unscaledTime）。
            if (UIManager.Instance != null) UIManager.Instance.SetExternalHold(true, true);
            bool fxDone = false;
            LevelExitManjiController.Play(_endPlayer, () => fxDone = true);
            while (!fxDone) yield return null;

            // 臨時包結算：過關＝整包併入真背包並清空、拿到內容快照顯示；死亡/返回＝整包丟棄（這趟零收穫）。
            // 見 readme/CORE_LOOP_DESIGN.md §6、RunProgress。
            List<KeyValuePair<int, int>> rewards = null;
            if (RunProgress.Exists)
            {
                if (kind == LevelEndKind.Clear) rewards = RunProgress.Instance.SettleIntoBag();
                else RunProgress.Instance.EndRunDiscard();
            }

            // 只有過關記進度（存檔）。死亡與主動返回都不算過關。（先結算臨時包再存檔，讓落袋的戰利品一起寫入。）
            if (kind == LevelEndKind.Clear && SaveManager.Instance != null && !string.IsNullOrEmpty(_endModule))
            {
                SaveManager.Instance.MarkModuleCleared(_endModule);
                SaveManager.Instance.SaveNow();
            }

            // 開結算畫面（覆蓋全螢幕、暫停）。玩家此時縮到 ~0 且在畫面外，被結算面板蓋住。
            // 過關時把臨時包內容（rewards）顯示在獎勵區；死亡/返回 rewards=null → 顯示「無」。
            ResultPanel.Show(win, showTitle, _endModule, DisplayNameOf(_endModule), rewards);
        }

        /// <summary>
        /// 過關延時倒數：期間玩家可自由操作（不擋不暫停），上方顯示「X 秒後即將進入結算」。
        /// 用 scaled time 計時 → boss 對話（會暫停遊戲）期間自動凍住，對話結束玩家能動時才繼續倒數。
        /// </summary>
        IEnumerator ClearCountdown(float total)
        {
            var panel = UIManager.Instance != null ? UIManager.Instance.Open<ExitCountdownPanel>() : null;
            float remaining = Mathf.Max(0f, total);
            while (remaining > 0f)
            {
                if (panel != null) panel.SetSeconds(Mathf.CeilToInt(remaining));
                remaining -= Time.deltaTime;   // scaled：遊戲暫停（對話中）時為 0 → 倒數凍住
                yield return null;
            }
            if (UIManager.Instance != null) UIManager.Instance.Close<ExitCountdownPanel>();
        }

        /// <summary>結算畫面「返回廣場」按鈕：淡黑 → 復活/還原玩家 → 回邪佛廣場中央 → 淡出。</summary>
        public void ReturnToHubFromResult()
        {
            StartCoroutine(ReturnFromResultRoutine());
        }

        IEnumerator ReturnFromResultRoutine()
        {
            var fader = ScreenFader.Ensure();
            yield return fader.FadeTo(1f, 0.3f);          // 蓋黑（暫停中，用 unscaledTime）

            if (UIManager.Instance != null) UIManager.Instance.Close<ResultPanel>();

            // 還原玩家：縮放復原 + 復活（血魔滿、解除死亡定格）。GoToMap 之後會把玩家移到廣場落點。
            if (_endPlayer != null)
            {
                _endPlayer.localScale = _endPlayerScale;
                var pc = _endPlayer.GetComponent<PlayerController>();
                if (pc != null) pc.ReviveFull();
            }

            // 解除暫停/鎖（GoToMap 的載入流程要在正常 timeScale 下跑）。
            if (UIManager.Instance != null) UIManager.Instance.SetExternalHold(false, false);

            // 回邪佛廣場中央
            if (MapManager.Instance != null)
                MapManager.Instance.GoToMap(SaveConstants.HubMapId, SaveConstants.HubEntranceCenter);

            // 等載入完成再淡出（跨 module 會出讀取頁，這裡多等它一下）。
            yield return null;
            float t = 0f;
            while (MapManager.Instance != null && MapManager.Instance.IsLoading && t < 10f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return fader.FadeTo(0f, 0.35f);

            _endingLevel = false;
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
            // 標題流程開啟時：把 SaveManager / MapManager 切到「由流程驅動」（不自動載檔/進關卡）。
            // 關閉時（DevQuickStart 直接進關卡）維持舊測試流程，不動這兩個抑制旗標。
            if (GameFlowManager.TitleFlowEnabled)
            {
                SaveManager.SuppressAutoLoad = true;
                MapManager.SuppressAutoStart = true;
            }

            // GameFlowManager **一律建立**（不論走不走標題流程）：過關/死亡/返回廣場等關卡結束流程都靠它，
            // DevQuickStart 直接進關卡時也要能用。它在 Start 只有 TitleFlowEnabled 才開標題，dev 模式下只是待命。
            if (GameFlowManager.Instance == null)
            {
                var go = new GameObject("[GameFlowManager]");
                go.AddComponent<GameFlowManager>();
            }
        }
    }
}
