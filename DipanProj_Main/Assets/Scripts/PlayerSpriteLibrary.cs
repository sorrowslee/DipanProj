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
}
