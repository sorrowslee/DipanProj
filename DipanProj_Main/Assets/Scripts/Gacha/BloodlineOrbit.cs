using UnityEngine;

/// <summary>
/// 血統特效第四層：**身體周圍浮空繞行的物件**（泰坦的碎石；之後也能拿來做骨頭、符咒、冰晶…）。
///
/// 與另外三層的差別在「輪廓」：<c>BloodlineAura</c> 是**一整張**罩住身體的序列圖，
/// 這一層是**好幾個離散的小東西各自繞軌道**——所以它是唯一會出現「東西轉到角色背後又轉回前面」的層。
///
/// ── 三個讓它看起來是「繞著轉」而不是「貼在畫面上」的細節 ──
/// ① **軌道要壓扁**（<see cref="Flatten"/>）。俯視角看一個水平圓，投影出來是扁橢圓；不壓扁的話
///    石頭會沿著正圓上下跑，看起來像在角色臉前面畫圈。
/// ② **前後要換排序**（這是關鍵）。轉到畫面**上方**＝在角色身後 ⇒ 排序要低於角色；
///    轉到**下方**＝在角色身前 ⇒ 高於角色。沒有這一步就只是一圈貼在同一平面的東西。
/// ③ **遠近縮放**（<see cref="DepthScale"/>）。後面的縮小一點，前面的放大一點。
///
/// 排序與另外幾層同一條算式（見 <c>MapDepthSort</c>），所以石頭同樣會被柱子正確遮擋。
///
/// ⚠ 位置一律用 <c>PlayerController.BodyCenterWorldPos</c> ／ <c>ScaledCharacterHeight</c>，
///   不要用 transform.position（那是畫布中心）。見 readme/PROBLEMS.md **E14**。
/// ⚠ VfxTable 那一列必須 <c>Loop=1</c> 且 <c>Duration=-1</c>（常駐，生死由本元件負責）；
///   該列的 `SortingOrder` 對這一層不生效（每幀被接管）。
/// </summary>
[DisallowMultipleComponent]
public class BloodlineOrbit : MonoBehaviour
{
    [Header("軌道")]
    [Tooltip("幾個物件")]
    public int Count = 4;

    [Tooltip("軌道半徑 = 角色高度 × 此值")]
    public float RadiusRatio = 0.40f;

    [Tooltip("俯視角壓扁比（Y/X）。1 = 正圓（看起來像在臉前畫圈）、0.3~0.45 才像水平繞行")]
    public float Flatten = 0.38f;

    [Tooltip("軌道中心相對「可見身體中心」的高度 = 角色高度 × 此值")]
    public float HeightRatio = 0f;

    [Tooltip("轉速（度/秒，正 = 逆時針）。慢一點才有重量感")]
    public float Speed = 40f;

    [Header("外觀")]
    [Tooltip("單顆高度 = 角色高度 × 此值")]
    public float SizeRatio = 0.13f;

    [Tooltip("前後遠近的縮放差。0.25 = 最前面 1.25 倍、最後面 0.75 倍")]
    public float DepthScale = 0.25f;

    [Tooltip("上下浮動幅度 = 角色高度 × 此值（讓它像浮著而不是釘在軌道上）")]
    public float Bob = 0.025f;
    public float BobHz = 0.55f;

    int _vfxId;
    VfxInstance[] _items;
    SpriteRenderer[] _srs;
    float[] _baseScale;
    float _spin;

    PlayerController _pc;
    PlayerAnimator _anim;
    YSortByFeet _ysort;
    VfxManager _vfx;

    float _lastSpawnAt;
    int _quickRespawns;

    void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _anim = GetComponent<PlayerAnimator>();
        _ysort = GetComponent<YSortByFeet>();
    }

    /// <summary>設定要繞的 VfxTable id；≤ 0 = 關掉這一層。重複傳同一個 id 不會重生。</summary>
    public void SetEffect(int vfxId)
    {
        if (vfxId <= 0) { _vfxId = 0; Clear(); return; }
        if (vfxId == _vfxId && _items != null) return;
        _vfxId = vfxId;
        _quickRespawns = 0;
        Rebuild();
    }

    /// <summary>體型或顆數改變後重生一整組。由 <c>PlayerController.RefreshBodyScaledVisuals()</c> 呼叫。</summary>
    public void Rebuild()
    {
        Clear();
        if (_vfxId <= 0 || !isActiveAndEnabled) return;

        int n = Mathf.Clamp(Count, 1, 16);
        if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();
        if (_vfx == null) return;                 // 場景還沒就緒 → 下次 LateUpdate 再試

        float h = _pc != null ? _pc.ScaledCharacterHeight : 2f;
        Vector2 c = _pc != null ? _pc.BodyCenterWorldPos : (Vector2)transform.position;

        _items = new VfxInstance[n];
        _srs = new SpriteRenderer[n];
        _baseScale = new float[n];

        for (int i = 0; i < n; i++)
        {
            var inst = _vfx.SpawnLoopSizedToHeight(_vfxId, c, h * Mathf.Max(0.01f, SizeRatio), -1f);
            if (inst == null) { Clear(); return; }
            inst.transform.SetParent(transform, true);      // 跟著玩家走（位置每幀仍自己算）
            _items[i] = inst;
            _srs[i] = inst.GetComponent<SpriteRenderer>();
            _baseScale[i] = inst.transform.localScale.x;    // SpawnLoopSizedToHeight 設好的基準，之後乘遠近倍率
        }
        _lastSpawnAt = Time.unscaledTime;
    }

    void LateUpdate()
    {
        if (_vfxId <= 0) return;

        if (_items == null || _items.Length == 0 || _items[0] == null)
        {
            // 同 BloodlineAura 的防呆：表格沒填 Duration=-1 的話會變成每幀重生一整組。
            if (Time.unscaledTime - _lastSpawnAt < 1f && ++_quickRespawns >= 3)
            {
                Debug.LogWarning($"[BloodlineOrbit] VfxTable {_vfxId} 生出來馬上就消失，已停用這一層。" +
                                 "常駐特效那一列必須 Loop=1 且 Duration=-1（見 readme/VFX.md）。");
                _vfxId = 0;
                return;
            }
            Rebuild();
            return;
        }

        if (_anim == null) _anim = GetComponent<PlayerAnimator>();

        // 趴著／倒下／爬起時整組關掉（同另外三層）——那時身體幾何是趴姿，繞著「站姿身體中心」轉會飄在半空。
        bool show = _anim == null || _anim.BodyFxVisible;
        if (!show)
        {
            for (int i = 0; i < _srs.Length; i++) if (_srs[i] != null) _srs[i].enabled = false;
            return;
        }

        float h = _pc != null ? _pc.ScaledCharacterHeight : 2f;
        Vector2 c = _pc != null ? _pc.BodyCenterWorldPos : (Vector2)transform.position;
        c.y += h * HeightRatio;

        float r = h * Mathf.Max(0.01f, RadiusRatio);
        float baseY = transform.position.y + (_ysort != null ? _ysort.FeetYOffset : 0f);
        int charOrder = MapDepthSort.Order(baseY, 0);

        // 用 Time.deltaTime：開背包／面板暫停時停下來，與其他戰鬥特效一致。
        _spin += Speed * Time.deltaTime;

        int n = _items.Length;
        for (int i = 0; i < n; i++)
        {
            if (_items[i] == null) { Rebuild(); return; }

            float ang = (_spin + i * (360f / n)) * Mathf.Deg2Rad;
            float sn = Mathf.Sin(ang);          // +1 = 最後方（畫面上）、-1 = 最前方（畫面下）

            float bob = h * Bob * Mathf.Sin((Time.time * BobHz + i * 0.37f) * Mathf.PI * 2f);
            _items[i].transform.position = new Vector3(
                c.x + Mathf.Cos(ang) * r,
                c.y + sn * r * Flatten + bob,
                0f);

            // 遠近：前面大、後面小
            float depth = 1f - sn * DepthScale;
            float s = _baseScale[i] * depth;
            _items[i].transform.localScale = new Vector3(s, s, 1f);

            if (_srs[i] != null)
            {
                _srs[i].enabled = true;
                // 繞到身後（sn > 0）畫在角色之下、繞到身前畫在之上——這一步才讓它像「繞著轉」。
                _srs[i].sortingOrder = charOrder + (sn > 0f ? -2 : 2);
            }
        }
    }

    void Clear()
    {
        if (_items != null)
            for (int i = 0; i < _items.Length; i++)
                if (_items[i] != null) Destroy(_items[i].gameObject);
        _items = null;
        _srs = null;
        _baseScale = null;
    }

    void OnDisable() { Clear(); }
    void OnDestroy() { Clear(); }
}
