using UnityEngine;
using System.Collections;

/// <summary>
/// 通用受擊反應元件：白光閃爍、擊退位移、無敵時間（半透明）。
/// 可掛載在任何有 SpriteRenderer + Rigidbody2D 的角色或怪物上。
/// </summary>
public class HitReactionHandler : MonoBehaviour
{
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rb;
    private MaterialPropertyBlock _mpb;

    private float _invincibleTimeSec;
    private float _knockbackThreshold;
    private float _knockbackPercent;

    public bool IsInvincible { get; private set; }
    public bool IsKnockedBack { get; private set; }

    private const float FLASH_DURATION = 0.06f;
    private const int FLASH_COUNT = 3;
    private const float FLASH_INTENSITY = 0.65f;
    private const float KNOCKBACK_DURATION = 0.1f;
    private const float INVINCIBLE_ALPHA = 0.4f;

    private float _accumulatedDamage;
    private static Material _sharedFlashMaterial;
    private Color _originalColor;

    public void Configure(SpriteRenderer sr, Rigidbody2D rb,
        float invincibleTimeMs, float knockbackThreshold, float knockbackPercent)
    {
        _spriteRenderer = sr;
        _rb = rb;
        _invincibleTimeSec = invincibleTimeMs / 1000f;
        _knockbackThreshold = knockbackThreshold;
        _knockbackPercent = knockbackPercent;

        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
            ApplyFlashMaterial();
        }

        _mpb = new MaterialPropertyBlock();
    }

    private void ApplyFlashMaterial()
    {
        if (_sharedFlashMaterial == null)
        {
            Shader flashShader = Resources.Load<Shader>("Shaders/SpriteFlash");
            if (flashShader != null)
                _sharedFlashMaterial = new Material(flashShader);
            else
                Debug.LogWarning("SpriteFlash shader not found at Resources/Shaders/SpriteFlash. White flash effect disabled.");
        }

        if (_sharedFlashMaterial != null)
            _spriteRenderer.sharedMaterial = _sharedFlashMaterial;
    }

    /// <summary>
    /// 嘗試觸發受擊反應。若角色正在無敵中則回傳 false（呼叫端應忽略此次傷害）。
    /// </summary>
    public bool TryHitReaction(float damage, Vector2 hitDirection)
    {
        if (IsInvincible) return false;

        StopAllCoroutines();
        ResetVisuals();

        StartCoroutine(HitReactionSequence(damage, hitDirection));
        return true;
    }

    private IEnumerator HitReactionSequence(float damage, Vector2 hitDirection)
    {
        _accumulatedDamage += damage;

        if (_knockbackPercent > 0 && _accumulatedDamage >= _knockbackThreshold)
        {
            _accumulatedDamage = 0;
            StartCoroutine(ApplyKnockback(hitDirection));
        }

        if (_invincibleTimeSec > 0)
            StartCoroutine(InvincibilityPeriod());

        for (int i = 0; i < FLASH_COUNT; i++)
        {
            SetFlashAmount(FLASH_INTENSITY);
            yield return new WaitForSeconds(FLASH_DURATION);
            SetFlashAmount(0f);
            yield return new WaitForSeconds(FLASH_DURATION);
        }
    }

    private IEnumerator ApplyKnockback(Vector2 direction)
    {
        if (_rb == null || _spriteRenderer == null || _spriteRenderer.sprite == null)
            yield break;

        float spriteWidth = _spriteRenderer.sprite.bounds.size.x;
        float worldWidth = spriteWidth * Mathf.Abs(transform.lossyScale.x);
        float knockbackDistance = worldWidth * (_knockbackPercent / 100f);
        Vector2 velocity = direction.normalized * (knockbackDistance / KNOCKBACK_DURATION);

        IsKnockedBack = true;
        _rb.velocity = velocity;

        yield return new WaitForSeconds(KNOCKBACK_DURATION);

        _rb.velocity = Vector2.zero;
        IsKnockedBack = false;
    }

    private IEnumerator InvincibilityPeriod()
    {
        IsInvincible = true;

        Color semiTransparent = _originalColor;
        semiTransparent.a = INVINCIBLE_ALPHA;
        if (_spriteRenderer != null)
            _spriteRenderer.color = semiTransparent;

        yield return new WaitForSeconds(_invincibleTimeSec);

        if (_spriteRenderer != null)
            _spriteRenderer.color = _originalColor;
        IsInvincible = false;
    }

    private void SetFlashAmount(float amount)
    {
        if (_spriteRenderer == null || _mpb == null) return;

        _spriteRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat("_FlashAmount", amount);
        _spriteRenderer.SetPropertyBlock(_mpb);
    }

    private void ResetVisuals()
    {
        IsKnockedBack = false;
        IsInvincible = false;
        SetFlashAmount(0f);
        if (_spriteRenderer != null)
            _spriteRenderer.color = _originalColor;
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}
