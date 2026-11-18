// 往上的光柱（2026-11-18，DipanVfx；傳送點用，說明見 readme/DIPAN_VFX.md §4c）。
// 純程序：在全白正方形上畫一道直的光（中間細核心＋外圍柔光），亮紋往上捲；底部淡入、越往上越淡。純加色。
// 程式把方塊縮放成光柱的寬高，底邊放在光池中心。
Shader "Custom/DipanLightBeam"
{
    Properties
    {
        [PerRendererData] _MainTex ("（不用，SpriteRenderer 需要）", 2D) = "white" {}
        _NoiseTex ("噪聲（程式自動指定）", 2D) = "gray" {}
        _ColorCore ("核心色", Color) = (0.78, 0.92, 1, 1)
        _ColorRim ("外圍色", Color) = (0.43, 0.69, 1, 1)
        _Intensity ("亮度", Float) = 0.8
        _Speed ("亮紋往上速度", Float) = 0.6
        _Fade ("整體淡入淡出", Range(0, 1)) = 1
        _Seed ("亂數種子", Float) = 0
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

            sampler2D _NoiseTex;
            fixed4 _ColorCore, _ColorRim;
            float _Intensity, _Speed, _Fade, _Seed, _Dim;

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
                float t = _Time.y;
                float x = (i.uv.x - 0.5) * 2.0;
                float y = i.uv.y;
                float prof = exp(-x * x * 8.0);
                float core = exp(-x * x * 40.0);
                float fade = pow(1.0 - y, 1.5) * smoothstep(0.0, 0.08, y);
                float s1 = tex2D(_NoiseTex, float2(i.uv.x * 2.0 + _Seed, y * 1.2 - t * _Speed)).r;
                float s2 = tex2D(_NoiseTex, float2(i.uv.x * 4.0, y * 2.5 - t * _Speed * 1.7 + _Seed)).g;
                float s = 0.55 + 0.45 * (s1 * 0.6 + s2 * 0.4);
                float a = (prof * 0.5 + core * 0.8) * s * fade * _Fade * i.color.a;
                float3 col = lerp(_ColorRim.rgb, _ColorCore.rgb, core);
                return float4(col * a * _Intensity * _Dim * i.color.rgb, 0);   // 純加色
            }
            ENDCG
        }
    }
    Fallback Off
}
