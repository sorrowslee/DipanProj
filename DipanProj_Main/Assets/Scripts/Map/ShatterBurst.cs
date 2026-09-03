using UnityEngine;

/// <summary>
/// 地上物破壞碎片（程序化，零素材）：把物件<b>自己的那張圖</b>當場切成 N 塊，
/// 每塊生成一個短命的 <see cref="SpriteRenderer"/>，順著擊退方向飛開、旋轉、縮小、淡出。
///
/// <para><b>為什麼不用共用特效</b>：一個共用的破壞煙塵套在石雕、木桶、布幔上都長一樣，
/// 玩家會覺得「每種材質都同一種破法」。碎片直接來自該物件的貼圖，所以石頭爆出石頭色、
/// 木頭爆出木頭色、噴泉爆出石雕帶水花——<b>材質差異是免費附帶的，不必為每個物件畫圖</b>。</para>
///
/// <para><b>為什麼不用 shader</b>：頂點位移式的碎裂 shader 仍然要先有「切好的碎片網格」，
/// 也就是這裡的切割那一步跑不掉，只是把移動搬到 GPU。以本專案一次頂多爆幾個物件的量級，
/// CPU 這邊量不出成本；等真的有「一發 AOE 炸掉二十個物件」的需求再升級。</para>
///
/// <para><b>合批</b>：所有碎片指向<b>同一張貼圖</b>（<see cref="Sprite.Create"/> 只是換取樣區域，
/// 不複製貼圖、不配置貼圖記憶體），並沿用來源的材質與排序，因此會併成同一批繪製。</para>
///
/// <para><b>刻意的限制（v1 最小版）</b>：
/// ① 切法是規則網格（<see cref="Cols"/>×<see cref="Rows"/>），不是 Voronoi 尖角碎塊——
///    0.6 秒內、碎片又在縮小，肉眼讀不出矩形；要更不規則之後改用 <c>Sprite.OverrideGeometry</c>
///    餵多邊形頂點即可（仍是 SpriteRenderer、仍合批）。
/// ② 不做「全透明格子跳過」——那要 <c>GetPixels32()</c> 掃整張貼圖（1024² 會配置 4MB、造成 GC 突波）。
///    全透明的碎片只是畫不出東西，成本是一個空 renderer。之後要省，catalog 裡的
///    <c>FootprintMask</c>（subdiv 8）本來就知道哪些格子是空的，直接查表即可。
/// ③ 碎片<b>不掛任何 Collider</b>：掛了會擋路、還會被武器的目標搜尋當成命中對象（同 PROBLEMS B4 那類坑）。
/// </para>
///
/// <para><b>掛在哪</b>：<see cref="DestructibleObject.Die"/> 呼叫 <see cref="Play"/>，
/// 傳入被破壞物件的 SpriteRenderer 與命中點。碎片<b>不是被破壞物件的子物件</b>
/// （同 <c>BlobShadow</c> 的理由：來源會在同一幀被 Destroy），而是掛在它的<b>父節點</b>底下——
/// 這樣換圖時 <c>MapLoader</c> 拆掉地圖根節點會一併清掉飛到一半的碎片，不會殘留到下一張圖。</para>
/// </summary>
public class ShatterBurst : MonoBehaviour
{
    // ── 切法與手感（v1 先寫死；之後要分材質就改成一張 CSV，key = assetId，空白＝這裡的預設）──

    /// <summary>橫向切幾塊。</summary>
    public const int Cols = 3;
    /// <summary>縱向切幾塊。</summary>
    public const int Rows = 4;

    /// <summary>碎片存活秒數。</summary>
    const float Lifetime = 0.6f;
    /// <summary>開始淡出的時間點（佔存活秒數的比例）——前段維持不透明，看得清楚才有「碎掉」的感覺。</summary>
    const float FadeStart = 0.4f;
    /// <summary>飛散初速 = 物件可見高度 × 此值（世界單位/秒）。大物件碎片飛得遠，比例才不會怪。</summary>
    const float SpeedPerHeight = 1.0f;
    /// <summary>初速的隨機範圍（±此比例）。</summary>
    const float SpeedJitter = 0.45f;
    /// <summary>每秒殘餘速度比例（空氣阻力；愈小停得愈快）。</summary>
    const float Drag = 0.12f;
    /// <summary>俯視角的「落地感」：每秒往畫面下方多掉這麼多世界單位/秒。</summary>
    const float SinkAccel = 2.2f;
    /// <summary>自轉角速度上限（度/秒）。</summary>
    const float SpinMax = 320f;
    /// <summary>存活期間縮到原本的幾成（愈小愈像碎掉後散開）。</summary>
    const float EndScale = 0.72f;
    /// <summary>擊退方向與「由中心往外」的混合比（0＝完全照擊退方向、1＝完全往外炸）。</summary>
    const float RadialBlend = 0.55f;

    struct Piece
    {
        public Transform tr;
        public SpriteRenderer sr;
        public Vector2 vel;
        public float spin;
        public Vector3 baseScale;
        public Color baseColor;
    }

    Piece[] _pieces;
    Sprite[] _sprites;   // 自己 Sprite.Create 出來的，OnDestroy 要收（不能靠 SpriteRenderer 拿——
                         // 銷毀時子物件可能已經先沒了，那時 sr.sprite 取不到，Sprite 就會漏掉）
    float _t;

    /// <summary>
    /// 在 <paramref name="src"/> 的位置炸出碎片。
    /// <para><paramref name="hitDirection"/> = <b>擊退方向</b>（來源 → 目標，見 <c>IDamageable.TakeDamage</c> 與
    /// <c>DamageInfo.HitDirection</c>）——<b>不是命中座標</b>。整條傷害管線傳的都是方向；
    /// 傳 <see cref="Vector2.zero"/>（例如 DOT／地面特效那種沒有方向的來源）會退回「純往外炸」。</para>
    /// </summary>
    /// <remarks>
    /// 必須在來源被 <c>Destroy</c> <b>之前</b>呼叫——這裡要讀它的 transform 與 sprite。
    /// （<c>Destroy</c> 是延遲到當幀結束才真的執行，所以「先呼叫再 Destroy」是安全的。）
    /// </remarks>
    public static void Play(SpriteRenderer src, Vector2 hitDirection)
    {
        if (src == null) return;
        var sp = src.sprite;
        if (sp == null || sp.texture == null) return;

        var srcTr = src.transform;
        Vector3 srcScale = srcTr.lossyScale;
        if (Mathf.Approximately(srcScale.x, 0f) || Mathf.Approximately(srcScale.y, 0f)) return;

        // 根節點掛在來源的父節點下（＝地圖的地上物根節點），換圖拆地圖時會一併清掉。
        var root = new GameObject("ShatterBurst");
        var rootTr = root.transform;
        rootTr.SetParent(srcTr.parent, false);
        rootTr.localPosition = Vector3.zero;
        rootTr.localRotation = Quaternion.identity;
        rootTr.localScale = Vector3.one;

        // 碎片的 localScale 要換算回根節點底下（父鏈若有縮放才不會走鐘）。
        Vector3 rootScale = rootTr.lossyScale;
        Vector3 pieceScale = new Vector3(
            SafeDiv(srcScale.x, rootScale.x),
            SafeDiv(srcScale.y, rootScale.y),
            1f);

        Rect r = sp.rect;                 // 這張 sprite 在貼圖上的像素區域
        float ppu = sp.pixelsPerUnit;
        Vector2 pivotPx = sp.pivot;       // 相對 r.min 的像素（本專案的地上物是置中）
        // 切格用整數像素：Sprite.Create 的 rect 給小數會在格子邊緣取樣到隔壁格（半像素滲色），
        // 而 256/3 這種除不盡的情況一定會有小數。用整數除法算邊界，餘數自然分給最後一格。
        int rx = Mathf.RoundToInt(r.x), ry = Mathf.RoundToInt(r.y);
        int rw = Mathf.RoundToInt(r.width), rh = Mathf.RoundToInt(r.height);

        Vector3 centerWorld = src.bounds.center;
        Vector2 hitDir = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : Vector2.zero;
        var burst = root.AddComponent<ShatterBurst>();
        burst._pieces = new Piece[Cols * Rows];
        burst._sprites = new Sprite[Cols * Rows];

        // 飛散初速依「可見高度」給，大物件碎片才飛得夠遠（同 VfxManager.SpawnSizedToHeight 的比例思路）。
        float baseSpeed = Mathf.Max(0.5f, src.bounds.size.y * SpeedPerHeight);

        int n = 0;
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                // 這一格在貼圖上的區域（整數像素，邊界對齊、不重疊）。FullRect：不要 Tight mesh——
                // Tight 的 bounds 不保證等於格子，碎片位置會偏（同 MapLoader.BuildObjectCollision 的註解）。
                int cx0 = col * rw / Cols, cx1 = (col + 1) * rw / Cols;
                int cy0 = row * rh / Rows, cy1 = (row + 1) * rh / Rows;
                int cwI = cx1 - cx0, chI = cy1 - cy0;
                if (cwI <= 0 || chI <= 0) continue;   // 圖比格數還小（極端小圖）
                var cellRect = new Rect(rx + cx0, ry + cy0, cwI, chI);
                var cellSprite = Sprite.Create(sp.texture, cellRect, new Vector2(0.5f, 0.5f),
                                               ppu, 0, SpriteMeshType.FullRect);

                // 格子中心相對 sprite 原點（＝pivot）的本地位移；來源的旋轉/縮放/鏡射由 TransformPoint 處理。
                Vector2 cellCenterInRect = new Vector2(cx0 + cwI * 0.5f, cy0 + chI * 0.5f);
                Vector2 localOffset = (cellCenterInRect - pivotPx) / ppu;
                if (src.flipX) localOffset.x = -localOffset.x;   // 地上物是用負 scale 翻的，這裡只是保險
                if (src.flipY) localOffset.y = -localOffset.y;
                Vector3 world = srcTr.TransformPoint(localOffset);

                var go = new GameObject("Shard");
                go.layer = src.gameObject.layer;
                var tr = go.transform;
                tr.SetParent(rootTr, false);
                tr.position = world;
                tr.rotation = srcTr.rotation;
                tr.localScale = pieceScale;

                var psr = go.AddComponent<SpriteRenderer>();
                psr.sprite = cellSprite;
                psr.sharedMaterial = src.sharedMaterial;      // 沿用材質＋同一張貼圖 ⇒ 可合批
                psr.sortingLayerID = src.sortingLayerID;
                psr.sortingOrder = src.sortingOrder;          // 跟被破壞的物件同層，不會鑽到地板或角色底下
                psr.color = src.color;

                // 方向：順著擊退方向，混一份「由物件中心往外」——只照擊退方向的話所有碎片會平行飛、像整張圖被推走。
                Vector2 fromCenter = (Vector2)world - (Vector2)centerWorld;
                Vector2 radial = fromCenter.sqrMagnitude > 0.000001f
                    ? fromCenter.normalized
                    : Random.insideUnitCircle.normalized;
                Vector2 dir = hitDir == Vector2.zero ? radial : Vector2.Lerp(hitDir, radial, RadialBlend);
                if (dir.sqrMagnitude < 0.0001f) dir = radial;
                dir = (dir.normalized + Random.insideUnitCircle * 0.25f).normalized;

                burst._sprites[n] = cellSprite;
                burst._pieces[n++] = new Piece
                {
                    tr = tr,
                    sr = psr,
                    vel = dir * baseSpeed * Random.Range(1f - SpeedJitter, 1f + SpeedJitter),
                    spin = Random.Range(-SpinMax, SpinMax),
                    baseScale = pieceScale,
                    baseColor = psr.color,
                };
            }
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        _t += dt;
        float k = Mathf.Clamp01(_t / Lifetime);

        // 淡出：前段維持不透明（看清楚碎塊），後段線性到 0。
        float alpha = k <= FadeStart ? 1f : 1f - (k - FadeStart) / (1f - FadeStart);
        float scaleK = Mathf.Lerp(1f, EndScale, k);
        float dragK = Mathf.Pow(Drag, dt);   // 每秒殘餘 Drag 比例，與幀率無關

        for (int i = 0; i < _pieces.Length; i++)
        {
            var p = _pieces[i];
            if (p.tr == null) continue;

            p.vel *= dragK;
            p.vel.y -= SinkAccel * dt;       // 俯視角：往畫面下方沉一點，像落到地上
            p.tr.position += (Vector3)(p.vel * dt);
            p.tr.Rotate(0f, 0f, p.spin * dt);
            p.tr.localScale = p.baseScale * scaleK;

            var c = p.baseColor;
            c.a = p.baseColor.a * alpha;
            p.sr.color = c;

            _pieces[i] = p;
        }

        if (k >= 1f) Destroy(gameObject);
    }

    /// <summary>
    /// 收掉 <see cref="Sprite.Create"/> 當場產生的 Sprite。
    /// ⚠ runtime 建的 Sprite <b>不會</b>跟著 SpriteRenderer 一起被回收，不手動 Destroy 會一路累積到換場景
    /// （貼圖是共用的、不會被動到，這裡只收 Sprite 物件本身）。
    /// </summary>
    void OnDestroy()
    {
        if (_sprites == null) return;
        for (int i = 0; i < _sprites.Length; i++)
            if (_sprites[i] != null) Destroy(_sprites[i]);
    }

    static float SafeDiv(float a, float b) => Mathf.Approximately(b, 0f) ? a : a / b;
}
