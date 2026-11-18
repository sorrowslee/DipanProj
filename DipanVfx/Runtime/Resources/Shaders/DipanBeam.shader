// 光束本體（2026-11-18，DipanVfx；雷射／鏡光／氣功砲／電光的外暈，說明見 readme/DIPAN_VFX.md §4d）。
// 純程序、純加色。網格由 BeamVisual 沿路徑鋪成一條連續帶子：
//   uv0.x＝沿路徑累積的世界長度（流紋、脈衝、亮紋都靠它往前推）
//   uv0.y＝橫向 −1~+1（網格邊緣；網格比可見寬多留了外暈與脈衝鼓起的空間）
//   uv1.x＝圓頭：0＝在路徑內、1＝圓頭最外緣（跟 uv0.y 合成半徑 ⇒ 頭尾是圓的，不是平切）
// 截面：r＝到中心線的距離／可見半寬；r＝1 是光柱邊緣、r＝_PadScale 是外暈盡頭（＝網格邊）。
Shader "Custom/DipanBeam"
{
    Properties
    {
        _NoiseTex ("噪聲（程式自動指定）", 2D) = "gray" {}
        _ColorCore ("核心色", Color) = (1, 1, 1, 1)
        _ColorMid ("光柱色", Color) = (0.36, 0.78, 1, 1)
        _ColorEdge ("外暈色", Color) = (0.1, 0.35, 1, 1)
        _Intensity ("亮度", Float) = 1.2
        _Core ("核心寬（×半寬）", Float) = 0.3
        _Solid ("實心程度", Range(0, 1)) = 0.6
        _PadScale ("網格半寬／可見半寬（＝外暈延伸×脈衝空間，程式算）", Float) = 1.8
        _Flow ("流紋速度", Float) = 8
        _Streak ("流紋量", Range(0, 1)) = 0.3
        _PulseSpacing ("脈衝間距", Float) = 0
        _PulseAmt ("脈衝鼓起", Float) = 0.15
        _PulseSpeed ("脈衝速度", Float) = 12
        _EdgeNoise ("外鞘翻騰", Range(0, 1)) = 0.25
        _Chroma ("色散", Float) = 0
        _Shimmer ("亮紋", Float) = 0
        _ShimmerSpacing ("亮紋間距", Float) = 2.5
        _ShimmerSpeed ("亮紋速度", Float) = 6
        _Flicker ("閃爍", Range(0, 1)) = 0.08
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
            float4 _ColorCore, _ColorMid, _ColorEdge;
            float _Intensity, _Core, _Solid, _PadScale;
            float _Flow, _Streak, _PulseSpacing, _PulseAmt, _PulseSpeed, _EdgeNoise;
            float _Chroma, _Shimmer, _ShimmerSpacing, _ShimmerSpeed, _Flicker;
            float _Fade, _Seed, _Dim;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float2 uv1    : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float  cap : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.cap = v.uv1.x;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float s = i.uv.x;                 // 沿路徑世界長度
                float g = length(float2(i.uv.y, i.cap));   // 0 中心線 ~ 1 網格邊（含圓頭）
                if (g >= 1.0) return 0;

                // 能量脈衝：一團一團往前推，經過處光柱鼓起、變亮
                float bump = 0;
                if (_PulseSpacing > 0.001)
                {
                    float ph = frac((s - t * _PulseSpeed) / _PulseSpacing + _Seed * 0.37);
                    bump = 1.0 - abs(ph * 2.0 - 1.0);       // 0..1 三角
                    bump = bump * bump * (3.0 - 2.0 * bump); // 平滑
                    bump = bump * bump;
                }
                float w = 1.0 + _PulseAmt * bump;

                // 外鞘翻騰：噪聲往前流，把邊緣推進推出
                float n1 = tex2D(_NoiseTex, float2(s * 0.35 - t * _Flow * 0.35 + _Seed, i.uv.y * 0.6 + t * 0.15)).g;
                float r = g * _PadScale / w;
                r *= 1.0 + (n1 - 0.5) * _EdgeNoise * 0.6 * saturate(r);

                // 截面：白熱核心＋光柱本體＋外暈
                float core = exp(-(r * r) / max(1e-4, _Core * _Core) * 1.6);
                float bodyHard = saturate(1.0 - r);
                float body = lerp(bodyHard * bodyHard * bodyHard, smoothstep(1.0, 0.55, r), _Solid);
                float glow = exp(-r * r * 2.2) * 0.55;
                float edgeFade = 1.0 - smoothstep(0.55, 1.0, g);   // 外暈在網格邊緣前收乾淨
                glow *= edgeFade;
                body *= edgeFade;

                // 流紋：沿長度拉長的噪聲往前推（橫向變化快、縱向慢 ⇒ 一絲一絲的能量往前衝）
                float n2 = tex2D(_NoiseTex, float2(s * 0.12 - t * _Flow * 0.12, i.uv.y * 0.9 + _Seed)).r;
                float n3 = tex2D(_NoiseTex, float2(s * 0.3 - t * _Flow * 0.3 + 0.5, i.uv.y * 1.7 + _Seed * 1.3)).b;
                float streak = lerp(1.0, 0.35 + 1.4 * (n2 * 0.65 + n3 * 0.35), _Streak);

                // 亮紋（鏡光）：一道一道沿光束滑過的強光
                float shim = 0;
                if (_Shimmer > 0.001)
                {
                    float sp = frac((s - t * _ShimmerSpeed) / _ShimmerSpacing);
                    shim = exp(-pow((sp - 0.5) * 9.0, 2.0)) * _Shimmer;
                }

                float flick = 1.0 + _Flicker * (tex2D(_NoiseTex, float2(t * 3.1 + _Seed, 0.37)).r - 0.5) * 2.0;

                // 外暈的噪聲明暗跟著外鞘翻騰走：EdgeNoise≥0.25 跟原本一樣、0＝完全均勻（鏡光，2026-11-18）
                float edgeMod = lerp(1.0, 0.6 + 0.4 * n1, saturate(_EdgeNoise * 4.0));
                float3 col = _ColorEdge.rgb * glow * edgeMod
                           + _ColorMid.rgb * body * streak
                           + _ColorCore.rgb * core * (0.75 + 0.25 * streak + shim);
                col *= 1.0 + bump * 0.6;
                col += _ColorCore.rgb * shim * body * 0.6;

                // 色散（鏡光）：光柱邊緣一圈流動的七彩
                if (_Chroma > 0.001)
                {
                    float3 spec = 0.5 + 0.5 * cos(6.2832 * (r * 1.2 - s * 0.15 + t * 0.6 + float3(0.0, 0.33, 0.67)));
                    float band = smoothstep(0.45, 0.85, r) * (1.0 - smoothstep(0.95, 1.4, r));
                    col += spec * band * _Chroma * 0.6 * edgeFade;
                }

                col *= _Intensity * flick * _Fade * _Dim;
                return float4(max(col, 0), 0);   // 純加色
            }
            ENDCG
        }
    }
    Fallback Off
}
