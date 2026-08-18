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

    /// <summary>
    /// 擊退距離用的「圖寬」補償倍率（預設 1 = 不補償）。
    ///
    /// 擊退距離是「角色圖寬 × 百分比」算的，所以只要角色被畫大，擊退就會跟著變遠。
    /// 玩家的血統體型倍率（<c>PlayerController.BodyScale</c>）刻意定義成**純視覺**，
    /// 所以那邊會把倍率填進這裡除掉，讓 1.5 倍體型的角色被擊退的距離跟 1 倍一樣。
    /// 怪物不用管（牠們的顯示大小本來就代表體型差異，擊退跟著變遠是合理的）。
    /// </summary>
    public float WidthScaleCompensation = 1f;

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
        if (WidthScaleCompensation > 0.01f) worldWidth /= WidthScaleCompensation;   // 見欄位說明
        float knockbackDistance = worldWidth * (_knockbackPercent / 100f);

        // 怪物現在是 trigger（無硬碰撞），擊退不會被牆擋 → 會飛穿牆再被 A* 拉回來。這裡用物理射線偵測「真正的牆」，
        // 把擊退距離夾到牆邊之前：後面空地就照原距離退、後面是牆就退到牆邊為止（退不夠遠也還是會退到能退的最遠處）。
        Vector2 dir = direction.normalized;
        knockbackDistance = ClampToWall(_rb.position, dir, knockbackDistance);
        Vector2 velocity = dir * (knockbackDistance / KNOCKBACK_DURATION);

        IsKnockedBack = true;
        _rb.velocity = velocity;

        yield return new WaitForSeconds(KNOCKBACK_DURATION);

        _rb.velocity = Vector2.zero;
        IsKnockedBack = false;
    }

    // 用物理射線沿擊退方向找「真正的牆/水」，把距離夾到牆邊之前（留一點邊距讓身體別陷進去）。
    // 用實際碰撞而非尋徑格：尋徑格為了怪身淨空把牆膨脹了一圈，會害怪退不進那圈；這裡要讓怪能一路退到真牆邊。
    // 後面沒牆 → 回原距離（正常退）；後面有牆 → 回「到牆邊」的距離（退到能退的最遠處，不會是 0，除非已經貼著牆）。
    private float ClampToWall(Vector2 start, Vector2 dir, float dist)
    {
        if (dist <= 0f) return dist;
        int mask = LayerMask.GetMask("Environment", "Water");
        if (mask == 0) return dist;
        const float margin = 0.15f;   // 約腳框半寬：讓身體停在牆邊而不是中心貼上牆
        RaycastHit2D hit = Physics2D.Raycast(start, dir, dist + margin, mask);
        if (hit.collider == null) return dist;             // 後面沒牆 → 照原距離退
        return Mathf.Max(0f, hit.distance - margin);       // 後面有牆 → 退到牆邊之前
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
