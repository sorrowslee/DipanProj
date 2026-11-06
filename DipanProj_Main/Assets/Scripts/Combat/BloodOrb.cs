using UnityEngine;

/// <summary>
/// 血球（2026-11-06 新增，作者需求）：怪物死亡時有機率掉落的半透明紅色圓球，**玩家碰到就吸收**，
/// 回復「最大血量的 20%」＋「最大魔力的 20%」（是最大值的 20%，不是當前值的 20%；原 50%，2026-11-06 改）。
///
/// 規則（作者拍板）：
///   ‧ 掉落機率 <see cref="DropChancePercent"/>（全域，10%）。每隻有資格掉寶的怪死亡時各自擲一次，跟掉寶表互不相干。
///   ‧ 只有「會掉寶」的怪才會掉：地圖生成的敵怪（DropsLoot）且 DropTableId &gt; 0。
///     ⇒ 夢境教學怪、召喚物、NPC、DropTableId 留空的怪都不會掉（要讓某關不掉，把那些怪的 DropTableId 留空即可）。
///   ‧ **滿血滿魔時碰到沒有反應**（球留在原地）；只要血或魔有一項沒滿就吸收，兩項一起補。
///   ‧ 跟地上掉落物一樣**在同一趟劇本內跨場景保留**（RunProgress 記座標，換圖回來原地重放），離開劇本才清掉。
///
/// 外觀：程序生成的半透明紅球（呼吸脈動＋上下漂浮）。要換成美術圖：把圖放到
///   <c>Resources/Pickups/BloodOrb.png</c>（圓形、去背），有圖就自動改用那張，大小仍由 <see cref="WorldSize"/> 決定。
///
/// 由 <see cref="InteractionManager"/> 生成／重放／換圖清除；碰觸判定在本元件自己的 Update。
/// 文件：readme/RUN_PROGRESS.md §9。
/// </summary>
public class BloodOrb : MonoBehaviour
{
    // ═══════════ 可調參數（全域；要調就改這裡）═══════════

    /// <summary>怪物死亡掉血球的機率（%）。作者拍板 10%，2026-11-06 實測後改 5%。</summary>
    public const float DropChancePercent = 5f;   // 2026-11-06 作者：10% 太高 → 5%
    /// <summary>吸收時回復「最大血量／最大魔力」的比例。作者拍板 0.5（＝50%），2026-11-06 實測嫌太多改 0.2（＝20%）。</summary>
    public const float RestoreFraction = 0.2f;

    /// <summary>清場血球：每條「戰鬥鏈」最後一組怪打完時掉幾顆（含頭尾，各半機率）。見 <see cref="MapClearReward"/>。</summary>
    public const int ClearDropMin = 1;
    public const int ClearDropMax = 2;

    /// <summary>球在世界中的直徑（世界單位；1 格≈1）。</summary>
    public const float WorldSize = 0.2f;   // 0.4 → 作者：再縮小一半
    // 2026-11-06 演進：0.55 → 0.9 → 1.8 都是在「球身幾乎透明、只看得到高光一個小點」的 bug（PROBLEMS E32）下調的；
    // bug 修好後 1.8 整顆畫出來太大，作者要縮回「當時看到的那個光點」的大小 ≈ 0.33 格 → 整顆球取 0.4（邊緣淡紅一圈）。
    /// <summary>光環、冒出閃光的基準大小（世界單位）。**刻意跟球的大小脫鉤**：作者看過、認可的是球 1.8 那時的光環／閃光尺寸。</summary>
    public const float FxRefSize = 1.8f;
    /// <summary>同時掉好幾顆時彼此至少隔多遠（世界單位），光環才不會疊成一團。</summary>
    public const float MinSpacing = 1.8f;
    /// <summary>玩家碰觸半徑（玩家碰撞位置到球心的距離，世界單位）。對齊碰撞、不對齊視覺（PROBLEMS B13）。球加大後跟著放大。</summary>
    public const float TouchRadius = 0.9f;    // 判定是「玩家腳底點 ↔ 球心」的距離、不算玩家身體寬度，所以要給寬一點（作者 2026-11-06：球縮到 0.2 後 0.5 太難碰到）
    /// <summary>血球落點離怪物死亡處的距離（世界單位，最小～最大）。掉落物散開半徑約 0.45，
    /// 拉開到 1 以上才不會跟掉落物 icon 與名稱文字混在一起（作者 2026-11-06）。落點一定挑可走的格子。</summary>
    public const float DropDistMin = 1.7f;   // 作者認可的距離（離掉落物 icon 與名稱文字夠遠），球縮小後不動
    public const float DropDistMax = 2.3f;
    /// <summary>美術圖路徑（Resources 相對、不含副檔名）。沒有這張圖就用程序生成。</summary>
    public const string ArtPath = "Pickups/BloodOrb";

    const float PulseAmp = 0.07f;        // 呼吸脈動的縮放幅度
    const float PulseSpeed = 3.2f;       // 呼吸速度（弧度/秒）
    const float BobAmp = 0.06f;          // 上下漂浮幅度（世界單位）
    const float BobSpeed = 2.1f;
    const float AbsorbDuration = 0.28f;  // 吸收動畫（飛向玩家＋縮小＋淡出）秒數
    const string SortingLayer = "Default";
    const int SortingOrder = 6;          // 跟地上掉落物同層（掉落物 5），畫在角色下面、地面上面
    const int ShadowSortingOrder = 4;    // 地上影子：在掉落物下面
    const int FlightSortingOrder = 20000;// 飛行中：畫在角色上面（16-bit 安全），不會被怪／主角擋住

    // ── 掉落動畫（2026-11-06 作者：要有掉落動畫、做漂亮點；落地前碰到沒效果）──
    const float DropPopTime = 0.12f;      // 從屍體位置「噗」一下冒出來（由小放大＋白閃）
    const float DropFlightTime = 0.5f;    // 拋物線飛到落點
    const float DropArcHeight = 1.6f;     // 拋物線最高點（世界單位，另加一點隨距離的高度）
    const float DropBounceHeight = 0.35f; // 落地後再小彈一下的高度
    const float DropBounceTime = 0.22f;
    const float DropSquash = 0.22f;       // 落地壓扁幅度（寬變大、高變小）
    const float DropSquashTime = 0.09f;
    const float RingTime = 0.45f;         // 落地擴散光環
    const float RingMaxScale = 1.7f;      // 光環最後大小（×球直徑）
    const float TrailInterval = 0.035f;   // 飛行殘影間隔
    const float TrailLife = 0.22f;
    const float ShadowAlpha = 0.32f;

    // ═══════════════════════════════════════════════

    /// <summary>對應 RunProgress 的血球記錄 id（0＝未登記）。</summary>
    public int RunOrbId { get; private set; }

    SpriteRenderer _sr;
    SpriteRenderer _shadow;
    Vector3 _basePos;
    float _phase;
    bool _absorbing;
    bool _landed;          // 落地前（掉落動畫中）碰到沒效果

    /// <summary>已經完整落在地上（可以被吸收）。</summary>
    public bool Landed => _landed;

    static Sprite _cachedSprite, _shadowSprite, _ringSprite;
    static Transform _player;
    static CombatStats _playerStats;

    /// <summary>
    /// 在落點 pos 生一顆血球（不登記進度；登記由 InteractionManager 負責）。
    /// <paramref name="dropFrom"/> 有值＝播掉落動畫（從那裡冒出來、拋物線飛到 pos、落地彈一下＋光環），落地前不能吸收；
    /// null＝直接放在地上（換圖回來重放用）。
    /// </summary>
    public static BloodOrb Create(Vector2 pos, int runOrbId, Vector2? dropFrom = null)
    {
        var go = new GameObject("BloodOrb");
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        var orb = go.AddComponent<BloodOrb>();
        orb.RunOrbId = runOrbId;
        orb.Build();
        if (dropFrom.HasValue) orb.StartCoroutine(orb.DropRoutine(dropFrom.Value));
        else orb._landed = true;
        return orb;
    }

    /// <summary>擲一次「這隻怪要不要掉血球」。</summary>
    public static bool RollDrop() => Random.value * 100f < DropChancePercent;

    void Build()
    {
        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = GetSprite();
        _sr.sortingLayerName = SortingLayer;
        _sr.sortingOrder = SortingOrder;
        // 依 sprite 實際尺寸換算縮放，讓不論程序圖或美術圖都是 WorldSize 大小（與 PPU 無關）。
        float w = _sr.sprite != null ? _sr.sprite.bounds.size.x : 1f;
        float s = w > 0.0001f ? WorldSize / w : 1f;
        transform.localScale = new Vector3(s, s, 1f);
        _basePos = transform.position;
        _phase = Random.value * Mathf.PI * 2f;   // 多顆不要同步呼吸

        // 地上的柔和影子：讓「浮在地上」與「飛行中離地多高」看得出來
        var sh = new GameObject("Shadow");
        sh.transform.SetParent(null, false);
        _shadow = sh.AddComponent<SpriteRenderer>();
        _shadow.sprite = GetShadowSprite();
        _shadow.sortingLayerName = SortingLayer;
        _shadow.sortingOrder = ShadowSortingOrder;
        PlaceShadow(_basePos, 0f);
    }

    float BaseScale => _sr.sprite != null && _sr.sprite.bounds.size.x > 0.0001f ? WorldSize / _sr.sprite.bounds.size.x : 1f;

    // 影子放在「地面點」：球底下一點；height＝離地高度，越高影子越小越淡
    void PlaceShadow(Vector3 ground, float height)
    {
        if (_shadow == null) return;
        float k = 1f / (1f + height * 0.6f);
        _shadow.transform.position = ground + new Vector3(0f, -WorldSize * 0.48f, 0f);
        float w = _shadow.sprite.bounds.size.x;
        float s = w > 0.0001f ? WorldSize * 0.75f * k / w : 1f;
        _shadow.transform.localScale = new Vector3(s, s, 1f);
        _shadow.color = new Color(0f, 0f, 0f, ShadowAlpha * k);
    }

    void OnDestroy()
    {
        if (_shadow != null) Destroy(_shadow.gameObject);
    }

    void Update()
    {
        if (_absorbing || !_landed) return;   // 掉落動畫中：位置由 DropRoutine 管、也不能吸收

        // 呼吸＋漂浮
        _phase += Time.deltaTime;
        float pulse = 1f + Mathf.Sin(_phase * PulseSpeed) * PulseAmp;
        float baseScale = BaseScale;
        transform.localScale = new Vector3(baseScale * pulse, baseScale * pulse, 1f);
        transform.position = _basePos + new Vector3(0f, Mathf.Sin(_phase * BobSpeed) * BobAmp, 0f);

        // 碰觸判定（玩家碰撞位置 vs 球的地面點）
        if (!FindPlayer()) return;
        Vector2 d = (Vector2)_player.position - (Vector2)_basePos;
        if (d.sqrMagnitude > TouchRadius * TouchRadius) return;

        var st = _playerStats;
        if (st == null || st.IsDead) return;
        // 滿血滿魔 → 沒反應（球留著）。只要有一項沒滿就吸收，兩項一起補。
        bool hurt = st.Health < st.MaxHealth - 0.001f;
        bool drained = st.Mana < st.MaxMana - 0.001f;
        if (!hurt && !drained) return;

        st.Heal(st.MaxHealth * RestoreFraction);
        st.RestoreMana(st.MaxMana * RestoreFraction);
        // 玩家身上播回復特效：跟喝血瓶同一個（作者 2026-11-06）
        var pcFx = _player.GetComponent<PlayerController>();
        if (pcFx != null) pcFx.PlayDrinkPotionVfx();

        if (RunProgress.Exists)
        {
            int mapId = MapManager.Instance != null ? MapManager.Instance.CurrentMapId : -1;
            RunProgress.Instance.RemoveBloodOrb(mapId, RunOrbId);
        }
        StartCoroutine(AbsorbRoutine());
    }

    // ───────────────────────── 掉落動畫 ─────────────────────────
    // ① 在屍體位置由小放大冒出來＋白閃 → ② 拋物線飛到落點（畫在角色上面、拖殘影、地上影子跟著跑）
    // → ③ 落地壓扁＋擴散光環 → ④ 再小彈一下 → ⑤ 回到地上的呼吸漂浮，這時才可以吸收。
    // 用遊戲時間：暫停（對話）時整段凍住。
    System.Collections.IEnumerator DropRoutine(Vector2 from)
    {
        _landed = false;
        Vector3 land = _basePos;
        Vector3 src = new Vector3(from.x, from.y, 0f);
        float bs = BaseScale;
        _sr.sortingOrder = FlightSortingOrder;

        // ① 冒出來（屍體位置先炸一團白紅光暈，告訴玩家「有東西掉了」）
        SpawnPopFlash(src);
        float t = 0f;
        while (t < DropPopTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / DropPopTime);
            float e = 1f - (1f - k) * (1f - k);                     // ease-out
            transform.position = src;
            transform.localScale = Vector3.one * bs * Mathf.Lerp(0.2f, 1.15f, e);
            PlaceShadow(src, 0f);
            yield return null;
        }

        // ② 拋物線飛過去
        float dist = Vector2.Distance(from, land);
        float arc = DropArcHeight + dist * 0.25f;
        float trailTimer = 0f;
        t = 0f;
        while (t < DropFlightTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / DropFlightTime);
            Vector3 ground = Vector3.Lerp(src, land, k);
            float h = 4f * arc * k * (1f - k);
            transform.position = ground + new Vector3(0f, h, 0f);
            // 上升時拉長、下墜時也拉長，頂點最圓
            float stretch = Mathf.Abs(1f - 2f * k) * 0.12f;
            transform.localScale = new Vector3(bs * (1.15f - 0.15f * k) * (1f - stretch), bs * (1.15f - 0.15f * k) * (1f + stretch), 1f);
            PlaceShadow(ground, h);

            trailTimer += Time.deltaTime;
            if (trailTimer >= TrailInterval) { trailTimer = 0f; SpawnTrail(transform.position, transform.localScale); }
            yield return null;
        }
        transform.position = land;
        _sr.sortingOrder = SortingOrder;
        PlaceShadow(land, 0f);

        // ③ 落地：壓扁＋光環
        SpawnRing(land);
        t = 0f;
        while (t < DropSquashTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / DropSquashTime);
            float q = Mathf.Sin(k * Mathf.PI) * DropSquash;
            transform.localScale = new Vector3(bs * (1f + q), bs * (1f - q), 1f);
            transform.position = land + new Vector3(0f, -WorldSize * 0.5f * q, 0f);   // 壓扁時底部貼地
            yield return null;
        }

        // ④ 小彈一下
        t = 0f;
        while (t < DropBounceTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / DropBounceTime);
            float h = 4f * DropBounceHeight * k * (1f - k);
            transform.position = land + new Vector3(0f, h, 0f);
            transform.localScale = Vector3.one * bs;
            PlaceShadow(land, h);
            yield return null;
        }
        transform.position = land;
        PlaceShadow(land, 0f);

        // ⑤ 落定：從這一刻起才能吸收
        _phase = 0f;
        _landed = true;
    }

    // 飛行殘影：一個淡紅、慢慢縮小淡掉的分身
    void SpawnTrail(Vector3 pos, Vector3 scale)
    {
        var go = new GameObject("BloodOrbTrail");
        go.transform.position = pos;
        go.transform.localScale = scale * 0.85f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _sr.sprite;
        sr.sortingLayerName = SortingLayer;
        sr.sortingOrder = FlightSortingOrder - 1;
        sr.color = new Color(1f, 0.35f, 0.35f, 0.45f);
        float s0 = go.transform.localScale.x;
        go.AddComponent<FadeAndDie>().Init(TrailLife, 0.45f, s0, s0 * 0.4f);
    }

    // 冒出來的光暈：一團白紅柔光由小放大淡出（球本身是紅色，tint 只能變暗不能變亮，所以閃光用另一張圖疊上去）
    void SpawnPopFlash(Vector3 pos)
    {
        var go = new GameObject("BloodOrbFlash");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetGlowSprite();
        sr.sortingLayerName = SortingLayer;
        sr.sortingOrder = FlightSortingOrder + 1;
        sr.color = new Color(1f, 0.85f, 0.85f, 0.9f);
        float w = sr.sprite.bounds.size.x;
        float baseS = w > 0.0001f ? FxRefSize / w : 1f;
        go.AddComponent<FadeAndDie>().Init(0.28f, 0.9f, baseS * 0.4f, baseS * 1.5f);
    }

    // 落地光環：紅色細環由小擴大淡出
    void SpawnRing(Vector3 pos)
    {
        var go = new GameObject("BloodOrbRing");
        go.transform.position = pos;   // 光環圓心＝球的落點（球在光環正中間）
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetRingSprite();
        sr.sortingLayerName = SortingLayer;
        sr.sortingOrder = SortingOrder + 1;
        sr.color = new Color(1f, 0.3f, 0.3f, 0.85f);
        float w = sr.sprite.bounds.size.x;
        float baseS = w > 0.0001f ? FxRefSize / w : 1f;
        go.AddComponent<FadeAndDie>().Init(RingTime, 0.85f, baseS * 0.35f, baseS * RingMaxScale, squashY: 0.45f);
    }

    /// <summary>小工具：在 life 秒內 alpha 從 alpha0 淡到 0、縮放從 s0 變到 s1（絕對值；squashY＝Y 方向再乘的比例，光環壓扁成橢圓貼地用），然後銷毀。用遊戲時間。</summary>
    sealed class FadeAndDie : MonoBehaviour
    {
        SpriteRenderer _r; float _life, _t, _a0, _s0, _s1, _sy;
        public void Init(float life, float alpha0, float s0, float s1, float squashY = 1f)
        {
            _r = GetComponent<SpriteRenderer>(); _life = Mathf.Max(0.01f, life); _a0 = alpha0;
            _s0 = s0; _s1 = s1; _sy = squashY;
            transform.localScale = new Vector3(s0, s0 * squashY, 1f);
        }
        void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _life);
            float e = 1f - (1f - k) * (1f - k);
            float sc = Mathf.Lerp(_s0, _s1, e);
            transform.localScale = new Vector3(sc, sc * _sy, 1f);
            if (_r != null) { var c = _r.color; c.a = _a0 * (1f - k); _r.color = c; }
            if (k >= 1f) Destroy(gameObject);
        }
    }

    // 吸收：飛向玩家身體中心、縮小、淡出，然後銷毀。
    System.Collections.IEnumerator AbsorbRoutine()
    {
        _absorbing = true;
        if (_shadow != null) { Destroy(_shadow.gameObject); _shadow = null; }
        Vector3 from = transform.position;
        Vector3 startScale = transform.localScale;
        Color c0 = _sr.color;
        var pc = _player != null ? _player.GetComponent<PlayerController>() : null;
        float t = 0f;
        while (t < AbsorbDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / AbsorbDuration);
            float e = k * k;   // 越飛越快
            Vector3 to = pc != null ? (Vector3)pc.BodyCenterWorldPos : (_player != null ? _player.position : from);
            transform.position = Vector3.Lerp(from, to, e);
            transform.localScale = startScale * (1f - 0.8f * k);
            _sr.color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - k));
            yield return null;
        }
        Destroy(gameObject);
    }

    static bool FindPlayer()
    {
        if (_player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            _player = go != null ? go.transform : null;
            _playerStats = go != null ? go.GetComponent<CombatStats>() : null;
        }
        return _player != null;
    }

    // ── 外觀：有美術圖用美術圖，沒有就程序生成一顆半透明紅球 ──
    static Sprite GetSprite()
    {
        if (_cachedSprite != null) return _cachedSprite;
        var art = Resources.Load<Sprite>(ArtPath);
        if (art != null) return _cachedSprite = art;
        return _cachedSprite = MakeProceduralSprite();
    }

    // 柔和橢圓影子（中心黑、邊緣淡）
    static Sprite GetShadowSprite()
    {
        if (_shadowSprite != null) return _shadowSprite;
        const int W = 64, H = 24;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H * 2f - 1f;
            float r = Mathf.Sqrt(u * u + v * v);
            float a = Mathf.Clamp01(1f - r); a = a * a;
            px[y * W + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px); tex.Apply();
        return _shadowSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), W);
    }

    // 柔和圓形光暈（冒出來的閃光用）
    static Sprite _glowSprite;
    static Sprite GetGlowSprite()
    {
        if (_glowSprite != null) return _glowSprite;
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
            float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v)); a = a * a;
            px[y * N + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px); tex.Apply();
        return _glowSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
    }

    // 細環（落地光環用）
    static Sprite GetRingSprite()
    {
        if (_ringSprite != null) return _ringSprite;
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
            float r = Mathf.Sqrt(u * u + v * v);
            float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.85f) / 0.12f);   // 半徑 0.85 處一圈、寬約 0.12
            px[y * N + x] = new Color(1f, 1f, 1f, a * a);
        }
        tex.SetPixels(px); tex.Apply();
        return _ringSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
    }

    // GLSL 的 smoothstep（門檻 e0/e1 把 x 映射到 0~1）。⚠ 不能用 Mathf.SmoothStep——那支是「在 from~to 之間插值」，
    // 門檻不是 0/1 時會靜默算錯（PROBLEMS E32；2026-11-06 血球就踩到：球身 alpha 只剩約 5%，只看得到高光一個小點）。
    static float Smooth(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    static Sprite MakeProceduralSprite()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[N * N];
        Color body = new Color(0.78f, 0.04f, 0.08f);   // 深紅
        Color rim = new Color(1f, 0.25f, 0.25f);       // 邊緣亮紅
        Vector2 hl = new Vector2(-0.32f, 0.34f);       // 左上高光位置（-1~1 座標）
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
            float r = Mathf.Sqrt(u * u + v * v);
            if (r >= 1f) { px[y * N + x] = Color.clear; continue; }
            // 球體：中心較透、邊緣較實（像一顆透明的血珠），最外一圈柔邊
            float rimK = Smooth(0.55f, 0.92f, r);
            Color c = Color.Lerp(body, rim, rimK);
            float a = Mathf.Lerp(0.45f, 0.85f, rimK) * (1f - Smooth(0.9f, 1f, r));
            // 左上高光
            float h = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), hl) / 0.28f);
            h = h * h;
            c = Color.Lerp(c, Color.white, h * 0.85f);
            a = Mathf.Max(a, h * 0.9f);
            px[y * N + x] = new Color(c.r, c.g, c.b, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
    }
}
