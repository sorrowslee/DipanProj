using System;
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
            LoadActiveOrCreateDefault();                       // 先載入（此時尚未訂閱，不會被自己的 Raise 標 dirty）
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

        /// <summary>建立新角色並設為活躍（背包清空）。回傳該存檔。</summary>
        public CharacterSave CreateCharacter(string name)
        {
            var save = new CharacterSave
            {
                characterId = Guid.NewGuid().ToString("N"),
                name = string.IsNullOrWhiteSpace(name) ? SaveConstants.DefaultTestCharacterName : name,
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
            if (p != null) { p.name = _current.name; p.generation = _current.generation; p.lastPlayedUtc = _current.lastPlayedUtc; }
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
            generation = s.generation,
            createdAtUtc = s.createdAtUtc,
            lastPlayedUtc = s.lastPlayedUtc,
        };
    }
}
