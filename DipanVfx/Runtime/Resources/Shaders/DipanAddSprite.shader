// 靈獸的頭、爪、發光層、煙團（2026-11-19，DipanVfx；說明見 readme/DIPAN_VFX.md §4e）。預乘混合：
// 顏色＝貼圖 rgb × a × 頂點色 × 亮度；_Opaque＝0 純加色（發光圖），＝1 會蓋住地板（黑龍這類實體、煙團）。
Shader "Custom/DipanAddSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Intensity ("亮度", Float) = 1
        _Opaque ("遮擋 (0=加色 1=蓋住地板)", Range(0, 1)) = 0
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
            "CanUseSpriteAtlas"="True"
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

            sampler2D _MainTex;
            float _Intensity, _Fade, _Dim, _Opaque;

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
                float4 t = tex2D(_MainTex, i.uv);
                float k = t.a * i.color.a * _Fade;
                float3 col = t.rgb * i.color.rgb * k * _Intensity * _Dim;
                return float4(max(col, 0), saturate(k * _Opaque));
            }
            ENDCG
        }
    }
    Fallback Off
}
