using UnityEngine;

/// <summary>
/// 掛在主相機上，把畫面經氛圍材質做一次全螢幕 Blit（Built-in 後處理進入點）。
/// 由 <see cref="AtmosphereController"/> 自動加掛，不需手動接線。
/// </summary>
[DisallowMultipleComponent]
public class AtmosphereBlit : MonoBehaviour
{
    public Material Material;

    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (Material == null) { Graphics.Blit(src, dst); return; }
        Graphics.Blit(src, dst, Material);
    }
}
