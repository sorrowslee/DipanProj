using UnityEngine;

/// <summary>
/// 血霧型（菁英血衛士）：**離目標遠了就被紅霧吞沒、化成無敵的血霧追過去，貼身才吹散紅霧現身揮刀**。
/// 逼玩家只能在牠現身、揮刀的那一段反擊（作者 2026-10-13 的設計）。見 readme/BOSS_MODULE.md §13。
///
/// <para><b>一輪：</b><br/>
/// `Melee`（委派 <see cref="MeleeChaseBrain"/>：近距離走過去、站定揮完、命中幀結算）<br/>
/// → 目標的邊緣距離 &gt; <see cref="MistDistance"/> → `Dissolve`（站定 idle，紅霧由下往上湧出蓋住全身，霧蓋滿後本體在霧底下淡掉）<br/>
/// → `Cloud`（只剩紅霧，以 CSV 的 Speed 追過去）<br/>
/// → 貼身 → `Reform`（站定，本體在霧底下淡回來、紅霧被吹散）<br/>
/// → 回 `Melee`（貼身＋冷卻好了就立刻起手揮刀）。</para>
///
/// <para>⭐⭐ <b>無敵的範圍＝「紅霧在場」的整段</b>（作者 2026-10-13 拍板）：從紅霧**開始湧出**的那一刻起就無敵，
/// 到現身時紅霧**完全散掉**才解除。所以湧出的前半段畫面上還看得到怪，但武器已經打不到——這是刻意的。</para>
///
/// <para><b>不用任何序列圖</b>：第一版用 mist 序列圖（人→霧）化霧、倒播現身，作者覺得序列圖醜，
/// 改成「idle ＋ shader 紅霧蓋住／吹散」（2026-10-13）。所以任何有 idle 的怪都能套這支。</para>
///
/// <para><b>為什麼攻擊段委派而不是自己寫：</b><see cref="MeleeChaseBrain"/> 的「站定揮完」「起手／停止兩個門檻」
/// 「命中幀用碰撞框邊緣距離結算（與體型無關，PROBLEMS F27/F29）」都踩過坑才定版，重寫一次等於重踩一次。</para>
///
/// <para><b>無敵怎麼做的：</b><see cref="MonsterController.SetUntargetable"/>——換到 Mist 層讓所有武器直接穿過
/// ＋ TakeDamage 防線。見 <see cref="MistLayer"/>。</para>
///
/// <para><b>近距離不化霧</b>（作者拍板）：目標只退一兩步、或牠在攻擊冷卻中，交給 MeleeChase 用 walk 走過去——
/// 這段**是可以被打的**，也是玩家的另一個反擊窗口。</para>
/// </summary>
public class BloodMistBrain : IMonsterBrain
{
    // ── 何時化霧／何時現身（一律用碰撞框的邊緣距離，同 MeleeChaseBrain，見 PROBLEMS F22）──
    // 邊緣距離 > 此值才化霧追；以內交給 MeleeChase 走過去（作者：「距離夠遠才化霧」）。
    const float MistDistance = 2.5f;
    // 霧追到邊緣距離 ≤ 此值就開始現身。＝MeleeChase 的起手門檻（AttackSlack 0.25）：
    // 現身完正好在「可以起手」的距離，冷卻好了就直接揮。
    const float ReformSlack = 0.25f;

    // 紅霧湧出到這個濃度（0~1）時開始讓本體淡掉——要等霧先蓋上去，人才在霧底下消失，
    // 太早淡會變成「人先不見、霧才出來」。
    const float HideBodyAtCloud = 0.6f;

    // 霧態最多追多久就強制現身（路被擋死、繞不過去時不要永遠是一團打不到的霧）。
    const float MaxCloudSeconds = 6f;

    // 保險：化霧／現身的過場最多等這麼久（正常約 0.4 秒；visual 異常時不要卡在無敵的過場裡）。
    const float MaxTransitionSeconds = 1.5f;

    /// <summary>除錯：印出每次化霧／現身的距離與時間。</summary>
    public static bool DebugLog = false;

    enum Phase { Melee, Dissolve, Cloud, Reform }

    readonly MeleeChaseBrain _melee = new MeleeChaseBrain();
    Phase _phase = Phase.Melee;
    float _phaseStart;
    bool _configured;
    BloodMistVisual _fx;
    MonsterController _self;
    Collider2D _selfCol, _targetCol;
    Transform _targetColOwner;

    public void Think(in MonsterContext ctx)
    {
        MonsterActuator act = ctx.Actuator;
        MonsterController self = ctx.Self;
        if (act == null || self == null) return;

        EnsureConfigured(self);

        Transform target = Nearest(act.transform.position, ctx.Enemy, ctx.Player);
        if (_selfCol == null) _selfCol = act.GetComponent<Collider2D>();
        if (target != null && _targetColOwner != target)
        {
            _targetColOwner = target;
            _targetCol = target.GetComponent<Collider2D>();
        }

        switch (_phase)
        {
            case Phase.Melee:
                // 揮刀／收招中絕不打斷；目標遠了才化霧
                if (target != null && !_melee.IsBusy && EdgeDistance() > MistDistance)
                {
                    BeginDissolve(act);
                    break;
                }
                _melee.Think(ctx);
                break;

            // ── 化霧：站定（idle），紅霧湧出蓋住全身 → 本體在霧底下淡掉 ──
            case Phase.Dissolve:
                act.Stop();
                if (_fx.Cloud >= HideBodyAtCloud) _fx.SetBodyVisible(false);
                if ((_fx.Cloud >= 0.999f && _fx.Body <= 0.001f) || Time.time - _phaseStart >= MaxTransitionSeconds)
                {
                    _fx.SetBodyVisible(false);
                    _phase = Phase.Cloud;
                    _phaseStart = Time.time;
                }
                break;

            // ── 霧態追擊：全程無敵，以 CSV 的 Speed 追過去 ──
            case Phase.Cloud:
                if (target == null || Touching(ReformSlack) || Time.time - _phaseStart >= MaxCloudSeconds)
                {
                    BeginReform(act);
                    break;
                }
                act.MoveTowards(target.position);
                break;

            // ── 現身：站定，本體淡回來、紅霧吹散；**霧完全散掉才解除無敵** ──
            case Phase.Reform:
                act.Stop();
                if ((_fx.Cloud <= 0.001f && _fx.Body >= 0.999f) || Time.time - _phaseStart >= MaxTransitionSeconds)
                {
                    self.SetUntargetable(false);
                    _phase = Phase.Melee;
                    if (DebugLog) Debug.Log($"[BloodMist] 現身完成　邊緣距離={EdgeDistance():F2}");
                }
                break;
        }
    }

    void EnsureConfigured(MonsterController self)
    {
        if (_configured) return;
        _configured = true;
        _self = self;

        // 攻擊段的設定（接管攻擊姿勢、關掉碰到就痛）**一開場就套**，不要等第一次進 Melee 才套
        _melee.EnsureConfigured(self);

        _fx = self.GetComponent<BloodMistVisual>();
        if (_fx == null) _fx = self.gameObject.AddComponent<BloodMistVisual>();
    }

    void BeginDissolve(MonsterActuator act)
    {
        act.Stop();
        // ⭐ 紅霧一出現就無敵（作者拍板）——不等霧蓋滿、也不等本體消失
        _self.SetUntargetable(true);
        _fx.SetCloud(true);
        _phase = Phase.Dissolve;
        _phaseStart = Time.time;
        if (DebugLog) Debug.Log($"[BloodMist] 化霧　邊緣距離={EdgeDistance():F2}");
    }

    void BeginReform(MonsterActuator act)
    {
        float cloudSeconds = Time.time - _phaseStart;
        act.Stop();
        // 本體先在霧底下淡回來（0.18 秒），霧被吹散得比較慢（約 0.3 秒）⇒ 霧散開時人已經站在那裡
        _fx.SetBodyVisible(true);
        _fx.SetCloud(false);
        _phase = Phase.Reform;
        _phaseStart = Time.time;
        if (DebugLog) Debug.Log($"[BloodMist] 開始現身　霧態 {cloudSeconds:F2}s　邊緣距離={EdgeDistance():F2}");
    }

    /// <summary>自己與目標的**碰撞框邊緣**距離 ≤ slack（重疊也算）。同 MeleeChaseBrain。</summary>
    bool Touching(float slack)
    {
        if (_selfCol == null || _targetCol == null) return false;
        ColliderDistance2D d = Physics2D.Distance(_selfCol, _targetCol);
        return d.isValid && (d.isOverlapped || d.distance <= slack);
    }

    /// <summary>邊緣距離；量不到回 -1（＝不觸發化霧）。</summary>
    float EdgeDistance()
    {
        if (_selfCol == null || _targetCol == null) return -1f;
        ColliderDistance2D d = Physics2D.Distance(_selfCol, _targetCol);
        return d.isValid ? (d.isOverlapped ? 0f : d.distance) : -1f;
    }

    static Transform Nearest(Vector2 pos, Transform a, Transform b)
    {
        if (a == null) return b;
        if (b == null) return a;
        float da = ((Vector2)a.position - pos).sqrMagnitude;
        float db = ((Vector2)b.position - pos).sqrMagnitude;
        return da <= db ? a : b;
    }
}
