using UnityEngine;

/// <summary>
/// 第三階血統的「隨從」——一隻停在宿主肩膀上的小動物，不定時起飛繞宿主一圈再飛回肩上；宿主倒下時飛走淡出。
/// 目前只有墨子（132）的木頭機關鷹（表B `FamiliarStyle` = <c>MoziEagle</c>，2026-11-24）。
/// 見 readme/BLOODLINE.md §5b〈`FamiliarStyle`：隨從〉。
///
/// ── 資料 ──
/// ‧ 外觀與行為參數：Assets/Data/FamiliarTable.csv（<see cref="FamiliarTable"/>）。
/// ‧ 宿主每個動作、每一幀的停棲點：Assets/Data/FamiliarAnchorTable.csv（座標慣例同影子錨點：X 相對畫布中心、Y 從畫布底，來源圖像素）。
/// ‧ 序列圖：GameAssets/Main/Characters/SequenceImage/&lt;Folder&gt;/{idle,takeoff,fly,glide,land}/（走 PlayerSpriteLibrary，同步工具會收）。
///
/// ── 對位 ──
/// 停棲點換成世界座標的算法照抄 <see cref="BlobShadow"/>：用宿主**當下那張 sprite** 的 rect／pivot／PPU 與 lossyScale、flipX 換算，
/// 所以 BodyScale、walk／attack 的體積正規化、翻面全部自動跟上，不必另外補。
/// 隨從自己的爪子位置（FootIdle／FootTakeoff／FootLand）也用它自己 sprite 的 pivot 換算，讓「爪子」而不是「圖中心」踩在肩上。
///
/// ── 一輪 ──
/// 停棲（idle 循環，WaitMin～WaitMax 秒）→ 起飛（takeoff：TakeoffLift 幀前爪子還釘在肩上，之後沿曲線飛到繞圈起點，播到 TakeoffEnd）
/// → 繞宿主頭頂一圈（扁橢圓；後半圈畫在宿主後面、前半圈畫在前面；GlideFrom～GlideTo 那段改播 glide）
/// → 降落（land：飛回肩上，LandTouch 幀起爪子釘回肩膀）→ 停棲。
/// 宿主倒下（死亡、變身倒地）→ 往後上方飛走、FlyAwayTime 秒淡出；宿主站起來後直接停回肩上。
/// 主角被藏起來（劇情 hidePlayer 等）→ 立刻隱藏。
///
/// ⚠ 獨立 GameObject、不是玩家子物件（同 BloodlineGhost／Mist：子物件會被角色 localScale 二次影響）。
/// </summary>
[DisallowMultipleComponent]
public class BloodlineFamiliar : MonoBehaviour
{
    enum Phase { Gone, Perched, Takeoff, Lap, Land, FlyAway }
    enum Clip { Idle = 0, Takeoff = 1, Fly = 2, Glide = 3, Land = 4 }
    static readonly string[] ClipNames = { "idle", "takeoff", "fly", "glide", "land" };

    string _style;
    string _hostFolder;
    FamiliarDef _def;

    PlayerController _pc;
    PlayerAnimator _anim;
    SpriteRenderer _hostSr;

    GameObject _go;
    SpriteRenderer _sr;
    readonly Sprite[][] _clips = new Sprite[5][];
    float _idleVisH = 0.5f;   // idle 第一幀的可見高（世界單位 @ tileSize 1）
    bool _built;

    Phase _phase = Phase.Gone;
    float _phaseT;
    float _wait;
    Clip _clip;
    float _clipT;
    bool _faceRight = true;
    int _lapDir = 1;
    float _theta0;
    Vector2 _fromPos;          // 起飛／降落那段曲線的起點（transform 位置）
    Vector2 _flyVel;

    void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _anim = GetComponent<PlayerAnimator>();
        _hostSr = GetComponent<SpriteRenderer>();
        if (_hostSr == null) _hostSr = GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>設定樣式（FamiliarTable 的 Key）與宿主的 SpriteFolder；留空 = 關掉這一層。重複傳同樣的值不會重建。</summary>
    public void SetStyle(string style, string hostFolder)
    {
        style = string.IsNullOrWhiteSpace(style) ? null : style.Trim();
        hostFolder = (hostFolder ?? "").Trim();
        if (style == null) { _style = null; Clear(); return; }
        if (style == _style && hostFolder == _hostFolder && _built) return;
        Clear();
        _style = style;
        _hostFolder = hostFolder;
        Build();
    }

    void Build()
    {
        _built = true;
        if (!FamiliarTable.Instance.TryGetDef(_style, out _def))
        {
            Debug.LogWarning($"[BloodlineFamiliar] FamiliarTable 找不到「{_style}」，隨從不顯示。");
            _def = null;
            return;
        }
        var lib = PlayerSpriteLibrary.Instance;
        for (int i = 0; i < ClipNames.Length; i++)
            _clips[i] = lib.GetFrames(_def.Folder, ClipNames[i], 1f, 1f);
        if (_clips[(int)Clip.Idle] == null || _clips[(int)Clip.Idle].Length == 0)
        {
            Debug.LogWarning($"[BloodlineFamiliar] 「{_def.Folder}/idle」沒有圖（序列圖放了嗎？跑過 Sync Map Assets 嗎？），隨從不顯示。");
            _def = null;
            return;
        }
        // 缺的動作退回能用的：glide→fly、fly→idle、takeoff／land→fly
        if (_clips[(int)Clip.Fly] == null) _clips[(int)Clip.Fly] = _clips[(int)Clip.Idle];
        if (_clips[(int)Clip.Glide] == null) _clips[(int)Clip.Glide] = _clips[(int)Clip.Fly];
        if (_clips[(int)Clip.Takeoff] == null) _clips[(int)Clip.Takeoff] = _clips[(int)Clip.Fly];
        if (_clips[(int)Clip.Land] == null) _clips[(int)Clip.Land] = _clips[(int)Clip.Fly];

        if (lib.TryGetVisibleBox(_def.Folder, "idle", out var size, out _) && size.y > 0.001f) _idleVisH = size.y;

        _go = new GameObject("BloodlineFamiliar_" + _style);
        _go.transform.SetParent(transform.parent, false);
        _sr = _go.AddComponent<SpriteRenderer>();
        if (_hostSr != null)
        {
            _sr.sortingLayerID = _hostSr.sortingLayerID;
            _sr.sharedMaterial = _hostSr.sharedMaterial;   // 跟宿主同一個材質（同樣吃場景的角色光照／融合）
        }
        _sr.enabled = false;
        _phase = Phase.Gone;
    }

    // ───────────────────────────── 每幀 ─────────────────────────────

    void LateUpdate()
    {
        if (_style == null || _def == null || _sr == null) return;
        if (_anim == null) _anim = GetComponent<PlayerAnimator>();
        if (_pc == null || _anim == null || _hostSr == null) return;

        if (Dipan.Cutscene.PlayerVisibility.IsHidden)
        {
            _sr.enabled = false;
            _phase = Phase.Gone;   // 重新現身時直接停在肩上
            return;
        }

        float dt = Time.deltaTime;
        bool hostDown = _pc.IsDead || _anim.CurrentGeomState == PlayerAnimator.State.Dead || _anim.IsWakeUpBusy;
        bool hostRight = _hostSr.flipX != _pc.SpriteSourceFacesRight;
        float h = _pc.ScaledCharacterHeight;
        float baseK = h * _def.Size / Mathf.Max(0.001f, _idleVisH);
        int hostOrder = _hostSr.sortingOrder;

        if (hostDown)
        {
            if (_phase == Phase.Perched || _phase == Phase.Takeoff || _phase == Phase.Lap || _phase == Phase.Land)
                BeginFlyAway(hostRight, h);
        }
        else if (_phase == Phase.Gone)
        {
            _phase = Phase.Perched;
            _phaseT = 0f;
            _wait = Random.Range(Mathf.Min(1.5f, _def.WaitMin), Mathf.Max(1.5f, _def.WaitMin));   // 剛現身先停一下就好
            SetClip(Clip.Idle);
        }

        _phaseT += dt;
        _clipT += dt;

        switch (_phase)
        {
            case Phase.Gone:
                _sr.enabled = false;
                return;

            case Phase.Perched:
            {
                _faceRight = hostRight;
                PlacePinned(PerchWorld(h, hostRight), Clip.Idle, CurrentFrame(true), baseK, _def.FootIdle);
                _sr.sortingOrder = hostOrder + 1;
                _wait -= dt;
                if (_wait <= 0f) BeginTakeoff(hostRight, h);
                break;
            }

            case Phase.Takeoff:
            {
                int f = CurrentFrame(false);
                int end = _def.TakeoffEnd >= 0 ? Mathf.Min(_def.TakeoffEnd, ClipLen(Clip.Takeoff) - 1) : ClipLen(Clip.Takeoff) - 1;
                int lift = Mathf.Min(_def.TakeoffLift, end);
                float k = baseK * _def.ScaleTakeoff;
                if (f < lift)
                {
                    PlacePinned(PerchWorld(h, hostRight), Clip.Takeoff, f, k, FootAt(_def.FootTakeoff, f, _def.FootIdle));
                    _fromPos = _go.transform.position;
                }
                else
                {
                    float dur = (end - lift + 1) / _def.Fps;
                    float u = Mathf.Clamp01((_clipT - lift / _def.Fps) / Mathf.Max(0.01f, dur));
                    Vector2 to = LapPos(_theta0, h, Clip.Takeoff, f, k);
                    ApplySprite(Clip.Takeoff, f, k);
                    _go.transform.position = Vector2.Lerp(_fromPos, to, Smooth(u));
                }
                _sr.sortingOrder = hostOrder + 1;
                if (f > end || (f == end && _clipT >= (end + 1) / _def.Fps)) BeginLap();
                break;
            }

            case Phase.Lap:
            {
                float p = _phaseT / _def.LapTime;
                if (p >= 1f) { BeginLand(); goto case Phase.Land; }
                float theta = _theta0 + p * Mathf.PI * 2f * _lapDir;
                bool glide = p >= _def.GlideFrom && p < _def.GlideTo;
                Clip c = glide ? Clip.Glide : Clip.Fly;
                if (_clip != c) SetClip(c);
                float vx = -Mathf.Sin(theta) * _lapDir;   // 橢圓切線的 X 分量（只看正負）
                if (Mathf.Abs(vx) > 0.15f) _faceRight = vx > 0f;
                float k = baseK * (glide ? _def.ScaleGlide : _def.ScaleFly);
                int f = CurrentFrame(true);
                ApplySprite(c, f, k);
                _go.transform.position = LapPos(theta, h, c, f, k);
                // 上半圈＝離鏡頭遠的那半圈 → 畫在宿主後面
                _sr.sortingOrder = Mathf.Sin(theta) > 0f ? hostOrder - 1 : hostOrder + 1;
                break;
            }

            case Phase.Land:
            {
                int f = CurrentFrame(false);
                int last = ClipLen(Clip.Land) - 1;
                int touch = Mathf.Min(_def.LandTouch, last);
                float k = baseK * _def.ScaleLand;
                Vector2 perch = PerchWorld(h, hostRight);
                Vector2 foot = FootAt(_def.FootLand, f, _def.FootIdle);
                if (f < touch)
                {
                    // 還在空中：從繞圈終點飛向「碰到肩膀那一幀會在的位置」
                    Vector2 pinned = PinnedPos(perch, Clip.Land, touch, k, FootAt(_def.FootLand, touch, _def.FootIdle), hostRight);
                    float u = Mathf.Clamp01(_clipT / Mathf.Max(0.01f, touch / _def.Fps));
                    if (Mathf.Abs(pinned.x - _go.transform.position.x) > 0.02f) _faceRight = pinned.x > _go.transform.position.x;
                    ApplySprite(Clip.Land, f, k);
                    _go.transform.position = Vector2.Lerp(_fromPos, pinned, Smooth(u));
                }
                else
                {
                    _faceRight = hostRight;
                    PlacePinned(perch, Clip.Land, f, k, foot);
                }
                _sr.sortingOrder = hostOrder + 1;
                if (f >= last && _clipT >= (last + 1) / _def.Fps)
                {
                    _phase = Phase.Perched;
                    _phaseT = 0f;
                    _wait = Random.Range(_def.WaitMin, Mathf.Max(_def.WaitMin, _def.WaitMax));
                    SetClip(Clip.Idle);
                }
                break;
            }

            case Phase.FlyAway:
            {
                float u = _phaseT / _def.FlyAwayTime;
                if (u >= 1f) { _phase = Phase.Gone; _sr.enabled = false; _sr.color = Color.white; return; }
                float k = baseK * _def.ScaleFly;
                ApplySprite(Clip.Fly, CurrentFrame(true), k);
                _go.transform.position += (Vector3)(_flyVel * dt);
                _sr.color = new Color(1f, 1f, 1f, 1f - u * u);
                _sr.sortingOrder = hostOrder + 1;
                break;
            }
        }
        _sr.enabled = true;
    }

    // ───────────────────────────── 狀態切換 ─────────────────────────────

    void BeginTakeoff(bool hostRight, float h)
    {
        _phase = Phase.Takeoff;
        _phaseT = 0f;
        SetClip(Clip.Takeoff);
        _faceRight = hostRight;
        _lapDir = hostRight ? 1 : -1;
        // 繞圈起點：橢圓上「最靠近肩膀」的那個角度（肩膀在橢圓裡面，沿中心連線往外推到橢圓上）
        Vector2 c = LapCenter(h);
        Vector2 s = PerchWorld(h, hostRight);
        float rx = Mathf.Max(0.01f, _def.LapRadiusX * h), ry = Mathf.Max(0.01f, _def.LapRadiusY * h);
        _theta0 = Mathf.Atan2((s.y - c.y) / ry, (s.x - c.x) / rx);
        _fromPos = _go.transform.position;
    }

    void BeginLap()
    {
        _phase = Phase.Lap;
        _phaseT = 0f;
        SetClip(Clip.Fly);
    }

    void BeginLand()
    {
        _phase = Phase.Land;
        _phaseT = 0f;
        SetClip(Clip.Land);
        _fromPos = _go.transform.position;
    }

    void BeginFlyAway(bool hostRight, float h)
    {
        _phase = Phase.FlyAway;
        _phaseT = 0f;
        SetClip(Clip.Fly);
        _faceRight = !hostRight;   // 往宿主背後飛走
        _flyVel = new Vector2((_faceRight ? 1f : -1f) * 0.9f * h, 1.1f * h);
    }

    void SetClip(Clip c) { _clip = c; _clipT = 0f; }

    // ───────────────────────────── 幀與位置 ─────────────────────────────

    int ClipLen(Clip c) => _clips[(int)c] != null ? _clips[(int)c].Length : 0;

    int CurrentFrame(bool loop)
    {
        int n = Mathf.Max(1, ClipLen(_clip));
        int f = Mathf.FloorToInt(_clipT * _def.Fps);
        return loop ? f % n : Mathf.Min(f, n - 1);
    }

    static float Smooth(float u) => u * u * (3f - 2f * u);

    static Vector2 FootAt(Vector2[] list, int f, Vector2 fallback)
        => (list != null && list.Length > 0) ? list[Mathf.Clamp(f, 0, list.Length - 1)] : fallback;

    Sprite SpriteAt(Clip c, int f)
    {
        var arr = _clips[(int)c];
        if (arr == null || arr.Length == 0) return null;
        return arr[Mathf.Clamp(f, 0, arr.Length - 1)];
    }

    void ApplySprite(Clip c, int f, float k)
    {
        var sp = SpriteAt(c, f);
        if (sp != null && _sr.sprite != sp) _sr.sprite = sp;
        _sr.flipX = !_faceRight;   // 來源圖朝右
        _go.transform.localScale = new Vector3(k, k, 1f);
    }

    /// <summary>來源圖上某個像素點（X 相對畫布中心、Y 從畫布底）相對 transform 的世界位移（已含縮放與翻面）。</summary>
    Vector2 LocalOf(Sprite sp, Vector2 px, float k, bool faceRight)
    {
        if (sp == null || sp.pixelsPerUnit <= 0.0001f) return Vector2.zero;
        Rect r = sp.rect;
        float lx = (px.x + r.width * 0.5f - sp.pivot.x) / sp.pixelsPerUnit;
        float ly = (px.y - sp.pivot.y) / sp.pixelsPerUnit;
        return new Vector2(lx * k * (faceRight ? 1f : -1f), ly * k);
    }

    /// <summary>爪子踩在 <paramref name="perch"/> 時，transform 該在哪。</summary>
    Vector2 PinnedPos(Vector2 perch, Clip c, int f, float k, Vector2 footPx, bool faceRight)
        => perch - LocalOf(SpriteAt(c, f), footPx, k, faceRight);

    void PlacePinned(Vector2 perch, Clip c, int f, float k, Vector2 footPx)
    {
        ApplySprite(c, f, k);
        _go.transform.position = PinnedPos(perch, c, f, k, footPx, _faceRight);
    }

    Vector2 LapCenter(float h) => _pc.FeetWorldPos + new Vector2(0f, _def.LapHeight * h);

    /// <summary>繞圈路徑上 θ 那一點：讓**圖的畫布中心**落在橢圓上（飛行圖的鳥大致畫在畫布中央）。</summary>
    Vector2 LapPos(float theta, float h, Clip c, int f, float k)
    {
        Vector2 p = LapCenter(h) + new Vector2(Mathf.Cos(theta) * _def.LapRadiusX * h, Mathf.Sin(theta) * _def.LapRadiusY * h);
        var sp = SpriteAt(c, f);
        if (sp == null) return p;
        Vector2 centerLocal = LocalOf(sp, Vector2.zero + new Vector2(0f, sp.rect.height * 0.5f), k, _faceRight);
        return p - centerLocal;
    }

    /// <summary>
    /// 宿主肩膀（停棲點）的世界座標。查 FamiliarAnchorTable「宿主 SpriteFolder／目前動作」那一列的第 N 幀；
    /// 沒有該動作的列就用 idle 的第 1 組；連 idle 都沒有 → 退回「身體中心往後上方」的估計值。換算照抄 BlobShadow。
    /// </summary>
    Vector2 PerchWorld(float h, bool hostRight)
    {
        Vector2 offset = new Vector2(_def.PerchOffsetX * h * (hostRight ? 1f : -1f), _def.PerchOffsetY * h);
        var st = _anim.CurrentAnimState;
        string name = st == PlayerAnimator.State.Walk ? "walk" : st == PlayerAnimator.State.Attack ? "attack" : "idle";
        int idx = _anim.CurrentFrameIndex;
        var table = FamiliarTable.Instance;
        if (!table.TryGetAnchors(_hostFolder, name, out var a))
        {
            idx = 0;
            if (!table.TryGetAnchors(_hostFolder, "idle", out a)) a = null;
        }
        var sp = _hostSr.sprite;
        if (a == null || a.Points == null || a.Points.Length == 0 || sp == null || sp.pixelsPerUnit <= 0.0001f)
            return _pc.BodyCenterWorldPos + new Vector2((hostRight ? -0.12f : 0.12f) * h, 0.28f * h) + offset;

        Vector2 pt = a.Points[Mathf.Clamp(idx, 0, a.Points.Length - 1)];
        Rect r = sp.rect;
        float sx = a.CanvasW > 0 ? r.width / a.CanvasW : 1f;
        float sy = a.CanvasH > 0 ? r.height / a.CanvasH : 1f;
        float localX = (pt.x * sx + r.width * 0.5f - sp.pivot.x) / sp.pixelsPerUnit;
        float localY = (pt.y * sy - sp.pivot.y) / sp.pixelsPerUnit;
        Vector3 ls = transform.lossyScale;
        float flip = _hostSr.flipX ? -1f : 1f;
        Vector3 p = transform.position;
        return new Vector2(p.x + localX * ls.x * flip, p.y + localY * ls.y) + offset;
    }

    // ───────────────────────────── 清理 ─────────────────────────────

    void Clear()
    {
        if (_go != null) Destroy(_go);
        _go = null;
        _sr = null;
        for (int i = 0; i < _clips.Length; i++) _clips[i] = null;
        _def = null;
        _built = false;
        _phase = Phase.Gone;
    }

    void OnDisable() { if (_go != null) _go.SetActive(false); }
    void OnEnable() { if (_go != null) _go.SetActive(true); }
    void OnDestroy() { if (_go != null) Destroy(_go); }
}
