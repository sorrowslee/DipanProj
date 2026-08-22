// 回憶特效（泛黃老照片 ＋ 柔邊暈影）——劇情演出期間的「持續型」全螢幕後處理。
//
// 與 EyeOpen / IllusionShatter / Mosaic（一次性過場，播 N 秒就結束）不同：
// 這張是「整段演出一直掛著」的持續效果，語意同 Atmosphere，所以走 MemoryFxController 的常駐 blit。
//
// 效果由外到內四層（強度都由 _Amount 0~1 統一淡入淡出，0＝完全原畫面）：
//   1. 泛黃：去飽和後往暖褐色偏（老照片/相紙氧化）
//   2. 柔邊：越靠畫面邊緣越模糊（4-tap 十字模糊，靠 _BlurPx 控半徑）
//   3. 暈影：邊緣壓暗 ＋ 往中心提亮一點（相紙邊緣氧化 + 中央曝光）
//   4. 顆粒：極輕的靜態雜訊，避免整片死板
//
// ⚠ Linear 色彩空間下疊色比直覺重（見 readme/PROBLEMS.md E11）：這裡的參數是「已經放輕過」的，
//    要調濃淡優先動 MemoryFxController 的 Sepia/Vignette 常數，不要在 shader 裡硬加。
Shader "Hidden/Dipan/MemoryFx"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float _Amount;      // 總強度 0~1（淡入淡出用）
            float _Sepia;       // 泛黃強度 0~1
            float _Desat;       // 去飽和 0~1
            float _VigStart;    // 暈影起始半徑（0~1，離中心多遠開始壓暗）
            float _VigPower;    // 暈影邊緣壓暗量 0~1
            float _BlurPx;      // 邊緣最大模糊半徑（像素）
            float _Grain;       // 顆粒強度 0~1
            float4 _Tint;       // 老照片的暖褐色
            float _Letterbox;   // 上下黑邊各佔畫面高度的比例（0＝沒有）。已含淡入淡出

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                // 螢幕比例修正後的「離中心距離」（0=中心, ~1=邊角）
                float2 d = i.uv - 0.5;
                d.x *= _ScreenParams.x / max(1.0, _ScreenParams.y);
                float r = saturate(length(d) * 1.55);

                // 邊緣越外圈越模糊：把模糊半徑乘上 r 的平方（中央保持銳利）
                float edge = saturate((r - _VigStart) / max(0.0001, 1.0 - _VigStart));
                float blur = _BlurPx * edge * edge * _Amount;

                float2 tx = _MainTex_TexelSize.xy * blur;
                fixed4 col = tex2D(_MainTex, i.uv);
                if (blur > 0.01)
                {
                    fixed4 b = col * 0.4;
                    b += tex2D(_MainTex, i.uv + float2( tx.x, 0)) * 0.15;
                    b += tex2D(_MainTex, i.uv + float2(-tx.x, 0)) * 0.15;
                    b += tex2D(_MainTex, i.uv + float2(0,  tx.y)) * 0.15;
                    b += tex2D(_MainTex, i.uv + float2(0, -tx.y)) * 0.15;
                    col = b;
                }

                // 泛黃：去飽和 → 往暖褐色偏
                float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                float3 gray = lerp(col.rgb, lum.xxx, _Desat);
                float3 sepia = gray * _Tint.rgb * (0.85 + lum * 0.45);
                float3 rgb = lerp(gray, sepia, _Sepia);

                // 暈影：邊緣壓暗、中央輕微提亮
                float vig = 1.0 - _VigPower * edge * edge;
                rgb *= vig;
                rgb *= 1.0 + 0.06 * (1.0 - r);

                // 顆粒（靜態，不隨幀跳動，避免閃爍）
                float g = hash21(i.uv * _ScreenParams.xy * 0.5) - 0.5;
                rgb += g * _Grain;

                col.rgb = lerp(tex2D(_MainTex, i.uv).rgb, rgb, _Amount);

                // 上下黑邊（電影過場感）。刻意做在後處理而不是 UI：
                // 與場景明暗完全無關，在全黑的幽暗地圖上也一眼看得出「進入過場」。
                // 邊緣留一點點羽化，避免硬邊在低解析度下抖動。
                if (_Letterbox > 0.0005)
                {
                    float soft = 0.004;
                    float bar = min(smoothstep(_Letterbox - soft, _Letterbox + soft, i.uv.y),
                                    smoothstep(_Letterbox - soft, _Letterbox + soft, 1.0 - i.uv.y));
                    col.rgb *= bar;
                }

                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
