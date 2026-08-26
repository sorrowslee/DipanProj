using System.Collections.Generic;
using UnityEngine;
using Dipan.MapRuntime;   // 玩家外型走「地圖素材管線」(catalog + StreamingAssets)，與怪物同套

/// <summary>
/// 玩家外型素材庫（路線 B：程式逐格動畫，零 prefab/Animator）。與 <see cref="MonsterSpriteLibrary"/> 同模式，
/// 只是換成「血統(bloodline) + 動作」當鍵——方便「血統換外型」。
///
/// 慣例：每個血統一個資料夾、每個動作一個子資料夾、單張 PNG 一幀：
///   GameAssets/Main/Characters/SequenceImage/&lt;血統&gt;/idle/idle_01.png ...
///                                            /walk/walk_01.png ...
///                                            /dead/dead_01.png ...（一次性）
/// 「Base」= 預設初始外型。同步工具把每個動作葉資料夾收成一筆 catalog item（category=Characters）。
///
/// 懶漢單例：第一次存取 <see cref="Instance"/> 自動載入 catalog 一次。
/// </summary>
public class PlayerSpriteLibrary
{
    public const string Marker = "Characters/SequenceImage/";

    static PlayerSpriteLibrary _instance;
    public static PlayerSpriteLibrary Instance
    {
        get
        {
            if (_instance == null) { _instance = new PlayerSpriteLibrary(); _instance.Load(); }
            return _instance;
        }
    }

    /// <summary>進入 Play 模式時丟掉單例（已關 Domain Reload；否則 static 快取會回傳上一輪被銷毀的 sprite → 角色只剩影子/不見）。由 PlayModeStaticReset 呼叫。</summary>
    public static void ResetForPlayMode() => _instance = null;

    readonly Dictionary<string, CatalogItem> _byTail = new Dictionary<string, CatalogItem>();   // "<血統>/<state>"(小寫)
    readonly Dictionary<string, Sprite[]> _frameCache = new Dictionary<string, Sprite[]>();
    MapSpriteLoader _loader;

    void Load()
    {
        var catalog = CatalogLoader.Load(out string assetRoot);
        _loader = new MapSpriteLoader(assetRoot);

        int n = 0;
        foreach (var item in catalog.items)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) continue;
            int idx = item.id.IndexOf(Marker, System.StringComparison.Ordinal);
            if (idx < 0) continue;
            string tail = item.id.Substring(idx + Marker.Length).ToLowerInvariant();   // "<血統>/<state>"
            _byTail[tail] = item;
            n++;
        }
        Debug.Log($"[PlayerSpriteLibrary] 索引 {n} 筆玩家外型素材。");
    }

    static string Key(string bloodline, string state)
        => $"{(bloodline ?? "").Trim().ToLowerInvariant()}/{(state ?? "").Trim().ToLowerInvariant()}";

    /// <summary>這個血統有沒有這個動作的圖（防呆判斷用）。</summary>
    public bool Has(string bloodline, string state)
    {
        string k = Key(bloodline, state);
        if (_frameCache.ContainsKey(k)) return _frameCache[k] != null;
        return _byTail.ContainsKey(k);
    }

    /// <summary>
    /// 取某血統某動作的幀（依序）。單張資料夾 → 長度 1（靜態姿勢）；找不到回 null。結果快取。
    /// <paramref name="tileSize"/> 決定顯示大小（PPU=256/tileSize）：256px 幀以 tileSize=1 → 1 世界單位，
    /// 用較大值把低解析度的圖放大到想要的角色尺寸（見 PlayerAnimator 的自動換算）。
    /// </summary>
    /// <param name="bodyScale">
    /// 體型倍率（1 = 原樣）。&gt;1 時會把 sprite 的 pivot 往下移，讓**可見腳底維持在體型 1 時的位置、
    /// 只往上長**——不然置中 pivot 會讓角色上下同時長，1.5 倍等於腳往下沉快半格（看起來像陷進地板）。
    /// <b>bodyScale = 1 時完全不動 pivot</b>，行為與改動前一模一樣。
    /// </param>
    public Sprite[] GetFrames(string bloodline, string state, float tileSize = 1f, float bodyScale = 1f)
    {
        string tail = Key(bloodline, state);
        if (bodyScale <= 0.01f) bodyScale = 1f;
        string cacheKey = $"{tail}|{tileSize}|{bodyScale}";
        if (_frameCache.TryGetValue(cacheKey, out var cached)) return cached;

        Sprite[] frames = null;
        if (_byTail.TryGetValue(tail, out var item) && _loader != null)
        {
            if (item.IsAnimated)
            {
                frames = _loader.GetAnimationFrames(item, tileSize);
            }
            else
            {
                var sp = _loader.GetWholeSprite(item, tileSize);
                if (sp != null) frames = new[] { sp };
            }
            frames = ApplyFootPivot(frames, bloodline, state, bodyScale);
        }
        _frameCache[cacheKey] = frames;
        return frames;
    }

    /// <summary>
    /// 可見內容底緣佔畫布高的比例（0 = 貼齊畫布底、1 = 貼齊畫布頂）。
    /// ⚠ 一定要除以 <paramref name="canvas"/>.y——早期版本假設畫布恆為 256px 而省略了這一步，
    /// 目前的角色圖剛好全是 256×256 所以算出來一樣，但哪天有人丟一張 512px 的進來就會**靜默算錯**。
    /// </summary>
    public static float VisibleBottomFraction(Vector2 visibleSize, Vector2 visibleOffset, Vector2 canvas)
    {
        float h = canvas.y > 0.0001f ? canvas.y : 1f;
        return 0.5f + (visibleOffset.y - visibleSize.y * 0.5f) / h;
    }

    /// <summary>
    /// 「腳底錨點」的 pivot y。<paramref name="fy"/> 見 <see cref="VisibleBottomFraction"/>。
    /// bodyScale = 1 時回 0.5（＝專案原本的置中 pivot），所以不放大的血統零影響。
    /// </summary>
    public static float FootPivotY(float fy, float bodyScale)
    {
        if (bodyScale <= 0.01f) bodyScale = 1f;
        return Mathf.Clamp(fy - (fy - 0.5f) / bodyScale, -1f, 2f);   // 極端倍率的保險，正常落在 0~0.5
    }

    /// <summary>
    /// 把整組幀改成「腳底錨點」的 pivot：放大時可見腳底留在原位、只往上長。
    ///
    /// 推導：pivot 的 y 以畫布底為 0、頂為 1。令 fy = 可見內容底緣佔畫布高的比例，
    /// 則可見腳底相對 transform 的位移 = (fy − pivotY) × tileSize，而 tileSize 與 bodyScale 成正比。
    /// 要讓這個位移在任何倍率下都等於「倍率 1 時的值」，解出
    ///   pivotY = fy − (fy − 0.5) / bodyScale
    /// bodyScale = 1 時剛好回到 0.5（＝專案原本的置中 pivot），所以**不放大的血統零影響**。
    ///
    /// ⚠ 刻意在這裡就地重建 sprite，而不是去改 <c>MapSpriteLoader</c> 的預設 pivot——
    /// 那支是怪物、地上物、背景共用的，動它等於動全遊戲。Sprite.Create 只是換個描述、不複製貼圖，很便宜。
    /// </summary>
    Sprite[] ApplyFootPivot(Sprite[] frames, string bloodline, string state, float bodyScale)
    {
        if (frames == null || frames.Length == 0) return frames;
        if (Mathf.Approximately(bodyScale, 1f)) return frames;
        if (!TryGetVisibleBox(bloodline, state, out var size, out var offset, out var canvas)) return frames;

        float fy = VisibleBottomFraction(size, offset, canvas);
        float pivotY = FootPivotY(fy, bodyScale);

        var outFrames = new Sprite[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            var f = frames[i];
            if (f == null) { outFrames[i] = null; continue; }
            outFrames[i] = Sprite.Create(f.texture, f.rect, new Vector2(0.5f, pivotY), f.pixelsPerUnit);
        }
        return outFrames;
    }

    /// <summary>
    /// 取某血統某動作（代表幀＝該動作第一幀）的「不透明像素貼合框」：size/offset 為世界單位 @ PPU 256
    /// （tileSize 1）。用來把不同解析度/留白的圖換算成一致的角色顯示高度。沿用 MapSpriteLoader.GetAlphaLocalBox。
    /// </summary>
    public bool TryGetVisibleBox(string bloodline, string state, out Vector2 size, out Vector2 offset)
        => TryGetVisibleBox(bloodline, state, out size, out offset, out _);

    /// <summary>同上，另外吐出整張畫布的世界尺寸（算「可見內容佔畫布比例」時要用）。</summary>
    public bool TryGetVisibleBox(string bloodline, string state, out Vector2 size, out Vector2 offset,
                                 out Vector2 canvas)
    {
        size = default; offset = default; canvas = Vector2.one;
        string tail = Key(bloodline, state);
        if (_loader == null || !_byTail.TryGetValue(tail, out var item)) return false;
        var box = _loader.GetAlphaLocalBox(item, 1f);
        if (!box.ok) return false;
        size = box.size;
        offset = box.offset;
        if (box.canvas.y > 0.0001f) canvas = box.canvas;
        return true;
    }

    // ─────────────────── 動作「起播幀」：跳過起手，全血統通用、零設定 ───────────────────
    //
    // 【要解決的問題】攻擊動畫是「一發＝一次」的短表演，但每個血統的素材起手長度都不一樣：
    // 實測 Nightborn / 旱魃 / Base 的第 1 幀就已經是伸手施法的姿勢，Cain 與 Crimson Count
    // 卻要到第 6~7 幀手才伸出去——前面五、六幀跟站著沒兩樣。以前攻擊姿勢只維持 0.12 秒
    // （＝12fps 下的 1.4 幀），於是那兩個血統的玩家「永遠只看得到起手」，看起來像攻擊動畫沒播。
    //
    // 【規則】起播幀 = 第一個「與站姿的輪廓差異達到該動作**自己峰值** ActionStartPeakRatio」的幀。
    // 從它開始播，前面的起手直接跳過。
    //
    // 【為什麼一定要用「相對自己的峰值」而不是絕對門檻】每個血統的動作幅度差很多——
    // 實測峰值 Cain 只有 25%、Nightborn 有 86%。任何絕對門檻都會對其中一邊失效：
    // 門檻設高，Cain 整段都達不到（起播幀退回 0，等於沒修）；門檻設低，Nightborn 的第 1 幀就過關
    // （沒差，它本來就該從 1 開始）但殭屍那種前段有小動作的會被誤判成「已經開始」。
    // 相對峰值讓每個血統只跟自己比，所以同一條規則對所有角色都成立。
    //
    // 【為什麼不做成 CSV 欄位】那等於每加一個血統就要有人開一次遊戲、逐幀看、填一個數字，
    // 而這個數字完全可以從圖本身算出來。這裡的作法與地上物碰撞遮罩（PROBLEMS B9）同一個思路：
    // 「只跟那張圖有關」的資訊就從圖算，不要變成人工維護的資料。
    //
    // 【成本】只在 Setup（進場／換血統）第一次查詢時掃一次，之後永久快取。
    // 掃描降到 PoseGrid×PoseGrid 的佔用格，50 張 256×256 約數毫秒；換血統時遊戲正暫停在煙霧裡，
    // 進場則有載入頁，兩處都看不到。⚠ 這裡**只有一條計算路徑**（runtime 當場掃），
    // 沒有 B9 那種「烘焙版 vs 退路版算出不同結果」的風險；日後若真要烘進 catalog，
    // 記得兩條路要走同一支函式。

    /// <summary>
    /// 「動作真正開始」的門檻：與站姿的輪廓差異達到該動作自己峰值的這個比例，就算動作已經開始。
    /// 實測 0.6 是甜蜜點——0.75 會把殭屍砍到只剩 13 幀、毛殭剩 10 幀（那兩組本來就沒問題，等於砍過頭）；
    /// 0.5 以下則開始把起手的小幅度晃動誤判成動作。**這是全遊戲唯一的一個數字，不是每個血統一個。**
    /// </summary>
    public const float ActionStartPeakRatio = 0.6f;

    /// <summary>
    /// 「動作最大幀」的門檻：與站姿的輪廓差異**第一次**達到峰值的這個比例，就當作動作已經到底（出手到底、法杖舉到最高）。
    /// 攻擊動畫只播到這一幀（＋<see cref="ActionEndTailFrames"/>），後面 AutoSprite 常常多出來的第二拳／手放到別處一律不播。
    /// 不取嚴格最大值而取「第一次到 90%」：兩拳的圖若第二拳伸得更開，嚴格最大會抓到第二拳。
    /// 全程式判斷、沒有手填的覆寫（作者拍板：抓歪了重做圖比找幀號快）。
    /// </summary>
    public const float ActionEndPeakRatio = 0.9f;

    /// <summary>最大幀之後再多播幾幀當收勢，免得從伸到底直接跳站姿太硬。1＝多一格；0＝到最大幀就停。</summary>
    public const int ActionEndTailFrames = 1;

    const int PoseGrid = 64;              // 輪廓比對的取樣格數（整張畫布 → PoseGrid×PoseGrid 佔用格）
    const byte PoseAlphaThreshold = 10;   // 與 MapSpriteLoader.AlphaThreshold 同值（去背邊當透明）

    readonly Dictionary<string, int> _startFrame = new Dictionary<string, int>();   // "<血統>/<state>" → 起播幀索引
    readonly Dictionary<string, int> _endFrame = new Dictionary<string, int>();     // "<血統>/<state>" → 結束幀索引（最大幀＋尾巴；-1＝算不出來，播到最後一幀）

    /// <summary>
    /// 這個血統這個動作該從第幾幀開始播（0 起算）。算不出來一律回 0＝從頭播，行為與改動前相同。
    /// 結果快取，同一個血統只會算一次。
    /// </summary>
    public int GetActionStartFrame(string bloodline, string state)
    {
        string key = Key(bloodline, state);
        if (_startFrame.TryGetValue(key, out int cached)) return cached;
        ComputeActionRange(bloodline, state, out int start, out int end);
        _startFrame[key] = start; _endFrame[key] = end;
        return start;
    }

    /// <summary>
    /// 這個血統這個動作該播到第幾幀為止（0 起算、含）：輪廓差異第一次達到峰值 <see cref="ActionEndPeakRatio"/> 的那幀＋<see cref="ActionEndTailFrames"/>。
    /// 算不出來回 -1＝播到最後一幀（行為與改動前相同）。與起播幀同一次掃描、同一份快取。
    /// </summary>
    public int GetActionEndFrame(string bloodline, string state)
    {
        string key = Key(bloodline, state);
        if (_endFrame.TryGetValue(key, out int cached)) return cached;
        ComputeActionRange(bloodline, state, out int start, out int end);
        _startFrame[key] = start; _endFrame[key] = end;
        return end;
    }

    /// <summary>該動作的總幀數（給報表用）；沒圖或單張回 0。</summary>
    public int GetActionFrameCount(string bloodline, string state)
    {
        if (!_byTail.TryGetValue(Key(bloodline, state), out var act) || act == null || !act.IsAnimated) return 0;
        return act.frames != null ? act.frames.Count : 0;
    }

    /// <summary>所有有 attack 圖的血統名（小寫），給編輯器報表用。</summary>
    public IEnumerable<string> BloodlinesWith(string state)
    {
        string suffix = "/" + (state ?? "").Trim().ToLowerInvariant();
        foreach (var k in _byTail.Keys) if (k.EndsWith(suffix)) yield return k.Substring(0, k.Length - suffix.Length);
    }

    void ComputeActionRange(string bloodline, string state, out int start, out int end)
    {
        start = 0; end = -1;
        if (_loader == null) return;
        if (!_byTail.TryGetValue(Key(bloodline, state), out var act) || act == null || !act.IsAnimated) return;
        if (!_byTail.TryGetValue(Key(bloodline, "idle"), out var idle) || idle == null) return;

        var stance = BuildStanceMask(idle);
        if (stance == null) return;
        int stanceCells = 0;
        for (int c = 0; c < stance.Length; c++) if (stance[c]) stanceCells++;
        if (stanceCells <= 0) return;

        var diffs = new float[act.frames.Count];
        float peak = 0f;
        for (int i = 0; i < act.frames.Count; i++)
        {
            var g = BuildPoseMask(_loader.GetFrameTexture(act.frames[i]));
            if (g == null) continue;
            int d = 0;
            for (int c = 0; c < g.Length; c++) if (g[c] != stance[c]) d++;
            diffs[i] = (float)d / stanceCells;
            if (diffs[i] > peak) peak = diffs[i];
        }
        if (peak <= 0.0001f) return;   // 整段都跟站姿一樣（或全部讀不到）→ 別自作聰明，從頭播到尾

        float gate = peak * ActionStartPeakRatio;
        for (int i = 0; i < diffs.Length; i++)
            if (diffs[i] >= gate) { start = i; break; }

        // 結束幀：第一次到峰值 90% 的那格（＝出手到底）＋尾巴；一定 ≥ 起播幀
        float endGate = peak * ActionEndPeakRatio;
        for (int i = start; i < diffs.Length; i++)
            if (diffs[i] >= endGate) { end = Mathf.Min(diffs.Length - 1, i + ActionEndTailFrames); break; }
        if (end >= 0 && end < start) end = start;
    }

    /// <summary>
    /// 「站姿」遮罩：idle 各幀的佔用格取多數決（超過一半的幀都畫了東西的格子才算站姿）。
    /// 用多數決而不是單取第 1 幀，是因為 idle 本身也在呼吸擺動，單幀會把晃動當成輪廓。
    /// </summary>
    bool[] BuildStanceMask(CatalogItem idle)
    {
        var votes = new int[PoseGrid * PoseGrid];
        int used = 0;
        int n = idle.IsAnimated ? idle.frames.Count : 1;
        for (int i = 0; i < n; i++)
        {
            var g = idle.IsAnimated ? BuildPoseMask(_loader.GetFrameTexture(idle.frames[i]))
                                    : BuildPoseMask(_loader.GetTexture(idle));
            if (g == null) continue;
            used++;
            for (int c = 0; c < g.Length; c++) if (g[c]) votes[c]++;
        }
        if (used == 0) return null;
        var mask = new bool[PoseGrid * PoseGrid];
        for (int c = 0; c < mask.Length; c++) mask[c] = votes[c] * 2 > used;
        return mask;
    }

    /// <summary>
    /// 把一張圖降成 PoseGrid×PoseGrid 的佔用格：一格內**只要有一個不透明像素**就算佔用（OR 降取樣）。
    /// 用 OR 而不是覆蓋率，是因為這裡要偵測的是「手有沒有伸出去」這種細長特徵，
    /// 算覆蓋率會被細手臂稀釋掉（同 ObjectFootprint.Downsample 的理由）。
    /// 座標系是畫布相對的，所以 idle 與 attack 就算畫布尺寸不同也對得起來。
    /// </summary>
    static bool[] BuildPoseMask(Texture2D tex)
    {
        if (tex == null || tex.width <= 0 || tex.height <= 0) return null;
        var px = tex.GetPixels32();
        int w = tex.width, h = tex.height;
        var mask = new bool[PoseGrid * PoseGrid];
        for (int y = 0; y < h; y++)
        {
            int rowBase = y * w;
            int gRow = (y * PoseGrid / h) * PoseGrid;
            for (int x = 0; x < w; x++)
                if (px[rowBase + x].a > PoseAlphaThreshold) mask[gRow + x * PoseGrid / w] = true;
        }
        return mask;
    }
}
