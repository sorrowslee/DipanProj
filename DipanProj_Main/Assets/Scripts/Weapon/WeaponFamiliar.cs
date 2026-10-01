using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **浮游武器（<see cref="WeaponMode.Familiar"/>）的本體**：每一把浮游武器有 N 個本體繞著玩家轉，
/// 射程內有怪就「從本體的位置」朝最近的怪射一發一般子彈。不用按攻擊鍵。
///
/// ── 2026-10-01 起是「一個元件管多組」──
/// 浮游武器改成**被動武器**（見 <see cref="PassiveWeaponSet"/>、readme/PASSIVE_WEAPON.md）：
/// 護身符、戒指、血統都能各帶一把，所以玩家身上可能同時有好幾把。每一把＝一個 <see cref="Group"/>，
/// 自己的本體、冷卻、輪替、相位；本元件只負責把 <c>PlayerController.PassiveWeapons</c> 的清單對齊到群組上。
/// 群組對齊走「依序對齊」：清單第 i 把對第 i 組；外型（VfxId／數量／大小）沒變就不重生、冷卻沿用。
///
/// ── 分工 ──
/// ‧ **本元件**：本體的生死與擺位（每幀自己問 <c>PlayerController.PassiveWeapons</c>，
///   所以背包開著卸下裝備、進禁武地圖時也會立刻收掉）＋發射節奏（誰射、什麼時候射）＋索敵。
/// ‧ **PlayerController**：真的射出去那一下（扣魔、<c>WeaponCastService.FireNormal</c>、命中鏈 <c>HandleBulletHit</c>）。
///   這些是玩家專屬狀態，不搬進來——同 WeaponCastService 的 seam 原則。
///
/// ── 環繞看起來像「繞著轉」的三件事（與 <see cref="BloodlineOrbit"/> 同一套，見 readme/BLOODLINE.md）──
/// ① 軌道壓扁（<see cref="Flatten"/>）② 轉到身後排在角色之下、身前之上 ③ 遠近縮放（<see cref="DepthScale"/>）。
///
/// ── 錯開射擊（作者拍板：錯開射、都打最近的）──
/// 每個本體自己的冷卻 = <c>FireInterval</c>；另外同一組「任兩發之間」至少隔 <c>FireInterval ÷ 本體數</c>，
/// 本體輪流出手 ⇒ 全部冷卻好的時候是均勻的「噠、噠、噠」，而不是 N 顆同一幀齊射。
/// 每個本體的射速仍然是 1 ÷ FireInterval（疾發珠照常有感）。**不同組之間不互相錯開**（各自獨立）。
///
/// ── 多組同軌道不重疊 ──
/// 兩組同樣的水珠（護身符一顆、血統一顆）會在同一條軌道上，所以每組的相位加上
/// <c>組序 × 360 ÷ (本體數 × 組數)</c> 的偏移，四顆剛好均分一圈。
///
/// ⚠ 位置一律用 <c>PlayerController.BodyCenterWorldPos</c>，不要用 transform.position（PROBLEMS **E14**）。
/// ⚠ 本體那一列 VfxTable 必須 <c>Loop=1</c>、<c>Duration=-1</c>（生死由本元件負責）；它的 SortingOrder 對這裡不生效。
/// </summary>
[DisallowMultipleComponent]
public class WeaponFamiliar : MonoBehaviour
{
    [Header("軌道外觀（全浮游武器共用；數量／半徑／大小／轉速在 CSV）")]
    [Tooltip("俯視角壓扁比（Y/X）。1 = 正圓；0.3~0.45 才像水平繞行")]
    public float Flatten = 0.38f;
    [Tooltip("軌道中心相對「可見身體中心」的高度 = 角色高度 × 此值")]
    public float HeightRatio = 0.05f;
    [Tooltip("前後遠近的縮放差。0.25 = 最前面 1.25 倍、最後面 0.75 倍")]
    public float DepthScale = 0.25f;
    [Tooltip("上下浮動幅度（世界單位，會乘血統體型）")]
    public float Bob = 0.06f;
    public float BobHz = 0.5f;

    [Header("索敵")]
    [Tooltip("只打畫面內看得到的怪（避免對畫面外的怪空放、白白耗魔）")]
    public bool RequireOnScreen = true;
    [Tooltip("魔力不夠時隔多久再試一次（秒）")]
    public float ManaRetrySeconds = 0.25f;
    [Tooltip("射程內沒有怪時隔多久再找一次（秒）")]
    public float NoTargetRetrySeconds = 0.1f;

    PlayerController _pc;
    PlayerAnimator _anim;
    YSortByFeet _ysort;
    VfxManager _vfx;

    readonly List<Group> _groups = new List<Group>();
    static readonly List<WeaponData> Empty = new List<WeaponData>();

    /// <summary>目前有幾組（＝幾把浮游武器在運作）。</summary>
    public int GroupCount => _groups.Count;

    /// <summary>目前所有組加總有幾個本體（沒在運作＝0）。</summary>
    public int Count
    {
        get { int n = 0; for (int i = 0; i < _groups.Count; i++) n += _groups[i].Count; return n; }
    }

    void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _anim = GetComponent<PlayerAnimator>();
        _ysort = GetComponent<YSortByFeet>();
    }

    float BodyScale()
    {
        if (_pc == null || _pc.CharacterWorldHeight <= 0.01f) return 1f;
        return Mathf.Max(0.01f, _pc.ScaledCharacterHeight / _pc.CharacterWorldHeight);
    }

    Vector2 BodyCenter() => _pc != null ? _pc.BodyCenterWorldPos : (Vector2)transform.position;

    // ═══════════════════════════ 群組對齊 ═══════════════════════════

    /// <summary>
    /// 把「現在應該有的浮游武器清單」對齊到群組：第 i 把對第 i 組，多的組收掉、少的組補上。
    /// <paramref name="weapons"/> 傳 null 或空＝全部收掉。每幀呼叫都便宜：外型沒變只更新半徑／轉速。
    /// </summary>
    public void Sync(IReadOnlyList<WeaponData> weapons)
    {
        int n = weapons != null ? weapons.Count : 0;
        while (_groups.Count > n) { _groups[_groups.Count - 1].Clear(); _groups.RemoveAt(_groups.Count - 1); }
        while (_groups.Count < n) _groups.Add(new Group(this));
        float bs = BodyScale();
        for (int i = 0; i < n; i++) _groups[i].Sync(weapons[i], bs);
    }

    /// <summary>
    /// 推進所有組的發射節奏。由 <c>PlayerController.HandleFiring</c> 每幀呼叫（所以背包開著、教學鎖住時自然停火）。
    /// <paramref name="fire"/>（武器、出手點、目標）回傳「真的射出去了沒」；false＝魔力不夠。
    /// </summary>
    public void TickFiring(float dt, IReadOnlyList<WeaponData> weapons, Func<WeaponData, Vector2, MonsterController, bool> fire)
    {
        Sync(weapons);
        if (fire == null) return;
        for (int i = 0; i < _groups.Count; i++) _groups[i].TickFiring(dt, fire);
    }

    void LateUpdate()
    {
        // 生死自理：每幀問玩家「現在身上有哪些被動武器、能不能運作」。
        // （發射節奏只在 HandleFiring 裡推進；但收掉本體不能等它——背包開著時 HandleFiring 不會被呼叫。）
        Sync(_pc != null ? _pc.PassiveWeapons : Empty);
        if (_groups.Count == 0) return;

        if (_anim == null) _anim = GetComponent<PlayerAnimator>();
        // 趴著／倒下／爬起時整組藏起來（同血統特效三層）——那時身體幾何是趴姿，繞「站姿身體中心」會飄在半空。
        bool show = _anim == null || _anim.BodyFxVisible;

        float bs = BodyScale();
        float h = _pc != null ? _pc.ScaledCharacterHeight : 2f;
        Vector2 c = BodyCenter();
        c.y += h * HeightRatio;
        float baseY = transform.position.y + (_ysort != null ? _ysort.FeetYOffset : 0f);
        int charOrder = MapDepthSort.Order(baseY, 0);
        // Time.deltaTime：開背包／面板暫停時停下來，與其他戰鬥特效一致。
        float dt = Time.deltaTime;

        for (int g = 0; g < _groups.Count; g++)
            _groups[g].LateUpdate(g, _groups.Count, show, bs, c, charOrder, dt);
    }

    void ClearAll()
    {
        for (int i = 0; i < _groups.Count; i++) _groups[i].Clear();
        _groups.Clear();
    }

    void OnDisable() { ClearAll(); }
    void OnDestroy() { ClearAll(); }

    // ═══════════════════════════ 索敵（各組共用）═══════════════════════════

    /// <summary>
    /// 射程內離玩家最近、可以打的怪。射程從玩家身上量（<c>transform.position</c>＝碰撞位置，PROBLEMS **B13**）。
    /// 「可以打」＝活著、可控（不是劇情演員）、在玩家子彈打得到的層（結盟方被切到 Ally 層 ⇒ 自動排除，
    /// 陣營規則單一真相仍在 FactionRelations），並排除友軍召喚物與中立。
    /// </summary>
    MonsterController FindNearestTarget(WeaponData w)
    {
        float range = (w.Recipe != null && w.Recipe.Data != null && w.Recipe.Data.BeamRange > 0f)
            ? w.Recipe.Data.BeamRange : RecipeEntry.FamiliarDefaultRange;
        float best = range * range;
        Vector2 origin = transform.position;
        int enemyMask = _pc != null ? _pc.EnemyLayer.value : ~0;
        Camera cam = RequireOnScreen ? Camera.main : null;

        MonsterController pick = null;
        var all = MonsterController.Active;
        for (int i = 0; i < all.Count; i++)
        {
            var m = all[i];
            if (m == null || m.IsDead || !m.Controllable) continue;
            if (m.Faction == MonsterFaction.PlayerAlly || m.Faction == MonsterFaction.Neutral) continue;
            if (((1 << m.gameObject.layer) & enemyMask) == 0) continue;
            float d2 = ((Vector2)m.transform.position - origin).sqrMagnitude;
            if (d2 > best) continue;
            if (cam != null && !OnScreen(cam, m)) continue;
            best = d2;
            pick = m;
        }
        return pick;
    }

    static bool OnScreen(Camera cam, MonsterController m)
    {
        const float Margin = 0.02f;
        Vector3 vp = cam.WorldToViewportPoint(m.BodyCenterWorldPos);
        if (vp.z < 0f) return false;
        return vp.x >= Margin && vp.x <= 1f - Margin && vp.y >= Margin && vp.y <= 1f - Margin;
    }

    static float FireIntervalOf(WeaponData w)
        => Mathf.Max(0.02f, (w != null && w.Recipe != null && w.Recipe.Data != null) ? w.Recipe.Data.FireInterval : 1f);

    // ═══════════════════════════ 一把浮游武器＝一組 ═══════════════════════════

    /// <summary>一把浮游武器的本體、冷卻與輪替。邏輯與 2026-10-01 之前的單武器版完全相同，只是包成可多個。</summary>
    sealed class Group
    {
        readonly WeaponFamiliar _o;
        public WeaponData Weapon { get; private set; }

        // 目前這組本體的外型（任一項變了才重生）
        int _vfxId;
        int _count;
        float _height;          // 實際生成高度（FamiliarSize × 體型）——體型變了也會觸發重生
        // 不需重生、每幀直接套用的參數（工坊 Play 中改值立刻看到）
        float _radius = 1f;
        float _spinSpeed = 60f;

        VfxInstance[] _items;
        SpriteRenderer[] _srs;
        float[] _baseScale;
        float[] _cooldown;      // 每個本體距離能再射的秒數
        float _spin;
        float _clock;           // 只在可開火時前進（TickFiring 才會推）
        float _nextShotAt;      // 下一發（任何本體）最早可以在 _clock 的哪個時間
        int _nextIndex;         // 輪到哪個本體先問

        float _lastSpawnAt;
        int _quickRespawns;
        int _disabledVfxId;     // 被防呆停用的 VfxTable id：同一個 id 不再重試，換成別的 id（換武器／工坊改值）才會再生

        public Group(WeaponFamiliar owner) { _o = owner; }

        public int Count => _items != null ? _items.Length : 0;

        /// <summary>依這把武器同步本體。<paramref name="w"/> 為 null（或沒填 FamiliarVfxId）＝收掉。</summary>
        public void Sync(WeaponData w, float bodyScale)
        {
            Weapon = w;
            int id = (w != null && w.Recipe != null && w.Recipe.Mode == WeaponMode.Familiar) ? w.FamiliarVfxId : 0;
            if (id > 0 && id == _disabledVfxId) id = 0;   // 這個外觀已被防呆停用 ⇒ 當成沒有本體
            if (id <= 0)
            {
                if (_vfxId != 0 || _items != null) { _vfxId = 0; Clear(); }
                return;
            }

            int n = Mathf.Clamp(w.Recipe.Data != null ? w.Recipe.Data.OrbitalCount : 1, 1, 16);
            float height = Mathf.Max(0.02f, w.FamiliarSize * bodyScale);
            _radius = Mathf.Max(0.05f, (w.Recipe.Data != null ? w.Recipe.Data.OrbitalRadius : 1f) * bodyScale);
            _spinSpeed = w.FamiliarSpin;

            bool same = id == _vfxId && n == _count && Mathf.Abs(height - _height) < 0.001f && _items != null;
            if (same) return;

            bool idChanged = id != _vfxId;
            _vfxId = id;
            _height = height;
            if (idChanged) _quickRespawns = 0;
            Rebuild(n, w);
        }

        void Rebuild(int n, WeaponData w)
        {
            // 冷卻沿用：數量變多時新的本體從「錯開的位置」起算，不會一裝珠子就整排同時開火
            float interval = FireIntervalOf(w);
            var oldCd = _cooldown;
            Clear();
            _count = n;
            if (_vfxId <= 0 || !_o.isActiveAndEnabled) return;
            if (_o._vfx == null) _o._vfx = UnityEngine.Object.FindObjectOfType<VfxManager>();
            if (_o._vfx == null) { _count = 0; return; }   // 場景還沒就緒 → 下一幀 Sync 再試

            Vector2 c = _o.BodyCenter();
            _items = new VfxInstance[n];
            _srs = new SpriteRenderer[n];
            _baseScale = new float[n];
            _cooldown = new float[n];
            for (int i = 0; i < n; i++)
            {
                var inst = _o._vfx.SpawnLoopSizedToHeight(_vfxId, c, _height, -1f);
                if (inst == null)
                {
                    Debug.LogWarning($"[WeaponFamiliar] VfxTable {_vfxId} 生不出來（ID 不存在或沒有圖？），浮游本體停用。");
                    _disabledVfxId = _vfxId;
                    Clear(); _vfxId = 0; return;
                }
                inst.transform.SetParent(_o.transform, true);   // 跟著玩家走（位置每幀仍自己算）
                _items[i] = inst;
                _srs[i] = inst.GetComponent<SpriteRenderer>();
                _baseScale[i] = inst.transform.localScale.x;
                _cooldown[i] = (oldCd != null && i < oldCd.Length) ? oldCd[i] : interval * i / n;
            }
            _lastSpawnAt = Time.unscaledTime;
        }

        public void TickFiring(float dt, Func<WeaponData, Vector2, MonsterController, bool> fire)
        {
            var w = Weapon;
            int n = Count;
            if (n == 0 || w == null) return;

            _clock += dt;
            for (int i = 0; i < n; i++) if (_cooldown[i] > 0f) _cooldown[i] -= dt;
            if (_clock < _nextShotAt) return;

            // 輪流：從 _nextIndex 開始找第一個冷卻好的
            int pick = -1;
            for (int k = 0; k < n; k++)
            {
                int idx = (_nextIndex + k) % n;
                if (_cooldown[idx] <= 0f) { pick = idx; break; }
            }
            if (pick < 0) return;

            MonsterController target = _o.FindNearestTarget(w);
            if (target == null) { _nextShotAt = _clock + _o.NoTargetRetrySeconds; return; }   // 冷卻不扣：怪一進來馬上射

            Vector2 origin = _items[pick] != null ? (Vector2)_items[pick].transform.position : _o.BodyCenter();
            if (fire(w, origin, target))
            {
                float interval = FireIntervalOf(w);
                _cooldown[pick] = interval;
                _nextShotAt = _clock + interval / n;   // 相鄰兩發的最小間隔 ⇒ N 個本體平均錯開
                _nextIndex = (pick + 1) % n;
            }
            else
            {
                _nextShotAt = _clock + _o.ManaRetrySeconds;   // 魔不夠：稍後再試，不要每幀嘗試
            }
        }

        /// <summary>擺位。<paramref name="groupIndex"/>／<paramref name="groupCount"/> 用來算多組同軌道的相位偏移。</summary>
        public void LateUpdate(int groupIndex, int groupCount, bool show, float bs, Vector2 c, int charOrder, float dt)
        {
            if (_vfxId <= 0) return;

            if (_items == null || _items.Length == 0 || _items[0] == null)
            {
                // 同 BloodlineOrbit 的防呆：VfxTable 沒填 Duration=-1 的話會變成每幀重生一整組。
                if (Time.unscaledTime - _lastSpawnAt < 1f && ++_quickRespawns >= 3)
                {
                    Debug.LogWarning($"[WeaponFamiliar] VfxTable {_vfxId} 生出來馬上就消失，浮游本體停用。" +
                                     "本體那一列必須 Loop=1 且 Duration=-1（見 readme/VFX.md）。");
                    _disabledVfxId = _vfxId;   // 同一個 id 不再重試；換武器（id 不同）才會再試
                    Clear();
                    _vfxId = 0;
                    return;
                }
                Clear();
                return;   // 下一幀 Sync 會重生
            }

            if (!show)
            {
                for (int i = 0; i < _srs.Length; i++) if (_srs[i] != null) _srs[i].enabled = false;
                return;
            }

            float r = _radius;
            _spin += _spinSpeed * dt;
            int n = _items.Length;
            // 多組同軌道：每組錯開 360 ÷ (本體數 × 組數)，讓所有組的本體均分一圈
            float phase = groupCount > 1 ? groupIndex * (360f / (n * groupCount)) : 0f;
            for (int i = 0; i < n; i++)
            {
                if (_items[i] == null) { Clear(); return; }
                float ang = (_spin + phase + i * (360f / n)) * Mathf.Deg2Rad;
                float sn = Mathf.Sin(ang);          // +1 = 最後方（畫面上）、-1 = 最前方（畫面下）
                float bob = _o.Bob * bs * Mathf.Sin((Time.time * _o.BobHz + (i + groupIndex * 0.5f) * 0.37f) * Mathf.PI * 2f);
                _items[i].transform.position = new Vector3(c.x + Mathf.Cos(ang) * r, c.y + sn * r * _o.Flatten + bob, 0f);

                float s = _baseScale[i] * (1f - sn * _o.DepthScale);
                _items[i].transform.localScale = new Vector3(s, s, 1f);

                if (_srs[i] != null)
                {
                    _srs[i].enabled = true;
                    // ±3（血統環繞層用 ±2）：應龍血統拿水球武器時，武器本體壓在血統水球之上
                    _srs[i].sortingOrder = charOrder + (sn > 0f ? -3 : 3);
                }
            }
        }

        public void Clear()
        {
            if (_items != null)
                for (int i = 0; i < _items.Length; i++)
                    if (_items[i] != null) UnityEngine.Object.Destroy(_items[i].gameObject);
            _items = null;
            _srs = null;
            _baseScale = null;
            _cooldown = null;
            _count = 0;
        }
    }
}
