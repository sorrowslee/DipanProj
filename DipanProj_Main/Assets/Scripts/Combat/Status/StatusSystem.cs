using UnityEngine;
using Dipan.Localization;

/// <summary>
/// 狀態效果（buff／debuff／DoT）的**唯一施加入口**（2026-11-18，見 readme/STATUS_EFFECT.md）。
/// 武器命中、怪物攻擊、地面特效、道具、觸發鏈、作弊面板……之後所有來源都呼叫這裡。
/// </summary>
public static class StatusSystem
{
    /// <summary>DoT／HoT 結算間隔（秒）。全遊戲共用一個時鐘，同一跳多種 DoT 才能合成一個數字。</summary>
    public const float TickInterval = 0.5f;

    /// <summary>控制（暈眩／冰凍）解除後的免疫秒數：這段時間內再來的控制不上身，防止高射速武器把雜兵永遠鎖死（第⑤階段）。</summary>
    public const float ControlImmuneSeconds = 2f;
    // ── 跳數字預算（第⑥階段效能）──
    /// <summary>每一個 0.5 秒的跳傷時段內，全場最多跳幾個狀態 DoT／回春數字（玩家自己的不算額度、一定跳）。</summary>
    public const int TickNumberBudget = 24;
    static int _tickBudgetFrameWindow = -1;
    static int _tickNumbersUsed;

    /// <summary>
    /// 這一跳要不要跳數字：玩家一定跳；怪物要**在畫面上**（看不到的不必建物件）而且全場這個時段還有預算。
    /// 傷害照算，只是不跳字——100 隻怪一起燒時，畫面上本來也看不清 200 個數字。
    /// </summary>
    public static bool TryShowTickNumber(StatusController sc)
    {
        if (sc == null) return false;
        if (sc.IsPlayer) return true;
        if (!sc.IsOnScreen) return false;
        int window = Mathf.FloorToInt(Time.time / TickInterval);
        if (window != _tickBudgetFrameWindow) { _tickBudgetFrameWindow = window; _tickNumbersUsed = 0; }
        if (_tickNumbersUsed >= TickNumberBudget) return false;
        _tickNumbersUsed++;
        return true;
    }

    /// <summary>「被控制時不能喝藥」提示（LanguageTable）。</summary>
    public const int TxtCantDrinkControlled = 12043;

    /// <summary>「免疫」字的語言表 id。</summary>
    public const int TxtImmune = 12041;
    /// <summary>狀態列 tip 的「剩餘 {0} 秒」。</summary>
    public const int TxtRemaining = 12042;
    static readonly Color ImmuneColor = new Color(0.85f, 0.85f, 0.85f, 1f);

    /// <summary>
    /// 對 <paramref name="target"/> 施加狀態。
    /// <para><paramref name="hitDamage"/>：DoT 的「百分比型」基準＝這一下命中的原始傷害（武器傷害，未過加成／減傷——每跳結算時才過）。</para>
    /// <para><paramref name="flatDot"/>：DoT 用固定值型（表上 FlatLv1／FlatPerLv）。<paramref name="hitDamage"/> ≤ 0 時也一律用固定值
    /// （怪物、地面、劇本、作弊面板施加的都是這樣）。武器上要用哪一種由武器設定（第④階段的配方欄）。</para>
    /// </summary>
    public static StatusController.ApplyResult Apply(GameObject target, int statusId, int level,
        GameObject source = null, float hitDamage = 0f, bool flatDot = false)
        => Apply(target, StatusEffectDatabase.Get(statusId), level, source, hitDamage, flatDot);

    public static StatusController.ApplyResult Apply(GameObject target, StatusEffectDef def, int level,
        GameObject source = null, float hitDamage = 0f, bool flatDot = false, bool forceChance = false)
    {
        if (target == null || def == null || level <= 0) return StatusController.ApplyResult.Invalid;
        var sc = ControllerOf(target, true);
        if (sc == null) return StatusController.ApplyResult.Invalid;

        float dps = 0f;
        if (def.Category == StatusCategory.Dot)
            dps = (!flatDot && hitDamage > 0f) ? hitDamage * def.ValueAt(level) : def.FlatAt(level);

        var r = sc.Apply(def, level, source, dps, forceChance);
        if (r == StatusController.ApplyResult.Immune && sc.ConsumeImmuneTextSlot())
            DamageNumberManager.ShowText(target, Language.GetText(TxtImmune), ImmuneColor);
        return r;
    }

    /// <summary>
    /// 取得（必要時掛上）狀態控制器。只有玩家與怪物會掛——可破壞物、牆不會（<paramref name="create"/> 對它們回 null）。
    /// 怪物在 MonsterController.Start、玩家在 PlayerController.Start 就已經掛好。
    /// </summary>
    public static StatusController ControllerOf(GameObject go, bool create)
    {
        if (go == null) return null;
        var sc = go.GetComponent<StatusController>();
        if (sc != null || !create) return sc;
        if (go.GetComponent<MonsterController>() == null && go.GetComponent<PlayerController>() == null) return null;
        return go.AddComponent<StatusController>();
    }

    /// <summary>清掉玩家身上所有狀態（RunProgress 開始／結束一趟關卡時呼叫：換圖保留、離開關卡全清，作者 2026-11-18 定）。</summary>
    public static void ClearPlayer()
    {
        var pc = Object.FindObjectOfType<PlayerController>();
        if (pc == null) return;
        var sc = pc.GetComponent<StatusController>();
        if (sc != null) sc.ClearAll();
    }
}
