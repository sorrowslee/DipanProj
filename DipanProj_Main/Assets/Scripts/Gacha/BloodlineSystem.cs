using UnityEngine;
using Dipan.Inventory;
using Dipan.Save;

namespace Dipan.Gacha
{
    /// <summary>
    /// 血統的執行期系統：把「本世血統」套到玩家身上（外型 ＋ 數值），並處理喝血統藥劑。
    ///
    /// 規則（拍板的設計，見 readme）：
    ///   1. 血統藥劑是**一次性消耗道具**：喝下去永久改變本世外型與數值。
    ///   2. **本世只能喝一次**。喝過之後再拿到任何血統藥劑都不能再喝（會提示「你的血脈已定」）。
    ///   3. **輪迴後回到人類外型**。但血統藥劑本身是道具，可以被輪迴帶物選中留到下一世再喝——
    ///      所以「已定型」旗標刻意存在 progress.flags（周目層），ReincarnateInPlace 換掉整個 progress
    ///      時自動失效，不需要任何額外的重置程式碼。
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
        float _baseMoveSpeed;
        float _baseMaxHealth;
        float _appliedDamageBonus;   // 目前這個血統貢獻了多少傷害加成（換血統時用差額還原，不蓋掉別人寫的）
        bool _baseCaptured;

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            // 玩家還沒生出來（載入中、標題畫面）→ 等。
            if (_pc == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go == null) return;
                _pc = go.GetComponent<PlayerController>();
                if (_pc == null) return;
                _appliedId = -1;              // 新的玩家物件 → 重新套一次
                _baseCaptured = false;
                _appliedDamageBonus = 0f;     // 新玩家的加成從 0 起算
            }

            int want = CurrentBloodlineId;
            if (want == _appliedId) return;
            ApplyTo(_pc, want);
        }

        // ───────────────────────── 對外查詢 ─────────────────────────

        /// <summary>本世血統 Id（沒喝過任何藥劑 = 人類）。存在周目旗標，輪迴自動失效。</summary>
        public static int CurrentBloodlineId
        {
            get
            {
                var sm = SaveManager.Instance;
                string v = sm != null ? sm.GetFlagValue(GachaConstants.BloodlineFlagKey) : null;
                if (!string.IsNullOrEmpty(v) && int.TryParse(v, out int id) && id > 0) return id;
                return BloodlineTable.HumanId;
            }
        }

        /// <summary>本世是否已經定型（喝過血統藥劑了）。定型後任何血統藥劑都不能再喝。</summary>
        public static bool IsFixedThisCycle
        {
            get
            {
                var sm = SaveManager.Instance;
                if (sm == null) return false;
                return !string.IsNullOrEmpty(sm.GetFlagValue(GachaConstants.BloodlineFlagKey));
            }
        }

        /// <summary>本世血統的名字（給 UI 顯示）。</summary>
        public static string CurrentDisplayName
        {
            get
            {
                var d = BloodlineTable.Get(CurrentBloodlineId);
                return d != null ? d.DisplayName : "人類";
            }
        }

        // ───────────────────────── 喝藥 ─────────────────────────

        /// <summary>
        /// 喝下一瓶血統藥劑（背包裡的某個道具 id）。成功回 true。
        /// 失敗原因放 <paramref name="reason"/>（呼叫端拿去 Toast）。
        /// </summary>
        public static bool TryDrink(int itemId, out string reason)
        {
            reason = null;
            var inv = InventorySystem.Instance;
            var data = inv != null ? inv.GetData(itemId) : null;
            if (data == null || !data.IsBloodline)
            {
                reason = "這不是血統藥劑";
                return false;
            }
            var def = BloodlineTable.Get(data.BloodlineID);
            if (def == null)
            {
                reason = "這瓶藥劑的血統在血統表找不到";
                Debug.LogWarning($"[BloodlineSystem] 道具 {itemId}「{data.Name}」的 BloodlineID={data.BloodlineID} 在 BloodlineTable.csv 找不到。");
                return false;
            }
            if (IsFixedThisCycle)
            {
                reason = $"你的血脈已定為「{CurrentDisplayName}」，這一世不能再改變";
                return false;
            }
            if (inv.CountOf(itemId) <= 0)
            {
                reason = "背包裡沒有這瓶藥劑";
                return false;
            }

            // ⚠ 一定要先確認「寫得進存檔」再扣藥劑。
            // SaveManager.SetFlag 在沒有 active character 時是直接 return 的（不是丟例外），
            // 所以「有 SaveManager 但還沒載入角色」時若先扣再寫，會變成：藥劑消失 → 效果套上去 →
            // 下一幀 Update 讀回存檔發現還是人類 → 立刻還原。玩家看到「喝了、閃一下、變回去、藥沒了」。
            var sm = SaveManager.Instance;
            if (sm == null || !sm.HasActiveCharacter)
            {
                reason = "還沒載入角色，現在不能喝";
                Debug.LogWarning("[BloodlineSystem] 沒有 SaveManager 或尚未載入角色，血統無法保存，拒絕飲用（避免藥劑白白消失）。");
                return false;
            }

            sm.SetFlag(GachaConstants.BloodlineFlagKey, def.Id.ToString());
            inv.RemoveItem(itemId, 1);

            // 立刻套用（不等 Update 那一幀，讓喝下去的回饋是即時的）。
            if (_instance != null && _instance._pc != null) _instance.ApplyTo(_instance._pc, def.Id);

            Debug.Log($"[BloodlineSystem] 血統定型：{def.DisplayName}（id {def.Id}）");
            return true;
        }

        // ───────────────────────── 套用 ─────────────────────────

        void ApplyTo(PlayerController pc, int bloodlineId)
        {
            if (pc == null) return;
            var stats = pc.GetComponent<CombatStats>();

            // ⚠ CombatStats 還沒就緒就先 return，等下一幀再來。
            // 不能在這裡「stats 為 null 就把 _baseMaxHealth 記成 0」——那會永久鎖成 0，
            // 之後每次套用都把玩家最大生命設成 max(1, 0 + MaxHpAdd)，人類就是 1 點血，
            // 一碰就死而且完全沒有錯誤訊息。
            if (stats == null) return;

            // 第一次看到這個玩家物件時記下「原始值」，之後所有血統效果都從原始值算起，
            // 避免反覆套用時倍率越乘越大。
            if (!_baseCaptured)
            {
                _baseMoveSpeed = pc.MoveSpeed;
                _baseMaxHealth = stats.MaxHealth;
                _baseCaptured = true;
            }

            var def = BloodlineTable.Get(bloodlineId);
            if (def == null)
            {
                // 血統表載不到（provider 沒接好）→ 維持現狀，但不要卡在每幀重試。
                Debug.LogWarning($"[BloodlineSystem] 血統 id {bloodlineId} 在 BloodlineTable 找不到，維持原樣。");
                _appliedId = bloodlineId;
                return;
            }

            // 1) 數值：一律「原始值 + 血統修正」，所以換血統/重載存檔都不會疊加。
            pc.MoveSpeed = _baseMoveSpeed * def.MoveSpeedMul;

            float newMax = Mathf.Max(1f, _baseMaxHealth + def.MaxHpAdd);
            float newHp = Mathf.Clamp(stats.Health, 1f, newMax);
            stats.Restore(newMax, newHp, stats.MaxMana, stats.Mana);

            // 傷害加成用「加減差額」而不是直接賦值——這個欄位之後會有別的來源（裝備/buff/料理），
            // 直接賦值會在每次套用血統時把別人寫的加成整個蓋掉。
            stats.OutgoingDamageBonusPercent += def.OutgoingDamageBonusPercent - _appliedDamageBonus;
            _appliedDamageBonus = def.OutgoingDamageBonusPercent;

            // 2) 外型：SpriteFolder 留空就不動外型（避免填了不存在的資料夾害角色只剩影子）。
            //    SetBloodline 內部會用新的 MoveSpeed 重新 Setup 動畫（走路動畫速度跟移動速度連動），
            //    所以一定要先改完 MoveSpeed 再呼叫。
            if (!string.IsNullOrEmpty(def.SpriteFolder) && pc.Bloodline != def.SpriteFolder)
                pc.SetBloodline(def.SpriteFolder);

            // 3) 技能：預留欄位，技能系統還沒做。
            if (def.SkillId > 0)
                Debug.Log($"[BloodlineSystem] 血統「{def.DisplayName}」帶技能 {def.SkillId}，但技能系統尚未實作，先略過。");

            _appliedId = bloodlineId;
        }
    }
}
