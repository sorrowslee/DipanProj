using UnityEngine;

/// <summary>
/// 地面龜裂（程序化 shader，零素材）：跳躍踐踏落地時在地上炸開的一圈裂痕 ＋ 一道往外掃的塵浪。
///
/// <para>圖樣是**乾裂泥地那種均勻的不規則多邊形**（Voronoi），不是放射狀的爆裂——
/// 2026-09-18 依作者的參考圖改寫，理由與演算法見 <c>Resources/Shaders/GroundCrack.shader</c> 的檔頭。
/// 「從中心往外蔓延」只是 0.85 秒的演出（`_Progress`），裂完是一整片均勻的龜裂、沒有中心。</para>
///
/// <para>用法一行：<c>GroundCrackFx.Spawn(落點, 半徑, 怪物的SpriteRenderer)</c>。
/// 之後自己跑完「裂開 → 停留 → 淡出 → 銷毀」然後消失，呼叫端不必保留引用。</para>
///
/// <para><b>純視覺，不含任何傷害</b>——踐踏的殺傷是另一個元件（<see cref="ImpactDamageArea"/>），
/// 兩者刻意分開：裂痕的視覺半徑通常要比殺傷半徑大一點（打不到的地方也該裂），
/// 綁在一起就會為了好看而改到傷害範圍。見 readme/BOSS_MODULE.md §9。</para>
///
/// <para>為什麼不做成 GroundEffectTable 的一列：那套系統的傷害遮罩是「怪物＋可破壞地上物」，
/// 是給**玩家武器**用的，打不到玩家；為了讓怪用而去改它，會動到所有玩家武器共用的傷害路徑。
/// 裂痕本身又是純視覺，接進去只是為了借一張表，不值得。</para>
/// </summary>
[DisallowMultipleComponent]
public class GroundCrackFx : MonoBehaviour
{
    // ── 節奏（秒）。要調手感就改這四個 ──
    // 裂痕從中心竄到最外圈要多久。
    // ⭐ 演進：0.22 → 0.85（作者要「慢慢延伸出去」）→ **0.06**（看了之後改回「猛烈的一次全部出現」）。
    //    0.06 秒 ＝ 60fps 下約 4 幀，實質上是瞬間炸開，但保留一點點爆發的過程；
    //    真的想要「第一幀就完整」就填 0。
    //    ⚠ 這個數字與 LeapSlamBrain 的落地定格 1 秒是**兩件事**：定格那一秒仍然存在（壓迫感來自停頓），
    //      只是裂痕不再用那一秒慢慢長，而是在砸到的瞬間就整片裂開、然後靜靜留在地上。
    const float CrackSeconds = 0.06f;
    // 塵浪也要跟著快：裂痕瞬間炸開、塵浪卻慢慢掃的話，兩者會脫節成兩個獨立的動畫。
    const float RingSeconds  = 0.32f;
    // 停留與淡出：3.2 / 1.6（第一版）作者實機回報「持續時間太短」。
    // 現在總共 9 + 3.5 ≈ 12.5 秒——足以在打完這一波之後還看得到地上那道痕跡。
    // ⚠ 再拉長要留意：裂痕是一個每幀跑 fragment shader 的半透明 quad，同時存在太多張會吃 overdraw。
    //   目前一隻怪一生只跳一次，場上頂多幾道，沒有問題；哪天做成「可重複跳」就要回來看這裡。
    const float HoldSeconds  = 9.0f;    // 裂痕留在地上多久才開始淡
    const float FadeSeconds  = 3.5f;    // 淡出

    // 俯視角壓扁：地面上的圓在畫面上是橢圓。0.5 ＝ 與 BlobShadow 的 HeightRatio 同一套透視。
    const float FlatRatio = 0.5f;

    // 排序：與地面特效同一階（8）——高於「可走地上物」(5)、低於角色與立體地上物的 Y 排序帶。
    // 所以裂痕會蓋在石板/地毯上，但不會蓋住柱子與角色。見 GroundEffectInstance.GroundEffectSortingOrder。
    const int SortingOrder = 8;

    static Sprite _sharedSprite;
    static Shader _shader;

    SpriteRenderer _sr;
    Material _mat;
    float _t;

    /// <summary>
    /// 生一道裂痕。<paramref name="radius"/> ＝ 世界單位的**視覺半徑**（不是殺傷半徑）。
    /// <paramref name="sortingRef"/> 用來抄 sortingLayer（傳怪物自己的 renderer 即可；null 就用預設層）。
    /// <paramref name="seed"/> &lt; 0 ＝ 隨機，所以每次落地的裂紋都不一樣。
    /// </summary>
    public static GroundCrackFx Spawn(Vector2 pos, float radius, SpriteRenderer sortingRef = null, float seed = -1f)
    {
        if (radius <= 0.01f) return null;
        var go = new GameObject("GroundCrackFx");
        go.transform.position = pos;
        var fx = go.AddComponent<GroundCrackFx>();
        fx.Init(radius, sortingRef, seed);
        return fx;
    }

    void Init(float radius, SpriteRenderer sortingRef, float seed)
    {
        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = GetSharedSprite();
        if (sortingRef != null) _sr.sortingLayerID = sortingRef.sortingLayerID;
        _sr.sortingOrder = SortingOrder;

        // 走 Resources.Load（專案慣例，見 SpriteFlash/AuraGlow/Mosaic…）——Shader.Find 只找得到
        // 「已經被某個材質引用而載進來」的 shader，build 裡沒人用它就會回 null。
        if (_shader == null) _shader = Resources.Load<Shader>("Shaders/GroundCrack");
        if (_shader == null)
        {
            // Shader 沒被打包進 build 時的防呆：寧可沒有裂痕，也不要跳一個洋紅色方塊在地上。
            // （Resources/Shaders/ 底下的 shader 會隨 Resources 一起進 build，正常不會走到這裡。）
            Debug.LogWarning("[GroundCrackFx] 載不到 Resources/Shaders/GroundCrack，略過裂痕特效。");
            Destroy(gameObject);
            return;
        }

        _mat = new Material(_shader);
        _sr.material = _mat;
        _mat.SetFloat("_Seed", seed >= 0f ? seed : Random.Range(0f, 100f));
        _mat.SetFloat("_Progress", 0f);
        _mat.SetFloat("_RingProgress", 0f);
        _mat.SetFloat("_Fade", 1f);
        // 泥塊密度也每次隨機一點，免得兩次落地的龜裂大小一模一樣。
        // 數字是「整個圓的直徑上有幾塊」：直徑 7 世界單位 ÷ 10 ⇒ 一塊約 0.7 單位（角色高 2.5 的 1/4 左右）。
        // 想要更碎就調大。⚠ 這是 uv 空間的密度，所以泥塊會跟著 FlatRatio 一起被壓扁——那正是俯視角該有的透視。
        _mat.SetFloat("_CellScale", Random.Range(9f, 12f));

        // sprite 的 native 尺寸 = 1 世界單位（PPU = 邊長），所以 localScale 直接就是直徑。
        float d = radius * 2f;
        transform.localScale = new Vector3(d, d * FlatRatio, 1f);
    }

    void Update()
    {
        if (_mat == null) return;
        _t += Time.deltaTime;

        // 裂開：ease-out（一開始竄很快、末端收慢），比線性更像材料崩裂
        float p = Mathf.Clamp01(_t / CrackSeconds);
        _mat.SetFloat("_Progress", 1f - (1f - p) * (1f - p));

        // 塵浪：掃完一圈就關掉（shader 端 >= 1 不畫）
        float ring = _t / RingSeconds;
        _mat.SetFloat("_RingProgress", ring >= 1f ? 1f : ring);

        float fadeStart = Mathf.Max(CrackSeconds, HoldSeconds);
        if (_t >= fadeStart)
        {
            float f = 1f - Mathf.Clamp01((_t - fadeStart) / FadeSeconds);
            _mat.SetFloat("_Fade", f);
            if (f <= 0.001f) Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        // 執行期 new 出來的材質要自己收（SpriteRenderer.material 是 instance，不收會漏）
        if (_mat != null) Destroy(_mat);
    }

    /// <summary>1×1 純白 sprite（PPU = 1 ⇒ native 尺寸 = 1 世界單位）。整張圖形都由 shader 畫，貼圖本身不被取樣。</summary>
    static Sprite GetSharedSprite()
    {
        if (_sharedSprite != null) return _sharedSprite;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        _sharedSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        _sharedSprite.hideFlags = HideFlags.HideAndDontSave;
        return _sharedSprite;
    }

    /// <summary>關閉 Domain Reload 時的 static 快取重置（見 readme/PROBLEMS.md I8）。</summary>
    public static void ResetForPlayMode() { _sharedSprite = null; _shader = null; }
}
