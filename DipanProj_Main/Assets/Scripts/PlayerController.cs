using UnityEngine;
using Sorrows.Ballistics;

public class PlayerController : MonoBehaviour
{
    public float MoveSpeed = 5f;
    public LayerMask EnvLayer; 
    public LayerMask EnemyLayer; 
    public int CurrentWeaponID = 1;

    private Animator _animator;
    private Rigidbody2D _rb;
    private Vector2 _moveInput;
    private SpriteRenderer _spriteRenderer;
    private WeaponManager _weaponManager;
    private WeaponData _currentWeapon;
    private float _fireTimer = 0f;

    public bool isFacingRightByDefault = true;

    void Start()
    {
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

        _weaponManager = FindObjectOfType<WeaponManager>();
        if (_weaponManager == null)
        {
            Debug.LogError("WeaponManager not found in scene!");
        }

        RefreshWeapon();
    }

    public void SwitchWeapon(int weaponID)
    {
        CurrentWeaponID = weaponID;
        RefreshWeapon();
    }

    private void RefreshWeapon()
    {
        if (_weaponManager != null)
        {
            _currentWeapon = _weaponManager.GetWeapon(CurrentWeaponID);
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

        if (_fireTimer > 0) _fireTimer -= Time.deltaTime;

        if ((Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0)) && _fireTimer <= 0)
        {
            Shoot();
            if (_currentWeapon?.Recipe?.Data != null)
                _fireTimer = _currentWeapon.Recipe.Data.FireInterval;
        }

        HandleVisuals();
    }

    private void HandleVisuals()
    {
        if (_animator == null || _spriteRenderer == null) return;

        float currentSpeed = (_rb != null) ? _rb.velocity.magnitude : 0f;
        _animator.SetBool("isMoving", currentSpeed > 0.1f);
    }

    void FixedUpdate()
    {
        _rb.velocity = _moveInput * MoveSpeed;
    }

    void Shoot()
    {
        if (_currentWeapon == null || _currentWeapon.BulletPrefab == null || _currentWeapon.Recipe == null) return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        Vector2 fireDirection = (mousePos - transform.position).normalized;
        Vector2 spawnPos = (Vector2)transform.position;

        ProjectileData recipe = _currentWeapon.Recipe.Data;
        LayerMask collisionMask = EnvLayer | EnemyLayer;
        LayerMask pierceableLayers = EnemyLayer;
        LayerMask nonBounceLayers = ResolveNonBounceLayers(_currentWeapon.Recipe.BounceTarget);

        BallisticsEngine.Spawn(recipe, _currentWeapon.BulletPrefab, spawnPos, fireDirection,
            collisionMask, pierceableLayers, nonBounceLayers, HandleBulletHit);
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
        MonsterController monster = target.GetComponent<MonsterController>();
        if (monster != null && _currentWeapon != null)
        {
            monster.TakeDamage(_currentWeapon.Damage);
        }
    }
}
