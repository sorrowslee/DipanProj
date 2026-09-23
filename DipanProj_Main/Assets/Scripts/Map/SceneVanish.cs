using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **場景吞噬**（鏈動作 <c>sceneVanish</c>，2026-09-23 為夢境教學佛掌段而做）：以玩家為中心，
/// 「還看得到的世界」由畫面外往內縮，縮到最後整張地圖只剩**極暗的血紅虛空**——
/// 只留下角色（玩家、骨牢、佛掌、怪物）與牠們的影子、傷害數字、HUD。**不會自己復原**（劇情用），換圖時隨地圖一起清掉。
///
/// <para><b>為什麼不是一張蓋在上面的黑幕</b>：角色和地上物共用同一條 Y 排序帶（MapDepthSort 7000~12999），
/// 黑幕不管放第幾層，不是把角色一起蓋掉、就是連地上物都蓋不到。所以改成**讓地圖自己消失**：</para>
/// <list type="number">
/// <item><b>背景與地上物的 sprite</b>（原本是預設 Sprites/Default 材質的）換成 <c>Custom/SceneVanish</c>：
///   逐像素依「離玩家多遠」決定要不要換成虛空色，吞噬線才能**平滑地**從一張地上物的中間切過去。</item>
/// <item><b>其他東西</b>（自訂材質的 sprite、粒子、光源、場景特效、地上掉落物、互動星星）：
///   吞噬線**整個越過**它時直接關掉。光源一定要關——否則虛空裡還會留著火盆照出來的一圈亮。</item>
/// </list>
///
/// <para>哪些東西會留下：凡是**不在 MapRoot 底下**的（玩家、骨牢、影子、特效、傷害數字），以及 MapRoot 底下
/// 帶 <see cref="MonsterController"/> 的整棵（怪物被 MapLoader 掛在 MapRoot 下，要特別排除）。</para>
///
/// <para>⚠ 只關 renderer／光源／特效元件，**不 SetActive(false)** 地圖物件：地上物身上還有碰撞、可破壞、
/// 出生點計時器（佛掌就是 MapMonsterRespawner 生出來的），整個關掉會把遊戲邏輯一起停掉。</para>
/// </summary>
[DisallowMultipleComponent]
public class SceneVanish : MonoBehaviour
{
    public const string ShaderName = "Custom/SceneVanish";
    const string DefaultSpriteShader = "Sprites/Default";

    /// <summary>虛空色預設（gamma 值；SetGlobalColor 會自己轉 linear）。極暗的血紅，不是純黑——夢境被邪佛吞掉的感覺。</summary>
    public static readonly Color DefaultVoidColor = new Color32(0x1C, 0x04, 0x07, 0xFF);
    /// <summary>吞噬線燒邊色預設。</summary>
    public static readonly Color DefaultEdgeColor = new Color32(0xC0, 0x18, 0x1E, 0xFF);

    // ── 手感參數（世界單位）──
    const float Feather = 2.0f;        // 吞噬線柔邊寬
    const float Wobble = 0.7f;         // 吞噬線起伏幅度
    const float EdgeGlow = 1.3f;       // 燒邊亮度
    const float RescanInterval = 0.5f; // 多久重掃一次（期間新冒出來的掉落物、粒子）

    static readonly int CenterId = Shader.PropertyToID("_SV_Center");
    static readonly int RadiusId = Shader.PropertyToID("_SV_Radius");
    static readonly int FeatherId = Shader.PropertyToID("_SV_Feather");
    static readonly int WobbleId = Shader.PropertyToID("_SV_Wobble");
    static readonly int EdgeGlowId = Shader.PropertyToID("_SV_EdgeGlow");
    static readonly int VoidColorId = Shader.PropertyToID("_SV_VoidColor");
    static readonly int EdgeColorId = Shader.PropertyToID("_SV_EdgeColor");

    /// <summary>一個「要被吞掉的東西」：吞噬線整個越過它（距離 − 半徑 &gt; 吞噬半徑）時關掉。</summary>
    class Unit
    {
        public Transform tr;
        public Vector2 pos;
        public float extent;
        public bool deactivate;   // true＝整個 SetActive(false)（純視覺的掉落物／互動星星）；false＝只關 renderer/光源/特效
        public bool gone;
    }

    readonly List<Unit> _units = new List<Unit>();
    readonly HashSet<Transform> _known = new HashSet<Transform>();
    readonly List<KeyValuePair<SpriteRenderer, Material>> _swapped = new List<KeyValuePair<SpriteRenderer, Material>>();

    Transform _mapRoot;
    Material _mat;
    Camera _cam;
    Color _camOrig;
    Vector2 _center;
    Color _void, _edge;
    float _delay, _seconds, _t;
    bool _started;
    float _rescanAt;
    System.Action _onDone;   // 吞完呼叫一次（sceneVanish 的「接續時機＝吞完後」）

    /// <summary>
    /// 開始吞噬。<paramref name="delay"/> 秒後才開始縮（先讓佛掌出現、玩家看清楚牠），<paramref name="seconds"/> 秒縮完。
    /// 同一張圖已經在吞了就不重來（回傳既有的）。找不到地圖回 null。
    /// </summary>
    /// <param name="onDone">吞完（整張全黑）的那一刻呼叫一次；null＝不需要。開不成或已經吞完也會立刻呼叫，不會讓呼叫端卡住。</param>
    public static SceneVanish Play(float delay, float seconds, Color voidColor, Color edgeColor, System.Action onDone = null)
    {
        var loader = Object.FindObjectOfType<MapLoader>();
        var root = loader != null ? loader.MapRoot : null;
        if (root == null)
        {
            Debug.LogWarning("[SceneVanish] 找不到目前的地圖（MapLoader.MapRoot 是空的），不做吞噬。");
            onDone?.Invoke();
            return null;
        }
        var existing = root.GetComponentInChildren<SceneVanish>(true);
        if (existing != null)
        {
            Debug.LogWarning("[SceneVanish] 這張圖已經在吞噬（或已吞完），忽略這次。");
            onDone?.Invoke();   // 不等它：重複觸發時等舊的那一份反而可能永遠等不到（舊的已經吞完、不會再通知）
            return existing;
        }

        var go = new GameObject("[SceneVanish]");
        go.transform.SetParent(root, false);   // 掛 MapRoot：換圖時一起被銷毀 → OnDestroy 還原相機底色
        var v = go.AddComponent<SceneVanish>();
        v._mapRoot = root;
        v._delay = Mathf.Max(0f, delay);
        v._seconds = Mathf.Max(0.05f, seconds);
        v._void = voidColor;
        v._edge = edgeColor;
        v._onDone = onDone;
        return v;
    }

    void Update()
    {
        if (!_started)
        {
            _t += Time.deltaTime;
            if (_t < _delay) return;
            Begin();
            _t = 0f;
        }

        _t += Time.deltaTime;
        float p = Mathf.Clamp01(_t / _seconds);
        float e = p * p * (3f - 2f * p);   // smoothstep：起步慢、收尾慢，中段吞得最快

        // 起點＝剛好在畫面最遠的角落外面（每幀算：鏡頭這時可能還在 camZone 拉遠中），終點＝負值（連燒邊都收掉＝全吞）。
        float rView = ViewRadius();
        float r = Mathf.Lerp(rView + Feather + Wobble, -(Feather + Wobble), e);
        Shader.SetGlobalFloat(RadiusId, r);
        if (_cam != null) _cam.backgroundColor = Color.Lerp(_camOrig, _void, e);   // 地圖外圍（背景圖沒蓋到的黑邊）也一起變虛空

        if (p >= 1f && _onDone != null) { var cb = _onDone; _onDone = null; cb(); }   // 先清再叫：回呼裡再觸發別的鏈也不會重入

        if (Time.time >= _rescanAt) { _rescanAt = Time.time + RescanInterval; Collect(); }

        for (int i = 0; i < _units.Count; i++)
        {
            var u = _units[i];
            if (u.tr == null) continue;
            if (u.gone) { if (u.deactivate && u.tr.gameObject.activeSelf) u.tr.gameObject.SetActive(false); continue; }   // 被別人重新打開（互動星星）→ 再關
            if (Vector2.Distance(u.pos, _center) - u.extent > r + Wobble) Hide(u);
        }
    }

    void Begin()
    {
        _started = true;
        var player = GameObject.FindGameObjectWithTag("Player");
        var pc = player != null ? player.GetComponent<PlayerController>() : null;
        _center = pc != null ? pc.FeetWorldPos : (player != null ? (Vector2)player.transform.position : Vector2.zero);

        _cam = Camera.main;
        if (_cam != null) _camOrig = _cam.backgroundColor;

        var shader = Shader.Find(ShaderName);
        if (shader == null)
            Debug.LogWarning($"[SceneVanish] 找不到 shader「{ShaderName}」——背景與地上物改成「吞噬線越過就整個關掉」（沒有平滑的逐像素吞噬）。");
        else
            _mat = new Material(shader) { name = "SceneVanish (runtime)" };

        Shader.SetGlobalVector(CenterId, new Vector4(_center.x, _center.y, 0f, 0f));
        Shader.SetGlobalFloat(FeatherId, Feather);
        Shader.SetGlobalFloat(WobbleId, Wobble);
        Shader.SetGlobalFloat(EdgeGlowId, EdgeGlow);
        Shader.SetGlobalColor(VoidColorId, _void);
        Shader.SetGlobalColor(EdgeColorId, _edge);
        Shader.SetGlobalFloat(RadiusId, ViewRadius() + Feather + Wobble);

        Collect();
        _rescanAt = Time.time + RescanInterval;
        Debug.Log($"[SceneVanish] 開始吞噬：中心 {_center}、{_seconds:0.##} 秒縮完、{_units.Count} 個物件、{_swapped.Count} 張 sprite 走逐像素。");
    }

    /// <summary>從吞噬中心到目前畫面最遠角落的距離。</summary>
    float ViewRadius()
    {
        if (_cam == null || !_cam.orthographic) return 30f;
        float hh = _cam.orthographicSize, hw = hh * _cam.aspect;
        Vector2 c = _cam.transform.position;
        float best = 0f;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                best = Mathf.Max(best, Vector2.Distance(_center, c + new Vector2(sx * hw, sy * hh)));
        return best;
    }

    /// <summary>收集（或補收）要被吞的東西。重複呼叫安全：看過的不會再收一次。</summary>
    void Collect()
    {
        if (_mapRoot == null) return;
        for (int i = 0; i < _mapRoot.childCount; i++)
        {
            var c = _mapRoot.GetChild(i);
            if (c == transform) continue;
            // 分類容器：底下每一個小孩各算一個（每個地上物各自被吞）
            if (c.name == "Objects" || c.name == "SceneFx" || c.name == "MapLights")
                for (int j = 0; j < c.childCount; j++) AddMapUnit(c.GetChild(j));
            else AddMapUnit(c);
        }
        // 不在 MapRoot 底下、但屬於「場景」的純視覺：地上掉落物、互動星星（祭壇上那幾顆）
        foreach (var loot in Object.FindObjectsOfType<GroundLoot>()) AddExtraUnit(loot.transform);
        foreach (var mk in Object.FindObjectsOfType<InteractMarker>()) AddExtraUnit(mk.transform);
    }

    void AddMapUnit(Transform t)
    {
        if (t == null || _known.Contains(t)) return;
        // 怪物（含佛掌）整棵保留。⚠ 用 GetComponentInChildren：MapLoader 把怪掛在 MapRoot 底下。
        if (t.GetComponentInChildren<MonsterController>(true) != null) return;

        var rends = t.GetComponentsInChildren<Renderer>(true);
        bool hasLight = t.GetComponentInChildren<LightSource>(true) != null;
        if (rends.Length == 0 && !hasLight) return;   // 純邏輯（出生點計時器、顯現管理器）——不碰，也不必再看

        _known.Add(t);
        var u = MakeUnit(t, rends, false);

        // 預設材質的 sprite → 換逐像素吞噬材質（背景、一般地上物）。其他材質的維持原樣，等吞噬線越過整個關掉。
        if (_mat != null)
            foreach (var r in rends)
                if (r is SpriteRenderer sr && sr.sharedMaterial != null && sr.sharedMaterial.shader != null
                    && sr.sharedMaterial.shader.name == DefaultSpriteShader)
                {
                    _swapped.Add(new KeyValuePair<SpriteRenderer, Material>(sr, sr.sharedMaterial));
                    sr.sharedMaterial = _mat;
                }

        _units.Add(u);
    }

    void AddExtraUnit(Transform t)
    {
        if (t == null || _known.Contains(t)) return;
        _known.Add(t);
        _units.Add(MakeUnit(t, t.GetComponentsInChildren<Renderer>(true), true));
    }

    static Unit MakeUnit(Transform t, Renderer[] rends, bool deactivate)
    {
        var u = new Unit { tr = t, pos = t.position, extent = 0f, deactivate = deactivate };
        bool any = false;
        Bounds b = default;
        foreach (var r in rends)
        {
            if (r == null) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (any) { u.pos = b.center; u.extent = b.extents.magnitude; }
        return u;
    }

    static void Hide(Unit u)
    {
        u.gone = true;
        if (u.tr == null) return;
        if (u.deactivate) { u.tr.gameObject.SetActive(false); return; }
        foreach (var r in u.tr.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (var l in u.tr.GetComponentsInChildren<LightSource>(true)) l.enabled = false;         // 否則虛空裡還留著火盆的光
        foreach (var fx in u.tr.GetComponentsInChildren<SceneFxEmitter>(true)) fx.enabled = false;    // 不再噴新的粒子
        foreach (var pf in u.tr.GetComponentsInChildren<PortalFx>(true)) pf.enabled = false;
    }

    void OnDestroy()
    {
        // 換圖（MapRoot 被拆）時走到這裡：相機底色一定要還回去，否則下一張圖的黑邊會變成血紅色。
        if (_cam != null) _cam.backgroundColor = _camOrig;
        foreach (var kv in _swapped) if (kv.Key != null) kv.Key.sharedMaterial = kv.Value;
        if (_mat != null) Destroy(_mat);
    }
}
