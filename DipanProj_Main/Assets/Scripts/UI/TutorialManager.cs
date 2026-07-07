using UnityEngine;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 新手教學（強制引導）。跨場景常駐、開場自動生成、寫死在程式（一次性、每次一樣，不做成可編輯）。
    ///
    /// 兩段：
    ///  A) 找邪佛的手指：玩家觸發「初入場景對話」→ 出現手指指上方；觸發「邪佛全貌」→ 收起。
    ///  B) 傳送門強制流程：偵測到背包出現紅嫁衣劇本(道具 104) → 放開自由跑
    ///     → 走到傳送門可按 F 那一刻定住、只能按 F → 開了傳送門 UI＋背包 → 遮罩＋手指指劇本(只能點劇本)
    ///     → 劇本進方框 → 遮罩＋手指指開啟鈕(只能點鈕) → 按下開門 → 結束（永久記號，之後不再出現）。
    ///
    /// 註：「給完劇本→鏡頭飄去傳送門＋黑幕」那段過場**已改成資料驅動**——由地圖上「鏡頭聚焦(鏈動作)」trigger
    ///     接在對話鏈後面表演（見 readme/TRIGGER_CHAIN.md），不在本程式裡。本程式只管上面的強制互動步驟。
    /// ⚠️ 寫死清單見 readme/TRIGGER_CHAIN.md「新手教學寫死清單」。改動前先看那段。
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        // ── 寫死的值（改這裡＋見文件）──
        const int ScriptItemId = 104;                 // 紅嫁衣劇本
        const string TrigArrive = "初入場景對話";       // 啟動「找邪佛手指」的觸發點名
        const string TrigSawBuddha = "邪佛全貌";        // 收起「找邪佛手指」的觸發點名
        const string DoneFlag = "永久:tutorialPortalDone"; // 傳送門教學做過的永久記號（程式內部用，不進旗標管理器）

        /// <summary>強制階段時鎖住其他快捷鍵（背包/倉庫開關等）。由本管理器控制、別的系統查詢。</summary>
        public static bool HardLock { get; private set; }

        /// <summary>「定住只能按 F」那一刻：即使輸入被擋，也放行 InteractionManager 的 F 互動。</summary>
        public static bool AllowInteract { get; private set; }

        enum Phase { Idle, WaitNear, ForceF, ClickScript, ClickButton, GuideToPortal, Done }
        Phase _phase = Phase.Idle;

        // ScriptsPanel 事件旗標（每次開始重置）
        bool _evtOpened, _evtPlaced, _evtOpenedPortal;
        RectTransform _lockedTarget;   // 目前遮罩/手指鎖在哪個元件（避免每幀重設造成閃爍）
        Vector3 _portalCenter;         // 傳送門世界中心（教學結束後手指指這裡）
        Transform _player;

        InteractionManager Interact => InteractionManager.Instance;

        Transform PlayerT()
        {
            if (_player == null) { var g = GameObject.FindGameObjectWithTag("Player"); if (g != null) _player = g.transform; }
            return _player;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            TriggerChain.OnTriggerFired += OnTriggerFired;
            ScriptsPanel.OnOpened += () => _evtOpened = true;
            ScriptsPanel.OnScriptPlaced += () => _evtPlaced = true;
            ScriptsPanel.OnPortalOpened += () => _evtOpenedPortal = true;
        }

        // ── A) 找邪佛手指（獨立於傳送門流程）：手指在玩家頭上、隨時指向邪佛 ──
        void OnTriggerFired(string name)
        {
            if (name == TrigArrive)
            {
                var p = PlayerT();
                if (p != null && TriggerChain.TryGetRegionCenter(TrigSawBuddha, out Vector2 bc))
                    GuideFingerPanel.ShowWorldGuide(p, new Vector3(bc.x, bc.y, 0f));
            }
            else if (name == TrigSawBuddha) GuideFingerPanel.HidePanel();
        }

        // ── B) 傳送門強制流程 ──
        void Update()
        {
            switch (_phase)
            {
                case Phase.Idle: TickIdle(); break;
                case Phase.WaitNear: TickWaitNear(); break;
                case Phase.ForceF: TickForceF(); break;
                case Phase.ClickScript: TickClickScript(); break;
                case Phase.ClickButton: TickClickButton(); break;
                case Phase.GuideToPortal: TickGuideToPortal(); break;
            }
        }

        bool HasScript() => InventorySystem.Instance != null && InventorySystem.Instance.CountOf(ScriptItemId) > 0;

        void TickIdle()
        {
            // 拿到劇本、而且這輩子還沒做過傳送門教學 → 進入「等玩家走到傳送門」。
            // 註：給完劇本後的「飄鏡頭＋黑幕」過場已改由對話鏈上的「鏡頭聚焦」trigger 負責，這裡不再做。
            //     那段期間玩家被鏡頭聚焦 trigger 定住、走不到傳送門，所以不會提早觸發下面的按 F。
            if (!HasScript() || TriggerChain.FlagTrue(DoneFlag)) return;
            if (Interact == null) return;
            if (!Interact.TryGetPortalWorld(out Vector2 portal)) return;   // 地圖還沒放傳送門就先不跑

            _evtOpened = _evtPlaced = _evtOpenedPortal = false;
            _portalCenter = new Vector3(portal.x, portal.y, 0f);   // 給教學尾聲手指指向用
            GuideFingerPanel.HidePanel();   // 收掉找邪佛手指（保險）
            _phase = Phase.WaitNear;
        }

        void TickWaitNear()
        {
            // 自由階段：走到傳送門可按 F 的範圍就進入強制按 F。
            if (Interact != null && Interact.PlayerNearPortal())
            {
                UIManager.Instance?.SetExternalHold(true, false);   // 定住玩家（不能走/攻擊）
                AllowInteract = true;   // 但放行按 F 開傳送門
                HardLock = true;
                TutorialDimPanel.ShowFullBlack();                   // 整個螢幕壓黑，只剩「按 F」字
                TutorialHintPanel.Show("按 F 開啟傳送門");
                _phase = Phase.ForceF;
            }
        }

        void TickForceF()
        {
            if (_evtOpened)   // F 按下、傳送門 UI 開了
            {
                AllowInteract = false;
                UIManager.Instance?.SetExternalHold(false, false);   // 交給面板接手（面板本身會暫停＋擋輸入）
                TutorialDimPanel.Hide();
                TutorialHintPanel.Hide();
                _phase = Phase.ClickScript;
            }
        }

        void TickClickScript()
        {
            if (_evtPlaced) { _lockedTarget = null; GuideFingerPanel.HidePanel(); TutorialBlockerPanel.Unlock(); _phase = Phase.ClickButton; return; }
            if (ScriptsPanel.OpenInstance() == null) { AbortToWaitNear(); return; }   // 中途關掉了 → 退回

            // 指向背包裡那格劇本、只允許點它。**只在目標改變時重設**（每幀重設會讓遮罩/hover 一直閃）。
            var bag = UIManager.Instance?.Get<InventoryPanel>();
            var slot = bag != null ? bag.FindGridSlotRect(ScriptItemId) : null;
            if (slot != null && slot != _lockedTarget)
            {
                _lockedTarget = slot;
                TutorialHintPanel.Show("點一下背包裡的劇本");
                GuideFingerPanel.ShowAtUI(slot);
                TutorialBlockerPanel.LockTo(slot.gameObject);
            }
        }

        void TickClickButton()
        {
            if (_evtOpenedPortal) { StartGuideToPortal(); return; }
            if (ScriptsPanel.OpenInstance() == null) { AbortToWaitNear(); return; }

            var p = ScriptsPanel.OpenInstance();
            var btn = p != null ? p.ButtonRect : null;
            if (btn != null && btn != _lockedTarget)
            {
                _lockedTarget = btn;
                TutorialHintPanel.Show("按下按鈕，開啟傳送門");
                GuideFingerPanel.ShowAtUI(btn);
                TutorialBlockerPanel.LockTo(btn.gameObject);
            }
        }

        void AbortToWaitNear()
        {
            _lockedTarget = null;
            GuideFingerPanel.HidePanel();
            TutorialBlockerPanel.Unlock();
            TutorialHintPanel.Hide();
            TutorialDimPanel.Hide();
            HardLock = false;
            AllowInteract = false;
            _phase = Phase.WaitNear;
        }

        // 按下開啟後：不再強制，改成手指指引玩家走去傳送門（走近就收手指）。
        void StartGuideToPortal()
        {
            _lockedTarget = null;
            TutorialBlockerPanel.Unlock();
            TutorialHintPanel.Hide();
            HardLock = false;
            AllowInteract = false;
            UIManager.Instance?.SetExternalHold(false, false);
            TriggerChain.SetFlag(DoneFlag);   // 永久：之後不再重播整段教學

            var p = PlayerT();
            if (p != null) GuideFingerPanel.ShowWorldGuide(p, _portalCenter);   // 頭上手指指向傳送門
            else GuideFingerPanel.HidePanel();
            _phase = Phase.GuideToPortal;
        }

        void TickGuideToPortal()
        {
            var p = PlayerT();
            // 走到傳送門附近就收手指（之後踩進去傳送、換圖時手指本來也會自動關）。
            if (p == null || ((Vector2)p.position - (Vector2)_portalCenter).sqrMagnitude < 1.6f * 1.6f)
            {
                GuideFingerPanel.HidePanel();
                _phase = Phase.Done;
            }
        }
    }

    /// <summary>開場前自動生出教學管理器。</summary>
    public static class TutorialBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (TutorialManager.Instance != null) return;
            var go = new GameObject("[TutorialManager]");
            go.AddComponent<TutorialManager>();
        }
    }
}
