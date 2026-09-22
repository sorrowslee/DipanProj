using System.Collections.Generic;
using UnityEngine;

public class MonsterController : MonoBehaviour, IDamageable, ICombatModifiers, IAirborneVisual
{
    private MonsterSensor _sensor;
    // 所有怪一律靠 A* 導航、碰撞框全設 trigger（不做硬碰撞、不會卡在牆/家具上）。
    private BoxCollider2D _bodyCol;
    private BoxCollider2D _feetCol;
    private MonsterActuator _actuator;
    private IMonsterBrain _brain;
    private HitReactionHandler _hitReaction;

    private Animator _animator;          // 舊路線後備：若怪用自帶 Unity Animator 的 prefab 才有
    private MonsterAnimator _monAnim;    // 路線 B：程式逐格動畫（量產怪用這個，零 prefab/Animator）
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rb;

    public string MonsterName;
    public float MaxHealth = 50f;
    public float HitboxPadding = 0.2f;
    [Tooltip("來源序列圖角色面朝右 = true（AutoSprite 輸出，與 PlayerController 同）；面朝左 = false。決定 flipX 對應。")]
    public bool SpriteSourceFacesRight = true;
    public float AnimFPS = 8f;           // 程式動畫播放幀率（CSV: AnimFPS，留空 = 8）
    public float AttackRange = 1.3f;     // 進入此距離且有 attack 圖 → 播攻擊動畫（略大於 ChaseBrain.StopDistance）

    // 逐動作顯示倍率（CSV: IdleScale/WalkScale/AttackScale；0＝留空＝走自動高度對齊）。由 Initialize 從 MonsterData 帶入。
    [HideInInspector] public float IdleScale, WalkScale, AttackScale, JumpScale;
    [Tooltip("施放技能（如召喚）後，attack 動畫維持播放的秒數（讓遠距離施法也看得到出手動作）")]
    public float SkillCastAnimSeconds = 0.6f;
    [Tooltip("角色站立顯示高度（世界單位），與主角 PlayerController.CharacterWorldHeight 同一套邏輯：" +
             "依 idle 可見高度自動換算，讓「同一張圖丟主角或怪物資料夾都一樣大」。要某隻怪特別大/小，再用 CSV 的 Scale 當倍率。" +
             "<=0 = 關閉自動換算、沿用原生像素大小。")]
    public float CharacterWorldHeight = 1.95f;
    private float _currentHealth;
    private bool _isDead = false;

    [Header("Animation")]
    [Tooltip("走路/發呆判定門檻（世界單位/秒）：以「實際位移速度」判斷是否在移動，超過才播走路、否則發呆。" +
             "改看實際位移（非指令速度 rb.velocity）→ 逃跑被牆/角落卡住而原地不動時不會誤播走路（原地踏步）。")]
    public float MoveAnimThreshold = 0.12f;
    private Vector2 _lastVisualPos;      // 上一幀量測位置（算實際位移速度用）
    private bool _lastVisualPosInit;     // 是否已初始化 _lastVisualPos
    private float _visualSpeedEma;       // 實際位移速度的指數平滑（避免單幀抖動造成走路/發呆閃爍）

    [Header("Hit Reaction")]
    public float InvincibleTimeMs = 0f;
    public float KnockbackThreshold = 0f;
    public float KnockbackPercent = 0f;

    [Header("Combat")]
    public float ContactDamage = 10f;            // 碰到玩家造成的傷害（CSV: MonsterData.ContactDamage）
    public float AttackInterval = 0.5f;          // 接觸攻擊間隔秒＝攻速（CSV: AttackInterval）
    public float DamageReductionPercent = 0f;    // 受擊減傷 %（掛勾；目前 CSV 預設 0，之後接減傷/抗性）

    [Tooltip("揮舞型近戰的命中幀：attack 序列圖的第幾張是「武器揮到位」（CSV: AttackHitFrame）。留空/0 ＝ 張數×0.7 粗估。**一定要逐怪量**，比例靠不住")]
    public int AttackHitFrame = 0;

    [Header("Leap Slam / 跳躍踐踏")]
    [Tooltip("落地踐踏的傷害（CSV: LeapDamage）。留空/0 ＝ 退回 ContactDamage 的 2 倍（見 LeapSlamBrain）。只有 BrainType=LeapSlam 會用到")]
    public float LeapDamage = 0f;
    [Tooltip("落地踐踏的殺傷半徑（世界單位，CSV: LeapRadius）。留空/0 ＝ 退回 1.6。裂痕的視覺大小也吃它")]
    public float LeapRadius = 0f;

    [Header("Archer / 射手型")]
    [Tooltip("射手型的放彈幀：attack 序列圖的第幾張是「武器已舉定、可以射了」（CSV: ReleaseFrame）。留空/0 ＝ 退回 14。**幀號即事件**，改 AnimFPS 時機自動跟著對。只有 BrainType=Archer 會用到")]
    public int ReleaseFrame = 0;

    [Header("Suicide Bomb / 自爆型")]
    [Tooltip("自爆傷害（CSV: BombDamage）。留空/0 ＝ 退回 ContactDamage 的 3 倍。只有 BrainType=SuicideBomb 會用到")]
    public float BombDamage = 0f;
    [Tooltip("自爆殺傷半徑（世界單位，CSV: BombRadius）。留空/0 ＝ 退回 1.8。⚠ 實際殺傷 ＝ 此值 ＋ 目標碰撞框半徑（玩家約 0.5）")]
    public float BombRadius = 0f;
    [Tooltip("引信秒數（CSV: BombFuse）：貼近後站定閃爍幾秒才爆。留空/0 ＝ 退回 0.6。進了引信就一定會爆")]
    public float BombFuse = 0f;

    [Header("Loot / 掉落")]
    [Tooltip("掉落表 ID（CSV: DropTableId）→ DropTable.csv。留空/0 ＝ 完全不掉寶。既有的怪一律填 1 ＝ 原本寫死的那組掉落")]
    public int DropTableId = 0;

    [Header("Move Trail / 移動拖尾")]
    [Tooltip("移動時在身後種的特效（CSV: MoveTrailFx）。格式 vfxId:大小倍率:每秒幾個，多層用 | 分隔。留空 ＝ 不掛")]
    public string MoveTrailFx = "";

    [Header("Weapon / Skill")]
    [Tooltip("這隻怪使用的武器 = WeaponTable 的 ID（CSV: MonsterData.Weapon 填數字）。Contact/空 = 只近戰接觸傷害、不掛武器。")]
    public int WeaponId = -1;
    // 怪物用武器的統一入口（召喚等技能走這裡；投射武器 Phase 2）。boss 級 Brain 透過 ctx.Self.WeaponUser 施放。
    public MonsterWeaponUser WeaponUser { get; private set; }
    private float _skillCastAnimUntil;   // < Time.time 前都播 attack 動畫（施放技能觸發，見 NotifySkillCast）

    /// <summary>程式逐格動畫器（route B 才有；舊 prefab 怪為 null）。Brain 要自己控動畫（如跳躍的 one-shot）時用。</summary>
    public MonsterAnimator Anim => _monAnim;

    /// <summary>這隻怪的體型倍率（＝ `MonsterData.csv` 的 `Scale` 欄，實作上就是 `transform.localScale`）。</summary>
    public float BodyScale => Mathf.Abs(transform.localScale.y);

    /// <summary>
    /// 把「以體型 1 為基準寫在 CSV／常數裡的世界單位半徑」換算成這隻怪**實際**的半徑。
    ///
    /// <para>⭐⭐ <b>怪的所有 AOE 半徑一律走這支</b>（爆炸、踐踏、震波、未來任何範圍技）。
    /// 直接拿 CSV 的數字當世界單位是 PROBLEMS <b>F27</b> 的第一條死因：
    /// <b>寫死的世界單位在怪放大時不會跟著長</b>——怪的碰撞框、圖、連武器都 ×`Scale` 變大了，
    /// 只有那個常數沒有 ⇒ 相對縮水，大到某個體型就完全打不到人，而且**沒有任何錯誤訊息**。</para>
    ///
    /// <para>實例：`ZhaYu_Bomb` 的 `BombRadius` 1.8 固定不變，但引信是用**碰撞框的邊緣距離**判定的、
    /// 會隨體型長大 ⇒ `Scale` 1 時「引信一點著就在殺傷圈內」，放大到 1.5 之後引信觸發距離變成 2.43、
    /// 殺傷只到 2.30 ⇒ **炸了卻炸不到玩家**（作者 2026-09-22 回報）。</para>
    ///
    /// <para>⚠ 所以 CSV 的半徑欄（`BombRadius`／`LeapRadius`）語意是「**體型 1 時的半徑**」，
    /// 不是最終世界單位。填表時照體型 1 去想就好，放大縮小由這裡處理。完整記錄見 readme/PROBLEMS.md <b>F29</b>。</para>
    /// </summary>
    public float ScaledRadius(float baseRadius) => baseRadius * BodyScale;


    // ── 騰空（IAirborneVisual）──
    // 由 LeapSlamBrain 在跳躍期間每幀寫入「這一幀視覺被抬高了多少」，落地歸 0。
    // BlobShadow 與 YSortByFeet 會把這段高度扣回地面（影子留在地上、排序不會誤判）。見 IAirborneVisual。
    // ⚠ 這是**純視覺**的高度：碰撞、傷害、尋徑全部照 transform 走，不因為「跳起來」而改變。
    [HideInInspector] public float AirborneVisualHeight;
    public float AirborneHeight => AirborneVisualHeight;

    // ── 不可中斷的演出（2026-09-18）──
    // 平常怪被打時的擊退窗口會**整段跳過 Think()**（見 readme/PROBLEMS.md F19），這對絕大多數 Brain 是對的
    // （被打飛的時候本來就不該還在做決策），但對**每幀自己算位移的演出**是致命的：
    // 跳躍踐踏的騰空只有 0.46 秒，玩家連射時擊退窗口首尾相連 ⇒ 那幾幀完全沒有位移 ⇒
    // 畫面上就是「蹲下、動畫演完、人還在原地」（作者 2026-09-18 實機回報「常常準備跳但沒跳出去」）。
    // Brain 在這種演出開始時把它設 true、結束設 false，期間 Think() 照跑。
    // ⚠ 這只讓「決策」不被跳過；擊退的**位移**還是照送（rb.velocity），要不要壓制由 Brain 自己決定
    //   （LeapSlamBrain 每幀直接寫 transform，等於自然壓制）。
    [HideInInspector] public bool SuppressKnockbackInterrupt;

    // ── Brain 接管攻擊姿勢（2026-09-18）──
    // 預設（false）＝既有行為：`HandleVisuals` 只要「目標距離 ≤ AttackRange 且有 attack 幀」就自動播攻擊動畫。
    // 那套的前提是「攻擊動畫純粹是貼身時的裝飾」，所以怪可以**一邊舉著劍一邊追著玩家跑**——
    // 作者 2026-09-18 實機回報「他已經舉起劍了，我躲閃，他竟然還能移動並保持舉劍的動作，太詭異」。
    // Brain 把這個設 true ＝「攻擊姿勢由我用 one-shot 全權控制」，`HandleVisuals` 不再自動判定，
    // 於是「進入攻擊動作 → 站定把動作做完 → 才能再移動」成立（見 MeleeChaseBrain）。
    // ⚠ 設了 true 就**一定**要自己播 attack，否則這隻怪永遠不會有攻擊動畫。
    // NotifySkillCast（召喚等技能的出手動作）不受影響，那條路仍然通。
    [HideInInspector] public bool BrainControlsAttackPose;

    /// <summary>怪物成功施放一次技能（召喚等）時由 MonsterWeaponUser 呼叫：讓 attack 動畫演一小段，
    /// 即使怪離玩家很遠（如紅嫁衣邊逃邊召）也看得到出手動作。</summary>
    public void NotifySkillCast() => _skillCastAnimUntil = Time.time + SkillCastAnimSeconds;

    private float _pantFrom, _pantUntil;   // 這個區間內播 pant 動畫（大絕後的喘息破綻，見 PlayPant）

    /// <summary>放完大絕後的「喘息」破綻：<paramref name="delay"/> 秒後開始，播 <paramref name="seconds"/> 秒
    /// pant 動畫（優先度壓過攻擊／走路／發呆）。<paramref name="delay"/> 是留給「出手動作」的時間——
    /// pant 壓得過 attack，不延後的話施法動作會被當場蓋掉。
    ///
    /// <para>⚠ <b>刻意做成「呼叫一次就排程好」而不是每幀輪詢</b>：怪被打時的擊退窗口會**整段跳過 Think()**
    /// （見 readme/PROBLEMS.md F19），Brain 那邊「時間到了再切 pant」的寫法會在玩家猛打時延後甚至不觸發。
    /// 起訖時間交給本元件保管，動畫每幀自己判讀，就跟 Brain 有沒有被跳過完全無關。</para>
    ///
    /// <para><b>本方法只管動畫</b>——要不要同時停止行動由 Brain 決定（紅嫁衣是完全停擺）。
    /// 沒有 pant 圖的怪會自動退回 idle（見 MonsterAnimator 的退回規則），所以任何怪都能安全呼叫。</para></summary>
    public void PlayPant(float seconds, float delay = 0f)
    {
        _pantFrom = Time.time + Mathf.Max(0f, delay);
        _pantUntil = _pantFrom + Mathf.Max(0f, seconds);
    }

    /// <summary>目前是否在喘息破綻中（含尚未開始的延遲期間＝false）。</summary>
    public bool IsPanting => Time.time >= _pantFrom && Time.time < _pantUntil;

    // ── 可見身體的幾何（給「要對準腳下 / 要罩住身體」的特效用）──
    // ⚠⚠ **route B 怪物的 pivot 是畫布中心（0.5），不是腳底**——與玩家相反（玩家的 pivot 在腳底，見 BLOODLINE.md §2）。
    //    腳底對齊那套（MonsterSpriteLibrary.GetFrames）只把「各動作」拉到同一條腳底線，
    //    **基準幀的 pivot 刻意維持 0.5**，好讓角色的絕對位置與碰撞框完全不動。
    //    後果：**任何直接畫在 transform.position 的地面特效都會出現在角色半身高的位置**（＝胸口）。
    //    狂族皇家衛士實測：idle 可見底邊在畫布下緣往上 28px、pivot 在 128px ⇒ 差 100px，
    //    換算 PPU 102、再乘 Scale 1.3 ＝ **腳底在 transform 下方 1.27 世界單位**（可見高才 2.54）。
    //    作者 2026-09-18 回報「龜裂在怪物胸口播放而不是腳底」，根因就是這個。
    //    這是玩家版 PROBLEMS **E14** 的怪物版：**別拿 transform.position 當腳底，用下面這三個**。
    float _feetLocalY;        // 腳底相對 transform 的本地 Y（未乘 localScale）
    float _visibleLocalH;     // 可見身體的本地高度（未乘 localScale）
    bool  _geomReady;         // FitVisibleBoxCollider 算過了沒（走 AutoAdjustCollider 後備時為 false）

    /// <summary>可見身體高度（世界單位，已乘體型）。特效要「蓋住這隻怪」時用它當基準。</summary>
    public float VisibleBodyHeight
    {
        get
        {
            float scale = Mathf.Abs(transform.localScale.y);
            if (_geomReady && _visibleLocalH > 0.01f) return _visibleLocalH * scale;
            var col = GetComponent<Collider2D>();          // 後備：用碰撞框（含 HitboxPadding，略大一點）
            return col != null ? col.bounds.size.y : CharacterWorldHeight * scale;
        }
    }

    /// <summary>
    /// 角色**腳下站的位置**（可見身體的底部中心）。裂地、影子、落點標記這類「畫在地上」的東西一律對準這裡。
    /// <para>⚠ 不要用 <c>transform.position</c>——那是畫布中心，會讓特效浮在半身高的位置（見上方註解）。</para>
    /// </summary>
    public Vector2 FeetWorldPos
    {
        get
        {
            Vector3 p = transform.position;
            float scale = Mathf.Abs(transform.localScale.y);
            if (_geomReady) return new Vector2(p.x, p.y + _feetLocalY * scale);
            var col = GetComponent<Collider2D>();          // 後備：碰撞框底邊（再把 padding 的一半補回去）
            if (col != null) return new Vector2(p.x, col.bounds.min.y + HitboxPadding * 0.5f);
            return new Vector2(p.x, p.y);
        }
    }

    /// <summary>可見身體的中心。要「罩住身體」的光環/煙霧對齊這裡。</summary>
    public Vector2 BodyCenterWorldPos
    {
        get
        {
            Vector2 f = FeetWorldPos;
            return new Vector2(f.x, f.y + VisibleBodyHeight * 0.5f);
        }
    }

    [Header("Faction")]
    [Tooltip("陣營：Enemy=一般敵怪/boss/其召喚物(追玩家)；PlayerAlly=玩家召喚的協戰怪(追敵怪)；Neutral=中立 NPC(不打人不被打)。由 MonsterSpawner / NpcSpawner 設定。")]
    public MonsterFaction Faction = MonsterFaction.Enemy;

    [Tooltip("移動時面向「移動方向」而不是玩家（NPC 巡邏用：往右走就面右）。停下時仍面向感測範圍內的玩家。")]
    public bool FaceMovement = false;
    public bool IsDead => _isDead;
    public float CurrentHealth => _currentHealth;                       // 目前血量（給 boss Brain 判階段用）
    public float HealthFraction => MaxHealth > 0f ? _currentHealth / MaxHealth : 0f;  // 血量比例 0~1

    [Header("Speech / 說話")]
    // 遊戲中頭上會講的話（CSV: 句子1~句子4）；由 Initialize 從 MonsterData 帶入，有填才在 Start 掛 MonsterSpeech。
    [HideInInspector] public List<MonsterSpeechLine> SpeechLines;
    // 是否已發現玩家（發現後才開口說話）。一旦發現就維持 true（黏著；小房間內玩家不會真的甩開）。
    public bool IsAwareOfPlayer { get; private set; }
    // 是否為 boss（劇情要角）。由 Initialize 的 BrainType 判定；MonsterSpeech 用它把說話頻率加倍、幾乎必說。
    public bool IsBoss { get; private set; }

    // 全場活著的怪物登記表：接觸傷害與友軍找目標都靠它 + Physics2D.Distance，**不用 OverlapCircle**
    // ——專案全域 queriesStartInColliders=false，OverlapCircle 會漏抓「重疊在查詢起點」的貼身目標（見 PROBLEMS）。
    public static readonly List<MonsterController> Active = new List<MonsterController>();
    void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    void OnDisable() { Active.Remove(this); }

    // 玩家 transform 快取（友軍跟隨用；玩家是常駐物件，找一次即可）。
    Transform _playerCache;
    public Transform PlayerTransform
    {
        get
        {
            if (_playerCache == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _playerCache = p.transform;
            }
            return _playerCache;
        }
    }

    /// <summary>換掉決策機（MonsterSpawner 在 PlayerAlly 陣營時改掛 AllyBrain）。</summary>
    public void SetBrain(IMonsterBrain b) { if (b != null) _brain = b; }

    [Header("Death")]
    [Tooltip("死亡時播的特效 = VfxTable 的 ID；0 = 不播。檔名/張數/FPS 都在 VfxTable 那一列設定。")]
    public int DeathVfxId = 7;                    // VfxTable ID 7 = 怪物死亡（暫借爆炸圖）
    [Tooltip("死亡時把此旗標設為 true（給觸發鏈 requireFlag 用，例：殺了家人→killedFamily→新娘生氣分支）。" +
             "由地圖出生點的「死亡觸發旗標」欄填入、每個擺放各自設定；空＝不寫。")]
    public string DeathFlag;                       // 由 MonsterSpawner 從出生點 trigger 的 deathFlag 參數設定
    static VfxManager _vfx;                       // 全場唯一，快取共用（仿 DestructibleObject）

    [Header("Run / 關卡進度")]
    [Tooltip("本張地圖唯一的出生點 key（由 MapLoader 依 monsterSpawn 區域 id + 格座標產生）。有值＝一次性地圖出生的怪，" +
             "死亡時記進 RunProgress『已清』（本趟不再重生）；空＝召喚物、或「重複產生」的出生點，不記進度。")]
    public string SpawnKey;

    [Tooltip("死亡時掉寶（銅錢＋機率藥）。由 MonsterSpawner 設定：一次性出生點＝true；重複產生的出生點＝true（但不記進度）；" +
             "召喚物＝false（防無限刷）。")]
    public bool DropsLoot;

    [Header("Loot / 暫定掉寶（正式掉寶公式之後換；數值可調）")]
    [Tooltip("必掉金錢（銅錢）數量下限。")] public int lootMoneyMin = 1;
    [Tooltip("必掉金錢（銅錢）數量上限。")] public int lootMoneyMax = 5;
    [Tooltip("機率掉一瓶藥（小回血 201 / 小回魔 202，各半）。0~1。")]
    [Range(0f, 1f)] public float lootPotionChance = 0.35f;

    void Start()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();
        
        if (_brain == null)
        {
            _currentHealth = MaxHealth;
            _sensor = gameObject.AddComponent<MonsterSensor>();
            _actuator = gameObject.AddComponent<MonsterActuator>();
            _brain = new ChaseBrain();
            AutoAdjustCollider();
        }

        _hitReaction = gameObject.AddComponent<HitReactionHandler>();
        _hitReaction.Configure(_spriteRenderer, _rb,
            InvincibleTimeMs, KnockbackThreshold, KnockbackPercent);

        // 接觸傷害：碰到玩家就扣血（幾何重疊判定，見 EnemyContactDamage）。ContactDamage 由 Initialize 從 CSV 設定，
        // 手動放置的怪用預設值。Initialize 在 Start 之前由 MonsterSpawner 呼叫，故此時值已就緒。
        var contact = gameObject.AddComponent<EnemyContactDamage>();
        contact.Configure(ContactDamage, Faction, AttackInterval);

        // 腳下影子（見 readme/SHADOW.md）
        if (GetComponent<BlobShadow>() == null) gameObject.AddComponent<BlobShadow>();

        // 常駐體光：暗地圖裡讓輪廓浮出來（見 MonsterSpawner.AttachMonsterGlow ／ readme/ATMOSPHERE.md）。
        // ⚠ **這是保險，正規入口在 MonsterSpawner.SpawnMonster**。兩邊都叫、方法自己判重不會掛兩顆。
        //    為什麼要保險：2026-09-04 加體光後，新娘房的 boss 與她的召喚物實機上沒有光，
        //    而走查所有已知生成路徑（地圖出生點／重生器／召喚）都該經過 SpawnMonster——既然對不上，
        //    就在「每隻怪一定會跑到」的這裡補一道，任何漏網的生成路徑都會被接住。
        // ⚠ **NPC 要排除**：NPC 沿用整套怪物地基（NpcSpawner 也 AddComponent<MonsterController>，
        //    連 BlobShadow/YSortByFeet 都是靠上面那幾行掛的），不擋掉的話 NPC 會跟著發鬼光。
        if (GetComponent<NpcAgent>() == null)
            MonsterSpawner.AttachMonsterGlow(gameObject, transform.localScale.y);

        // 依腳底 Y 動態排序，和地上物一起正確交錯遮蔽（見 MapDepthSort / YSortByFeet）。
        if (GetComponent<YSortByFeet>() == null) gameObject.AddComponent<YSortByFeet>();

        // 走路動畫速度跟著實際移動速度（避免腳滑；見 readme/CHARACTER_SETUP.md）。
        // ReferenceSpeed 設成這隻怪的正常移動速度 → 正常追擊時 1×，被放慢時動畫自動變慢。
        if (GetComponent<AnimatorSpeedByVelocity>() == null)
        {
            var asv = gameObject.AddComponent<AnimatorSpeedByVelocity>();
            var act = GetComponent<MonsterActuator>();
            if (act != null) asv.ReferenceSpeed = act.MoveSpeed;
        }

        // 遊戲中說話（見 MonsterSpeech / MonsterSpeechPanel）：CSV 有填句子才掛；發現玩家後才會開口。
        if (SpeechLines != null && SpeechLines.Count > 0 && GetComponent<MonsterSpeech>() == null)
            gameObject.AddComponent<MonsterSpeech>().Configure(this);

        // 路線 B：程式逐格動畫——依怪名載 idle/walk/attack 並播放（見 MonsterAnimator / MonsterSpriteLibrary）。
        // 只在「沒有 Unity Animator ＋ 有怪名」時啟用，避免和舊 prefab 的 Animator 同時搶著換 sprite：
        //   ‧ 量產怪（程式建、無 Animator、Initialize 給了怪名）→ 走這條（route B）。
        //   ‧ 舊 prefab 怪（自帶 Animator）→ 交給 Animator（下方 HandleVisuals 的 isMoving 後備）。
        if (_animator == null && !string.IsNullOrEmpty(MonsterName))
        {
            _monAnim = GetComponent<MonsterAnimator>();
            if (_monAnim == null) _monAnim = gameObject.AddComponent<MonsterAnimator>();
            var actForFps = GetComponent<MonsterActuator>();
            float refSpeed = actForFps != null ? actForFps.MoveSpeed : 3f;

            var lib = MonsterSpriteLibrary.Instance;

            // 顯示大小：與主角完全同一套邏輯——依 idle(取不到改 walk) 可見高度，把幀放大到 CharacterWorldHeight 世界高。
            // 所以「同一張圖丟主角或怪物資料夾，顯示就一樣大」。每隻怪特別的大小差異再用 CSV Scale（transform.localScale）當倍率。
            // CharacterWorldHeight <= 0 → 關閉自動換算（tileSize 1，沿用原生像素大小）。
            float tileSize = 1f;
            if (CharacterWorldHeight > 0f)
            {
                if (lib.TryGetVisibleBox(MonsterName, "idle", 1f, out var vb, out _) && vb.y > 0.0001f)
                    tileSize = CharacterWorldHeight / vb.y;
                else if (lib.TryGetVisibleBox(MonsterName, "walk", 1f, out var wb, out _) && wb.y > 0.0001f)
                    tileSize = CharacterWorldHeight / wb.y;
                tileSize = Mathf.Clamp(tileSize, 0.1f, 30f);
            }

            _monAnim.Setup(MonsterName, AnimFPS, refSpeed, tileSize, IdleScale, WalkScale, AttackScale, JumpScale);

            // 碰撞框用同一個 tileSize 量 → 與放大後的 sprite 對齊（之後再 × transform.localScale = CSV Scale，一起縮放）。
            Vector2 vSize, vOff;
            if (lib.TryGetVisibleBox(MonsterName, "idle", tileSize, out vSize, out vOff)
                || lib.TryGetVisibleBox(MonsterName, "walk", tileSize, out vSize, out vOff))
                FitVisibleBoxCollider(vSize, vOff);
            else
                AutoAdjustCollider();   // 後備：用整張 sprite bounds（Setup 已指上第 0 幀）
        }
    }

    /// <summary>
    /// 把碰撞框設成貼合「圖的不透明像素」的 BoxCollider2D（size/offset 為 scale 1 的世界單位，
    /// 會隨怪物 transform 的 Scale 一起縮放，與顯示的圖對齊）。整體大小用 MonsterData 的 Scale 調、
    /// 鬆緊用 HitboxPadding 調。
    /// </summary>
    private void FitVisibleBoxCollider(Vector2 visSize, Vector2 visOffset)
    {
        // 身體框（貼合可見像素）：用於「打擊/接觸」幾何判定（玩家攻擊查詢 queriesHitTriggers=1 吃得到、
        // EnemyContactDamage 用 Physics2D.Distance 也吃得到，皆不受 trigger 影響）。
        if (_bodyCol == null)
        {
            var col = GetComponent<Collider2D>();
            _bodyCol = col as BoxCollider2D;
            if (_bodyCol == null)
            {
                if (col != null) Destroy(col);   // 萬一有別型 collider（如舊圓）→ 換成貼合的 Box
                _bodyCol = gameObject.AddComponent<BoxCollider2D>();
            }
        }
        _bodyCol.size = new Vector2(Mathf.Max(0.01f, visSize.x + HitboxPadding),
                                    Mathf.Max(0.01f, visSize.y + HitboxPadding));
        _bodyCol.offset = visOffset;

        // 所有怪一律靠 A* 導航、不做硬碰撞——身體框＋腳底框都設 trigger（只做「被打到／接觸傷害」的幾何判定，
        // `queriesHitTriggers=1`、`Physics2D.Distance` 都吃得到，不擋路）。怪永遠不會頂在牆/家具上卡死，只照 A*
        // 路徑平滑走；牆的迴避由 A*（路徑一定走在有淨空的可走格上）＋ DirectClear 的格視線保證，連通的圖不會穿牆。
        _bodyCol.isTrigger = true;
        if (_feetCol == null) _feetCol = gameObject.AddComponent<BoxCollider2D>();
        float feetH = Mathf.Clamp(visSize.x * 0.35f, 0.1f, 0.3f);
        float baseY = visOffset.y - visSize.y * 0.5f;   // 可見框底 = 腳的位置
        _feetLocalY = baseY;                            // 給 FeetWorldPos 用（見下方「可見身體的幾何」）
        _visibleLocalH = visSize.y;
        _geomReady = true;
        _feetCol.size = new Vector2(Mathf.Max(0.05f, visSize.x * 0.5f), feetH);
        _feetCol.offset = new Vector2(visOffset.x, baseY + feetH * 0.5f);
        _feetCol.isTrigger = true;
    }

    public void Initialize(MonsterData data)
    {
        MonsterName = data.Name;
        MaxHealth = data.HP;
        _currentHealth = MaxHealth;

        InvincibleTimeMs = data.InvincibleTimeMs;
        IdleScale = data.IdleScale; WalkScale = data.WalkScale; AttackScale = data.AttackScale; JumpScale = data.JumpScale;
        KnockbackThreshold = data.KnockbackThreshold;
        KnockbackPercent = data.KnockbackPercent;

        ContactDamage = data.ContactDamage;
        AttackInterval = data.AttackInterval;
        DamageReductionPercent = data.DamageReduction;
        AnimFPS = data.AnimFPS;
        AttackHitFrame = data.AttackHitFrame;   // 揮舞型近戰的命中幀（留空＝張數×0.7 粗估）
        LeapDamage = data.LeapDamage;     // 跳躍踐踏（BrainType=LeapSlam）專用；其他怪留空＝用不到
        LeapRadius = data.LeapRadius;
        ReleaseFrame = data.ReleaseFrame; // 射手型的放彈幀（留空＝ArcherBrain 退路 14）
        BombDamage = data.BombDamage;     // 自爆型專用；其他怪留空＝用不到
        BombRadius = data.BombRadius;
        BombFuse = data.BombFuse;
        DropTableId = data.DropTableId;   // 掉落表（留空/0 ＝ 不掉寶）
        MoveTrailFx = data.MoveTrailFx;   // 移動拖尾特效（留空 ＝ 不掛）
        SpeechLines = data.SpeechLines;   // 遊戲中說話用（見 MonsterSpeech）

        _sensor = gameObject.GetComponent<MonsterSensor>();
        if (_sensor == null) _sensor = gameObject.AddComponent<MonsterSensor>();
        _sensor.DetectionRange = data.DetectionRange;   // 感測半徑資料化（CSV: DetectionRange）。boss 級 Brain 之後在 Think 內可再覆寫（如紅嫁衣→30）。
        
        _actuator = gameObject.GetComponent<MonsterActuator>();
        if (_actuator == null) _actuator = gameObject.AddComponent<MonsterActuator>();
        _actuator.MoveSpeed = data.Speed;   // 移動速度資料化（CSV: Speed）；走路動畫的 ReferenceSpeed 之後在 Start 自動讀這個值

        switch (data.BrainType)
        {
            case "Chase":
                _brain = new ChaseBrain();
                break;
            case "War":             // 三方陣營劇本的部族戰士：追最近的敵對目標（敵對怪或玩家）貼上互咬（見 WarBrain）
                _brain = new WarBrain();
                break;
            case "Pounce":          // 撲擊型（狗/狼/豹…）：觀望→蓄力→直線撲擊→收招（見 PounceBrain）
                _brain = new PounceBrain();
                break;
            case "MeleeChase":      // 近戰追擊：貼身才揮武器，**揮的期間站定把動作做完**才能再移動（見 MeleeChaseBrain）
                _brain = new MeleeChaseBrain();
                break;
            case "LeapSlam":        // 跳躍踐踏型：開場一次跳躍踐踏（落地裂地＋AOE），之後永久轉近戰追擊（見 LeapSlamBrain）
                _brain = new LeapSlamBrain();
                break;
            case "Archer":          // 射手型（弓/弩/火槍…）：評估「原地射得到嗎」→ 射不到才移動（見 ArcherBrain）
                _brain = new ArcherBrain();
                break;
            case "SuicideBomb":     // 自爆型：追到貼身 → 引信閃爍預告 → 一次性爆炸傷害圈＋自毀（見 SuicideBombBrain）
                _brain = new SuicideBombBrain();
                break;
            case "RedBridalGown":   // 紅嫁衣女殭屍 boss：逃跑＋召喚（見 RedBridalGownBrain）
                _brain = new RedBridalGownBrain();
                IsBoss = true;
                break;
            case "BanyanTree":      // 榕樹妖 boss：不可直接打，玩家打牠的地刺反傷本體（見 BanyanTreeBrain）
                _brain = new BanyanTreeBrain();
                DeathVfxId = 0;        // 死亡用臉地上物的自訂燃燒演出（BanyanBossFace），不放一般死亡特效
                IsBoss = true;
                break;
            default:
                _brain = new ChaseBrain();
                break;
        }

        // 武器/技能：MonsterData.Weapon 填「WeaponTable 的 ID」時，掛上 MonsterWeaponUser（Contact/空/非數字 = 不掛）。
        WeaponId = (!string.IsNullOrWhiteSpace(data.Weapon) && int.TryParse(data.Weapon.Trim(), out int wid)) ? wid : -1;
        if (WeaponId > 0)
        {
            WeaponUser = GetComponent<MonsterWeaponUser>();
            if (WeaponUser == null) WeaponUser = gameObject.AddComponent<MonsterWeaponUser>();
            WeaponUser.Configure(this, WeaponId);
        }

        // 移動拖尾（CSV: MoveTrailFx）：填了才掛；解析不出任何一層時元件會自己停用。
        // ⚠ 這裡只 Configure，身寬是在 Update 才讀 collider 的——此刻碰撞框可能還沒量好（Setup 在後）。
        if (!string.IsNullOrWhiteSpace(MoveTrailFx))
        {
            var trail = GetComponent<MonsterMoveTrail>();
            if (trail == null) trail = gameObject.AddComponent<MonsterMoveTrail>();
            trail.Configure(this, MoveTrailFx);
        }

        AutoAdjustCollider();
    }

    private void AutoAdjustCollider()
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        Collider2D col = GetComponent<Collider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();

        if (col is BoxCollider2D box)
        {
            // 🟢 安全保護：確保 size 不會小於等於 0
            float sizeX = Mathf.Max(0.01f, sr.sprite.bounds.size.x + HitboxPadding);
            float sizeY = Mathf.Max(0.01f, sr.sprite.bounds.size.y + HitboxPadding);
            box.size = new Vector2(sizeX, sizeY);
            box.offset = sr.sprite.bounds.center;
        }
        else if (col is CircleCollider2D circle)
        {
            float maxDim = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
            // 🟢 安全保護：確保 radius 不會小於等於 0
            circle.radius = Mathf.Max(0.005f, (maxDim / 2f) + (HitboxPadding / 2f));
            circle.offset = sr.sprite.bounds.center;
        }
        if (col != null) col.isTrigger = true;   // 所有怪：無硬碰撞、純 A* 導航
    }

    // 🟢 在編輯器中顯示紅色受擊範圍，方便即時調整
    private void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = Color.red;
            if (col is BoxCollider2D box)
                Gizmos.DrawWireCube(transform.TransformPoint(box.offset), box.size);
            else if (col is CircleCollider2D circle)
                Gizmos.DrawWireSphere(transform.TransformPoint(circle.offset), circle.radius);
        }
    }

    void Update()
    {
        if (_isDead) return;

        // 目標（統一查 FactionRelations）：
        //  ‧ enemyTarget＝最近的敵對怪：PlayerAlly 一直找；部族開戰後找（HasMonsterFoes）；其餘 null。
        //  ‧ playerTarget：PlayerAlly＝玩家本體（跟隨用）；會攻擊玩家的陣營＝感測範圍內的玩家；其餘 null
        //    （Neutral／和平期部族 AttacksPlayer=false → 不追蹤玩家，NPC「平時不看玩家」也靠這條）。
        Transform enemyTarget = FactionRelations.HasMonsterFoes(Faction) ? FindNearestEnemy() : null;
        Transform playerTarget = (Faction == MonsterFaction.PlayerAlly) ? PlayerTransform
                               : (FactionRelations.AttacksPlayer(Faction) ? _sensor.GetTargetPlayer() : null);

        // 發現玩家（或友軍發現敵怪）→ 記住，之後才允許說話（黏著，不再變回未發現）。
        if (!IsAwareOfPlayer && (playerTarget != null || enemyTarget != null)) IsAwareOfPlayer = true;

        // 擊退期間預設不做決策（F19）；但 Brain 正在演「不可中斷的動作」時照跑（見 SuppressKnockbackInterrupt）。
        if (_hitReaction == null || !_hitReaction.IsKnockedBack || SuppressKnockbackInterrupt)
        {
            var ctx = new MonsterContext
            {
                Self = this,
                Actuator = _actuator,
                Sensor = _sensor,
                Player = playerTarget,
                Enemy = enemyTarget,
                DeltaTime = Time.deltaTime,
            };
            _brain.Think(in ctx);
        }

        // 面向/攻擊動畫的對象：有敵對怪目標（友軍打敵怪、部族互咬）就面向它，否則面向玩家目標。
        Transform faceTarget = enemyTarget != null ? enemyTarget : playerTarget;
        HandleVisuals(faceTarget);
    }

    // 友軍找最近的敵怪：走登記表(不用 OverlapCircle，避開 queriesStartInColliders 貼身漏抓)。範圍 = 感知器 DetectionRange。
    public Transform FindNearestEnemy()
    {
        float range = (_sensor != null) ? _sensor.DetectionRange : 10f;
        float rangeSq = range * range;
        Transform best = null; float bestSq = float.MaxValue;
        var list = Active;
        for (int i = 0; i < list.Count; i++)
        {
            MonsterController mc = list[i];
            if (mc == null || mc == this || mc.IsDead || !FactionRelations.Hostile(Faction, mc.Faction)) continue;   // 「打得到的才是敵人」統一查 FactionRelations
            float sq = ((Vector2)mc.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (sq <= rangeSq && sq < bestSq) { bestSq = sq; best = mc.transform; }
        }
        return best;
    }

    private void HandleVisuals(Transform player)
    {
        if (_spriteRenderer == null) return;

        // 「有沒有在移動」改看『實際位移速度』（每幀真的移動了多少），不看指令速度 _rb.velocity——
        // 逃跑/被卡在牆角時 velocity 仍是滿的 MoveSpeed 但位置沒變，會誤播走路（原地踏步）。
        // 玩家/怪物 Rigidbody2D 已開 Interpolate（見 PROBLEMS E5），transform.position 每幀平滑更新 → 位移量測穩定。
        Vector2 pos = transform.position;
        if (!_lastVisualPosInit) { _lastVisualPos = pos; _lastVisualPosInit = true; }
        float dt = Time.deltaTime;
        float rawSpeed = dt > 0.0001f ? Vector2.Distance(pos, _lastVisualPos) / dt : 0f;
        float moveDx = pos.x - _lastVisualPos.x;   // 本幀實際位移 X（FaceMovement 面向用；在覆寫前取）
        _lastVisualPos = pos;
        // 指數平滑：吃掉單幀抖動（物理步與畫面步不完全對齊時的零位移幀），避免走路/發呆一幀一跳。
        _visualSpeedEma = (dt > 0.0001f)
            ? Mathf.Lerp(_visualSpeedEma, rawSpeed, 1f - Mathf.Exp(-dt / 0.08f))
            : rawSpeed;
        float currentSpeed = _visualSpeedEma;
        bool moving = currentSpeed > MoveAnimThreshold;

        // 1. 狀態決策（路線 B）：在攻擊範圍內且有 attack 圖 → 攻擊；否則 移動→走路 / 靜止→發呆。
        //    沒有 attack 圖的怪不會被選到 Attack（Has 防呆），自然只演走路/發呆。
        if (_monAnim != null)
        {
            MonsterAnimator.State st;
            bool casting = Time.time < _skillCastAnimUntil;   // 施放技能中 → 出手動作（不限距離）
            // Brain 接管時不做自動判定（見 BrainControlsAttackPose）——那是「舉著劍追人」的來源。
            bool inAttackRange = !BrainControlsAttackPose && player != null
                && Vector2.Distance(transform.position, player.position) <= AttackRange;
            bool wantAttackPose = casting || inAttackRange;

            if (IsPanting)
                // 喘息破綻最優先：壓過攻擊/走路/發呆。這段期間她就算被打得後退，畫面上也該是喘、不是走路。
                st = MonsterAnimator.State.Pant;
            else if (wantAttackPose && _monAnim.Has(MonsterAnimator.State.Attack))
                st = MonsterAnimator.State.Attack;            // 有 attack 幀 → 播真正的攻擊/施法動作
            else if (casting)
                // 施法但這隻怪沒有 attack 幀（如紅嫁衣的 attack 尚未 Sync 進 StreamingAssets）：
                // 退回播走路當「出手」表演，保留召喚時的動作感（不會像 idle 那樣完全不動）。
                // 只在 0.6s 召喚視窗內，平常靜止仍是 idle——不會回到「原地踏步」的舊 bug。
                st = MonsterAnimator.State.Walk;
            else if (moving) st = MonsterAnimator.State.Walk;
            else st = MonsterAnimator.State.Idle;

            // 走路 fps 平常跟實際位移連動；但「施法退回走路且原地不動」時改用正常移動速度餵，
            // 讓出手表演以正常節奏播（否則實際位移≈0 會被 fps 連動壓到最慢）。
            float animSpeed = currentSpeed;
            if (st == MonsterAnimator.State.Walk && casting && !moving)
                animSpeed = (_actuator != null && _actuator.MoveSpeed > 0.01f) ? _actuator.MoveSpeed : 3f;
            _monAnim.SetState(st, animSpeed);
        }

        // 舊路線後備：若這隻怪用的是自帶 Unity Animator 的 prefab，沿用 isMoving 驅動。
        if (_animator != null) _animator.SetBool("isMoving", moving);

        // 2. 左右翻轉 (Flip)：依玩家位置與「來源圖朝向」決定（與動畫系統無關；同 PlayerController.SetFacing）。
        //    來源朝右(SpriteSourceFacesRight=true)：面右=不翻(flipX=false)、面左=翻(true)；來源朝左：相反。
        //    FaceMovement（NPC 巡邏）：移動中面向「移動方向」（不然往右走、玩家在左，會邊倒退邊走）；
        //    停下時退回下面的「面向玩家」（玩家走近，NPC 轉頭看他）。
        if (FaceMovement && moving && Mathf.Abs(moveDx) > 0.002f)
        {
            bool faceRight = moveDx > 0f;
            _spriteRenderer.flipX = (faceRight != SpriteSourceFacesRight);
        }
        else if (player != null)
        {
            bool faceRight = player.position.x > transform.position.x;
            _spriteRenderer.flipX = (faceRight != SpriteSourceFacesRight);
        }
    }

    public void TakeDamage(float amount, Vector2 hitDirection)
    {
        if (_isDead) return;

        if (_hitReaction != null && !_hitReaction.TryHitReaction(amount, hitDirection))
            return;

        _currentHealth -= amount;
        if (!CombatSystem.CurrentHitTheatrical)   // 演戲傷害（兩族互打 1/100）：不跳數字、不印 log（30 隻互毆會洗版）；白光閃爍照舊
        {
            DamageNumberManager.Show(gameObject, amount);   // 頭上跳傷害數字（已過無敵判定 = 確實吃到傷害）
            Debug.Log($"{MonsterName} took {amount} damage. HP: {_currentHealth}/{MaxHealth}");
        }

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, Vector2.zero);
    }

    /// <summary>
    /// **立刻致死**，略過受擊反應與無敵幀。走的仍然是既有的死亡流程
    /// （死亡特效／掉落／死亡旗標／RunProgress『已清』／召喚回收全部照跑），只是不經過 <see cref="TakeDamage"/>。
    ///
    /// <para>給「怪自己把自己弄死」的機制用——目前是自爆型（<see cref="SuicideBombBrain"/>）。</para>
    ///
    /// <para>⚠ **不要用 `TakeDamage(超大數字)` 代替**：那條路會先問 <c>HitReactionHandler.TryHitReaction</c>，
    /// 怪正處在無敵幀內就直接 return false、一滴血都不扣 ⇒ 變成「爆炸放了、怪沒死」，
    /// 而 Brain 已經進了終結狀態 ⇒ 牠會站在原地不動、不再追人也不再爆。
    /// 目前自爆怪的 `InvincibleTimeMs` 填 0 所以碰不到，但那是 CSV 隨時可以改的值，
    /// 不該讓「自爆會不會成功」取決於另一個欄位（同 PROBLEMS **F19**「無敵時間是一份隱形的行為預算」）。</para>
    /// </summary>
    public void Kill() => Die();

    // ── ICombatModifiers：怪物作為攻擊方無加成（1）；作為受擊方套用減傷掛勾（目前 CSV 預設 0 = 不減傷）──
    public float OutgoingDamageMultiplier(in DamageInfo info) => 1f;

    public float IncomingDamageMultiplier(in DamageInfo info)
    {
        float r = Mathf.Clamp(DamageReductionPercent, 0f, 90f);
        return 1f - r / 100f;
    }

    bool _dying;   // 已致死、待本幀 LateUpdate 才真正銷毀

    // 致死：只標記，不立刻銷毀。真正的銷毀延到本幀 LateUpdate（所有 Update 跑完之後），
    // 這樣「殺死這隻怪的那一幀」，這隻怪自己的 EnemyContactDamage 仍會執行一次 → **死掉也能還手**。
    // ⇒ 兩隻怪一接觸，不管誰的 Update 先跑、不管攻速差多少，第一下一定雙方互換傷害（玻璃大炮撞上去也會一起受傷）。
    void Die()
    {
        if (_isDead) return;
        _isDead = true;    // 立刻停止行動（Update 提前 return）＋別的怪不再把它當目標（IsDead 過濾）
        _dying = true;     // 實際銷毀延到 LateUpdate

        // 榕樹妖：死亡不是「怪消失」而是一段燃燒演出（臉燒→消失→整棵樹此起彼落燒）。
        if (_brain is BanyanTreeBrain)
        {
            BanyanBossFace.Instance?.PlayDeath();
            BossSpike.CancelAll();   // 地刺立刻停止（含預警中/冒出中），不再傷人
        }

        // 召喚者（boss）死亡：回收還在場上的召喚分身（例：紅嫁衣的家人幽靈）。
        // ⚠ 用 GetComponents（複數）：一隻 boss 可能掛**不只一把**召喚武器——紅嫁衣除了平時那把，
        // 大絕「家人齊聚」另有一把獨立的（獨立名單＝獨立同時上限，見 RedBridalGownBrain）。
        // 只收第一個的話，大絕叫出來的 11 隻會在 boss 死後留在場上繼續追殺玩家（違反 BOSS_MODULE §6.7）。
        var weaponUsers = GetComponents<MonsterWeaponUser>();
        for (int i = 0; i < weaponUsers.Length; i++)
            if (weaponUsers[i] != null) weaponUsers[i].RecallSummons();
    }

    void LateUpdate()
    {
        if (!_dying) return;
        _dying = false;

        // 死亡寫旗標（資料驅動）：例「殺了家人→killedFamily→新娘生氣分支」。旗標為空＝不寫。
        if (!string.IsNullOrEmpty(DeathFlag)) TriggerChain.SetFlag(DeathFlag);

        // 關卡進度＋掉寶：只對「地圖出生的敵怪」且在關卡 run 內處理。兩件事刻意分開判斷：
        //   ‧ 記進 RunProgress『已清』(有 SpawnKey)→ 本趟換圖回來不再重生（Boss 也走這條，死了不復生）。
        //     「重複產生」的出生點刻意不給 SpawnKey——不然第一波死光後就永遠不再生了。
        //   ‧ 掉寶 (DropsLoot)：必掉銅錢＋機率掉藥，掉在屍體位置、按 F 撿進臨時包（見 DropRunLoot）。
        // 召喚物兩者都沒有，避免無限刷。
        if (Faction == MonsterFaction.Enemy && RunProgress.Exists && RunProgress.Instance.RunActive)
        {
            if (!string.IsNullOrEmpty(SpawnKey))
            {
                int mapId = MapManager.Instance != null ? MapManager.Instance.CurrentMapId : -1;
                RunProgress.Instance.MarkSpawnKilled(mapId, SpawnKey);
            }
            if (DropsLoot) DropRunLoot();
        }

        // 死亡特效（VfxTable 的 DeathVfxId）：獨立 GameObject，不受怪物銷毀影響。
        if (DeathVfxId > 0)
        {
            if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();
            if (_vfx != null)
            {
                // 特效大小跟著怪物：縮放到「這隻怪的可見高度」（同招喚特效的做法，見 VfxManager.SpawnSizedToHeight）。
                if (_spriteRenderer != null && _spriteRenderer.bounds.size.y > 0.0001f)
                    _vfx.SpawnSizedToHeight(DeathVfxId, _spriteRenderer.bounds.center, _spriteRenderer.bounds.size.y);
                else
                    _vfx.Spawn(DeathVfxId, transform.position, 0f);
            }
            else Debug.LogWarning("[MonsterController] 場景找不到 VfxManager，死亡特效略過。");
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// 掉寶：擲一次這隻怪的 <see cref="DropTableId"/>（<see cref="DropTable"/>），掉在屍體位置。
    /// 透過 InteractionManager.DropLoot 掉落，會自動登記進 RunProgress（沒撿的換圖回來還在）。
    ///
    /// <para>⚠ **`DropTableId` 留空／0 ＝ 完全不掉寶**（作者 2026-09-22 拍板）。
    /// 2026-09-22 之前這裡是寫死的「必掉銅錢 1~5 ＋ 35% 掉一瓶藥（201/202 各半）」，
    /// 現在那組住在 `DropTable.csv` 的 **ID 1**，既有的怪全部填 1 ⇒ 導入本身零行為變化。</para>
    ///
    /// <para>Inspector 上的 `lootMoneyMin/Max`／`lootPotionChance` 已經沒有人讀了，
    /// 留著只是為了不動到既有 prefab 的序列化資料；要調掉落請改 CSV。</para>
    /// </summary>
    void DropRunLoot()
    {
        if (!InteractionManager.Exists) return;
        if (DropTableId <= 0) return;            // 不掉寶（夢境教學怪、以及任何刻意不給獎勵的怪）

        var drops = DropTable.Roll(DropTableId);
        if (drops.Count == 0) return;

        var im = InteractionManager.Instance;
        Vector2 pos = transform.position;
        for (int i = 0; i < drops.Count; i++)
            im.DropLoot(drops[i].itemId, drops[i].count, pos);
    }
}