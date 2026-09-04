using UnityEngine;

/// <summary>
/// 角色體光（怪物用）：在角色**背後**疊一圈柔和的加色光暈，讓它在暗地圖裡浮出輪廓。
/// 掛上即可用：<c>gameObject.AddComponent&lt;CharacterGlow&gt;()</c>（由 MonsterSpawner.AttachMonsterGlow 統一掛）。
/// 全程式建構、零素材——光暈圖是程序生成的徑向漸層（一張共用快取），同 BlobShadow 的路數。
///
/// ── ⚠ 為什麼需要這個？<see cref="LightSource"/> 那條路在暗地板上會失效 ──
/// `LightSource` 走的是 <c>AtmosphereController</c> → `Atmosphere.shader` 的照明系統，而那套光是
/// **乘法**——type 2 幽暗那段是 <c>col.rgb *= lerp(0.35, 1.0, v)</c>，光的作用是「把壓暗**還原**」，
/// 不是「加光」。所以光圈亮不亮**完全取決於該處原本的畫面有多亮**：
///   ‧ 淺色石板地（原亮度 ~0.5）：0.175 → 0.33，光圈很明顯
///   ‧ 新娘房的深色木地板（原亮度 ~0.15）：0.053 → 0.099，兩個都是「很暗」，**肉眼分不出來**
/// 2026-09-04 實機就是這樣：同一份程式、同樣的參數（log 證實都掛上了 r=1.5 i=0.55），
/// 一般房間的怪很亮、新娘房的 boss 完全看不見，差別只在腳下地板的顏色。
/// **再怎麼調 intensity 都沒用**——0.55 調到 1.0 也只是把 0.15 還原成 0.15。
/// 這與 <c>MemoryFxController</c> 檔頭記的是同一條：「在黑色上乘任何顏色還是黑」。
///
/// 所以這一層走**加色**（`Custom/AuraGlow`，`Blend One One`）：不管地板多暗都會提亮。
/// 兩層是互補、不是二選一，MonsterSpawner 會同時掛：
///   ‧ <see cref="LightSource"/>：讓怪**周圍的地面**跟著亮一點，有「光照」感（亮地板上有效）
///   ‧ 本元件：保證**輪廓**在任何地板上都看得見
///
/// ── 幾個刻意的決定 ──
/// ‧ 光暈畫在角色**之下**（sortingOrder −1）：只有角色 sprite 的透明區（＝輪廓外圈）會亮起來，
///   角色本身的顏色不被加色洗掉 ⇒ 是「剪影浮出來」不是「整隻怪發光」。
/// ‧ 獨立物件、不是子物件（同 BlobShadow）：避免被角色的 flipX／localScale 二次影響。
/// ‧ 光暈**也會被 Atmosphere 壓暗**（它是世界端 sprite），但它自己的亮度遠高於暗地板，
///   壓暗後對比仍在（實測估算：光暈 0.5×0.35=0.175 vs 地板 0.053，約 3 倍）。
/// ‧ ⚠ 專案是 Linear 色彩空間，加色疊上去比直覺重約一倍（PROBLEMS **E11**），
///   所以 <see cref="Intensity"/> 的預設值刻意保守。嫌暗就往上調，但先看實機。
/// </summary>
[DisallowMultipleComponent]
public class CharacterGlow : MonoBehaviour
{
    [Header("光暈外觀（可調）")]
    [Tooltip("光暈顏色。怪物用陰冷青白，與玩家體光的微暖白區隔")]
    public Color GlowColor = new Color(0.55f, 0.80f, 0.95f, 1f);
    [Tooltip("亮度（加色量）。⚠ Linear 色彩空間疊色比直覺重約一倍，預設刻意保守")]
    public float Intensity = 0.11f;
    [Tooltip("光暈直徑 = 角色可見高度 × 此值")]
    public float SizeFactor = 1.35f;
    [Tooltip("比角色的 sortingOrder 低幾階（要低，光暈才在角色背後、不會洗掉角色顏色）")]
    public int SortingOrderBelow = 1;

    const string GlowShaderPath = "Shaders/AuraGlow";   // Custom/AuraGlow：Blend One One 加色

    static Sprite _sharedSprite;      // 程序生成的徑向漸層（Unity 物件被銷毀後 ==null 為 true，會自動重建）
    static Material _sharedMat;

    SpriteRenderer _charSr;
    GameObject _glowGo;
    SpriteRenderer _glowSr;

    void Start()
    {
        _charSr = GetComponent<SpriteRenderer>();
        if (_charSr == null) { enabled = false; return; }

        var mat = GetSharedMaterial();
        if (mat == null) { enabled = false; return; }   // shader 載不到 → 靜靜停用，不擋任何流程

        // 獨立物件（不是子物件）：角色會 flipX、會被 CSV 的 Scale 縮放，做成子物件會被二次影響（同 BlobShadow）。
        _glowGo = new GameObject($"{name}_Glow");
        _glowGo.transform.SetParent(transform.parent, false);   // 掛在角色的父節點下 ⇒ 換圖拆地圖時一起清掉
        _glowSr = _glowGo.AddComponent<SpriteRenderer>();
        _glowSr.sprite = GetSharedSprite();
        _glowSr.sharedMaterial = mat;
        _glowSr.sortingLayerID = _charSr.sortingLayerID;
        ApplyColor();
    }

    /// <summary>顏色/亮度改了之後叫一次（Inspector 上調完會自動生效，因為 LateUpdate 也會套）。</summary>
    void ApplyColor()
    {
        if (_glowSr == null) return;
        // AuraGlow 的亮度 = vertex color.a × _Intensity × 貼圖 alpha ⇒ 把 Intensity 塞進 color.a。
        _glowSr.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, Mathf.Max(0f, Intensity));
    }

    void LateUpdate()
    {
        if (_glowGo == null || _charSr == null) return;

        // 角色被藏起來（劇情、死亡演出）時光暈也跟著收，不然空地上會浮著一圈光。
        bool visible = _charSr.enabled && _charSr.gameObject.activeInHierarchy;
        if (_glowSr.enabled != visible) _glowSr.enabled = visible;
        if (!visible) return;

        // 位置：角色**可見範圍的中心**（不是 transform，那是腳底）。用 bounds 是因為怪物逐格動畫每幀高度會變，
        // 取中心比固定偏移穩。（玩家那邊不能這樣用——見 PROBLEMS E14；那是玩家 pivot 的坑，怪物沒有。）
        Bounds b = _charSr.bounds;
        _glowGo.transform.position = new Vector3(b.center.x, b.center.y, transform.position.z);

        // 大小：直徑 = 可見高度 × SizeFactor。共用 sprite 是 1 世界單位見方，所以 localScale 就是直徑。
        float d = Mathf.Max(0.01f, b.size.y * SizeFactor);
        _glowGo.transform.localScale = new Vector3(d, d, 1f);

        // 排序：跟著角色走（角色用 YSortByFeet 每幀改 order），但**低一階**畫在它背後。
        _glowSr.sortingOrder = _charSr.sortingOrder - SortingOrderBelow;
        ApplyColor();
    }

    void OnDestroy()
    {
        if (_glowGo != null) Destroy(_glowGo);
    }

    // ── 程序生成的徑向漸層：中心實、往外柔和收掉。RGB 全白，顏色交給 SpriteRenderer.color ──
    // 二次方衰減讓光集中在中心、邊緣不會出現一圈生硬的邊。
    static Sprite GetSharedSprite()
    {
        if (_sharedSprite != null) return _sharedSprite;
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        {
            float dy = (y + 0.5f) / N * 2f - 1f;
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);          // 0=中心、1=外緣
                float a = Mathf.Clamp01(1f - d);
                a = a * a;                                        // 二次方：中心集中、邊緣柔
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        // pixelsPerUnit = N ⇒ sprite 剛好 1 世界單位見方，localScale 直接等於直徑。
        _sharedSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        return _sharedSprite;
    }

    static Material GetSharedMaterial()
    {
        if (_sharedMat != null) return _sharedMat;
        var sh = Resources.Load<Shader>(GlowShaderPath);
        if (sh == null)
        {
            Debug.LogWarning($"[CharacterGlow] 找不到 Resources/{GlowShaderPath}，怪物體光的加色層停用" +
                             "（LightSource 那層不受影響，但在深色地板上幾乎看不出來）。");
            return null;
        }
        _sharedMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        _sharedMat.SetFloat("_Intensity", 1f);   // 亮度統一由 SpriteRenderer 的 color.a 控制
        return _sharedMat;
    }
}
