using UnityEngine;

/// <summary>
/// 近戰追擊：**貼身才揮武器，而且揮的期間站定把動作做完，做完才能再移動。**
///
/// <para>和 <see cref="ChaseBrain"/> 的差別只有一件事，但那件事很要命：
/// `ChaseBrain` 只管「離目標 &gt; 0.2 就一直走過去」，攻擊動畫則由
/// `MonsterController.HandleVisuals` 在「距離 ≤ `AttackRange`」時自動播——
/// 兩件事互不相干 ⇒ **怪會一邊舉著劍一邊追著玩家跑**。
/// 作者 2026-09-18 實機回報：「他已經舉起劍了，我躲閃，他竟然還能移動並保持舉劍的動作，太詭異」。</para>
///
/// <para>所以這支把攻擊變成**一段有始有終、期間不能移動的動作**：<br/>
/// `Chase`（走過去，動畫是走路）→ 貼身 → `Attack`（<b>站定</b>，attack 用 one-shot 播完整一輪）→ 回 `Chase`。</para>
///
/// <para><b>傷害沿用既有的接觸傷害</b>（<c>EnemyContactDamage</c>：貼著就每 `AttackInterval` 扣一次
/// `ContactDamage`），本 Brain 一行傷害程式都沒有。所以「揮空」在數值上不等於零傷害——
/// 只要揮的時候還貼著，該扣的照扣；真正閃開的玩家自然就不在接觸範圍內。
/// 要做成「只有揮到的那一幀才判傷」是另一個功能（幀事件 ＋ 扇形判定），還沒做。</para>
///
/// <para>⚠ 用這支就**一定**要設 <c>BrainControlsAttackPose = true</c>（本 Brain 自己會設）：
/// 不設的話 `HandleVisuals` 還是會在 1.3 距離內自動播 attack，於是「走過去的路上就已經舉著劍」，
/// 等於白做。</para>
///
/// `BrainType` 填 `MeleeChase` 即可單獨使用；<see cref="LeapSlamBrain"/> 跳完之後也是委派給它。
/// 見 readme/BOSS_MODULE.md §10。
/// </summary>
public class MeleeChaseBrain : IMonsterBrain
{
    // ⚠ **貼身判定一律用碰撞框的邊緣距離，不要用中心距離**（同 PounceBrain 的 ContactSlack，見 PROBLEMS F22）：
    //   `MonsterController.AttackRange` 預設 1.3 是**中心距離**，而接觸傷害要求兩個框重疊（邊緣 ≤ 0.02）。
    //   怪的框半寬 ≈0.45 ＋ 玩家半徑 0.5 ⇒ **中心距離 1.3 時兩個框還差 0.35**。
    //   用中心距離當「開始揮劍」的門檻，等於每次都在打不到的距離揮空。
    const float AttackSlack = 0.25f;   // 邊緣距離 ≤ 此就起手揮（比接觸門檻寬一點，讓動作提早一拍開始，比較自然）

    // ⚠ **但「停下來」的門檻要比「起手」緊得多**，否則怪會系統性地停在打不到的距離。
    //   2026-09-18 作者附圖回報「維持這個距離怪就砍不到我，他們一直攻擊但我不會受傷」——
    //   根因就是第一版拿 AttackSlack(0.25) 同時當「停下來」的門檻：
    //   狂族皇家衛士的框半寬 0.77 ＋ 玩家半徑 0.5 ⇒ 接觸傷害要求中心距離 ≤ 1.27，
    //   而怪一到 1.52 就停 ⇒ **每次都差 0.25**。不是偶爾打不到，是永遠打不到。
    const float StopSlack = 0.02f;     // 邊緣距離 ≤ 此才停下來（＝與接觸傷害同一條線）

    // ── 揮擊命中判定（2026-09-18 加）──
    // 在「劍掃出去的那一幀」對前方開一次傷害圈（ImpactDamageArea），而不是靠「碰到就痛」。
    // 為什麼要這樣：近戰武器有長度，劍尖掃得到的地方遠比身體碰撞框寬——
    // 狂族皇家衛士的 attack 第 9~12 幀劍已經掃出畫布外，而接觸傷害完全不知道劍在哪裡。
    // **幀號即事件**（同 §8.2b 的放箭幀）：改 CSV 的 AnimFPS 時機會自動跟著對。
    // 命中幀（attack 的第幾張是「武器揮到位」）**從 CSV 的 `AttackHitFrame` 讀**，這裡只是沒填時的退路。
    // ⚠ **比例完全靠不住，一定要逐怪量**：實測狂族皇家衛士 9/12（75%）、狼人兵 8/24（33%）、吸血鬼兵 11/25（44%）。
    //   量法：印 attack 每幀不透明像素的 bbox，**寬度／邊緣突然暴增的那一幀**就是武器揮出去的時刻。
    const float HitFrameFallbackRatio = 0.7f;

    // ⭐⭐ **命中判定與起手判定用同一把尺：碰撞框的邊緣距離**（2026-09-18 大修，見 PROBLEMS **F27**）。
    //   第一版是「以怪的腳底為圓心、半徑＝框半寬＋0.55 開一個圈」去猜打得到誰——**那裡錯了兩件事**：
    //   ① `0.55` 是寫死的世界單位：怪放大時劍也跟著放大，但這個數字不會 ⇒ 相對變短。
    //   ② 更致命：**圈心在腳底，而玩家的碰撞圓 offset 是 0 ⇒ 圓心就在玩家的 transform**。
    //      怪的 pivot 在畫布中心，腳底離 transform 有半個身高，**而那個差距隨 Scale 線性長大**
    //      ⇒ Scale 1.3 時圈邊緣還多 0.27 才碰得到玩家、1.5 剩 0.23、**2.0 只剩 0.12**，再大就砍不到。
    //      作者 2026-09-18 把 Scale 調到 1.5 之後回報「砍不到的問題又出現了」，就是這條。
    //   現在改成：命中幀直接問「目標的碰撞框離我的碰撞框多遠」——**與起手門檻同一個函式**，
    //   所以「起手時打得到的目標，命中幀只要沒跑遠就一定打得到」在數學上成立，而且**完全與體型無關**。
    const float SwingReachSlack = 0.75f;   // 命中門檻（邊緣距離）。比起手門檻 0.25 大，代表劍比身體長
    const float SwingHitRadius  = 0.2f;    // 判定過了才在**目標身上**開一個小圈結算 ⇒ 必中，不會因為圈心算錯而 miss

    // 用揮擊判定的怪要不要關掉「碰到就痛」。
    // true ＝ 傷害**只**來自揮擊 ⇒ 玩家貼著牠走位、躲過揮擊就不會掉血（近戰該有的樣子）。
    // false ＝ 兩者並存（貼著會被接觸傷害慢慢磨，揮擊再補一刀）。改這一行就能切換。
    const bool DisableContactDamage = true;

    // 攻擊動畫的播放倍率（相對 CSV 的 AnimFPS）。1 ＝ 照 CSV 的節奏。
    const float AttackFpsMul = 1f;

    // 揮空/打完之後的硬直：站著不動多久才恢復追擊。0 ＝ 動作一播完立刻能動。
    const float RecoverSeconds = 0.12f;

    // 這隻怪沒有 attack 圖時的退路：不硬演，就是單純的貼上去（＝ChaseBrain 的行為）。
    const float StopDistance = 0.2f;

    /// <summary>除錯：印出每次揮劍的距離與結果。</summary>
    public static bool DebugLog = false;

    enum Phase { Chase, Attack, Recover }

    Phase _phase = Phase.Chase;
    float _phaseUntil;
    float _nextAttackAt;         // 下一次可以揮的時間（＝攻擊節奏，見 BeginAttack）
    bool _configured;
    MonsterAnimator _anim;
    Collider2D _selfCol, _targetCol;
    Transform _targetColOwner;
    Vector2 _swingDir = Vector2.right;   // 這一次揮擊的方向（起手時記下，揮到一半目標移動也不轉——動作已經定型）
    bool _swingHit;                      // 這一次揮擊是否已經結算過命中幀
    int  _hitFrame = 1;                  // 這隻怪實際用的命中幀（起手時依 CSV／退路算好並夾進張數）
    bool _hasAttackAnim;                 // 這隻怪有沒有 attack 圖；沒有的話整套「揮舞型」都不適用（見 EnsureConfigured）

    /// <summary>
    /// 套用本 Brain 需要的設定：接管攻擊姿勢、關掉「碰到就痛」。呼叫幾次都只會生效一次。
    /// <para>公開是為了讓 <see cref="LeapSlamBrain"/> 在**跳躍階段就先套用**——否則怪在跳完之前
    /// 還是舊行為（走近就自動舉劍、碰到就扣血），跳完交棒的那一刻才突然改變。</para>
    /// </summary>
    public void EnsureConfigured(MonsterController self)
    {
        if (_configured || self == null) return;
        _configured = true;
        _anim = self.Anim;

        // ⚠⚠ **沒有 attack 圖的怪不能套這一套**，而且後果很嚴重：
        //   本 Brain 會把「碰到就痛」關掉、改由揮擊的命中幀結算，
        //   但沒有 attack 圖就永遠不會揮 ⇒ **那隻怪會完全無害**，而且不會有任何錯誤訊息。
        //   所以這裡整段退回「衝撞型」（＝ChaseBrain 的行為：貼上去，傷害走接觸傷害），
        //   並且**印一則 warning**——會走到這裡一定是 CSV 設定錯了（該填 Chase 卻填了 MeleeChase）。
        _hasAttackAnim = _anim != null && _anim.Has(MonsterAnimator.State.Attack);
        if (!_hasAttackAnim)
        {
            Debug.LogWarning($"[MeleeChase] 「{self.MonsterName}」沒有 attack 圖，" +
                             "自動退回「衝撞型」（貼上去、傷害走接觸傷害）。" +
                             "揮舞型近戰需要 attack 序列圖；只想要衝撞的話 MonsterData 的 BrainType 請填 Chase。");
            return;
        }

        // 攻擊姿勢從此由本 Brain 全權控制（見 MonsterController.BrainControlsAttackPose）。
        self.BrainControlsAttackPose = true;

        // 傷害改由揮擊的命中幀結算（見 DisableContactDamage）。
        if (DisableContactDamage)
        {
            var contact = self.GetComponent<EnemyContactDamage>();
            if (contact != null) contact.enabled = false;
        }
    }

    public void Think(in MonsterContext ctx)
    {
        MonsterActuator act = ctx.Actuator;
        if (act == null) return;

        EnsureConfigured(ctx.Self);

        Vector2 pos = act.transform.position;
        Transform target = Nearest(pos, ctx.Enemy, ctx.Player);

        if (target == null)
        {
            if (_phase == Phase.Chase) act.Stop();   // 攻擊中不因為目標消失而中斷（動作要做完）
        }

        // 貼身判定要的兩個 collider（快取；目標換人才重取）
        if (_selfCol == null) _selfCol = act.GetComponent<Collider2D>();
        if (target != null && _targetColOwner != target)
        {
            _targetColOwner = target;
            _targetCol = target.GetComponent<Collider2D>();
        }

        switch (_phase)
        {
            case Phase.Chase:
                if (target == null) { act.Stop(); break; }

                // 沒有 attack 圖 → 衝撞型：一路貼上去，傷害走接觸傷害（見 EnsureConfigured）
                if (!_hasAttackAnim)
                {
                    if (Vector2.Distance(pos, target.position) > StopDistance) act.MoveTowards(target.position);
                    else act.Stop();
                    break;
                }

                if (Touching(AttackSlack) && Time.time >= _nextAttackAt && BeginAttack(ctx, act, target)) break;

                // ⚠ 停下來的門檻是 StopSlack(0.02) 不是 AttackSlack(0.25)：拿起手門檻當停止門檻
                //   會讓怪系統性地停在打不到的距離（見 StopSlack 的註解）。
                if (Touching(StopSlack)) { act.Stop(); break; }

                if (Vector2.Distance(pos, target.position) > StopDistance) act.MoveTowards(target.position);
                else act.Stop();
                break;

            // ── 揮武器：站定，把動作做完。**這一段沒有任何中途取消**——
            //    目標跑掉了也要揮完，這正是作者要的「進入攻擊動作後就不能移動」。──
            case Phase.Attack:
                act.Stop();

                // 劍掃出去的那一幀 → 開一次揮擊傷害圈。**只結算一次**（_swingHit）。
                if (!_swingHit && _anim != null && _anim.OneShotFrame >= _hitFrame)
                {
                    _swingHit = true;
                    SpawnSwingHit(ctx, act);
                }

                if (_anim == null || _anim.OneShotFinished || Time.time >= _phaseUntil)
                {
                    if (_anim != null) _anim.CancelOneShot();
                    _phase = Phase.Recover;
                    _phaseUntil = Time.time + RecoverSeconds;
                    if (DebugLog) Debug.Log($"[MeleeChase] 揮完　邊緣距離={EdgeDistance():F2}");
                }
                break;

            case Phase.Recover:
                act.Stop();
                if (Time.time >= _phaseUntil) _phase = Phase.Chase;
                break;
        }
    }

    /// <summary>
    /// 開始揮一次。沒有 attack 圖就回 false（**退回單純追擊**，不硬演一個不存在的動作）。
    /// </summary>
    bool BeginAttack(in MonsterContext ctx, MonsterActuator act, Transform target)
    {
        if (_anim == null) return false;
        if (!_anim.PlayOneShot(MonsterAnimator.State.Attack, 1, 0, AttackFpsMul)) return false;

        act.Stop();
        _swingHit = false;

        // 揮擊方向＝**起手當下**的目標方向。揮到一半目標移動也不修正——動作已經定型，
        // 這也是玩家「走位閃掉揮擊」的空間（同 §9 落點鎖定的取捨）。
        Vector2 d = (Vector2)target.position - (Vector2)act.transform.position;
        if (d.sqrMagnitude > 1e-4f) _swingDir = d.normalized;

        var self = ctx.Self;
        float fps = Mathf.Max(1f, (self != null ? self.AnimFPS : 8f) * AttackFpsMul);
        int frames = _anim.FrameCount(MonsterAnimator.State.Attack);
        float animSeconds = frames > 0 ? frames / fps : 1f;

        // 命中幀：CSV 的 AttackHitFrame 優先，沒填就用張數 × 比例粗估；一律夾進實際張數
        // （否則換一隻 attack 只有 6 張的怪時，一個寫死的 9 永遠不會到 ⇒ 一輩子揮空不傷人）。
        int want = (self != null && self.AttackHitFrame > 0)
                   ? self.AttackHitFrame
                   : Mathf.RoundToInt(frames * HitFrameFallbackRatio);
        _hitFrame = frames > 0 ? Mathf.Clamp(want, 1, frames) : Mathf.Max(1, want);

        // 攻擊節奏：從**這一次揮的開始**算 AttackInterval，而不是從結束算——
        // 否則 CSV 的 AttackInterval 會變成「動作時間 ＋ 間隔」，一隻攻速 0.8 的怪實際上要 1.7 秒才揮一次。
        // 動作本身比間隔長時，以動作為準（動作沒播完當然不能再揮）。
        float interval = (self != null && self.AttackInterval > 0.01f) ? self.AttackInterval : 0.5f;
        _nextAttackAt = Time.time + Mathf.Max(interval, animSeconds);

        // 保險：動畫萬一沒有正常結束（幀數異常、被外部 Cancel），也不要卡在 Attack 不動
        _phaseUntil = Time.time + animSeconds + 0.5f;
        _phase = Phase.Attack;

        if (DebugLog)
            Debug.Log($"[MeleeChase] 揮劍：{frames} 幀 @{fps:F0}fps ＝ {animSeconds:F2}s　" +
                      $"下次可揮 @{_nextAttackAt - Time.time:F2}s 後　邊緣距離={EdgeDistance():F2}");
        return true;
    }

    /// <summary>
    /// 命中幀的結算。**不自己算圈在哪**——直接問「目標的碰撞框離我的碰撞框多遠」
    /// （<see cref="SwingReachSlack"/>），過了就在**目標身上**開一個小圈讓既有的傷害系統結算。
    ///
    /// <para>⭐ 兩個好處：① **與起手門檻同一把尺**，所以「起手時打得到、命中幀還沒跑遠 ⇒ 必中」在數學上成立；
    /// ② **完全與體型無關**——邊緣距離是框對框算的，怪放大縮小、pivot 在哪裡都不影響。
    /// 自己算圓心就會踩到「基準不一致 × 體型放大」那個坑（見 readme/PROBLEMS.md **F27**）。</para>
    /// </summary>
    void SpawnSwingHit(in MonsterContext ctx, MonsterActuator act)
    {
        var self = ctx.Self;
        if (self == null) return;

        float damage = self.ContactDamage;
        float edge = EdgeDistance();

        if (_targetCol == null || !Touching(SwingReachSlack))
        {
            // 揮空了——目標在起手之後走開了。這是**設計上允許的**（走位閃刀），所以只在除錯時印。
            if (DebugLog)
                Debug.Log($"[MeleeChase] 揮空：命中幀邊緣距離 {edge:F2} > {SwingReachSlack}（起手門檻 {AttackSlack}）");
            return;
        }

        // 開在**目標的碰撞框中心**、半徑很小 ⇒ 必定重疊，不會因為圈心算錯而 miss。
        ImpactDamageArea.Spawn(_targetCol.bounds.center, SwingHitRadius, damage, self.Faction);

        if (DebugLog)
            Debug.Log($"[MeleeChase] 揮中：邊緣距離 {edge:F2} ≤ {SwingReachSlack}　傷害 {damage:F0}");
    }

    /// <summary>自己與目標的**碰撞框邊緣**距離 ≤ slack（重疊也算）。見 <see cref="AttackSlack"/>。</summary>
    bool Touching(float slack)
    {
        if (_selfCol == null || _targetCol == null) return false;
        ColliderDistance2D d = Physics2D.Distance(_selfCol, _targetCol);
        return d.isValid && (d.isOverlapped || d.distance <= slack);
    }

    float EdgeDistance()
    {
        if (_selfCol == null || _targetCol == null) return -1f;
        ColliderDistance2D d = Physics2D.Distance(_selfCol, _targetCol);
        return d.isValid ? d.distance : -1f;
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
