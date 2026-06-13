using UnityEngine;

/// <summary>
/// 一次性視覺特效（VFX）的配方資料，由 VfxTable.csv 載入。
/// 與 GroundEffect 不同：VFX 不造成傷害、不鋪 tile，只是「在某座標播一輪序列圖後自毀」的純表演。
/// 發射特效（玩家身上）與擊中特效（命中點）共用這個原語。
/// </summary>
public class VfxData
{
    public int ID;
    public string Name;

    public string AniPath;
    public int AniNumber;
    public float AnimFPS;

    /// <summary>整體縮放倍率（世界尺寸），留空或 &le; 0 視為 1。</summary>
    public float Scale = 1f;

    /// <summary>是否循環播放；false = 播完一輪自毀（預設），true = 循環直到 Duration 秒。</summary>
    public bool Loop;

    /// <summary>僅 Loop = true 時有意義：循環存活秒數。Loop = false 時忽略（壽命 = 一輪動畫長度）。</summary>
    public float Duration;

    public Sprite[] AnimationSprites;
}
