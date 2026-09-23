using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Drama;
using Dipan.Gacha;
using Dipan.Save;
using Dipan.UI;

namespace Dipan.Flow
{
    /// <summary>
    /// 「新手夢境教學」的開場流程（夢境-初始洞窟）。
    ///
    /// 玩家新建角色後不是直接進山道，而是先做一場夢：夢裡他已經是某個**隨機的三階血統**，
    /// 在洞窟醒來、走到邪佛廣場打一場，最後被邪佛一掌壓下——那是上一輪的記憶，
    /// 醒來才接回爬山（設計脈絡見專案討論記錄）。這支負責「夢的開頭」那一段：
    ///
    ///   1. 立起全黑幕（sortingOrder 刻意壓在對話面板之下，見 <see cref="BlackoutSortingOrder"/>）。
    ///   2. 隨機挑一個三階血統，用 <see cref="BloodlineSystem.SetDreamOverride"/> 覆寫上去
    ///      （**不寫存檔**：夢是假的，醒來必須是人類）。
    ///   3. 等地圖載完、玩家生出來、外觀確實換成那個血統（此時畫面還全黑，玩家看不到變身過程）。
    ///   4. 在全黑畫面上播開頭對話（drama 31「最近，我常常做奇怪的夢」）。
    ///   5. 再播該血統的夢境台詞（drama = <see cref="DramaBloodlineBase"/> + SeriesId）。
    ///   6. 撤黑幕、播馬賽克收斂（ScreenFxTable id 3），畫面「馬賽克式展開」露出洞窟，
    ///      玩家此時已經站在那裡、是三階外貌。
    ///
    /// ⚠ **對話 id 與系列 id 是隱含契約**：drama 32~39 依序對應系列 1~8（殭屍→蟲族），
    ///   所以 <c>dramaId = 31 + SeriesId</c>。以後在 BloodlineSeriesTable 新增第 9 個系列，
    ///   要記得同步在 DramaTable 補 drama 40 那一列（與 DramaTalkTable 的對應 group），
    ///   否則那個系列被抽到時會找不到台詞（會印警告、流程照走）。
    ///
    /// 生命週期：由 <see cref="GameFlowManager"/> 在新建遊戲帶圖後呼叫 <see cref="Begin"/> 生成，
    /// 跑完開場後**不會馬上銷毀**——它還要守著「離開夢境地圖就解除血統覆寫」這道保險（見 Update）。
    /// </summary>
    public class DreamTutorialFlow : MonoBehaviour
    {
        // ── 常數 ──

        /// <summary>輸入鎖的持有者名（具名鎖：解除時不會動到別人掛的，見 PROBLEMS D13）。</summary>
        const string HoldOwner = "DreamTutorialFlow";

        /// <summary>
        /// 瀕死保護：邪佛廣場裡玩家血量掉到最大血量的這個比例以下，就不再出怪、直接跳到結尾演出。
        /// 見 <see cref="CheckNearDeath"/>。
        /// </summary>
        const float NearDeathRatio = 0.2f;

        /// <summary>
        /// 瀕死時要直接跳去的 trigger 名稱：邪佛廣場上的 drama「打完小怪後對話」（drama 42＝DramaTalkTable 第 64 句
        /// 「這次好像有點長進…先接下我這掌吧」），它的 next 才是震退回入口→骨牢→場景吞噬→佛掌。
        /// 與正常流程同一條路，只是跳過還沒打完的波次（作者 2026-09-23 實測後指定，先講這句再震退）。
        /// ⚠ 同 <see cref="AfterMosaicTrigger"/>：在編輯器改了那顆的名字，這個常數要一起改（找不到時 TriggerChain 會印警告）。
        /// </summary>
        const string NearDeathJumpTrigger = "打完小怪後對話";

        /// <summary>夢境開頭那一句（與血統無關）。</summary>
        const int DramaIntro = 31;

        /// <summary>血統台詞的基底：實際 id = 這個值 + SeriesId（見上方的隱含契約說明）。</summary>
        const int DramaBloodlineBase = 31;

        /// <summary>ScreenFxTable 的「馬賽克清晰」。</summary>
        const int MosaicScreenFx = 3;

        /// <summary>
        /// 馬賽克播完後要交棒的 trigger 名稱（擺在夢境洞窟的地圖上，目前是一顆 drama）。
        /// 程式刻意只認這個「掛點名」：之後那裡要播哪一段對話、要不要接教學提示或其他動作，
        /// 全部在地圖編輯器裡排，不必回頭改程式。
        /// ⚠ 在編輯器改了那顆 trigger 的名字，這個常數要一起改（找不到時只會印警告，夢境流程照樣結束）。
        /// </summary>
        const string AfterMosaicTrigger = "馬賽克結束後對話";

        /// <summary>
        /// 黑幕的 sortingOrder。刻意夾在 HUD（UILayer.HUD = 0）之上、主視窗（UILayer.Window = 100，
        /// DramaPanel 在這一層）之下——這樣黑幕會蓋掉世界與血球，但**對話框仍看得見**。
        /// ⚠ 不能用 <see cref="ScreenFader"/> 當這個黑幕：它是 30000，會把對話一起蓋掉。
        /// </summary>
        const int BlackoutSortingOrder = 50;

        /// <summary>
        /// 【測試用】固定夢境血統的 id，**0 ＝ 正常隨機**（正式行為）。
        /// 每次新建角色都隨機會很難重現同一個情境，測某個血統的立繪／外型時把它填上就固定了。
        /// ⚠ 不為 0 時每次開場都會在 Console 印一則黃色警告提醒，測完記得改回 0。
        /// </summary>
        const int DebugForceBloodlineId = 0;    // 0 = 正常隨機；要固定測某個血統就填它的 id（例：82 = 蟲皇）

        /// <summary>
        /// 等「玩家外觀換成該血統」的上限。
        /// ⚠ **地圖還在載的時間不算在這裡面**（見 <see cref="WaitForPlayerReady"/> 的說明與 PROBLEMS H2）——
        ///   以前是連載圖一起計時，遇到編輯器邊玩邊匯入素材、載圖超過 15 秒，就會在載入頁還開著的時候
        ///   提早往下播對話，然後被換圖清掉回呼 → 永久黑幕。
        /// </summary>
        const float PlayerReadyTimeout = 15f;

        /// <summary>等地圖載完的硬上限（保險絲，正常跑不會用到）。超過就照走，畫面可能會怪但不會卡死。</summary>
        const float MapReadyTimeout = 90f;

        const float DramaOpenTimeout = 3f;      // 對話面板「開起來」的上限（開不起來＝這段跳過，不要空等）
        const float DramaTimeout = 120f;        // 對話關閉回呼的保險絲（玩家可能掛在那裡不按）
        const float MosaicTimeout = 10f;        // 馬賽克播完回呼的保險絲

        // ── 狀態 ──

        static DreamTutorialFlow _instance;

        /// <summary>夢境開場是否正在播（給別的系統判斷「現在不要插話」用）。</summary>
        public static bool IsPlaying { get; private set; }

        /// <summary>進 Play 時歸零（Domain Reload 已關）。</summary>
        public static void ResetForPlayMode() { _instance = null; IsPlaying = false; }

        GameObject _blackout;
        bool _armed;        // 已經確認進到夢境地圖了 → Update 才開始檢查「離開」
        bool _nearDeathFired;       // 瀕死保護已經觸發過（一場夢只跳一次）
        float _nearDeathNextCheck;  // 節流：血量低時才會去找出生點，最多每 0.2 秒一次
        CombatStats _playerStats;   // 快取（換圖玩家物件不變，但保險起見失效就重抓）

        /// <summary>
        /// 啟動夢境開場。重複呼叫會被忽略（已經有一份在跑）。
        /// 呼叫時機：新建遊戲流程已經 <c>GoToMap</c> 到夢境洞窟之後、螢幕黑幕淡出之前。
        /// </summary>
        public static void Begin()
        {
            if (_instance != null)
            {
                Debug.LogWarning("[DreamTutorial] 夢境開場已經在跑，忽略這次 Begin()。");
                return;
            }
            var go = new GameObject("[DreamTutorialFlow]");
            _instance = go.AddComponent<DreamTutorialFlow>();
            DontDestroyOnLoad(go);
            _instance.StartCoroutine(_instance.Run());
        }

        /// <summary>這個地圖 id 是否屬於新手夢境教學。⚠ 夢境 module 之後加新地圖要一起加進來。</summary>
        static bool IsDreamMap(int mapId)
            => mapId == SaveConstants.DreamTutorialStartMapId
            || mapId == SaveConstants.DreamTutorialSquareMapId;

        // ───────────────────────── 主流程 ─────────────────────────

        IEnumerator Run()
        {
            IsPlaying = true;
            ShowBlackout();
            SetHold(true);

            // 1) 隨機挑一個三階血統並覆寫上去（不寫存檔）。
            int bloodlineId = PickRandomStage3();
            if (bloodlineId <= 0)
            {
                Debug.LogWarning("[DreamTutorial] 挑不到可用的三階血統（表A/表B 還沒載好？），夢境開場略過。");
                yield return EndRoutine();
                yield break;
            }
            BloodlineSystem.SetDreamOverride(bloodlineId);

            // 夢裡一律不准玩家自己開選單（背包／倉庫／鍛造／設定）：武器是劇情覆寫、不在背包裡，
            // 開背包去換裝或拿東西只會出事（作者 2026-09-23 拍板）。離開夢境才解，見 Update／OnDestroy。
            UIManager.SetPlayerMenuLock(HoldOwner, true);

            // 夢裡玩家不能死：最多扣到 1 滴血、不觸發死亡流程（作者 2026-09-23 拍板）。
            // 快死時改走「不再出怪、直接跳結尾」，見 Update → CheckNearDeath。
            CombatStats.SetDeathGuard(HoldOwner, true);

            // 2) 等地圖載完、玩家生出來、外觀確實換成那個血統（畫面還是全黑，變身過程玩家看不到）。
            yield return WaitForPlayerReady(bloodlineId);
            _armed = true;

            // 2b) 依血統直接裝上夢境武器（表B 的 DreamWeaponId）——不進背包、不進存檔，醒來收回。
            EquipDreamWeapon(bloodlineId);

            // 3) 全黑畫面上播開頭那一句。
            yield return PlayDrama(DramaIntro);

            // 4) 再播這個血統的夢境台詞。
            yield return PlayDrama(BloodlineDramaId(bloodlineId));

            // 5) 撤黑幕、馬賽克收斂成清晰 → 畫面「展開」露出洞窟。
            HideBlackout();
            yield return PlayMosaic();

            // 6) 先放掉自己的輸入鎖，再把棒子交給地圖上的鏈（那顆 drama 會自己暫停遊戲、
            //    關閉時照常接它的 next），夢境開場到此結束。
            yield return EndRoutine();
            TriggerChain.Activate(AfterMosaicTrigger);
        }

        IEnumerator EndRoutine()
        {
            HideBlackout();
            SetHold(false);
            IsPlaying = false;
            yield break;
        }

        // ───────────────────────── 各步驟 ─────────────────────────

        /// <summary>從表A 取所有系列的第三階，隨機一個（表B 找不到的跳過）。挑不到回 0。</summary>
        static int PickRandomStage3()
        {
            // 【測試用】固定血統：見 DebugForceBloodlineId 的說明，正式版該是 0。
            if (DebugForceBloodlineId > 0)
            {
                if (BloodlineTable.Get(DebugForceBloodlineId) != null)
                {
                    Debug.LogWarning($"[DreamTutorial] ⚠ 測試模式：夢境血統固定為 " +
                                     $"{BloodlineTable.NameOf(DebugForceBloodlineId)}（id {DebugForceBloodlineId}），**沒有隨機**。" +
                                     "要恢復隨機請把 DreamTutorialFlow.DebugForceBloodlineId 改回 0。");
                    return DebugForceBloodlineId;
                }
                Debug.LogWarning($"[DreamTutorial] DebugForceBloodlineId={DebugForceBloodlineId} 在表B 找不到，退回正常隨機。");
            }

            var pool = new List<int>();
            foreach (var s in BloodlineSeriesTable.All)
            {
                if (s == null) continue;
                int stage3 = s.IdOfStage(BloodlineSeriesTable.MaxStage);   // 第三階（沒填到第三階回 0）
                if (stage3 <= 0) continue;
                if (BloodlineTable.Get(stage3) == null) continue;          // 表B 還沒有那一列 → 不抽
                pool.Add(stage3);
            }
            if (pool.Count == 0) return 0;
            int picked = pool[Random.Range(0, pool.Count)];
            Debug.Log($"[DreamTutorial] 本次夢境血統 = {BloodlineTable.NameOf(picked)}（id {picked}，可抽 {pool.Count} 種）。");
            return picked;
        }

        /// <summary>
        /// 裝上這個血統的夢境武器（表B <c>DreamWeaponId</c>）。留空＝不覆寫，玩家用自己裝的（新角色通常是空手）。
        /// 走 <c>PlayerController.SetScriptedWeapon</c>＝WeaponManager 的劇情覆寫，背包與存檔完全不受影響。
        /// </summary>
        static void EquipDreamWeapon(int bloodlineId)
        {
            var def = BloodlineTable.Get(bloodlineId);
            int weaponId = def != null ? def.DreamWeaponId : 0;
            if (weaponId <= 0)
            {
                Debug.LogWarning($"[DreamTutorial] 血統 {BloodlineTable.NameOf(bloodlineId)}（id {bloodlineId}）沒有填 DreamWeaponId，夢裡沿用玩家自己的武器。");
                return;
            }
            var pc = FindPlayer();
            if (pc == null)
            {
                Debug.LogWarning("[DreamTutorial] 找不到玩家，夢境武器沒有裝上。");
                return;
            }
            pc.SetScriptedWeapon(weaponId);
            Debug.Log($"[DreamTutorial] 夢境武器 = WeaponTable {weaponId}（血統 {BloodlineTable.NameOf(bloodlineId)}）。");
        }

        /// <summary>收回夢境武器＋解開選單鎖。離開夢境（Update）與物件被銷毀（OnDestroy）都會呼叫，重複呼叫無害。</summary>
        static void ReleaseDreamLoadout()
        {
            UIManager.SetPlayerMenuLock(HoldOwner, false);
            CombatStats.SetDeathGuard(HoldOwner, false);
            var pc = FindPlayer();
            if (pc != null) pc.SetScriptedWeapon(0);
        }

        static PlayerController FindPlayer()
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            return go != null ? go.GetComponent<PlayerController>() : null;
        }

        /// <summary>某個血統的夢境台詞 drama id＝31 + 它所屬系列的 SeriesId（見檔頭的隱含契約）。</summary>
        static int BloodlineDramaId(int bloodlineId)
        {
            if (!BloodlineSeriesTable.TryLocate(bloodlineId, out var series, out _) || series == null)
            {
                Debug.LogWarning($"[DreamTutorial] 血統 id {bloodlineId} 在表A 查不到系列，退回開頭對話。");
                return DramaIntro;
            }
            return DramaBloodlineBase + series.SeriesId;
        }

        /// <summary>
        /// 等「載入頁關閉 ＋ 玩家存在 ＋ 外觀已換成該血統」。超時就照走（不讓流程卡死）。
        ///
        /// <para>⚠ **兩段計時，不能混成一個**（PROBLEMS H2）：
        /// 「地圖載完」要多久不是這支能控制的（編輯器裡剛匯入完素材的第一次 Play 可以拖上幾十秒），
        /// 而「外觀換好」只是幾幀的事。以前共用一個 15 秒上限，結果變成**地圖載很慢時提早放行**——
        /// 在載入頁還開著的時候就去播開場對話，隨後地圖載完呼叫 <c>TriggerChain.Setup</c>，
        /// 它會清掉所有未結的對話完成回呼，於是 <see cref="PlayDrama"/> 永遠等不到通知、黑幕永遠不撤。</para>
        ///
        /// <para>所以：外觀的 15 秒**只在「地圖已就緒」之後才開始累計**，地圖那段另外給一條寬鬆的硬保險絲。</para>
        /// </summary>
        IEnumerator WaitForPlayerReady(int bloodlineId)
        {
            var def = BloodlineTable.Get(bloodlineId);
            string wantFolder = def != null ? def.SpriteFolder : null;

            float total = 0f;    // 含載圖的硬保險絲
            float skinT = 0f;    // 只累計「地圖已就緒、還在等外觀」的時間
            while (total < MapReadyTimeout)
            {
                total += Time.unscaledDeltaTime;

                var ui = UIManager.Instance;
                bool loading = ui != null && ui.IsOpen<LoadingPanel>();
                var playerGo = GameObject.FindGameObjectWithTag("Player");
                var pc = playerGo != null ? playerGo.GetComponent<PlayerController>() : null;

                bool skinReady = pc != null
                                 && (string.IsNullOrEmpty(wantFolder) || pc.Bloodline == wantFolder);

                if (!loading && skinReady) yield break;

                // 地圖還在載／玩家還沒生出來 → 外觀計時歸零重算（這段不是「外觀換不過來」的錯）。
                if (!loading && pc != null) skinT += Time.unscaledDeltaTime;
                else skinT = 0f;

                if (skinT >= PlayerReadyTimeout)
                {
                    Debug.LogWarning($"[DreamTutorial] 地圖已就緒，但等玩家換好外觀等了 {PlayerReadyTimeout} 秒還沒好，先往下播對話。");
                    yield break;
                }
                yield return null;
            }
            Debug.LogWarning($"[DreamTutorial] 等地圖載完等了 {MapReadyTimeout} 秒還沒好，先往下播對話（畫面可能會怪）。");
        }

        /// <summary>
        /// 播一段 drama，等面板關閉。
        ///
        /// ⚠ 分派必須與 <c>TriggerChain.ExecuteDrama</c> 一致：DramaTable 的 <c>Type=2</c> 是「對話組」，
        /// 要走 <see cref="DramaTalkController"/>（TalkPanel，顯示 DramaTalkTable 的台詞）；
        /// 只有其他 Type 才走 <see cref="DramaPanel"/>（圖文面板，顯示 DramaTable 自己的 Text 欄）。
        /// 直接叫 DramaPanel 的話，畫面上會變成顯示「新手夢境教學開頭」這種備註文字而不是台詞。
        /// 兩個面板關閉時都會呼叫 <c>TriggerChain.NotifyDramaClosed()</c>，所以等待方式共用。
        /// </summary>
        IEnumerator PlayDrama(int dramaId)
        {
            var db = DramaDatabase.Instance;
            var dd = db != null ? db.Get(dramaId) : null;
            if (dd == null)
            {
                Debug.LogWarning($"[DreamTutorial] DramaTable 找不到 id {dramaId}，這一段會被跳過。");
                yield break;
            }

            bool closed = false;
            TriggerChain.CompleteAfterDramaAction(() => closed = true);

            bool useTalk = dd.Type == 2;
            if (useTalk) DramaTalkController.Play(dd.TalkGroup);
            else DramaPanel.Show(dramaId);

            // ── 1) 等面板真的開起來（開面板不是同幀的事）──
            float t = 0f;
            while (!closed && !PanelOpen(useTalk) && t < DramaOpenTimeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!closed && !PanelOpen(useTalk))
            {
                Debug.LogWarning($"[DreamTutorial] drama {dramaId} 的面板沒有開起來（等了 {DramaOpenTimeout} 秒），這一段跳過。");
                yield break;
            }

            // ── 2) 等面板關閉 ──
            // ⚠ **不能只靠 TriggerChain 的完成回呼**（PROBLEMS H2）：那個回呼只有一格，
            //   換圖時 TriggerChain.Setup 會把它清掉、別的對話來源也會蓋掉它。回呼一旦不見，
            //   這裡就會空等到 120 秒保險絲，而那整段時間黑幕都還蓋著＝玩家看到的就是「按完一句之後全黑」。
            //   所以**同時盯面板自己的開關狀態**，兩個訊號誰先到都算數。
            t = 0f;
            while (!closed && PanelOpen(useTalk) && t < DramaTimeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // 回呼是「延一幀」送的（見 TriggerChain.NotifyDramaClosed），面板關掉後多等幾幀再判定，
            // 免得正常流程也被當成「回呼被吃掉」而印警告。
            for (int i = 0; i < 5 && !closed; i++) yield return null;

            if (!closed && !PanelOpen(useTalk))
                Debug.LogWarning($"[DreamTutorial] drama {dramaId} 沒收到關閉通知（回呼被別的流程清掉或蓋掉），" +
                                 "改用面板狀態判定，流程繼續。");
            else if (!closed)
                Debug.LogWarning($"[DreamTutorial] drama {dramaId} 沒有等到關閉通知（超時 {DramaTimeout} 秒），流程繼續。");
        }

        /// <summary>對話面板目前是否開著（Type=2 走 TalkPanel，其餘走 DramaPanel）。</summary>
        static bool PanelOpen(bool useTalk)
        {
            var ui = UIManager.Instance;
            if (ui == null) return false;
            return useTalk ? ui.IsOpen<TalkPanel>() : ui.IsOpen<DramaPanel>();
        }

        /// <summary>播馬賽克收斂（ScreenFxTable id 3），等播完。</summary>
        IEnumerator PlayMosaic()
        {
            bool done = false;
            ScreenFxPlayer.Play(MosaicScreenFx, () => done = true);

            float t = 0f;
            while (!done && t < MosaicTimeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!done) Debug.LogWarning($"[DreamTutorial] 馬賽克沒有回報播完（超時 {MosaicTimeout} 秒），流程繼續。");
        }

        // ───────────────────────── 黑幕 / 輸入鎖 ─────────────────────────

        void ShowBlackout()
        {
            if (_blackout != null) return;

            _blackout = new GameObject("[DreamBlackout]");
            _blackout.transform.SetParent(transform, false);

            var canvas = _blackout.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = BlackoutSortingOrder;

            var imgGO = new GameObject("Black");
            imgGO.transform.SetParent(_blackout.transform, false);
            var img = imgGO.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;                       // 不擋點擊（對話還要能按）
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void HideBlackout()
        {
            if (_blackout == null) return;
            Destroy(_blackout);
            _blackout = null;
        }

        static void SetHold(bool hold)
        {
            var ui = UIManager.Instance;
            if (ui == null) return;
            // 只鎖輸入、不暫停：對話面板自己就是模態（PausesGame），這裡再壓一層暫停沒有意義。
            ui.SetExternalHold(HoldOwner, hold, false);
        }

        // ───────────────────────── 保險：離開夢境就解除覆寫 ─────────────────────────

        void Update()
        {
            if (!_armed) return;

            var mm = MapManager.Instance;
            if (mm == null) return;

            int mapId = mm.CurrentMapId;
            if (mapId <= 0) return;
            if (IsDreamMap(mapId))
            {
                if (mapId == SaveConstants.DreamTutorialSquareMapId && !mm.IsLoading) CheckNearDeath();
                return;
            }

            // 走出夢境（接回山道／讀檔／輪迴）→ 血統覆寫一定要解除，否則玩家醒來還是三階外貌。
            Debug.Log($"[DreamTutorial] 已離開夢境地圖（現在 MapId={mapId}），解除血統覆寫並收掉流程物件。");
            BloodlineSystem.ClearDreamOverride();
            ReleaseDreamLoadout();   // 收回夢境武器＋解開選單鎖
            SetHold(false);
            IsPlaying = false;
            _instance = null;
            Destroy(gameObject);
        }

        /// <summary>
        /// 瀕死保護（邪佛廣場）：玩家血量 ≤ 最大血量 × <see cref="NearDeathRatio"/>，而且**小怪波次正在打**的時候——
        /// 中止所有進行中的出生點（場上的怪一起炸掉、不推它們的鏈），收掉畫面上的提示，
        /// 直接跳到 <see cref="NearDeathJumpTrigger"/>（打完小怪後對話 → 震退回入口 → 骨牢 → 場景吞噬 → 佛掌壓下）。
        ///
        /// <para>為什麼要有這條：玩家若在廣場完全不攻擊，會被小怪打死 ⇒ 走一般死亡流程，整段夢境就壞了。
        /// 真正「不會死」是 <see cref="CombatStats.SetDeathGuard"/> 保證的（最多扣到 1 滴血）；這裡負責的是
        /// 「快死了就別再讓他挨打，直接進結尾」，看起來像是邪佛出手打斷了這場戰鬥。</para>
        ///
        /// <para>⚠ 只在「有出生點正在打」時才跳（<c>AbortActiveWaves</c> 回傳 &gt; 0）：
        /// 小怪都打完、已經在跑結尾（打完小怪後對話／震退／佛掌）時血量再低也不跳，免得同一句對話與震退被觸發兩次。
        /// 那段期間靠不死保護兜底就夠了。</para>
        /// </summary>
        void CheckNearDeath()
        {
            if (_nearDeathFired) return;
            if (_playerStats == null)
            {
                var pc = FindPlayer();
                _playerStats = pc != null ? pc.GetComponent<CombatStats>() : null;
                if (_playerStats == null) return;
            }
            if (_playerStats.Health > _playerStats.MaxHealth * NearDeathRatio) return;

            // 血量低了才去找出生點；再節流一下，免得「低血量但沒在打」的整段時間每幀 FindObjectOfType。
            if (Time.unscaledTime < _nearDeathNextCheck) return;
            _nearDeathNextCheck = Time.unscaledTime + 0.2f;

            var respawner = FindObjectOfType<MapMonsterRespawner>();
            int aborted = respawner != null ? respawner.AbortActiveWaves() : 0;
            if (aborted <= 0) return;   // 沒有正在打的波次 ⇒ 已經在結尾了（或還沒開打），不跳

            _nearDeathFired = true;
            Debug.Log($"[DreamTutorial] 玩家瀕死（HP {_playerStats.Health:F0}/{_playerStats.MaxHealth:F0}）→ " +
                      $"中止 {aborted} 個出生點、跳到「{NearDeathJumpTrigger}」。");
            PlayerHintPanel.HidePanel();   // 「按左鍵攻擊」「按 E」這類提示還掛著的話一起收掉
            TriggerChain.Activate(NearDeathJumpTrigger);
        }

        void OnDestroy()
        {
            // 保險：不管是正常離開還是別的原因被銷毀（回標題／結束 Play），選單鎖與武器覆寫都不能留著。
            ReleaseDreamLoadout();
            if (_instance == this) _instance = null;
        }
    }
}
