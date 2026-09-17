using System.Collections.Generic;
using UnityEngine;
using Dipan.MapRuntime;   // 怪物圖走「地圖素材管線」(catalog + StreamingAssets)，與劇情大圖/頭像同套

/// <summary>
/// 怪物外觀素材庫（路線 B：程式逐格動畫，零 prefab、零 Unity Animator）。
///
/// 慣例：每隻怪一個資料夾、每個動作一個子資料夾、單張 PNG 一幀：
///   GameAssets/Modules/&lt;關卡&gt;/Monsters/SequenceImage/&lt;怪名&gt;/idle/idle_01.png ...
///                                                          /walk/walk_01.png ...
///                                                          /attack/attack_01.png ...（可選）
/// 同步工具（Sync Map Assets）把每個「動作葉資料夾」收成一筆 catalog item
/// （id = 資料夾相對路徑、≥2 幀帶 frameCount/frames），本庫再依「&lt;怪名&gt;/&lt;state&gt;」索引取用。
///
/// 懶漢單例：第一次存取 <see cref="Instance"/> 自動載入 catalog 一次（之後共用快取）。
/// 載入方式與 <see cref="Dipan.Drama.DramaTalkDatabase"/> 的頭像一致（CatalogLoader + MapSpriteLoader）。
/// </summary>
public class MonsterSpriteLibrary
{
    // catalog id 內標記怪物素材的固定中綴（跨 module，靠它把 id 切出「<怪名>/<state>」尾段）
    public const string Marker = "Monsters/SequenceImage/";

    static MonsterSpriteLibrary _instance;
    public static MonsterSpriteLibrary Instance
    {
        get
        {
            if (_instance == null) { _instance = new MonsterSpriteLibrary(); _instance.Load(); }
            return _instance;
        }
    }

    /// <summary>進入 Play 模式時丟掉單例（已關 Domain Reload；否則 static 快取會回傳上一輪被銷毀的 sprite → 怪物只剩影子/不見）。由 PlayModeStaticReset 呼叫。</summary>
    public static void ResetForPlayMode() => _instance = null;

    // 「<怪名>/<state>」(小寫) → catalog item
    readonly Dictionary<string, CatalogItem> _byTail = new Dictionary<string, CatalogItem>();
    // 「<怪名>/<state>」(小寫) → 已載好的幀（快取，避免重覆建 sprite）
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
            string tail = item.id.Substring(idx + Marker.Length).ToLowerInvariant(); // "<怪名>/<state>"
            _byTail[tail] = item;
            n++;
        }
        Debug.Log($"[MonsterSpriteLibrary] 索引 {n} 筆怪物動作素材。");
    }

    static string Key(string monsterName, string state)
        => $"{(monsterName ?? "").Trim().ToLowerInvariant()}/{(state ?? "").Trim().ToLowerInvariant()}";

    /// <summary>這隻怪有沒有這個動作的圖（防呆判斷用）。</summary>
    public bool Has(string monsterName, string state)
    {
        string k = Key(monsterName, state);
        if (_frameCache.ContainsKey(k)) return _frameCache[k] != null;
        return _byTail.ContainsKey(k);
    }

    /// <summary>
    /// 取某怪某動作的幀（依序）。單張資料夾 → 長度 1 的陣列（靜態姿勢）；找不到回 null。
    /// 結果快取，重覆呼叫同一隻同一動作不會重建。
    /// </summary>
    /// <param name="baseTileSize">**基準動作（idle）的 tileSize**，給腳底對齊用；&lt;=0 或等於 tileSize 時等同不縮放。
    /// 各動作的 tileSize 不一樣時（逐動作縮放），只比像素會算錯，見下方註解。</param>
    public Sprite[] GetFrames(string monsterName, string state, float tileSize = 1f, float baseTileSize = 0f)
    {
        string tail = Key(monsterName, state);
        if (baseTileSize <= 0.0001f) baseTileSize = tileSize;
        string cacheKey = $"{tail}|{tileSize}|b{baseTileSize}";
        if (_frameCache.TryGetValue(cacheKey, out var cached)) return cached;

        Sprite[] frames = null;
        if (_byTail.TryGetValue(tail, out var item) && _loader != null)
        {
            if (item.IsAnimated)
            {
                // ⭐ 腳底對齊（2026-09-17）：AI 生成的序列圖常常把角色畫在畫布的不同高度——
                //    戰狼實測 idle 的腳底在畫布底往上 56px、walk 卻是 69px，**差 13px**；
                //    而角色會被 `CharacterWorldHeight / idle可見高` 放大（戰狼是 3.49 倍），
                //    那 13px 在遊戲裡就是 **0.18 世界單位**（約玩家身高的 9%）⇒ 切 idle↔walk 時整隻狼上下跳，
                //    腳下的影子當然也跟著跳。逐幀把 pivot.y 補償到同一條腳底線就解掉了。
                //    基準取 **idle**：碰撞框與顯示大小也都是用 idle 算的（見 MonsterController.Setup），
                //    所以基準幀的 pivot 維持 0.5、**角色的絕對位置與碰撞框完全不動**，只有其他動作被拉齊。
                frames = _loader.GetAnimationFrames(item, tileSize, BaselineBottomPx(monsterName), baseTileSize / tileSize);
            }
            else
            {
                var sp = _loader.GetWholeSprite(item, tileSize);
                if (sp != null) frames = new[] { sp };
            }
        }
        _frameCache[cacheKey] = frames;   // 連 null 也快取，避免每幀重查
        return frames;
    }

    readonly Dictionary<string, int> _baselineBottom = new Dictionary<string, int>();

    /// <summary>
    /// 這隻怪的「腳底基準」＝ **idle 第一幀**的不透明內容距畫布底的像素（取不到 idle 就用 walk，再取不到回 -1＝不對齊）。
    /// 所有動作的幀都會被補償到這條線上，見 <see cref="GetFrames"/>。結果快取（每隻怪只量一次）。
    /// ⚠ 基準**必須**跟「建碰撞框／算顯示大小」用的那一個動作一致（目前是 idle，見 MonsterController.Setup），
    ///   否則基準幀的 pivot 不是 0.5，角色的絕對位置與碰撞框就會整個偏掉。
    /// </summary>
    int BaselineBottomPx(string monsterName)
    {
        string k = (monsterName ?? "").Trim().ToLowerInvariant();
        if (_baselineBottom.TryGetValue(k, out int cached)) return cached;

        int result = -1;
        foreach (string state in BaselineStates)
        {
            if (!_byTail.TryGetValue(Key(monsterName, state), out var item) || item == null) continue;
            string fp = item.IsAnimated ? (item.frames != null && item.frames.Count > 0 ? item.frames[0] : null) : null;
            if (string.IsNullOrEmpty(fp)) continue;
            var bp = _loader.GetFrameBottomPx(fp);
            if (bp.x >= 0) { result = bp.x; break; }
        }
        _baselineBottom[k] = result;
        return result;
    }

    // 找腳底基準的優先順序（與 MonsterController.Setup 算碰撞框/顯示大小的順序一致）。
    static readonly string[] BaselineStates = { "idle", "walk" };

    readonly Dictionary<string, ShadowAnchorPx> _shadowAnchor = new Dictionary<string, ShadowAnchorPx>();

    /// <summary>
    /// 某怪某動作的影子錨點（像素、畫布座標）。先查 ShadowAnchorTable.csv（Key = Monsters/&lt;怪名&gt;/&lt;動作&gt;），
    /// 沒有就用同一條演算法掃全幀當場算（見 <see cref="ShadowAnchorMath"/>）。結果快取；沒圖回 ok=false。
    /// </summary>
    public ShadowAnchorPx GetShadowAnchor(string monsterName, string state)
    {
        string key = Key(monsterName, state);
        if (_shadowAnchor.TryGetValue(key, out var cached)) return cached;
        var a = PlayerSpriteLibrary.ComputeShadowAnchor(ShadowAnchorTable.KindMonsters, monsterName, state, _loader, _byTail.TryGetValue(key, out var item) ? item : null);
        _shadowAnchor[key] = a;
        return a;
    }

    /// <summary>
    /// 取某怪某動作（取代表幀＝該動作第一幀）的「不透明像素貼合框」，給碰撞框用：
    /// size / offset 為世界單位 @ scale 1（PPU 256），offset 相對 sprite 中心。
    /// 透明邊不算進去，所以瘦長的鬼魂不會被空白邊撐大碰撞範圍。沿用家具用的 MapSpriteLoader.GetAlphaLocalBox。
    /// </summary>
    public bool TryGetVisibleBox(string monsterName, string state, float tileSize, out Vector2 size, out Vector2 offset)
    {
        size = default; offset = default;
        string k = Key(monsterName, state);
        if (_loader == null || !_byTail.TryGetValue(k, out var item)) return false;
        var box = _loader.GetAlphaLocalBox(item, tileSize);
        if (!box.ok) return false;
        size = box.size;
        offset = box.offset;
        return true;
    }
}
