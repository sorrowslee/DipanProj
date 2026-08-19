using UnityEngine;

/// <summary>
/// 俯視角深度排序（Y-sort）的單一來源。**地上物、玩家、怪物共用同一套**：
/// 「畫面上越下面（世界 Y 越小）＝越靠近鏡頭＝畫在越前面」。這樣角色走到地上物前面就蓋住它、
/// 走到後面就被它蓋住，而不是像以前角色固定 sortingOrder 永遠被地上物蓋住。
///
/// ══════════════════════════════════════════════════════════════════════════
/// 全遊戲的排序層配置（改任何一個數字之前先看這張表）
///
///   -1000            地圖背景
///       5            可走地上物、地面掉落物
///       8            地面特效（tile 火/毒、靜態圓暈）
///      20            互動星星
///   ────────────────  ↓ 世界 Y 排序帶（本檔負責）↓
///    1000 ~  6999    zOrder = -1（整層往後：壓在別的地上物下面的東西）
///    7000 ~ 12999    zOrder =  0（絕大多數地上物 ＋ 玩家 ＋ 怪物）
///   13000 ~ 18999    zOrder = +1（整層往前：桌上的花瓶、供桌上的香爐燭台…）
///   ────────────────  ↑ 世界帶到此為止，上面全是「表演層」↑
///   20000            地上掉落物的名稱標籤
///   22000            VfxManager 全域預設、拼接雷柱、像素雷射
///   22050 / 22100    血統變身的環繞電弧 / 煙塵
///   24000            SceneFx 傳送門、頭上傷害數字
///   25000            SceneFx 煙火冰毒、過關離場卍字
///   30000            場景火雨
///   32767            16-bit 上限（絕對不能超過，見 readme/PROBLEMS.md E4）
/// ══════════════════════════════════════════════════════════════════════════
///
/// ⚠️ **2026-08-18 重寫，拿掉了 base 1,000,000 的「繞回」寫法。** 舊版是
/// `1000000 + zOrder*10000 + round(-Y*100)`，靠 16-bit 溢位繞回落到 +16960 那一帶。
/// 那個寫法只有 `zOrder = 0` 時安全：**`zOrder = 1` 會把整帶平移到 21960~31960，
/// 正好壓在所有表演層之上**——實測紅嫁衣的燭台/香爐/花瓶（zOrder=1）sortingOrder 到 27409，
/// 於是傷害數字、離場卍字、傳送門、雷柱、煙塵全部被一個燭台蓋住。
/// 見 readme/PROBLEMS.md **E15**。
///
/// 現在的寫法**完全不繞回**：低基底 ＋ 把 Y 的貢獻夾在一個 band 內，所以
/// 「zOrder 大的一定在前面」是硬保證，而且整個世界帶不會侵入表演層。
/// 386 個既有地上物驗算過，兩兩相對順序與舊版**完全一致**（世界內的遮蔽關係零改變）。
/// </summary>
public static class MapDepthSort
{
    /// <summary>zOrder = 0 那一層的起點。要在「互動星星(20)」之上、且留出 zOrder=-1 的空間。</summary>
    public const int SortBase = 7000;

    /// <summary>
    /// 每一階 zOrder 佔的寬度。同時也是「Y 的貢獻」能用的上限——
    /// 6000 / SortScale = **地圖高度最多 60 世界單位**，超過的部分會被夾住（同一排序值）。
    /// 目前最高的地圖是邪佛廣場 36.7 單位，還有很多餘裕。
    /// ⚠ 要放更高的地圖就得加大這個值，但世界帶頂端不能超過 20000（掉落物名稱標籤）。
    /// </summary>
    public const int BandStep = 6000;

    public const float SortScale = 100f;   // 每世界單位 Y 的排序差

    /// <summary>zOrder 的合法範圍。超出會被夾住並印一次警告——放任它往上跑就會撞進表演層。</summary>
    public const int MinZOrder = -1;
    public const int MaxZOrder = 1;

    static bool _warnedZ;

    /// <summary>進 Play 時歸零（Domain Reload 已關）。由 PlayModeStaticReset 呼叫。</summary>
    public static void ResetForPlayMode() => _warnedZ = false;

    /// <summary>依世界 Y（越小越前）＋ zOrder 算 sortingOrder。地上物用 sortKey(=放置Y)，角色用腳底 Y。</summary>
    public static int Order(float worldY, int zOrder = 0)
    {
        if (zOrder < MinZOrder || zOrder > MaxZOrder)
        {
            if (!_warnedZ)
            {
                _warnedZ = true;
                Debug.LogWarning($"[MapDepthSort] zOrder={zOrder} 超出合法範圍 " +
                                 $"[{MinZOrder}, {MaxZOrder}]，已夾住。" +
                                 "再往上會讓地上物的排序值撞進『表演層』（傷害數字、特效、卍字…）而蓋住它們，" +
                                 "見 readme/PROBLEMS.md E15。要更多層數請改 MapDepthSort 的配置表。");
            }
            zOrder = Mathf.Clamp(zOrder, MinZOrder, MaxZOrder);
        }

        // Y 的貢獻夾在 [0, BandStep-1]：保證「zOrder 大的永遠在前面」，也保證世界帶不會外溢。
        // （地圖是左上角錨定、往下 Y 為負，所以 -worldY 一定 ≥ 0。）
        int y = Mathf.Clamp(Mathf.RoundToInt(-worldY * SortScale), 0, BandStep - 1);
        return SortBase + zOrder * BandStep + y;
    }
}
