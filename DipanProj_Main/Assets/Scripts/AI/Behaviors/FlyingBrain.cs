using UnityEngine;

/// <summary>
/// 飛行怪的基底模組（2026-10-11，遊戲中第一個飛行模組）。`BrainType=Flying` 直接用這支；
/// 蝙蝠類（會倒吊）繼承成 <see cref="BatBrain"/>（`BrainType=Bat`）。完整說明見 readme/BOSS_MODULE.md §12。
///
/// <para><b>飛行怎麼做的：地面點 ＋ 高度</b>（沿用跳躍踐踏 §9.6 的 <see cref="IAirborneVisual"/>）。
/// 俯視角的 Y 同時是「畫面高度」與「地面深度」，所以本 Brain 自己保管一個 <c>_ground</c>（影子所在的地面點）
/// 和一個 <c>_height</c>，每幀寫 <c>transform = _ground + (0, _height)</c>，並把高度報給
/// <c>MonsterController.AirborneVisualHeight</c> ⇒ 影子留在地上、Y 排序照地面點（作者 2026-10-11 拍板）。
/// 碰撞框跟著圖一起被抬高 ⇒ 玩家瞄準看得到的蝙蝠就打得到。</para>
///
/// <para><b>為什麼不走 MonsterActuator / A*</b>：那套拿 transform 去查格子，飛行怪的 transform 被抬高了，
/// 查到的是另一個地方。而且飛行的障礙規則不同——**只有牆擋，水/坑/家具都飛得過去**（作者拍板，
/// 見 <see cref="MapNavGrid.IsFlyableWorld"/>）。所以地面點的移動由本 Brain 自己推，以地面點查格子。</para>
///
/// <para><b>一輪行為</b>：
/// Rest（在家上空懸停；蝙蝠＝倒吊）→ 目標出現 → Wake（起飛演出）→ Engage（繞著目標盤旋）
/// → 繞到目標**左右兩側**且冷卻已過 → Attack（V 字俯衝：起手 → 撲過目標、命中幀咬一口 → 換 walk 往前飛＋拉高）
/// → 回 Engage。目標消失一段時間 → Return（飛回家）→ Rest。</para>
///
/// <para><b>只從左右兩側俯衝</b>：attack 圖只有「面右／面左」，從畫面正上方往正下方撲的話側面圖會很怪。</para>
///
/// <para><b>傷害只來自咬的那一幀</b>（同 MeleeChase §10.4）：接觸傷害關掉——否則牠在玩家頭上盤旋時，
/// 被抬高的身體框會一直擦到玩家扣血。命中判定走<b>碰撞框邊緣距離</b>（<c>Physics2D.Distance</c>，F22），
/// 過了才在目標身上開小圈 <see cref="ImpactDamageArea"/>（必中）。</para>
///
/// <para><b>俯衝落點在起手中途鎖定、之後不追蹤</b>（同 §9.3／§10.4 的取捨）：看到起手就側閃得掉。
/// 靜態模擬（readme/BOSS_MODULE.md §12.4）：站著不動 100%、隨意走動約 50%、看到起手 0.4 秒內側閃 0%。</para>
///
/// <para>⚠ 擊退：不開免疫特例（作者拍板），血蝠靠 CSV 把 `KnockbackThreshold` 填高。
/// 萬一真的被擊退，外力造成的位移會被吸收進地面點（見 <see cref="AbsorbExternalDisplacement"/>），不會被下一幀拉回去。
/// 俯衝那一小段則比照 §9.3 用 <c>SuppressKnockbackInterrupt</c>：撲出去了就一定撲完。</para>
/// </summary>
public class FlyingBrain : IMonsterBrain
{
    /// <summary>手感對不上時設 true：每次俯衝印「起點／鎖定點／命中邊緣距離／中了沒」。</summary>
    public static bool DebugLog = false;

    // ═══════════════ 手感常數（子類別可以覆寫 virtual 的那幾個）═══════════════

    // ── 高度：以「人的身高」為單位（CharacterWorldHeight 1.95，**不乘這隻怪的 Scale**）──
    // 「在敵人上方盤旋」是相對**目標**的描述，不是相對蝙蝠自己（蝙蝠的基準是坐姿，乘自己的身高會飛在人腰上）。
    /// <summary>盤旋高度（人身高的倍數）。1.25 ≈ 在人頭頂上方一點。</summary>
    protected virtual float CruiseHeightInHumans => 1.25f;
    const float BobAmplitude = 0.10f;      // 盤旋時高度上下起伏（世界單位）——振翅的浮動感
    const float BobHz = 0.7f;
    const float HeightRate = 6.0f;         // 高度往目標值靠近的最大速度（單位/秒，非俯衝時）

    // ── 盤旋 ──
    const float OrbitRadius = 2.6f;        // 繞目標的半徑（**體型 1 時**的世界單位，實際走 ScaledRadius，F29）
    const float OrbitRadiusJitter = 0.4f;  // 每隻隨機 ±，一群蝙蝠才不會疊在同一圈上
    const float ChaseSpeedMul = 1.6f;      // 追著盤旋點跑的速度上限 ＝ CSV Speed × 此值（目標在動時要追得上圈）
    const float SideHalfAngle = 35f;       // 「在目標左右兩側」的扇形半角（度）——只有在這裡面才能出手
    const float DiveRingMin = 0.6f, DiveRingMax = 1.5f;   // 離目標的距離 ∈ [半徑×min, 半徑×max] 才出手
    const float MinCooldown = 1.5f;        // 兩次俯衝的最短間隔（CSV AttackInterval 比這小時以這個為準）
    const float CooldownJitter = 0.6f;     // 冷卻隨機加 0~此值秒（一群一起醒時不會同步俯衝）

    // ── 俯衝（V 字）──
    /// <summary>attack 播放倍率（相對 CSV AnimFPS）。血蝠 25fps × 0.8 ⇒ 起手 0.5 秒、命中在 0.7 秒。</summary>
    protected virtual float AttackFpsMul => 0.8f;
    const float WindupRise = 0.25f;        // 起手時再往上提一點（世界單位）——蓄勢的預告
    const float BiteReachSlack = 0.5f;     // 命中幀：雙方碰撞框邊緣距離 ≤ 此值算咬到（與決策同一份幾何，F22）
    const float BiteHitRadius = 0.2f;      // 判定過了才在目標身上開的小圈（必中）
    const float ExitDistance = 2.6f;       // V 字後半段：咬完之後沿原方向再飛多遠（體型 1 時，走 ScaledRadius）
    const float ExitSeconds = 0.7f;        // 後半段花多久（同時把高度拉回盤旋高度）
    const float NoAttackWindup = 0.4f;     // 沒有 attack 圖的飛行怪：起手秒數（只懸停、不播動作）
    const float NoAttackDive = 0.2f;       //                        撲出秒數

    // ── 目標 ──
    const float LoseTargetSeconds = 1.5f;  // 目標從感測範圍消失多久才放棄
    const float AggroOnHitSeconds = 6f;
    const float LeashMul = 2f;             // 開打之後，目標離我（地面距離）超過 DetectionRange × 此值才算跑掉    // 被打之後，就算玩家在感測範圍外也追他這麼久（倒吊時被狙要會醒）
    const float ArriveDist = 0.15f;
    const float ReturnStuckSeconds = 1.5f; // 歸巢時這麼久沒更接近 ⇒ 放棄那個點、原地休息（家被牆圍住之類的保底）

    // ═══════════════ 狀態 ═══════════════
    protected enum Phase { Rest, Wake, Engage, Attack, Exit, Return }
    protected Phase _phase = Phase.Rest;

    protected MonsterController _self;
    protected MonsterAnimator _anim;
    protected Rigidbody2D _rb;
    HitReactionHandler _hitReact;
    Collider2D _selfCol;
    bool _configured;

    protected Vector2 _ground;          // 地面點（影子所在；＝「站在地上時 transform 會在的位置」）
    protected float _height;            // 目前離地高度（不含起伏）
    protected Vector2 _spawnGround;     // 出生點
    protected Vector2 _home;            // 休息的地方（基底＝出生點；蝙蝠會換成地圖上方的新棲點）
    Vector3 _lastWritten;
    float _bobPhase;
    float _speed;                       // CSV Speed
    float _orbitR;                      // 這隻的盤旋半徑（已含體型與隨機）
    int _orbitDir;                      // +1 逆時針 / -1 順時針
    float _orbitAng;
    float _nextAttackAt;
    float _lostTimer;
    float _lastHp = -1f;
    float _aggroUntil = -1f;
    float _wakeAt = -1f;                // 休息中發現目標後的預定醒來時間（-1＝還沒發現）
    bool _hitThisFrame;
    protected Transform _target;

    // 俯衝的資料（鎖定後不再改）
    float _atkStart;
    float _atkFps;
    int _hitFrame, _lockFrame, _diveFrame, _cutFrame;
    bool _atkHasAnim, _locked, _bitDone;
    Vector2 _diveFrom, _diveTo, _diveDir;
    float _diveFromH, _biteH;
    Collider2D _atkTargetCol;
    float _exitStart;
    Vector2 _exitFrom;
    float _exitFromH;
    float _returnBest, _returnBestAt;

    /// <summary>目前是不是在休息（倒吊／懸停在家）。給除錯與子類別用。</summary>
    public bool IsResting => _phase == Phase.Rest;

    // ═══════════════ 子類別的掛鉤 ═══════════════

    /// <summary>休息時的姿勢。基底＝振翅懸停（walk）；蝙蝠＝倒吊（hang）。</summary>
    protected virtual MonsterAnimator.State RestPose => MonsterAnimator.State.Walk;
    /// <summary>休息時的離地高度（世界單位）。基底＝盤旋高度。</summary>
    protected virtual float RestHeight => CruiseHeight;
    /// <summary>出生時是否已經在休息高度（基底 true：一生出來就在空中懸停）。</summary>
    protected virtual bool SpawnAtRestHeight => true;

    /// <summary>目標消失（放棄追擊）時決定要回哪裡休息。基底＝回出生點。</summary>
    protected virtual void OnTargetLost() { _home = _spawnGround; }

    /// <summary>
    /// 休息中發現目標後，**再休息多久才醒**（秒）。基底 0＝立刻醒；蝙蝠 ≈3 秒（讓玩家看得到倒掛的模樣，作者 2026-10-11）。
    /// 期間目標離開範圍就重新計時；**被打一律立刻醒**（不吃這段延遲）。每次發現時呼叫一次，可以回隨機值。
    /// </summary>
    protected virtual float WakeDelaySeconds() => 0f;

    /// <summary>從休息醒來的那一刻。基底沒有演出（直接進盤旋）。</summary>
    protected virtual void BeginWake() { }
    /// <summary>醒來演出的每一幀；回 true ＝ 演完，進盤旋。</summary>
    protected virtual bool TickWake(float dt) => true;

    /// <summary>attack 的「起手鎖定」幀（1-based）。預設＝命中幀往前 8 張（血蝠 15 → 7）。</summary>
    protected virtual int LockFrame(int hit) => Mathf.Max(1, hit - 8);
    /// <summary>attack 的「開始撲出」幀。預設＝命中幀往前 4 張（血蝠 15 → 11）。</summary>
    protected virtual int DiveFrame(int hit) => Mathf.Max(1, hit - 4);
    /// <summary>attack 播到哪一張就切成 walk 飛後半段。預設＝命中幀（咬到就立刻接 walk 往前飛＋拉高）。
    /// ⚠ 2026-10-11 初版是「命中幀＋2」並在命中後原地撐到那一張，作者實測回報「嘴巴張開的最後一幀停太久，像在半空中停住」——
    ///    飛行怪在空中**任何**停頓都會讀成「卡住」（地面怪站定揮完刀是自然的，空中不是）。要加回咬合頓點就把這個改大，
    ///    但記得命中後仍會繼續沿俯衝方向移動，不會再原地凍住。</summary>
    protected virtual int CutFrame(int hit, int count) => Mathf.Min(count, hit);

    // ═══════════════ 共用量 ═══════════════
    protected float HumanHeight => (_self != null && _self.CharacterWorldHeight > 0f) ? _self.CharacterWorldHeight : 1.95f;
    protected float CruiseHeight => CruiseHeightInHumans * HumanHeight;

    // ═══════════════ 主流程 ═══════════════
    public void Think(in MonsterContext ctx)
    {
        if (ctx.Self == null) return;
        if (!_configured) Configure(ctx);
        float dt = ctx.DeltaTime;

        AbsorbExternalDisplacement();
        UpdateTarget(ctx, dt);

        switch (_phase)
        {
            case Phase.Rest:   TickRest(dt); break;
            case Phase.Wake:   if (TickWake(dt)) EnterEngage(); break;
            case Phase.Engage: TickEngage(dt); break;
            case Phase.Attack: TickAttack(); break;
            case Phase.Exit:   TickExit(); break;
            case Phase.Return: TickReturn(dt); break;
        }

        ApplyTransform();
    }

    void Configure(in MonsterContext ctx)
    {
        _configured = true;
        _self = ctx.Self;
        _anim = _self.Anim;
        _rb = _self.GetComponent<Rigidbody2D>();
        _hitReact = _self.GetComponent<HitReactionHandler>();
        _selfCol = _self.GetComponent<Collider2D>();
        _speed = (ctx.Actuator != null && ctx.Actuator.MoveSpeed > 0.01f) ? ctx.Actuator.MoveSpeed : 3f;

        // 姿勢與攻擊動作全部由本 Brain 控（見 MonsterController.BrainPoseActive）
        _self.BrainPoseActive = true;
        _self.BrainControlsAttackPose = true;
        _self.FaceMovement = true;          // 盤旋時面向飛行方向；停著時面向目標

        // 傷害只來自咬的命中幀（理由見檔頭）
        var contact = _self.GetComponent<EnemyContactDamage>();
        if (contact != null) contact.enabled = false;

        // 每幀直接寫 transform ⇒ 內插要關，不然 rb 會用上一個物理步的位置往回補、兩邊互拉而抖（同 §9.6）
        if (_rb != null) { _rb.interpolation = RigidbodyInterpolation2D.None; _rb.velocity = Vector2.zero; }

        // ⭐ 出生點＝牠「在空中」的位置（倒掛／懸停在哪），地面點（影子）在它正下方 RestHeight 處。
        //   2026-10-11 作者把血蝠出生點畫在地圖最上排的牆上（＝「掛在上面那面牆」），而初版把出生點當地面點再往上抬
        //   ⇒ 整隻被抬出地圖頂端、鏡頭照不到；地面點又在牆裡 ⇒ 醒來也飛不出來。見 readme/PROBLEMS.md F33。
        Vector2 spawnPos = _self.transform.position;
        _height = SpawnAtRestHeight ? RestHeight : 0f;
        _spawnGround = GroundBelow(spawnPos, _height);
        _ground = _spawnGround;
        _home = _spawnGround;
        _bobPhase = Random.value * Mathf.PI * 2f;
        _orbitR = _self.ScaledRadius(OrbitRadius + Random.Range(-OrbitRadiusJitter, OrbitRadiusJitter));
        _orbitDir = Random.value < 0.5f ? 1 : -1;
        _nextAttackAt = Time.time + Random.Range(0.6f, 1.2f);   // 醒來先繞一下再出手
        _lastHp = _self.CurrentHealth;
        _lastWritten = _self.transform.position;
        _self.BrainPose = RestPose;
        _phase = Phase.Rest;
    }

    // ── 目標：取「敵對怪」與「玩家」的近者（同 §7.4）；被打過就算玩家在範圍外也追一陣子 ──
    void UpdateTarget(in MonsterContext ctx, float dt)
    {
        _hitThisFrame = _self.CurrentHealth < _lastHp - 0.001f;
        if (_hitThisFrame) _aggroUntil = Time.time + AggroOnHitSeconds;
        _lastHp = _self.CurrentHealth;

        // ⚠ 距離一律用**地面距離**（我的影子 ↔ 目標腳下），不用感測器的 transform 距離：
        //   飛行怪的 transform 被抬高了（倒吊 3.3、盤旋 2.4），感測器量到的是斜邊——
        //   DetectionRange 4 的血蝠盤旋時離玩家本來就 ≈3.5，會一直誤判「目標跑掉」（2026-10-11 作者把範圍砍半時發現）。
        //   而且「驚醒距離」與「放棄距離」要分開：醒來靠 DetectionRange，開打之後要跑出 × LeashMul 才放棄。
        float range = ctx.Sensor != null ? ctx.Sensor.DetectionRange : 10f;
        bool engaged = _phase == Phase.Engage || _phase == Phase.Attack || _phase == Phase.Exit || _phase == Phase.Wake;
        float reach = engaged ? range * LeashMul : range;

        Transform p = null;
        if (FactionRelations.AttacksPlayer(_self.Faction))
        {
            var pt = _self.PlayerTransform;
            if (pt != null && GroundDistance(pt) <= reach) p = pt;
        }
        Transform e = ctx.Enemy;
        if (e == null && engaged && _target != null && _target != _self.PlayerTransform && GroundDistance(_target) <= reach)
            e = _target;   // 敵對怪跑出感測範圍但還在牽制距離內 ⇒ 繼續追
        Transform t = Nearest(_ground, e, p);
        if (t == null && Time.time < _aggroUntil && FactionRelations.AttacksPlayer(_self.Faction))
            t = _self.PlayerTransform;
        if (t != null && !t.gameObject.activeInHierarchy) t = null;
        var mc = t != null ? t.GetComponent<MonsterController>() : null;
        if (mc != null && mc.IsDead) t = null;

        if (t != null) { _target = t; _lostTimer = 0f; }
        else
        {
            if (_phase == Phase.Rest) { _target = null; _wakeAt = -1f; }   // 等待醒來期間目標離開 ⇒ 重新計時
            _lostTimer += dt;
            // 俯衝中不放棄（撲出去了就要撲完，同 §9.3）；其餘狀態超時就放棄
            if (_lostTimer >= LoseTargetSeconds && _phase != Phase.Attack && _phase != Phase.Exit) _target = null;
        }

        if (_target == null && (_phase == Phase.Engage || _phase == Phase.Wake))
        {
            BeginReturn();
            if (DebugLog) Debug.Log($"[Flying] {_self.MonsterName} 失去目標 → 回 {_home}");
        }
        else if (_target != null && (_phase == Phase.Rest || _phase == Phase.Return))
        {
            // 休息中發現目標：先再休息 WakeDelaySeconds（蝙蝠＝倒掛著盯玩家 3 秒）；被打就不等。歸巢途中發現不等。
            if (_phase == Phase.Rest && !_hitThisFrame)
            {
                if (_wakeAt < 0f) _wakeAt = Time.time + Mathf.Max(0f, WakeDelaySeconds());
                if (Time.time < _wakeAt) return;
            }
            _wakeAt = -1f;
            _phase = Phase.Wake;
            _self.BrainPose = MonsterAnimator.State.Walk;
            BeginWake();
        }
    }

    // ── 休息 ──
    void TickRest(float dt)
    {
        _self.BrainPose = RestPose;
        _self.BrainFacing = 0;
        FlyTowards(_home, _speed, dt);
        _height = Mathf.MoveTowards(_height, RestHeight, HeightRate * dt);
    }

    // ── 歸巢 ──
    void TickReturn(float dt)
    {
        _self.BrainPose = MonsterAnimator.State.Walk;
        _self.BrainFacing = 0;
        bool arrived = FlyTowards(_home, _speed, dt);
        // 飛行途中維持盤旋高度，快到了才往休息高度靠（蝙蝠＝往上飛去倒吊）
        float distHome = Vector2.Distance(_ground, _home);
        if (distHome < _returnBest - 0.05f) { _returnBest = distHome; _returnBestAt = Time.time; }
        else if (Time.time - _returnBestAt > ReturnStuckSeconds) { _home = _ground; distHome = 0f; arrived = true; }
        float wantH = distHome < 1.5f ? RestHeight : CruiseHeight;
        _height = Mathf.MoveTowards(_height, wantH, HeightRate * dt);
        if (arrived && Mathf.Abs(_height - RestHeight) < 0.05f)
        {
            _phase = Phase.Rest;
            if (DebugLog) Debug.Log($"[Flying] {_self.MonsterName} 回到休息點");
        }
    }

    void BeginReturn()
    {
        OnTargetLost();
        _phase = Phase.Return;
        _self.BrainPose = MonsterAnimator.State.Walk;
        _self.BrainFacing = 0;
        _returnBest = float.MaxValue;
        _returnBestAt = Time.time;
    }

    // ── 盤旋 ──
    void EnterEngage()
    {
        _phase = Phase.Engage;
        _self.BrainPose = MonsterAnimator.State.Walk;
        _self.BrainFacing = 0;
        if (_target != null)
        {
            Vector2 c = OrbitCenter(_target);
            Vector2 d = _ground - c;
            _orbitAng = d.sqrMagnitude > 0.0001f ? Mathf.Atan2(d.y, d.x) : Random.value * Mathf.PI * 2f;
        }
    }

    void TickEngage(float dt)
    {
        if (_target == null) return;
        _self.BrainPose = MonsterAnimator.State.Walk;
        _self.BrainFacing = 0;

        Vector2 c = OrbitCenter(_target);
        _orbitAng += _orbitDir * (_speed / Mathf.Max(0.5f, _orbitR)) * dt;
        Vector2 want = c + new Vector2(Mathf.Cos(_orbitAng), Mathf.Sin(_orbitAng)) * _orbitR;
        if (!Flyable(want))
        {
            // 盤旋點撞牆 ⇒ 反向繞（一直撞就一直來回，至少不會鑽牆）
            _orbitDir = -_orbitDir;
            _orbitAng += _orbitDir * 0.3f;
        }
        FlyTowards(want, _speed * ChaseSpeedMul, dt);
        _height = Mathf.MoveTowards(_height, CruiseHeight, HeightRate * dt);

        if (Time.time >= _nextAttackAt && InDiveSlot(c)) BeginAttack();
    }

    /// <summary>在目標的左右兩側扇形內、距離合適 ⇒ 可以出手。</summary>
    bool InDiveSlot(Vector2 c)
    {
        Vector2 d = _ground - c;
        float dist = d.magnitude;
        if (dist < _orbitR * DiveRingMin || dist > _orbitR * DiveRingMax) return false;
        float s = Mathf.Abs(d.y) / Mathf.Max(0.0001f, dist);
        if (s > Mathf.Sin(SideHalfAngle * Mathf.Deg2Rad)) return false;
        // 要飛得過去：從這裡穿過目標再往前一段都不能有牆
        Vector2 dir = (c - _ground) / dist;
        return HasFlyLine(_ground, c + dir * _self.ScaledRadius(ExitDistance));
    }

    // ── 俯衝（V 字前半段）──
    void BeginAttack()
    {
        _phase = Phase.Attack;
        _atkStart = Time.time;
        _locked = false;
        _bitDone = false;
        _diveFrom = _ground;
        _diveFromH = _height;

        Vector2 c = OrbitCenter(_target);
        _self.BrainFacing = (c.x >= _ground.x) ? 1 : -1;   // 面向要撲的那一側，整段鎖住

        float fps = Mathf.Max(1f, _self.AnimFPS) * AttackFpsMul;
        _atkFps = fps;
        int count = _anim != null ? _anim.FrameCount(MonsterAnimator.State.Attack) : 0;
        _atkHasAnim = count > 0;
        if (_atkHasAnim)
        {
            _hitFrame = _self.AttackHitFrame > 0 ? Mathf.Clamp(_self.AttackHitFrame, 1, count)
                                                 : Mathf.Max(1, Mathf.RoundToInt(count * 0.6f));
            _lockFrame = Mathf.Clamp(LockFrame(_hitFrame), 1, _hitFrame);
            _diveFrame = Mathf.Clamp(DiveFrame(_hitFrame), _lockFrame, _hitFrame);
            _cutFrame  = Mathf.Clamp(CutFrame(_hitFrame, count), _hitFrame, count);
            _atkHasAnim = _anim.PlayOneShot(MonsterAnimator.State.Attack, 1, _cutFrame, AttackFpsMul);
        }

        // ⭐ 撲出去了就一定撲完（§9.3）：俯衝這段不被擊退跳過 Think
        _self.SuppressKnockbackInterrupt = true;

        // 冷卻從「出手」算（同 §10.2）
        float interval = Mathf.Max(MinCooldown, _self.AttackInterval);
        _nextAttackAt = Time.time + interval + Random.Range(0f, CooldownJitter);

        if (DebugLog) Debug.Log($"[Flying] {_self.MonsterName} 起手：from {_diveFrom} 目標 {_target?.name}  hit={_hitFrame} lock={_lockFrame} dive={_diveFrame} cut={_cutFrame} fps={fps:F1}");
    }

    float FrameTime(int frame1) => (frame1 - 1) / _atkFps;

    void TickAttack()
    {
        float t = Time.time - _atkStart;
        float tLock = _atkHasAnim ? FrameTime(_lockFrame) : NoAttackWindup * 0.6f;
        float tDive = _atkHasAnim ? FrameTime(_diveFrame) : NoAttackWindup;
        float tHit  = _atkHasAnim ? FrameTime(_hitFrame)  : NoAttackWindup + NoAttackDive;
        float tCut  = _atkHasAnim ? Mathf.Max(tHit, FrameTime(_cutFrame)) : tHit;

        if (!_locked && t >= tLock) LockDive();

        if (t < tDive)
        {
            // 起手：原地懸停、往上提一點（蓄勢）
            float u = tDive > 0.0001f ? Mathf.Clamp01(t / tDive) : 1f;
            _height = _diveFromH + WindupRise * Mathf.Sin(u * Mathf.PI * 0.5f);
            if (!_atkHasAnim) _self.BrainPose = MonsterAnimator.State.Walk;
            return;
        }

        if (!_locked) LockDive();

        // 撲出：地面點從起點直線撲到落點，高度降到咬的高度（ease-in：越撲越快）
        float span = Mathf.Max(0.0001f, tHit - tDive);
        float k = Mathf.Clamp01((t - tDive) / span);
        float e = k * k;
        float startH = _diveFromH + WindupRise;
        if (k < 1f)
        {
            _ground = Vector2.Lerp(_diveFrom, _diveTo, e);
            _height = Mathf.Lerp(startH, _biteH, e);
        }
        else
        {
            if (!_bitDone) { _ground = _diveTo; _height = _biteH; DoBite(); }
            // 命中幀之後**不原地停**（空中停頓會讀成卡住）：CutFrame > 命中幀時，沿俯衝方向繼續滑到切換那一刻
            if (t < tCut)
            {
                Vector2 slide = _diveTo + _diveDir * (t - tHit) * (_self.ScaledRadius(ExitDistance) / ExitSeconds) * 2f;
                if (Flyable(slide)) _ground = slide;
            }
            else EnterExit();
        }
    }

    void LockDive()
    {
        _locked = true;
        if (_target == null) { _diveTo = _diveFrom + new Vector2(_self.BrainFacing, 0f) * _orbitR; _biteH = CruiseHeight * 0.5f; _atkTargetCol = null; }
        else
        {
            TargetGeometry(_target, out Vector2 feet, out Vector2 body, out _atkTargetCol);
            // 落點（地面點）＝ 目標腳下對應的地面點；咬的高度＝讓「我的身體中心」對上「目標的身體中心」。
            Vector2 feetOff = SelfFeetOffset();
            Vector2 colOff = SelfColliderOffset();
            _diveTo = feet - feetOff;
            _biteH = Mathf.Max(0f, (body.y - colOff.y) - _diveTo.y);
        }
        Vector2 d = _diveTo - _diveFrom;
        _diveDir = d.sqrMagnitude > 0.0001f ? d.normalized : new Vector2(_self.BrainFacing, 0f);
        // 撲到的地方若是牆（目標貼牆站），退回最後一個飛得到的點
        if (!HasFlyLine(_diveFrom, _diveTo)) _diveTo = LastFlyable(_diveFrom, _diveTo);
        if (DebugLog) Debug.Log($"[Flying] {_self.MonsterName} 鎖定落點 {_diveTo}  咬高 {_biteH:F2}");
    }

    void DoBite()
    {
        _bitDone = true;
        if (_atkTargetCol == null || _selfCol == null) return;
        if (_atkTargetCol.gameObject == null || !_atkTargetCol.gameObject.activeInHierarchy) return;

        // 先把這一幀的位置寫上去，碰撞框才在「咬下去的位置」（Physics2D.Distance 讀 collider 的當下位置）
        ApplyTransform();
        Physics2D.SyncTransforms();
        ColliderDistance2D d = Physics2D.Distance(_selfCol, _atkTargetCol);
        bool hit = d.isValid && (d.isOverlapped || d.distance <= BiteReachSlack);
        if (hit) ImpactDamageArea.Spawn(_atkTargetCol.bounds.center, BiteHitRadius, _self.ContactDamage, _self.Faction);
        if (DebugLog) Debug.Log($"[Flying] {_self.MonsterName} {(hit ? "咬中" : "咬空")}：邊緣距離 {(d.isValid ? d.distance : -1f):F2}（門檻 {BiteReachSlack}）");
    }

    // ── V 字後半段：換 walk，順著原方向往前飛＋拉回盤旋高度 ──
    void EnterExit()
    {
        if (!_bitDone) DoBite();
        _phase = Phase.Exit;
        _exitStart = Time.time;
        _exitFrom = _ground;
        _exitFromH = _height;
        if (_anim != null) _anim.CancelOneShot();
        _self.BrainPose = MonsterAnimator.State.Walk;
        _self.SuppressKnockbackInterrupt = false;   // 一定要還（否則這隻怪一輩子不會被擊退打斷）
    }

    void TickExit()
    {
        float k = Mathf.Clamp01((Time.time - _exitStart) / ExitSeconds);
        // 水平：ease-out（剛咬完還帶著衝勢，越飛越慢）；高度：ease-out 更快（先往上拉、再平飛）＝ V 的右半邊
        float ex = 1f - (1f - k) * (1f - k);
        float eh = 1f - Mathf.Pow(1f - k, 3f);
        Vector2 want = _exitFrom + _diveDir * _self.ScaledRadius(ExitDistance) * ex;
        if (Flyable(want)) _ground = want;   // 撞牆就停在牆前、只拉高度
        _height = Mathf.Lerp(_exitFromH, CruiseHeight, eh);
        if (k >= 1f)
        {
            if (_target != null) EnterEngage();
            else BeginReturn();
        }
    }

    // ═══════════════ 位置 ═══════════════

    /// <summary>
    /// 被擊退等「外力」推動時，把那段位移吸收進地面點——不然下一幀 <see cref="ApplyTransform"/> 會把牠拉回原處。
    /// 俯衝中不吸收（那段位置是算出來的絕對插值，§9.8 第 2 點）。
    /// </summary>
    void AbsorbExternalDisplacement()
    {
        Vector3 now = _self.transform.position;
        Vector2 delta = (Vector2)(now - _lastWritten);
        if (delta.sqrMagnitude < 0.000001f) return;
        if (_phase == Phase.Attack) return;
        Vector2 g = _ground + delta;
        if (Flyable(g)) _ground = g;
    }

    void ApplyTransform()
    {
        bool bobbing = _phase == Phase.Engage || _phase == Phase.Return || _phase == Phase.Wake
                       || (_phase == Phase.Rest && RestPose != MonsterAnimator.State.Hang);
        float bob = bobbing ? BobAmplitude * Mathf.Sin(Time.time * BobHz * Mathf.PI * 2f + _bobPhase) : 0f;
        float h = Mathf.Max(0f, _height + bob);
        Vector3 p = _self.transform.position;
        p = new Vector3(_ground.x, _ground.y + h, p.z);
        _self.transform.position = p;
        _self.AirborneVisualHeight = h;
        _lastWritten = p;
        // 不是被擊退中就把速度歸零（本 Brain 不靠 velocity 移動；擊退中留給 HitReactionHandler）
        if (_rb != null && (_hitReact == null || !_hitReact.IsKnockedBack)) _rb.velocity = Vector2.zero;
    }

    /// <summary>
    /// 出生時的地面點：空中位置往下 <paramref name="height"/>。那裡若是牆（出生點畫得太靠牆），
    /// 往下找第一個飛得到的點（最多 8 單位）；找不到就用原本那點（下面 FlyTowards 會讓牠自己飛出來）。
    /// </summary>
    static Vector2 GroundBelow(Vector2 airPos, float height)
    {
        Vector2 g = airPos - new Vector2(0f, height);
        if (Flyable(g)) return g;
        for (float dy = 0.25f; dy <= 8f; dy += 0.25f)
        {
            Vector2 p = g - new Vector2(0f, dy);
            if (Flyable(p)) return p;
        }
        return g;
    }

    /// <summary>地面點往 goal 飛（只有牆擋；被擋就試著沿 X 或 Y 單軸滑）。回 true ＝ 到了。
    /// ⚠ 目前就站在牆裡（出生點畫在牆上、被推進牆）時**不擋**——否則每一步都是牆、永遠出不來（F33）。</summary>
    protected bool FlyTowards(Vector2 goal, float speed, float dt)
    {
        Vector2 d = goal - _ground;
        float dist = d.magnitude;
        if (dist <= ArriveDist) return true;
        float step = Mathf.Min(dist, speed * dt);
        Vector2 next = _ground + d / dist * step;
        if (Flyable(next) || !Flyable(_ground)) { _ground = next; return dist - step <= ArriveDist; }
        Vector2 nx = new Vector2(next.x, _ground.y), ny = new Vector2(_ground.x, next.y);
        if (Mathf.Abs(d.x) >= Mathf.Abs(d.y)) { if (Flyable(nx)) _ground = nx; else if (Flyable(ny)) _ground = ny; }
        else { if (Flyable(ny)) _ground = ny; else if (Flyable(nx)) _ground = nx; }
        return false;
    }

    protected static bool Flyable(Vector2 p) { var nav = MapNavGrid.Instance; return nav == null || nav.IsFlyableWorld(p); }
    protected static bool HasFlyLine(Vector2 a, Vector2 b) { var nav = MapNavGrid.Instance; return nav == null || nav.HasFlyLineOfSight(a, b); }

    static Vector2 LastFlyable(Vector2 from, Vector2 to)
    {
        Vector2 best = from;
        for (int i = 1; i <= 20; i++)
        {
            Vector2 p = Vector2.Lerp(from, to, i / 20f);
            if (!Flyable(p)) break;
            best = p;
        }
        return best;
    }

    // ═══════════════ 幾何 ═══════════════

    /// <summary>我的影子到目標腳下的地面距離。</summary>
    float GroundDistance(Transform t)
    {
        TargetGeometry(t, out Vector2 feet, out _, out _);
        return Vector2.Distance(_ground + SelfFeetOffset(), feet);
    }

    /// <summary>盤旋中心（地面點座標系）：讓我的影子繞著「目標的腳下」轉。</summary>
    Vector2 OrbitCenter(Transform t)
    {
        TargetGeometry(t, out Vector2 feet, out _, out _);
        return feet - SelfFeetOffset();
    }

    /// <summary>我的腳底相對 transform 的位移（站在地上時）。地面點 ＋ 這個 ＝ 影子／腳下的位置。</summary>
    Vector2 SelfFeetOffset() => _self.FeetWorldPos - (Vector2)_self.transform.position;

    /// <summary>我的碰撞框中心相對 transform 的位移。</summary>
    Vector2 SelfColliderOffset() => _selfCol != null ? (Vector2)_selfCol.bounds.center - (Vector2)_self.transform.position : Vector2.zero;

    /// <summary>目標的腳下（地面）、可見身體中心、碰撞框。玩家與怪各有正典 API（E14／G13），別用 transform。</summary>
    static void TargetGeometry(Transform t, out Vector2 feet, out Vector2 body, out Collider2D col)
    {
        col = t.GetComponent<Collider2D>();
        var pc = t.GetComponent<PlayerController>();
        if (pc != null) { feet = pc.FeetWorldPos; body = pc.BodyCenterWorldPos; return; }
        var mc = t.GetComponent<MonsterController>();
        if (mc != null)
        {
            float air = Mathf.Max(0f, mc.AirborneHeight);   // 目標自己也在空中（另一隻飛行怪／跳躍中）⇒ 腳下要扣回地面
            feet = mc.FeetWorldPos - new Vector2(0f, air);
            body = mc.BodyCenterWorldPos;
            return;
        }
        feet = t.position;
        body = col != null ? (Vector2)col.bounds.center : (Vector2)t.position;
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
