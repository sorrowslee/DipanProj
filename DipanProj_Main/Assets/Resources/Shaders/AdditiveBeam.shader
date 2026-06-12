// 雷射光束 / 光暈用的加色（Additive）Unlit Shader。
// 純黑背景的素材在此 Blend One One 下黑色自動變透明、亮處發光；
// 顏色由頂點色（LineRenderer / SpriteRenderer 的 color）與 _TintColor 相乘染色。
//
// 雷射感的來源（兩層）：
//   1) 貼圖沿長度方向的能量波帶 + LineRenderer 的 ScrollSpeed 捲動 → 「一波一波」流動。
//   2) 本 shader 的「白熱核心」(中心趨白、邊緣染色) + 微脈動呼吸 → 從平光變雷射。
//   顏色(hue)與亮度(brightness)分離，所以波動在核心也看得到、不會被白核蓋掉。
//
// 光暈(muzzle/impact)共用本 shader，但會把 _CoreWhiteness 設 0 停用白核（圓形光暈不需要橫向白條）。
Shader "Custom/AdditiveBeam"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture (R=亮度包絡)", 2D) = "white" {}
        _TintColor ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity 整體亮度", Float) = 1.3

        _CoreWidth ("Core Width 白熱核心寬度(0~1)", Range(0,1)) = 0.4
        _CoreWhiteness ("Core Whiteness 核心趨白程度", Range(0,1)) = 0.7

        _FlickerStrength ("Flicker Strength 脈動幅度", Range(0,0.5)) = 0.07
        _FlickerSpeed ("Flicker Speed 脈動速度", Float) = 11
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend One One           // 加色混合：黑=透明、亮=發光
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TintColor;
            float _Intensity;
            float _CoreWidth;
            float _CoreWhiteness;
            float _FlickerStrength;
            float _FlickerSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float env = tex.r;                          // 亮度包絡：截面×能量波帶（含捲動 → 流動）

                // 橫向位置（0=中心 1=邊緣）→ 白熱核心。光暈材質把 _CoreWhiteness 設 0 即停用。
                float vc = abs(i.uv.y - 0.5) * 2.0;
                float core = saturate(1.0 - smoothstep(0.0, _CoreWidth, vc)) * _CoreWhiteness;

                // 顏色：邊緣用頂點色×Tint 染色，核心往白熱推（顏色與亮度分離）
                fixed3 tint = i.color.rgb * _TintColor.rgb;
                fixed3 hue  = lerp(tint, fixed3(1, 1, 1), core);

                // 微脈動：整體呼吸感，與 UV 無關（不和捲動耦合，避免打架）
                float flicker = 1.0 + _FlickerStrength * sin(_Time.y * _FlickerSpeed);

                // 亮度：波動(env)保留在這裡 → 核心也看得到一波一波
                float bright = env * i.color.a * _Intensity * flicker;
                return fixed4(hue * bright, 1);
            }
            ENDCG
        }
    }
}
