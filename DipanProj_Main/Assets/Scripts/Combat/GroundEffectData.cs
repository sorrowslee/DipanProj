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

    /// <summary>
    /// 燈火明滅：true 時（RenderMode=Glow）單圖護罩改用 Custom/AuraGlow 加色發光材質，
    /// 並每幀以不規則明滅調整亮度 ＋ 微幅呼吸縮放（佛光那種忽強忽弱的燈光感）。
    /// 只影響視覺，傷害判定仍嚴格按 Radius 走 OverlapCircle，不受縮放影響。
    /// 蘊含 SingleSprite = true。
    /// </summary>
    public bool GlowFlicker = false;

    /// <summary>
    /// 背景旋轉符號圖路徑（相對 Resources、不含副檔名，例：InitialStory/Manji）；留空 = 沒有這一層。
    /// 在圓的「後面」加一張緩緩自轉的半透明符號（墮落佛光的卍字就是這樣做的）。
    /// 與 RenderMode 無關——Tile / Single / Glow 都能掛，想要明滅發光卻不想要符號就把這欄留空。
    /// 純視覺：符號通常畫得比 Radius 大，傷害判定仍嚴格按 Radius 走 OverlapCircle。
    /// 圖需匯入為 Sprite 類型（同 AniPath 的規則）。
    /// </summary>
    public string SigilPath;

    /// <summary>由 SigilPath 載好的符號 sprite；載不到 = null（該層直接略過，不影響特效本體）。</summary>
    public Sprite SigilSprite;

    /// <summary>
    /// 發光半徑（世界單位）；&le; 0 或留空 = 不發光。
    /// &gt; 0 時特效生成的當下會掛一個 <see cref="LightSource"/>，被 AtmosphereController 當成場上的一盞燈收走，
    /// **真的照亮暗場景的地圖**（不只是畫一張發光的圖）。特效銷毀時 LightSource 隨之 OnDisable 退出登記表。
    /// 與 ItemTable 的 LightRadius（裝備著就恆亮的提燈）是兩回事：這個只在特效存在期間亮。
    /// 光色／亮度／搖晃等其餘參數用 LightSource 的預設暖橘，要調改 GroundEffectInstance 上方常數。
    /// </summary>
    public float LightRadius;

    public Sprite[] AnimationSprites;
}
