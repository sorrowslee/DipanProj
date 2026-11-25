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

    /// <summary>
    /// 地上燃燒外觀（GroundFireTable ID；2026-11-18，DipanVfx，見 readme/DIPAN_VFX.md §4）。&gt; 0 時**不畫序列圖**，
    /// 改由 <see cref="Dipan.Vfx.GroundFireBurst"/> 畫焦土＋火舌＋火星；傷害照舊由這個 GroundEffect 結算。0／留空＝照舊畫序列圖。
    /// </summary>
    public int FireStyleID;

    /// <summary>每次傷害順便給的狀態（2026-11-18，CSV <c>StatusFx</c>）：狀態清單「名稱:等級;…」，DoT 一律固定值。留空＝無。見 readme/STATUS_EFFECT.md §4。</summary>
    public string StatusFx = "";

    // ── 黑洞類（2026-11-25，克蘇魯（古神系列第三階）的黑洞；見 readme/GROUND_EFFECT.md〈黑洞：出現定身、延時單擊、致死捲入〉）──

    /// <summary>
    /// 出現瞬間給範圍內單位的狀態（CSV <c>SpawnStatus</c>，狀態清單「名稱:等級;…」）：**持續時間＝這個地面特效的存在時間**（不用狀態表的 Duration）。
    /// 黑洞填 <c>Bind:1</c>（束縛：原地播走路、不能動不能打）。只看出現那一刻在圈內的；頭目照狀態規則免疫控制。留空＝無。
    /// </summary>
    public string SpawnStatus = "";

    /// <summary>
    /// 延時單擊（CSV <c>DamageDelay</c>）：&gt; 0＝生成後第 N 秒對圈內結算**一次**傷害，之後不再傷害（DamageInterval 不看）。
    /// 黑洞填 Duration 的一半（「動畫播到一半扣血」）。0／留空＝照舊（DamageInterval 決定瞬間單擊或週期 DOT）。
    /// </summary>
    public float DamageDelay;

    /// <summary>
    /// 致死捲入（CSV <c>SuckInKill</c>）：1＝被這個特效打死的怪不播一般死亡特效，改成旋轉縮小捲進特效中心（<see cref="SuckInDeathFx"/>），
    /// 掉寶也落在中心。頭目（Controllable=0）不適用、照正常死亡。0／留空＝一般死亡。
    /// </summary>
    public bool SuckInKill;

    /// <summary>
    /// 拖向中心（CSV <c>PullSpeed</c>，世界單位／秒）：&gt; 0＝出現時被 <see cref="SpawnStatus"/> 束縛到的怪，特效存在期間**腳底被拖向中心**
    /// （離中心越近越快，到了就停；面向不變、照樣原地播走路）。只拖出現那一刻上了束縛的；頭目免疫束縛 ⇒ 不會被拖。0／留空＝不拖。
    /// </summary>
    public float PullSpeed;

    public Sprite[] AnimationSprites;
}
