using UnityEngine;
using Dipan.Inventory;
using Dipan.Save;
using Dipan.Localization;

namespace Dipan.Gacha
{
    /// <summary>
    /// 血統的執行期系統：把「本世血統」套到玩家身上，並處理喝血統藥劑（含系列起始與逐階進階）。
    ///
    /// 規則（拍板的設計，見 readme/BLOODLINE.md）：
    ///   1. 血統分「系列」，一個系列有三階（例：殭屍 → 毛殭 → 旱魃）。系列與階段的對應在
    ///      <see cref="BloodlineSeriesTable"/>（表A）；每一階長什麼樣、數值多少在 <see cref="BloodlineTable"/>（表B）。
    ///   2. **系列起始藥劑**（ItemTable 的 BloodlineID 指到某系列第一階）：本世只能喝一次，
    ///      喝完就鎖死系列，不能再改吃別的系列。
    ///   3. **血統進階藥劑**（ItemTable 的 BloodlineUpgrade = 目標階數）：**全系列通用**，
    ///      沿著目前系列往上走一階。必須逐階——第 1 階的人不能直接喝高階藥劑。
    ///   4. **輪迴後回到人類**。血統藥劑本身是道具，可以被輪迴帶物選中留到下一世再喝——
    ///      所以「本世血統」刻意存在 progress.flags（周目層），ReincarnateInPlace 換掉整個 progress
    ///      時自動失效，不需要任何額外的重置程式碼。
    ///
    /// ⚠ **血統目前不改變任何遊戲數值**。表B 的五個屬性（行走速度/力量/敏捷/魔力/體力）
    ///   只是概念值、沒有任何一處會讀。換血統在體感上只有外型與立繪會變，這是預期行為。
    ///   （舊版這裡會套 MaxHpAdd / MoveSpeedMul / 傷害加成，那是屬性系統還沒有時的權宜做法，
    ///     已於 2026-08-18 拿掉，避免之後與真正的屬性系統打架。）
    ///
    /// 生命週期：常駐單例、自動生成、零接線（同 UIBootstrap / VfxManager 的風格）。
    /// 每幀比對「存檔裡的血統」與「已套用的血統」，不一致才動作——所以不管存檔載入、換圖、
    /// 玩家物件重建的順序如何，最後都會收斂到正確狀態。
    /// </summary>
    public class BloodlineSystem : MonoBehaviour
    {
        static BloodlineSystem _instance;

        /// <summary>進 Play 時歸零（Domain Reload 已關）。由 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[BloodlineSystem]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BloodlineSystem>();
        }

        // ── 已套用的狀態（跟玩家物件綁；玩家換了就重來）──
        PlayerController _pc;
        int _appliedId = -1;

        /// <summary>
        /// 變身表演進行中（世界演出 ＋ 立繪揭示面板，兩段合起來算一段）。
        /// 表演期間停止收斂——不然 Update 會搶在煙霧散開前就把外型換掉，
        /// 玩家會先看到新造型再看到煙霧。
        /// </summary>
        bool _transforming;
        float _transformStartedAt;

        /// <summary>
        /// 表演期間掛在 UIManager 的具名 external hold。
        ///
        /// ⚠ <b>為什麼要一個「橫跨兩段」的鎖，而不是讓兩段各自鎖自己。</b>
        /// 世界演出（<c>BloodlineTransformFxRunner</c>）和立繪面板（<c>BloodlineIntroPanel</c>）
        /// 是接力的：前者 finally 先 <c>ReleaseHold()</c> 再回呼，後者要延一幀才開得起來
        /// （避免 OnClose 重入，見 PROBLEMS D8）。中間那一兩幀若沒有人壓著，
        /// <c>timeScale</c> 會彈回 1、玩家可動、怪物動一下——會看得出來卡一格。
        /// 所以這裡從喝下去的那一刻壓到面板關閉為止，全程不放手。
        ///
        /// ⚠ 一定要用**具名**版：玩家若在這段期間死掉，死亡流程也會掛 hold，
        /// 共用預設持有者的話兩邊會互相清掉對方的鎖（見 PROBLEMS D13）。
        /// </summary>
        const string PerformanceHoldOwner = "BloodlinePerformance";

        /// <summary>
        /// 變身表演的保險絲（秒）。超過就強制解除，避免特效寫錯時血統永遠套不上去、
        /// 或是 hold 沒放導致玩家整場不能動。
        /// 30 秒＝「正常世界演出約 6 秒 ＋ 立繪面板約 4 秒 ＋ 最壞情況（倒下/爬起各逾時 6 秒）」再留餘裕。
        /// 調節奏時不用跟著動，但如果之後把整段加長到 25 秒以上，這裡要一起調大。
        /// </summary>
        const float TransformTimeout = 30f;

        /// <summary>
        /// 變身表演進行中（世界演出或立繪面板任一段）。給「會暫停遊戲的東西」查詢用——
        /// 目前是 <c>StorageBagCoordinator</c> 的背包/倉庫/鍛造熱鍵：那三個面板都會蓋在表演上面。
        /// </summary>
        public static bool IsPerforming
            => (_instance != null && _instance._transforming)
            || BloodlineTransformFxRunner.IsPlaying
            || Dipan.UI.BloodlineIntroPanel.IsShowing;

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            if (_transforming)
            {
                // 保險絲：表演應該自己走到 FinishPerformance。萬一哪一段漏叫回呼（或中途換場景被打斷），
                // 這裡強制解除，否則血統會永遠停在舊外型、而且 external hold 不會放
                // ＝玩家整場不能動，完全沒有錯誤訊息。
                if (Time.unscaledTime - _transformStartedAt <= TransformTimeout) return;
                Debug.LogWarning("[BloodlineSystem] 變身表演超過 " + TransformTimeout +
                                 " 秒沒有結束，強制解除。檢查 BloodlineTransformFx.Play 的 onFinished " +
                                 "與 BloodlineIntroPanel.Show 的 onFinished 是否都有被呼叫。");
                FinishPerformance();
            }

            // 玩家還沒生出來（載入中、標題畫面）→ 等。
            if (_pc == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go == null) return;
                _pc = go.GetComponent<PlayerController>();
                if (_pc == null) return;
                _appliedId = -1;              // 新的玩家物件 → 重新套一次
            }

            int want = CurrentBloodlineId;
            if (want == _appliedId) return;
            ApplyTo(_pc, want);
        }

        // ───────────────────────── 對外查詢 ─────────────────────────

        /// <summary>
        /// 存檔旗標裡記著的血統 Id（沒喝過 = 0）。**不驗證它在不在表B**，只有內部用。
        /// </summary>
        static int StoredBloodlineId
        {
            get
            {
                var sm = SaveManager.Instance;
                string v = sm != null ? sm.GetFlagValue(GachaConstants.BloodlineFlagKey) : null;
                if (!string.IsNullOrEmpty(v) && int.TryParse(v, out int id) && id > 0) return id;
                return 0;
            }
        }

        /// <summary>
        /// 本世血統 Id（沒喝過任何藥劑 = 人類）。存在周目旗標，輪迴自動失效。
        ///
        /// ⚠ 存的 Id 在表B 找不到時**當成人類**。這是給舊存檔的救生艇：
        /// 血統表刪過列（2026-08-18 移除了野魂/幽靈），舊角色身上還記著已經不存在的 Id，
        /// 若照實回報會變成「已定型成一個不存在的血統」——起始藥劑喝不了（說你已定為「人類」，自相矛盾）、
        /// 進階藥劑也喝不了（找不到所屬系列），本世血統徹底卡死只能靠輪迴。
        /// 當成未定型就能重新選一次。
        ///
        /// 表還沒載好時 Get 也會回 null → 暫時回報人類，等表載好下一幀自然收斂（Update 是收斂式的）。
        /// 這段期間也喝不了藥（Plan 一樣查得到 null 會擋下），所以不會有「趁表沒載好偷喝第二瓶」的漏洞。
        /// </summary>
        public static int CurrentBloodlineId
        {
            get
            {
                int id = StoredBloodlineId;
                if (id > 0 && BloodlineTable.Get(id) != null) return id;
                return BloodlineTable.HumanId;
            }
        }

        /// <summary>
        /// 本世是否已經選定系列（喝過系列起始藥劑了）。選定後不能再吃別的系列。
        /// 判斷與 <see cref="CurrentBloodlineId"/> 一致：存的 Id 在表B 找不到 = 未定型（見上面的理由）。
        /// </summary>
        public static bool IsFixedThisCycle
        {
            get
            {
                int id = StoredBloodlineId;
                return id > 0 && BloodlineTable.Get(id) != null;
            }
        }

        /// <summary>本世血統的名字（給 UI 顯示）。</summary>
        public static string CurrentDisplayName => BloodlineTable.NameOf(CurrentBloodlineId);

        /// <summary>本世血統所屬的系列；還是人類（或表A 沒登記）時回 null。</summary>
        public static BloodlineSeriesDef CurrentSeries
        {
            get
            {
                BloodlineSeriesTable.TryLocate(CurrentBloodlineId, out var s, out _);
                return s;
            }
        }

        /// <summary>本世血統在系列裡的第幾階（1-based）；還是人類時回 0。</summary>
        public static int CurrentStage
        {
            get
            {
                BloodlineSeriesTable.TryLocate(CurrentBloodlineId, out _, out int stage);
                return stage;
            }
        }

        // ───────────────────────── 喝藥：規劃 ─────────────────────────

        /// <summary>
        /// 「喝這瓶藥會發生什麼事」的規劃結果。
        /// UI 端拿它做三件事：擋下不能喝的（顯示 <see cref="Reason"/>）、
        /// 顯示確認視窗文案（<see cref="ConfirmText"/>）、成功後的 Toast（<see cref="DoneText"/>）。
        /// </summary>
        public struct DrinkPlan
        {
            public bool Ok;
            public string Reason;        // Ok=false 時的說明（直接拿去 Toast）
            public string ConfirmText;   // 確認視窗文案
            public string DoneText;      // 成功後的 Toast
            public int TargetBloodlineId;
        }

        // 多語系 id（2001–2099 = 血統系統）。用 Txt() 帶 fallback，語言表還沒補也不會變成 [lang:xxxx]。
        const int TxtNotBloodline = 2001;
        const int TxtAlreadyFixed = 2002;
        const int TxtNotInTable = 2003;
        const int TxtNoneInBag = 2004;
        const int TxtNoCharacter = 2005;
        const int TxtNotAwakened = 2006;
        const int TxtNoSeries = 2007;
        const int TxtNotMature = 2008;
        const int TxtAlreadyBeyond = 2009;
        const int TxtAtPeak = 2010;
        const int TxtConfirmStarter = 2011;
        const int TxtConfirmUpgrade = 2012;
        const int TxtDoneStarter = 2013;
        const int TxtDoneUpgrade = 2014;
        const int TxtCannotDrink = 2015;

        static string Txt(int id, string fallback)
        {
            string s = null;
            try { s = Language.GetText(id); } catch { }
            if (string.IsNullOrEmpty(s) || s.StartsWith("[")) return fallback;
            return s;
        }

        /// <summary>
        /// 規劃「喝下這個道具」會發生什麼，但**不改變任何狀態**。
        /// UI 在右鍵當下就呼叫它——不能喝的直接說明理由，不要讓玩家按完確認才發現沒反應。
        /// </summary>
        public static DrinkPlan Plan(int itemId)
        {
            var plan = new DrinkPlan { Ok = false };

            var inv = InventorySystem.Instance;
            var data = inv != null ? inv.GetData(itemId) : null;
            if (data == null || !data.IsBloodline)
            {
                plan.Reason = Txt(TxtNotBloodline, "這不是血統藥劑");
                return plan;
            }
            if (inv.CountOf(itemId) <= 0)
            {
                plan.Reason = Txt(TxtNoneInBag, "背包裡沒有這瓶藥劑");
                return plan;
            }

            // ⚠ 一定要先確認「寫得進存檔」才讓玩家喝。
            // SaveManager.SetFlag 在沒有 active character 時是直接 return 的（不是丟例外），
            // 所以「有 SaveManager 但還沒載入角色」時若照喝，會變成：藥劑消失 → 效果套上去 →
            // 下一幀 Update 讀回存檔發現還是人類 → 立刻還原。玩家看到「喝了、閃一下、變回去、藥沒了」。
            var sm = SaveManager.Instance;
            if (sm == null || !sm.HasActiveCharacter)
            {
                plan.Reason = Txt(TxtNoCharacter, "還沒載入角色，現在不能喝");
                return plan;
            }

            return data.IsBloodlineUpgrade ? PlanUpgrade(data) : PlanStarter(data);
        }

        /// <summary>系列起始藥劑：本世還沒定型才能喝。</summary>
        static DrinkPlan PlanStarter(ItemData data)
        {
            var plan = new DrinkPlan { Ok = false };

            var def = BloodlineTable.Get(data.BloodlineID);
            if (def == null)
            {
                plan.Reason = Txt(TxtNotInTable, "這瓶藥劑的血統在血統表找不到");
                Debug.LogWarning($"[BloodlineSystem] 道具 {data.ID}「{data.Name}」的 BloodlineID={data.BloodlineID} " +
                                 "在 BloodlineTable.csv 找不到。");
                return plan;
            }
            if (IsFixedThisCycle)
            {
                plan.Reason = string.Format(Txt(TxtAlreadyFixed, "你的血脈已定為「{0}」，這一世不能再改變"),
                                            CurrentDisplayName);
                return plan;
            }

            plan.Ok = true;
            plan.TargetBloodlineId = def.Id;
            plan.ConfirmText = string.Format(
                Txt(TxtConfirmStarter, "喝下「{0}」？\n血統一世只能決定一次，喝下去就不能反悔。"), data.Name);
            plan.DoneText = string.Format(Txt(TxtDoneStarter, "血脈已定：{0}"), def.DisplayName);
            return plan;
        }

        /// <summary>
        /// 進階藥劑：全系列通用，只認「目標階數」。必須逐階，且不能倒退。
        /// </summary>
        static DrinkPlan PlanUpgrade(ItemData data)
        {
            var plan = new DrinkPlan { Ok = false };
            int targetStage = data.BloodlineUpgrade;

            if (!IsFixedThisCycle)
            {
                plan.Reason = Txt(TxtNotAwakened, "你尚未覺醒任何血脈，無法進階");
                return plan;
            }

            int currentId = CurrentBloodlineId;
            if (!BloodlineSeriesTable.TryLocate(currentId, out var series, out int currentStage))
            {
                // 已定型、但目前的血統不隸屬任何系列（表A 沒登記）→ 資料問題，不是玩家的錯，說清楚。
                plan.Reason = Txt(TxtNoSeries, "你的血脈不屬於任何系列，無法進階");
                Debug.LogWarning($"[BloodlineSystem] 血統 id {currentId} 在 BloodlineSeriesTable.csv 找不到所屬系列，" +
                                 "進階被擋下。請確認表A 有登記它。");
                return plan;
            }

            if (currentStage >= series.StageCount)
            {
                plan.Reason = Txt(TxtAtPeak, "你的血脈已至頂點");
                return plan;
            }
            if (targetStage <= currentStage)
            {
                plan.Reason = Txt(TxtAlreadyBeyond, "你的血脈已在此之上");
                return plan;
            }
            if (targetStage > currentStage + 1)
            {
                // 逐階規則：第 1 階不能直接喝高階藥劑。訊息要指出「還缺哪一階」，不要只說不行。
                int needStage = currentStage + 1;
                string needName = BloodlineTable.NameOf(series.IdOfStage(needStage), $"第 {needStage} 階");
                plan.Reason = string.Format(Txt(TxtNotMature, "血脈尚未成熟，需先進階為「{0}」"), needName);
                return plan;
            }

            int nextId = series.IdOfStage(targetStage);
            var nextDef = BloodlineTable.Get(nextId);
            if (nextId <= 0 || nextDef == null)
            {
                plan.Reason = Txt(TxtNotInTable, "這瓶藥劑的血統在血統表找不到");
                Debug.LogWarning($"[BloodlineSystem] 系列「{series.DisplayName}」第 {targetStage} 階的血統 id={nextId} " +
                                 "在 BloodlineTable.csv 找不到。");
                return plan;
            }

            plan.Ok = true;
            plan.TargetBloodlineId = nextId;
            plan.ConfirmText = string.Format(
                Txt(TxtConfirmUpgrade, "喝下「{0}」？\n你的血脈將從「{1}」進為「{2}」，無法還原。"),
                data.Name, BloodlineTable.NameOf(currentId), nextDef.DisplayName);
            plan.DoneText = string.Format(Txt(TxtDoneUpgrade, "血脈進化：{0}"), nextDef.DisplayName);
            return plan;
        }

        // ───────────────────────── 喝藥：執行 ─────────────────────────

        /// <summary>
        /// 真的喝下去。成功回 true。
        ///
        /// <paramref name="message"/> **成功與失敗都會填**：成功是「血脈已定：殭屍」這種回饋，
        /// 失敗是擋下的理由。呼叫端直接拿去 Toast 就好，不要自己記成功文案——
        /// 內部會重新 <see cref="Plan"/> 一次（確認視窗開著的期間狀態可能變了），
        /// 所以真正發生的事情可能和 UI 幾秒前算出來的不一樣。
        /// </summary>
        public static bool TryDrink(int itemId, out string message)
        {
            var plan = Plan(itemId);
            if (!plan.Ok)
            {
                message = plan.Reason ?? Txt(TxtCannotDrink, "無法飲用");
                return false;
            }

            message = plan.DoneText;
            var inv = InventorySystem.Instance;
            var sm = SaveManager.Instance;

            int fromId = CurrentBloodlineId;
            int toId = plan.TargetBloodlineId;

            sm.SetFlag(GachaConstants.BloodlineFlagKey, toId.ToString());
            inv.RemoveItem(itemId, 1);

            // 立刻套用（不等 Update 那一幀，讓喝下去的回饋是即時的）。
            // 走 PlayTransform 是為了讓「閃電＋煙霧」之後能夾在中間換裝——現在是空實作，等同瞬間換。
            if (_instance != null) _instance.PlayTransform(fromId, toId);

            Debug.Log($"[BloodlineSystem] 血統變更：{BloodlineTable.NameOf(fromId)} → {BloodlineTable.NameOf(toId)}（id {toId}）");
            return true;
        }

        // ───────────────────────── 套用 ─────────────────────────

        /// <summary>
        /// 喝藥造成的血統變更：完整的兩段表演。
        ///   ① 世界演出（<c>BloodlineTransformFxRunner</c>）：倒下 → 天雷 → 煙霧裡換外型 → 爬起。
        ///   ② 立繪揭示（<c>BloodlineIntroPanel</c>）：舊立繪斑駁剝落 → 新立繪浮現 ＋ 血統名。
        /// 兩段之間由 <see cref="PerformanceHoldOwner"/> 這個 hold 接住，中間不會有一幀鬆手。
        /// </summary>
        void PlayTransform(int fromId, int toId)
        {
            if (_pc == null)
            {
                // 玩家還沒生出來（理論上喝不到藥，但保險）：不演出，讓 Update 之後自己收斂。
                _appliedId = -1;
                return;
            }

            // 表演中不再起第二段（理論上進不來：表演期間遊戲暫停、面板全關、操作鎖住）。
            // 真的重入的話兩個 runner 會互搶 PlayerAnimator 的表演回呼與 static 的 IsPlaying，
            // 症狀是角色卡在趴姿、鎖不解。旗標已經寫進存檔了，交給 Update 收斂即可。
            if (_transforming)
            {
                Debug.LogWarning("[BloodlineSystem] 變身表演還沒結束又收到一次變更，略過演出直接收斂。");
                _appliedId = -1;
                return;
            }

            _transforming = true;
            _transformStartedAt = Time.unscaledTime;

            // 從這一刻壓到立繪面板關掉為止。世界演出與面板各自也會鎖自己那一段，
            // 這一層是為了接住兩段之間的縫（見 PerformanceHoldOwner 的說明）。
            var ui = Dipan.UI.UIManager.Instance;
            if (ui != null) ui.SetExternalHold(PerformanceHoldOwner, true, true);

            var fromDef = BloodlineTable.Get(fromId);
            var toDef = BloodlineTable.Get(toId);

            BloodlineTransformFx.Play(
                _pc, fromDef, toDef,
                onSwap: () => ApplyTo(_pc, toId),
                onFinished: () => ShowIntro(fromDef, toDef, toId));
        }

        /// <summary>世界演出結束 → 開立繪揭示面板。面板收掉才真正還玩家自由。</summary>
        void ShowIntro(BloodlineDef fromDef, BloodlineDef toDef, int toId)
        {
            // 玩家死了／不見了就別演立繪了：死亡流程接著要開結算畫面，兩個模態面板疊在一起會打架。
            // （現在整段是暫停播的，演出中被打死幾乎不可能，但換場景/例外仍會走到這裡。）
            if (_pc == null || _pc.IsDead) { FinishPerformance(); return; }

            Dipan.UI.BloodlineIntroPanel.Show(
                fromDef != null ? fromDef.SpriteFolder : null,
                toDef != null ? toDef.SpriteFolder : null,
                BloodlineTable.NameOf(toId),
                FinishPerformance);
        }

        /// <summary>
        /// 整段表演結束：解除收斂鎖與 external hold。
        /// **可以重複呼叫**（正常路徑走一次、保險絲可能再走一次），第二次以後什麼都不做。
        /// </summary>
        void FinishPerformance()
        {
            if (!_transforming) return;
            _transforming = false;
            var ui = Dipan.UI.UIManager.Instance;
            if (ui != null) ui.SetExternalHold(PerformanceHoldOwner, false, false);
        }

        void OnDestroy()
        {
            // 理論上不會發生（本元件與 UIManager 都是 DontDestroyOnLoad），但萬一被外力銷毀，
            // 沒放的 hold 會讓玩家永遠不能動而且完全沒有錯誤訊息。
            if (_instance == this) FinishPerformance();
        }

        void ApplyTo(PlayerController pc, int bloodlineId)
        {
            if (pc == null) return;

            var def = BloodlineTable.Get(bloodlineId);
            if (def == null)
            {
                // 血統表載不到（provider 沒接好）→ 維持現狀，但不要卡在每幀重試。
                Debug.LogWarning($"[BloodlineSystem] 血統 id {bloodlineId} 在 BloodlineTable 找不到，維持原樣。");
                _appliedId = bloodlineId;
                return;
            }

            // 1) 外型與體型：SpriteFolder 留空就不動外型（避免填了不存在的資料夾害角色只剩影子）。
            //    這一行同時決定對話立繪——DramaTalkDatabase 是拿 pc.Bloodline 去組
            //    Characters/Talk/<血統>/<情緒>，所以序列圖與立繪的資料夾必須同名。
            //    ⚠ 體型倍率也要比對：同一個外型資料夾但 BodyScale 被改過（調表後重新載入）也要重跑 Setup，
            //      只比資料夾名的話改 CSV 不會生效。
            if (!string.IsNullOrEmpty(def.SpriteFolder)
                && (pc.Bloodline != def.SpriteFolder || !Mathf.Approximately(pc.BodyScale, def.BodyScale)))
                pc.SetBloodline(def.SpriteFolder, def.BodyScale);

            // 2) 屬性：⚠ 刻意什麼都不做。表B 的五個屬性目前只存不套用（沒有角色屬性系統）。
            //    等屬性系統做好，套用點就在這裡；在那之前不要偷偷改 CombatStats 或 MoveSpeed，
            //    否則會跟未來的屬性系統變成兩套來源打架（舊版就是這樣，已拿掉）。

            // 3) 技能：預留欄位，技能系統還沒做。
            if (def.SkillId > 0)
                Debug.Log($"[BloodlineSystem] 血統「{def.DisplayName}」帶技能 {def.SkillId}，但技能系統尚未實作，先略過。");

            _appliedId = bloodlineId;
        }
    }
}
