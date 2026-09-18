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
    IAirborneVisual _airSrc;   // 「現在離地多高」的來源；null＝這個角色不會離地（＝既有所有角色，行為不變）

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = GetComponentInChildren<SpriteRenderer>();
        _airSrc = GetComponent<IAirborneVisual>();
    }

    void LateUpdate()
    {
        if (_sr == null) return;
        // 騰空時 transform.position.y 被往上推了，排序基準要扣回地面——否則角色跳起來的那半秒
        // 會突然被「其實在牠後面」的地上物蓋住（Y 越大＝越後面）。見 IAirborneVisual。
        float airH = (_airSrc != null) ? Mathf.Max(0f, _airSrc.AirborneHeight) : 0f;
        _sr.sortingOrder = MapDepthSort.Order(transform.position.y - airH + FeetYOffset, 0);
    }
}
