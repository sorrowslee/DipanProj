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

    /// <summary>取某血統某動作的幀（依序）。單張資料夾 → 長度 1（靜態姿勢）；找不到回 null。結果快取。</summary>
    public Sprite[] GetFrames(string bloodline, string state)
    {
        string k = Key(bloodline, state);
        if (_frameCache.TryGetValue(k, out var cached)) return cached;

        Sprite[] frames = null;
        if (_byTail.TryGetValue(k, out var item) && _loader != null)
        {
            if (item.IsAnimated)
            {
                frames = _loader.GetAnimationFrames(item, 1f);
            }
            else
            {
                var sp = _loader.GetWholeSprite(item, 1f);
                if (sp != null) frames = new[] { sp };
            }
        }
        _frameCache[k] = frames;
        return frames;
    }
}
