// 淡出黑幕（螢幕特效 id 4）一次性全螢幕後處理 —— Built-in 算繪管線。由 FadeOutController 掛在主相機上做一次 Blit。
// _Fade 0＝原畫面、1＝全黑。刻意做成相機後處理而不是蓋一張 UI 黑幕：播完直接停 blit 就乾淨了，
// 不會留下一張要有人記得撤掉的黑幕蓋在下一張地圖上。
Shader "Hidden/ScreenFadeOut"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Fade ("Fade", Range(0,1)) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Fade;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                c.rgb *= 1.0 - saturate(_Fade);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
