using System.Collections.Generic;
using UnityEngine;

public class MonsterController : MonoBehaviour, IDamageable, ICombatModifiers
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
    [Tooltip("角色站立顯示高度（世界單位），與主角 PlayerController.CharacterWorldHeight 同一套邏輯：" +
             "依 idle 可見高度自動換算，讓「同一張圖丟主角或怪物資料夾都一樣大」。要某隻怪特別大/小，再用 CSV 的 Scale 當倍率。" +
             "<=0 = 關閉自動換算、沿用原生像素大小。")]
    public float CharacterWorldHeight = 1.95f;
    private float _currentHealth;
    private bool _isDead = false;

    [Header("Hit Reaction")]
    public float InvincibleTimeMs = 0f;
    public float KnockbackThreshold = 0f;
    public float KnockbackPercent = 0f;

    [Header("Combat")]
    public float ContactDamage = 10f;            // 碰到玩家造成的傷害（CSV: MonsterData.ContactDamage）
    public float AttackInterval = 0.5f;          // 接觸攻擊間隔秒＝攻速（CSV: AttackInterval）
    public float DamageReductionPercent = 0f;    // 受擊減傷 %（掛勾；目前 CSV 預設 0，之後接減傷/抗性）

    [Header("Weapon / Skill")]
    [Tooltip("這隻怪使用的武器 = WeaponTable 的 ID（CSV: MonsterData.Weapon 填數字）。Contact/空 = 只近戰接觸傷害、不掛武器。")]
    public int WeaponId = -1;
    // 怪物用武器的統一入口（召喚等技能走這裡；投射武器 Phase 2）。boss 級 Brain 透過 ctx.Self.WeaponUser 施放。
    public MonsterWeaponUser WeaponUser { get; private set; }

    [Header("Faction")]
    [Tooltip("陣營：Enemy=一般敵怪/boss/其召喚物(追玩家)；PlayerAlly=玩家召喚的協戰怪(追敵怪)。由 MonsterSpawner 設定。")]
    public MonsterFaction Faction = MonsterFaction.Enemy;
    public bool IsDead => _isDead;

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

            _monAnim.Setup(MonsterName, AnimFPS, refSpeed, tileSize);

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
        float baseY = visOffset.y - visSize.y * 0.5f;   // 可見框底 = 腳的位置（俯視角 pivot 在腳）
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
        KnockbackThreshold = data.KnockbackThreshold;
        KnockbackPercent = data.KnockbackPercent;

        ContactDamage = data.ContactDamage;
        AttackInterval = data.AttackInterval;
        DamageReductionPercent = data.DamageReduction;
        AnimFPS = data.AnimFPS;

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
            case "RedBridalGown":   // 紅嫁衣女殭屍 boss：逃跑＋召喚（見 RedBridalGownBrain）
                _brain = new RedBridalGownBrain();
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

        // 目標：Enemy 陣營＝追玩家；PlayerAlly＝跟玩家(ctx.Player) + 打最近敵怪(ctx.Enemy)。
        Transform enemyTarget = (Faction == MonsterFaction.PlayerAlly) ? FindNearestEnemy() : null;
        Transform playerTarget = (Faction == MonsterFaction.PlayerAlly) ? PlayerTransform : _sensor.GetTargetPlayer();

        if (_hitReaction == null || !_hitReaction.IsKnockedBack)
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

        // 面向/攻擊動畫的對象：友軍面向正在打的敵怪（沒有就面向玩家）；敵人面向玩家。
        Transform faceTarget = (Faction == MonsterFaction.PlayerAlly)
            ? (enemyTarget != null ? enemyTarget : playerTarget)
            : playerTarget;
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
            if (mc == null || mc == this || mc.IsDead || mc.Faction != MonsterFaction.Enemy) continue;
            float sq = ((Vector2)mc.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (sq <= rangeSq && sq < bestSq) { bestSq = sq; best = mc.transform; }
        }
        return best;
    }

    private void HandleVisuals(Transform player)
    {
        if (_spriteRenderer == null) return;

        float currentSpeed = (_rb != null) ? _rb.velocity.magnitude : 0f;
        bool moving = currentSpeed > 0.1f;

        // 1. 狀態決策（路線 B）：在攻擊範圍內且有 attack 圖 → 攻擊；否則 移動→走路 / 靜止→發呆。
        //    沒有 attack 圖的怪不會被選到 Attack（Has 防呆），自然只演走路/發呆。
        if (_monAnim != null)
        {
            MonsterAnimator.State st;
            if (player != null
                && _monAnim.Has(MonsterAnimator.State.Attack)
                && Vector2.Distance(transform.position, player.position) <= AttackRange)
                st = MonsterAnimator.State.Attack;
            else if (moving) st = MonsterAnimator.State.Walk;
            else st = MonsterAnimator.State.Idle;

            _monAnim.SetState(st, currentSpeed);
        }

        // 舊路線後備：若這隻怪用的是自帶 Unity Animator 的 prefab，沿用 isMoving 驅動。
        if (_animator != null) _animator.SetBool("isMoving", moving);

        // 2. 左右翻轉 (Flip)：依玩家位置與「來源圖朝向」決定（與動畫系統無關；同 PlayerController.SetFacing）。
        //    來源朝右(SpriteSourceFacesRight=true)：面右=不翻(flipX=false)、面左=翻(true)；來源朝左：相反。
        if (player != null)
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
        DamageNumberManager.Show(gameObject, amount);   // 頭上跳傷害數字（已過無敵判定 = 確實吃到傷害）
        Debug.Log($"{MonsterName} took {amount} damage. HP: {_currentHealth}/{MaxHealth}");

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, Vector2.zero);
    }

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
    }

    void LateUpdate()
    {
        if (!_dying) return;
        _dying = false;

        // 死亡寫旗標（資料驅動）：例「殺了家人→killedFamily→新娘生氣分支」。旗標為空＝不寫。
        if (!string.IsNullOrEmpty(DeathFlag)) TriggerChain.SetFlag(DeathFlag);

        // 死亡特效（VfxTable 的 DeathVfxId）：獨立 GameObject，不受怪物銷毀影響。
        if (DeathVfxId > 0)
        {
            if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();
            if (_vfx != null) _vfx.Spawn(DeathVfxId, transform.position, 0f);
            else Debug.LogWarning("[MonsterController] 場景找不到 VfxManager，死亡特效略過。");
        }

        Destroy(gameObject);
    }
}