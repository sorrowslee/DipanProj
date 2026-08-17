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

    private const float AuraIntensity   = 1.4f;   // AuraGlow 底亮度（>1 讓亮處更溢光）。
                                                //    ⚠️ 要調它請注意：貼圖自身的 alpha 會先乘一刀（佛光圖中心 alpha 只有 0.55），
                                                //    所以這個數字不等於畫面上的實際亮度。見 readme/PROBLEMS.md E12。
    private const float FlickerMinAlpha = 0.45f;  // 最暗時的亮度倍率
    private const float FlickerMaxAlpha = 1.0f;   // 最亮時的亮度倍率
    private const float ScalePulse      = 0.03f;  // 呼吸縮放幅度（±3%）

    // ── 旋轉符號層（GroundEffectTable 第 12 欄 SigilPath「背景旋轉符號圖」，留空＝沒有這一層）──
    // 在特效的圓上疊一張緩緩自轉的符號（法陣／符文／卍字…）。與 RenderMode 無關：Tile / Single / Glow 都能掛。
    //
    // 📌 2026-08-17 現況：**機制完成、但目前沒有任何一列地面特效使用它**（SigilPath 全表留空）。
    //    當初是為墮落佛光做的，最後作者決定佛光回到原本的暖金光圈，這一層就閒置備用。
    //    要啟用：GroundEffectTable 那一列的 SigilPath 填圖路徑即可，程式不用動。
    //
    // ⚠️ 下面那組常數是走過三輪才收斂的，別當成隨手填的預設值——完整原委見 readme/GROUND_EFFECT.md
    //    的「背景旋轉符號層」一節與 readme/PROBLEMS.md E12/E13。一句話版本：
    //    **符號與本體同位置同色相時，兩個都靠「比較亮」被看見是零和的**，一定要一個發光、一個吃光。
    private SpriteRenderer _sigilRenderer;  // 符號 renderer（自轉 / 呼吸對象）
    private Vector3 _sigilBaseScale;        // 符號基準縮放（依 Radius 算出，呼吸以此為中心）
    private float _sigilAngle;              // 當前旋轉角
    private float _sigilSeed;               // 呼吸相位（與光環明滅刻意錯開）

    /// <summary>
    /// 符號直徑 = 特效直徑 × 此倍率。綁在 Radius 上，改 CSV 的 Radius 符號會自動跟著變。
    /// ⚠️ 算「符號會不會超出本體的圓」時要記得**扣掉圖的透明留白**：Manji.png 的筆畫外接框只佔圖寬的 71%，
    /// 而佛光貼圖的可見圓大約到半徑 0.80（亮度衰減到中心的 20%）。
    /// 所以筆畫實際半徑 = Radius × 倍率 × 0.71，要 ≤ Radius × 0.80 才在圓內 → 倍率上限約 1.13。
    /// 0.95 讓筆畫半徑約 0.81 世界單位，明確落在佛光圓內、還留一點邊距。
    /// </summary>
    private const float SigilRadiusMul   = 0.95f;
    /// <summary>旋轉速度（度/秒，正＝逆時針左旋）。沿用開場墜落 IntroFallController.ManjiRotateSpeed = 32。</summary>
    private const float SigilRotateSpeed = 32f;
    /// <summary>
    /// 符號顏色＝**深紫近黑的剪影**（不是發光的紫）。走 alpha 混合，所以這是「蓋上去的顏色」不是「加上去的光」。
    /// 為什麼要暗：符號與本體同位置、同色相、同時出現，兩個都靠「比較亮」被看見時是零和的——
    /// 本體調亮符號就消失、符號調實本體就消失，調 alpha 永遠跳不出這個循環。
    /// 讓符號改成「吃光」就離開了這場競賽：亮地板上靠剪影讀形狀，本體繼續發光，兩個都看得清楚。
    /// （同 PROBLEMS E11 的教訓：優先用明暗對比，別靠大面積半透明。）
    /// </summary>
    private static readonly Color SigilColor = new Color(0.16f, 0.05f, 0.26f, 1f);
    private const float SigilAlpha     = 0.85f;  // alpha 混合下這是「覆蓋率」：1 = 完全遮住地板。⚠️ Linear 色彩空間下實際會比數字看起來重（PROBLEMS E11）
    private const float SigilBreathe   = 0.02f;  // 自己的呼吸幅度（±2%）
    private const float SigilBreatheHz = 0.42f;  // 比光環呼吸(1.1)慢很多：燈火是搖曳的火，符號是常駐的法

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
        BuildSigil();   // 背景旋轉符號：獨立於 RenderMode，由「背景符號圖」欄位決定有沒有
        BuildLight();   // 真的照亮暗場景：由「發光半徑」欄位決定有沒有
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

    /// <summary>
    /// 背景旋轉符號：獨立子物件，直徑 = 2 × Radius × SigilRadiusMul（綁 Radius，不寫死世界單位）。
    /// 有沒有這一層完全由 GroundEffectTable 的「背景符號圖」欄位決定，與 RenderMode 無關。
    /// ⚠️ 刻意「不」加進 _tileRenderers——那個清單每幀會被 TickAnimation 換 sprite、被 SetTilesSortingOrder 統一排序，
    /// 符號加進去會被本體的動畫幀蓋掉、也會跟本體同層而失去「在後面」的效果。
    /// </summary>
    private void BuildSigil()
    {
        if (_data == null || _data.Radius <= 0f) return;

        Sprite sigil = _data.SigilSprite;
        if (sigil == null) return;   // 欄位留空、或圖載不到 → 沒有這一層，不影響特效本體

        var go = new GameObject("AuraSigil");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        float spriteWorld = sigil.bounds.size.x;
        float scale = spriteWorld > 1e-5f
            ? (2f * _data.Radius * SigilRadiusMul) / spriteWorld
            : 1f;
        go.transform.localScale = new Vector3(scale, scale, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sigil;
        if (_templateRenderer != null) sr.sortingLayerID = _templateRenderer.sortingLayerID;

        // ⚠️ 刻意「不」指定材質 → 用預設的 Sprites-Default，也就是 **alpha 混合**，不是本體那套 AuraGlow 加色。
        // 這是整個設計的關鍵：加色只能讓底下變亮、永遠遮不住東西，所以加色的符號沒辦法「實心」，
        // 一遇到旁邊更亮的光就被洗掉。改用 alpha 混合＋暗色，符號才會真的蓋住地板、形成剪影。
        // 符號因此是「吃光」的，本體是「發光」的，兩層不再爭同一個亮度維度。
        // 圖是白色去背，sr.color 直接當染色用；alpha 就是覆蓋率（1 = 完全實心）。
        Color c = SigilColor; c.a = SigilAlpha;
        sr.color = c;

        _sigilRenderer = sr;
        _sigilBaseScale = go.transform.localScale;
        _sigilSeed = Random.value * 1000f;
    }

    // ── 照明（GroundEffectTable 第 13 欄「發光半徑」，留空/<=0 ＝不發光）──
    // ⚠️ 這跟「畫一張發光的圖」是兩回事：發光的圖只是把自己畫亮，**不會讓周圍的地圖從黑暗中顯現**。
    //    真正照亮暗場景（Atmosphere=2 幽暗那類）的是 AtmosphereController 的光源系統，
    //    而它收光源的唯一管道就是 LightSource 的靜態登記表。所以掛一顆 LightSource 就接上了，
    //    AtmosphereController 一行都不用改。
    // 生死自動：LightSource 在 OnEnable 登記、OnDisable 退出，而特效物件銷毀前 Unity 一定會呼叫 OnDisable
    //    → 佛光放開的瞬間光就熄，不需要任何手動清理。
    private const float EffectLightIntensity = 1.0f;    // 亮度倍率（1 = 標準）
    private const float EffectLightFlicker   = 1.0f;    // 搖晃強度（1 = 標準燭火；0 = 完全不動）
    private const float EffectLightSoftness  = 0.46f;   // 邊緣柔和度（LightSource 預設值）

    /// <summary>
    /// 發光半徑 &gt; 0 時，在本特效身上掛一顆 <see cref="LightSource"/>，讓它成為場上的一盞燈。
    /// 光跟著特效走（佛光那種跟隨玩家的特效，光自然也跟著玩家）。
    /// 光色沿用 LightSource 的預設暖橘；要換色／換搖晃感改上面三個常數。
    /// </summary>
    private void BuildLight()
    {
        if (_data == null || _data.LightRadius <= 0f) return;

        var ls = gameObject.AddComponent<LightSource>();
        ls.radius       = _data.LightRadius;
        ls.intensity    = EffectLightIntensity;
        ls.color        = LightSource.DefaultWarm;
        ls.flicker      = EffectLightFlicker;
        ls.flickerSpeed = 1f;
        ls.softness     = EffectLightSoftness;
    }

    // 高於可走地上物(5)、低於角色/一般地上物(Y 排序帶) → 火在可走石板之上、在祭壇/柱子與角色之下。
    private const int GroundEffectSortingOrder = 8;
    // 佛光光環往後偏一點，讓玩家畫在光環之上（光環繞在腳邊、人在光中）。
    private const float AuraYSortBias = 0.3f;

    /// <summary>
    /// 把整團特效（template＋所有 tile/單圖）設成同一個排序值；
    /// 背景符號一律再高一階 → 蓋在本體之上（暗剪影要吃得到光），但跟著同一個排序值走
    /// （所以本體被角色/地上物擋住時，符號也一起被擋，不會出現符號浮在前面）。
    /// </summary>
    private void SetTilesSortingOrder(int order)
    {
        if (_templateRenderer != null) _templateRenderer.sortingOrder = order;
        for (int i = 0; i < _tileRenderers.Count; i++)
            if (_tileRenderers[i] != null) _tileRenderers[i].sortingOrder = order;
        // ⚠️ 是 +1「疊在本體之上」不是 -1。暗剪影畫在加色光的下面，會被光直接填亮而消失——
        // 要吃光就得蓋在光上面。仍遠低於玩家：AuraYSortBias 0.3 × SortScale 100 = 低玩家 30 階，+1 翻不過去。
        if (_sigilRenderer != null) _sigilRenderer.sortingOrder = order + 1;
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
        TickSigil();
        TickDamage();
        TickLifetime();
    }

    /// <summary>
    /// 背景符號：等速自轉 ＋ 一條很慢、與本體明滅不同相位的呼吸縮放。
    /// 刻意不跟著本體一起閃——燈火是搖曳的火、符號是常駐的法，同步閃會顯得廉價。
    /// 純視覺：符號通常比傷害半徑大，傷害仍嚴格走 Radius 的 OverlapCircle，不受這裡影響。
    /// </summary>
    private void TickSigil()
    {
        if (_sigilRenderer == null) return;

        _sigilAngle += SigilRotateSpeed * Time.deltaTime;
        if (_sigilAngle >= 360f) _sigilAngle -= 360f;   // 避免長時間累加後浮點精度變差

        Transform tr = _sigilRenderer.transform;
        tr.localEulerAngles = new Vector3(0f, 0f, _sigilAngle);

        float breathe = Mathf.Sin(Time.time * SigilBreatheHz + _sigilSeed);   // -1..1
        tr.localScale = _sigilBaseScale * (1f + SigilBreathe * breathe);
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
