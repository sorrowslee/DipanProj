// 傳送點光池（2026-11-18，DipanVfx；說明見 readme/DIPAN_VFX.md §4c）。
// 純程序：畫在一張全白正方形上，程式把它壓成橢圓（俯視角的地面）。
//   深色池身（會蓋住地板，像一灘深水）＋發光細邊（亮度沿邊流動）＋往外擴的漣漪＋緩慢旋轉的漩渦絲＋中心光＋外圈光暈。
// 混合：premultiplied alpha（池身 alpha、發光部分加色）。
Shader "Custom/DipanPortalPool"
{
    Properties
    {
        [PerRendererData] _MainTex ("（不用，SpriteRenderer 需要）", 2D) = "white" {}
        _NoiseTex ("噪聲（程式自動指定）", 2D) = "gray" {}
        _ColorCore ("中心色", Color) = (0.78, 0.92, 1, 1)
        _ColorRim ("邊與漣漪色", Color) = (0.43, 0.69, 1, 1)
        _ColorDeep ("池身色", Color) = (0.055, 0.1, 0.19, 1)
        _Intensity ("亮度", Float) = 1.1
        _Opacity ("池身不透明度", Range(0, 1)) = 0.6
        _Swirl ("漩渦轉速", Float) = 0.22
        _Twist ("漩渦扭曲", Float) = 7
        _RippleFreq ("漣漪密度", Float) = 2
        _RippleSpeed ("漣漪速度", Float) = 0.45
        _Breathe ("亮度呼吸幅度", Range(0, 0.5)) = 0.08
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
            fixed4 _ColorCore, _ColorRim, _ColorDeep;
            float _Intensity, _Opacity, _Swirl, _Twist, _RippleFreq, _RippleSpeed, _Breathe, _Fade, _Seed, _Dim;

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
                float2 p = (i.uv - 0.5) * 2.0;
                float r = length(p) / 0.82;                       // 池緣在 r=0.92，外面留給光暈
                float ang = atan2(p.y, p.x) / 6.2831853;          // -0.5～0.5；乘整數再取噪聲，接縫處連續

                float sw  = tex2D(_NoiseTex, float2(ang * 3.0 + r * _Twist * 0.15 - t * _Swirl + _Seed, r * 0.8 - t * 0.05)).r;
                float sw2 = tex2D(_NoiseTex, float2(ang * 4.0 + r * _Twist * 0.10 - t * _Swirl * 0.7, r * 1.2 + t * 0.04 + _Seed)).g;

                float body = 1.0 - smoothstep(0.78, 1.0, r);
                float rim = exp(-pow((r - 0.92) / 0.045, 2.0)) * (0.5 + 0.8 * sw);
                float rr = frac(r * _RippleFreq - t * _RippleSpeed);
                float rip = smoothstep(0.0, 0.12, rr) * (1.0 - smoothstep(0.12, 0.3, rr)) * body * smoothstep(0.1, 0.4, r);
                float center = exp(-r * r * 16.0) * 0.7;
                float streak = smoothstep(0.55, 0.75, sw * 0.6 + sw2 * 0.4) * body * (0.2 + 0.8 * r);
                float halo = exp(-pow(max(r - 0.92, 0.0) / 0.12, 2.0)) * step(0.92, r) * 0.35;

                float breathe = 1.0 + _Breathe * sin(t * 1.3 + _Seed);
                float3 em = _ColorRim.rgb * (rim + halo + rip * 0.3 + streak * 0.35)
                          + _ColorCore.rgb * (center + streak * 0.25);
                float bodyA = body * _Opacity * _Fade * i.color.a;
                float3 rgb = _ColorDeep.rgb * bodyA + em * _Intensity * breathe * _Fade * i.color.a;
                return float4(rgb * _Dim * i.color.rgb, bodyA);
            }
            ENDCG
        }
    }
    Fallback Off
}
