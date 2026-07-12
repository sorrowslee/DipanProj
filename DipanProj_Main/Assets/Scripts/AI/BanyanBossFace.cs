using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 榕樹妖的「臉」地上物控制器（掛在地圖上的 treeFace 地上物，由 MapLoader 依 assetId 自動掛上）。
/// 主要職責＝**死亡燃燒演出**：boss 被打敗時——臉先燒 1 秒 → 臉消失 → 各地陸續起火、越冒越多，最後鋪滿整棵樹、一直燒不停。
/// 由 <see cref="MonsterController"/>（死亡）呼叫 <see cref="PlayDeath"/>。手感常數在下方。
///
/// 註：`FlashAttacking`（發招換 crazy 臉）目前**未使用**——兩張臉素材搭不上，維持原本 vicious 臉即可。
/// 保留這段管線，日後有合適素材再由 brain 呼叫即可恢復。
/// </summary>
public class BanyanBossFace : MonoBehaviour
{
    // 場上唯一的榕樹妖臉（brain / MonsterController 透過這裡找到它）。
    public static BanyanBossFace Instance { get; private set; }

    // ── 死亡燃燒演出參數（要調就改這裡）──
    const int   FireVfxId       = 16;    // 紅色鬼火（VfxTable，本身就是循環動畫）
    const float FaceBurnTime    = 1.0f;  // 臉先燒多久，臉才消失
    const float FaceFireScale   = 1.0f;  // 臉上那團火的倍率（相對 VfxTable.Scale）
    const float TreeFireScale   = 1.0f;  // 樹上每團火的倍率
    const float FireLife        = -1f;   // 火焰壽命：<0 = 無限循環、永不熄滅（著火就一直燒）

    // ── 擴散式蔓延（不設固定上限；用網格鋪滿整棵樹，隨機順序點燃、節奏越來越快＝越冒越多，鋪滿即止）──
    const float FireSpacing         = 0.9f;  // 火點網格間距（越小＝越密、越多火，也越吃效能）
    const float FireJitter          = 0.5f;  // 每個火點的隨機位移，避免看起來像整齊網格
    const float SpreadStartInterval = 0.45f; // 剛開始每點火的間隔（慢）
    const float SpreadMinInterval   = 0.05f; // 加速到最快的間隔（快）
    const float SpreadAccel         = 0.88f; // 每點一次間隔乘這個（<1 → 越點越快＝越冒越多）

    // 樹的燃燒範圍（世界座標盒；以臉的位置為基準，涵蓋畫面上半的樹冠＋樹幹＋樹根，可調）。
    const float TreeAreaHalfW   = 6.0f;  // 以臉 x 為中心的左右半寬
    const float TreeAreaTop     = -0.3f; // 燃燒盒頂（世界 y，靠近地圖頂＝樹冠）
    const float TreeAreaBottom  = -4.0f; // 燃燒盒底（世界 y，可走區上緣＝樹根）

    SpriteRenderer _sr;
    Sprite _viciousSprite;
    Sprite _crazySprite;
    float  _crazyUntil;   // < Time.time 就恢復 vicious（0 = 現在就是 vicious）
    bool   _dying;

    /// <summary>MapLoader 建好臉地上物後呼叫：交出 SpriteRenderer 與兩張臉圖。</summary>
    public void Setup(SpriteRenderer sr, Sprite vicious, Sprite crazy)
    {
        Instance = this;
        _sr = sr;
        _viciousSprite = vicious;
        _crazySprite = crazy;
        if (_sr != null && _viciousSprite != null) _sr.sprite = _viciousSprite;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>發招：臉切 crazy，durationSeconds 後自動恢復 vicious（沒發招＝vicious）。重複呼叫會延長 crazy 時間。</summary>
    public void FlashAttacking(float durationSeconds)
    {
        if (_dying || _sr == null || _crazySprite == null) return;
        _crazyUntil = Mathf.Max(_crazyUntil, Time.time + durationSeconds);
        _sr.sprite = _crazySprite;
    }

    void Update()
    {
        if (_dying || _sr == null) return;
        if (_crazyUntil > 0f && Time.time >= _crazyUntil)
        {
            _crazyUntil = 0f;
            if (_viciousSprite != null) _sr.sprite = _viciousSprite;   // 沒發招 → 恢復 vicious
        }
    }

    /// <summary>boss 死亡演出：臉燒 1 秒 → 臉消失 → 各地陸續起火、越冒越多、鋪滿整棵樹、一直燒。只會跑一次。</summary>
    public void PlayDeath()
    {
        if (_dying) return;
        _dying = true;
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        var vfx = FindObjectOfType<VfxManager>();

        // 臉維持原本那張（不換 crazy）。臉的世界中心。
        Vector2 faceCenter = (_sr != null) ? (Vector2)_sr.bounds.center : (Vector2)transform.position;

        // ① 臉的位置先起火（持續火，不會停）。
        if (vfx != null) vfx.SpawnLoop(FireVfxId, faceCenter, FaceFireScale, FireLife);
        yield return new WaitForSeconds(FaceBurnTime);

        // ② 臉（地上物）消失（火繼續燒）。
        if (_sr != null) _sr.enabled = false;

        // ③ 各地陸續起火、越冒越多，最後鋪滿整棵樹：
        //    用網格把整棵樹的範圍切成火點 → 洗牌成隨機順序 → 逐點點燃、間隔越來越短（越冒越多）→ 鋪滿即止。
        float cx = faceCenter.x;
        float left = cx - TreeAreaHalfW, right = cx + TreeAreaHalfW;

        var cells = new List<Vector2>();
        for (float x = left; x <= right + 0.001f; x += FireSpacing)
            for (float y = TreeAreaBottom; y <= TreeAreaTop + 0.001f; y += FireSpacing)
                cells.Add(new Vector2(x, y));

        // Fisher-Yates 洗牌，讓點燃順序隨機（此起彼落）。
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector2 tmp = cells[i]; cells[i] = cells[j]; cells[j] = tmp;
        }

        float interval = SpreadStartInterval;
        foreach (var cell in cells)
        {
            Vector2 p = cell + new Vector2(Random.Range(-FireJitter, FireJitter), Random.Range(-FireJitter, FireJitter));
            if (vfx != null) vfx.SpawnLoop(FireVfxId, p, TreeFireScale, FireLife);
            yield return new WaitForSeconds(interval);
            interval = Mathf.Max(SpreadMinInterval, interval * SpreadAccel);   // 越點越快＝越冒越多
        }
        // 整棵樹鋪滿後不再新增，已點燃的持續燒。
    }
}
