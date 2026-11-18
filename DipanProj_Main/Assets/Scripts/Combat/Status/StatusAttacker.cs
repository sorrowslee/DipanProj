using System.Collections.Generic;
using UnityEngine;
using Dipan.Inventory;

/// <summary>
/// **狀態效果的觸發器**（2026-11-18，readme/STATUS_EFFECT.md §4）：掛在「會因為自己的動作給出狀態」的單位上——
/// 玩家一定有；怪物只有在武器配方有 <c>StatusLv</c> 或 MonsterData 有 <c>ContactStatus</c> 時才掛（省效能）。
///
/// <para><b>兩種來源</b>：
///   ‧ <b>效果</b>（GemTable 的狀態效果列，帶觸發時機）：玩家＝武器配方起點＋裝備內建效果＋珠子＋血統內建效果，全身等級相加；
///     怪物＝武器配方 <c>StatusLv</c>。由 <see cref="SetEffects"/> 一次給「GemID → 總等級」。
///   ‧ <b>直接狀態</b>（怪物 <c>ContactStatus</c>）：命中必給目標，DoT 一律固定值（<see cref="SetDirectStatuses"/>）。</para>
///
/// <para><b>四個觸發點</b>（呼叫端）：
///   ‧ <see cref="NotifyHit"/>：<c>CombatSystem.Apply</c>（傷害確實打進去、不是狀態 DoT 一跳、沒被閃掉）。
///   ‧ <see cref="NotifyKill"/>：<c>CombatSystem.Apply</c>（這一下讓怪物從活變死；狀態 DoT 打死的也算，施加者記在狀態上）。
///   ‧ <see cref="NotifyFire"/>：玩家 <c>Shoot</c> 真的射出去／持續型武器每次扣魔；怪物 <c>MonsterWeaponUser.TryUse</c> 成功。
///   ‧ <see cref="NotifyHurt"/>：玩家 <c>TakeDamage</c> 確實扣到血（不含狀態 DoT 一跳）。</para>
///
/// <para><b>給誰</b>：狀態表 <c>Kind=Buff</c> 給自己；<c>Debuff</c> 給「對方」（命中＝被打的、受擊＝打我的；發射／擊殺沒有對方 ⇒ 略過）。
/// <b>機率／冷卻</b>：GemTable 的 <c>Chance</c>／<c>Cooldown</c>，每一種效果各自計時；冷卻只在真的上身（或刷新）後才開始。</para>
/// </summary>
[DisallowMultipleComponent]
public class StatusAttacker : MonoBehaviour
{
    struct Eff
    {
        public GemData Gem;
        public StatusEffectDef Def;
        public int Level;
    }

    readonly List<Eff> _onHit = new List<Eff>();
    readonly List<Eff> _onFire = new List<Eff>();
    readonly List<Eff> _onKill = new List<Eff>();
    readonly List<Eff> _onHurt = new List<Eff>();
    readonly List<StatusSpec.Entry> _direct = new List<StatusSpec.Entry>();
    readonly Dictionary<int, float> _readyAt = new Dictionary<int, float>();

    /// <summary>DoT 算法：true＝狀態表固定值；false＝命中那一下的傷害×狀態表百分比（武器配方 <c>StatusDmgCalc</c>）。</summary>
    public bool DmgFlat;

    /// <summary>有沒有任何東西要在命中時處理（CombatSystem 用來少做一點事）。</summary>
    public bool HasOnHit => _onHit.Count > 0 || _direct.Count > 0;
    public bool HasOnKill => _onKill.Count > 0;

    public static StatusAttacker Ensure(GameObject go)
    {
        if (go == null) return null;
        var a = go.GetComponent<StatusAttacker>();
        return a != null ? a : go.AddComponent<StatusAttacker>();
    }

    /// <summary>
    /// 換一組效果：<paramref name="levels"/>＝GemID → 全身總等級（非狀態效果的 GemID 自動略過）。
    /// 總等級超過該效果 MaxLv 照 MaxLv 算，再用 GemTable 的 Lv1／PerLv 換成「給出去的狀態等級」。
    /// </summary>
    public void SetEffects(IEnumerable<KeyValuePair<int, int>> levels)
    {
        _onHit.Clear(); _onFire.Clear(); _onKill.Clear(); _onHurt.Clear();
        if (levels == null) return;
        var gems = ItemManager.Gems;
        if (gems == null) return;
        foreach (var kv in levels)
        {
            if (kv.Value <= 0) continue;
            var g = gems.Get(kv.Key);
            if (g == null || !g.IsStatusEffect) continue;
            var def = StatusEffectDatabase.Get(g.StatusId);
            if (def == null)
            {
                Debug.LogWarning($"[StatusAttacker] 效果 {g.GemID}「{g.Name}」的 StatusID {g.StatusId} 在 StatusEffectTable 找不到。");
                continue;
            }
            int lv = Mathf.Clamp(kv.Value, 1, Mathf.Max(1, g.MaxLv));
            int statusLv = Mathf.Max(1, Mathf.RoundToInt(g.Lv1 + (lv - 1) * g.PerLv));
            var e = new Eff { Gem = g, Def = def, Level = statusLv };
            switch (g.Trigger)
            {
                case StatusTrigger.OnFire: _onFire.Add(e); break;
                case StatusTrigger.OnKill: _onKill.Add(e); break;
                case StatusTrigger.OnHurt: _onHurt.Add(e); break;
                default: _onHit.Add(e); break;
            }
        }
    }

    /// <summary>直接狀態（怪物 ContactStatus）：命中必給目標。</summary>
    public void SetDirectStatuses(List<StatusSpec.Entry> list)
    {
        _direct.Clear();
        if (list != null) _direct.AddRange(list);
    }

    /// <summary>目前生效的效果（作弊面板／除錯用）：(名稱, 時機, 狀態等級)。</summary>
    public void GetSummary(List<string> into)
    {
        into.Clear();
        void Add(List<Eff> l) { foreach (var e in l) into.Add($"{e.Gem.Name}（{e.Def.Name} Lv{e.Level}）"); }
        Add(_onHit); Add(_onFire); Add(_onKill); Add(_onHurt);
        foreach (var d in _direct)
        {
            var def = StatusEffectDatabase.Get(d.Id);
            if (def != null) into.Add($"命中給 {def.Name} Lv{d.Level}");
        }
    }

    // ───────────────────────── 觸發點 ─────────────────────────

    public void NotifyHit(GameObject target, float hitDamage)
    {
        if (target == null) return;
        if (_onHit.Count > 0) Run(_onHit, target, hitDamage);
        for (int i = 0; i < _direct.Count; i++)
        {
            var def = StatusEffectDatabase.Get(_direct[i].Id);
            if (def == null) continue;
            var dst = def.Kind == StatusKind.Buff ? gameObject : target;
            StatusSystem.Apply(dst, def, _direct[i].Level, gameObject, 0f, flatDot: true);
        }
    }

    public void NotifyFire() { if (_onFire.Count > 0) Run(_onFire, null, 0f); }
    public void NotifyKill(GameObject victim) { if (_onKill.Count > 0) Run(_onKill, null, 0f); }
    public void NotifyHurt(GameObject attacker) { if (_onHurt.Count > 0) Run(_onHurt, attacker, 0f); }

    void Run(List<Eff> list, GameObject other, float hitDamage)
    {
        float now = Time.time;
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            int id = e.Gem.GemID;
            if (e.Gem.Cooldown > 0f && _readyAt.TryGetValue(id, out float t) && now < t) continue;
            if (e.Gem.Chance < 0.999f && Random.value >= e.Gem.Chance) continue;

            bool self = e.Def.Kind == StatusKind.Buff;
            GameObject dst = self ? gameObject : other;
            if (dst == null) continue;   // 發射／擊殺沒有「對方」：減益效果放在這兩個時機不會有作用
            var r = StatusSystem.Apply(dst, e.Def, e.Level, gameObject, self ? 0f : hitDamage, DmgFlat || hitDamage <= 0f);
            if (e.Gem.Cooldown > 0f && (r == StatusController.ApplyResult.Applied || r == StatusController.ApplyResult.Refreshed))
                _readyAt[id] = now + e.Gem.Cooldown;
        }
    }

    // ───────────────────────── 攻擊者歸屬 ─────────────────────────

    /// <summary>
    /// 傷害來源 → 真正的攻擊者：怪物揮擊／踐踏／咬擊／自爆是另生的 <see cref="ImpactDamageArea"/>，它的 <c>EnemyContactDamage.Owner</c> 才是那隻怪。
    /// 其他情況來源就是攻擊者本人（玩家子彈、怪物武器、狀態 DoT 都傳單位本身）。
    /// </summary>
    public static GameObject ResolveAttacker(GameObject source)
    {
        if (source == null) return null;
        if (source.TryGetComponent<EnemyContactDamage>(out var ecd) && ecd.Owner != null) return ecd.Owner;
        return source;
    }
}
