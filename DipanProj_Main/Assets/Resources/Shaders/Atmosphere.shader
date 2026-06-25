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
//  10 = 山頂狂風（陰冷暴風：冷灰調 + 翻騰白霧 + 不規則橫向風絲 + 陣風時強時弱）
// 提燈光圈（type 2/3/9）半徑由控制器的 _InnerR / _OuterR 餵入並做油燈式呼吸；
// UV 位移：熱浪（4/5/6）、水下折射（7/8/9）、山頂狂風吹拂（10）——皆以滾動正弦位移取樣 UV（_Time 驅動，無需貼圖）。
Shader "Custom/Atmosphere"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _PlayerPos ("Player Viewport", Vector) = (0.5, 0.5, 0, 0)
        _Aspect ("Aspect (w/h)", Float) = 1.777
        _InnerR ("Inner Radius", Float) = 0.13
        _OuterR ("Outer Radius", Float) = 0.28
        _Mode ("Mode (2=dim,3=nightmare,4=noon,5=ember,6=dust,7=shallow,8=deep,9=deepHorror,10=wind)", Float) = 2
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

            float hash11(float n) { return frac(sin(n) * 43758.5453); }

            // 不規則橫向風絲（type 10 山頂狂風用）：把畫面壓成許多細橫帶，每帶隨機相位/速度，
            // 沿 x 切段、用雜湊隨機決定哪些段有風絲（並非整條線），段內做頭亮尾淡的 dash。
            // 多呼叫幾層不同 scale/speed 疊起來 → 散亂、不規律、有快有慢的暴風感。
            float windStreak(float2 uv, float t, float scale, float speed, float seed)
            {
                float y = uv.y * scale + seed;
                float lane = floor(y);
                float fy = frac(y);
                float lr = hash11(lane * 1.7 + seed);              // 每帶亂數
                float x = uv.x + t * speed * (0.7 + lr * 0.7);     // 捲動，速度逐帶不同
                float seg = x * (2.0 + lr * 3.0);                  // 沿 x 切段，段長逐帶不同
                float segId = floor(seg);
                float fx = frac(seg);
                float on = step(0.55, hash11(segId * 3.1 + lane * 7.3 + seed)); // 約 45% 段有風絲
                float dash = on * smoothstep(0.0, 0.08, fx) * (1.0 - smoothstep(0.15, 0.9, fx));
                float laneMask = 1.0 - smoothstep(0.0, 0.5, abs(fy - 0.5)); // 只在帶中央亮
                return dash * laneMask * (0.4 + lr * 0.6);
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                // UV 位移：山頂狂風=隨陣風的水平吹拂；炎熱=熱浪（快、幅度小）；海洋=水下折射（慢、幅度略大）。
                float2 uv = i.uv;
                if (_Mode > 9.5)
                {
                    // 山頂狂風吹拂（10）：主要水平、隨陣風時強時弱
                    float t = _Time.y;
                    float gust = 0.6 + 0.4 * sin(t * 0.6) * sin(t * 0.23 + 1.3);
                    uv.x += sin(uv.y * 9.0 + t * 5.0) * 0.0016 * gust;
                    uv.y += sin(uv.x * 7.0 + t * 3.0) * 0.0006;
                }
                else if (_Mode > 6.5)
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

                if (_Mode > 9.5)
                {
                    // ── type 10：山頂狂風（陰冷暴風）──
                    float tw = _Time.y;
                    // 陣風包絡：兩個不同步的慢波相乘 → 風一陣一陣、有強有弱（0.35~1）
                    float gust = saturate(0.35 + 0.65 * (0.5 + 0.5 * sin(tw * 0.7)) * (0.5 + 0.5 * sin(tw * 0.31 + 2.0)));

                    // 暴風冷灰調（不要太亮太乾淨，偏陰天/風雪）
                    float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                    col.rgb = lerp(col.rgb, lum.xxx, 0.30);          // 去飽和
                    col.rgb *= float3(0.86, 0.90, 0.98);            // 冷灰藍
                    col.rgb = col.rgb * 0.95 + 0.02;                 // 壓對比、輕微提亮

                    // 翻騰白霧：兩側更濃、隨時間翻滾、隨陣風增強（暴風感主來源）
                    float2 hc = i.uv - 0.5; hc.x *= _Aspect;
                    float edge = smoothstep(0.28, 0.85, length(hc));
                    float churn = 0.55 + 0.45 * sin(i.uv.y * 7.0 - tw * 2.2 + sin(i.uv.x * 4.0 + tw * 1.3) * 1.5);
                    float fog = saturate(edge * churn) * (0.45 + 0.55 * gust);
                    col.rgb = lerp(col.rgb, float3(0.82, 0.86, 0.93), fog * 0.7);

                    // 不規則風絲：兩層 hash 打散的橫向短截線，速度不同，隨陣風增強
                    float w = windStreak(i.uv, tw, 55.0, 1.4, 0.0)
                            + windStreak(i.uv, tw, 90.0, 2.2, 13.7) * 0.7;
                    col.rgb += w * (0.4 + 0.6 * gust) * 0.07 * float3(0.92, 0.95, 1.0);

                    col.rgb = saturate(col.rgb);
                }
                else if (_Mode > 8.5)
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
                    col.rgb += smoothstep(0.6, 1.0, i.uv.y) * float3(0.03, 0.06, 0.06); // 頂部陽光（淡）
                    // 焦散光網：三道不同斜向正弦干涉，再銳化成細光絲（避免格狀方塊），整體很淡、緩慢飄動。
                    float2 p = i.uv * float2(_Aspect, 1.0);
                    float t = _Time.y * 0.5;
                    float c = sin(p.x * 14.0 + p.y *  8.0 + t * 1.3)
                            + sin(p.x * -9.0 + p.y * 16.0 - t * 1.1)
                            + sin(p.x * 11.0 - p.y * 13.0 + t * 0.9);
                    c = saturate(c * 0.3333);
                    c = pow(c, 3.0);                                  // 銳化成細光絲
                    col.rgb += c * float3(0.05, 0.09, 0.10);         // 淡青色焦散
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
