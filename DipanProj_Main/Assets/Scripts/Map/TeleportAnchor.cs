using UnityEngine;
using Dipan.MapRuntime;

/// <summary>
/// 傳送點的「錨點」——**外型位置、踩踏區、傳送落點三者的單一真相**。
///
/// <para><b>為什麼</b>：傳送點原本是 Trigger 層塗的整格，那些格子同時兼三個職務（踩踏偵測／落點／外型中心）。
/// 但門的美術畫在背景圖裡、位置任意，格子只有整格精度，三者永遠對不齊——
/// 而且**踩踏是拿 <c>transform.position</c>（在胸口高度，比腳底高約一格）去比對格子**，
/// 所以擺格子時還得無意識地往上補一格，「看到的門」和「實際會傳送的地方」自然分家。
/// 見 readme/MAP_SYSTEM.md、readme/MapEditor_DESIGN.md §4.5。</para>
///
/// <para><b>現在</b>：傳送點＝一個錨點 <c>markerX</c>/<c>markerY</c> ＋ 一個矩形 <c>markerW</c>/<c>markerH</c>。
/// 光盤畫在錨點、玩家**腳底**進到矩形就傳送、別張圖傳過來也落在錨點
/// （落點安全交給 <c>MapManager.FreeSpotNear</c>——門在牆上會自動推到門前地板）。
/// 傳送點光盤本來就是「畫在地上的光圈」，所以「腳踩上去」才是它該有的語意。</para>
///
/// <para><b>向下相容</b>：沒設錨點的舊傳送點自動退回「格子」模式（同以前，用 <c>transform.position</c> 比對格子），
/// 兩種可以在同一張圖並存。</para>
///
/// ⚠ 編輯器有一份鏡像 <c>Preview/TeleportMarkerPreview.cs</c>（錨點與矩形的讀取規則），改這裡要一起改。
/// </summary>
public static class TeleportAnchor
{
    public const string KeyX = "markerX";
    public const string KeyY = "markerY";
    public const string KeyW = "markerW";
    public const string KeyH = "markerH";

    /// <summary>沒填寬高時的預設踩踏矩形（世界單位）。門通常「寬而矮」，所以預設不是正方形。</summary>
    public const float DefaultW = 1.0f;
    public const float DefaultH = 0.6f;

    /// <summary>這個傳送點有沒有設錨點（有＝點模式、沒有＝舊的格子模式）。</summary>
    public static bool HasAnchor(TriggerRegion r)
        => r?.Params != null && r.Params.ContainsKey(KeyX) && r.Params.ContainsKey(KeyY);

    /// <summary>錨點世界座標。沒設回 false（呼叫端自行退回格子平均中心）。</summary>
    public static bool TryPoint(TriggerRegion r, out Vector2 p)
    {
        p = Vector2.zero;
        if (!HasAnchor(r)) return false;
        p = new Vector2(r.GetFloat(KeyX), r.GetFloat(KeyY));
        return true;
    }

    /// <summary>踩踏矩形的尺寸（沒填或填 0 用預設）。</summary>
    public static Vector2 TouchSize(TriggerRegion r)
    {
        float w = r != null ? r.GetFloat(KeyW, DefaultW) : DefaultW;
        float h = r != null ? r.GetFloat(KeyH, DefaultH) : DefaultH;
        if (w <= 0.001f) w = DefaultW;
        if (h <= 0.001f) h = DefaultH;
        return new Vector2(w, h);
    }

    /// <summary>踩踏矩形（以錨點為中心）。沒設錨點回 false。</summary>
    public static bool TryTouchRect(TriggerRegion r, out Rect rect)
    {
        rect = new Rect();
        if (!TryPoint(r, out Vector2 c)) return false;
        Vector2 s = TouchSize(r);
        rect = new Rect(c.x - s.x * 0.5f, c.y - s.y * 0.5f, s.x, s.y);
        return true;
    }

    /// <summary>
    /// 「這個點（玩家腳底）踩在這個傳送點上嗎」。沒設錨點的舊傳送點一律回 false，由呼叫端走格子路徑。
    /// </summary>
    public static bool Contains(TriggerRegion r, Vector2 worldPoint)
        => TryTouchRect(r, out Rect rect) && rect.Contains(worldPoint);

    /// <summary>
    /// 傳送點的中心：優先錨點，沒有才退回「格子平均中心」（＝舊行為）。
    /// 外型位置與傳送落點都用它。<paramref name="map"/> 為 null 時只看錨點。
    /// </summary>
    public static bool TryCenter(TriggerRegion r, MapData map, out Vector2 center)
    {
        if (TryPoint(r, out center)) return true;
        center = Vector2.zero;
        if (r?.cells == null || r.cells.Count == 0 || map == null) return false;
        Vector2 sum = Vector2.zero; int n = 0;
        foreach (var c in r.cells)
        {
            if (c == null || c.Length < 2) continue;
            sum += MapCoords.CellCenter(c[0], c[1], map); n++;
        }
        if (n == 0) return false;
        center = sum / n;
        return true;
    }
}
