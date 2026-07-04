using System.Collections.Generic;
using UnityEngine;

public class GroundEffectInstance : MonoBehaviour
{
    private GroundEffectData _data;
    private LayerMask _damageMask;   // 怪物 + 環境(可破壞地上物)
    private SpriteRenderer _templateRenderer;
    private readonly List<SpriteRenderer> _tileRenderers = new List<SpriteRenderer>();

    private float _lifeRemaining;
    private bool _hasInfiniteLife;
    private float _animTimer;
    private int _animFrame;
    private float _damageTimer;
    private bool _initialized;
    private float _damageOverride = -1f;  // >= 0 時改用此值（例如佛光以武器表 Damage 結算），< 0 = 用 GroundEffectTable 的 Damage

    // ── 燈火明滅（RenderMode=Glow，佛光用）──
    private SpriteRenderer _glowRenderer;   // 單圖護罩的 renderer（明滅 / 縮放對象）
    private Material _glowMat;              // 本實例專屬的 AuraGlow 材質（用完隨物件銷毀）
    private Vector3 _glowBaseScale;        // 護罩基準縮放（呼吸以此為中心 ±ScalePulse）
    private float _flickerSeed;            // 每個護罩隨機相位，避免多個同步閃

    private const float AuraIntensity   = 1.4f;   // AuraGlow 底亮度（>1 讓亮處更溢光）
    private const float FlickerMinAlpha = 0.45f;  // 最暗時的亮度倍率
    private const float FlickerMaxAlpha = 1.0f;   // 最亮時的亮度倍率
    private const float ScalePulse      = 0.03f;  // 呼吸縮放幅度（±3%）

    /// <param name="damageOverride">
    /// &lt; 0（預設）= 用 GroundEffectTable 的 Damage；&ge; 0 = 改用此值結算傷害
    /// （佛光等「載體型」特效把武器表 Damage 餵進來，讓同一張圓的傷害可隨武器調整）。
    /// </param>
    public void Initialize(GroundEffectData data, LayerMask damageMask, float damageOverride = -1f)
    {
        _data = data;
        _damageMask = damageMask;
        _damageOverride = damageOverride;

        // Prefab 上的 SpriteRenderer 只當 sortingLayer / order / material 的範本，
        // 自身不顯示任何圖（由動態產生的 tile 子物件負責繪製）。
        _templateRenderer = GetComponent<SpriteRenderer>();
        if (_templateRenderer != null)
            _templateRenderer.sprite = null;

        BuildTiles();
        if (_data.GlowFlicker)
        {
            // 佛光（Glow 模式）＝跟著玩家的光環：依中心 Y 和玩家一起排序（每幀更新，見 Update），
            // 所以玩家在祭壇前面時光環也在前、在祭壇後面時被擋——不像地板火固定壓在低層。單張圖、無大範圍 tile 問題。
            ApplyAuraYSort();
        }
        else
        {
            // 地板型（tile 火/毒、靜態單圖）：固定排序，高於「可走地上物」(MapLoader.WalkableObjectSortingOrder=5)、
            // 低於角色與一般（不可走）地上物（Y 排序帶）。正好用「可走與否」分了「火在物件上或下」：
            // 可走物＝地板鋪面(火畫在其上)、不可走立體物(祭壇/柱子)＝火在其腳下、被它蓋過。
            SetTilesSortingOrder(GroundEffectSortingOrder);
        }

        _hasInfiniteLife = _data.Duration < 0f;
        _lifeRemaining = _data.Duration;
        _damageTimer = 0f;
        _initialized = true;

        // DamageInterval == 0：生成瞬間單次爆裂；之後不再傷害。
        if (_data.DamageInterval <= 0f)
        {
            ApplyAreaDamage();
        }
    }

    private const int TileCountWarnThreshold = 500;

    /// <summary>
    /// 真實圓形掃描：以原點為中心、整數網格 (i, j)，當 tile 中心點 (i*TileSize, j*TileSize) 落在 Radius 內就保留。
    /// tile 排列嚴格貼齊網格（無半步偏移），高解析度（R/TileSize ≥ ~4）時呈現自然圓形；
    /// 低解析度（如 R=1.5、TileSize=1，直徑只跨 3 顆）會看起來偏方塊，這是 resolution 限制，
    /// 想看圓形請把 TileSize 調小（建議 ≤ Radius / 4）。
    /// 傷害判定走 OverlapCircleAll、嚴格按 Radius，與此鋪面範圍一致。
    /// </summary>
    private void BuildTiles()
    {
        if (_data == null) return;
        if (_data.AnimationSprites == null || _data.AnimationSprites.Length == 0) return;
        if (_data.Radius <= 0f) return;

        // 單圖模式：只放一張 sprite，整張縮放到直徑 = 2*Radius（佛光等柔和發光圓暈）。
        if (_data.SingleSprite)
        {
            BuildSingleSprite();
            return;
        }

        float tileSize = _data.TileSize > 0f ? _data.TileSize : 1f;
        float radius = _data.Radius;
        float radiusSqr = radius * radius;
        int maxOffset = Mathf.CeilToInt(radius / tileSize);
        Sprite firstFrame = _data.AnimationSprites[0];

        for (int j = -maxOffset; j <= maxOffset; j++)
        {
            float y = j * tileSize;
            float ySqr = y * y;
            if (ySqr > radiusSqr) continue;

            for (int i = -maxOffset; i <= maxOffset; i++)
            {
                float x = i * tileSize;
                if (x * x + ySqr > radiusSqr) continue;

                var tile = new GameObject($"Tile_{j}_{i}");
                tile.transform.SetParent(transform, false);
                tile.transform.localPosition = new Vector3(x, y, 0f);
                tile.transform.localScale = new Vector3(tileSize, tileSize, 1f);

                var sr = tile.AddComponent<SpriteRenderer>();
                sr.sprite = firstFrame;
                if (_templateRenderer != null)
                {
                    sr.sortingLayerID = _templateRenderer.sortingLayerID;
                    sr.sortingOrder = _templateRenderer.sortingOrder;
                    sr.sharedMaterial = _templateRenderer.sharedMaterial;
                }
                _tileRenderers.Add(sr);
            }
        }

        if (_tileRenderers.Count > TileCountWarnThreshold)
        {
            Debug.LogWarning($"GroundEffect '{_data.Name}' spawned {_tileRenderers.Count} tiles (Radius={radius}, TileSize={tileSize}); consider increasing TileSize for performance.");
        }
    }

    /// <summary>
    /// 單圖模式：放一張 sprite，整張縮放到「直徑 = 2*Radius」。
    /// 縮放依 sprite 實際世界尺寸（bounds）動態算出，與 PPU 無關。
    /// 沿用同一條動畫路徑（把這唯一的 renderer 放進 _tileRenderers），所以多幀圖也能播。
    /// </summary>
    private void BuildSingleSprite()
    {
        Sprite firstFrame = _data.AnimationSprites[0];
        if (firstFrame == null) return;

        var go = new GameObject("AuraSprite");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        // sprite 在 localScale=1 時的世界寬度；縮放到直徑 = 2*Radius
        float spriteWorld = firstFrame.bounds.size.x;
        float scale = spriteWorld > 1e-5f ? (2f * _data.Radius) / spriteWorld : 1f;
        go.transform.localScale = new Vector3(scale, scale, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = firstFrame;
        if (_templateRenderer != null)
        {
            sr.sortingLayerID = _templateRenderer.sortingLayerID;
            sr.sortingOrder = _templateRenderer.sortingOrder;
            sr.sharedMaterial = _templateRenderer.sharedMaterial;
        }
        _tileRenderers.Add(sr);

        // 燈火明滅：改用 Custom/AuraGlow 加色發光材質，並登記每幀明滅 / 呼吸縮放對象。
        if (_data.GlowFlicker)
        {
            Shader glowShader = Resources.Load<Shader>("Shaders/AuraGlow");
            if (glowShader != null)
            {
                _glowMat = new Material(glowShader);
                _glowMat.SetFloat("_Intensity", AuraIntensity);
                sr.sharedMaterial = _glowMat;  // 覆寫範本材質，改成加色發光
            }
            else
            {
                Debug.LogWarning("找不到 Shaders/AuraGlow，佛光改用範本材質（無發光明滅）。");
            }
            _glowRenderer = sr;
            _glowBaseScale = go.transform.localScale;
            _flickerSeed = Random.value * 1000f;
        }
    }

    // 高於可走地上物(5)、低於角色/一般地上物(Y 排序帶) → 火在可走石板之上、在祭壇/柱子與角色之下。
    private const int GroundEffectSortingOrder = 8;
    // 佛光光環往後偏一點，讓玩家畫在光環之上（光環繞在腳邊、人在光中）。
    private const float AuraYSortBias = 0.3f;

    /// <summary>把整團特效（template＋所有 tile/單圖）設成同一個排序值。</summary>
    private void SetTilesSortingOrder(int order)
    {
        if (_templateRenderer != null) _templateRenderer.sortingOrder = order;
        for (int i = 0; i < _tileRenderers.Count; i++)
            if (_tileRenderers[i] != null) _tileRenderers[i].sortingOrder = order;
    }

    /// <summary>佛光：依中心 Y（跟著玩家）進 Y 排序帶，和玩家/地上物一起交錯遮蔽。</summary>
    private void ApplyAuraYSort()
    {
        SetTilesSortingOrder(MapDepthSort.Order(transform.position.y + AuraYSortBias, 0));
    }

    private void Update()
    {
        if (!_initialized) return;

        if (_data.GlowFlicker) ApplyAuraYSort();   // 佛光跟著玩家移動 → 每幀重算排序（單張圖，成本極低）

        TickAnimation();
        TickFlicker();
        TickDamage();
        TickLifetime();
    }

    /// <summary>
    /// 燈火忽強忽弱：用 Perlin noise（慢速不規則漂移）＋ 一條快速正弦（細微抖動）混出
    /// 有機的明滅，調 SpriteRenderer 的 color.a 餵進 AuraGlow 當亮度倍率；
    /// 另以一條較慢、獨立相位的正弦做 ±3% 呼吸縮放。皆為純視覺，不動傷害半徑。
    /// </summary>
    private void TickFlicker()
    {
        if (!_data.GlowFlicker || _glowRenderer == null) return;

        float t = Time.time;

        // 不規則明滅：Perlin 慢漂（主）＋ 一點點慢速正弦（次）。
        // 油燈是緩慢搖曳，所以兩條都放慢、快閃權重壓低，避免高頻抖動。
        float slow = Mathf.PerlinNoise(_flickerSeed, t * 0.55f);         // 0..1 平滑慢漂
        float fast = Mathf.Sin(t * 3.2f + _flickerSeed) * 0.5f + 0.5f;   // 0..1 緩慢搖曳
        float mix  = Mathf.Clamp01(0.85f * slow + 0.15f * fast);
        float bright = Mathf.Lerp(FlickerMinAlpha, FlickerMaxAlpha, mix);

        Color c = _glowRenderer.color;   // 保持 rgb=白，只調 a 當亮度
        c.a = bright;
        _glowRenderer.color = c;

        // 呼吸縮放 ±3%（更慢、與明滅不同相位，較自然；只縮視覺，不影響傷害圈）
        float breathe = Mathf.Sin(t * 1.1f + _flickerSeed * 0.5f);       // -1..1
        _glowRenderer.transform.localScale = _glowBaseScale * (1f + ScalePulse * breathe);
    }

    private void TickAnimation()
    {
        if (_data.AnimationSprites == null || _data.AnimationSprites.Length <= 1) return;
        if (_data.AnimFPS <= 0f) return;
        if (_tileRenderers.Count == 0) return;

        _animTimer += Time.deltaTime;
        float frameDuration = 1f / _data.AnimFPS;
        if (_animTimer >= frameDuration)
        {
            _animTimer -= frameDuration;
            _animFrame = (_animFrame + 1) % _data.AnimationSprites.Length;
            Sprite frame = _data.AnimationSprites[_animFrame];
            for (int i = 0; i < _tileRenderers.Count; i++)
            {
                if (_tileRenderers[i] != null)
                    _tileRenderers[i].sprite = frame;
            }
        }
    }

    private void TickDamage()
    {
        if (_data.DamageInterval <= 0f) return;

        _damageTimer += Time.deltaTime;
        if (_damageTimer >= _data.DamageInterval)
        {
            _damageTimer -= _data.DamageInterval;
            ApplyAreaDamage();
        }
    }

    private void TickLifetime()
    {
        if (_hasInfiniteLife) return;

        _lifeRemaining -= Time.deltaTime;
        if (_lifeRemaining <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyAreaDamage()
    {
        // 傷害值：damageOverride >= 0 時改用它（佛光以武器表 Damage 結算），否則用表格 Damage。
        float damage = _damageOverride >= 0f ? _damageOverride : _data.Damage;
        if (damage <= 0f || _data.Radius <= 0f) return;

        Vector2 center = transform.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, _data.Radius, _damageMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null) continue;

            // 走中央 CombatSystem（isDot=true）：套用目標減傷/抗性後結算；牆等無 IDamageable 者由 CombatSystem 自動略過。
            // 來源未知（地面特效是脫離武器的獨立物件）→ source = null（攻擊加成已在生成時透過 damageOverride 帶入）。見 readme/COMBAT.md
            Vector2 hitDir = ((Vector2)col.transform.position - center).normalized;
            CombatSystem.Apply(null, col.gameObject, damage, hitDir, DamageType.Physical, isDot: true);
        }
    }

    private void OnDestroy()
    {
        // 釋放本實例執行期建立的 AuraGlow 材質（每個護罩各一份），避免材質洩漏。
        if (_glowMat != null)
        {
            Destroy(_glowMat);
            _glowMat = null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_data == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _data.Radius);
    }
}
