using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 狀態效果圖示（2026-11-18，見 readme/STATUS_EFFECT.md §5）。
///
/// <para>所有圖示合在<b>同一張貼圖</b> <c>Resources/UI/Icons/Status/status_atlas.png</c>，
/// 同資料夾的 <c>status_atlas_index.txt</c> 每行「名稱,x,y,w,h」（像素、左下原點）；這裡切出 Sprite 並快取。
/// 為什麼不一張圖一個檔：怪物頭上的圖示是世界空間 SpriteRenderer，<b>同材質＋同貼圖 Unity 才會自動合批</b>，
/// 100 隻怪各掛 2～3 個圖示也只要幾個 draw call（作者：之後會有大批怪物，省效能最重要）。專案沒有開 Sprite Atlas 資產，所以自己合。</para>
///
/// <para>名稱＝StatusEffectTable 的 <c>Icon</c> 欄。重新產圖後用 <c>TempImage/status_icons/tools/build_atlas.py</c> 重合一次即可。</para>
/// </summary>
public static class StatusIcons
{
    const string AtlasPath = "UI/Icons/Status/status_atlas";
    const string IndexPath = "UI/Icons/Status/status_atlas_index";
    const float PixelsPerUnit = 128f;   // 一格 128px ＝ 1 世界單位（頭上圖示再自己縮）

    static Texture2D _tex;
    static Dictionary<string, Rect> _rects;
    static float _scale = 1f;   // 實際匯入尺寸 ÷ 合成時尺寸（.meta 的 maxTextureSize 會把貼圖縮小）
    static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
    static bool _loaded;
    static Sprite _white;

    public static void ResetForPlayMode() { _tex = null; _rects = null; _cache.Clear(); _loaded = false; _white = null; _scale = 1f; }

    public static Sprite Get(StatusEffectDef def) => def == null ? null : Get(def.Icon);

    public static Sprite Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (_cache.TryGetValue(name, out var s) && s != null) return s;
        Load();
        if (_tex == null || _rects == null || !_rects.TryGetValue(name, out var r)) return null;
        // 索引是合成時（512）的像素；貼圖被匯入縮小時照比例換算，PPU 一起縮 ⇒ 世界大小不變
        //（2026-11-18 作者實測：範本 .meta 的預設平台上限是 256，512 的貼圖被縮成 256 ⇒ Sprite.Create 丟例外、狀態列整個建不出來）
        var rr = new Rect(r.x * _scale, r.y * _scale, r.width * _scale, r.height * _scale);
        if (rr.xMax > _tex.width + 0.5f || rr.yMax > _tex.height + 0.5f)
        {
            Debug.LogWarning($"[StatusIcons] 圖示 {name} 的範圍 {rr} 超出貼圖 {_tex.width}x{_tex.height}，略過。");
            return null;
        }
        s = Sprite.Create(_tex, rr, new Vector2(0.5f, 0.5f), PixelsPerUnit * _scale, 0, SpriteMeshType.FullRect);
        s.name = name;
        _cache[name] = s;
        return s;
    }

    /// <summary>白色小 sprite：UI 的 Filled（剩餘時間轉圈）要有 sprite 才會動。</summary>
    public static Sprite White
    {
        get
        {
            if (_white == null)
            {
                var t = Texture2D.whiteTexture;
                _white = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
                _white.name = "StatusWhite";
            }
            return _white;
        }
    }

    static void Load()
    {
        if (_loaded && _tex != null) return;
        _loaded = true;
        _tex = Resources.Load<Texture2D>(AtlasPath);
        var idx = Resources.Load<TextAsset>(IndexPath);
        _rects = new Dictionary<string, Rect>();
        if (_tex == null || idx == null)
        {
            Debug.LogWarning($"[StatusIcons] 找不到狀態圖示：Resources/{AtlasPath}.png 或 {IndexPath}.txt（見 readme/STATUS_EFFECT.md §5）。狀態列與頭上圖示會是空的。");
            return;
        }
        foreach (var raw in idx.text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            var p = line.Split(',');
            if (p.Length >= 3 && p[0].Trim() == "size")
            {
                if (float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float sw) && sw > 0f)
                    _scale = _tex.width / sw;
                continue;
            }
            if (p.Length < 5) continue;
            if (float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float w) &&
                float.TryParse(p[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float h))
                _rects[p[0].Trim()] = new Rect(x, y, w, h);
        }
    }
}
