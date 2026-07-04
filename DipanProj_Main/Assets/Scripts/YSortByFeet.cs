using UnityEngine;

/// <summary>
/// 讓角色（玩家/怪物）依「腳底 Y」動態決定 sortingOrder，和地上物走同一套 Y 排序（見 <see cref="MapDepthSort"/>）。
/// 每幀在 LateUpdate 更新，所以角色移動時會即時和地上物正確交錯遮蔽：
/// 走到地上物「前面（畫面下方）」就蓋住它、走到「後面（畫面上方）」就被它蓋住。
///
/// 掛法：玩家在 PlayerController.Start、怪物在 MonsterController.Start 各自 AddComponent（仿 BlobShadow）。
/// 排序基準用 transform.position.y ＋ <see cref="FeetYOffset"/>；若覺得角色「太早/太晚」被地上物蓋住，微調這個偏移即可。
/// </summary>
[DisallowMultipleComponent]
public class YSortByFeet : MonoBehaviour
{
    [Tooltip("排序基準 = transform.position.y + 此偏移。負值＝把基準點下移（角色更容易畫在地上物前面）。")]
    public float FeetYOffset = 0f;

    SpriteRenderer _sr;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = GetComponentInChildren<SpriteRenderer>();
    }

    void LateUpdate()
    {
        if (_sr == null) return;
        _sr.sortingOrder = MapDepthSort.Order(transform.position.y + FeetYOffset, 0);
    }
}
