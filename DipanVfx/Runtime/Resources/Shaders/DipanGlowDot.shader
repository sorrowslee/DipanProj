// 加色光點（2026-11-18，DipanVfx：火星、餘燼粒子用；說明見 readme/DIPAN_VFX.md）。
// 純加色（Blend One One）：只會讓底下變亮、遮不住東西（readme/PROBLEMS.md E13）。顏色＝頂點色（SpriteRenderer.color）。
Shader "Custom/DipanGlowDot"
{
    Properties
    {
        [PerRendererData] _MainTex ("光點圖", 2D) = "white" {}
        _Intensity ("亮度", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv    : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float a = tex2D(_MainTex, i.uv).a * i.color.a;
                return float4(i.color.rgb * a * _Intensity, 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
