// 光束的砲口／命中光球、反彈光點（2026-11-18，DipanVfx；說明見 readme/DIPAN_VFX.md §4d）。
// 純程序：在全白正方形上畫白熱核心＋帶色光暈＋旋轉放射光芒（可選十字星芒）；方塊邊緣前一定收到 0，不會看到方形。純加色。
Shader "Custom/DipanBeamOrb"
{
    Properties
    {
        [PerRendererData] _MainTex ("（不用，SpriteRenderer 需要）", 2D) = "white" {}
        _NoiseTex ("噪聲（程式自動指定）", 2D) = "gray" {}
        _ColorCore ("核心色", Color) = (1, 1, 1, 1)
        _ColorMid ("光暈色", Color) = (0.36, 0.78, 1, 1)
        _Intensity ("亮度", Float) = 1.2
        _Rays ("放射光芒數", Float) = 6
        _Star ("十字星芒", Float) = 0
        _Spin ("光芒旋轉速度", Float) = 0.6
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
            float4 _ColorCore, _ColorMid;
            float _Intensity, _Rays, _Star, _Spin, _Fade, _Seed, _Dim;

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
                float t = _Time.y;
                float2 p = (i.uv - 0.5) * 2.0;    // −1..1
                float r = length(p);
                if (r >= 1.0) return 0;
                float edge = 1.0 - smoothstep(0.6, 1.0, r);

                float core = exp(-r * r * 26.0);
                float halo = exp(-r * r * 5.0) * 0.55;

                // 放射光芒：角度上一根一根，長短隨噪聲抖動、整體慢慢轉
                float rays = 0;
                if (_Rays > 0.5)
                {
                    float ang = atan2(p.y, p.x);
                    float a1 = ang * _Rays * 0.5 + t * _Spin + _Seed;
                    float a2 = ang * (_Rays * 0.5 + 1.0) - t * _Spin * 1.6 + _Seed * 2.0;
                    float len = 0.55 + 0.45 * tex2D(_NoiseTex, float2(ang * 0.159 + _Seed, t * 0.8)).r;
                    rays = pow(abs(cos(a1)), 10.0) * 0.7 + pow(abs(cos(a2)), 16.0) * 0.4;
                    rays *= exp(-r / max(0.05, len * 0.45)) * 0.9;
                }

                // 十字星芒（鏡光反彈的閃光）
                float star = 0;
                if (_Star > 0.001)
                {
                    float sx = exp(-abs(p.y) * 38.0) * exp(-abs(p.x) * 2.6);
                    float sy = exp(-abs(p.x) * 38.0) * exp(-abs(p.y) * 2.6);
                    float2 q = float2(p.x + p.y, p.x - p.y) * 0.7071;
                    float sd = (exp(-abs(q.y) * 60.0) * exp(-abs(q.x) * 5.0) + exp(-abs(q.x) * 60.0) * exp(-abs(q.y) * 5.0)) * 0.35;
                    star = (sx + sy + sd) * _Star * (0.85 + 0.15 * sin(t * 9.0 + _Seed));
                }

                float k = _Intensity * _Fade * _Dim * i.color.a;
                float3 col = (_ColorMid.rgb * (halo + rays) + _ColorCore.rgb * (core + star)) * edge * k;
                return float4(max(col, 0), 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
