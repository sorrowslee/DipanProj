// 氛圍後處理（Built-in 算繪管線）—— 由地圖驅動，型別寫在 MapsTable.csv 的 Atmosphere 欄。
// _Mode 由 AtmosphereController 依當前地圖餵入（type 1「正常」由控制器 passthrough，不會用到本 shader）：
//   2 = 幽暗場景 + 打光（周邊提亮 35%、中心暖周邊冷，看得到美術）
//   3 = 噩夢場景 + 打光（周邊近全黑、統一冷色，最壓迫）
//   4 = 烈日曝曬（過曝暖白 + 高對比 + 頂部刺眼天光 + 克制熱浪）
//   5 = 焦土餘燼（暗橙紅 + 煙塵壓暗 + 餘燼暖光底 + 克制熱浪）
//   6 = 沙塵暴（橙褐沙塵霧罩頂、降能見度與對比 + 克制熱浪）
//   7 = 淺海（青綠水色 + 頂部陽光 + 焦散光斑 + 水下折射晃動）
//   8 = 深海（深藍壓暗、低能見度、冷色去飽和 + 水下折射晃動）
//   9 = 深海 + 恐怖（深海再套潛水燈光圈：玩家周圍一圈可見、其餘近全黑）
// 提燈光圈（type 2/3/9）半徑由控制器的 _InnerR / _OuterR 餵入並做油燈式呼吸；
// 炎熱型別（4/5/6）啟用「熱浪扭曲」、海洋型別（7/8/9）啟用「水下折射晃動」：皆以滾動正弦位移取樣 UV（_Time 驅動，無需貼圖）。
Shader "Custom/Atmosphere"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _PlayerPos ("Player Viewport", Vector) = (0.5, 0.5, 0, 0)
        _Aspect ("Aspect (w/h)", Float) = 1.777
        _InnerR ("Inner Radius", Float) = 0.13
        _OuterR ("Outer Radius", Float) = 0.28
        _Mode ("Mode (2=dim,3=nightmare,4=noon,5=ember,6=dust,7=shallow,8=deep,9=deepHorror)", Float) = 2
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

            static const float VigStart = 0.45;
            static const float VigEnd   = 0.95;
            static const float VigDark  = 0.85;

            fixed4 frag (v2f_img i) : SV_Target
            {
                // UV 位移：炎熱型別=熱浪（快、幅度小）；海洋型別=水下折射（慢、幅度略大）。
                float2 uv = i.uv;
                if (_Mode > 6.5)
                {
                    // 海洋折射晃動（7/8/9）
                    float t = _Time.y;
                    float wob = sin(uv.y * 22.0 + t * 1.6) + sin(uv.x * 18.0 - t * 1.2);
                    uv.x += wob * 0.0022;
                    uv.y += sin(uv.x * 16.0 + t * 1.3) * 0.0018;
                }
                else if (_Mode > 3.5)
                {
                    // 熱浪扭曲（4/5/6）
                    float t = _Time.y;
                    float wob = sin(uv.y * 38.0 + t * 3.0) + sin(uv.y * 21.0 - t * 2.1);
                    uv.x += wob * 0.0014;
                    uv.y += sin(uv.x * 30.0 + t * 1.7) * 0.0011;
                }
                fixed4 col = tex2D(_MainTex, uv);

                // 提燈光圈係數 v（恐怖型別用）：近玩家=1、遠處=0
                float2 d = i.uv - _PlayerPos.xy;
                d.x *= _Aspect;
                float v = 1.0 - smoothstep(_InnerR, _OuterR, length(d));

                // 暈影
                float2 vc = i.uv - 0.5;
                vc.x *= _Aspect;
                float vig = saturate(1.0 - smoothstep(VigStart, VigEnd, length(vc)) * VigDark);

                if (_Mode > 8.5)
                {
                    // ── type 9：深海 + 恐怖（潛水燈光圈）──
                    col.rgb *= lerp(0.04, 1.0, v);                    // 周邊近全黑、只剩玩家一圈
                    float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                    col.rgb = lerp(col.rgb, lum.xxx, 0.50);           // 去飽和
                    col.rgb *= float3(0.32, 0.52, 0.80);             // 冷深藍
                    col.rgb *= 0.78;                                  // 壓暗
                    col.rgb = lerp(col.rgb, float3(0.02, 0.06, 0.14), 0.30); // 深藍水霧
                    col.rgb *= vig;
                    col.rgb = saturate(col.rgb);
                }
                else if (_Mode > 7.5)
                {
                    // ── type 8：深海 ──
                    float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                    col.rgb = lerp(col.rgb, lum.xxx, 0.45);           // 較去飽和
                    col.rgb *= float3(0.30, 0.50, 0.85);             // 深藍
                    col.rgb *= 0.70;                                  // 壓暗
                    col.rgb += smoothstep(0.7, 1.0, i.uv.y) * float3(0.02, 0.05, 0.08); // 殘餘頂部天光
                    col.rgb = lerp(col.rgb, float3(0.04, 0.10, 0.20), 0.25); // 深藍水霧
                    col.rgb *= lerp(0.50, 1.0, vig);                  // 重暈影
                    col.rgb = saturate(col.rgb);
                }
                else if (_Mode > 6.5)
                {
                    // ── type 7：淺海 ──
                    float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                    col.rgb = lerp(col.rgb, lum.xxx, 0.15);           // 微去飽和
                    col.rgb *= float3(0.62, 0.92, 1.02);             // 青綠水色
                    col.rgb += smoothstep(0.5, 1.0, i.uv.y) * float3(0.05, 0.10, 0.10); // 頂部陽光透下
                    // 焦散光斑：兩道滾動正弦相乘成水波光網，上方較強
                    float caust = saturate(sin(i.uv.x * 30.0 + _Time.y * 1.3) * sin(i.uv.y * 26.0 - _Time.y * 1.1));
                    col.rgb += caust * float3(0.06, 0.10, 0.10) * smoothstep(0.3, 1.0, i.uv.y);
                    col.rgb *= lerp(0.90, 1.0, vig);                  // 輕暈影
                    col.rgb = saturate(col.rgb);
                }
                else if (_Mode > 5.5)
                {
                    // ── type 6：沙塵暴 ──
                    float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                    col.rgb = lerp(col.rgb, lum.xxx, 0.35);           // 去飽和（塵霧吃色）
                    col.rgb = (col.rgb - 0.5) * 0.85 + 0.5;           // 降對比（霧化）
                    col.rgb *= float3(1.12, 0.95, 0.72);              // 暖黃褐
                    col.rgb = lerp(col.rgb, float3(0.62, 0.50, 0.34), 0.30); // 罩一層沙塵霧
                    col.rgb *= lerp(0.70, 1.0, vig);
                    col.rgb = saturate(col.rgb);
                }
                else if (_Mode > 4.5)
                {
                    // ── type 5：焦土餘燼 ──
                    float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                    col.rgb = lerp(col.rgb, lum.xxx, 0.25);           // 部分去飽和
                    col.rgb = (col.rgb - 0.5) * 1.10 + 0.5;           // 對比
                    col.rgb *= float3(1.18, 0.72, 0.50);              // 強橙紅
                    col.rgb *= 0.92;                                  // 黃昏/煙塵壓暗
                    col.rgb *= lerp(0.55, 1.0, vig);                  // 較重暗角（煙）
                    col.rgb += float3(0.05, 0.02, 0.0);               // 餘燼暖光底
                    col.rgb = saturate(col.rgb);
                }
                else if (_Mode > 3.5)
                {
                    // ── type 4：烈日曝曬 ──
                    col.rgb = (col.rgb - 0.5) * 1.18 + 0.5;           // 提高對比
                    col.rgb = col.rgb * 1.22 + 0.06;                 // 過曝拉亮 + 白色 lift
                    float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                    col.rgb = lerp(col.rgb, lum.xxx, 0.18);          // 微去飽和（曬褪色）
                    col.rgb *= float3(1.08, 1.02, 0.88);             // 暖白／琥珀
                    col.rgb += smoothstep(0.55, 1.0, i.uv.y) * 0.12; // 頂部刺眼天光
                    col.rgb *= lerp(0.85, 1.0, vig);                 // 輕暈影
                    col.rgb = saturate(col.rgb);
                }
                else if (_Mode > 2.5)
                {
                    // ── type 3：噩夢場景 + 打光 ──
                    col.rgb *= lerp(0.06, 1.0, v);
                    col.rgb *= vig;
                    float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                    col.rgb = lerp(col.rgb, lum.xxx, 0.55);
                    col.rgb *= float3(0.78, 0.86, 1.02) * 0.82;
                }
                else
                {
                    // ── type 2：幽暗場景 + 打光 ──
                    col.rgb *= lerp(0.35, 1.0, v);
                    col.rgb *= vig;
                    float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                    float desat = lerp(0.60, 0.30, v);
                    col.rgb = lerp(col.rgb, lum.xxx, desat);
                    float3 tint = lerp(float3(0.72, 0.84, 1.05), float3(1.06, 0.98, 0.84), v);
                    col.rgb *= tint * 0.85;
                }

                return col;
            }
            ENDCG
        }
    }
}
