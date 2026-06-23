using UnityEngine;

/// <summary>
/// 一個躺在地上的掉落物：用該道具的背包 icon 縮小顯示（之後要換成專屬地上圖再說）。
/// 由 <see cref="InteractionManager"/> 生成與管理，本元件只持資料與外觀。
/// </summary>
public class GroundLoot : MonoBehaviour
{
    public int ItemId { get; private set; }
    public int Count { get; private set; }
    public string DisplayName { get; private set; }

    SpriteRenderer _sr;

    /// <summary>初始化外觀與資料。worldSize = 圖在世界中的目標大小（依 sprite 實際尺寸換算縮放，與 PPU 無關）。</summary>
    public void Init(int itemId, int count, string displayName, Sprite icon, float worldSize,
                     string sortingLayerName, int sortingOrder)
    {
        ItemId = itemId;
        Count = count;
        DisplayName = displayName;

        _sr = gameObject.GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = icon;
        _sr.sortingLayerName = sortingLayerName;
        _sr.sortingOrder = sortingOrder;

        // 依 sprite 實際世界尺寸縮放到 worldSize（取較長邊），讓不同 icon 在地上大小一致。
        if (icon != null)
        {
            Vector2 sz = icon.bounds.size;
            float longest = Mathf.Max(sz.x, sz.y);
            float scale = (longest > 0.0001f) ? worldSize / longest : 1f;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    /// <summary>更新堆疊數量（部分撿取後剩餘）。</summary>
    public void SetCount(int count) => Count = count;
}
