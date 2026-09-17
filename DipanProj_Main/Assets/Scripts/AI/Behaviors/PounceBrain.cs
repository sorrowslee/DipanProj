using UnityEngine;

/// <summary>
/// 撲擊型（狗／狼／豹這類四足掠食者）的戰鬥模組：
/// **不像 <see cref="ChaseBrain"/> 那樣一路貼上去磨**，而是「站定觀察 → 側移試探 → 再站定 → 突然加速撲過來 → 收招喘一下」。
/// 玩家要讀的是牠的節奏（看到牠停下來壓低就準備側身閃），不是單純的走位拉扯。
///
/// 六段狀態機（`Phase`）：
///   Approach   太遠 → 正常速度接近，進入 StalkRange 就開始觀望
///   StalkHold  **站定不動**（動畫回 idle）——觀望的主體，掠食者在等破綻
///   StalkStep  往側邊**移動一小段固定距離就停**（不是持續繞圈）
///   ↑ 這兩段交替 `StalkRoundsMin~Max` 輪（各自隨機），才進入下一段
///   Windup     站定蓄力 ＝ 給玩家看得見的預告窗口
///   Charge     **起跑鎖定方向、全程直線**、速度 ×ChargeSpeedMul
///   Recover    站定收招 ＝ 玩家的輸出窗口，然後回 StalkHold
///
/// **傷害完全沿用既有的接觸傷害**（<c>EnemyContactDamage</c>：碰到就依 `AttackInterval` 扣 `ContactDamage`）——
/// 所以「撞擊」與「咬」在數值上是同一回事，本 Brain 一行傷害程式都沒有。
/// **attack 動畫也不必自己播**：`MonsterController.HandleVisuals` 只要「目標距離 ≤ `AttackRange`(1.3) 且有 attack 幀」
/// 就會自動演攻擊動作，所以「停在你面前咬」是現成的。
///
/// 目標取「最近的敵對目標」（`ctx.Enemy` 與 `ctx.Player` 取近者，同 <see cref="WarBrain"/>）——
/// 對 `Enemy` 陣營與三方陣營的 `Werewolf`／`Vampire` 都成立，不必為陣營寫特例。
///
/// ⚠ **衝刺與側移都綁「距離」不綁「時間」**（秒數只是保險上限）：綁時間的話 CSV 的 `Speed` 一調小，
/// 每次移動的距離就等比縮水，變成「原地抽動一下」——同 readme/PROBLEMS.md **F20** 踩過的坑。
/// </summary>
public class PounceBrain : IMonsterBrain
{
    // ── 距離（世界單位）──
    const float StalkRange = 5.0f;   // 進到這麼近就停止直線接近、改成觀望
    // 超出這個距離就回去追（觀望圈的外緣）。⚠ 第一版寫 StalkRange×1.25＝6.25，遲滯開太大：
    // 玩家只是在走動、還沒真的跑遠，狼卻站在原地不跟上來 ⇒ 作者實機回報「在我身邊等啊等」。
    // 只要比 StalkRange 大一點、夠擋住臨界抖動就好。
    const float StalkExitRange = 5.6f;
    const float StalkNear  = 3.0f;   // 側移時想維持的最小半徑（不要黏上去，撲擊才有空間）
    // ⚠ **貼身判定一律用「碰撞框邊緣距離」，不要用中心距離**（2026-09-17 修，這是「咬不到人」的真正原因）：
    //   戰狼的身體框半寬 ≈0.45（可見 0.70 ＋ HitboxPadding 0.2），玩家的 CircleCollider2D 半徑 0.5 ⇒
    //   **中心距離 1.3 的時候兩個框還差 0.35 沒碰到**，而 EnemyContactDamage 要求「重疊或邊緣距離 ≤0.02」
    //   ⇒ 狼衝到 1.3 就停下來播咬的動作，血卻一點都沒掉（作者實機回報「衝過來咬一下、然後沒咬到」）。
    //   體型一換這個誤差就變，寫死中心距離永遠會錯；`Physics2D.Distance` 直接算兩個框的邊緣距離，
    //   與接觸傷害同一個幾何來源。（同 readme/PROBLEMS.md **B7** 的通則：貼身判定用 Physics2D.Distance。）
    const float ContactSlack = 0.02f;   // 衝刺「到了」＝與接觸傷害同一條線（重疊或邊緣 ≤0.02）
    const float BiteSlack    = 0.25f;   // 觀望時「算不算已經貼身」＝決策用，寬鬆一點避免在邊緣抖動

    // ── 觀望：站定 ↔ 側移 交替 ──
    const float HoldSecondsMin = 0.6f;   // 站定觀察多久（隨機區間）。這是「牠在等」的那段，別調太短
    const float HoldSecondsMax = 1.2f;
    const float StepDistMin = 1.5f;      // 一次側移的距離（隨機區間；綁距離不綁時間）
    const float StepDistMax = 2.5f;
    const float StepMaxSeconds = 2.0f;   // 側移的保險上限（被牆擋住時才會用到）
    // 站定↔側移交替幾輪才撲（各自隨機，一群狼才不會同步）。
    // ⚠ 第一版 1~3，作者實機回報「在我旁邊晃啊晃大概 3~4 次」——觀望是為了製造壓迫感，太多輪就變成發呆。
    const int   StalkRoundsMin = 1;
    const int   StalkRoundsMax = 2;
    const float SideFlipChance = 0.4f;   // 每次側移有多少機率換邊（左右來回試探，不是一直繞同一圈）
    const float StepAngleMin = 40f;      // 側移方向：以「目標→自己」為 0°，轉這個角度區間（越大越像繞圈）
    const float StepAngleMax = 70f;

    // 貼身咬的最長時間：超過就強制退開一步、重新拉開距離再撲。
    // ⚠ 沒有這個上限，玩家只要站著不動（硬扛/在打別的怪），狼撲過來之後就**永遠貼著咬**——
    //    模擬跑 60 秒只撲得出一次，節奏整個不見，玩家也沒有輸出窗口（2026-09-17 靜態模擬抓到）。
    const float BiteMaxSeconds = 1.5f;

    // ── 蓄力 / 收招（站定不動）──
    const float WindupSeconds = 0.4f;    // 預告窗口
    const float RecoverSeconds = 0.7f;   // 輸出窗口

    // ── 衝刺（2026-09-17 改：全程追蹤，不再鎖定起跑點）──
    const float ChargeSpeedMul = 3.0f;   // 衝刺速度 = CSV Speed × 此。⭐ 手感就調這個數字
    // 衝多久放棄（不管有沒有咬到都收招）＝**依起跑時的距離動態算**，不是固定秒數。
    // ⚠ 固定秒數是錯的：0.8 秒 ×10.5 ＝ 只能跑 8.4 單位，**起跑距離再遠也只前進那麼多** ⇒
    //   離得遠一點就「衝到一半停下來咬空氣」（作者實機回報：「不管我跟他多遠，他都只前進固定距離」）。
    //   牠要做的是「一口氣跑到你身上」，所以時限 = 跑完那段距離要的時間 × 餘裕 ＋ 固定緩衝。
    // ⭐ `ChargeTimeSlack` 就是「玩家能不能靠移動躲掉」的旋鈕：1.0 ＝ 只夠跑完起跑距離（你一動就閃掉），
    //    越大越黏。嫌太難纏先調它，再調 ChargeSpeedMul。
    const float ChargeTimeSlack = 1.6f;
    const float ChargeTimeBuffer = 0.25f;   // 起步/轉向的固定損耗
    const float ArriveDistSq = 0.35f * 0.35f;   // StalkStep 判「走到落點了沒」用
    // 撞到東西的判定：**看一段時間內的累積位移**，不是單幀位移。
    // ⚠ 單幀位移是錯的，而且錯得很隱蔽：物理是 60Hz，只要畫面跑得比 60fps 快，就有整幀 transform 完全沒動
    //   （見 MonsterActuator.Awake 補 Interpolate 的那段註解）⇒ `moved = 0` ⇒ 第一個這樣的幀就把衝刺判成撞牆。
    //   實機症狀是「衝刺只前進 1.2 單位（≈ grace 那段）就停下來咬空氣」，而**靜態模擬永遠測不出來**
    //   （模擬裡每一幀都有位移）。改成每 BlockCheckInterval 結算一次累積位移，物理與渲染不同步就影響不到它。
    const float StuckMoveRatio = 0.35f;
    const float ChargeGraceSeconds = 0.12f;   // 起跑緩衝（velocity 剛設、還沒推起來）
    const float BlockCheckInterval = 0.2f;    // 每隔這麼久結算一次「這段時間到底跑了多遠」

    /// <summary>除錯：把每次衝刺的起跑距離／結束原因／實際跑了多遠印出來。抓手感或懷疑判定出錯時打開。</summary>
    public static bool DebugLog = false;

    enum Phase { Approach, StalkHold, StalkStep, Windup, Charge, Recover }

    Phase _phase = Phase.Approach;
    float _phaseUntil;        // 這個階段到什麼時候（Time.time）
    // 還要交替幾輪才撲。**-1 ＝ 還沒抽過**（只有「第一次進入觀望」與「撲完一輪之後」才重抽，見 EnterStalk）。
    int _roundsLeft = -1;
    int _side = 1;            // 目前側移方向（+1 / -1）
    Vector2 _stepTarget;      // 側移落點（進入 StalkStep 時算好、全程不再修正）
    float _stepStart;         // 側移開始時間（保險上限用）
    float _chargeStart;
    float _chargeDeadline;    // 這次衝刺的時限（BeginCharge 依起跑距離算）
    float _blockCheckAt;      // 下次結算累積位移的時間
    Vector2 _blockCheckPos;   // 上次結算時的位置
    Vector2 _chargeFromPos;   // 起跑位置（除錯 log 用）
    float _chargeFromDist;    // 起跑距離（除錯 log 用）
    Collider2D _selfCol;      // 自己的身體框（快取；貼身判定用邊緣距離，見 ContactSlack）
    Collider2D _targetCol;    // 目標的碰撞框（快取）
    Transform _targetColOwner;
    float _biteStart = -1f;   // 這一段貼身咬是從什麼時候開始的（-1＝現在沒在貼身）

    // 衝刺期間暫改 Actuator 的兩個設定，結束要還原。
    bool _charging;
    float _savedSpeed;
    bool _savedAvoid;

    bool _inited;

    public void Think(in MonsterContext ctx)
    {
        MonsterActuator act = ctx.Actuator;
        if (act == null) return;
        Vector2 pos = act.transform.position;

        if (!_inited)
        {
            _inited = true;
            // 起始側移方向左右隨機：一群狼才不會整齊劃一地往同一邊繞。
            // （撲擊的時間點不必在這裡錯開——每輪的 Hold 秒數與輪數都各自隨機，自然就不同步了。）
            _side = Random.value < 0.5f ? 1 : -1;
        }

        // ⚠ 衝刺的收尾檢查放在最前面、不放在 Charge 分支裡：怪被打時的擊退窗口會**整段跳過 Think()**
        //   （見 readme/PROBLEMS.md F19），窗口一結束要第一時間把速度/避障還原，別讓牠帶著 3 倍速跑去做別的事。
        if (_charging && _phase != Phase.Charge) EndCharge(act);

        Transform target = Nearest(pos, ctx.Enemy, ctx.Player);
        if (target == null)
        {
            // 沒有敵對目標（Enemy 陣營＝玩家不在感測範圍；部族＝劇本正在和平段）→ 站著。
            if (_charging) EndCharge(act);
            act.Stop();
            _phase = Phase.Approach;
            return;
        }

        Vector2 tp = target.position;
        float dist = Vector2.Distance(pos, tp);   // 中心距離：只拿來判「遠近」，貼身一律用下面的 Touching

        // 貼身判定要的兩個 collider（快取；目標換人才重取）。
        if (_selfCol == null) _selfCol = act.GetComponent<Collider2D>();
        if (_targetColOwner != target) { _targetColOwner = target; _targetCol = target.GetComponent<Collider2D>(); }

        switch (_phase)
        {
            // ── 太遠：正常速度走過去 ──
            case Phase.Approach:
                // ⚠ 回到觀望圈時**不重抽輪數**（reroll=false）：玩家開火的擊退會把牠往外推
                //   （每次約 0.5 單位，兩下就超出 StalkExitRange），重抽的話「觀望進度」永遠歸零、
                //   牠就一輩子撲不出來——作者實機回報「我一攻擊牠就比較不會發動攻擊，一直左晃右晃」。
                if (dist <= StalkRange) { EnterStalk(false); break; }
                act.MoveTowards(tp);
                break;

            // ── 觀望‧站定：掠食者在等破綻。這一段是「不動」，節奏感全靠它 ──
            case Phase.StalkHold:
                // 「站定觀望」其實是**保持距離**，不是絕對靜止：被推出觀望圈就自己走回來
                // （不中斷觀望、不消耗輪數）。
                // ⚠ 這一條是被實測數據逼出來的：原本這裡無條件 act.Stop()，而玩家開火的擊退每次推 0.5 單位、
                //   一段 0.6~1.2 秒的站定會被推 2~3 次 ⇒ **必定**超出 StalkExitRange ⇒ 回 Approach 走回來 ⇒
                //   又站定又被推出去……輪數永遠減不掉、撲不出來。靜態模擬：擊退頻率 ≥ 每 0.35 秒一次時，
                //   60 秒內撲擊次數是 0.1（等於被完全壓制）。改成會自己逼回來之後才解得掉。
                //   正常情況（沒被打）dist 遠小於 StalkRange，還是站著不動，節奏感不受影響。
                if (dist > StalkRange) act.MoveTowards(tp);
                else act.Stop();
                if (Touching(BiteSlack))
                {
                    // 已經貼身了就別撲（撲出去反而拉開距離）——站定咬，動畫由 MonsterController 自動切 attack。
                    if (_biteStart < 0f) _biteStart = Time.time;
                    // …但咬夠了就要退開重新拉距離，否則玩家一站著不動，狼會永遠黏在他身上磨血（見 BiteMaxSeconds）。
                    if (Time.time - _biteStart >= BiteMaxSeconds)
                    {
                        _biteStart = -1f;
                        _roundsLeft = 0;              // 退這一步之後就直接進蓄力→撲，不再多繞
                        BeginStep(pos, tp, dist);     // want 會被夾到 StalkNear ⇒ 落點在外圈＝往後退
                        break;
                    }
                    _phaseUntil = Mathf.Max(_phaseUntil, Time.time + 0.2f);
                    break;
                }
                _biteStart = -1f;
                if (Time.time >= _phaseUntil)
                {
                    // ⚠ 距離檢查放在「這一輪站定站完」之後，不是每幀檢查就跳走：
                    //   被擊退推出圈外時，每幀檢查會讓牠立刻中斷觀望跑去 Approach，玩家只要持續開火
                    //   就能無限打斷牠的節奏（連 rounds 都減不掉）。站完再看，牠至少推進得了進度。
                    if (dist > StalkExitRange) { _phase = Phase.Approach; break; }   // 目標真的走遠了 → 跟上去
                    if (_roundsLeft <= 0) { _phase = Phase.Windup; _phaseUntil = Time.time + WindupSeconds; }
                    else BeginStep(pos, tp, dist);
                }
                break;

            // ── 觀望‧側移：往側邊挪一小段就停（落點進來時算好，不每幀追著目標修正）──
            case Phase.StalkStep:
            {
                // ⚠ **這裡刻意不做「貼身就中斷」的判定**（第一版有，是個死結）：
                //   咬到上限之後的「退開」也是一次 StalkStep，而退開的第一幀兩個框還貼著，
                //   中斷判定會把牠立刻踢回 StalkHold → 又開始咬 → 又退 → …… 玩家站著不動時
                //   模擬 60 秒只撲得出 1 次、貼身磨了 53 秒（2026-09-17 靜態模擬抓到）。
                //   一步最多 2.5 單位／2 秒，走完自然回 StalkHold，不需要這個捷徑。
                bool arrived = (_stepTarget - pos).sqrMagnitude <= ArriveDistSq;
                bool timeout = Time.time - _stepStart >= StepMaxSeconds;             // 被牆擋住時的保險
                if (arrived || timeout) { _roundsLeft--; EnterHold(); break; }
                act.MoveTowards(_stepTarget);
                break;
            }

            // ── 蓄力：站定（動畫自然回 idle），給玩家看得見的預告 ──
            case Phase.Windup:
                act.Stop();
                if (Time.time >= _phaseUntil) BeginCharge(act, pos, dist);
                break;

            // ── 衝刺：**全程追著目標當下的位置衝**（2026-09-17 改）──
            // 第一版是「起跑鎖定、全程直線」，實機上完全咬不到人：衝 3~5 單位要 0.3~0.5 秒，
            // 玩家在這段時間已經走掉 1.5~2.5 單位，狼撲到的永遠是玩家半秒前站的地方（作者實機回報）。
            // ⚠ **刻意不做「預判落點」**（不算玩家前方的攔截點）：那會變成「只要出手就一定咬到」，
            //    玩家的走位完全沒有意義。作者要的是「衝到你現在的位置、咬下去，咬不到算牠倒楣」。
            //    躲不躲得掉由 ChargeTimeSlack 決定，不是由演算法的聰明程度決定。
            case Phase.Charge:
            {
                bool arrived = Touching(ContactSlack);        // 真的碰到了（＝接觸傷害這一幀就會結算）
                bool timeout = Time.time >= _chargeDeadline;  // 依起跑距離算的時限（見 ChargeTimeSlack）
                bool blocked = false;
                if (Time.time >= _blockCheckAt)               // 每 BlockCheckInterval 結算一次累積位移
                {
                    float moved = (pos - _blockCheckPos).magnitude;
                    float expect = act.MoveSpeed * BlockCheckInterval;
                    blocked = expect > 0.0001f && moved < expect * StuckMoveRatio;
                    _blockCheckPos = pos;
                    _blockCheckAt = Time.time + BlockCheckInterval;
                }

                if (arrived || timeout || blocked)
                {
                    EndCharge(act);
                    act.Stop();
                    // **不管有沒有咬到，都把咬的動作播出來**（作者拍板）。用 NotifySkillCast 而不是靠
                    // MonsterController 的 AttackRange 自動判定——撲空時距離不夠，自動判定不會播，
                    // 畫面上就變成「衝過去然後呆站著」。這支 API 本來就是給「出手」用的。
                    if (ctx.Self != null) ctx.Self.NotifySkillCast();
                    if (DebugLog)
                    {
                        float ranDist = (pos - _chargeFromPos).magnitude;
                        string why = arrived ? "碰到了" : (timeout ? "時限到" : "撞到東西");
                        Debug.Log($"[Pounce] 衝刺結束：{why}　起跑距離={_chargeFromDist:F2}　實際跑了={ranDist:F2}" +
                                  $"　耗時={Time.time - _chargeStart:F2}s　結束時中心距離={dist:F2}" +
                                  $"　邊緣距離={EdgeDistance():F2}（≤{ContactSlack} 才算咬到）");
                    }
                    _phase = Phase.Recover;
                    _phaseUntil = Time.time + RecoverSeconds;
                    break;
                }
                act.MoveTowards(tp);   // ⭐ 每幀朝「目標當下的位置」，不是起跑時記下來的那個點
                break;
            }

            // ── 收招：站定喘一下＝玩家的輸出窗口（此時若貼身，動畫會自動變 attack＝咬）──
            case Phase.Recover:
                act.Stop();
                if (Time.time >= _phaseUntil) EnterStalk(true);   // 撲完一輪 → 重抽下一輪要試探幾次
                break;
        }
    }

    /// <summary>
    /// 進入觀望。<paramref name="reroll"/>＝true 才重抽「這一輪要交替幾次」——
    /// **只有「撲完一輪之後」與「第一次接觸目標」該重抽**。
    /// 被擊退推出圈外再走回來那種情況一律 false，否則玩家持續開火就能讓牠的觀望進度永遠歸零、撲不出來。
    /// </summary>
    void EnterStalk(bool reroll)
    {
        if (reroll || _roundsLeft < 0) _roundsLeft = Random.Range(StalkRoundsMin, StalkRoundsMax + 1);
        EnterHold();
    }

    void EnterHold()
    {
        _phase = Phase.StalkHold;
        _phaseUntil = Time.time + Random.Range(HoldSecondsMin, HoldSecondsMax);
    }

    /// <summary>
    /// 算出這一次側移的落點並開始移動。方向＝以「目標→自己」為 0° 轉 StepAngle（＝繞著目標的切線方向），
    /// 半徑夾在 StalkNear~StalkRange（太近會順便往外站），最後把**步長**夾成 StepDistMin~Max ——
    /// 綁距離不綁時間，CSV 的 Speed 調小只是走得慢，不會變成原地抽動（PROBLEMS F20）。
    /// </summary>
    void BeginStep(Vector2 pos, Vector2 targetPos, float dist)
    {
        if (Random.value < SideFlipChance) _side = -_side;   // 偶爾換邊＝左右來回試探

        Vector2 away = pos - targetPos;
        if (away.sqrMagnitude < 1e-4f) away = Vector2.right;
        away.Normalize();

        float want = Mathf.Clamp(dist, StalkNear, StalkRange);
        Vector2 dir = Rotate(away, Random.Range(StepAngleMin, StepAngleMax) * _side);
        Vector2 raw = targetPos + dir * want;

        Vector2 d = raw - pos;
        float step = Random.Range(StepDistMin, StepDistMax);
        _stepTarget = d.sqrMagnitude < 1e-4f ? pos : pos + d.normalized * Mathf.Min(d.magnitude, step);

        _stepStart = Time.time;
        _phase = Phase.StalkStep;
    }

    void BeginCharge(MonsterActuator act, Vector2 pos, float dist)
    {
        _chargeStart = Time.time;

        _savedSpeed = act.MoveSpeed;
        _savedAvoid = act.AvoidObstacles;
        act.MoveSpeed = _savedSpeed * ChargeSpeedMul;

        // 時限依「這一次要跑多遠」算，不是固定秒數（見 ChargeTimeSlack 的註解）。
        float chargeSpeed = Mathf.Max(0.01f, act.MoveSpeed);
        _chargeDeadline = Time.time + (dist / chargeSpeed) * ChargeTimeSlack + ChargeTimeBuffer;

        _blockCheckPos = pos;
        _blockCheckAt = Time.time + ChargeGraceSeconds + BlockCheckInterval;
        _chargeFromPos = pos;
        _chargeFromDist = dist;
        act.AvoidObstacles = false;   // 撲擊就是要直線；開著 A* 會繞路，看起來像在散步
        _charging = true;

        _phase = Phase.Charge;
    }

    void EndCharge(MonsterActuator act)
    {
        if (!_charging) return;
        _charging = false;
        if (act != null)
        {
            act.MoveSpeed = _savedSpeed;
            act.AvoidObstacles = _savedAvoid;
        }
    }

    /// <summary>
    /// 自己與目標的**碰撞框邊緣**距離是不是 ≤ slack（重疊也算）。
    /// 走 <c>Physics2D.Distance</c> 而不是中心距離：見 <see cref="ContactSlack"/> 的註解，
    /// 這是「咬的動作有播、血卻沒掉」的根因。**不要用 OverlapCircle**（PROBLEMS B7：專案全域
    /// `queriesStartInColliders=false` 會讓它漏抓重疊在查詢起點的目標）。
    /// </summary>
    bool Touching(float slack)
    {
        if (_selfCol == null || _targetCol == null) return false;
        ColliderDistance2D d = Physics2D.Distance(_selfCol, _targetCol);
        return d.isValid && (d.isOverlapped || d.distance <= slack);
    }

    /// <summary>兩個碰撞框的邊緣距離（重疊時為負或 0）。取不到回 -1。除錯 log 用。</summary>
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

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
