using System.Collections;
using System.Collections.Generic;
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
    const float HitBoxW = 0.9f;     // 後備核心框寬（× scale）：只有在抓不到地刺特效可見邊界時才用
    const float HitBoxH = 1.3f;     // 後備核心框高（× scale）
    const float PlayerHitInterval = 0.25f; // 對玩家的重擊嘗試間隔（真正節流靠玩家無敵時間）

    // 【除錯】true = 地刺危險時，用紅框把『實際受傷範圍』畫在畫面上（Game View 也看得到）。抓完手感改回 false。
    public static bool DebugDrawHitbox = false;

    static VfxManager _vfx;
    static readonly List<BossSpike> _active = new List<BossSpike>();   // 場上所有還在的地刺（boss 死掉要一次收掉）

    MonsterController _boss;   // 反傷對象（本體）
    float _damage;             // 碰到玩家的傷害
    float _scale;              // 1 = 一般，10 = 放大版大絕
    bool _exposed;             // 危險窗內才 true（才傷人、才可被打）
    BoxCollider2D _col;
    VfxInstance _spikeVfx;   // 冒出的地刺特效實體（被打時閃白光用）
    VfxInstance _arrowVfx;   // 預警箭頭特效實體（提前取消時要一起收）
    EnemyContactDamage _contact;
    LineRenderer _dbgBox;   // 除錯用：受傷範圍紅框

    /// <summary>生成一根地刺。pos = 生成點（可走地面），scale = 大小倍率，boss = 反傷對象，damage = 碰玩家傷害。</summary>
    // hitFillW / hitFillH：碰撞框＝可見地刺的寬/高比例（貼齊基座往上長）。預設就套用，讓每一根地刺都跟大地刺同樣範圍。
    public static BossSpike Fire(Vector2 pos, float scale, MonsterController boss, float damage, float startDelay = 0f, float hitFillW = 0.75f, float hitFillH = 0.60f)
    {
        var go = new GameObject("BossSpike");
        go.transform.position = pos;
        var s = go.AddComponent<BossSpike>();
        s.Init(pos, scale, boss, damage, startDelay, hitFillW, hitFillH);
        return s;
    }

    float _startDelay;
    float _hitFillW;  // >0：冒出時把碰撞框寬對齊「可見地刺」的這個比例（放大版大絕用）；0：用固定核心框
    float _hitFillH;  // >0：碰撞框高對齊可見地刺的比例，且『貼齊地刺基座』往上長（地刺是從底部往上冒，圖的上半是空的）

    void Init(Vector2 pos, float scale, MonsterController boss, float damage, float startDelay, float hitFillW, float hitFillH)
    {
        _boss = boss;
        _startDelay = startDelay;
        _hitFillW = hitFillW;
        _hitFillH = hitFillH;
        _scale = scale <= 0f ? 1f : scale;
        _damage = damage;
        if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();
        _active.Add(this);

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
        if (_startDelay > 0f) yield return new WaitForSeconds(_startDelay);   // 排掃：錯開時間 → 推進浪

        // ① 預警：箭頭往下
        if (_vfx != null) _arrowVfx = _vfx.Spawn(ArrowVfxId, transform.position, 0f, _scale);
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
        // 放大版大絕：冒出當下把碰撞框對齊「看得到的地刺」。地刺是從圖的底部往上冒、圖的上半是空的，
        // 所以碰撞框要『貼齊基座、只涵蓋下半』，不能罩滿整張圖（會在空白處誤傷）。
        if (on && _col != null && _hitFillW > 0f && _hitFillH > 0f && _spikeVfx != null)
        {
            Bounds b = _spikeVfx.WorldBounds;
            if (b.size.x > 0.01f && b.size.y > 0.01f)
            {
                float w = b.size.x * _hitFillW;
                float h = b.size.y * _hitFillH;
                _col.size = new Vector2(w, h);
                float boxBottom = b.center.y - b.extents.y;          // 貼齊圖底＝地刺基座
                float centerY   = boxBottom + h * 0.5f;              // 從基座往上長
                _col.offset = new Vector2(b.center.x - transform.position.x, centerY - transform.position.y);
            }
        }
        if (_col != null) _col.enabled = on;
        if (_contact != null) _contact.enabled = on;

        if (DebugDrawHitbox) { if (on) DrawDebugBox(); else ClearDebugBox(); }
    }

    // 用 LineRenderer 把碰撞框（含 size/offset）畫成紅色矩形，Game View 也看得到。
    void DrawDebugBox()
    {
        if (_col == null) return;
        if (_dbgBox == null)
        {
            var go = new GameObject("SpikeHitboxDbg");
            go.transform.SetParent(transform, false);
            _dbgBox = go.AddComponent<LineRenderer>();
            _dbgBox.useWorldSpace = false;
            _dbgBox.loop = true;
            _dbgBox.positionCount = 4;
            _dbgBox.widthMultiplier = 0.06f;
            _dbgBox.numCornerVertices = 0;
            _dbgBox.material = new Material(Shader.Find("Sprites/Default"));
            _dbgBox.startColor = _dbgBox.endColor = new Color(1f, 0f, 0f, 0.9f);
            _dbgBox.sortingLayerName = "Default";
            _dbgBox.sortingOrder = 32000;   // 疊在最上面
        }
        Vector2 c = _col.offset;
        Vector2 h = _col.size * 0.5f;
        _dbgBox.SetPosition(0, new Vector3(c.x - h.x, c.y - h.y, 0f));
        _dbgBox.SetPosition(1, new Vector3(c.x + h.x, c.y - h.y, 0f));
        _dbgBox.SetPosition(2, new Vector3(c.x + h.x, c.y + h.y, 0f));
        _dbgBox.SetPosition(3, new Vector3(c.x - h.x, c.y + h.y, 0f));
        _dbgBox.enabled = true;
    }

    void ClearDebugBox()
    {
        if (_dbgBox != null) _dbgBox.enabled = false;
    }

    void OnDestroy() { _active.Remove(this); }

    /// <summary>把場上所有地刺（含預警中、冒出中）立刻收掉——boss 被打敗時呼叫，招式不再傷人。</summary>
    public static void CancelAll()
    {
        var snapshot = new List<BossSpike>(_active);   // Destroy 會改動 _active，先複製一份
        foreach (var s in snapshot)
            if (s != null) s.CancelImmediate();
        _active.Clear();
    }

    // 立刻消滅這根地刺：連它的預警箭頭 / 地刺特效一起收（不留殘影、不再判傷）。
    void CancelImmediate()
    {
        if (_arrowVfx != null) Destroy(_arrowVfx.gameObject);
        if (_spikeVfx != null) Destroy(_spikeVfx.gameObject);
        Destroy(gameObject);
    }

    // 玩家打到「露在外面的地刺」→ 傷害轉給榕樹妖本體（收回/預警期間打不到，這裡再保險擋一層）。
    public void TakeDamage(float amount, Vector2 hitDirection)
    {
        if (!_exposed || _boss == null) return;
        if (_spikeVfx != null) _spikeVfx.Flash();          // 受擊白光（像怪物被打一樣）
        _boss.TakeDamage(amount, hitDirection);
    }
}
