using UnityEngine;

/// <summary>
/// 掛在主相機上，把畫面經氛圍材質做一次全螢幕 Blit（Built-in 後處理進入點）。
/// 由 <see cref="AtmosphereController"/> 自動加掛，不需手動接線。
///
/// ── Bloom 前置 pass（2026-09-02 加，只給室內系 mode 16/17）──
/// <see cref="BloomEnabled"/> 為 true 時，主 Blit 之前先跑兩個降解析度的小 pass
/// （<c>Custom/AtmosphereBloom</c>：逐級 1/2 → 1/4 抽亮部 → 1/8 模糊），把結果當 <c>_BloomTex</c> 餵給主材質。
/// **關掉時完全不經過**——其餘 17 種氛圍的成本與加這功能之前一模一樣。
/// 為什麼 bloom 不做在主 shader 裡：見那支 shader 的檔頭（半徑大而 tap 數不夠＝重影，PROBLEMS J5）。
/// </summary>
[DisallowMultipleComponent]
public class AtmosphereBlit : MonoBehaviour
{
    public Material Material;

    /// <summary>Bloom 前置材質（<c>Custom/AtmosphereBloom</c>）。由 AtmosphereController 指派。</summary>
    public Material BloomMaterial;

    /// <summary>這一幀要不要跑 bloom 前置 pass（由 AtmosphereController 依當前 mode 決定）。</summary>
    public bool BloomEnabled;

    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        // 角色環境融合要量「後處理之後」的最終畫面（一張圖進場後量兩次，其餘幀只是一個 bool 比較）。
        // dst 可能是 null（直接畫到螢幕）——那就先畫進暫存 RT 量完再送上螢幕。見 CharacterEnvFusion 檔頭。
        bool probe = CharacterEnvFusion.WantsProbe;
        RenderTexture probeTarget = dst;
        if (probe && dst == null)
        {
            probeTarget = RenderTexture.GetTemporary(src.width, src.height, 0, src.format);
            Render(src, probeTarget);
            CharacterEnvFusion.ProbeFrom(probeTarget);
            Graphics.Blit(probeTarget, dst);
            RenderTexture.ReleaseTemporary(probeTarget);
            return;
        }
        Render(src, dst);
        if (probe) CharacterEnvFusion.ProbeFrom(dst);
    }

    void Render(RenderTexture src, RenderTexture dst)
    {
        if (Material == null) { Graphics.Blit(src, dst); return; }

        if (!BloomEnabled || BloomMaterial == null)
        {
            // 不跑 bloom：把 _BloomTex 清成黑，避免材質還握著上一幀已經釋放的暫存 RT。
            Material.SetTexture("_BloomTex", Texture2D.blackTexture);
            Graphics.Blit(src, dst, Material);
            return;
        }

        // 逐級 2x 降採樣（抗鋸齒的關鍵，見 readme/PROBLEMS.md E27）：
        //   src →(無材質 Blit) 1/2 →(pass0: 4-tap box + 亮部抽取) 1/4 →(pass1: 9-tap 模糊) 1/8
        // 第一級刻意不帶材質——Graphics.Blit 縮到一半時硬體 bilinear 本來就會平均 2x2，
        // 等於一個**免費的 box filter**；所以「多一級」不但沒變貴，總取樣量還比單級塞 9 個 tap 低。
        int w2 = Mathf.Max(1, src.width / 2), h2 = Mathf.Max(1, src.height / 2);
        int w4 = Mathf.Max(1, w2 / 2),        h4 = Mathf.Max(1, h2 / 2);
        int w8 = Mathf.Max(1, w4 / 2),        h8 = Mathf.Max(1, h4 / 2);

        RenderTexture half = RenderTexture.GetTemporary(w2, h2, 0, src.format);
        RenderTexture quarter = RenderTexture.GetTemporary(w4, h4, 0, src.format);
        RenderTexture eighth = RenderTexture.GetTemporary(w8, h8, 0, src.format);
        half.filterMode = FilterMode.Bilinear;
        quarter.filterMode = FilterMode.Bilinear;
        eighth.filterMode = FilterMode.Bilinear;   // 放大回全螢幕時要靠雙線性自己柔化

        Graphics.Blit(src, half);                          // 免費的 2x2 box
        Graphics.Blit(half, quarter, BloomMaterial, 0);    // pass 0：4-tap box + soft-knee 亮部抽取
        Graphics.Blit(quarter, eighth, BloomMaterial, 1);  // pass 1：9-tap tent 模糊
        Material.SetTexture("_BloomTex", eighth);
        Graphics.Blit(src, dst, Material);

        RenderTexture.ReleaseTemporary(half);
        RenderTexture.ReleaseTemporary(quarter);
        RenderTexture.ReleaseTemporary(eighth);
    }
}
