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
    private HitReactionHandler _hitReaction;
    private float _fireTimer = 0f;
    private float _currentHealth;
    private readonly List<BulletInstance> _activeOrbitalBullets = new List<BulletInstance>();

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
            _weaponManager.SwitchToNextWeapon();
        }

        if (_fireTimer > 0) _fireTimer -= Time.deltaTime;

        if ((Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0)) && _fireTimer <= 0)
        {
            Shoot();
        }

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

        if (recipe.IsOrbital)
        {
            ShootOrbital(weapon, recipe);
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
        LayerMask pierceableLayers = EnemyLayer;
        LayerMask nonBounceLayers = ResolveNonBounceLayers(weapon.Recipe.BounceTarget);

        Vector3 bulletScale = weapon.BulletPrefab.transform.localScale * PlayerScale * weapon.BulletScale;
        BallisticsEngine.Spawn(recipe, weapon.BulletPrefab, spawnPos, fireDirection,
            collisionMask, pierceableLayers, nonBounceLayers, HandleBulletHit, weapon.WeaponSprite, weapon.SpriteAngleOffset, bulletScale, weapon.WeaponSprites, weapon.AnimFPS);
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
    }

    private void ShootOrbital(WeaponData weapon, ProjectileData recipe)
    {
        ClearActiveOrbitalBullets();

        int count = recipe.OrbitalCount;
        float radius = recipe.OrbitalRadius;

        LayerMask collisionMask = EnvLayer | EnemyLayer;
        LayerMask pierceableLayers = EnemyLayer;
        LayerMask nonBounceLayers = ResolveNonBounceLayers(weapon.Recipe.BounceTarget);
        Vector3 bulletScale = weapon.BulletPrefab.transform.localScale * PlayerScale * weapon.BulletScale;

        for (int i = 0; i < count; i++)
        {
            float angle = i * (2f * Mathf.PI / count);
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Vector2 spawnPos = (Vector2)transform.position + offset;
            Vector2 tangent = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));

            BulletInstance bullet = BallisticsEngine.Spawn(recipe, weapon.BulletPrefab, spawnPos, tangent,
                collisionMask, pierceableLayers, nonBounceLayers, HandleBulletHit, weapon.WeaponSprite, weapon.SpriteAngleOffset, bulletScale, weapon.WeaponSprites, weapon.AnimFPS);

            if (bullet != null)
            {
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

    void HandleBulletHit(BulletInstance bullet, GameObject target, RaycastHit2D hit)
    {
        if (_weaponManager == null) return;

        WeaponData weapon = _weaponManager.GetCurrentWeapon();
        MonsterController monster = target.GetComponent<MonsterController>();
        if (monster != null && weapon != null)
        {
            Vector2 hitDir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            monster.TakeDamage(weapon.Damage, hitDir);
        }
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
