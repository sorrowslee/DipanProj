// 雷射「光束本體」用的全參數化加色（Additive）Unlit Shader。
// 外型 100% 由參數控制（不需要貼圖）：截面、縱向能量波帶、雜訊、白熱核心、脈動、流動全來自數字 + uv + _Time。
// 一個「雷射種類(BeamStyle)」= 一組這裡的參數值；換種類只是換數字 → 不用產圖、加第 11 種只要再給一組數字。
//
// 約定：mesh 的 uv.x = 沿光束的世界長度（單位：世界單位）、uv.y = 橫向 0~1（0/1=邊緣、0.5=中心）。
// 顏色由頂點色（mesh vertex color = BeamColor）帶入，_TintColor 維持白色不另外染。
// 純黑底在 Blend One One 下自動去背發光。
Shader "Custom/AdditiveBeam"
{
    Properties
    {
        _TintColor ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity 整體亮度", Float) = 1.3
        _EdgeStart ("EdgeStart 截面實心比例(越大越像實心粗光)", Range(0,1)) = 0.5

        _CoreWidth ("CoreWidth 白熱核心寬度", Range(0,1)) = 0.4
        _CoreWhiteness ("CoreWhiteness 核心趨白程度", Range(0,1)) = 0.7

        _FlowSpeed ("FlowSpeed 波帶流動速度", Float) = 1.5
        _BandFreq ("BandFreq 波帶密度(每世界單位)", Float) = 0.8
        _BandDepth ("BandDepth 波帶明暗深度(0=均勻無波)", Range(0,1)) = 0.45
        _BandSharp ("BandSharp 波帶銳利度(1平滑/高=能量包/更高=虛線)", Float) = 2.5

        _NoiseAmt ("NoiseAmt 雜訊量(電漿/閃電)", Range(0,1)) = 0
        _NoiseSpeed ("NoiseSpeed 雜訊翻騰速度", Float) = 0

        _FlickerStrength ("FlickerStrength 脈動幅度", Range(0,0.5)) = 0.07
        _FlickerSpeed ("FlickerSpeed 脈動速度", Float) = 11
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend One One
        Cull Off
        ZWrite Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                fixed4 color : COLOR;
            };

            fixed4 _TintColor;
            float _Intensity, _EdgeStart, _CoreWidth, _CoreWhiteness;
            float _FlowSpeed, _BandFreq, _BandDepth, _BandSharp;
            float _NoiseAmt, _NoiseSpeed, _FlickerStrength, _FlickerSpeed;

            float hash11(float p) { return frac(sin(p * 12.9898) * 43758.5453123); }
            float vnoise(float x)
            {
                float i = floor(x); float f = frac(x);
                float u = f * f * (3.0 - 2.0 * f);
                return lerp(hash11(i), hash11(i + 1.0), u);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float u = i.uv.x;                       // 沿光束世界長度
                float vc = abs(i.uv.y - 0.5) * 2.0;     // 0 中心 → 1 邊緣

                // 截面：到 _EdgeStart 都全亮、之後淡出到邊緣（視覺寬度≈命中寬度，所見即所得）
                float cross = 1.0 - smoothstep(_EdgeStart, 1.0, vc);

                // 縱向能量波帶（依 _Time 流動）
                float phase = u * _BandFreq - _Time.y * _FlowSpeed;
                float w = 0.5 + 0.5 * sin(phase * 6.2831853);
                w = pow(saturate(w), max(0.01, _BandSharp));         // 1=平滑波；高=能量包；更高≈虛線
                float bands = 1.0 - _BandDepth * (1.0 - w);          // depth 0 → 恆為 1（均勻）

                // 雜訊（電漿/閃電才開）
                if (_NoiseAmt > 0.001)
                {
                    float ns = vnoise(u * 3.0 + _Time.y * _NoiseSpeed);
                    bands *= (1.0 - _NoiseAmt * ns);
                }

                float env = cross * bands;                           // 亮度包絡（波動在這層 → 核心也看得到）

                // 白熱核心：中心趨白、邊緣由頂點色(BeamColor)染色
                float core = saturate(1.0 - smoothstep(0.0, _CoreWidth, vc)) * _CoreWhiteness;
                fixed3 hue = lerp(i.color.rgb * _TintColor.rgb, fixed3(1, 1, 1), core);

                // 整體微脈動（呼吸感，與位置無關）
                float flicker = 1.0 + _FlickerStrength * sin(_Time.y * _FlickerSpeed);

                float bright = env * i.color.a * _Intensity * flicker;
                return fixed4(hue * bright, 1);
            }
            ENDCG
        }
    }
}
