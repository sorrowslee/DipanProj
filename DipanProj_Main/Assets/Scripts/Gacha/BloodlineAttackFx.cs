using UnityEngine;

/// <summary>
/// 血統特效第五層：**攻擊時才出現的一次性罩身特效**（尼德霍格的熔岩爆）。
///
/// <para>與另外四層的差別是**時機**，不是輪廓——前四層（<c>BloodlineAura</c> 環繞、
/// <c>BloodlineHalo</c> 背後圓盤、<c>BloodlineTrail</c> 移動足跡、<c>BloodlineOrbit</c> 繞行物件）
/// 都是「站著就一直在」的常駐視覺；這一層平常完全看不到，**只在出手那一瞬間罩上來一次**。
/// 所以它不佔用「輪廓與位置」的預算——同一個血統就算已經有常駐層，也還能再掛這一層。</para>
///
/// <para><b>節奏</b>：單擊 = 播一次；<b>壓住連射時不會每發都播</b>，而是播一次之後至少隔
/// <see cref="Gap"/> 秒才會再播（由表B 的 <c>AttackFxGap</c> 指定）。實作就是一個冷卻時間戳——
/// 單擊之所以「每次都播得到」，是因為兩次點擊之間本來就超過冷卻。</para>
///
/// <para><b>怎麼知道玩家攻擊了</b>：<c>PlayerController.TrySpawnFireEffect</c> 是四條發射路徑
/// （離散武器／雷射／召喚／佛光）**唯一的共同節點**，所以通知掛在那裡的最前面
/// （刻意在「這把武器有沒有 FireEffectID」的 early-return 之前，否則沒填發射特效的武器就不會通知）。</para>
///
/// <para><b>⚠ 兩種武器要分開接</b>：<c>TrySpawnFireEffect</c> 對**離散武器**是每發都會經過，
/// 但對**持續型武器（雷射／佛光）只有按下的那一幀**會經過——按著不放的期間一次都不再呼叫。
/// 所以持續型另外靠 <see cref="Update"/> 每幀問 <c>PlayerController.IsContinuousFireActive</c>；
/// 兩條路都走同一個 <see cref="NotifyAttack"/>，冷卻自然把多餘的擋掉。
/// （第一版漏了這一半，症狀是「雷射按著只播第一次」。）</para>
///
/// <para>⚠ 位置與大小一律走那三個身體幾何屬性（見 readme/PROBLEMS.md <b>E14</b>）：
/// 大小用 <c>ScaledCharacterHeight</c>；錨點看素材構圖——<see cref="AnchorAtFeet"/> 打勾時對 <c>FeetWorldPos</c>
/// （「從地面往上竄」的圖，它的底邊就是地面），取消時對 <c>BodyCenterWorldPos</c>（球狀爆炸那種）。
/// 特效 <b>SetParent 到玩家底下</b>，所以播放期間角色移動它會跟著走。</para>
///
/// <para>⚠ 目前只有一個血統用這一層，所以 <see cref="AnchorAtFeet"/> 還是元件上的值、不是表格欄位。
/// **第二個血統需要不同錨點時**，照 <c>OrbitCount</c>／<c>OrbitSize</c> 的方式開成表B 欄位即可。</para>
///
/// <para>⚠ VfxTable 那一列是**一次性**的：<c>Loop=0</c>、<c>Duration</c> 留空。播放速度用該列的
/// <c>AnimFPS</c> 調（幀數 ÷ FPS ＝ 一輪多長），不要在這裡另外做計時。</para>
/// </summary>
[DisallowMultipleComponent]
public class BloodlineAttackFx : MonoBehaviour
{
    [Tooltip("特效高度 = 角色高度 × 此值（1.25 ≒ 比角色略大一圈、像罩上去）")]
    public float HeightRatio = 1.25f;

    [Tooltip("特效底邊對齊腳底（往上竄的火焰/噴發用）；取消勾選 = 特效中心對齊身體中心（球狀爆炸用）")]
    public bool AnchorAtFeet = true;

    [Tooltip("兩次播放之間的最短間隔（秒）。壓住攻擊時就是每隔這麼久播一次；由表B 的 AttackFxGap 覆寫")]
    public float Gap = 3f;

    int _vfxId;
    float _lastAt = -999f;

    PlayerController _pc;
    PlayerAnimator _anim;
    VfxManager _vfx;

    void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _anim = GetComponent<PlayerAnimator>();
    }

    // 持續型武器（雷射／佛光）：按著不放的期間 TrySpawnFireEffect 一次都不會再被呼叫，
    // 所以這裡每幀問一次「還在放嗎」。NotifyAttack 自己有冷卻，所以每幀叫也只會照 Gap 播。
    void Update()
    {
        if (_vfxId <= 0) return;
        if (_pc != null && _pc.IsContinuousFireActive) NotifyAttack();
    }

    /// <summary>
    /// 設定攻擊特效；<paramref name="vfxId"/> ≤ 0 = 關掉這一層。
    /// <paramref name="gapSeconds"/> ≤ 0 = 保留 Inspector 上的值。
    /// </summary>
    public void SetEffect(int vfxId, float gapSeconds = 0f)
    {
        _vfxId = Mathf.Max(0, vfxId);
        if (gapSeconds > 0f) Gap = gapSeconds;
        // 換血統當下不重置 _lastAt：剛變身完馬上攻擊本來就該播得出來（冷卻是給「壓住連射」用的）。
    }

    /// <summary>
    /// 由 <c>PlayerController</c> 在每次出手時呼叫。冷卻沒到就什麼都不做。
    /// </summary>
    public void NotifyAttack()
    {
        if (_vfxId <= 0) return;
        if (Time.time - _lastAt < Mathf.Max(0f, Gap)) return;

        // 與另外四層同一個白名單：趴著（dead 幀）時身體幾何的定位算式不成立，整層關掉而不是硬對。
        if (_anim != null && !_anim.BodyFxVisible) return;

        if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();
        if (_vfx == null) return;                     // 場景還沒就緒 → 這次不播，下次再試（不記時間戳）

        float h = (_pc != null ? _pc.ScaledCharacterHeight : 2f) * Mathf.Max(0.01f, HeightRatio);

        // 錨點要看素材的構圖：「從地面往上竄」的圖，它的**底邊就是地面**，中心對齊身體中心會整個懸空；
        // 球狀爆炸則相反，中心對齊才是對的。SpawnSizedToHeight 收的是特效中心，所以腳底錨要自己往上推半個高度。
        Vector2 c;
        if (_pc == null) c = transform.position;
        else if (AnchorAtFeet) c = _pc.FeetWorldPos + new Vector2(0f, h * 0.5f);
        else c = _pc.BodyCenterWorldPos;

        var inst = _vfx.SpawnSizedToHeight(_vfxId, c, h);
        if (inst == null) return;                     // 表格填錯／素材缺 → 同樣不記時間戳，免得靜默卡死
        inst.transform.SetParent(transform, true);    // 播放期間跟著角色走

        _lastAt = Time.time;
    }
}
