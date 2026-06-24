// 氛圍後處理（Built-in 算繪管線）—— 由地圖驅動，型別寫在 MapsTable.csv 的 Atmosphere 欄。
// _Mode 由 AtmosphereController 依當前地圖餵入：
//   2 = 幽暗場景 + 打光（原 feature/atmosphere-full-soft）：周邊提亮 35%、中心暖周邊冷，看得到美術。
//   3 = 噩夢場景 + 打光（原 feature/atmosphere-full）：周邊近全黑、統一冷色，最壓迫。
// （type 1「正常」由控制器直接 passthrough，不會用到本 shader。）
// 兩種模式的提燈光圈半徑相同，由控制器的 _InnerR / _OuterR 餵入（呼吸由控制器處理）。
Shader "Custom/Atmosphere"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _PlayerPos ("Player Viewport", Vector) = (0.5, 0.5, 0, 0)
        _Aspect ("Aspect (w/h)", Float) = 1.777
        _InnerR ("Inner Radius", Float) = 0.13
        _OuterR ("Outer Radius", Float) = 0.28
        _Mode ("Mode (2=dim,3=nightmare)", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _PlayerPos;
            float _Aspect, _InnerR, _OuterR, _Mode;

            // 暈影固定參數（兩模式共用）
            static const float VigStart = 0.45;
            static const float VigEnd   = 0.95;
            static const float VigDark  = 0.85;

            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                // 提燈光圈係數 v：近玩家=1、遠處=0
                float2 d = i.uv - _PlayerPos.xy;
                d.x *= _Aspect;
                float v = 1.0 - smoothstep(_InnerR, _OuterR, length(d));

                // 暈影（四角壓黑，兩模式相同）
                float2 vc = i.uv - 0.5;
                vc.x *= _Aspect;
                float vig = saturate(1.0 - smoothstep(VigStart, VigEnd, length(vc)) * VigDark);

                if (_Mode > 2.5)
                {
                    // ── type 3：噩夢場景 + 打光 ──
                    col.rgb *= lerp(0.06, 1.0, v);                 // 周邊近全黑
                    col.rgb *= vig;
                    float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                    col.rgb = lerp(col.rgb, lum.xxx, 0.55);        // 統一去飽和
                    col.rgb *= float3(0.78, 0.86, 1.02) * 0.82;    // 統一冷色 + 壓暗
                }
                else
                {
                    // ── type 2：幽暗場景 + 打光 ──
                    col.rgb *= lerp(0.35, 1.0, v);                 // 周邊提亮到 35%
                    col.rgb *= vig;
                    float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                    float desat = lerp(0.60, 0.30, v);             // 遠處更灰、近處留色
                    col.rgb = lerp(col.rgb, lum.xxx, desat);
                    float3 tint = lerp(float3(0.72, 0.84, 1.05),   // 遠：冷青藍
                                       float3(1.06, 0.98, 0.84),   // 近：暖光（提燈感）
                                       v);
                    col.rgb *= tint * 0.85;
                }

                return col;
            }
            ENDCG
        }
    }
}
