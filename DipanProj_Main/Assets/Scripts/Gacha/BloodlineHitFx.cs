using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 血統特效第六層：**打到怪物時，在那隻怪身上播一次的擊中特效**（蟲皇的咬擊）。
///
/// <para><b>與第五層 <see cref="BloodlineAttackFx"/> 的差別是「在誰身上」</b>：第五層是「出手就播、罩在
/// 自己身上」，不管有沒有打到東西；這一層是「**打中了才播、播在對方身上**」。所以它的定位基準在
/// **目標**身上（大小跟著那隻怪的可見高度走，大怪大特效、小怪小特效），而不是玩家的身體幾何——
/// 這是唯一一層不看 <c>FeetWorldPos</c>／<c>BodyCenterWorldPos</c> 的。</para>
///
/// <para><b>⚠ 這一層凌駕武器自己的 <c>HitEffectID</c></b>（2026-09-14 作者拍板，**所有三階血統通用**，
/// 不是蟲皇的特例）：血統填了這一欄之後，**打到怪物**時武器的擊中特效讓位、只播血統這一個；
/// **打到牆／地上物仍照播武器自己的**——那不是「攻擊到怪物」，讓位會讓打牆的回饋憑空消失。
/// 讓位的實作在 <c>PlayerController.TrySpawnHitEffect</c>（它多收一個 <c>hitEnemy</c> 參數）。</para>
///
/// <para><b>怎麼知道打中了</b>：掛在 <see cref="CombatSystem.Apply"/> 的結尾——那是**全遊戲傷害的
/// 單一入口**，所以四種武器路徑（子彈／雷射 tick／連鎖閃電／AOE 爆炸）一條都不會漏，
/// 也不必像第五層那樣「離散與持續各接一條」。⚠ 因此**這一層不需要知道武器是什麼**，
/// 之後新增武器模式也不用回來改這裡。</para>
///
/// <para><b>節流：每隻怪各自計時</b>（<see cref="Gap"/>，表B 的 <c>HitFxGap</c>）。雷射與火焰噴射器
/// 每個 <c>DotInterval</c> tick 都會命中同一隻怪，不節流會在牠身上狂閃；而分怪計時又能保證
/// 「AOE 一次打中五隻 ＝ 五隻身上都有」——全域計時會讓範圍感整個不見。</para>
/// </summary>
[DisallowMultipleComponent]
public class BloodlineHitFx : MonoBehaviour
{
    [Tooltip("同一隻怪兩次擊中特效之間的最短間隔（秒）。由表B 的 HitFxGap 覆寫")]
    public float Gap = 0.35f;

    [Tooltip("特效高度 = 目標可見高度 × 此值（1 = 與目標等高）。再乘上 VfxTable 那一列的 Scale")]
    public float HeightRatio = 1f;

    /// <summary>
    /// 目前玩家身上這一層的 VfxTable id；0 = 沒有這一層。
    /// <c>PlayerController</c> 用它決定「武器的擊中特效要不要讓位」。
    /// </summary>
    public static int ActiveVfxId { get; private set; }

    /// <summary>玩家身上是否掛著血統擊中特效（＝武器的 <c>HitEffectID</c> 打到怪時該讓位）。</summary>
    public static bool HasEffect => ActiveVfxId > 0;

    static BloodlineHitFx _instance;

    int _vfxId;
    VfxManager _vfx;

    // 每隻怪各自的上次播放時間（鍵＝目標的 InstanceID）。
    readonly Dictionary<int, float> _lastAt = new Dictionary<int, float>();
    float _nextSweepAt;

    void OnEnable() { _instance = this; ActiveVfxId = _vfxId; }

    void OnDisable()
    {
        if (_instance == this) { _instance = null; ActiveVfxId = 0; }
    }

    /// <summary>
    /// 設定擊中特效；<paramref name="vfxId"/> ≤ 0 = 關掉這一層。
    /// <paramref name="gapSeconds"/> ≤ 0 = 保留 Inspector 上的值。
    /// </summary>
    public void SetEffect(int vfxId, float gapSeconds = 0f)
    {
        _vfxId = Mathf.Max(0, vfxId);
        if (gapSeconds > 0f) Gap = gapSeconds;
        if (_instance == this) ActiveVfxId = _vfxId;
        _lastAt.Clear();   // 換血統 = 換特效，舊的冷卻紀錄沒有意義
    }

    /// <summary>
    /// 由 <see cref="CombatSystem.Apply"/> 在「傷害真的結算出去」之後呼叫。
    /// 來源不是玩家、目標不是怪、沒掛這一層、或該目標的冷卻沒到，都會安靜跳過。
    /// </summary>
    public static void NotifyHit(GameObject source, GameObject target)
    {
        if (_instance == null || _instance._vfxId <= 0) return;
        if (source == null || target == null) return;
        if (source != _instance.gameObject) return;        // 只有玩家自己打出去的才算（怪打怪不播）
        _instance.PlayOn(target);
    }

    void PlayOn(GameObject victim)
    {
        // 只在怪物身上播。可破壞地上物也實作 IDamageable，但「攻擊到怪物」不包含打爆家具——
        // 那種情況維持武器自己的擊中特效（PlayerController 那邊的 hitEnemy 判斷同一條線）。
        var mc = victim.GetComponentInParent<MonsterController>();
        if (mc == null) return;

        int key = mc.gameObject.GetInstanceID();
        float now = Time.time;
        if (_lastAt.TryGetValue(key, out float last) && now - last < Mathf.Max(0f, Gap)) return;

        if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();
        if (_vfx == null) return;                          // 場景還沒就緒 → 這次不播，也不記時間戳

        // 大小與位置都跟著「那隻怪」走（同怪物死亡特效的做法，見 VFX.md）。
        // 取不到 renderer 時退回 transform 位置、不指定大小——寧可大小不準，也不要整個不播。
        var sr = mc.GetComponentInChildren<SpriteRenderer>();
        VfxInstance inst;
        if (sr != null && sr.bounds.size.y > 0.0001f)
            inst = _vfx.SpawnSizedToHeight(_vfxId, sr.bounds.center, sr.bounds.size.y * Mathf.Max(0.01f, HeightRatio));
        else
            inst = _vfx.Spawn(_vfxId, mc.transform.position, 0f);

        if (inst == null) return;                          // 表格填錯／素材缺 → 不記時間戳，免得靜默卡死
        _lastAt[key] = now;

        Sweep(now);
    }

    /// <summary>
    /// 清掉早就過期的紀錄（怪死了之後鍵會永遠留著）。冷卻只看「上次到現在」，
    /// 所以任何超過 Gap 很久的項目都可以丟——下次再打到同一隻，查不到就是直接播。
    /// </summary>
    void Sweep(float now)
    {
        if (now < _nextSweepAt) return;
        _nextSweepAt = now + 10f;

        float stale = Mathf.Max(1f, Gap) * 4f;
        var drop = new List<int>();
        foreach (var kv in _lastAt)
            if (now - kv.Value > stale) drop.Add(kv.Key);
        for (int i = 0; i < drop.Count; i++) _lastAt.Remove(drop[i]);
    }
}
