using UnityEngine;

/// <summary>
/// 怪物接觸傷害：怪物碰到玩家就扣血。
///
/// 為什麼不靠物理碰撞事件：專案的 Layer Collision Matrix 把 **Enemy×Player 關閉**（怪物穿過玩家、
/// 不互推），所以 OnCollision / IsTouching 都不會回報。改用 <see cref="Physics2D.Distance"/> 做幾何
/// 重疊判定——它直接算兩個 collider 的距離，**不受碰撞矩陣影響**。
///
/// 反覆接觸的節流由「玩家自己的無敵時間（HitReactionHandler）」處理：第一次碰到扣血並進入無敵，
/// 無敵期間再怎麼貼著都不會再扣。傷害一律走中央 <see cref="CombatSystem"/>（吃玩家減傷等修正）。
/// 見 readme/COMBAT.md。
/// </summary>
public class EnemyContactDamage : MonoBehaviour
{
    private float _damage;
    private Collider2D _myCol;
    private Transform _player;
    private Collider2D _playerCol;

    /// <summary>由 MonsterController 設定接觸傷害值（來自 MonsterData.csv 的 ContactDamage）。</summary>
    public void Configure(float contactDamage)
    {
        _damage = contactDamage;
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

        // 快取玩家；遺失（死亡/重生）時自動重找
        if (_player == null || _playerCol == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            _player = p.transform;
            _playerCol = p.GetComponent<Collider2D>();
            if (_playerCol == null) _playerCol = p.GetComponentInChildren<Collider2D>();
            if (_playerCol == null) return;
        }

        // 幾何距離（無視碰撞矩陣）；重疊或極近 = 接觸
        ColliderDistance2D d = Physics2D.Distance(_myCol, _playerCol);
        if (d.isValid && (d.isOverlapped || d.distance <= 0.02f))
        {
            Vector2 hitDir = ((Vector2)_player.position - (Vector2)transform.position).normalized;
            // 來源 = 怪物（其 ICombatModifiers 目前攻擊加成 = 1）；目標 = 玩家（吃玩家減傷 + 無敵時間）
            CombatSystem.Apply(gameObject, _player.gameObject, _damage, hitDir);
        }
    }
}
