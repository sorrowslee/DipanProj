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
            if (!SuppressAutoLoad)
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

        /// <summary>金錢（存錢抽關卡用）。</summary>
        public int Currency => _current != null ? _current.stats.currency : 0;

        /// <summary>是否已由開場鏈首次抵達邪佛廣場（決定出生點：洞穴出口 vs 中央）。</summary>
        public bool HubIntroSpawnDone
        {
            get => _current != null && _current.progress.hubIntroSpawnDone;
            set { if (_current != null && _current.progress.hubIntroSpawnDone != value) { _current.progress.hubIntroSpawnDone = value; MarkDirty(); } }
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

        /// <summary>加錢（可為負，但不會低於 0；扣錢建議用 TrySpendCurrency）。</summary>
        public void AddCurrency(int amount)
        {
            if (_current == null || amount == 0) return;
            _current.stats.currency = Mathf.Max(0, _current.stats.currency + amount);
            MarkDirty();
        }

        /// <summary>嘗試花費金錢；不足回 false、不扣。</summary>
        public bool TrySpendCurrency(int amount)
        {
            if (_current == null || amount < 0) return false;
            if (_current.stats.currency < amount) return false;
            _current.stats.currency -= amount;
            MarkDirty();
            return true;
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
