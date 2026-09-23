using UnityEngine;

/// <summary>
/// **骨牢的視覺**（玩家被 <c>bindPlayer</c> 綁住、怪被 <c>WeaponMode.Cage</c> 關住，兩邊共用這一份）。
///
/// <para>兩張圖夾住角色：<c>bone_prison_back</c> 畫在角色**後面**、<c>bone_prison_front</c> 畫在**前面**，
/// 角色夾在中間 ⇒ 看起來是真的被關在裡面。這正是它**不能做成 VfxTable 一列**的原因——
/// VfxTable 一列只有一個 <c>SortingOrder</c>，表達不了前後夾層。</para>
///
/// <para><b>對位</b>：呼叫端給了 <c>anchorSpot</c> 就用它（怪物端：<c>MonsterAnimator.TryGetCageAnchorLocal</c>——
/// X＝idle **軀幹**中心、Y＝idle 地面線，一次算好、不隨動作跳）；沒給才問影子（<see cref="BlobShadow.TryGetGroundSpot"/>，玩家端）。
/// ⚠ 2026-09-23 第四版：只問影子不夠——影子的 X 是**兩腳中點**，拿武器的怪兩腳之間被拖地的武器佔住，
/// 籠子就套到武器上去了（ZhaYu_HugeSword 偏 0.6 單位）；而且影子錨點每換一個動作就換一組，
/// 每幀跟著它走＝籠子在怪身邊左右滑。籠子罩的是**身體**，所以 X 對軀幹、Y 才對地面（詳見 PROBLEMS **G15**）。
/// 第一版用 <c>FeetWorldPos</c>（X＝畫布中心）同樣不準，別退回去。
/// <b>但大小不跟影子走</b>：影子寬量的是「底部 15% 帶的跨距」，
/// 拿武器的怪會把**拖在地上的武器**一起算進去（實測 ZhaYu_HugeSword 影子寬 3.04 ＞ 牠的身高 2.63，
/// 牢籠因此大了一倍多）。尺寸改用**可見身高**——那個不受手上拿什麼影響。</para>
///
/// <para>生長演出＝兩件事一起做，缺一個都不像「長出來」：
/// ① shader <c>Custom/BoneCageGrow</c> 由下往上揭露，門檻依 X 抖動 ⇒ 三根骨刺**錯開**破土；
/// ② 本元件對整個根節點做**縱向超調回彈**（0.72 → 1.06 → 1.0）。①負責「冒出來」、②負責「力道」。</para>
///
/// <para>⚠ 這東西**不會自己消失**，一定要有人呼叫 <see cref="Burst"/> 或 <see cref="Dismiss"/>。
/// 持有者（<c>PlayerBind</c>／<c>MonsterCage</c>）在自己被停用時都要收掉它。</para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]   // 排在 YSortByFeet（預設 0）之後：每幀讀到的是角色**這一幀**的排序值，不會落後一幀
public class BoneCageVisual : MonoBehaviour
{
    // ── 素材 ──
    const string BackPath = "Weapon/single/bone_prison/bone_prison_back";
    const string FrontPath = "Weapon/single/bone_prison/bone_prison_front";
    const string ShaderName = "Custom/BoneCageGrow";

    // ── 圖的幾何（**量出來的，不是目測**；換圖要重量）──
    /// <summary>骨刺根部在地面線附近的水平跨距佔圖寬的比例＝「牢籠內徑」。實測 back 圖 0.652。</summary>
    const float InnerWidthRatio = 0.652f;
    /// <summary>圖裡的「地面線」（＝中央那片角色站的暗紅橢圓中心）在圖高的幾成處，從**底部**量。實測 0.33。</summary>
    const float GroundLineFromBottom = 0.33f;

/// <summary>
    /// 牢籠內徑 ÷ **可見身高**的預設比例。俯視角下一座剛好圍住角色的圈大約是身高的七成寬。
    /// 玩家端與怪物端共用同一個數字；武器的「牢籠大小」（<c>BulletScale</c>）再乘上去。
    /// ⚠ 這裡刻意用身高而不是影子寬，理由見檔頭。
    /// </summary>
    public const float InnerWidthPerBodyHeight = 0.7f;

    /// <summary>
    /// 牢籠內徑 ÷ **軀幹寬**的下限（怪物端）：內徑至少要比軀幹寬一成半，兩側骨刺才罩得住身體。
    /// 瘦高的怪由上面的身高比例決定，寬胖的怪（ZhaYu_Bomb）由這條決定。見 MonsterCage.Begin。
    /// </summary>
    public const float InnerWidthPerTorsoWidth = 1.15f;

    // ── 演出參數 ──
    const float GrowSeconds = 0.45f;
    const float StartScaleY = 0.72f, OvershootScaleY = 1.06f;
    const float OvershootAt = 0.75f;
    const float EdgeGlowPeak = 0.5f;
    /// <summary>崩裂後多久收掉根節點。要比 ShatterBurst 的碎片壽命(0.6s)長，否則碎片會跟著被清掉。</summary>
    const float DestroyDelayAfterBurst = 0.8f;

    // ── 排序：角色是 10，前後各讓一階 ──
    const int BackOrderOffset = -1, FrontOrderOffset = +1;

    static readonly int GrowId = Shader.PropertyToID("_Grow");
    static readonly int EdgeGlowId = Shader.PropertyToID("_EdgeGlow");

    SpriteRenderer _back, _front;
    SpriteRenderer _sortRef;            // 被關住的角色；前後兩片每幀跟著它的排序值走（見 SyncSorting）
    MaterialPropertyBlock _mpb;
    BlobShadow _shadow;                 // 對位的單一真相
    System.Func<Vector2> _anchorSpot;   // 呼叫端指定的籠心（優先於影子；見檔頭〈對位〉）
    System.Func<Vector2> _fallbackSpot; // 沒有影子時的退路
    float _elapsed;
    bool _bursting;

    /// <summary>
    /// 生一座骨牢，套在 <paramref name="host"/> 身上。
    /// </summary>
    /// <param name="host">被關住的角色（玩家或怪）。位置每幀跟著它的影子走。</param>
    /// <param name="innerWidth">牢籠內徑的世界寬（呼叫端算好：可見身高 × <see cref="InnerWidthPerBodyHeight"/> × 施放大小）。</param>
    /// <param name="bodyHeight">那個角色的可見身高，只用來做「影子是不是量歪了」的診斷。</param>
    /// <param name="fallbackSpot">沒有影子時的地面中心退路（通常給 FeetWorldPos）。</param>
    /// <param name="anchorSpot">籠心的世界座標（每幀呼叫）。給了就**優先於影子**；null＝問影子（見檔頭〈對位〉）。</param>
    public static BoneCageVisual Spawn(GameObject host, float innerWidth, float bodyHeight,
                                       System.Func<Vector2> fallbackSpot, System.Func<Vector2> anchorSpot = null)
    {
        if (host == null) return null;

        var backSp = Resources.Load<Sprite>(BackPath);
        var frontSp = Resources.Load<Sprite>(FrontPath);
        if (backSp == null && frontSp == null)
        {
            Debug.LogWarning($"[BoneCage] 兩張骨牢圖都載不到（{BackPath} / {FrontPath}）——" +
                             "確認它們在 Resources 下、且匯入類型是 Sprite。這次不放視覺。");
            return null;
        }

        var shadow = host.GetComponent<BlobShadow>();
        var sortRef = host.GetComponent<SpriteRenderer>() ?? host.GetComponentInChildren<SpriteRenderer>();

        // 籠心：呼叫端指定的優先（怪物＝軀幹 X＋地面 Y），否則問影子（見檔頭〈對位〉）。影子寬照樣取來做診斷。
        // 兩個都沒有才用退路，並且講出來——
        // 「悄悄用了另一套座標」正是上一版對不準卻查不到原因的那種 bug。
        Vector2 center = host.transform.position;
        float shadowW = 0f;
        bool hasShadow = shadow != null && shadow.TryGetGroundSpot(out center, out shadowW);
        Vector2 shadowCenter = hasShadow ? center : Vector2.zero;
        if (anchorSpot != null) center = anchorSpot();
        else if (hasShadow) { }
        else
        {
            center = fallbackSpot != null ? fallbackSpot() : (Vector2)host.transform.position;
            Debug.LogWarning($"[BoneCage] 「{host.name}」身上沒有可用的 BlobShadow，" +
                             "骨牢改用退路座標 ⇒ 位置可能跟影子對不齊。");
        }

        // 順手診斷：影子寬理當明顯小於身高（它是腳下的橢圓）。比身高還寬多半是那隻角色的
        // 影子錨點把**拖在地上的武器**一起算進「底部帶跨距」了——影子本身會偏，不只影響骨牢。
        if (bodyHeight > 0.01f && shadowW > bodyHeight)
            Debug.LogWarning($"[BoneCage] 「{host.name}」的影子寬 {shadowW:0.00} 比可見身高 {bodyHeight:0.00} 還大，" +
                             "那隻角色的影子錨點多半把拖在地上的武器算進去了（Project Tools 重算一次）。" +
                             (anchorSpot != null ? "骨牢的大小與位置都不吃這個值，但**影子本身**是偏的。"
                                                 : "骨牢的**大小**不吃這個值，但**位置**會跟著影子一起偏。"));

        innerWidth = Mathf.Max(0.05f, innerWidth);
        float imageWidth = innerWidth / InnerWidthRatio;   // 圖要多寬，牢籠內徑才有那麼大

        var shader = Shader.Find(ShaderName);
        if (shader == null)
            Debug.LogWarning($"[BoneCage] 找不到 shader「{ShaderName}」，改用預設 sprite 材質（沒有生長演出）。");

        var go = new GameObject("[BoneCage]");
        go.transform.position = center;
        var v = go.AddComponent<BoneCageVisual>();
        v._shadow = shadow;
        v._anchorSpot = anchorSpot;
        v._fallbackSpot = fallbackSpot;
        v._mpb = new MaterialPropertyBlock();
        v._sortRef = sortRef;

        string layer = sortRef != null ? sortRef.sortingLayerName : "Default";
        int order = sortRef != null ? sortRef.sortingOrder : 10;

        v._back = v.MakePart("Back", backSp, shader, layer, order + BackOrderOffset, imageWidth);
        v._front = v.MakePart("Front", frontSp, shader, layer, order + FrontOrderOffset, imageWidth);
        v.ApplyGrow(0f);

        string where = anchorSpot != null
            ? (hasShadow ? $"籠心對軀幹（與當下影子差 X {center.x - shadowCenter.x:+0.00;-0.00}、Y {center.y - shadowCenter.y:+0.00;-0.00}）"
                         : "籠心對軀幹")
            : (hasShadow ? "籠心對影子" : "籠心用退路座標");
        Debug.Log($"[BoneCage] 套在「{host.name}」：可見身高 {bodyHeight:0.00}、牢籠內徑 {innerWidth:0.00}、" +
                  $"圖寬 {imageWidth:0.00}（影子寬 {shadowW:0.00}）；{where}。大小調武器的「牢籠大小」欄。");
        return v;
    }

    // ══════════════════════════════════════════════════════════════════════
    // 籠心與尺寸（怪物端 MonsterAnimator／玩家端 PlayerAnimator 共用，2026-09-23）
    // ══════════════════════════════════════════════════════════════════════
    // ⚠ 這**不是**影子的第二份真相，是**另一個問題的答案**（PROBLEMS G15）：
    //   ‧ 影子是「腳下的一灘」→ X 取兩腳中點。拿武器的怪兩腳之間常被**拖在地上的武器**佔據，
    //     ZhaYu_HugeSword idle 的影子 X 在畫布中心右邊 +43.5px，軀幹卻在 −8.7px ⇒ 差 0.6 世界單位。
    //   ‧ 骨牢是「罩住整個身體的籠子」→ X 該對**軀幹**，Y 才對地面。
    //   ‧ 籠子是實體，**不該跟著動作換錨點跳**：影子錨點逐動作不同，每幀跟著走籠子會左右滑。
    // ⇒ 一律用 **idle** 算一次：X＝idle 各幀「可見框上方 TorsoFraction 的像素欄質心」中位數、
    //   Y＝idle 的影子錨點 Y（與 idle 影子同一條地面線；沒有就退回可見框底）、軀幹寬＝同一段像素的跨距中位數。

    /// <summary>軀幹取可見框上方多少比例（下方是腿、拖地的武器、長袍下擺——這些都會把中心拉歪）。</summary>
    const float TorsoFraction = 0.6f;
    /// <summary>軀幹寬取像素分佈的哪一段（兩端各去掉這個比例），免得一根飄出去的毛髮/破布把寬度撐大。</summary>
    const float TorsoWidthTrim = 0.02f;

    /// <summary>
    /// 用 idle 幀算籠心（相對 transform 的本地位移，**未乘 lossyScale、未翻面**）與軀幹寬（本地單位）。
    /// 貼圖不可讀／沒有幀回 false ⇒ 呼叫端退回問影子。
    /// </summary>
    public static bool ComputeIdleAnchor(Sprite[] frames, ShadowAnchorPx idleAnchor, out Vector2 local, out float torsoWidth)
    {
        local = Vector2.zero; torsoWidth = 0f;
        if (frames == null || frames.Length == 0) return false;

        var xs = new System.Collections.Generic.List<float>(frames.Length);
        var bottoms = new System.Collections.Generic.List<float>(frames.Length);
        var widths = new System.Collections.Generic.List<float>(frames.Length);
        Sprite first = null;
        foreach (var sp in frames)
        {
            if (sp == null || sp.pixelsPerUnit <= 0.0001f) continue;
            if (!TryMeasureTorso(sp, out float cxPx, out float bottomPx, out float widthPx)) continue;
            if (first == null) first = sp;
            // 每幀用**自己的** rect／pivot 換算：同一個資料夾混了不同畫布的舊圖時（PROBLEMS F28），至少不會整組飛掉。
            xs.Add((cxPx - sp.pivot.x) / sp.pixelsPerUnit);
            bottoms.Add((bottomPx - sp.pivot.y) / sp.pixelsPerUnit);
            widths.Add(widthPx / sp.pixelsPerUnit);
        }
        if (xs.Count == 0 || first == null) return false;

        float y;
        if (idleAnchor.ok)
        {
            float sy = (idleAnchor.canvasH > 0) ? first.rect.height / idleAnchor.canvasH : 1f;   // 同 BlobShadow 的換算
            y = (idleAnchor.yFromBottomPx * sy - first.pivot.y) / first.pixelsPerUnit;
        }
        else y = ShadowAnchorMath.Median(bottoms);

        local = new Vector2(ShadowAnchorMath.Median(xs), y);
        torsoWidth = ShadowAnchorMath.Median(widths);
        return true;
    }

    /// <summary>
    /// 把本地籠心換成**每幀**的世界座標：套上當下的位置／體型／翻面／離地高度（同 BlobShadow 的換算）。
    /// ⚠ 翻面時 X 取負（錨點是未翻面的來源圖方向）——角色轉身時籠子會跟著身體對稱移一下，這是對的。
    /// </summary>
    public static System.Func<Vector2> MakeSpot(Transform tr, SpriteRenderer sr, Vector2 local)
    {
        var air = tr != null ? tr.GetComponent<IAirborneVisual>() : null;
        return () =>
        {
            if (tr == null) return Vector2.zero;
            Vector3 p = tr.position;
            Vector3 ls = tr.lossyScale;
            float flip = (sr != null && sr.flipX) ? -1f : 1f;
            float airH = air != null ? Mathf.Max(0f, air.AirborneHeight) : 0f;   // 騰空時 transform 被往上推，扣回地面
            return new Vector2(p.x + local.x * ls.x * flip, p.y + local.y * ls.y - airH);
        };
    }

    /// <summary>牢籠內徑（世界單位，未乘施放倍率）＝ max(可見身高 × 0.7, 軀幹寬 × 1.15)。兩端共用。</summary>
    public static float InnerWidthFor(float bodyHeight, float torsoWidthWorld)
        => Mathf.Max(bodyHeight * InnerWidthPerBodyHeight, torsoWidthWorld * InnerWidthPerTorsoWidth);

    /// <summary>量一幀：可見框上方 <see cref="TorsoFraction"/> 的像素欄質心 X、軀幹寬、可見框底 Y（都是相對 sprite rect 左下的像素）。</summary>
    static bool TryMeasureTorso(Sprite sp, out float cxPx, out float bottomPx, out float widthPx)
    {
        cxPx = 0f; bottomPx = 0f; widthPx = 0f;
        var tex = sp.texture;
        if (tex == null || !tex.isReadable) return false;
        Color32[] px;
        try { px = tex.GetPixels32(); } catch { return false; }

        // ⚠⚠ 一定要用 sp.rect，**不能用 sp.textureRect**：Sprite.Create 預設是 Tight 網格，Unity 會把四周透明邊裁掉，
        //    textureRect 回傳的是**裁過的**框（左緣往右縮了 textureRectOffset.x）⇒ 量出來的 X 相對裁過的左緣，
        //    再拿去減「相對整張 rect 的 pivot」就整個往左偏。2026-09-23 第一版就栽在這：四隻怪全部往左 0.6~1.0 單位，
        //    偏多少取決於各自左邊透明邊有多寬（所以每隻都不一樣、看起來毫無規律）。
        Rect r = sp.rect;
        int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y);
        int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height), tw = tex.width;
        byte thr = ShadowAnchorMath.AlphaThreshold;

        int minY = int.MaxValue, maxY = -1;
        for (int y = 0; y < h; y++)
        {
            int row = (y0 + y) * tw + x0;
            for (int x = 0; x < w; x++)
                if (px[row + x].a > thr) { if (y < minY) minY = y; maxY = y; break; }
        }
        if (maxY < 0) return false;

        int from = maxY - Mathf.Max(1, Mathf.RoundToInt((maxY - minY + 1) * TorsoFraction)) + 1;
        double sum = 0; long cnt = 0;
        var colCount = new int[w];
        for (int y = Mathf.Max(from, minY); y <= maxY; y++)
        {
            int row = (y0 + y) * tw + x0;
            for (int x = 0; x < w; x++)
                if (px[row + x].a > thr) { sum += x; cnt++; colCount[x]++; }
        }
        if (cnt == 0) return false;
        cxPx = (float)(sum / cnt) + 0.5f;   // 像素中心

        // 軀幹寬：像素分佈去掉兩端各 TorsoWidthTrim 之後的跨距
        long lo = (long)(cnt * TorsoWidthTrim), hi = (long)(cnt * (1f - TorsoWidthTrim));
        long acc = 0; int xLo = -1, xHi = -1;
        for (int x = 0; x < w; x++)
        {
            acc += colCount[x];
            if (xLo < 0 && acc > lo) xLo = x;
            if (xHi < 0 && acc >= hi) { xHi = x; break; }
        }
        widthPx = (xLo >= 0 && xHi >= xLo) ? (xHi - xLo + 1) : 0f;
        bottomPx = minY;
        return true;
    }

    SpriteRenderer MakePart(string name, Sprite sp, Shader shader, string layer, int order, float imageWidth)
    {
        if (sp == null) return null;

        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        if (shader != null) sr.material = new Material(shader);

        // 等比縮放到指定圖寬，並把**地面線**對到根節點原點（＝影子中心）。
        Vector2 native = sp.bounds.size;
        float scale = (native.x > 0.0001f && imageWidth > 0f) ? imageWidth / native.x : 1f;
        go.transform.localScale = new Vector3(scale, scale, 1f);
        go.transform.localPosition = new Vector3(0f, (0.5f - GroundLineFromBottom) * native.y * scale, 0f);
        return sr;
    }

    /// <summary>
    /// 前後兩片的排序**每幀**對齊角色：back ＝ 角色 −1（被角色蓋住）、front ＝ 角色 ＋1（蓋住角色）。
    /// ⚠ 以前只在 Spawn 抓一次——但角色的 sortingOrder 是 <c>YSortByFeet</c> 依腳底 Y **每幀**算的，
    ///   每 0.01 世界單位就差 1（MapDepthSort.SortScale＝100），而前後片只跟角色差 1。
    ///   被關的怪只要被打得晃一下（擊退）、或 Spawn 那一幀還沒排序，back 就翻到角色前面 ⇒
    ///   **後面那片中間的骨柱畫在怪身上**（作者 2026-09-23 實測：四隻只有一隻對，純看運氣）。見 PROBLEMS G16。
    /// </summary>
    void SyncSorting()
    {
        if (_sortRef == null) return;
        int order = _sortRef.sortingOrder;
        int layer = _sortRef.sortingLayerID;
        if (_back != null) { _back.sortingLayerID = layer; _back.sortingOrder = order + BackOrderOffset; }
        if (_front != null) { _front.sortingLayerID = layer; _front.sortingOrder = order + FrontOrderOffset; }
    }

    void LateUpdate()
    {
        SyncSorting();

        // 跟著籠心／影子走（被擊退／被推開時牢籠不跟上去會整個穿幫）。
        // ⚠ 只跟**位置**不跟大小：影子寬會隨動作換圖而變，跟著改會讓牢籠忽大忽小。
        if (!_bursting)
        {
            if (_anchorSpot != null) transform.position = _anchorSpot();
            else if (_shadow != null && _shadow.TryGetGroundSpot(out var c, out _)) transform.position = c;
            else if (_fallbackSpot != null) transform.position = _fallbackSpot();
        }

        if (_bursting || _elapsed >= GrowSeconds) return;

        _elapsed += Time.deltaTime;
        ApplyGrow(Mathf.Clamp01(_elapsed / GrowSeconds));
    }

    void ApplyGrow(float t)
    {
        // 揭露走 ease-out（一開始快、收尾慢）＝破土的感覺。
        float grow = 1f - Mathf.Pow(1f - t, 3f);

        // 縱向超調回彈：矮胖 → 衝過頭 → 落回 1。這才是「長出來」的力道來源。
        // 支點在根節點原點＝地面線，所以地上的血色法陣幾乎不動，只有骨刺在抽長。
        float sy = t < OvershootAt
            ? Mathf.Lerp(StartScaleY, OvershootScaleY, 1f - Mathf.Pow(1f - t / OvershootAt, 3f))
            : Mathf.Lerp(OvershootScaleY, 1f, 1f - Mathf.Pow(1f - (t - OvershootAt) / (1f - OvershootAt), 3f));
        transform.localScale = new Vector3(1f, sy, 1f);

        float edge = EdgeGlowPeak * (1f - t);   // 破土時最亮，長完熄掉
        SetPart(_back, grow, edge);
        SetPart(_front, grow, edge);
    }

    void SetPart(SpriteRenderer sr, float grow, float edge)
    {
        if (sr == null) return;
        sr.GetPropertyBlock(_mpb);
        _mpb.SetFloat(GrowId, grow);
        _mpb.SetFloat(EdgeGlowId, edge);
        sr.SetPropertyBlock(_mpb);
    }

    /// <summary>
    /// 崩裂：兩片都切成碎片炸開，然後收掉自己。
    /// ⚠ 碎片是 <c>ShatterBurst</c> 掛在**本節點底下**的，所以不能馬上 Destroy 根節點——
    ///   要等過碎片壽命（見 <see cref="DestroyDelayAfterBurst"/>），否則碎片會跟著一起消失、什麼都看不到。
    /// </summary>
    public void Burst(Vector2 direction)
    {
        if (_bursting) return;
        _bursting = true;

        ShatterBurst.Play(_back, direction);
        ShatterBurst.Play(_front, direction);
        if (_back != null) _back.enabled = false;
        if (_front != null) _front.enabled = false;

        Destroy(gameObject, DestroyDelayAfterBurst);
    }

    /// <summary>不崩裂、直接收掉（玩家解綁、換圖、目標死掉都走這條）。呼叫幾次都安全。</summary>
    public void Dismiss()
    {
        if (this == null) return;
        Destroy(gameObject);
    }
}
