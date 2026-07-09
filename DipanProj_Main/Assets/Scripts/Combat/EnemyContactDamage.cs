using UnityEngine;

/// <summary>
/// 怪物接觸傷害（陣營制）：碰到「敵對陣營」就對它扣血。
///  ‧ Enemy 陣營怪 → 打玩家 ＋ 玩家的召喚物(PlayerAlly)。
///  ‧ PlayerAlly（玩家召喚的協戰怪）→ 打敵怪(Enemy)。
///
/// **重要**：目標判定走「怪物登記表(<see cref="MonsterController.Active"/>) ＋ <see cref="Physics2D.Distance"/>」，
/// **不用 OverlapCircle**——專案全域 `queriesStartInColliders=false` 會讓 OverlapCircle 漏抓「重疊在查詢起點」的
/// 貼身目標（且因兩者大小/位置不同而不對稱：敵人打得到友軍、友軍卻打不到敵人）。`Physics2D.Distance` 是兩個
/// collider 的直接距離運算，不受該設定影響——這也是原本「敵人打玩家」一直能穩定運作的原因。
/// 反覆接觸的節流靠目標自己的無敵時間；傷害統一走中央 <see cref="CombatSystem"/>。見 readme/COMBAT.md、PROBLEMS.md。
/// </summary>
public class EnemyContactDamage : MonoBehaviour
{
    private float _damage;
    private MonsterFaction _faction = MonsterFaction.Enemy;
    private Collider2D _myCol;
    private Transform _player;
    private Collider2D _playerCol;

    /// <summary>由 MonsterController 設定接觸傷害值（MonsterData.ContactDamage）與陣營。</summary>
    public void Configure(float contactDamage, MonsterFaction faction = MonsterFaction.Enemy)
    {
        _damage = contactDamage;
        _faction = faction;
        _myCol = GetComponent<Collider2D>();
    }

    void Update()
    {
        if (_damage <= 0f) return;
        if (_myCol == null)
        {
            _myCol = GetComponent<Collider2D>();
            if (_myCol == null) return;
        }

        if (_faction == MonsterFaction.Enemy)
        {
            DamagePlayerIfTouching();                       // 敵人 → 玩家
            DamageMonstersOfFaction(MonsterFaction.PlayerAlly); // 敵人 → 玩家召喚物
        }
        else
        {
            DamageMonstersOfFaction(MonsterFaction.Enemy);  // 友軍 → 敵怪
        }
    }

    private void DamagePlayerIfTouching()
    {
        if (_player == null || _playerCol == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            _player = p.transform;
            _playerCol = p.GetComponent<Collider2D>();
            if (_playerCol == null) _playerCol = p.GetComponentInChildren<Collider2D>();
            if (_playerCol == null) return;
        }
        if (Touching(_playerCol)) Hit(_player.gameObject);
    }

    private void DamageMonstersOfFaction(MonsterFaction target)
    {
        var list = MonsterController.Active;
        for (int i = 0; i < list.Count; i++)
        {
            MonsterController mc = list[i];
            if (mc == null || mc.IsDead || mc.Faction != target) continue;
            if (mc.gameObject == gameObject) continue;
            Collider2D col = mc.GetComponent<Collider2D>();
            if (col == null) continue;
            if (Touching(col)) Hit(mc.gameObject);
        }
    }

    private bool Touching(Collider2D other)
    {
        ColliderDistance2D d = Physics2D.Distance(_myCol, other);
        return d.isValid && (d.isOverlapped || d.distance <= 0.02f);
    }

    private void Hit(GameObject target)
    {
        Vector2 dir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
        CombatSystem.Apply(gameObject, target, _damage, dir);
    }
}
