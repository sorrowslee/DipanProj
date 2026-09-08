using UnityEngine;

/// <summary>
/// 紅嫁衣女殭屍 Boss 戰鬥模組（第一個 boss 級 Brain，示範「一隻強怪＝一個 Brain 模組」）。
///
/// 行為：
///  ‧ 躲玩家——玩家靠近就往「反方向」逃（走 A* 尋徑會自動繞牆／家具，和其他怪一樣不做硬碰撞）。
///    「讓玩家追得上」不靠被卡住，而是把她的 MonsterData.Speed 調慢即可。
///  ‧ **逃之前先確認「真的有路可跑」**（見 <see cref="TryPickFleeTarget"/>）：以反方向為基準往兩側掃
///    ±150°，挑第一個「落點可走 ＋ 中間沒被擋 ＋ 不會反而更靠近玩家」的方向；四面都沒路就 Stop()，
///    velocity 歸零、實際位移為 0 → 動畫自然回 idle，不會演成原地踏步。
///  ‧ **逃跑節奏＝跑一段、喘一下**（`FleeBurstDistance` / `FleeRestSeconds`）：她不會一路跑到底，
///    每跑滿一段就站著喘一下，給玩家攻擊窗口，玩家才不必整場追著她跑。喘息期間照樣召喚。
///    ⚠ 這個節奏是**明確參數**，刻意不依賴「被打時擊退窗口會阻斷 Think」那個副作用（見 PROBLEMS F19）——
///    那個副作用的長度等於玩家射了多久，換武器就變，而且之後若給她加 InvincibleTimeMs 會無預警消失。
///  ‧ 定時召喚家人幽靈當追兵——召喚是一把 WeaponTable 的「召喚武器」，冷卻/名單/數量/同時上限
///    全走配方（RecipeTable 的 Mode=Summon 那組欄位），由 <see cref="MonsterWeaponUser"/> 結算。
///    召喚不綁逃跑狀態、只看冷卻，確保 boss 持續施壓（她速度慢、多半在逃，若綁「安全才召」會幾乎不召）。
///
/// 未來每隻 boss：新增一個 XxxBrain + 在 MonsterController 的 BrainType switch 掛上、CSV 指定 BrainType 即可。
/// 手感全在下方常數，要調就改這裡。
/// </summary>
public class RedBridalGownBrain : IMonsterBrain
{
    // ── 手感常數（要調就改這裡）──
    const float FleeRange = 4.0f;      // 玩家進入此距離 → 開始逃
    const float SafeRange = 6.5f;      // 玩家拉開超過此距離 → 停下（做遲滯，> FleeRange 避免臨界抖動）
    const float DetectionRange = 30f;  // Boss 感測玩家範圍（比一般怪大，整個房間都算得到）
    const float AwayLookahead = 2f;    // 逃跑目標點 = 自身沿逃跑方向外推這麼遠（純方向用，實速由 Actuator.MoveSpeed 決定）
    // 逃跑節奏（跑跑停停）：跑滿 Burst 就站著喘 Rest，給玩家攻擊時間。要她更難纏就拉長 Burst／縮短 Rest。
    // ⭐ Burst 用「跑多遠」不是「跑多久」：用秒數的話，CSV 的 Speed 一調小，每段跑的距離就跟著等比縮水
    //    （Speed 0.5 時 1.5 秒只挪 0.75 格＝不到一個身位），看起來像「偶爾動一下就停」。見 PROBLEMS F20。
    //    改成距離之後，調 Speed 只改變她跑得多快，不改變行為模式。
    const float FleeBurstDistance = 5f;      // 一段要跑多遠（世界單位；≈ Speed 3.5 下跑 1.5 秒的距離）
    const float FleeBurstMaxSeconds = 6f;    // 保險上限：速度極慢／一直被擋時，最多跑這麼久就先喘一次
    const float FleeRestSeconds  = 0.8f;     // 喘息多久（這段站著不動、播 idle，但召喚照常）

    // 逃跑方向掃描：以「玩家反方向」為 0°，往兩側逐步加大角度找可跑的路。
    // 排序＝偏離最小優先，所以正後方能跑就走正後方，被牆擋住才退而求其次沿牆逃。
    // 最大 ±150°（不含 180°：那是直接往玩家身上撞）。
    static readonly float[] FleeScanAngles =
        { 0f, 25f, -25f, 50f, -50f, 75f, -75f, 100f, -100f, 125f, -125f, 150f, -150f };
    const float SameDirBonus = 0.05f;  // 方向遲滯：上一幀選過的角度仍可行就沿用，避免左右方向每幀跳動

    bool _inited;
    bool _fleeing;
    bool _resting;                     // 逃跑節奏：true = 正在喘息（站著不動）
    float _phaseTimer;                 // 目前這一段（跑 or 喘）已經過多久
    float _burstDistance;              // 這一段「真的跑了多遠」（量實際位移，撞牆原地磨不算數）
    Vector2 _lastPos;                  // 上一幀位置（算實際位移用）
    bool _lastPosInit;
    float _lastFleeAngle;              // 上一幀採用的偏離角（相對「玩家反方向」）
    bool _hasLastFleeAngle;
    MonsterWeaponUser _weapon;

    void EnsureInit(in MonsterContext ctx)
    {
        if (_inited) return;
        _inited = true;
        if (ctx.Sensor != null) ctx.Sensor.DetectionRange = DetectionRange;
        _weapon = (ctx.Self != null) ? ctx.Self.WeaponUser : null;
    }

    public void Think(in MonsterContext ctx)
    {
        EnsureInit(ctx);

        MonsterActuator act = ctx.Actuator;
        Transform player = ctx.Player;

        // 召喚：玩家在場、冷卻好就召（不綁逃跑狀態）。冷卻/上限在 MonsterWeaponUser 內部結算。
        if (player != null && _weapon != null) _weapon.TryUse();

        if (player == null) { act.Stop(); ResetFleeState(); return; }

        Vector2 pos = act.transform.position;
        Vector2 playerPos = player.position;
        float dist = Vector2.Distance(pos, playerPos);

        // 遲滯：開始逃後要拉開到 SafeRange 才停；還沒逃則要近到 FleeRange 才開始逃。
        if (_fleeing) { if (dist >= SafeRange) _fleeing = false; }
        else          { if (dist <= FleeRange) { _fleeing = true; ResetFleeState(); } }   // 一開始逃＝從「跑」這一段起算

        if (!_fleeing) { act.Stop(); ResetFleeState(); return; }

        // 喘息段：站著不動（召喚在上面已經跑過，不受影響）。時間到就回到跑的那一段。
        if (_resting)
        {
            _phaseTimer += ctx.DeltaTime;
            if (_phaseTimer >= FleeRestSeconds) { _resting = false; _phaseTimer = 0f; }
            act.Stop();
            return;
        }

        Vector2 away = pos - playerPos;
        if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle.normalized;   // 重疊時隨機挑個方向
        away.Normalize();

        if (TryPickFleeTarget(pos, playerPos, away, dist, out Vector2 target))
        {
            act.MoveTowards(target);   // 有路才跑（走 A* 會自動繞牆）；追得上與否靠 Speed 調慢

            // 累積「真的跑了多遠」：量實際位移，所以被牆卡住原地磨的那幾幀不會吃掉她的逃跑額度。
            // 單幀增量夾在「這一幀最多可能跑多遠」之內——擊退／傳送造成的位移不是她自己跑的，不能算數。
            if (_lastPosInit)
            {
                float step = Vector2.Distance(pos, _lastPos);
                float cap = act.MoveSpeed * ctx.DeltaTime * 1.5f;
                if (step <= cap) _burstDistance += step;
            }
            _lastPos = pos; _lastPosInit = true;

            _phaseTimer += ctx.DeltaTime;
            if (_burstDistance >= FleeBurstDistance || _phaseTimer >= FleeBurstMaxSeconds)
            {
                _resting = true; _phaseTimer = 0f; _burstDistance = 0f; _lastPosInit = false;
            }
        }
        else
        {
            // 四面都沒路（被逼進死角）→ 站住不動。velocity 歸零 → 實際位移 0 → MonsterController
            // 的動畫判定（看實際位移）自然選 idle，不會出現「想跑但跑不動」的原地踏步。
            act.Stop();
            _hasLastFleeAngle = false;
            _lastPosInit = false;
        }
    }

    /// <summary>回到「沒在逃」的乾淨狀態：下次開始逃時從「跑」那一段重新起算，不會一進逃跑就先喘。</summary>
    void ResetFleeState()
    {
        _resting = false;
        _phaseTimer = 0f;
        _burstDistance = 0f;
        _lastPosInit = false;
        _hasLastFleeAngle = false;
    }

    /// <summary>
    /// 挑一個「真的跑得動」的逃跑目標點：以 <paramref name="away"/>（玩家反方向）為 0°，
    /// 依 <see cref="FleeScanAngles"/> 由小到大偏離掃描，取第一個同時滿足三個條件的方向：
    ///  ① 落點所在格可走（不是牆/水/家具）；② 從自身到落點沿線沒被擋（和 A* 同一份障礙圖）；
    ///  ③ 這個方向不會反而更靠近玩家（大角度時才可能違反）。
    /// 全都不行 → 回 false，呼叫端停下改 idle。
    /// </summary>
    bool TryPickFleeTarget(Vector2 pos, Vector2 playerPos, Vector2 away, float curDist, out Vector2 target)
    {
        target = Vector2.zero;
        var nav = MapNavGrid.Instance;

        // 沒有尋徑格（單場景測試），或她自己正嵌在不可走格裡（貼牆／卡進家具）：
        // 此時可走性資訊不可信（起點不可走會讓所有視線檢查一律 false，掃描永遠找不到路 → 永久 idle 卡死）。
        // → 退回舊行為，直接往反方向逃，交給 MonsterActuator 的脫困側滑處理。
        if (nav == null || !nav.Ready || !nav.IsWalkableWorld(pos))
        {
            target = pos + away * AwayLookahead;
            return true;
        }

        // 方向遲滯：上一幀選過的角度若仍可行就沿用，避免在兩個對稱方向間每幀跳動（看起來會左右抽動）。
        if (_hasLastFleeAngle && IsFleeDirOk(nav, pos, playerPos, away, _lastFleeAngle, curDist, out target))
            return true;

        for (int i = 0; i < FleeScanAngles.Length; i++)
        {
            if (IsFleeDirOk(nav, pos, playerPos, away, FleeScanAngles[i], curDist, out target))
            {
                _lastFleeAngle = FleeScanAngles[i];
                _hasLastFleeAngle = true;
                return true;
            }
        }
        return false;
    }

    bool IsFleeDirOk(MapNavGrid nav, Vector2 pos, Vector2 playerPos, Vector2 away,
                     float angleDeg, float curDist, out Vector2 cand)
    {
        cand = pos + Rotate(away, angleDeg) * AwayLookahead;
        if (!nav.IsWalkableWorld(cand)) return false;                        // 落點是牆/家具
        if (!nav.HasLineOfSight(pos, cand)) return false;                    // 中間被擋，這段路走不過去
        if (Vector2.Distance(cand, playerPos) < curDist - SameDirBonus) return false;  // 反而更靠近玩家
        return true;
    }

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
