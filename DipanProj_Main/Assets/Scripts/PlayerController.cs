using System.Collections.Generic;
using UnityEngine;
using Sorrows.Ballistics;

public class PlayerController : MonoBehaviour
{
    public float MoveSpeed = 5f;
    public LayerMask EnvLayer; 
    public LayerMask EnemyLayer; 

    [Header("Player Size")]
    public float PlayerScale = 1f;

    [Header("Player Stats")]
    public float PlayerMaxHealth = 100f;

    [Header("Hit Reaction (hardcoded for now)")]
    public float PlayerInvincibleTimeMs = 1000f;
    public float PlayerKnockbackThreshold = 0f;
    public float PlayerKnockbackPercent = 10f;

    private Animator _animator;
    private Rigidbody2D _rb;
    private Vector2 _moveInput;
    private SpriteRenderer _spriteRenderer;
    private WeaponManager _weaponManager;
    private Dipan.Inventory.InventorySystem _inventory;
    private int _lastEquippedWeaponItemId = -1;
    private GroundEffectManager _groundEffectManager;
    private VfxManager _vfxManager;
    private HitReactionHandler _hitReaction;
    private float _fireTimer = 0f;
    private float _currentHealth;
    private readonly List<BulletInstance> _activeOrbitalBullets = new List<BulletInstance>();
    private float _orbitalGroupExpireTime = -1f;

    // 持續型雷射光束（按住維持、放開銷毀）
    private readonly List<LaserBeam> _activeBeams = new List<LaserBeam>();
    private readonly List<float> _beamAngleOffsets = new List<float>();
    private WeaponData _activeBeamWeapon;
    // 火焰噴射器：雷射的 TrailEffectID > 0 時，沿光束路徑維護一排循環火焰 Vfx（不畫光束本體）
    private readonly List<GameObject> _flameVfx = new List<GameObject>();
    private static readonly List<Vector2> _flamePosBuffer = new List<Vector2>(64);

    // 佛光：按住維持一個「跟著玩家移動」的 GroundEffect（圓形 AOE），放開/切武器銷毀
    private GroundEffectInstance _activeAura;
    private WeaponData _activeAuraWeapon;

    // 連鎖閃電：折線緩衝（避免每次發射配置）+ 閃光存活秒數
    private static readonly List<Vector2> _chainPathBuffer = new List<Vector2>(16);
    private const float ChainFlashDuration = 0.16f;

    // 天降雷擊：從畫面上緣再往上多少單位開始劈、BlastRadius 留空時的預設 AOE 半徑
    private const float SkyStrikeTopMargin = 2f;
    private const float SkyStrikeDefaultBlast = 1.2f;
    private static readonly HashSet<int> _emptyHitSet = new HashSet<int>(); // 追蹤吸附用的空排除集（FindNearestDamageable 只讀不寫）

    // 命中迸發子武器：生成點沿命中面法線往外推的最小距離（避免生在牆/家具表面內被自己的 CheckSpawnOverlap 瞬殺）
    private const float SubWeaponSpawnOffset = 0.35f;

    public bool isFacingRightByDefault = true;

    void Start()
    {
        transform.localScale = Vector3.one * PlayerScale;

        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (_spriteRenderer != null)
        {
            _spriteRenderer.flipX = isFacingRightByDefault;
        }

        _currentHealth = PlayerMaxHealth;

        _hitReaction = gameObject.AddComponent<HitReactionHandler>();
        _hitReaction.Configure(_spriteRenderer, _rb,
            PlayerInvincibleTimeMs, PlayerKnockbackThreshold, PlayerKnockbackPercent);

        _weaponManager = FindObjectOfType<WeaponManager>();
        if (_weaponManager == null)
        {
            Debug.LogError("WeaponManager not found in scene!");
        }

        // 背包橋接：裝備武器欄的武器 → 切到該武器（E 鍵切換仍保留，兩者並存）。
        _inventory = Dipan.Inventory.InventorySystem.Instance;
        _inventory.OnChanged += OnInventoryChanged;
        OnInventoryChanged();   // 初始同步（若一開始就有裝備）

        _groundEffectManager = FindObjectOfType<GroundEffectManager>();
        if (_groundEffectManager == null)
        {
            Debug.LogWarning("GroundEffectManager not found in scene; recipes with GroundEffectID will be ignored.");
        }

        _vfxManager = FindObjectOfType<VfxManager>();
        if (_vfxManager == null)
        {
            Debug.LogWarning("VfxManager not found in scene; weapons with FireEffectID / HitEffectID will be ignored.");
        }
    }

    void Update()
    {
        // UI 輸入閘門：開啟背包等視窗時，停止移動/攻擊/切武器（最小侵入；旗標由 UIManager 統合）。
        if (Dipan.UI.UIManager.IsGameplayInputBlocked)
        {
            _moveInput = Vector2.zero;
            HandleVisuals();
            return;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        _moveInput = new Vector2(h, v).normalized;

        if (_spriteRenderer != null)
        {
            bool isAttacking = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);
            if (isAttacking)
            {
                Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                float mouseDiffX = mousePos.x - transform.position.x;
                
                if (mouseDiffX < 0) _spriteRenderer.flipX = false;
                else if (mouseDiffX > 0) _spriteRenderer.flipX = true;
            }
            else if (Mathf.Abs(h) > 0.01f)
            {
                if (h < 0) _spriteRenderer.flipX = false;
                else if (h > 0) _spriteRenderer.flipX = true;
            }
        }

        if (Input.GetKeyDown(KeyCode.E) && _weaponManager != null)
        {
            _weaponManager.SwitchToPreviousWeapon();
        }

        if (_fireTimer > 0) _fireTimer -= Time.deltaTime;

        HandleFiring();

        if (_orbitalGroupExpireTime > 0f && Time.time >= _orbitalGroupExpireTime)
            ClearActiveOrbitalBullets();

        HandleVisuals();
    }

    private void HandleVisuals()
    {
        if (_animator == null || _spriteRenderer == null) return;

        float currentSpeed = (_rb != null) ? _rb.velocity.magnitude : 0f;
        _animator.SetBool("isMoving", currentSpeed > 0.1f);
    }

    private void OnDestroy()
    {
        ClearActiveOrbitalBullets();
        ClearActiveBeams();
        ClearActiveAura();
        if (_inventory != null) _inventory.OnChanged -= OnInventoryChanged;
    }

    // 背包武器欄變動時呼叫：裝備哪把武器就切到哪把（用該物品的 WeaponID 對應 WeaponTable）。
    // 卸下（武器欄清空）時保留當前武器、不切換。E 鍵的循環切換不受影響。
    private void OnInventoryChanged()
    {
        if (_weaponManager == null || _inventory == null) return;
        int itemId = _inventory.GetEquipped(Dipan.Inventory.EquipSlot.Weapon);
        if (itemId == _lastEquippedWeaponItemId) return;   // 武器欄沒變（只是別處變動）就略過
        _lastEquippedWeaponItemId = itemId;
        if (itemId <= 0) return;                            // 卸下：保留當前武器
        var data = _inventory.GetData(itemId);
        if (data != null && data.WeaponID > 0)
            _weaponManager.SwitchWeapon(data.WeaponID);
    }

    /// <summary>換地圖時清掉跨幀維持的武器實例（環繞彈／雷射／火焰柱／佛光），避免殘留到新圖。由 MapManager 在換圖前呼叫。</summary>
    public void ClearPersistentWeaponsForMapChange()
    {
        ClearActiveOrbitalBullets();
        ClearActiveBeams();
        ClearActiveAura();
    }

    // ── 發射總入口：雷射走持續光束路徑，其餘走離散發射 ──
    private void HandleFiring()
    {
        WeaponData weapon = (_weaponManager != null) ? _weaponManager.GetCurrentWeapon() : null;
        bool firing = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);

        bool isLaser = weapon != null && weapon.Recipe != null
                       && weapon.Recipe.Data != null && weapon.Recipe.Data.IsLaser;
        bool isAura = weapon != null && weapon.Recipe != null && weapon.Recipe.IsAura;

        // 切到非雷射武器、或換了不同雷射武器 → 先清掉舊光束
        if (_activeBeams.Count > 0 && (!isLaser || weapon != _activeBeamWeapon))
            ClearActiveBeams();
        // 切到非佛光武器、或換了不同佛光武器 → 先清掉舊佛光
        if (_activeAura != null && (!isAura || weapon != _activeAuraWeapon))
            ClearActiveAura();

        if (isLaser)
        {
            UpdateLaser(weapon, firing);
            return;
        }

        if (isAura)
        {
            UpdateAura(weapon, firing);
            return;
        }

        if (firing && _fireTimer <= 0)
            Shoot();
    }

    // ── 佛光：按住攻擊時，在玩家身上維持一個「跟著玩家移動」的 GroundEffect 圓形 AOE ──
    // 圓的半徑/節拍/外觀走配方 GroundEffectID 指向的 GroundEffectTable；傷害走武器表 Damage。
    // 放開攻擊鍵 / 切武器時銷毀整個佛光（仿雷射的群組生命週期）。不發射任何子彈、不碰彈道系統。
    private void UpdateAura(WeaponData weapon, bool firing)
    {
        if (!firing)
        {
            if (_activeAura != null) ClearActiveAura();
            return;
        }

        if (_groundEffectManager == null)
        {
            Debug.LogWarning("GroundEffectManager 不存在，佛光武器無法生效。");
            return;
        }

        if (_activeAura == null)
        {
            int auraId = (weapon.Recipe != null) ? weapon.Recipe.GroundEffectID : 0;
            if (auraId <= 0)
            {
                Debug.LogWarning($"佛光武器 '{weapon.Name}' 的配方沒有設定 GroundEffectID（要指向 GroundEffectTable 的佛光圓）。");
                return;
            }
            // 傷害走武器表 Damage（餵進 GroundEffectInstance 的 damageOverride）。佛光圓用 Duration=-1（永久，由本控制器管理生死）。
            _activeAura = _groundEffectManager.Spawn(auraId, transform.position, weapon.Damage);
            _activeAuraWeapon = weapon;
            // 發射特效（可選）：佛光在按下瞬間播一次（持續存在期間不每幀重播）
            TrySpawnFireEffect(weapon, AimDirectionToMouse());
        }

        // 每幀把佛光移到玩家身上：GroundEffectInstance 的 tile／單圖是子物件、傷害每拍即時讀 transform.position，
        // 所以只要移動 transform，「視覺圈」與「傷害圈」就會一起跟著玩家（GroundEffect 本身零改動）。
        if (_activeAura != null)
            _activeAura.transform.position = transform.position;
    }

    private void ClearActiveAura()
    {
        if (_activeAura != null) Destroy(_activeAura.gameObject);
        _activeAura = null;
        _activeAuraWeapon = null;
    }

    void FixedUpdate()
    {
        if (_hitReaction != null && _hitReaction.IsKnockedBack)
            return;

        _rb.velocity = _moveInput * MoveSpeed;
    }

    void Shoot()
    {
        if (_weaponManager == null) return;

        WeaponData weapon = _weaponManager.GetCurrentWeapon();
        if (weapon == null || weapon.BulletPrefab == null || weapon.Recipe == null) return;

        ProjectileData recipe = weapon.Recipe.Data;

        if (recipe.IsLaser)
        {
            // 雷射由 HandleFiring → UpdateLaser 持續路徑處理，不走離散發射
            return;
        }

        // 發射特效：每次離散發射在玩家身上播一次，朝瞄準方向
        TrySpawnFireEffect(weapon, AimDirectionToMouse());

        if (weapon.Recipe.IsSkyStrike)
        {
            ShootSkyStrike(weapon, recipe);
        }
        else if (weapon.Recipe.IsChain)
        {
            ShootChain(weapon, recipe);
        }
        else if (recipe.IsOrbital)
        {
            ShootOrbital(weapon, recipe);
        }
        else if (recipe.IsParabolic)
        {
            ShootParabolic(weapon, recipe);
        }
        else
        {
            ShootNormal(weapon, recipe);
        }

        _fireTimer = recipe.FireInterval;
    }

    private void ShootNormal(WeaponData weapon, ProjectileData recipe)
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        Vector2 fireDirection = (mousePos - transform.position).normalized;
        Vector2 spawnPos = (Vector2)transform.position;

        LayerMask collisionMask = EnvLayer | EnemyLayer;
        LayerMask pierceableLayers = ResolvePierceableLayers(weapon.Recipe);
        LayerMask nonBounceLayers = ResolveNonBounceLayers(weapon.Recipe.BounceTarget);

        Vector3 bulletScale = weapon.BulletPrefab.transform.localScale * PlayerScale * weapon.BulletScale;
        WeaponData firedWeapon = weapon;
        BallisticsEngine.Spawn(recipe, weapon.BulletPrefab, spawnPos, fireDirection,
            collisionMask, pierceableLayers, nonBounceLayers,
            (b, t, h) => HandleBulletHit(firedWeapon, b, t, h),
            weapon.WeaponSprite, weapon.SpriteAngleOffset, bulletScale, weapon.WeaponSprites, weapon.AnimFPS,
            (b, pos) => TrySpawnTrailEffect(firedWeapon, pos));
    }

    private void ClearActiveOrbitalBullets()
    {
        for (int i = 0; i < _activeOrbitalBullets.Count; i++)
        {
            BulletInstance b = _activeOrbitalBullets[i];
            if (b != null)
                Destroy(b.gameObject);
        }
        _activeOrbitalBullets.Clear();
        _orbitalGroupExpireTime = -1f;
    }

    // ── 雷射光束：按住維持一組（依 SpreadCount 扇形）光束，每幀更新砲口與瞄準 ──
    private void UpdateLaser(WeaponData weapon, bool firing)
    {
        if (!firing)
        {
            if (_activeBeams.Count > 0) ClearActiveBeams();
            return;
        }

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        Vector2 origin = (Vector2)transform.position;
        Vector2 baseDir = (Vector2)mousePos - origin;
        baseDir = baseDir.sqrMagnitude > 0.0001f ? baseDir.normalized : Vector2.right;
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        if (_activeBeams.Count == 0)
        {
            SpawnLaserBeams(weapon);
            // 發射特效：雷射在按下瞬間播一次砲口特效（持續光束不每幀重播）
            TrySpawnFireEffect(weapon, baseDir);
        }

        for (int i = 0; i < _activeBeams.Count; i++)
        {
            LaserBeam beam = _activeBeams[i];
            if (beam == null) continue;
            float ang = (baseAngle + _beamAngleOffsets[i]) * Mathf.Deg2Rad;
            beam.Origin = origin;
            beam.AimDirection = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        }

        // 火焰噴射器：沿光束路徑維護一排循環火焰 Vfx（跟著光束掃）
        if (weapon.TrailEffectID > 0)
            UpdateFlameColumn(weapon);
    }

    // ── 火焰噴射器：把火焰 Vfx 沿所有光束路徑、每隔 TrailStep 鋪一根，每幀重新定位（跟著掃）──
    // 火焰 Vfx 用 Loop=1 + Duration=-1（無限循環），生死由本方法/ClearActiveBeams 管理。
    private void UpdateFlameColumn(WeaponData weapon)
    {
        if (_vfxManager == null || weapon.TrailEffectID <= 0) return;

        float step = (weapon.Recipe != null && weapon.Recipe.Data != null && weapon.Recipe.Data.TrailStep > 0f)
            ? weapon.Recipe.Data.TrailStep : 0.5f;

        // 1) 收集所有光束路徑上、每隔 step 的火焰位置
        _flamePosBuffer.Clear();
        for (int b = 0; b < _activeBeams.Count; b++)
        {
            if (_activeBeams[b] != null)
                SampleAlongPath(_activeBeams[b].Points, step, _flamePosBuffer);
        }

        int need = _flamePosBuffer.Count;

        // 2) 移除多餘的火焰
        while (_flameVfx.Count > need)
        {
            int last = _flameVfx.Count - 1;
            if (_flameVfx[last] != null) Destroy(_flameVfx[last]);
            _flameVfx.RemoveAt(last);
        }

        // 3) 重用現有火焰（重新定位）、不足則補生
        for (int i = 0; i < need; i++)
        {
            if (i < _flameVfx.Count && _flameVfx[i] != null)
            {
                _flameVfx[i].transform.position = _flamePosBuffer[i];
            }
            else
            {
                VfxInstance inst = _vfxManager.Spawn(weapon.TrailEffectID, _flamePosBuffer[i], 0f);
                GameObject go = (inst != null) ? inst.gameObject : null;
                if (i < _flameVfx.Count) _flameVfx[i] = go;
                else _flameVfx.Add(go);
            }
        }
    }

    // 沿折線路徑每隔 step 取樣一個點。供火焰柱沿光束鋪設用。
    // 第一根從砲口前方 step 處開始（不放在砲口/角色身上，避免第一顆火焰被壓在角色身下）。
    // 註：這只影響火焰「視覺」位置；命中判定由 LaserBeam 的 CircleCast + 砲口 OverlapCircle 處理，貼身怪照樣打得到。
    private static void SampleAlongPath(System.Collections.Generic.IReadOnlyList<Vector2> points, float step, List<Vector2> outPositions)
    {
        if (points == null || points.Count < 2 || step <= 0f) return;
        float nextAt = step;   // 跳過起點(角色位置)，第一根火焰在前方 step 處
        float cum = 0f;
        for (int s = 0; s < points.Count - 1; s++)
        {
            Vector2 a = points[s], b = points[s + 1];
            float segLen = Vector2.Distance(a, b);
            if (segLen < 1e-5f) continue;
            Vector2 dir = (b - a) / segLen;
            float segEnd = cum + segLen;
            while (nextAt <= segEnd)
            {
                outPositions.Add(a + dir * (nextAt - cum));
                nextAt += step;
            }
            cum = segEnd;
        }
    }

    private void ClearActiveFlames()
    {
        for (int i = 0; i < _flameVfx.Count; i++)
            if (_flameVfx[i] != null) Destroy(_flameVfx[i]);
        _flameVfx.Clear();
    }

    private void SpawnLaserBeams(WeaponData weapon)
    {
        ProjectileData recipe = weapon.Recipe.Data;
        int count = Mathf.Max(1, recipe.SplitCount);
        float totalSpread = recipe.SpreadAngle;

        LayerMask collisionMask = EnvLayer | EnemyLayer;
        LayerMask pierceableLayers = ResolvePierceableLayers(weapon.Recipe);
        LayerMask nonBounceLayers = ResolveNonBounceLayers(weapon.Recipe.BounceTarget);

        float width = weapon.BeamWidth * PlayerScale;
        WeaponData firedWeapon = weapon;

        _activeBeamWeapon = weapon;
        _beamAngleOffsets.Clear();

        Vector2 origin = (Vector2)transform.position;
        for (int i = 0; i < count; i++)
        {
            float offset = 0f;
            if (count > 1)
            {
                float t = (float)i / (count - 1);
                offset = -totalSpread * 0.5f + totalSpread * t;
            }
            _beamAngleOffsets.Add(offset);

            // TrailEffectID > 0 = 火焰噴射器模式：不畫光束 mesh，改沿路徑鋪火焰 Vfx
            bool drawBeam = weapon.TrailEffectID <= 0;
            LaserBeam beam = BallisticsEngine.SpawnBeam(
                recipe, origin, Vector2.right,
                collisionMask, pierceableLayers, nonBounceLayers,
                weapon.BeamStyle, weapon.BeamColor, width,
                weapon.BeamMuzzleSprite, weapon.BeamImpactSprite,
                (b, hits) => HandleBeamTick(firedWeapon, b, hits),
                drawBeam);
            // 環境命中(牆/可破壞地上物)走獨立回呼：只用來扣可破壞物的血，不在牆上噴擊中特效/分裂
            if (beam != null)
                beam.OnBeamEnvironmentTick = (b, envHits) => HandleBeamEnvironment(firedWeapon, envHits);
            _activeBeams.Add(beam);
        }
    }

    private void ClearActiveBeams()
    {
        for (int i = 0; i < _activeBeams.Count; i++)
            if (_activeBeams[i] != null) Destroy(_activeBeams[i].gameObject);
        _activeBeams.Clear();
        _beamAngleOffsets.Clear();
        _activeBeamWeapon = null;
        ClearActiveFlames();   // 火焰噴射器：放開/切武器時一併清掉火焰柱
    }

    // 光束每 DotInterval 秒回報一次當下命中清單 → 在此結算傷害 / 地面特效 / OnHit 分裂
    private void HandleBeamTick(WeaponData firedWeapon, LaserBeam beam, List<LaserBeam.BeamHit> hits)
    {
        if (firedWeapon == null) return;

        for (int i = 0; i < hits.Count; i++)
        {
            GameObject target = hits[i].Target;
            if (target == null) continue;
            Vector2 point = hits[i].Point;

            int hitLayerBit = 1 << target.layer;
            bool hitEnemy = (hitLayerBit & EnemyLayer.value) != 0;
            bool hitEnv = (hitLayerBit & EnvLayer.value) != 0;

            // 統一傷害（怪物 + 可破壞地上物若混在 _hits 中）；無敵時間由各自 TakeDamage 內部處理
            Vector2 hitDir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            ApplyDamage(target, firedWeapon.Damage, hitDir);

            TryTriggerGroundEffect(firedWeapon.Recipe, GroundEffectTrigger.OnHit, point, hitEnemy, hitEnv, false);
            TrySpawnHitEffect(firedWeapon, point);
            TrySpawnBeamSplit(firedWeapon, point);
        }
    }

    // 雷射的環境命中回呼：對可破壞地上物扣血（每 DotInterval tick 一次），不噴擊中特效/不分裂
    private void HandleBeamEnvironment(WeaponData firedWeapon, List<LaserBeam.BeamHit> envHits)
    {
        if (firedWeapon == null || envHits == null) return;
        for (int i = 0; i < envHits.Count; i++)
        {
            GameObject target = envHits[i].Target;
            if (target == null) continue;
            Vector2 dir = (envHits[i].Point - (Vector2)transform.position).normalized;
            ApplyDamage(target, firedWeapon.Damage, dir);
        }
    }

    // 統一傷害入口：對任何實作 IDamageable 的目標(怪物 / 可破壞地上物)結算傷害;牆等無此元件者略過。
    private static void ApplyDamage(GameObject target, float damage, Vector2 hitDir)
    {
        if (target == null || damage <= 0f) return;
        IDamageable d = target.GetComponent<IDamageable>();
        if (d != null) d.TakeDamage(damage, hitDir);
    }

    // OnHit 分裂：在命中點依 SpreadCount/SpreadAngle 射出 SubRecipeID 子彈（節流已綁在 DotInterval tick）
    private void TrySpawnBeamSplit(WeaponData firedWeapon, Vector2 point)
    {
        RecipeEntry recipe = firedWeapon.Recipe;
        if (recipe == null) return;
        ProjectileData data = recipe.Data;
        if (data == null || !data.HasSplit || data.Timing != SplitTiming.OnHit || data.SubProjectileData == null)
            return;
        if (firedWeapon.BulletPrefab == null) return;

        int count = Mathf.Max(1, data.SplitCount);
        float totalSpread = data.SpreadAngle;
        Vector2 baseDir = point - (Vector2)transform.position;
        baseDir = baseDir.sqrMagnitude > 0.0001f ? baseDir.normalized : Vector2.right;
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        ProjectileData sub = data.SubProjectileData;
        GameObject prefab = firedWeapon.BulletPrefab;
        LayerMask collisionMask = EnvLayer | EnemyLayer;
        LayerMask pierceableLayers = EnemyLayer;
        LayerMask nonBounceLayers = EnvLayer | EnemyLayer;
        Vector3 scale = prefab.transform.localScale * PlayerScale * firedWeapon.BulletScale;
        WeaponData fw = firedWeapon;

        for (int i = 0; i < count; i++)
        {
            float ang = baseAngle;
            if (count > 1)
            {
                float t = (float)i / (count - 1);
                ang = baseAngle - totalSpread * 0.5f + totalSpread * t;
            }
            float r = ang * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(r), Mathf.Sin(r));

            BallisticsEngine.Spawn(sub, prefab, point, dir,
                collisionMask, pierceableLayers, nonBounceLayers,
                (b, t2, h) => HandleBulletHit(fw, b, t2, h),
                fw.WeaponSprite, fw.SpriteAngleOffset, scale, fw.WeaponSprites, fw.AnimFPS);
        }
    }

    private void ShootParabolic(WeaponData weapon, ProjectileData recipe)
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        Vector2 mouseTarget = (Vector2)mousePos;

        int count = Mathf.Max(1, recipe.SplitCount);
        float totalSpreadDeg = recipe.SpreadAngle;
        float scatterRadius = Mathf.Max(0f, recipe.LandingScatterRadius);

        LaunchSource launchSrc = weapon.Recipe.LaunchSource;

        // 扇形目標永遠以「玩家 → 滑鼠」為基準，即使 LaunchSource = Offscreen 也以玩家視角來分扇形
        Vector2 fanReference = (Vector2)transform.position;
        Vector2 baseDir = mouseTarget - fanReference;
        float distance = baseDir.magnitude;
        if (distance < 0.0001f)
        {
            baseDir = Vector2.right;
            distance = 1f;
        }
        else
        {
            baseDir = baseDir.normalized;
        }
        float baseAngleDeg = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        LayerMask collisionMask = EnvLayer | EnemyLayer;
        LayerMask pierceableLayers = 0;
        LayerMask nonBounceLayers = 0;
        Vector3 bulletScale = weapon.BulletPrefab.transform.localScale * PlayerScale * weapon.BulletScale;
        WeaponData firedWeapon = weapon;

        for (int i = 0; i < count; i++)
        {
            // 計算扇形角度
            float angleDeg = baseAngleDeg;
            if (count > 1)
            {
                float t = (float)i / (count - 1);
                angleDeg = baseAngleDeg - totalSpreadDeg * 0.5f + totalSpreadDeg * t;
            }
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 spreadDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 spreadTarget = fanReference + spreadDir * distance;

            // 落點隨機誤差（圓盤內均勻分布）
            Vector2 scatter = scatterRadius > 0f ? (Vector2)Random.insideUnitCircle * scatterRadius : Vector2.zero;
            Vector2 finalTarget = spreadTarget + scatter;

            // 起點：Player 共用玩家位置；Offscreen 每顆都重抽一個視野外的隨機點
            Vector2 startPos = (launchSrc == LaunchSource.Offscreen)
                ? ResolveParabolicStartPos(LaunchSource.Offscreen, finalTarget)
                : fanReference;

            Vector2 fireDir = finalTarget - startPos;
            fireDir = (fireDir.sqrMagnitude < 0.0001f) ? Vector2.right : fireDir.normalized;

            BulletInstance bullet = BallisticsEngine.Spawn(recipe, weapon.BulletPrefab, startPos, fireDir,
                collisionMask, pierceableLayers, nonBounceLayers,
                null, // 拋物線不走 OnHit 流程
                weapon.WeaponSprite, weapon.SpriteAngleOffset, bulletScale, weapon.WeaponSprites, weapon.AnimFPS,
                (b, pos) => TrySpawnTrailEffect(firedWeapon, pos));

            if (bullet == null) continue;

            foreach (var b in bullet.GetBehaviors())
            {
                if (b is ParabolicBehavior parabolic)
                {
                    parabolic.Initialize(startPos, finalTarget);
                    break;
                }
            }

            bullet.OnGroundLanded += (b, landPos) => HandleParabolicLanded(firedWeapon, b, landPos);
        }
    }

    private Vector2 ResolveParabolicStartPos(LaunchSource source, Vector2 targetPos)
    {
        if (source != LaunchSource.Offscreen)
            return (Vector2)transform.position;

        Camera cam = Camera.main;
        if (cam == null) return (Vector2)transform.position;

        // 攝影機視野邊界（orthographic）
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector2 camCenter = (Vector2)cam.transform.position;

        // 隨機方向，從攝影機中心射出，找出射線出視野的距離 + 1 單位緩衝
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        const float kEpsilon = 1e-4f;
        float tX = halfW / Mathf.Max(kEpsilon, Mathf.Abs(dir.x));
        float tY = halfH / Mathf.Max(kEpsilon, Mathf.Abs(dir.y));
        float t = Mathf.Min(tX, tY) + 1f; // +1 單位讓起點明確在視野外

        return camCenter + dir * t;
    }

    private void ShootOrbital(WeaponData weapon, ProjectileData recipe)
    {
        ClearActiveOrbitalBullets();

        int count = recipe.OrbitalCount;
        float radius = recipe.OrbitalRadius;

        LayerMask collisionMask = EnvLayer | EnemyLayer;
        LayerMask pierceableLayers = ResolvePierceableLayers(weapon.Recipe);
        LayerMask nonBounceLayers = ResolveNonBounceLayers(weapon.Recipe.BounceTarget);
        Vector3 bulletScale = weapon.BulletPrefab.transform.localScale * PlayerScale * weapon.BulletScale;
        WeaponData firedWeapon = weapon;

        for (int i = 0; i < count; i++)
        {
            float angle = i * (2f * Mathf.PI / count);
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Vector2 spawnPos = (Vector2)transform.position + offset;
            Vector2 tangent = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));

            BulletInstance bullet = BallisticsEngine.Spawn(recipe, weapon.BulletPrefab, spawnPos, tangent,
                collisionMask, pierceableLayers, nonBounceLayers,
                (b, t, h) => HandleBulletHit(firedWeapon, b, t, h),
                weapon.WeaponSprite, weapon.SpriteAngleOffset, bulletScale, weapon.WeaponSprites, weapon.AnimFPS,
                (b, pos) => TrySpawnTrailEffect(firedWeapon, pos));

            if (bullet != null)
            {
                // 個別子彈不依 LifeTime 自動銷毀，改由 PlayerController 群組到期統一銷毀
                bullet.LifeTime = -1f;
                _activeOrbitalBullets.Add(bullet);
                foreach (var b in bullet.GetBehaviors())
                {
                    if (b is OrbitalBehavior orbital)
                    {
                        orbital.Initialize(transform, angle);
                        break;
                    }
                }
            }
        }

        if (recipe.LifeTime >= 0f && _activeOrbitalBullets.Count > 0)
            _orbitalGroupExpireTime = Time.time + recipe.LifeTime;
        else
            _orbitalGroupExpireTime = -1f;
    }

    // ── 連鎖閃電：吃配方的散射（SpreadCount/SpreadAngle）→ 一發射出多道扇形分布的連鎖，每道各自獨立連鎖 ──
    // 目標搜尋與傷害全在主遊戲側（守住「彈道系統不算傷害」邊界）；LaserBeam 只當折線視覺（SpawnChainVisual）。
    private void ShootChain(WeaponData weapon, ProjectileData recipe)
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        Vector2 origin = (Vector2)transform.position;
        Vector2 baseDir = (Vector2)mousePos - origin;
        baseDir = baseDir.sqrMagnitude > 0.0001f ? baseDir.normalized : Vector2.right;
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        int count = Mathf.Max(1, recipe.SplitCount);   // 散射道數（= SpreadCount 欄）
        float totalSpread = recipe.SpreadAngle;        // 扇形總角度（= SpreadAngle 欄）

        for (int i = 0; i < count; i++)
        {
            float ang = baseAngle;
            if (count > 1)
            {
                float t = (float)i / (count - 1);
                ang = baseAngle - totalSpread * 0.5f + totalSpread * t;
            }
            float rad = ang * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            CastOneChain(weapon, recipe, origin, dir);
        }
    }

    // 放一道連鎖閃電：朝 dir 找首節點（HasHoming 時用扇形錐 aim-assist）→ 逐跳 → 結算傷害 → 畫鋸齒折線。
    private void CastOneChain(WeaponData weapon, ProjectileData recipe, Vector2 origin, Vector2 dir)
    {
        float reach = recipe.BeamRange;                       // 第一段射程（BeamRange 欄）
        float aimR = Mathf.Max(0.1f, weapon.BeamWidth * 0.5f); // 第一段瞄準容差半徑
        int envMask = EnvLayer.value;        // 環境：牆（不可破壞、擋路）+ 可破壞地上物（有 IDamageable）
        int enemyMask = EnemyLayer.value;
        int damageableMask = enemyMask | envMask; // 連鎖搜尋對象：怪 + 可破壞地上物（牆會被 IDamageable 過濾掉）

        var hitSet = new HashSet<int>();   // 首目標搜尋的排除集（此處為空）
        Transform firstTarget = null;

        // 追蹤(aim-assist)：閃電瞬發、不會飛行轉彎，所以「追蹤」對它＝首目標自動鎖定。
        // HasHoming 時首段不必正好瞄到，改鎖定「以 dir 為軸、半角 = HomingTurnSpeed(上限180) 的扇形錐內、reach 內、最近」的目標。
        // 180 = 錐張滿一圈 = 鎖最近任意方向的目標。（目前不檢查中間有沒有牆遮擋，之後可加。）
        if (recipe.HasHoming)
        {
            float coneHalf = Mathf.Min(180f, recipe.HomingTurnSpeed);
            firstTarget = FindNearestInCone(origin, reach, dir, coneHalf, hitSet, damageableMask);
        }

        // 非追蹤（或追蹤沒鎖到）：朝 dir 直線找第一個目標，環境(牆/家具)會擋住前進；純牆則無首節點。
        if (firstTarget == null)
        {
            RaycastHit2D env = Physics2D.Raycast(origin, dir, reach, envMask);
            float maxDist = (env.collider != null) ? env.distance : reach;
            RaycastHit2D enemyHit = Physics2D.CircleCast(origin, aimR, dir, maxDist, enemyMask);

            if (enemyHit.collider != null && (env.collider == null || enemyHit.distance <= env.distance))
                firstTarget = enemyHit.collider.transform;
            else if (env.collider != null && env.collider.GetComponent<IDamageable>() != null)
                firstTarget = env.collider.transform;   // 擋路的可破壞家具（牆沒有 IDamageable，不會進來）

            // 貼身目標補抓：queriesStartInColliders=false 會略過重疊在起點的目標（同雷射的陷阱）
            if (firstTarget == null)
                firstTarget = FindNearestDamageable(origin, aimR, hitSet, damageableMask);
        }

        if (firstTarget != null)
        {
            // 連鎖行為參數用本武器自己的配方（ChainCount/ChainRadius）
            RunChain(weapon, origin, firstTarget, weapon.Recipe.ChainCount, weapon.Recipe.ChainRadius);
        }
        else
        {
            // 沒命中任何目標：畫一道到牆/射程末端的閃電（純視覺，讓玩家看到有發射出去）
            RaycastHit2D w = Physics2D.Raycast(origin, dir, reach, envMask);
            float md = (w.collider != null) ? w.distance : reach;
            _chainPathBuffer.Clear();
            _chainPathBuffer.Add(origin);
            _chainPathBuffer.Add(origin + dir * md);
            List<Vector2> jagged = BuildJaggedPath(_chainPathBuffer);
            BallisticsEngine.SpawnChainVisual(jagged, weapon.BeamStyle, weapon.BeamColor,
                weapon.BeamWidth * PlayerScale, weapon.BeamMuzzleSprite, weapon.BeamImpactSprite, ChainFlashDuration);
        }
    }

    // 從 firstTarget 起連鎖：逐跳找最近可傷害目標、結算傷害、畫鋸齒折線。
    // startPoint = 折線起點（玩家位置 or 天降雷擊落點）；用本武器 Damage + 外觀，連鎖次數/半徑由呼叫者傳入。
    // 連鎖閃電武器走自己的配方；天降雷擊接 SubRecipeID 時，由雷擊在落點呼叫此方法、傳入 sub-recipe 的次數/半徑。
    private void RunChain(WeaponData weapon, Vector2 startPoint, Transform firstTarget, int chainCount, float chainRadius)
    {
        int dmgMask = EnemyLayer.value | EnvLayer.value;
        var targets = new List<Transform>();
        var hitSet = new HashSet<int>();
        targets.Add(firstTarget);
        hitSet.Add(firstTarget.gameObject.GetInstanceID());

        Vector2 from = firstTarget.position;
        for (int j = 0; j < Mathf.Max(0, chainCount); j++)
        {
            Transform next = FindNearestDamageable(from, chainRadius, hitSet, dmgMask);
            if (next == null) break;
            targets.Add(next);
            hitSet.Add(next.gameObject.GetInstanceID());
            from = next.position;
        }

        Vector2 prev = startPoint;
        for (int i = 0; i < targets.Count; i++)
        {
            Vector2 tp = targets[i].position;
            Vector2 hd = (tp - prev).sqrMagnitude > 0.0001f ? (tp - prev).normalized : Vector2.up;
            ApplyDamage(targets[i].gameObject, weapon.Damage, hd);
            TrySpawnHitEffect(weapon, tp);
            prev = tp;
        }

        _chainPathBuffer.Clear();
        _chainPathBuffer.Add(startPoint);
        for (int i = 0; i < targets.Count; i++) _chainPathBuffer.Add(targets[i].position);
        List<Vector2> jagged = BuildJaggedPath(_chainPathBuffer);
        BallisticsEngine.SpawnChainVisual(jagged, weapon.BeamStyle, weapon.BeamColor,
            weapon.BeamWidth * PlayerScale, weapon.BeamMuzzleSprite, weapon.BeamImpactSprite, ChainFlashDuration);
    }

    // 從一點找「半徑內、不在排除清單、有 IDamageable、最近」的目標（連鎖閃電每一跳的搜尋）。
    // 用 IDamageable 過濾 → 怪物與可破壞地上物都算，純牆（無 IDamageable）被排除、不會浪費跳躍。
    private static Transform FindNearestDamageable(Vector2 from, float radius, HashSet<int> exclude, int mask)
    {
        Collider2D[] cols = Physics2D.OverlapCircleAll(from, radius, mask);
        float best = float.MaxValue;
        Transform bestT = null;
        for (int i = 0; i < cols.Length; i++)
        {
            if (exclude.Contains(cols[i].gameObject.GetInstanceID())) continue;
            if (cols[i].GetComponent<IDamageable>() == null) continue;   // 牆等無 IDamageable 者排除
            float d = ((Vector2)cols[i].transform.position - from).sqrMagnitude;
            if (d < best) { best = d; bestT = cols[i].transform; }
        }
        return bestT;
    }

    // 連鎖閃電的追蹤(aim-assist)：找「以 axisDir 為軸、半角 halfAngleDeg 的扇形錐內、radius 內、有 IDamageable、最近」的目標。
    // halfAngleDeg = 180 → 錐張滿一圈（等於 FindNearestDamageable，鎖最近任意方向）。
    private static Transform FindNearestInCone(Vector2 from, float radius, Vector2 axisDir, float halfAngleDeg, HashSet<int> exclude, int mask)
    {
        Vector2 axis = axisDir.sqrMagnitude > 1e-4f ? axisDir.normalized : Vector2.right;
        float cosHalf = Mathf.Cos(Mathf.Clamp(halfAngleDeg, 0f, 180f) * Mathf.Deg2Rad);
        Collider2D[] cols = Physics2D.OverlapCircleAll(from, radius, mask);
        float best = float.MaxValue;
        Transform bestT = null;
        for (int i = 0; i < cols.Length; i++)
        {
            if (exclude.Contains(cols[i].gameObject.GetInstanceID())) continue;
            if (cols[i].GetComponent<IDamageable>() == null) continue;
            Vector2 to = (Vector2)cols[i].transform.position - from;
            float sqr = to.sqrMagnitude;
            if (sqr < 1e-6f) return cols[i].transform;          // 重疊在原點，直接取
            if (Vector2.Dot(to / Mathf.Sqrt(sqr), axis) < cosHalf) continue; // 不在扇形錐內
            if (sqr < best) { best = sqr; bestT = cols[i].transform; }
        }
        return bestT;
    }

    // 在每段之間插入橫向抖動中點 → 閃電鋸齒外觀（純視覺，不影響命中）。
    // 細分數依「段長」決定（每約 segPerJag 單位一個鋸齒點），所以短段不過密、長段（如天降雷擊的長垂直閃電）也夠鋸齒。
    private static List<Vector2> BuildJaggedPath(List<Vector2> pts)
    {
        var outp = new List<Vector2>(pts.Count * 6);
        const float jitter = 0.22f;     // 橫向抖動幅度（世界單位）
        const float segPerJag = 0.6f;   // 每約 0.6 單位一個鋸齒點
        for (int s = 0; s < pts.Count - 1; s++)
        {
            Vector2 a = pts[s], b = pts[s + 1];
            outp.Add(a);
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len > 1e-3f)
            {
                int subdiv = Mathf.Clamp(Mathf.RoundToInt(len / segPerJag), 2, 32);
                Vector2 perp = new Vector2(-d.y, d.x) / len;
                for (int k = 1; k < subdiv; k++)
                {
                    float t = (float)k / subdiv;
                    float off = (Random.value * 2f - 1f) * jitter;
                    outp.Add(a + d * t + perp * off);
                }
            }
        }
        outp.Add(pts[pts.Count - 1]);
        return outp;
    }

    // ── 天降雷擊：從畫面上緣外劈下到滑鼠所在點，落地以 BlastRadius 做圓形 AOE ──
    // 吃 SpreadCount/SpreadAngle（多道落點，仿拋物線扇形分佈）與 HomingTurnSpeed（落點吸附最近怪，當搜尋半徑）。
    // 目標搜尋與傷害全在主遊戲側；視覺複用 LaserBeam 折線（垂直鋸齒閃電）。
    private void ShootSkyStrike(WeaponData weapon, ProjectileData recipe)
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        Vector2 player = (Vector2)transform.position;
        Vector2 mouse = (Vector2)mousePos;

        int count = Mathf.Max(1, recipe.SplitCount);   // 落點道數（= SpreadCount 欄）
        float totalSpread = recipe.SpreadAngle;        // 扇形總角度（= SpreadAngle 欄）

        // 落點分佈：以「玩家→滑鼠」為基準軸，N 個落點在 ±SpreadAngle/2 的扇形上、與滑鼠等距（同拋物線）
        Vector2 axis = mouse - player;
        float dist = axis.magnitude;
        float baseAngle;
        if (dist < 0.0001f) { dist = 1f; baseAngle = 90f; }
        else baseAngle = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;

        int dmgMask = EnemyLayer.value | EnvLayer.value;

        for (int i = 0; i < count; i++)
        {
            float ang = baseAngle;
            if (count > 1)
            {
                float t = (float)i / (count - 1);
                ang = baseAngle - totalSpread * 0.5f + totalSpread * t;
            }
            float rad = ang * Mathf.Deg2Rad;
            Vector2 target = player + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * dist;

            // 追蹤(aim-assist)：HasHoming 時把落點吸附到附近最近的可傷害目標（HomingTurnSpeed 當搜尋半徑，世界單位）
            if (recipe.HasHoming)
            {
                Transform near = FindNearestDamageable(target, recipe.HomingTurnSpeed, _emptyHitSet, dmgMask);
                if (near != null) target = near.position;
            }

            StrikeAt(weapon, target, dmgMask);
        }
    }

    // 在 impact 點劈一道雷：垂直鋸齒閃電視覺 + 圓形 AOE 傷害（武器 Damage，含怪與可破壞家具）+ 可選地面特效。
    private void StrikeAt(WeaponData weapon, Vector2 impact, int dmgMask)
    {
        // 1) 視覺：從畫面上緣外往下劈到 impact 的鋸齒閃電（複用靜態折線渲染）
        Camera cam = Camera.main;
        float topY = (cam != null) ? cam.transform.position.y + cam.orthographicSize + SkyStrikeTopMargin : impact.y + 12f;
        _chainPathBuffer.Clear();
        _chainPathBuffer.Add(new Vector2(impact.x, topY));
        _chainPathBuffer.Add(impact);
        List<Vector2> jagged = BuildJaggedPath(_chainPathBuffer);
        BallisticsEngine.SpawnChainVisual(jagged, weapon.BeamStyle, weapon.BeamColor,
            weapon.BeamWidth * PlayerScale, weapon.BeamMuzzleSprite, weapon.BeamImpactSprite, ChainFlashDuration);

        // 2) 擊中特效（落點）
        TrySpawnHitEffect(weapon, impact);

        // 3) 圓形 AOE：以 BlastRadius（留空用預設）對範圍內 IDamageable（怪 + 可破壞家具）以武器 Damage 結算一次
        float radius = (weapon.Recipe != null && weapon.Recipe.BlastRadius > 0f) ? weapon.Recipe.BlastRadius : SkyStrikeDefaultBlast;
        if (weapon.Damage > 0f)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(impact, radius, dmgMask);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == null) continue;
                IDamageable d = hits[i].GetComponent<IDamageable>();
                if (d == null) continue;
                Vector2 hd = ((Vector2)hits[i].transform.position - impact).normalized;
                d.TakeDamage(weapon.Damage, hd);
            }
        }

        // 4) 可選：落點留一團地面特效（GroundEffectID > 0 時），例如焦痕/殘電
        if (_groundEffectManager != null && weapon.Recipe != null && weapon.Recipe.GroundEffectID > 0)
            _groundEffectManager.Spawn(weapon.Recipe.GroundEffectID, impact);

        // 5) SubRecipeID → 連鎖：落點接一條連鎖閃電轟擊旁邊的怪（用本武器 Damage/外觀，sub-recipe 的 ChainCount/ChainRadius）。
        //    首目標 = 落點 ChainRadius 內最近的可傷害目標；找到才連，之後逐跳。
        RecipeEntry sub = (weapon.Recipe != null) ? weapon.Recipe.SubRecipe : null;
        if (sub != null && sub.IsChain)
        {
            Transform first = FindNearestDamageable(impact, sub.ChainRadius, _emptyHitSet, dmgMask);
            if (first != null)
            {
                // 連鎖的外觀＋傷害＝「定義該連鎖配方的那把武器」（例如連鎖閃電武器，藍/白），找不到才退回本武器（雷擊）。
                // → 雷擊本身與接出來的連鎖可各有顏色/粗細/傷害。
                WeaponData chainWeapon = (_weaponManager != null) ? _weaponManager.GetWeaponByRecipeID(sub.ID) : null;
                if (chainWeapon == null) chainWeapon = weapon;
                RunChain(chainWeapon, impact, first, sub.ChainCount, sub.ChainRadius);
            }
        }
    }

    private LayerMask ResolvePierceableLayers(RecipeEntry recipe)
    {
        LayerMask layers = EnemyLayer;
        if (recipe != null && !recipe.BlockedByEnvironment)
            layers |= EnvLayer;
        return layers;
    }

    private LayerMask ResolveNonBounceLayers(BounceTarget bounceTarget)
    {
        return bounceTarget switch
        {
            BounceTarget.Environment => EnemyLayer,
            BounceTarget.Enemy => EnvLayer,
            _ => EnvLayer | EnemyLayer
        };
    }

    void HandleBulletHit(WeaponData firedWeapon, BulletInstance bullet, GameObject target, RaycastHit2D hit)
    {
        if (firedWeapon == null) return;

        int hitLayerBit = 1 << target.layer;
        bool hitEnemy = (hitLayerBit & EnemyLayer.value) != 0;
        bool hitEnv = (hitLayerBit & EnvLayer.value) != 0;

        // 統一傷害：怪物與可破壞地上物都實作 IDamageable;牆等無此元件者自動略過
        Vector2 hitDir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
        ApplyDamage(target, firedWeapon.Damage, hitDir);

        Vector2 spawnPos = (hit.point != Vector2.zero) ? hit.point : (Vector2)bullet.transform.position;
        TryTriggerGroundEffect(firedWeapon.Recipe, GroundEffectTrigger.OnHit, spawnPos, hitEnemy, hitEnv, false);
        TrySpawnHitEffect(firedWeapon, spawnPos);
        TryTriggerSubWeapon(firedWeapon, bullet, hit, spawnPos, hitEnemy, hitEnv);
    }

    // ── 命中迸發子武器：子彈命中（依 SubWeaponHitTarget 過濾）時，在命中點生成「另一把武器」的子彈 ──
    // 與 SubRecipeID 不同：子武器是「武器表上的武器」，自帶外型/傷害/追蹤（蜂巢→自己的蜜蜂圖）。
    private void TryTriggerSubWeapon(WeaponData firedWeapon, BulletInstance bullet, RaycastHit2D hit, Vector2 spawnPos, bool hitEnemy, bool hitEnv)
    {
        RecipeEntry recipe = firedWeapon.Recipe;
        if (recipe == null || recipe.SubWeaponOnHit <= 0 || _weaponManager == null) return;

        bool allowed = recipe.SubWeaponHitTarget switch
        {
            SubWeaponHitTarget.All => hitEnemy || hitEnv,
            SubWeaponHitTarget.Environment => hitEnv,
            _ => hitEnemy   // Enemy / 預設
        };
        if (!allowed) return;

        WeaponData subWeapon = _weaponManager.GetWeapon(recipe.SubWeaponOnHit);
        if (subWeapon == null) return;

        // 迸發基準方向：從命中面法線往外噴（家具/牆會給法線）；沒有法線就用母彈反向速度，再不行就朝上。
        Vector2 baseDir = hit.normal;
        if (baseDir.sqrMagnitude < 0.0001f)
        {
            Vector2 vel = (bullet != null) ? bullet.Velocity : Vector2.zero;
            baseDir = vel.sqrMagnitude > 0.0001f ? -vel.normalized : Vector2.up;
        }
        else baseDir = baseDir.normalized;

        // 把生成點沿法線往外推離命中面：否則子武器生在牆/家具表面內，一生成就被 CheckSpawnOverlap 判定撞到該面而瞬間銷毀
        //（牆是實心永遠在，所以打牆時子武器一生就死＝看起來沒生出來）。推出去的距離至少要蓋過子武器自己的判定半徑。
        float subRadius = (subWeapon.Recipe != null && subWeapon.Recipe.Data != null) ? subWeapon.Recipe.Data.Radius : 0.1f;
        float offset = Mathf.Max(SubWeaponSpawnOffset, subRadius + 0.2f);
        Vector2 spawnAt = spawnPos + baseDir * offset;

        SpawnSubWeaponAt(subWeapon, spawnAt, baseDir);
    }

    // 在 pos 以 baseDir 為基準發射「子武器」一發（吃子武器自己的配方行為：散射/追蹤/反彈…與外型/傷害）。
    // 子武器若是 3 分裂(OnSpawn) 追蹤 → 自動迸成 3 隻會追蹤的「蜜蜂」（用子武器自己的圖）。
    private void SpawnSubWeaponAt(WeaponData subWeapon, Vector2 pos, Vector2 baseDir)
    {
        if (subWeapon == null || subWeapon.BulletPrefab == null || subWeapon.Recipe == null) return;
        ProjectileData subRecipe = subWeapon.Recipe.Data;
        if (subRecipe == null) return;

        // 子武器只支援「會飛的一般子彈」；特殊型(雷射/環繞/拋物線/連鎖/雷擊)不適合當 OnHit 迸發，先擋掉並提示。
        if (subRecipe.IsLaser || subRecipe.IsOrbital || subRecipe.IsParabolic
            || subWeapon.Recipe.IsChain || subWeapon.Recipe.IsSkyStrike)
        {
            Debug.LogWarning($"SubWeaponOnHit 指向的武器 '{subWeapon.Name}' 是特殊型(雷射/環繞/拋物線/連鎖/雷擊)，命中迸發目前只支援一般飛行子彈。");
            return;
        }

        LayerMask collisionMask = EnvLayer | EnemyLayer;
        LayerMask pierceableLayers = ResolvePierceableLayers(subWeapon.Recipe);
        LayerMask nonBounceLayers = ResolveNonBounceLayers(subWeapon.Recipe.BounceTarget);
        Vector3 scale = subWeapon.BulletPrefab.transform.localScale * PlayerScale * subWeapon.BulletScale;
        WeaponData fired = subWeapon;

        BallisticsEngine.Spawn(subRecipe, subWeapon.BulletPrefab, pos, baseDir,
            collisionMask, pierceableLayers, nonBounceLayers,
            (b, t, h) => HandleBulletHit(fired, b, t, h),
            subWeapon.WeaponSprite, subWeapon.SpriteAngleOffset, scale, subWeapon.WeaponSprites, subWeapon.AnimFPS,
            (b, p) => TrySpawnTrailEffect(fired, p));
    }

    private void HandleParabolicLanded(WeaponData firedWeapon, BulletInstance bullet, Vector2 landPos)
    {
        if (firedWeapon == null) return;
        // 拋物線最終落地：飛行中不撞怪，落地才以 BlastRadius 做一次性 AOE 殺傷（可選），再觸發地面特效與擊中特效
        TryApplyParabolicBlast(firedWeapon, landPos);
        TryTriggerGroundEffect(firedWeapon.Recipe, GroundEffectTrigger.OnHit, landPos, false, false, true);
        TrySpawnHitEffect(firedWeapon, landPos);
    }

    // 拋物線落地殺傷：BlastRadius > 0 時，對落點半徑內怪物以武器 Damage 炸一次。
    // 與地面火堆（GroundEffect）獨立——可同時「炸傷一次 ＋ 留一灘火延燒」。傷害只結算在怪物上、吃怪物無敵時間、擊退方向由爆心朝外。
    private void TryApplyParabolicBlast(WeaponData firedWeapon, Vector2 landPos)
    {
        if (firedWeapon == null || firedWeapon.Recipe == null) return;
        float radius = firedWeapon.Recipe.BlastRadius;
        if (radius <= 0f || firedWeapon.Damage <= 0f) return;

        // 範圍含怪物與地上物(Environment),兩者都實作 IDamageable
        Collider2D[] hits = Physics2D.OverlapCircleAll(landPos, radius, EnemyLayer | EnvLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null) continue;
            IDamageable d = col.GetComponent<IDamageable>();
            if (d == null) continue;

            Vector2 hitDir = ((Vector2)col.transform.position - landPos).normalized;
            d.TakeDamage(firedWeapon.Damage, hitDir);
        }
    }

    private void TryTriggerGroundEffect(RecipeEntry recipe, GroundEffectTrigger trigger, Vector2 spawnPos, bool hitEnemy, bool hitEnv, bool hitGround)
    {
        if (recipe == null || _groundEffectManager == null) return;
        if (recipe.GroundEffectID <= 0) return;
        if (recipe.GroundEffectTrigger != trigger) return;

        // 首版只支援 OnHit；OnSpawn / OnDeath 待事件鉤子補完。
        if (trigger != GroundEffectTrigger.OnHit)
        {
            Debug.LogWarning($"GroundEffectTrigger '{trigger}' is not yet implemented; only 'OnHit' is supported in this version.");
            return;
        }

        // 命中目標 layer 必須符合 GroundEffectHitTarget 設定才觸發
        bool allowed = recipe.GroundEffectHitTarget switch
        {
            GroundEffectHitTarget.Any => hitEnemy || hitEnv,
            GroundEffectHitTarget.Environment => hitEnv,
            GroundEffectHitTarget.Ground => hitGround,
            _ => hitEnemy // Enemy / 預設
        };
        if (!allowed) return;

        _groundEffectManager.Spawn(recipe.GroundEffectID, spawnPos);
    }

    // ── 一次性特效（VFX）：發射特效生在玩家身上、擊中特效生在命中點，皆讀發射快照武器 ──
    private Vector2 AimDirectionToMouse()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        Vector2 dir = (Vector2)mousePos - (Vector2)transform.position;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
    }

    private void TrySpawnFireEffect(WeaponData weapon, Vector2 aimDir)
    {
        if (_vfxManager == null || weapon == null || weapon.FireEffectID <= 0) return;
        float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        _vfxManager.Spawn(weapon.FireEffectID, transform.position, angle);
    }

    private void TrySpawnHitEffect(WeaponData firedWeapon, Vector2 pos)
    {
        if (_vfxManager == null || firedWeapon == null || firedWeapon.HitEffectID <= 0) return;
        _vfxManager.Spawn(firedWeapon.HitEffectID, pos, 0f);
    }

    // 沿子彈飛行路徑每隔 TrailStep 距離種一個特效（地刺武器：載體隱形、靠這個沿路長出尖刺）。
    // 由 BulletInstance.OnTrailPoint 觸發；子彈反彈/分裂/追蹤後的彎折路徑都會跟著種。
    private void TrySpawnTrailEffect(WeaponData firedWeapon, Vector2 pos)
    {
        if (_vfxManager == null || firedWeapon == null || firedWeapon.TrailEffectID <= 0) return;
        _vfxManager.Spawn(firedWeapon.TrailEffectID, pos, 0f);
    }

    public void TakeDamage(float amount, Vector2 hitDirection)
    {
        if (_hitReaction != null && !_hitReaction.TryHitReaction(amount, hitDirection))
            return;

        _currentHealth -= amount;
        Debug.Log($"Player took {amount} damage. HP: {_currentHealth}/{PlayerMaxHealth}");

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Player died!");
    }
}
