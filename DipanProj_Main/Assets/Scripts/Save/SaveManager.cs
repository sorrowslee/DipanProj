using System;
using System.Collections.Generic;
using UnityEngine;
using Dipan.Inventory;

namespace Dipan.Save
{
    /// <summary>
    /// 存檔系統的大腦（跨場景常駐單例，仿 MapManager / UIManager 的全程式建構）。
    /// 負責：載入活躍角色 → 還原到各系統；標記 dirty + 自動存 + 退出/暫停存；建立/刪除/轉生角色。
    ///
    /// 解耦：本類別**依賴**遊戲系統（InventorySystem…），遊戲系統不依賴它（只開 Capture/RestoreState）。
    /// 由 SaveBootstrap 在開場前自動生出，零手動接線。執行順序設早（-500），確保「載入存檔」早於
    /// InventoryLauncher 的「空背包才塞測試物品」——所以重開遊戲時載回的物品不會被測試種子覆蓋/重複。
    /// 見 readme/SAVE_SYSTEM.md。
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        /// <summary>
        /// 開機時是否略過「自動載入/建立活躍角色」。由 GameFlowManager 在開機時設 true——
        /// 改由標題→存讀檔 UI 決定要載哪個欄位或新建，而不是一進場就自動生 test001。
        /// GameFlow 不存在時（純測試場景）維持 false = 舊行為（自動載入/建 test001）。
        /// </summary>
        public static bool SuppressAutoLoad = false;

        /// <summary>
        /// 測試模式（DevQuickStart「直接進關卡」）：開機時不載入舊角色，改建立一個乾淨的一次性測試角色
        /// （generation=1、所有旗標／進度／背包全空）。每次按 Play 進來都先砍掉上一個 dev 角色再重建，
        /// 保證每趟測試都是全新狀態（例如可反覆測新手教學）。由 DevQuickStart 設 true、PlayModeStaticReset 每次 Play 歸零。
        /// 選「關閉（走正式流程）」時維持 false，完全走正式三欄存檔、不受影響。見 readme/SAVE_SYSTEM.md §10。
        /// </summary>
        public static bool DevFreshCharacter = false;

        /// <summary>
        /// 測試用：建立 dev 測試角色時，順便把這些 module 標記成「已通關」。
        /// 給「要通過 N 關才會出現的東西」測試用（例如祭壇的地上物出現條件 appearAfterClears）——
        /// 不然每次都得真的從頭把關卡打完一次才看得到。
        /// 由 <c>DevQuickStart</c>（Editor-only）設定；null／空＝不預先通關。
        /// </summary>
        public static string[] DevPreClearedModules = null;

        /// <summary>一次性測試角色的固定 id（永遠 slotIndex=-1，不會出現在正式三欄）。每次 dev 進場砍掉重建同一個。</summary>
        public const string DevCharacterId = "__dev_quickstart__";

        [Header("自動存檔")]
        [Tooltip("有變動時，每隔幾秒自動存一次。")]
        public float autoSaveIntervalSeconds = 90f;

        [Header("測試快捷鍵")]
        public KeyCode manualSaveKey = KeyCode.F5;   // 手動存
        public KeyCode reloadKey = KeyCode.F9;       // 從磁碟重載活躍角色（驗證存讀）

        ProfileRoster _roster;
        CharacterSave _current;
        InventorySystem _inv;        // 快取參照：避免在 OnDestroy/退出期間用懶漢 Instance getter 又 new 出新物件
        StorageSystem _storage;
        bool _dirty;
        float _autoTimer;
        DateTime _sessionStartUtc;

        public CharacterSave Current => _current;
        public string CurrentCharacterId => _current != null ? _current.characterId : null;
        public ProfileRoster Roster => _roster;

        /// <summary>取背包/倉庫（快取；只在正常流程用，OnDestroy 一律用欄位、不走這裡）。</summary>
        InventorySystem Inv => _inv != null ? _inv : (_inv = InventorySystem.Instance);
        StorageSystem Storage => _storage != null ? _storage : (_storage = StorageSystem.Instance);

        static string NowUtc() => DateTime.UtcNow.ToString("o");

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _roster = SaveSystem.LoadRoster();
        }

        void Start()
        {
            _inv = InventorySystem.Instance;                   // 快取一次（此時建立沒問題）
            _storage = StorageSystem.Instance;
            if (DevFreshCharacter)
                CreateOrResetDevCharacter();                   // 測試模式：砍掉舊 dev 角色、建一個全新乾淨角色（所有旗標歸零）
            else if (!SuppressAutoLoad)
                LoadActiveOrCreateDefault();                   // 先載入（此時尚未訂閱，不會被自己的 Raise 標 dirty）
            // SuppressAutoLoad = true 時不自動載：由 GameFlowManager 依玩家在存讀檔畫面的選擇呼叫 LoadSlot/StartNewGameInSlot。
            _inv.OnChanged += MarkDirty;                       // 之後的背包/倉庫變動才標記待存
            _storage.OnChanged += MarkDirty;
            _autoTimer = autoSaveIntervalSeconds;
            _sessionStartUtc = DateTime.UtcNow;
        }

        void OnDestroy()
        {
            // 只用快取參照退訂——不呼叫 Instance getter（否則對方已被銷毀時會在 OnDestroy 期間又 new 一個，
            // 觸發「Some objects were not cleaned up when closing the scene」警告）。
            // 被銷毀的物件 Unity 的 == 會視為 null，下面判斷自動跳過。
            if (Instance == this)
            {
                if (_inv != null) _inv.OnChanged -= MarkDirty;
                if (_storage != null) _storage.OnChanged -= MarkDirty;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(manualSaveKey)) SaveNow();
            if (Input.GetKeyDown(reloadKey)) ReloadActiveFromDisk();

            if (_dirty)
            {
                _autoTimer -= Time.unscaledDeltaTime;
                if (_autoTimer <= 0f) { SaveNow(); _autoTimer = autoSaveIntervalSeconds; }
            }
        }

        void OnApplicationQuit() => SaveNow();
        void OnApplicationPause(bool paused) { if (paused) SaveNow(); }

        void MarkDirty() => _dirty = true;

        // ───────────── 載入 / 建立 ─────────────

        void LoadActiveOrCreateDefault()
        {
            CharacterProfile prof = _roster.Find(_roster.activeCharacterId);
            if (prof == null)
                prof = _roster.characters.Find(c => c != null && !c.corrupt);   // 沒有指定活躍 → 挑第一個健康的

            if (prof == null) { CreateCharacter(SaveConstants.DefaultTestCharacterName); return; }

            if (SaveSystem.LoadCharacter(prof.characterId, out var save, out var status))
            {
                if (status == LoadStatus.RecoveredFromBackup)
                    Debug.LogWarning($"[SaveManager] 角色「{prof.name}」主檔損毀，已從備份還原。");
                _current = save;
                _roster.activeCharacterId = save.characterId;
                ApplyToSystems(save);
                Debug.Log($"[SaveManager] 載入角色：{save.name}（第 {save.generation} 代，id={save.characterId}）");
            }
            else
            {
                prof.corrupt = true;
                SaveSystem.SaveRoster(_roster);
                Debug.LogError($"[SaveManager] 角色「{prof.name}」存檔損毀且無法救回，建立新測試角色。");
                CreateCharacter(SaveConstants.DefaultTestCharacterName);
            }
        }

        /// <summary>
        /// 測試模式專用：砍掉上一個一次性測試角色（固定 <see cref="DevCharacterId"/>）再建一個全新乾淨的。
        /// generation=1、progress/stats/lifetimeFlags 全空、背包清空 → 所有旗標（含永久旗標如新手教學）歸零，
        /// 每次按 Play 直接進關卡都是全新狀態。slotIndex 固定 -1 → 永遠不會出現在正式三欄、也不會被正式流程載到。
        /// 會設為活躍角色，之後 F5/F9/自動存都寫到這個 dev 角色，不會動到玩家的正式存檔。見 readme/SAVE_SYSTEM.md §10。
        /// </summary>
        void CreateOrResetDevCharacter()
        {
            // 砍：清掉上一輪 dev 角色的存檔與名冊項（若在）。用固定 id 回收，磁碟上永遠只留一個 dev 角色。
            DeleteCharacter(DevCharacterId);

            // 重建：固定 id、全空的一次性測試角色。
            var save = new CharacterSave
            {
                characterId = DevCharacterId,
                name = "DEV(測試)",
                slotIndex = -1,
                generation = 1,
                createdAtUtc = NowUtc(),
                lastPlayedUtc = NowUtc(),
            };
            _current = save;
            ApplyToSystems(save);   // 清空背包/倉庫

            _roster.characters.Add(ToProfile(save));
            _roster.activeCharacterId = save.characterId;

            // 測試用「預先通關」：把指定 module 記成已通關，讓「要通過 N 關才出現」的東西直接看得到。
            // 走 MarkModuleCleared 而不是自己塞 list，才會一併更新跨輪迴高水位 lifetimeMaxClears
            // （地上物出現條件的 appearScope=lifetime 讀的是那一個）。
            string preCleared = "";
            if (DevPreClearedModules != null)
            {
                foreach (string m in DevPreClearedModules)
                {
                    if (string.IsNullOrWhiteSpace(m)) continue;
                    MarkModuleCleared(m.Trim());
                    preCleared += (preCleared.Length > 0 ? "、" : "") + m.Trim();
                }
                // roster 摘要（存讀檔畫面顯示的「完成 N 關」）也要跟著更新，否則卡片會顯示 0。
                var prof = _roster.Find(save.characterId);
                if (prof != null) prof.clearedModuleCount = save.progress.clearedModules.Count;
            }

            SaveSystem.SaveCharacter(save);
            SaveSystem.SaveRoster(_roster);
            Debug.Log("[SaveManager] 測試模式：已建立全新測試角色 DEV(測試)，所有旗標/進度歸零。" +
                      (preCleared.Length > 0 ? $"（已預先標記通關：{preCleared}）" : "") +
                      "（要走正式存檔：選單 → 直接進關卡 → 關閉）");
        }

        /// <summary>建立新角色並設為活躍（背包清空）。slotIndex 指定存檔欄位（-1 = 不指定）。回傳該存檔。</summary>
        public CharacterSave CreateCharacter(string name, int slotIndex = -1)
        {
            var save = new CharacterSave
            {
                characterId = Guid.NewGuid().ToString("N"),
                name = string.IsNullOrWhiteSpace(name) ? SaveConstants.DefaultTestCharacterName : name,
                slotIndex = slotIndex,
                generation = 1,
                createdAtUtc = NowUtc(),
                lastPlayedUtc = NowUtc(),
            };
            _current = save;
            ApplyToSystems(save);   // 清空背包

            _roster.characters.Add(ToProfile(save));
            _roster.activeCharacterId = save.characterId;

            SaveSystem.SaveCharacter(save);
            SaveSystem.SaveRoster(_roster);
            Debug.Log($"[SaveManager] 建立新角色：{save.name}（id={save.characterId}）");
            return save;
        }

        /// <summary>
        /// 轉生：先存當前角色（保留其檔），再建立繼承 carryItemId 的新角色、世代 +1、設為活躍。
        /// 採多角色機制 → 舊角色檔預設保留（不刪）。見 readme/SAVE_SYSTEM.md §5.2。
        /// </summary>
        public CharacterSave Reincarnate(int carryItemId, string newName)
        {
            int prevGen = _current != null ? _current.generation : 0;
            SaveNow();   // 落定舊角色

            var save = new CharacterSave
            {
                characterId = Guid.NewGuid().ToString("N"),
                name = string.IsNullOrWhiteSpace(newName) ? SaveConstants.DefaultTestCharacterName : newName,
                generation = prevGen + 1,
                createdAtUtc = NowUtc(),
                lastPlayedUtc = NowUtc(),
            };
            save.progress.inheritedItemId = carryItemId;

            _current = save;
            ApplyToSystems(save);                                  // 清空
            if (carryItemId > 0) Inv.AddItem(carryItemId, 1);      // 繼承一樣物品

            _roster.characters.Add(ToProfile(save));
            _roster.activeCharacterId = save.characterId;

            SaveSystem.SaveCharacter(save);
            SaveSystem.SaveRoster(_roster);
            Debug.Log($"[SaveManager] 轉生 → 第 {save.generation} 代「{save.name}」，繼承物品 ID {carryItemId}");
            return save;
        }

        /// <summary>切換到名冊裡另一個角色（載入其存檔）。</summary>
        public bool SwitchCharacter(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return false;
            SaveNow();
            if (!SaveSystem.LoadCharacter(characterId, out var save, out _)) return false;
            _current = save;
            _roster.activeCharacterId = characterId;
            ApplyToSystems(save);
            SaveSystem.SaveRoster(_roster);
            return true;
        }

        /// <summary>刪除某角色（檔案 + 名冊）。刪到活躍角色會清空當前參照。</summary>
        public void DeleteCharacter(string characterId)
        {
            SaveSystem.DeleteCharacter(characterId);
            _roster.characters.RemoveAll(c => c != null && c.characterId == characterId);
            if (_roster.activeCharacterId == characterId) _roster.activeCharacterId = null;
            SaveSystem.SaveRoster(_roster);
        }

        // ───────────── 存檔欄位（槽位）API ─────────────
        //
        // 存讀檔畫面有 SlotCount 個欄位；一個欄位 = 一個角色（同一 slotIndex）。
        // roster 只保留每欄最新的一個角色（覆蓋/刪除會清掉舊的）。

        /// <summary>是否有活躍角色（false = 還在標題/存讀檔畫面、尚未載入任何進度）。</summary>
        public bool HasActiveCharacter => _current != null;

        /// <summary>取某欄位的摘要（沒有或損毀回 null）。給存讀檔 UI 畫卡片用（只讀 roster、不載完整檔）。</summary>
        public CharacterProfile GetSlotProfile(int slotIndex)
        {
            if (_roster == null || _roster.characters == null) return null;
            return _roster.characters.Find(c => c != null && !c.corrupt && c.slotIndex == slotIndex);
        }

        /// <summary>某欄位是否已有存檔。</summary>
        public bool SlotOccupied(int slotIndex) => GetSlotProfile(slotIndex) != null;

        /// <summary>
        /// 在某欄位新建遊戲：若該欄已有角色先刪掉（＝覆蓋），再建一個 generation=1 的空角色並設為活躍。
        /// 覆蓋前的「是否確認」由 UI 負責（見 SaveSlotPanel）。回傳新存檔。
        /// </summary>
        public CharacterSave StartNewGameInSlot(int slotIndex, string name)
        {
            var existing = GetSlotProfile(slotIndex);
            if (existing != null) DeleteCharacter(existing.characterId);   // 覆蓋：清掉舊角色
            return CreateCharacter(name, slotIndex);
        }

        /// <summary>載入某欄位的存檔並設為活躍。回傳成功與否。</summary>
        public bool LoadSlot(int slotIndex)
        {
            var prof = GetSlotProfile(slotIndex);
            if (prof == null) return false;
            return SwitchCharacter(prof.characterId);
        }

        /// <summary>刪除某欄位的存檔（測試用；之後要不要開給玩家再說）。</summary>
        public void DeleteSlot(int slotIndex)
        {
            var prof = GetSlotProfile(slotIndex);
            if (prof != null) DeleteCharacter(prof.characterId);
        }

        // ───────────── 輪迴（in-place：同一欄位、周目 +1、重置進度、帶物）─────────────

        /// <summary>
        /// 輪迴：在「同一個存檔欄位」把當前角色重開一輪——generation(周目) +1、進度全部歸零、
        /// 只帶入 carryItemIds 指定的物品（數量會夾到 min(周目, MaxCarryOnReincarnate)），
        /// hubIntroSpawnDone 重置（回到「第一次進廣場」）。**倉庫不動**。
        /// 帶哪幾件由玩家在輪迴選物 UI 決定（本方法只做資料重置＋塞入），回到廣場的移動由 GameFlowManager 處理。
        /// </summary>
        public void ReincarnateInPlace(IList<int> carryItemIds)
        {
            if (_current == null) return;

            int leavingCycle = _current.generation;                 // 要離開的周目
            int allowed = CarryCountForCycle(leavingCycle);         // 這次可帶幾件
            var carried = new List<int>();
            if (carryItemIds != null)
                for (int i = 0; i < carryItemIds.Count && carried.Count < allowed; i++)
                    if (carryItemIds[i] > 0) carried.Add(carryItemIds[i]);

            // 重置：周目 +1、屬性/進度全新（記錄本代帶入物品），背包清空後塞回帶入物品。倉庫保留不動。
            _current.generation = leavingCycle + 1;
            _current.stats = new StatsDTO();
            _current.progress = new ProgressDTO { inheritedItems = new List<int>(carried) };

            Inv.RestoreState(null);                                 // 清空背包/裝備
            foreach (int id in carried) Inv.AddItem(id, 1);         // 帶入物品（吃堆疊規則）

            SaveNow();
            Debug.Log($"[SaveManager] 輪迴 → 第 {_current.generation} 周目（帶入 {carried.Count} 件），進度已重置。");
        }

        // ───────────── 進度 API（周目 / 完成關卡 / 金錢 / 出生點旗標）─────────────
        //
        // 進度直接掛在 _current 上，用這些方法讀寫並標 dirty；存檔時 SaveNow 一併寫入。
        // 「關卡」= MapsTable 的一個 Module（如 "RedBridalGown"）。見 readme/SAVE_SYSTEM.md。

        /// <summary>大進度：周目（= 轉生世代 generation）。</summary>
        public int Cycle => _current != null ? _current.generation : 0;

        /// <summary>小進度：已完成的關卡數（去重後的 module 數）。</summary>
        public int ClearedModuleCount => _current != null ? _current.progress.clearedModules.Count : 0;

        /// <summary>曾達到過的最高「完成關卡數」（跨輪迴保存）。給地上物「永久出現」條件用。
        /// 取「存下的高水位」與「本周目當前完成數」的較大者，涵蓋這欄加入前就已通關的舊存檔冷啟動。</summary>
        public int LifetimeMaxClears
            => _current == null ? 0 : System.Math.Max(_current.lifetimeMaxClears, _current.progress.clearedModules.Count);

        /// <summary>金錢（存錢抽關卡用）。</summary>
        public int Currency => _current != null ? _current.stats.currency : 0;

        /// <summary>是否已由開場鏈首次抵達邪佛廣場（決定出生點：洞穴出口 vs 中央）。</summary>
        public bool HubIntroSpawnDone
        {
            get => _current != null && _current.progress.hubIntroSpawnDone;
            set { if (_current != null && _current.progress.hubIntroSpawnDone != value) { _current.progress.hubIntroSpawnDone = value; MarkDirty(); } }
        }

        /// <summary>上次所在的地圖（只會是 Main module 的圖；0 = 沒有記錄）。給「繼續遊戲」決定落點用。</summary>
        public int LastMapId => _current != null ? _current.progress.lastMapId : 0;

        /// <summary>上次所在地圖的落點名（可能為 null＝用該圖的 playerSpawn）。</summary>
        public string LastEntrance => _current != null ? _current.progress.lastEntrance : null;

        /// <summary>
        /// 記下「玩家現在在哪」。由 MapManager 在進入 <b>Main module</b> 的地圖時呼叫；關卡不呼叫。
        /// 值沒變就不寫（避免每次換圖都把存檔標成 dirty）。
        /// </summary>
        public void RecordLastLocation(int mapId, string entrance)
        {
            if (_current == null || mapId <= 0) return;
            var p = _current.progress;
            if (p.lastMapId == mapId && p.lastEntrance == entrance) return;
            p.lastMapId = mapId;
            p.lastEntrance = entrance;
            MarkDirty();
        }

        /// <summary>
        /// 標記某關卡（module）為已通關。idempotent：重複通關同一關不會重覆計數。
        /// 由各關卡的「達成目標」trigger 呼叫。回傳 true 代表這是「第一次」通關（進度 +1）。
        /// </summary>
        public bool MarkModuleCleared(string moduleId)
        {
            if (_current == null || string.IsNullOrEmpty(moduleId)) return false;
            var list = _current.progress.clearedModules;
            if (list.Contains(moduleId)) return false;       // 已通關過 → 不算進度 +1
            list.Add(moduleId);
            if (list.Count > _current.lifetimeMaxClears) _current.lifetimeMaxClears = list.Count;  // 更新跨輪迴高水位（永久出現條件用）
            MarkDirty();
            Debug.Log($"[SaveManager] 完成關卡：{moduleId}（完成關卡數 {list.Count}）");
            return true;
        }

        /// <summary>某關卡（module）是否已通關。</summary>
        public bool IsModuleCleared(string moduleId)
            => _current != null && !string.IsNullOrEmpty(moduleId) && _current.progress.clearedModules.Contains(moduleId);

        /// <summary>解鎖（抽到）某關卡（module）。idempotent。</summary>
        public bool MarkModuleUnlocked(string moduleId)
        {
            if (_current == null || string.IsNullOrEmpty(moduleId)) return false;
            var list = _current.progress.unlockedModules;
            if (list.Contains(moduleId)) return false;
            list.Add(moduleId);
            MarkDirty();
            return true;
        }

        /// <summary>某關卡（module）是否已解鎖。</summary>
        public bool IsModuleUnlocked(string moduleId)
            => _current != null && !string.IsNullOrEmpty(moduleId) && _current.progress.unlockedModules.Contains(moduleId);

        /// <summary>自訂進度旗標是否成立（progress.flags[key] == "1"）。給觸發鏈的 requireFlag/enableFlag 等用，見 readme/TRIGGER_CHAIN.md。</summary>
        public bool GetFlag(string key)
            => _current != null && !string.IsNullOrEmpty(key)
               && _current.progress.flags != null
               && _current.progress.flags.TryGetValue(key, out var v) && v == "1";

        /// <summary>寫自訂進度旗標（預設 "1"）。idempotent；跟其他進度一樣走 MarkDirty → SaveNow 落盤。</summary>
        public void SetFlag(string key, string value = "1")
        {
            if (_current == null || string.IsNullOrEmpty(key)) return;
            if (_current.progress.flags == null) _current.progress.flags = new System.Collections.Generic.Dictionary<string, string>();
            if (_current.progress.flags.TryGetValue(key, out var old) && old == value) return;
            _current.progress.flags[key] = value;
            MarkDirty();
            Debug.Log($"[SaveManager] 旗標 {key} = {value}");
        }

        /// <summary>
        /// 讀自訂進度旗標的**原始字串值**（沒有這個 key 回空字串）。
        /// <see cref="GetFlag"/> 只回答「是不是 1」，但旗標值本來就能存任意字串——
        /// 例如「本世血統」就把 BloodlineTable 的 Id 存在旗標值裡（見 BloodlineSystem），
        /// 這樣輪迴時 ReincarnateInPlace 換掉整個 progress，血統自動回到未定型狀態。
        /// </summary>
        public string GetFlagValue(string key)
            => (_current != null && !string.IsNullOrEmpty(key)
                && _current.progress.flags != null
                && _current.progress.flags.TryGetValue(key, out var v)) ? v : "";

        // ───────────── 抽選池的永久解鎖清單（跨輪迴保存）─────────────
        //
        // 「打贏紅嫁衣 → 血統池 +幽靈」這類解鎖存在這裡，不改 CSV。
        // 實際抽選池 = 基本表 ∪ 這份清單（見 Dipan.Gacha.GachaService.BuildCandidates）。
        // 存在 CharacterSave 頂層 = ReincarnateInPlace 不會清掉，所以解鎖是永久的。

        /// <summary>把某個物品解鎖進某個抽選池。idempotent。回傳 true 代表這是第一次解鎖。</summary>
        public bool UnlockRollEntry(string poolId, int itemId)
        {
            if (_current == null || string.IsNullOrEmpty(poolId) || itemId <= 0) return false;
            if (_current.unlockedRollEntries == null)
                _current.unlockedRollEntries = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>();
            if (!_current.unlockedRollEntries.TryGetValue(poolId, out var list) || list == null)
            {
                list = new System.Collections.Generic.List<int>();
                _current.unlockedRollEntries[poolId] = list;
            }
            if (list.Contains(itemId)) return false;
            list.Add(itemId);
            MarkDirty();
            Debug.Log($"[SaveManager] 抽選池「{poolId}」解鎖物品 {itemId}");
            return true;
        }

        /// <summary>某個抽選池目前已解鎖的物品清單（永遠不回 null）。</summary>
        public System.Collections.Generic.IReadOnlyList<int> GetUnlockedRollEntries(string poolId)
        {
            if (_current != null && !string.IsNullOrEmpty(poolId)
                && _current.unlockedRollEntries != null
                && _current.unlockedRollEntries.TryGetValue(poolId, out var list) && list != null)
                return list;
            return System.Array.Empty<int>();
        }

        /// <summary>某個物品是否已解鎖進某個抽選池。</summary>
        public bool IsRollEntryUnlocked(string poolId, int itemId)
        {
            if (itemId <= 0) return false;
            var list = GetUnlockedRollEntries(poolId);
            for (int i = 0; i < list.Count; i++) if (list[i] == itemId) return true;
            return false;
        }

        /// <summary>終身旗標是否成立（lifetimeFlags[key] == "1"）。跨輪迴保存，只有開新角色才空。觸發鏈以「永久:」前綴路由到這裡。</summary>
        public bool GetLifetimeFlag(string key)
            => _current != null && !string.IsNullOrEmpty(key)
               && _current.lifetimeFlags != null
               && _current.lifetimeFlags.TryGetValue(key, out var v) && v == "1";

        /// <summary>寫終身旗標（預設 "1"）。跨輪迴保存（ReincarnateInPlace 不動 lifetimeFlags）。</summary>
        public void SetLifetimeFlag(string key, string value = "1")
        {
            if (_current == null || string.IsNullOrEmpty(key)) return;
            if (_current.lifetimeFlags == null) _current.lifetimeFlags = new System.Collections.Generic.Dictionary<string, string>();
            if (_current.lifetimeFlags.TryGetValue(key, out var old) && old == value) return;
            _current.lifetimeFlags[key] = value;
            MarkDirty();
            Debug.Log($"[SaveManager] 終身旗標 {key} = {value}");
        }

        /// <summary>金錢數字變動時觸發（背包面板的金額顯示靠它即時更新）。</summary>
        public event System.Action OnCurrencyChanged;

        /// <summary>加錢（可為負，但不會低於 0；扣錢建議用 TrySpendCurrency）。</summary>
        public void AddCurrency(int amount)
        {
            if (_current == null || amount == 0) return;
            _current.stats.currency = Mathf.Max(0, _current.stats.currency + amount);
            MarkDirty();
            OnCurrencyChanged?.Invoke();
        }

        /// <summary>嘗試花費金錢；不足回 false、不扣。</summary>
        public bool TrySpendCurrency(int amount)
        {
            if (_current == null || amount < 0) return false;
            if (_current.stats.currency < amount) return false;
            _current.stats.currency -= amount;
            MarkDirty();
            OnCurrencyChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 把背包（與倉庫）裡的銅錢道具收進金錢數字。
        ///
        /// <para>金錢原本是「可堆疊的背包道具 101」，後來改成獨立的數字顯示在背包下方，
        /// 不再佔背包格。這支負責兩件事：①把舊存檔裡已經堆在背包裡的銅錢換算成數字（一次性遷移）；
        /// ②當作安全網——萬一哪個路徑漏了攔截、讓銅錢又跑進背包，下次載入會自動收乾淨。</para>
        /// </summary>
        void SweepMoneyIntoWallet()
        {
            if (_current == null) return;
            int moneyId = RunProgress.MoneyItemId;
            int total = 0;

            if (Inv != null)
            {
                int n = Inv.CountOf(moneyId);
                if (n > 0) { Inv.RemoveItem(moneyId, n); total += n; }
            }
            if (Storage != null)
            {
                for (int page = 0; page < Storage.Pages; page++)
                {
                    var g = Storage.Page(page);
                    if (g == null) continue;
                    for (int i = 0; i < g.Capacity; i++)
                    {
                        var st = g.GetAt(i);
                        if (st.ItemId == moneyId && st.Count > 0)
                        {
                            total += st.Count;
                            g.SetAt(i, ItemStack.Empty);
                        }
                    }
                }
            }

            if (total <= 0) return;
            _current.stats.currency = Mathf.Max(0, _current.stats.currency + total);
            MarkDirty();
            OnCurrencyChanged?.Invoke();
            Debug.Log($"[SaveManager] 已把背包/倉庫裡的銅錢 {total} 收進金錢（金錢改成獨立數字，不再佔背包格）。");
        }

        /// <summary>第 cycle 周目輪迴時可帶入的物品數 = min(cycle, 上限)。純函式，供轉生流程（Phase B）用。</summary>
        public static int CarryCountForCycle(int cycle)
            => Mathf.Clamp(cycle, 0, SaveConstants.MaxCarryOnReincarnate);

        // ───────────── 存檔 ─────────────

        /// <summary>把當前各系統狀態收集起來，原子寫回活躍角色的檔。</summary>
        public void SaveNow()
        {
            if (_current == null) return;
            CaptureFromSystems();

            var now = DateTime.UtcNow;
            _current.playTimeSeconds += (long)Math.Max(0, (now - _sessionStartUtc).TotalSeconds);
            _sessionStartUtc = now;
            _current.lastPlayedUtc = NowUtc();

            SaveSystem.SaveCharacter(_current);

            var p = _roster.Find(_current.characterId);
            if (p != null)
            {
                p.name = _current.name;
                p.slotIndex = _current.slotIndex;
                p.generation = _current.generation;
                p.clearedModuleCount = _current.progress.clearedModules.Count;
                p.lastPlayedUtc = _current.lastPlayedUtc;
            }
            SaveSystem.SaveRoster(_roster);

            _dirty = false;
            Debug.Log($"[SaveManager] 已存檔：{_current.name}");
        }

        void ReloadActiveFromDisk()
        {
            if (_current == null) return;
            if (SaveSystem.LoadCharacter(_current.characterId, out var save, out _))
            {
                _current = save;
                ApplyToSystems(save);
                Debug.Log($"[SaveManager] 已從磁碟重載：{save.name}");
            }
        }

        // ───────────── 系統 ↔ 存檔（capture / restore）─────────────

        void CaptureFromSystems()
        {
            _current.inventory = Inv.CaptureState();
            _current.storages = Storage.CaptureState();   // 倉庫 5 分頁 → 一頁一筆 StorageDTO
            // 註：HP/MP 刻意不存檔——每次進遊戲都滿血滿魔（方便測試）。見 readme/COMBAT.md §7。
            // 未來：_current.progress / mapStates …
        }

        void ApplyToSystems(CharacterSave save)
        {
            Inv.RestoreState(save != null ? save.inventory : null);
            Storage.RestoreState(save != null ? save.storages : null);   // 沒有就各頁還原成空
            SweepMoneyIntoWallet();   // 舊存檔裡堆在背包的銅錢 → 換算成金錢數字（見該函式說明）
            // HP/MP 不還原：由 PlayerController 在 Start 以 CombatStats.Init 設成滿血滿魔。
        }

        static CharacterProfile ToProfile(CharacterSave s) => new CharacterProfile
        {
            characterId = s.characterId,
            name = s.name,
            slotIndex = s.slotIndex,
            generation = s.generation,
            clearedModuleCount = s.progress != null ? s.progress.clearedModules.Count : 0,
            createdAtUtc = s.createdAtUtc,
            lastPlayedUtc = s.lastPlayedUtc,
        };
    }
}
