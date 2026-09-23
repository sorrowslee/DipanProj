using System;
using UnityEngine;

/// <summary>
/// 一個戰鬥單位的「數值層」：HP（血量）＋ MP（魔力）＋ 回復 ＋ 傷害加成／減傷掛勾。
/// 純資料元件，**不認識 UI、不認識存檔**——變動時發事件讓血/魔條訂閱重繪（同 InventorySystem 的設計）。
///
/// 目前掛在玩家身上（HP+MP）。怪物維持原本輕量的 HP（MonsterController 自管），只實作
/// <see cref="ICombatModifiers"/> 提供減傷掛勾。加成／減傷預設 0（掛勾就位、之後接 CSV／buff）。
/// 見 readme/COMBAT.md。
/// </summary>
public class CombatStats : MonoBehaviour, ICombatModifiers
{
    // ── 不死保護（2026-09-23，新手夢境教學）──
    // 開著時，受傷最多扣到 1 滴血，**永遠不會觸發 OnDeath**（＝不進死亡流程）。
    // 理由：夢境是劇情演出，玩家在夢裡死掉會走一般的死亡／結算流程，整段夢境會壞掉（作者 2026-09-23 拍板）。
    // 具名持有者（同 UIManager.SetExternalHold 的理由）：兩個系統同時開，先關的那一個不會把另一個的保護一起拿掉。
    // static：CombatStats 目前只掛在玩家身上（怪物的血量是 MonsterController 自己管），不需要分實例。
    static readonly System.Collections.Generic.HashSet<string> _deathGuards = new System.Collections.Generic.HashSet<string>();

    /// <summary>目前是否有人開著不死保護。</summary>
    public static bool DeathGuarded => _deathGuards.Count > 0;

    /// <summary>開／關不死保護（具名：只影響自己這一份）。</summary>
    public static void SetDeathGuard(string owner, bool on)
    {
        if (string.IsNullOrEmpty(owner)) owner = "__default__";
        if (on) _deathGuards.Add(owner); else _deathGuards.Remove(owner);
    }

    /// <summary>進 Play 時清掉（Domain Reload 已關）。</summary>
    public static void ResetForPlayMode() => _deathGuards.Clear();

    // ── 血量 ──
    public float MaxHealth { get; private set; } = 100f;
    public float Health { get; private set; } = 100f;
    public float HealthRegenPerSec { get; private set; } = 0f;

    // ── 魔力 ──
    public float MaxMana { get; private set; } = 50f;
    public float Mana { get; private set; } = 50f;
    public float ManaRegenPerSec { get; private set; } = 0f;

    // ── 傷害修正掛勾（預設無效果；之後由裝備／buff／CSV 餵入）──
    /// <summary>攻擊加成（百分比）：20 = 打出去 +20% 傷害。</summary>
    public float OutgoingDamageBonusPercent = 0f;
    /// <summary>減傷（百分比）：20 = 受到的傷害 -20%。夾在 0~90%。</summary>
    public float IncomingDamageReductionPercent = 0f;

    public bool IsDead { get; private set; }

    /// <summary>血量變動（含受傷／回血／初始化）。參數：當前、上限。</summary>
    public event Action<float, float> OnHealthChanged;
    /// <summary>魔力變動（含消耗／回魔／初始化）。參數：當前、上限。</summary>
    public event Action<float, float> OnManaChanged;
    /// <summary>血量歸零（只觸發一次）。</summary>
    public event Action OnDeath;

    /// <summary>初始化（滿血滿魔）。由擁有者（PlayerController）在 Start 呼叫。</summary>
    public void Init(float maxHealth, float maxMana, float healthRegenPerSec = 0f, float manaRegenPerSec = 0f)
    {
        MaxHealth = Mathf.Max(1f, maxHealth);
        MaxMana = Mathf.Max(0f, maxMana);
        HealthRegenPerSec = Mathf.Max(0f, healthRegenPerSec);
        ManaRegenPerSec = Mathf.Max(0f, manaRegenPerSec);
        Health = MaxHealth;
        Mana = MaxMana;
        IsDead = false;
        RaiseHealth();
        RaiseMana();
    }

    /// <summary>還原存檔狀態（上限 + 當前值）。找不到存檔時不要呼叫，改用 Init。</summary>
    public void Restore(float maxHealth, float health, float maxMana, float mana)
    {
        MaxHealth = Mathf.Max(1f, maxHealth);
        MaxMana = Mathf.Max(0f, maxMana);
        Health = Mathf.Clamp(health, 0f, MaxHealth);
        Mana = Mathf.Clamp(mana, 0f, MaxMana);
        IsDead = Health <= 0f;
        RaiseHealth();
        RaiseMana();
    }

    void Update()
    {
        if (IsDead) return;

        if (HealthRegenPerSec > 0f && Health < MaxHealth)
        {
            Health = Mathf.Min(MaxHealth, Health + HealthRegenPerSec * Time.deltaTime);
            RaiseHealth();
        }
        if (ManaRegenPerSec > 0f && Mana < MaxMana)
        {
            Mana = Mathf.Min(MaxMana, Mana + ManaRegenPerSec * Time.deltaTime);
            RaiseMana();
        }
    }

    // ── 血量 ──

    /// <summary>套用血量變化（負 = 受傷、正 = 回血）。回傳實際扣的血。死亡時觸發 OnDeath 一次。</summary>
    public float ApplyHealthDelta(float delta)
    {
        if (IsDead) return 0f;
        float before = Health;
        Health = Mathf.Clamp(Health + delta, 0f, MaxHealth);
        // 不死保護：受傷最多扣到 1（本來就不到 1 的就停在原值），所以下面的死亡判定永遠不成立。
        if (DeathGuarded && delta < 0f) Health = Mathf.Max(Health, Mathf.Min(before, 1f));
        RaiseHealth();

        if (Health <= 0f && before > 0f)
        {
            IsDead = true;
            OnDeath?.Invoke();
        }
        return before - Health;
    }

    public void Heal(float amount)
    {
        if (amount > 0f) ApplyHealthDelta(amount);
    }

    // ── 魔力 ──

    /// <summary>夠魔就扣掉並回傳 true；不夠回傳 false（不扣）。cost &lt;= 0 一律成功。</summary>
    public bool TrySpendMana(float cost)
    {
        if (cost <= 0f) return true;
        if (Mana < cost) return false;
        Mana -= cost;
        RaiseMana();
        return true;
    }

    public void RestoreMana(float amount)
    {
        if (amount <= 0f) return;
        Mana = Mathf.Min(MaxMana, Mana + amount);
        RaiseMana();
    }

    // ── ICombatModifiers：把上面的百分比掛勾換算成乘數 ──

    public float OutgoingDamageMultiplier(in DamageInfo info)
    {
        return 1f + OutgoingDamageBonusPercent / 100f;
    }

    public float IncomingDamageMultiplier(in DamageInfo info)
    {
        float reduction = Mathf.Clamp(IncomingDamageReductionPercent, 0f, 90f);
        return 1f - reduction / 100f;
    }

    private void RaiseHealth() => OnHealthChanged?.Invoke(Health, MaxHealth);
    private void RaiseMana() => OnManaChanged?.Invoke(Mana, MaxMana);
}
