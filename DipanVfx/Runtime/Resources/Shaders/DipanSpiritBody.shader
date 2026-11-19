// 靈獸身體（2026-11-19，DipanVfx；電龍、黑龍等，說明見 readme/DIPAN_VFX.md §4e）。
// _Opaque＝0：純加色（電龍）；＝1：遮擋（黑龍：貼圖 a 會蓋住地板，柔光改成貼著外緣的一圈輪廓光）。
// _SmokeAmount＞0：身體外緣往尾巴流的黑氣（alpha 混合，畫在身體底下）。
// SpiritBeastVisual 沿身體鋪一條帶子：
//   uv0.x＝沿身體的貼圖座標（身長 ÷ 一節貼圖長；貼圖 Wrap U＝Repeat ⇒ 一節一節無縫接下去）
//   uv0.y＝橫向 −1~+1（網格邊；網格比可見寬多留了柔光的空間），**+1 一律是背**（程式依游動方向決定哪側是背）
//   頂點色 a＝這一段的亮度（尾巴淡出）
// 貼圖：左右無縫、上緣＝背鬃、下緣＝腹甲；rgb＝顏色、a＝亮度（純黑底轉來的發光圖）。
Shader "Custom/DipanSpiritBody"
{
    Properties
    {
        _MainTex ("身體貼圖（程式指定）", 2D) = "black" {}
        _NoiseTex ("噪聲（程式自動指定）", 2D) = "gray" {}
        _Opaque ("遮擋 (0=加色 1=蓋住地板)", Range(0, 1)) = 0
        _TileLen ("一節貼圖多長（世界單位，程式算）", Float) = 1
        _SmokeAmount ("黑氣濃度", Range(0, 1)) = 0
        _SmokeColor ("黑氣色", Color) = (0.04, 0.025, 0.06, 1)
        _SmokeSpeed ("黑氣流速", Float) = 1.6
        _Intensity ("亮度", Float) = 1.15
        _GlowColor ("柔光色", Color) = (0.31, 0.66, 1, 1)
        _GlowAmount ("柔光量", Float) = 0.3
        _GlowWidth ("網格半寬／可見半寬", Float) = 1.5
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

            sampler2D _MainTex, _NoiseTex;
            float4 _GlowColor, _SmokeColor;
            float _Intensity, _GlowAmount, _GlowWidth, _Fade, _Dim;
            float _Opaque, _TileLen, _SmokeAmount, _SmokeSpeed;

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
                float a = i.uv.y * _GlowWidth;              // ÷ 可見半寬：|a|<1 在身體內
                float aa = abs(a);
                float g = abs(i.uv.y);
                float tail = i.color.a;                     // 尾巴淡出

                // 身體貼圖（預乘）
                float3 bodyC = 0; float bodyA = 0;
                if (aa < 1.0)
                {
                    float4 t = tex2D(_MainTex, float2(i.uv.x, 0.5 + 0.5 * a * 0.98));
                    float edge = (1.0 - smoothstep(0.85, 1.0, aa)) * tail;
                    bodyC = t.rgb * t.a * edge * _Intensity;
                    bodyA = t.a * edge * _Opaque;
                }

                // 黑氣：往尾巴流的噪聲，畫在身體底下
                float3 smokeC = 0; float smokeA = 0;
                if (_SmokeAmount > 0.001)
                {
                    float s = i.uv.x * _TileLen;            // 沿身體的世界長度
                    float tm = _Time.y;
                    float n = tex2D(_NoiseTex, float2(s * 0.55 - tm * _SmokeSpeed, a * 0.35 + tm * 0.2)).r * 0.6
                            + tex2D(_NoiseTex, float2(s * 1.3 - tm * _SmokeSpeed * 1.6, a * 0.8)).g * 0.4;
                    float reach = 1.0 - smoothstep(0.9, _GlowWidth, aa);
                    smokeA = smoothstep(0.42, 0.75, n + (1.0 - saturate(aa - 0.7)) * 0.25) * reach * _SmokeAmount * (0.4 + 0.6 * tail);
                    smokeC = _SmokeColor.rgb * smokeA;
                }

                // 柔光（加色）：加色模式＝中心往外的柔光；遮擋模式＝貼著外緣的一圈輪廓光
                float glowC = exp(-a * a * 2.5) * (1.0 - smoothstep(0.6, 1.0, g));
                float glowR = exp(-pow((aa - 1.0) / 0.3, 2.0)) * (1.0 - smoothstep(0.85, 1.0, g));
                float glow = lerp(glowC, glowR, _Opaque) * _GlowAmount * tail;

                float3 col = bodyC + (1.0 - bodyA) * smokeC;
                float A = bodyA + (1.0 - bodyA) * smokeA;
                col += _GlowColor.rgb * glow * (1.0 - A);
                col *= _Fade * _Dim;
                return float4(max(col, 0), saturate(A * _Fade));
            }
            ENDCG
        }
    }
    Fallback Off
}
