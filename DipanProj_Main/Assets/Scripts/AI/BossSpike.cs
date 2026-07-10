using System.Collections;
using UnityEngine;

/// <summary>
/// 榕樹妖的「地刺」攻擊實體（一次性）。不是怪物、也不只是特效，而是自己跑三段狀態機的攻擊物：
///  ① 預警：在地上播「箭頭往下」(VfxTable 13)，warnTime 秒——無碰撞、不傷人、打不到。
///  ② 冒出（危險窗）：播「榕樹妖地刺」(VfxTable 11)，開一個 trigger 碰撞框(Enemy 層)＋掛 EnemyContactDamage
///     → 玩家碰到就扣血（沿用怪物接觸傷害那套，走玩家無敵幀、連續扣）；並實作 IDamageable → 玩家打它就把傷害轉給榕樹妖本體。
///  ③ 收回：關碰撞、等地刺動畫視覺播完，銷毀自己。
/// 由 <see cref="BanyanTreeBrain"/> 在隨機可走點生成。單根/多根/一整排/放大版都用這同一個實體（差在生成的位置/時間/scale）。
/// </summary>
public class BossSpike : MonoBehaviour, IDamageable
{
    // ── 手感常數（要調就改這裡）──
    const int   ArrowVfxId  = 13;   // 箭頭往下（預警）
    const int   SpikeVfxId  = 11;   // 榕樹妖的地刺
    const float WarnTime    = 1.0f; // 箭頭預警多久後地刺才冒出
    const float EruptRise   = 0.25f;// 地刺開始冒出到「真的危險/可打」的緩衝
    const float ActiveTime  = 1.1f; // 危險＋可被打的窗口長度
    const float SpikeVfxLife = 2.2f;// 地刺特效總壽命（讓收回動畫視覺播完才銷毀）
    const float HitBoxW = 0.9f;     // 危險/命中框寬（× scale）
    const float HitBoxH = 1.3f;     // 危險/命中框高（× scale）
    const float PlayerHitInterval = 0.25f; // 對玩家的重擊嘗試間隔（真正節流靠玩家無敵時間）

    static VfxManager _vfx;

    MonsterController _boss;   // 反傷對象（本體）
    float _damage;             // 碰到玩家的傷害
    float _scale;              // 1 = 一般，10 = 放大版大絕
    bool _exposed;             // 危險窗內才 true（才傷人、才可被打）
    BoxCollider2D _col;
    VfxInstance _spikeVfx;   // 冒出的地刺特效實體（被打時閃白光用）
    EnemyContactDamage _contact;

    /// <summary>生成一根地刺。pos = 生成點（可走地面），scale = 大小倍率，boss = 反傷對象，damage = 碰玩家傷害。</summary>
    public static BossSpike Fire(Vector2 pos, float scale, MonsterController boss, float damage)
    {
        var go = new GameObject("BossSpike");
        go.transform.position = pos;
        var s = go.AddComponent<BossSpike>();
        s.Init(pos, scale, boss, damage);
        return s;
    }

    void Init(Vector2 pos, float scale, MonsterController boss, float damage)
    {
        _boss = boss;
        _scale = scale <= 0f ? 1f : scale;
        _damage = damage;
        if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();

        // 危險/命中框：trigger（不擋路），Enemy 層讓玩家武器打得到。先建好，預警期間關閉、冒出時才開。
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) gameObject.layer = enemyLayer;
        _col = gameObject.AddComponent<BoxCollider2D>();
        _col.isTrigger = true;
        _col.size = new Vector2(HitBoxW * _scale, HitBoxH * _scale);
        _col.enabled = false;

        // 對玩家的接觸傷害：沿用怪物那套（EnemyContactDamage，Enemy 陣營）——走玩家無敵幀、連續扣。預警期間關閉。
        _contact = gameObject.AddComponent<EnemyContactDamage>();
        _contact.Configure(_damage, MonsterFaction.Enemy, PlayerHitInterval);
        _contact.enabled = false;

        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // ① 預警：箭頭往下
        if (_vfx != null) _vfx.Spawn(ArrowVfxId, transform.position, 0f, _scale);
        yield return new WaitForSeconds(WarnTime);

        // ② 冒出：地刺特效 + 開危險/命中窗
        if (_vfx != null) _spikeVfx = _vfx.Spawn(SpikeVfxId, transform.position, 0f, _scale);
        yield return new WaitForSeconds(EruptRise);

        SetExposed(true);
        yield return new WaitForSeconds(ActiveTime);
        SetExposed(false);

        // ③ 收回：等地刺動畫視覺播完再銷毀
        float rest = SpikeVfxLife - EruptRise - ActiveTime;
        if (rest > 0f) yield return new WaitForSeconds(rest);
        Destroy(gameObject);
    }

    void SetExposed(bool on)
    {
        _exposed = on;
        if (_col != null) _col.enabled = on;
        if (_contact != null) _contact.enabled = on;
    }

    // 玩家打到「露在外面的地刺」→ 傷害轉給榕樹妖本體（收回/預警期間打不到，這裡再保險擋一層）。
    public void TakeDamage(float amount, Vector2 hitDirection)
    {
        if (!_exposed || _boss == null) return;
        if (_spikeVfx != null) _spikeVfx.Flash();          // 受擊白光（像怪物被打一樣）
        _boss.TakeDamage(amount, hitDirection);
    }
}
