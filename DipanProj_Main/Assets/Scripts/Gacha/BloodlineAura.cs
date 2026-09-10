using UnityEngine;

/// <summary>
/// 第三階血統的「渾身環繞特效」——把一顆無限循環的 Vfx 掛在玩家身上跟著走（電光／能量光環那一層）。
///
/// 生法完全沿用血統變身演出（<c>BloodlineTransformFxRunner</c>）已經驗證過的三行：
/// <c>VfxManager.SpawnLoopSizedToHeight</c> 生一顆 Loop=1 / Duration=-1 的特效 → SetParent 到玩家底下。
/// 差別只有兩點，都是「短演出可以不管、常駐就會露餡」的事：
///
/// 1. **生死由本元件負責。** Duration=-1 的特效永不自毀，一定要有人清（見 readme/VFX.md 的警告）。
///    變身那邊是演出結束就 Destroy，這裡是換血統／體型變更／玩家消失時才清。
/// 2. **排序每幀接管。** VfxTable 的 SortingOrder 是表演層的固定值（變身電弧 22050），
///    短演出無所謂；常駐就會變成「玩家走到柱子後面、電光還飄在柱子前面」。
///    這裡改成與角色**同一條 Y 排序算式** + <see cref="SortOffset"/>，與角色同進退（見 MapDepthSort）。
///    ⚠ 所以 VfxTable 那列的 SortingOrder 對這個用途不生效，只是沒被接管時的退路。
///
/// ⚠ 位置一律用 <c>PlayerController.BodyCenterWorldPos</c>，**不要用 transform.position**
///   （那是畫布中心，體型放大後會沉到腳踝附近）。見 readme/PROBLEMS.md **E14**。
///
/// 掛法：由 <c>BloodlineSystem</c> 在套用血統時 AddComponent 並 <see cref="SetEffect"/>；
/// 血統表 <c>AuraVfxId</c> 留空 = 傳 0 = 這一層整個關掉。
/// </summary>
[DisallowMultipleComponent]
public class BloodlineAura : MonoBehaviour
{
    /// <summary>特效覆蓋高度 = 角色高度 × 此倍率。沿用變身演出的 1.25——電弧要比身體大一圈才包得住。</summary>
    public float HeightRatio = 1.25f;

    /// <summary>相對角色的排序偏移。+1 = 畫在角色之上（電光罩在身上）。</summary>
    public int SortOffset = 1;

    /// <summary>
    /// **間歇播放**：播完一輪之後隔多久再播一次（秒）。**0 = 不間歇**（維持無限循環）。
    ///
    /// 常駐電弧一直不停地閃會太吵、也把角色本身蓋掉（該隱那次退回的原因之一）。
    /// 間歇的作法是「播一輪就讓它自毀，等這段空檔過去再生一顆新的」——
    /// 刻意**不是**把 renderer 關掉再打開：那樣動畫會在隱藏期間繼續跑，再現身時是從播到一半的地方接，
    /// 看起來像卡了一下。重生才會每次都從第 0 幀開始。
    ///
    /// 「一輪多長」是從 VfxTable 算的（張數 ÷ AnimFPS），所以改了表格的幀率這裡自動跟上。
    /// </summary>
    public float PulseGap = 2.5f;

    int _vfxId;
    VfxInstance _inst;
    SpriteRenderer _instSr;
    PlayerController _pc;
    PlayerAnimator _anim;
    YSortByFeet _ysort;
    VfxManager _vfx;

    // 防呆：VfxTable 那一列若忘了填 Loop=1 / Duration=-1，特效會自己播完消失，
    // 而下面的 LateUpdate 看到 _inst == null 就補生一顆 —— 兩者湊起來 = 每幀生一顆特效。
    // 所以「生出來馬上就沒了」連續幾次就停用這一層並印一次警告，不讓它無聲地拖垮效能。
    float _lastSpawnAt;
    int _quickRespawns;
    float _nextSpawnAt;         // 間歇模式：這個時間點之後才生下一顆
    const int MaxQuickRespawns = 3;

    void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _anim = GetComponent<PlayerAnimator>();
        _ysort = GetComponent<YSortByFeet>();
    }

    /// <summary>設定要播的 VfxTable id；≤ 0 = 關掉這一層。重複傳同一個 id 不會重生。</summary>
    public void SetEffect(int vfxId)
    {
        if (vfxId <= 0) { _vfxId = 0; Clear(); return; }
        if (vfxId == _vfxId && _inst != null) return;
        _vfxId = vfxId;
        _quickRespawns = 0;
        Rebuild();
    }

    /// <summary>
    /// 重生一顆。**體型（BodyScale）改變後必須叫**——特效大小是生成當下依角色高度算死的，不會自己跟。
    /// 由 <c>PlayerController.RefreshBodyScaledVisuals()</c> 呼叫。
    /// </summary>
    public void Rebuild()
    {
        Clear();
        if (_vfxId <= 0 || !isActiveAndEnabled) return;

        if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();
        if (_vfx == null) return;          // 場景還沒就緒（載入中）→ 下一次 LateUpdate 再試

        float h = (_pc != null ? _pc.ScaledCharacterHeight : 2f) * Mathf.Max(0.01f, HeightRatio);
        Vector2 pos = _pc != null ? _pc.BodyCenterWorldPos : (Vector2)transform.position;

        // 間歇模式：只讓它活「一輪動畫」那麼久，播完自毀，空檔過了再生一顆新的（下面的 LateUpdate）。
        // 一輪多長直接從 VfxTable 算，改表格的幀率這裡自動跟上。
        float life = -1f;
        if (PulseGap > 0f)
        {
            float loop = LoopSeconds();
            life = loop;
            _nextSpawnAt = Time.time + loop + PulseGap;
        }

        _inst = _vfx.SpawnLoopSizedToHeight(_vfxId, pos, h, life);
        if (_inst == null) return;

        _inst.transform.SetParent(transform, true);   // 跟著玩家移動
        _instSr = _inst.GetComponent<SpriteRenderer>();
        _lastSpawnAt = Time.unscaledTime;

        // Unscaled 刻意維持預設 false：開背包／面板暫停時，這一層本來就該跟著停。
    }

    void LateUpdate()
    {
        if (_vfxId <= 0) return;

        if (_inst == null)
        {
            // 間歇模式：播完那一輪之後刻意留白，時間到了才生下一顆。
            if (PulseGap > 0f && Time.time < _nextSpawnAt) return;

            // 特效不在了：VfxManager 剛就緒、剛換過圖、或（非間歇模式下）表格沒填 Duration=-1。
            if (PulseGap <= 0f && Time.unscaledTime - _lastSpawnAt < 1f && ++_quickRespawns >= MaxQuickRespawns)
            {
                Debug.LogWarning($"[BloodlineAura] VfxTable {_vfxId} 生出來馬上就消失，已停用這一層。" +
                                 "常駐的環繞特效那一列必須 Loop=1 且 Duration=-1（見 readme/VFX.md）。");
                _vfxId = 0;
                return;
            }
            Rebuild();
            return;
        }

        if (_anim == null) _anim = GetComponent<PlayerAnimator>();

        // 趴著／倒下／爬起時整層關掉（同 BloodlineHalo）——變身演出本身就有自己的環繞電弧，
        // 兩層疊在一起也只是亂。見 PlayerAnimator.BodyFxVisible。
        if (_anim != null)
        {
            bool show = _anim.BodyFxVisible;
            if (_instSr != null) _instSr.enabled = show;
            if (!show) return;
        }

        // 位置：可見身體中心（趴著時比站著低），不是 transform.position。見 PROBLEMS E14。
        if (_pc != null) _inst.transform.position = _pc.BodyCenterWorldPos;

        // 排序：與角色同一條算式（YSortByFeet 用的也是這一條），+SortOffset 疊在角色之上。
        // 刻意不直接讀角色的 sortingOrder：那是別人的 LateUpdate 寫的，同為 LateUpdate 沒有順序保證。
        if (_instSr != null)
        {
            float baseY = transform.position.y + (_ysort != null ? _ysort.FeetYOffset : 0f);
            _instSr.sortingOrder = MapDepthSort.Order(baseY, 0) + SortOffset;
        }
    }

    /// <summary>一輪動畫多長（秒）＝ VfxTable 的張數 ÷ AnimFPS。查不到就回 1 秒。</summary>
    float LoopSeconds()
    {
        var data = _vfx != null ? _vfx.GetEffect(_vfxId) : null;
        if (data == null || data.AnimFPS <= 0f
            || data.AnimationSprites == null || data.AnimationSprites.Length == 0) return 1f;
        return data.AnimationSprites.Length / data.AnimFPS;
    }

    void Clear()
    {
        if (_inst != null) Destroy(_inst.gameObject);
        _inst = null;
        _instSr = null;
    }

    void OnDisable() { Clear(); }
    void OnDestroy() { Clear(); }
}
