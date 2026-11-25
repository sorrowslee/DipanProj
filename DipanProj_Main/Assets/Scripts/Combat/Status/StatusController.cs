using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一個戰鬥單位（玩家或怪物）身上的狀態效果（buff／debuff／DoT）。2026-11-18，見 readme/STATUS_EFFECT.md。
///
/// <para><b>省效能的寫法</b>（作者：之後會有大批怪物，省效能是最重要的）：
///   ‧ 身上沒有任何狀態時元件是 <c>enabled = false</c>，沒有 Update 成本——所以每隻怪都掛著也沒關係。
///   ‧ 乘數（造成傷害／受到傷害／移速）只在狀態增減時重算一次並快取，戰鬥中查詢是讀欄位。
///   ‧ DoT／HoT 共用一個 0.5 秒時鐘：同一跳裡多種 DoT 加總成**一個**傷害數字（不是每種各跳一個）。</para>
///
/// <para><b>不要直接呼叫 <see cref="Apply"/></b>：施加一律走 <see cref="StatusSystem.Apply(GameObject,int,int,GameObject,float,bool)"/>
/// （那裡負責算 DoT 每秒傷害、顯示「免疫」）。</para>
///
/// <para>⚠ 狀態 DoT 結算走 <c>CombatSystem.Apply</c>（<c>IsStatusTick=true</c>）：照吃減傷／易傷、
/// 但**不觸發無敵時間、不閃白、不擊退、不播三階血統擊中特效**、也不再吃施加者的攻擊加成（施加當下已算進每秒傷害）。</para>
/// </summary>
[DisallowMultipleComponent]
public class StatusController : MonoBehaviour
{
    public enum ApplyResult { Applied, Refreshed, Ignored, Immune, Invalid }

    // ── 夾值（避免把遊戲弄壞的極端值；平常碰不到）──
    const float MinIncomingMul = 0.1f;   // 受到傷害最少還有 10%（護體再多也不會無敵）
    const float MinMoveMul = 0.2f;       // 減速最多到 20% 速度
    const float MaxMoveMul = 3f;         // 加速最多 3 倍
    const float ImmuneTextGap = 1f;      // 「免疫」字最快每秒跳一次（機槍打頭目會洗版）

    struct Layer
    {
        public int Level;
        public float Remaining;
        public float Dps;   // 只有 DoT 用：施加當下算好的每秒傷害
    }

    sealed class Entry
    {
        public StatusEffectDef Def;
        public readonly List<Layer> Layers = new List<Layer>(1);
        public GameObject Source;
        public float ShownDuration;   // 給狀態列轉圈用的總長
        public bool Pinned;           // 常駐（作弊面板的開關）：不會到期，要明確 Remove

        public int MaxLevel { get { int m = 0; for (int i = 0; i < Layers.Count; i++) if (Layers[i].Level > m) m = Layers[i].Level; return m; } }
        public float MaxRemaining { get { float m = 0f; for (int i = 0; i < Layers.Count; i++) if (Layers[i].Remaining > m) m = Layers[i].Remaining; return m; } }
        public float TotalDps { get { float s = 0f; for (int i = 0; i < Layers.Count; i++) s += Layers[i].Dps; return s; } }
        public float TotalValue { get { float s = 0f; for (int i = 0; i < Layers.Count; i++) s += Def.ValueAt(Layers[i].Level); return s; } }
    }

    /// <summary>給狀態列／作弊面板看的一筆。</summary>
    public struct View
    {
        public StatusEffectDef Def;
        public int Level;
        public int Stacks;
        public float Remaining;
        public float Duration;
        /// <summary>Stat：百分比加總（0.2＝20%）；Dot：每秒傷害；Hot：每秒回血量；Control：0。</summary>
        public float Magnitude;
        /// <summary>常駐（作弊面板開關上的）：沒有剩餘時間，Remaining／Duration 不要拿來算。</summary>
        public bool Pinned;
    }

    readonly List<Entry> _entries = new List<Entry>(4);
    readonly List<(StatusEffectDef def, GameObject src, float amount)> _tickBuf = new List<(StatusEffectDef, GameObject, float)>(4);
    float _tickClock;
    float _outMul = 1f, _inMul = 1f, _moveMul = 1f;
    bool _controlled, _frozen, _bindOnly;
    float _lastImmuneText = -99f;

    MonsterController _mc;
    PlayerController _pc;
    CombatStats _stats;
    MonsterActuator _act;
    Renderer _rend;           // 在不在畫面上（跳數字預算用）
    StatusVisuals _visuals;   // 身上呈現（染色、火、泡、頭上圖示）；第一次有狀態時才掛
    bool _inited;

    /// <summary>這個單位的狀態有增減（UI 重畫用）。</summary>
    public event Action Changed;
    /// <summary>任何單位的狀態有增減（怪物頭上圖示那類集中管理器用）。</summary>
    public static event Action<StatusController> AnyChanged;
    public static void ResetForPlayMode() { AnyChanged = null; }

    public int Count => _entries.Count;
    public bool IsPlayer { get { Init(); return _pc != null; } }
    /// <summary>造成傷害的倍率（增傷、虛弱）。CombatSystem 在攻擊方讀。</summary>
    public float OutgoingMultiplier => _outMul;
    /// <summary>受到傷害的倍率（易傷、護體）。CombatSystem 在受擊方讀。</summary>
    public float IncomingMultiplier => _inMul;
    /// <summary>移動速度倍率（疾行、減速）。玩家在 FixedUpdate、怪物在 MonsterActuator 讀。</summary>
    public float MoveSpeedMultiplier => _moveMul;
    /// <summary>身上有控制效果（暈眩、冰凍）：鎖移動、鎖攻擊、玩家不能喝藥（第⑤階段，見 readme/STATUS_EFFECT.md §2.8）。</summary>
    public bool IsControlled => _controlled;
    /// <summary>控制中、而且是冰凍（動作定格；暈眩則是收招站著）。</summary>
    public bool IsFrozen => _frozen;
    /// <summary>控制中、而且只有束縛（沒有暈眩／冰凍）：鎖移動與攻擊、原地播走路（2026-11-25，古神黑洞）。</summary>
    public bool IsBoundWalk => _bindOnly;
    /// <summary>免疫行動控制（頭目：MonsterData 的 Controllable＝0）。</summary>
    public bool ImmuneToMovementImpair { get { Init(); return _mc != null && !_mc.Controllable; } }

    void Awake()
    {
        Init();
        enabled = false;   // 沒有狀態就不跑 Update
    }

    void Init()
    {
        if (_inited) return;
        _inited = true;
        _mc = GetComponent<MonsterController>();
        _pc = GetComponent<PlayerController>();
        _stats = GetComponent<CombatStats>();
        _act = GetComponent<MonsterActuator>();
    }

    /// <summary>身體在任何攝影機的畫面內（Renderer.isVisible）。找不到 Renderer 時當作在畫面上。</summary>
    public bool IsOnScreen
    {
        get
        {
            if (_rend == null) _rend = GetComponentInChildren<SpriteRenderer>();
            return _rend == null || _rend.isVisible;
        }
    }

    bool TargetDead => (_mc != null && _mc.IsDead) || (_pc != null && _pc.IsDead);

    /// <summary>施加一個狀態。⚠ 請走 StatusSystem.Apply（見類別說明）。<paramref name="dps"/> 只有 DoT 用。</summary>
    /// <param name="durationOverride">&gt; 0＝這一次改用這個秒數，不用表上的 Duration（黑洞的束縛＝黑洞還剩多久）；≤ 0＝照表。</param>
    public ApplyResult Apply(StatusEffectDef def, int level, GameObject source, float dps, bool forceChance = false, bool pinned = false,
                             float durationOverride = -1f)
    {
        if (def == null || level <= 0) return ApplyResult.Invalid;
        Init();
        if (TargetDead) return ApplyResult.Invalid;
        if (def.IsMovementImpairing && ImmuneToMovementImpair) return ApplyResult.Immune;

        if (def.Category == StatusCategory.Control)
        {
            // 2026-11-18 晚 作者：不要隱性規則（算難度會漏）⇒ 控制類**沒有任何特例**：
            //   沒有解除後免疫、控制中照樣可以再上；暈眩與冰凍可同時存在、各自計時（照表上的 Stack 規則＝Refresh）。
            //   玩家過強改由機率％上限控制（平衡時定）。
            // 等級＝觸發機率（作者 2026-11-18 定：有強度的效果等級加強度，沒有強度的加機率）。作弊面板可略過機率。
            if (!forceChance && UnityEngine.Random.value >= def.ValueAt(level)) return ApplyResult.Ignored;
        }

        float dur = pinned ? float.PositiveInfinity : (durationOverride > 0f ? durationOverride : def.Duration);   // 常駐＝無限時間（倒數扣不完）
        var e = Find(def.Id);
        ApplyResult result;
        if (e == null)
        {
            e = new Entry { Def = def, Source = source, Pinned = pinned };
            e.Layers.Add(new Layer { Level = level, Remaining = dur, Dps = dps });
            _entries.Add(e);
            result = ApplyResult.Applied;
        }
        else
        {
            result = ApplyResult.Refreshed;
            int sigBefore = Signature(e);
            switch (def.Stack)
            {
                case StatusStack.Stack:
                    if (e.Layers.Count >= def.MaxStack) e.Layers.RemoveAt(IndexOfSoonest(e));
                    e.Layers.Add(new Layer { Level = level, Remaining = dur, Dps = dps });
                    break;

                case StatusStack.Strongest:
                {
                    var cur = e.Layers[0];
                    float curStr = def.Category == StatusCategory.Dot ? cur.Dps : Mathf.Abs(def.ValueAt(cur.Level));
                    float newStr = def.Category == StatusCategory.Dot ? dps : Mathf.Abs(def.ValueAt(level));
                    if (newStr + 1e-4f < curStr && !forceChance) return ApplyResult.Ignored;   // 比較弱就不換（作弊面板照換）
                    e.Layers[0] = new Layer { Level = level, Remaining = dur, Dps = dps };
                    break;
                }

                case StatusStack.Extend:
                {
                    var cur = e.Layers[0];
                    cur.Level = Mathf.Max(cur.Level, level);
                    cur.Dps = Mathf.Max(cur.Dps, dps);
                    cur.Remaining = Mathf.Min(cur.Remaining + dur, dur * def.MaxStack);
                    e.Layers[0] = cur;
                    break;
                }

                default:   // Refresh
                {
                    var cur = e.Layers[0];
                    cur.Level = Mathf.Max(cur.Level, level);
                    cur.Dps = Mathf.Max(cur.Dps, dps);
                    cur.Remaining = Mathf.Max(cur.Remaining, dur);
                    e.Layers[0] = cur;
                    break;
                }
            }
            if (source != null) e.Source = source;
            // 常駐的狀態被一般命中刷新（例：燃燒「只留最強」整個換掉）時，還是要保持常駐
            if (pinned) e.Pinned = true;
            if (e.Pinned && def.Stack != StatusStack.Stack && e.Layers.Count > 0)
            {
                var l0 = e.Layers[0]; l0.Remaining = float.PositiveInfinity; e.Layers[0] = l0;
            }
            // 效能（第⑥階段）：高射速武器每發都會刷新同一個狀態。只是續時間（層數、等級都沒變）時乘數與圖示都不會變，
            // 不必重算／重畫／發事件——狀態列的轉圈是每幀自己讀剩餘時間，不靠事件。100 隻怪被機槍掃時省下大量 Refresh。
            if (Signature(e) == sigBefore)
            {
                e.ShownDuration = e.Pinned ? 0f : Mathf.Max(dur, e.MaxRemaining);
                enabled = true;
                return result;
            }
        }
        e.ShownDuration = e.Pinned ? 0f : Mathf.Max(dur, e.MaxRemaining);
        enabled = true;   // ⚠ 一定要在 MarkChanged 之前：邏輯先生效，畫面（圖示、染色）之後才處理
        MarkChanged();
        return result;
    }

    /// <summary>「免疫」字的節流：距離上次顯示超過 1 秒才回 true。</summary>
    public bool ConsumeImmuneTextSlot()
    {
        if (Time.time - _lastImmuneText < ImmuneTextGap) return false;
        _lastImmuneText = Time.time;
        return true;
    }

    /// <summary>這種狀態目前在身上、而且是常駐的（作弊面板開關）。</summary>
    public bool IsPinned(int statusId) { var e = Find(statusId); return e != null && e.Pinned; }

    /// <summary>拿掉某一種狀態（淨化道具之後用）。</summary>
    public bool Remove(int statusId)
    {
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].Def.Id == statusId) { _entries.RemoveAt(i); MarkChanged(); return true; }
        return false;
    }

    /// <summary>全部清掉（離開關卡、死亡）。</summary>
    public void ClearAll()
    {
        if (_entries.Count == 0 && !enabled) return;
        _entries.Clear();
        _tickClock = 0f;
        MarkChanged();
        enabled = false;
    }

    public bool Has(int statusId) => Find(statusId) != null;

    /// <summary>把目前所有狀態寫進 <paramref name="outList"/>（先清空）。</summary>
    public void GetViews(List<View> outList)
    {
        outList.Clear();
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            float mag;
            switch (e.Def.Category)
            {
                case StatusCategory.Dot: mag = e.TotalDps; break;
                case StatusCategory.Hot: mag = MaxHealth * e.TotalValue; break;
                case StatusCategory.Stat: mag = e.TotalValue; break;
                default: mag = 0f; break;
            }
            outList.Add(new View
            {
                Def = e.Def,
                Level = e.MaxLevel,
                Stacks = e.Layers.Count,
                Remaining = e.MaxRemaining,
                Duration = e.ShownDuration,
                Magnitude = mag,
                Pinned = e.Pinned,
            });
        }
    }

    void Update()
    {
        if (_entries.Count == 0) { enabled = false; return; }
        if (TargetDead) { ClearAll(); return; }
        float dt = Time.deltaTime;   // 遊戲時間：暫停面板開著時一起停
        if (dt <= 0f) return;

        // 1) DoT／HoT：共用時鐘，先結算再扣時間（剛上身的 DoT 不會因為同一幀到期而少跳）
        if (HasTicking())
        {
            _tickClock += dt;
            while (_tickClock >= StatusSystem.TickInterval && _entries.Count > 0)
            {
                _tickClock -= StatusSystem.TickInterval;
                Tick();
                if (TargetDead) { ClearAll(); return; }
            }
        }
        else _tickClock = 0f;

        // 2) 扣時間、移除到期
        bool removed = false;
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            for (int k = e.Layers.Count - 1; k >= 0; k--)
            {
                var l = e.Layers[k];
                l.Remaining -= dt;
                if (l.Remaining <= 0f) { e.Layers.RemoveAt(k); removed = true; }
                else e.Layers[k] = l;
            }
            if (e.Layers.Count == 0) { _entries.RemoveAt(i); removed = true; }
        }
        if (removed) MarkChanged();
        if (_entries.Count == 0) enabled = false;
    }

    void Tick()
    {
        float interval = StatusSystem.TickInterval;
        float heal = 0f;
        _tickBuf.Clear();
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.Def.Category == StatusCategory.Dot)
                _tickBuf.Add((e.Def, e.Source, e.TotalDps * interval));
            else if (e.Def.Category == StatusCategory.Hot)
                heal += MaxHealth * e.TotalValue * interval;
        }

        // 結算傷害（先收集再結算：結算可能把目標打死 → ClearAll 會改動 _entries）
        float dealt = 0f, best = 0f;
        Color color = Color.white;
        for (int i = 0; i < _tickBuf.Count; i++)
        {
            var (def, src, amount) = _tickBuf[i];
            if (amount <= 0f) continue;
            float got = CombatSystem.Apply(new DamageInfo
            {
                Source = src,
                Target = gameObject,
                BaseAmount = amount,
                HitDirection = Vector2.zero,
                Type = def.DamageType,
                IsDot = true,
                IsStatusTick = true,
            });
            dealt += got;
            if (got > best) { best = got; color = def.Color; }
            if (TargetDead) break;
        }
        // 同一跳多種 DoT 合成一個數字；數字有全場預算（第⑥階段效能：傷害數字每個都是新建物件，100 隻怪一起跳會很重）
        if (dealt > 0f && StatusSystem.TryShowTickNumber(this)) DamageNumberManager.ShowDot(gameObject, dealt, color);

        if (heal > 0f && !TargetDead)
        {
            if (_stats != null) _stats.Heal(heal);
            else if (_mc != null) _mc.Heal(heal);
            if (StatusSystem.TryShowTickNumber(this)) DamageNumberManager.ShowHeal(gameObject, heal);
        }
    }

    float MaxHealth => _stats != null ? _stats.MaxHealth : (_mc != null ? _mc.MaxHealth : 0f);

    bool HasTicking()
    {
        for (int i = 0; i < _entries.Count; i++) if (_entries[i].Def.IsTicking) return true;
        return false;
    }

    Entry Find(int id)
    {
        for (int i = 0; i < _entries.Count; i++) if (_entries[i].Def.Id == id) return _entries[i];
        return null;
    }

    /// <summary>會影響乘數／圖示的部分（層數、各層等級）：一樣＝只是續時間。</summary>
    static int Signature(Entry e)
    {
        int h = e.Layers.Count;
        for (int i = 0; i < e.Layers.Count; i++) h = h * 31 + e.Layers[i].Level;
        return h;
    }

    static int IndexOfSoonest(Entry e)
    {
        int idx = 0;
        for (int i = 1; i < e.Layers.Count; i++) if (e.Layers[i].Remaining < e.Layers[idx].Remaining) idx = i;
        return idx;
    }

    /// <summary>重算三個乘數（只在增減時）＋推給怪物的 Actuator＋通知 UI。</summary>
    void MarkChanged()
    {
        float o = 0f, inc = 0f, mv = 0f;
        bool ctl = false, frz = false, stn = false, bnd = false;
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.Def.Category == StatusCategory.Control)
            {
                ctl = true;
                if (e.Def.Attribute == StatusAttribute.Freeze) frz = true;
                else if (e.Def.Attribute == StatusAttribute.Bind) bnd = true;
                else stn = true;   // Stun（以及沒填 Attribute 的控制）＝收招站著
                continue;
            }
            if (e.Def.Category != StatusCategory.Stat) continue;
            float v = e.TotalValue;
            switch (e.Def.Attribute)
            {
                case StatusAttribute.Outgoing: o += v; break;
                case StatusAttribute.Incoming: inc += v; break;
                case StatusAttribute.MoveSpeed: mv += v; break;
            }
        }
        _outMul = Mathf.Max(0f, 1f + o);
        _inMul = Mathf.Max(MinIncomingMul, 1f + inc);
        _moveMul = Mathf.Clamp(1f + mv, MinMoveMul, MaxMoveMul);
        _controlled = ctl;
        _frozen = frz;
        _bindOnly = bnd && !frz && !stn;
        if (_act != null) _act.SpeedMultiplier = _moveMul;

        if (_entries.Count > 0 && _visuals == null)
        {
            _visuals = GetComponent<StatusVisuals>();
            if (_visuals == null) _visuals = gameObject.AddComponent<StatusVisuals>();
        }
        // ⚠ 畫面出錯不能拖垮邏輯（2026-11-18 作者實測：頭上圖示切圖丟例外 ⇒ 例外一路往上丟、元件沒被打開 ⇒
        //   怪物身上的燃燒／中毒完全不跳傷害，見 PROBLEMS E50）。這裡吞掉例外、只印錯誤，乘數與 DoT 照常運作。
        if (_visuals != null)
        {
            try { _visuals.Refresh(); }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }

        Changed?.Invoke();
        AnyChanged?.Invoke(this);
    }
}
