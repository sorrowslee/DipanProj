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

        if (recipe.IsOrbital)
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
