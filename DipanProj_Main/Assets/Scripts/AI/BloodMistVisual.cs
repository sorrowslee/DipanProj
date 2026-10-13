using UnityEngine;

/// <summary>
/// 血霧的畫面層（shader <c>Resources/Shaders/BloodMist</c>＝Boss 開戰黑霧 <c>UI/BossAura</c> 的世界空間紅色版，
/// 共用 BossAura 的兩張煙霧密度圖）。由 <see cref="BloodMistBrain"/> 掛上並控制。
/// <para>⚠ **整段不用序列圖**（2026-10-13 作者拍板）：化霧＝idle 站著、這團霧由下往上湧出蓋住全身、本體在霧底下淡掉；
/// 現身＝本體在霧底下淡回來、霧被吹散。第一版用過 mist 序列圖，作者嫌醜而移除。</para>
///
/// <para>只管兩件「看得到的事」，兩者都是**目標值＋固定速度逼近**（Brain 只說要不要，不管過程）：</para>
/// <list type="bullet">
/// <item><b>霧團</b>（<see cref="SetCloud"/>）：湧出時由下往上長出來、吹散時往上飄、上方先散（shader 的 <c>_Fade</c>，＝BossAura 的吹散反過來用）。</item>
/// <item><b>本體</b>（<see cref="SetBodyVisible"/>）：怪物自己的 SpriteRenderer 的 alpha。紅霧蓋上去之後把本體收掉、
/// 讓霧團接手；現身時反過來。腳下影子跟著本體一起收放（霧不投影）。</item>
/// </list>
///
/// <para><b>定位與大小</b>（<see cref="MonsterController.FeetWorldPos"/>／<see cref="MonsterController.VisibleBodyHeight"/>，
/// 不用 transform.position——route B 怪物 pivot 在畫布中心，見 PROBLEMS **G13**）：
/// 霧團中心在腳底上方 0.5 身高。quad 刻意比霧大（霧團外緣只到 quad 的約 0.36～0.5，見 shader `_OuterRadius`），
/// 留空間給煙的外緣與往後拖長，不會被 quad 邊切掉。</para>
///
/// <para><b>獨立物件、不當子物件</b>（同 BlobShadow／CharacterGlow）：怪物會 flipX、會被 CSV 的 Scale 縮放，
/// 掛成子物件會被二次影響。物件掛在怪物的父節點下 ⇒ 換圖拆地圖時一起清掉；怪物先被銷毀時 <see cref="OnDestroy"/> 收。</para>
///
/// <para><b>排序</b>：每幀抄怪物的 sortingLayer／sortingOrder（YSortByFeet 會動態改），＋1 畫在本體之上
/// ——化霧／現身時兩者重疊，霧要蓋在正在淡掉／淡回來的人身上。</para>
///
/// <para>⭐ <b>紅霧一定收得掉</b>（作者要求：任何特殊狀況下怪死了都不能留一團霧）：
/// ① 怪死亡（`IsDead`）的那一幀 <see cref="ResetToBody"/>（霧歸零、本體全亮——死亡特效量的是本體）；
/// ② 怪物被銷毀（死亡流程最後就是 Destroy）→ <see cref="OnDestroy"/> 連霧的物件一起銷毀；
/// ③ 怪物被停用 → <see cref="OnDisable"/> 關掉霧；④ 換圖拆地圖 → 霧掛在同一個父節點下一起清掉。</para>
/// </summary>
[DisallowMultipleComponent]
public class BloodMistVisual : MonoBehaviour
{
    const string ShaderPath = "Shaders/BloodMist";
    // 與 Boss 開戰黑霧（BossIntroPanel／UI/BossAura）共用的兩張灰階密度圖——**霧的形狀全靠它們**，
    // 純程序 noise 做不出煙的絲與捲（BossAura 檔頭；本元件第一版就踩過）。匯入設定見 BossAura.shader 檔頭。
    const string SmokeTexAPath = "UI/BossIntroPanel/BossIntroPanel_Smoke1";   // 厚重霧體
    const string SmokeTexBPath = "UI/BossIntroPanel/BossIntroPanel_Smoke2";   // 細絮煙流

    [Tooltip("quad 寬 = 可見身高 × 此值。霧團本身約佔 quad 的 3/4（外緣半徑 0.36＋啃邊），看到的霧寬 ≈ 身高 × 1.5")]
    public float WidthFactor = 2.0f;
    [Tooltip("quad 高 = 可見身高 × 此值（霧略高於寬）")]
    public float HeightFactor = 2.0f;
    [Tooltip("霧團中心在腳底上方幾個身高")]
    public float CenterAboveFeet = 0.5f;
    [Tooltip("煙的紋理跟著世界走的比例：1＝煙完全釘在世界上（怪走過去、煙原地不動地被穿過）；0＝煙黏在怪身上一起平移。" +
             "介於中間 ⇒ 移動時煙往後流，看起來是一團在飄的霧")]
    public float WorldFlowFollow = 0.6f;

    [Tooltip("霧團淡入／淡出秒數")]
    // 湧出 0.35／吹散 0.30：這兩段就是整個「變身」演出（沒有序列圖了），太快會像閃一下就不見。
    public float CloudFadeInSeconds = 0.35f, CloudFadeOutSeconds = 0.30f;
    [Tooltip("本體（怪物 sprite）淡入／淡出秒數")]
    public float BodyFadeSeconds = 0.18f;

    [Tooltip("拖曳：每 1 單位/秒的移動速度往後拖多少（uv）")]
    public float DragPerSpeed = 0.12f;
    [Tooltip("拖曳上限（uv）。太大霧會被甩成斜長條")]
    public float MaxDrag = 0.45f;
    [Tooltip("拖曳的平滑時間（秒）：起步時霧慢慢被甩開、停下時慢慢收回")]
    public float DragSmoothTime = 0.15f;

    static Material _sharedMat;
    static Sprite _sharedSprite;

    MonsterController _mc;
    SpriteRenderer _bodySr;
    BlobShadow _shadow;
    GameObject _go;
    SpriteRenderer _sr;
    MaterialPropertyBlock _mpb;

    float _cloud, _cloudTarget;
    float _body = 1f, _bodyTarget = 1f;
    bool _bodyTouched;          // 本體 alpha 是否被本元件改過（改過才需要還原）
    bool _shadowHidden;
    Vector2 _drag, _dragVel;
    Vector2 _lastPos;
    bool _lastPosInit;
    float _seed;
    bool _ok;

    /// <summary>霧團目前的顯現程度 0~1。</summary>
    public float Cloud => _cloud;
    /// <summary>本體目前的 alpha 0~1。</summary>
    public float Body => _body;

    /// <summary>霧團要不要出現（以固定速度淡入／淡出）。</summary>
    public void SetCloud(bool on) => _cloudTarget = on ? 1f : 0f;

    /// <summary>本體（怪物 sprite）要不要看得到（以固定速度淡入／淡出）。影子跟著一起收放。</summary>
    public void SetBodyVisible(bool on) => _bodyTarget = on ? 1f : 0f;

    /// <summary>立刻回到「正常人形」：霧團歸零、本體全亮、影子打開。Brain 被中斷／死亡時的保險。</summary>
    public void ResetToBody()
    {
        _cloud = _cloudTarget = 0f;
        _body = _bodyTarget = 1f;
        ApplyBodyAlpha();
        ApplyShadow();
        if (_sr != null) _sr.enabled = false;
    }

    void Awake()
    {
        _mc = GetComponent<MonsterController>();
        _bodySr = GetComponent<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
        _seed = Random.Range(0f, 10f);   // 加在紋理座標上（Repeat 平鋪），範圍不必大

        var mat = GetSharedMaterial();
        if (mat == null || _bodySr == null)
        {
            // Shader 沒進 build 時的防呆：沒有霧團，但本體淡入淡出照常 ⇒ 化霧時人會直接消失、現身時出現，玩法不受影響。
            Debug.LogWarning($"[BloodMistVisual] 載不到 Resources/{ShaderPath}，血霧只剩本體淡出淡入。");
            return;
        }

        _go = new GameObject($"{name}_BloodMist");
        _go.transform.SetParent(transform.parent, false);   // 掛在怪物的父節點下 ⇒ 換圖拆地圖時一起清掉
        _sr = _go.AddComponent<SpriteRenderer>();
        _sr.sprite = GetSharedSprite();
        _sr.sharedMaterial = mat;
        _sr.enabled = false;
        _ok = true;
    }

    void OnDestroy()
    {
        if (_go != null) Destroy(_go);
    }

    void OnDisable()
    {
        if (_sr != null) _sr.enabled = false;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        // 死了就立刻回人形（現身到一半被打死時，死亡演出要演在看得到的本體上）
        if (_mc != null && _mc.IsDead && (_cloud > 0f || _body < 1f)) ResetToBody();

        // ── 目標值逼近 ──
        float cloudRate = _cloudTarget > _cloud ? 1f / Mathf.Max(0.01f, CloudFadeInSeconds)
                                                : 1f / Mathf.Max(0.01f, CloudFadeOutSeconds);
        _cloud = Mathf.MoveTowards(_cloud, _cloudTarget, cloudRate * dt);
        _body  = Mathf.MoveTowards(_body,  _bodyTarget,  dt / Mathf.Max(0.01f, BodyFadeSeconds));
        ApplyBodyAlpha();
        ApplyShadow();

        // ── 拖曳：用實際位移算速度（不看指令速度，被牆卡住時霧不該被甩開）──
        Vector2 pos = transform.position;
        if (!_lastPosInit) { _lastPos = pos; _lastPosInit = true; }
        Vector2 vel = dt > 0.0001f ? (pos - _lastPos) / dt : Vector2.zero;
        _lastPos = pos;
        // 往「移動的反方向」拖；uv 的 x/y 與世界同向（quad 不翻面、不旋轉）
        Vector2 want = Vector2.ClampMagnitude(-vel * DragPerSpeed, MaxDrag);
        _drag = Vector2.SmoothDamp(_drag, want, ref _dragVel, DragSmoothTime, Mathf.Infinity, dt);

        if (!_ok) return;

        bool show = _cloud > 0.001f && _bodySr.enabled && gameObject.activeInHierarchy;
        if (_sr.enabled != show) _sr.enabled = show;
        if (!show) return;

        // ── 定位與大小（腳底＋身高，見類別註解）──
        float h = _mc != null ? _mc.VisibleBodyHeight : 2f;
        Vector2 feet = _mc != null ? _mc.FeetWorldPos : pos;
        _go.transform.position = new Vector3(feet.x, feet.y + h * CenterAboveFeet, transform.position.z);
        _go.transform.localScale = new Vector3(h * WidthFactor, h * HeightFactor, 1f);

        // ── 排序：跟著本體走，＋1 蓋在本體之上 ──
        _sr.sortingLayerID = _bodySr.sortingLayerID;
        _sr.sortingOrder = _bodySr.sortingOrder + 1;

        // 煙的紋理座標跟著世界位置走一部分（換成 quad 的 uv 單位）⇒ 移動時煙往後流
        float qw = Mathf.Max(0.01f, h * WidthFactor), qh = Mathf.Max(0.01f, h * HeightFactor);
        Vector2 worldUv = new Vector2(feet.x / qw, feet.y / qh) * WorldFlowFollow;

        _sr.GetPropertyBlock(_mpb);
        _mpb.SetFloat("_Fade", _cloud);
        _mpb.SetFloat("_Seed", _seed);
        _mpb.SetVector("_Drag", new Vector4(_drag.x, _drag.y, 0f, 0f));
        _mpb.SetVector("_WorldOffset", new Vector4(worldUv.x, worldUv.y, 0f, 0f));
        _sr.SetPropertyBlock(_mpb);
    }

    void ApplyBodyAlpha()
    {
        if (_bodySr == null) return;
        if (_body >= 0.999f && !_bodyTouched) return;   // 沒碰過就不碰（不跟受擊閃爍搶 color）
        Color c = _bodySr.color;
        c.a = _body;
        _bodySr.color = c;
        _bodyTouched = _body < 0.999f;                    // 回到全亮後交還控制權
    }

    void ApplyShadow()
    {
        bool hide = _body < 0.5f;
        if (hide == _shadowHidden) return;
        _shadowHidden = hide;
        if (_shadow == null) _shadow = GetComponent<BlobShadow>();
        if (_shadow != null) _shadow.SetVisible(!hide);
    }

    static Material GetSharedMaterial()
    {
        if (_sharedMat != null) return _sharedMat;
        // 走 Resources.Load（專案慣例，同 GroundCrackFx）——Shader.Find 只找得到被引用或 Always Included 的 shader。
        var sh = Resources.Load<Shader>(ShaderPath);
        if (sh == null) return null;
        _sharedMat = new Material(sh) { name = "BloodMist (shared)" };

        var texA = LoadSmokeTex(SmokeTexAPath);
        var texB = LoadSmokeTex(SmokeTexBPath);
        if (texA == null || texB == null)
            Debug.LogWarning($"[BloodMistVisual] 找不到煙霧密度圖 Resources/{SmokeTexAPath} 或 Resources/{SmokeTexBPath}，" +
                             "血霧會退化成一團沒有煙紋的紅色漸層。");
        else
        {
            _sharedMat.SetTexture("_SmokeA", texA);
            _sharedMat.SetTexture("_SmokeB", texB);
        }
        return _sharedMat;
    }

    // 同 BossIntroPanel.LoadSmokeTex：圖是 Sprite 匯入，Texture2D 載不到就從 Sprite 拿
    static Texture2D LoadSmokeTex(string path)
    {
        var tex = Resources.Load<Texture2D>(path);
        if (tex != null) return tex;
        var spr = Resources.Load<Sprite>(path);
        return spr != null ? spr.texture : null;
    }

    static Sprite GetSharedSprite()
    {
        if (_sharedSprite != null) return _sharedSprite;
        // 1×1 白點、PPU 1 ⇒ 世界尺寸 1×1、uv 0~1；大小全由 localScale 決定（同 GroundCrackFx）。
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _sharedSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        _sharedSprite.name = "BloodMistQuad";
        return _sharedSprite;
    }
}
