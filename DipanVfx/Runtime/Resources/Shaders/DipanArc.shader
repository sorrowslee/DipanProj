// 電弧（2026-11-18，DipanVfx；說明見 readme/DIPAN_VFX.md §4d）。
// BeamVisual 每幀把每條電弧鋪成一條細帶：uv.x＝這條電弧從頭到尾 0~1、uv.y＝橫向 −1~+1；
// 頂點色 a＝這一點的亮度（螺旋繞到光柱後面時變暗、兩端收尖）。純加色。
Shader "Custom/DipanArc"
{
    Properties
    {
        _Color ("電弧色", Color) = (0.75, 0.9, 1, 1)
        _Intensity ("亮度", Float) = 1.6
        _Fade ("整體淡入淡出", Range(0, 1)) = 1
        _Dim ("整體壓暗 (1=原樣)", Float) = 1
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
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            float _Intensity, _Fade, _Dim;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
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
                float a = i.uv.y;
                float core = exp(-a * a * 14.0);                       // 白熱細芯
                float glow = exp(-a * a * 2.5) * (1.0 - a * a) * 0.45; // 帶色外暈，網格邊收到 0
                float k = i.color.a * _Intensity * _Fade * _Dim;
                float3 col = (_Color.rgb * glow + lerp(_Color.rgb, float3(1, 1, 1), 0.7) * core) * k;
                return float4(max(col, 0), 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
