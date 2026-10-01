using UnityEngine;

/// <summary>
/// 近戰揮擊刀光（程序化 shader，零素材）：三道爪痕沿「判定那個扇形」掃過、停一下、淡出、自毀。
///
/// <para>用法一行：<c>MeleeSlashFx.Spawn(圓心, 瞄準角度, 判定半徑, 扇形半角, 樣式, 是否鏡像)</c>。
/// 呼叫端不必保留引用。**純視覺、不含任何傷害**——判定在 <c>PlayerController.ShootMelee</c>，
/// 兩者吃同一組圓心／半徑／角度，所以看到多大就打到多大。</para>
///
/// <para>樣式＝WeaponTable 的 <c>SlashStyle</c>（0＝不畫，走舊的「揮擊時播 HitEffectID」）：
/// 1＝血月（血紅三爪）。要加新樣式在 <see cref="ApplyStyle"/> 加一個 case（換四個顏色；形狀要不同就得改 shader）。</para>
///
/// <para><c>flip</c>＝左右爪交替：掃動方向反過來（由上往下／由下往上），連打有節奏。</para>
///
/// <para>時間吃 <c>Time.deltaTime</c>：開面板暫停時刀光一起凍住（正確——它屬於那一刀的畫面）。</para>
/// </summary>
[DisallowMultipleComponent]
public class MeleeSlashFx : MonoBehaviour
{
    // ── 節奏（秒）：與 shader 的 _Sweep/_Hold/_FadeTime 同一組數字，作者看過預覽後拍板 ──
    const float SweepSeconds = 0.10f;   // 三道爪痕從扇形一側掃到另一側
    const float HoldSeconds  = 0.03f;   // 掃完停一下
    const float FadeSeconds  = 0.12f;   // 淡出（爪痕收細＋變暗）

    /// <summary>
    /// 排序：表演層、比 VfxManager 全域預設（22000）低一點，讓**命中噴血蓋在刀光上面**。
    /// 已登記在 <c>MapDepthSort.cs</c> 檔頭的排序配置表。
    /// </summary>
    public const int SortingOrder = 21990;

    static Sprite _sharedSprite;
    static Shader _shader;

    Material _mat;
    float _t;

    /// <param name="center">揮擊圓心（＝判定圓心，玩家用 MuzzleWorldPos）。</param>
    /// <param name="aimDeg">瞄準方向（度，0＝右、逆時針為正）。</param>
    /// <param name="radius">判定半徑（世界單位，已含 BulletScale）。</param>
    /// <param name="halfAngleDeg">扇形半角（度）。</param>
    /// <param name="style">WeaponTable SlashStyle（≤0 不生成）。</param>
    /// <param name="flip">true＝掃動方向反過來（左右爪交替）。</param>
    public static MeleeSlashFx Spawn(Vector2 center, float aimDeg, float radius, float halfAngleDeg, int style, bool flip)
    {
        if (style <= 0 || radius <= 0.01f) return null;
        if (_shader == null) _shader = Resources.Load<Shader>("Shaders/MeleeSlash");   // 專案慣例：Shader.Find 在 build 裡找不到沒人引用的 shader
        if (_shader == null)
        {
            Debug.LogWarning("[MeleeSlashFx] 載不到 Resources/Shaders/MeleeSlash，略過刀光。");
            return null;
        }

        var go = new GameObject("MeleeSlashFx");
        go.transform.position = center;
        go.transform.rotation = Quaternion.Euler(0f, 0f, aimDeg);
        // sprite 的 native 尺寸＝1 世界單位 ⇒ localScale＝直徑。**不壓扁**：判定是世界座標的正圓（OverlapCircle），
        // 刀光要跟判定重合，所以不做地面特效那種俯視角 0.5 壓扁。
        float d = radius * 2f;
        go.transform.localScale = new Vector3(d, d, 1f);

        var fx = go.AddComponent<MeleeSlashFx>();
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSharedSprite();
        sr.sortingOrder = SortingOrder;
        fx._mat = new Material(_shader);
        sr.material = fx._mat;
        fx._mat.SetFloat("_T", 0f);
        fx._mat.SetFloat("_Dir", flip ? -1f : 1f);
        fx._mat.SetFloat("_HalfAngle", Mathf.Clamp(halfAngleDeg, 1f, 180f) * Mathf.Deg2Rad);
        fx._mat.SetFloat("_Sweep", SweepSeconds);
        fx._mat.SetFloat("_Hold", HoldSeconds);
        fx._mat.SetFloat("_FadeTime", FadeSeconds);
        ApplyStyle(fx._mat, style);
        return fx;
    }

    /// <summary>依樣式換顏色。預設值（shader Properties）就是 1＝血月。</summary>
    static void ApplyStyle(Material m, int style)
    {
        switch (style)
        {
            case 1:   // 血月：血紅主體、爪尖白熱、暗紅外緣、黑色撕裂邊（作者 2026-09-30 看過預覽）
            default:
                m.SetColor("_CoreColor", new Color(1.00f, 0.92f, 0.88f, 1f));
                m.SetColor("_MainColor", new Color(0.84f, 0.09f, 0.16f, 1f));
                m.SetColor("_DarkColor", new Color(0.40f, 0.02f, 0.07f, 1f));
                m.SetColor("_RimColor",  new Color(0.07f, 0.01f, 0.03f, 1f));
                break;
        }
    }

    void Update()
    {
        if (_mat == null) { Destroy(gameObject); return; }
        _t += Time.deltaTime;
        _mat.SetFloat("_T", _t);
        if (_t >= SweepSeconds + HoldSeconds + FadeSeconds) Destroy(gameObject);
    }

    void OnDestroy()
    {
        // 執行期 new 出來的材質要自己收（不收會漏）
        if (_mat != null) Destroy(_mat);
    }

    /// <summary>1×1 純白 sprite（PPU＝1 ⇒ native 尺寸＝1 世界單位）。圖形全由 shader 畫，貼圖不被取樣。</summary>
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
