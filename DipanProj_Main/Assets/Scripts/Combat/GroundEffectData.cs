using UnityEngine;

public class GroundEffectData
{
    public int ID;
    public string Name;
    public float Radius;
    /// <summary>存活秒數；&lt; 0（例如 -1）表示永久存在直到外部銷毀。</summary>
    public float Duration;
    /// <summary>傷害節拍：0 = 生成瞬間單次爆裂；&gt; 0 = 每 N 秒週期 DOT。</summary>
    public float DamageInterval;
    public float Damage;

    public string AniPath;
    public int AniNumber;
    public float AnimFPS;

    /// <summary>單個 tile 的世界尺寸（同時也是格子間距）；&le; 0 視為 1。</summary>
    public float TileSize = 1f;

    /// <summary>
    /// 渲染模式：false（預設）= 用 tile 把圓鋪滿（火堆／毒霧那種）；
    /// true = 只放「一張」sprite，整張縮放到直徑 = 2*Radius（佛光那種柔和發光圓暈）。
    /// 單圖模式不受 TileSize 影響，傷害判定仍走 OverlapCircleAll、嚴格按 Radius。
    /// </summary>
    public bool SingleSprite = false;

    public Sprite[] AnimationSprites;
}
