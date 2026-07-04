using UnityEngine;

/// <summary>
/// 俯視角深度排序（Y-sort）的單一來源。**地上物、玩家、怪物共用同一套**：
/// 「畫面上越下面（世界 Y 越小）＝越靠近鏡頭＝畫在越前面」。這樣角色走到地上物前面就蓋住它、
/// 走到後面就被它蓋住，而不是像以前角色固定 sortingOrder 永遠被地上物蓋住。
///
/// ⚠️ Unity 的 sortingOrder 實為 **16-bit（-32768~32767）**，超出會溢位繞回（見 readme/PROBLEMS.md E4）。
/// 本專案地上物沿用 base 1,000,000 的公式，靠 16-bit 繞回落在某一帶；讓角色**用同一條公式**就會繞回同一帶、
/// 依 Y 正確交錯。需要畫在「角色/地上物之上」的表演層（傷害數字、擊中特效）另用高於此帶的固定值
/// （見 DamageNumberManager.SortingOrder / VfxManager.SortingOrder）。
/// </summary>
public static class MapDepthSort
{
    public const int SortBase = 1000000;
    public const int BandStep = 10000;    // zOrder 每階（>0 整層往前、<0 往後）
    public const float SortScale = 100f;  // 每世界單位 Y 的排序差

    /// <summary>依世界 Y（越小越前）＋ zOrder 算 sortingOrder。地上物用 sortKey(=放置Y)，角色用腳底 Y。</summary>
    public static int Order(float worldY, int zOrder = 0)
        => SortBase + zOrder * BandStep + Mathf.RoundToInt(-worldY * SortScale);
}
