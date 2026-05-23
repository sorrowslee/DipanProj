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

    public Sprite[] AnimationSprites;
}
