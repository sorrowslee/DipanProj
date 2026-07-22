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

        // ── 柴房佛燈教學（紅嫁衣關第一間房）寫死的值 ──
        const string LampStartTrig = "柴房教學起點";       // onEnter 進場觸發點名：一進柴房自動啟動佛燈教學
        const string LampPickupTrig = "柴房佛燈";          // 佛燈 pickup 觸發點名（手指指向它＋撿完由此名廣播）
        const int LampItemId = 8;                          // 佛燈＝道具/武器 8（佛光；ItemTable 8 WeaponID=8）
        const string LampTakenFlag = "永久:woodshedLampTaken";      // 撿走佛燈（pickup setFlag；也驅動佛燈地上物 disappearFlag）
        const string LampDoneFlag = "永久:woodshedLampTutorialDone"; // 整段佛燈教學做過的永久記號（做完不再強制引導）

        /// <summary>強制階段時鎖住其他快捷鍵（背包/倉庫開關等）。由本管理器控制、別的系統查詢。</summary>
        public static bool HardLock { get; private set; }

        /// <summary>「定住只能按 F」那一刻：即使輸入被擋，也放行 InteractionManager 的 F 互動。</summary>
        public static bool AllowInteract { get; private set; }

        /// <summary>佛燈教學「按 B 開/關背包」步驟：即使 HardLock 鎖著別的快捷鍵，也放行 B 鍵。由 StorageBagCoordinator 查詢。</summary>
        public static bool AllowBag { get; private set; }

        enum Phase
        {
            Idle, WaitNear, ForceF, ClickScript, ClickButton, GuideToPortal, Done,
            // ── 柴房佛燈教學 ──
            LampGuidePick,   // 手指指向佛燈、自由走過去
            LampForceF,      // 走到可撿範圍→定住只能按 F
            LampOpenBag,     // 提示按 B 打開背包
            LampClickEquip,  // 手指指佛燈格、只放行它→點一下裝備
            LampCloseBag,    // 提示按 B 關閉背包
            LampLight,       // 放行→按住左鍵/空白鍵點亮佛燈
        }
        Phase _phase = Phase.Idle;

        // ScriptsPanel 事件旗標（每次開始重置）
        bool _evtOpened, _evtPlaced, _evtOpenedPortal;
        RectTransform _lockedTarget;   // 目前遮罩/手指鎖在哪個元件（避免每幀重設造成閃爍）
        Vector3 _portalCenter;         // 傳送門世界中心（教學結束後手指指這裡）
        Transform _player;

        // 佛燈教學用
        bool _lampPicked;              // 佛燈 pickup 已完成（由 OnTriggerFired 收 LampPickupTrig 設）
        float _auraHoldTimer;          // 佛光已持續開著的累計秒數（點亮判定要稍微持續，避免誤觸）
        PlayerController _pc;           // 玩家控制器（讀 IsAuraActive）

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
            else if (name == LampStartTrig) BeginLampTutorial();   // 一進柴房→啟動佛燈教學
            else if (name == LampPickupTrig) _lampPicked = true;    // 佛燈撿完（pickup 完成廣播）
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
                case Phase.LampGuidePick: TickLampGuidePick(); break;
                case Phase.LampForceF: TickLampForceF(); break;
                case Phase.LampOpenBag: TickLampOpenBag(); break;
                case Phase.LampClickEquip: TickLampClickEquip(); break;
                case Phase.LampCloseBag: TickLampCloseBag(); break;
                case Phase.LampLight: TickLampLight(); break;
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

        // ═════════════════════ 柴房佛燈教學（強制引導）═════════════════════
        // 一進柴房（onEnter「柴房教學起點」）→ 走去撿佛燈（強制按 F）→ 按 B 開背包→手指指佛燈格→點一下裝備→按 B 關背包
        // → 放行→按住左鍵/空白鍵點亮佛燈 → 完成（寫永久旗標，之後不再強制）。

        PlayerController PC()
        {
            if (_pc == null) { var g = GameObject.FindGameObjectWithTag("Player"); if (g != null) _pc = g.GetComponent<PlayerController>(); }
            return _pc;
        }

        void BeginLampTutorial()
        {
            if (TriggerChain.FlagTrue(LampDoneFlag)) return;               // 整段做過了（永久）
            if (_phase != Phase.Idle && _phase != Phase.Done) return;      // 正在別的教學流程中，不插隊
            _lampPicked = false;
            _auraHoldTimer = 0f;
            GuideFingerPanel.HidePanel();

            // 「是否已撿過『這盞教學佛燈』」只認 LampTakenFlag（pickup 專屬旗標）。
            // 不可用「背包裡有沒有道具 8」判斷——測試存檔/起始裝可能本來就有佛光，會誤判成已撿而跳過撿取步驟。
            if (!TriggerChain.FlagTrue(LampTakenFlag))
            {
                // 從頭：手指指向佛燈、提示走過去撿。
                if (Interact != null && Interact.TryGetPickupWorld(LampItemId, out Vector2 lw) && PlayerT() != null)
                    GuideFingerPanel.ShowWorldGuide(PlayerT(), new Vector3(lw.x, lw.y, 0f));
                TutorialHintPanel.Show("走過去，撿起佛燈（靠近按 F）");
                _phase = Phase.LampGuidePick;
                return;
            }

            // 已撿過（可能中途離開再回來）：已裝→教點亮；未裝→教裝備。
            var inv = InventorySystem.Instance;
            bool equipped = inv != null && inv.GetEquipped(Dipan.Inventory.EquipSlot.Weapon) == LampItemId;
            if (equipped) StartLampLight(); else StartLampOpenBag();
        }

        void TickLampGuidePick()
        {
            if (Interact == null) return;

            // 佛燈拾取點不在：可能是「已被撿走」也可能是「互動點還沒建好」。只有確實已撿走才跳去教裝備，
            // 否則（點還在建）就先等，避免載入 race 誤跳過撿取。
            if (!Interact.TryGetPickupWorld(LampItemId, out Vector2 lw))
            {
                // 只認撿取旗標（不看背包道具 8，避免起始裝誤判）；點還沒建好就先等。
                if (TriggerChain.FlagTrue(LampTakenFlag)) StartLampOpenBag();
                return;
            }

            // 手指指向佛燈（沒開才開，避免每幀重開閃爍；手指根部跟玩家、指尖朝佛燈由 GuideFingerPanel 自己更新）。
            var finger = UIManager.Instance?.Get<GuideFingerPanel>();
            var p = PlayerT();
            if ((finger == null || !finger.IsOpen) && p != null)
                GuideFingerPanel.ShowWorldGuide(p, new Vector3(lw.x, lw.y, 0f));

            // 走到可撿範圍→定住只能按 F。
            if (Interact.PlayerNearPickup(LampItemId))
            {
                UIManager.Instance?.SetExternalHold(true, false);   // 定住玩家（不能走/攻擊）
                AllowInteract = true;                                // 放行按 F 撿
                HardLock = true;
                GuideFingerPanel.HidePanel();
                TutorialHintPanel.Show("按 F 撿起佛燈");
                _phase = Phase.LampForceF;
            }
        }

        void TickLampForceF()
        {
            if (_lampPicked) StartLampOpenBag();   // 佛燈撿完（OnTriggerFired 收到 LampPickupTrig）→ 教裝備
        }

        void StartLampOpenBag()
        {
            _lockedTarget = null;
            GuideFingerPanel.HidePanel();
            AllowInteract = false;
            UIManager.Instance?.SetExternalHold(true, false);   // 續定住玩家（不能走/攻擊）
            HardLock = true;                                     // 鎖倉庫等快捷鍵
            AllowBag = true;                                     // 但放行 B 開背包
            TutorialHintPanel.Show("按 B 打開背包");
            _phase = Phase.LampOpenBag;
        }

        void TickLampOpenBag()
        {
            var bag = UIManager.Instance?.Get<InventoryPanel>();
            if (bag != null && bag.IsOpen)
            {
                AllowBag = false;   // 開了就先鎖 B（避免提前關）；接著只准點佛燈格
                _lockedTarget = null;
                TutorialHintPanel.Show("點一下佛燈，裝備它");
                _phase = Phase.LampClickEquip;
            }
        }

        void TickLampClickEquip()
        {
            var inv = InventorySystem.Instance;
            if (inv != null && inv.GetEquipped(Dipan.Inventory.EquipSlot.Weapon) == LampItemId)
            {
                _lockedTarget = null;
                GuideFingerPanel.HidePanel();
                TutorialBlockerPanel.Unlock();
                AllowBag = true;                       // 放行 B 關背包
                TutorialHintPanel.Show("按 B 關閉背包");
                _phase = Phase.LampCloseBag;
                return;
            }

            var bag = UIManager.Instance?.Get<InventoryPanel>();
            if (bag == null || !bag.IsOpen) { StartLampOpenBag(); return; }   // 背包被關掉（ESC）→回到「按 B 開背包」

            // 指向背包裡佛燈那格、只放行它（只在目標改變時重設，避免閃爍）。
            var slot = bag.FindGridSlotRect(LampItemId);
            if (slot != null && slot != _lockedTarget)
            {
                _lockedTarget = slot;
                GuideFingerPanel.ShowAtUI(slot);
                TutorialBlockerPanel.LockTo(slot.gameObject);
            }
        }

        void TickLampCloseBag()
        {
            var bag = UIManager.Instance?.Get<InventoryPanel>();
            if (bag == null || !bag.IsOpen) StartLampLight();
        }

        void StartLampLight()
        {
            _lockedTarget = null;
            GuideFingerPanel.HidePanel();
            TutorialBlockerPanel.Unlock();
            HardLock = false;
            AllowBag = false;
            AllowInteract = false;
            _auraHoldTimer = 0f;
            UIManager.Instance?.SetExternalHold(false, false);   // 放行玩家：可攻擊/開佛光
            TutorialHintPanel.Show("按住左鍵或空白鍵，點亮佛燈");
            _phase = Phase.LampLight;
        }

        void TickLampLight()
        {
            var pc = PC();
            if (pc != null && pc.IsAuraActive) _auraHoldTimer += Time.deltaTime;
            else _auraHoldTimer = 0f;

            if (_auraHoldTimer >= 0.35f)   // 稍微持續＝確實點亮（避免手滑一下就過）
            {
                TutorialHintPanel.Hide();
                GuideFingerPanel.HidePanel();
                TriggerChain.SetFlag(LampDoneFlag);   // 永久：整段教學完成，之後不再強制引導
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
