using UnityEngine;

/// <summary>
/// 一個躺在地上的掉落物：用該道具的背包 icon 縮小顯示，並在上方常駐一行「名稱x數量」標籤
/// （只有 1 個時不顯示 x1，例如「銅錢」；多個顯示「銅錢x25」）。標籤預設顯示、可按 Shift 全域切換
/// （見 <see cref="InteractionManager"/>）。「按 F 拾取」提示則是靠近才出現（PickupTipPanel，由 InteractionManager 驅動）。
/// 由 InteractionManager 生成與管理，本元件只持資料與外觀。
/// </summary>
public class GroundLoot : MonoBehaviour
{
    public int ItemId { get; private set; }
    public int Count { get; private set; }
    public string DisplayName { get; private set; }

    /// <summary>
    /// 這一件專屬的資料（孔位/珠子等級）；null = 一般可疊道具。
    /// **東西掉在地上的那一刻就已經決定了**——玩家撿起來拿到的就是標籤上寫的那一把，不會重骰。
    /// 見 readme/GEM_SOCKET.md。
    /// </summary>
    public Dipan.Inventory.ItemInstance Inst { get; private set; }

    /// <summary>把這一件包成可以直接放進背包/臨時包的 ItemStack。</summary>
    public Dipan.Inventory.ItemStack ToStack()
        => new Dipan.Inventory.ItemStack { ItemId = ItemId, Count = Count, Inst = Inst };

    /// <summary>對應 RunProgress 的掉落物記錄 id（&gt;0 = 本趟關卡登記過、撿取/更新要回寫）；0 = 未登記（廣場溢出等）。</summary>
    public int RunDropId;

    /// <summary>地上掉落物「名稱x數量」標籤的全域顯示開關（Shift 切換；預設顯示）。新生成的掉落物沿用目前狀態。</summary>
    public static bool LabelsVisible = true;

    // ── 名稱標籤外觀常數（要調就改這裡；風格對齊 DamageNumberManager 的世界 TextMesh）──
    const int LabelFontSize = 64;            // 字型解析度（大=清晰；世界大小由 characterSize 控）
    const float LabelCharSize = 0.05f;       // TextMesh 世界縮放
    const float LabelGap = 0.12f;            // 標籤在 icon 上緣之上的間距（世界單位）
    const int LabelSortingOrder = 20000;     // 高於角色/地上物 Y 排序帶（16-bit 安全，<32767），名稱永遠讀得到
    const float LabelShadowOffset = 0.03f;   // 描邊/陰影偏移（世界單位）
    static readonly Color LabelColor = new Color(1f, 0.95f, 0.72f, 1f);        // 暖白
    static readonly Color LabelShadowColor = new Color(0f, 0f, 0f, 0.85f);     // 黑描邊

    SpriteRenderer _sr;
    GameObject _labelGo;
    TextMesh _labelTm, _labelShadowTm;
    float _labelWorldY;   // 標籤世界 Y（icon 上緣 + 間距）；掉落物不移動，算一次即可

    /// <summary>
    /// 初始化外觀與資料。worldSize = 圖在世界中的目標大小（依 sprite 實際尺寸換算縮放，與 PPU 無關）。
    /// 圖示一律走 <see cref="Dipan.UI.ItemIcons"/>——能力珠是「珠身（依等級）＋能力符號」兩層，
    /// 所以地上就看得出是幾級的什麼珠（見 readme/GEM_SOCKET.md）。
    /// </summary>
    public void Init(Dipan.Inventory.ItemStack stack, string displayName, float worldSize,
                     string sortingLayerName, int sortingOrder)
    {
        ItemId = stack.ItemId;
        Count = stack.Count;
        Inst = stack.Inst;
        DisplayName = displayName;

        _sr = gameObject.GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sortingLayerName = sortingLayerName;
        _sr.sortingOrder = sortingOrder;
        Dipan.UI.ItemIcons.Apply(_sr, stack, sortingLayerName, sortingOrder);
        Sprite icon = _sr.sprite;

        // 依 sprite 實際世界尺寸縮放到 worldSize（取較長邊），讓不同 icon 在地上大小一致。
        float drawnHeight = worldSize;
        if (icon != null)
        {
            Vector2 sz = icon.bounds.size;
            float longest = Mathf.Max(sz.x, sz.y);
            float scale = (longest > 0.0001f) ? worldSize / longest : 1f;
            transform.localScale = new Vector3(scale, scale, 1f);
            drawnHeight = sz.y * scale;   // icon 實際世界高度（標籤擺在其上緣之上）
        }

        _labelWorldY = transform.position.y + drawnHeight * 0.5f + LabelGap;
        BuildLabel();
    }

    /// <summary>更新堆疊數量（部分撿取後剩餘），同步刷新名稱標籤。</summary>
    public void SetCount(int count)
    {
        Count = count;
        RefreshLabelText();
    }

    /// <summary>切換此掉落物名稱標籤的顯示（InteractionManager 在 Shift 時對全部掉落物呼叫）。</summary>
    public void SetLabelVisible(bool visible)
    {
        if (_labelGo != null) _labelGo.SetActive(visible);
    }

    // 「名稱」或「名稱x數量」（只有 1 個時不加 x1）；有孔的裝備會在後面標出孔數，
    // 讓玩家在地上就看得出「這把是 6 孔的」——這是打寶的即時回饋。
    string LabelText()
    {
        string t = Count > 1 ? $"{DisplayName}x{Count}" : DisplayName;
        if (Inst != null)
        {
            if (Inst.HasSockets && Inst.UnlockedCount > 0) t += $" ({Inst.UnlockedCount}孔)";
            else if (Inst.level > 0) t += $" Lv{Inst.level}";
        }
        return t;
    }

    // 名稱標籤：獨立世界 GameObject（不掛在被縮放的 icon 底下，字級才不隨 icon 大小變動）。掉落物不移動，位置算一次。
    void BuildLabel()
    {
        _labelGo = new GameObject("GroundLootLabel");
        _labelGo.transform.position = new Vector3(transform.position.x, _labelWorldY, 0f);

        // 描邊感：黑色陰影複本（右下偏移、低一階排序）
        var shadowGo = new GameObject("Shadow");
        shadowGo.transform.SetParent(_labelGo.transform, false);
        shadowGo.transform.localPosition = new Vector3(LabelShadowOffset, -LabelShadowOffset, 0f);
        _labelShadowTm = BuildTextMesh(shadowGo, LabelShadowColor, LabelSortingOrder - 1);

        _labelTm = BuildTextMesh(_labelGo, LabelColor, LabelSortingOrder);

        RefreshLabelText();
        _labelGo.SetActive(LabelsVisible);
    }

    TextMesh BuildTextMesh(GameObject go, Color color, int sortingOrder)
    {
        var tm = go.AddComponent<TextMesh>();
        tm.font = Dipan.UI.UIBuilder.DefaultFont;
        tm.fontSize = LabelFontSize;
        tm.characterSize = LabelCharSize;
        tm.anchor = TextAnchor.LowerCenter;   // 文字底部貼在 icon 上緣之上，往上長
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null && tm.font != null)
        {
            mr.sharedMaterial = tm.font.material;
            mr.sortingOrder = sortingOrder;
        }
        return tm;
    }

    void RefreshLabelText()
    {
        string t = LabelText();
        if (_labelTm != null) _labelTm.text = t;
        if (_labelShadowTm != null) _labelShadowTm.text = t;
    }

    void OnDestroy()
    {
        if (_labelGo != null) Destroy(_labelGo);   // 標籤是獨立物件，隨掉落物一起銷毀
    }
}
