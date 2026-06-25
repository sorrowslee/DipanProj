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
//  10 = 風雪（陰冷暴風：冷灰調 + 翻騰白霧 + 不規則橫向風絲 + 陣風時強時弱）
//  11 = 強風（去白霧、幾乎不調色，只留單一斜向的風絲 + 陣風）
//  12 = 綿綿細雨（陰天微冷 + 稀疏細雨絲，近垂直落下）
//  13 = 大雨（細密雨點往下落）
//  14 = 陰森森林鬼霧（畫面偏暗、陰綠冷調 + 漂移黑霧雲塊、偶爾飄來一陣濃霧）
//  15 = 電視雜訊（雪花噪點 + 掃描線 + 滾動同步條 + 偶發水平撕裂 + 灰調閃爍）
// 提燈光圈（type 2/3/9）半徑由控制器的 _InnerR / _OuterR 餵入並做油燈式呼吸；
// UV 位移：熱浪（4/5/6）、水下折射（7/8/9）、山頂風吹拂（10/11）——皆以滾動正弦位移取樣 UV（_Time 驅動，無需貼圖）。
// 風絲/雨絲共用 windStreak（雜湊打散的不規則短截線）：風絲斜向（rot2）、雨絲近垂直（rot2 約 1.4 rad）。
Shader "Custom/Atmosphere"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _PlayerPos ("Player Viewport", Vector) = (0.5, 0.5, 0, 0)
        _Aspect ("Aspect (w/h)", Float) = 1.777
        _InnerR ("Inner Radius", Float) = 0.13
        _OuterR ("Outer Radius", Float) = 0.28
        _Mode ("Mode (2..6,7..9 ocean,10 snow,11 gale,12 drizzle,13 rain,14 ghostFog,15 tvNoise)", Float) = 2
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
            // 15 種型別分支攤平在同一個 pixel shader，指令數大；提高編譯目標避免超過預設上限（洋紅錯誤）。
            #pragma target 3.5
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _PlayerPos;
            float _Aspect, _InnerR, _OuterR, _Mode;

            static const float VigStart = 0.45;
            static const float VigEnd   = 0.95;
            static const float VigDark  = 0.85;

            float hash11(float n) { return frac(sin(n) * 43758.5453); }

            // 繞原點旋轉（type 11 強風用：把風絲轉成斜向）。
            float2 rot2(float2 p, float a) { float s = sin(a), c = cos(a); return float2(c * p.x - s * p.y, s * p.x + c * p.y); }

            // 2D 白噪雜湊（type 15 電視雜訊用：雪花、撕裂）。
            float rand2(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            // 不規則風絲（type 10 風雪橫向 / type 11 強風斜向用）：把畫面壓成許多細橫帶，每帶隨機相位/速度，
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
                // UV 位移：山頂風(10/11)=隨陣風的水平吹拂；炎熱=熱浪（快、幅度小）；海洋=水下折射（慢、幅度略大）。
                float2 uv = i.uv;
                if (_Mode > 9.5 && _Mode < 11.5)
                {
                    // 山頂風吹拂（10/11）：主要水平、隨陣風時強時弱
                    float t = _Time.y;
                    float gust = 0.6 + 0.4 * sin(t * 0.6) * sin(t * 0.23 + 1.3);
                    uv.x += sin(uv.y * 9.0 + t * 5.0) * 0.0016 * gust;
                    uv.y += sin(uv.x * 7.0 + t * 3.0) * 0.0006;
                }
                else if (_Mode > 6.5 && _Mode < 9.5)
                {
                    // 海洋折射晃動（7/8/9）
                    float t = _Time.y;
                    float wob = sin(uv.y * 22.0 + t * 1.6) + sin(uv.x * 18.0 - t * 1.2);
                    uv.x += wob * 0.0022;
                    uv.y += sin(uv.x * 16.0 + t * 1.3) * 0.0018;
                }
                else if (_Mode > 14.5)
                {
                    // 電視雜訊（15）：偶發整列水平撕裂位移（取樣前做）
                    // 注意：'line' 是 HLSL 保留字，變數名用 ln。
                    float ln = floor(uv.y * 90.0);
                    float fr = floor(_Time.y * 14.0);
                    float g = rand2(float2(ln, fr));
                    uv.x += step(0.93, g) * (rand2(float2(ln, fr + 1.0)) - 0.5) * 0.05;
                }
                else if (_Mode > 3.5 && _Mode < 6.5)
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

                if (_Mode > 14.5)
                {
                    // ── type 15：電視雜訊（雪花 + 掃描線 + 滾動同步條 + 灰調閃爍；撕裂在取樣前）──
                    float t = _Time.y;
                    // 老電視灰調 + 輕微壓暗
                    float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                    col.rgb = lerp(col.rgb, lum.xxx, 0.35);
                    col.rgb *= 0.95;
                    // 掃描線（固定線數，與解析度無關以免閃爍）
                    col.rgb *= 0.90 + 0.10 * sin(i.uv.y * 540.0);
                    // 雪花雜訊：每幀變化的白噪混入
                    float fr = floor(t * 24.0);
                    float snow = rand2(i.uv * _ScreenParams.xy * 0.5 + fr);
                    col.rgb = lerp(col.rgb, snow.xxx, 0.18);
                    // 滾動同步條（一條亮帶緩慢上捲）
                    float bar = frac(i.uv.y - t * 0.25);
                    col.rgb += smoothstep(0.0, 0.06, bar) * (1.0 - smoothstep(0.08, 0.22, bar)) * 0.06;
                    // 整體輕微閃爍
                    col.rgb *= 0.97 + 0.03 * sin(t * 30.0);
                    col.rgb = saturate(col.rgb);
                }
                else if (_Mode > 13.5)
                {
                    // ── type 14：陰森森林鬼霧（畫面偏暗 + 偶爾一團黑霧緩慢橫越畫面）──
                    float t = _Time.y;
                    // 森林陰冷底調：去飽和 + 陰綠 + 壓暗 + 加深暈影
                    float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                    col.rgb = lerp(col.rgb, lum.xxx, 0.40);
                    col.rgb *= float3(0.62, 0.78, 0.66);          // 陰綠冷調
                    col.rgb *= 0.70;                               // 壓暗
                    col.rgb *= lerp(0.40, 1.0, vig);               // 加深暈影

                    // 一團黑霧：每個週期生一團，從畫面外一側緩慢飄到另一側離場（方向/高度/大小隨機），團間有空檔。
                    float period = 24.0;                          // 每團週期（秒）；越大越「偶爾」、飄越慢
                    float ci = floor(t / period);                 // 第幾團
                    float ph = frac(t / period);                  // 此團進度 0..1
                    float h1 = hash11(ci * 1.37);
                    float h2 = hash11(ci * 2.51 + 7.3);
                    float h3 = hash11(ci * 3.77 + 1.9);
                    float dir = (h1 < 0.5) ? 1.0 : -1.0;          // 左→右 / 右→左
                    float yc = 0.20 + 0.60 * h2;                  // 隨機高度
                    float cross = saturate(ph / 0.83);            // 前 83% 橫越（約 20 秒、原本的一半速度）、其餘為空檔
                    float xc = lerp((dir > 0 ? -0.6 : 1.6), (dir > 0 ? 1.6 : -0.6), cross);
                    yc += sin(ph * 3.1416) * 0.05;                // 橫越時輕微起伏
                    float rad = 0.28 + 0.12 * h3;                 // 隨機大小
                    float2 q = i.uv - float2(xc, yc);
                    float blob = 1.0 - smoothstep(rad * 0.35, rad, length(float2(q.x * 0.6, q.y * 1.3))); // 橫向橢圓霧團
                    float n = sin(i.uv.x * 7.0 + t * 0.6) * sin(i.uv.y * 6.0 - t * 0.5);                  // 邊緣翻騰
                    blob *= 0.65 + 0.35 * (0.5 + 0.5 * n);
                    blob = saturate(blob);
                    col.rgb = lerp(col.rgb, float3(0.01, 0.02, 0.015), blob * 0.85);                      // 黑霧壓黑
                    col.rgb = saturate(col.rgb);
                }
                else if (_Mode > 12.5)
                {
                    // ── type 13：大雨（細密雨點、往下落）──
                    float tr = _Time.y;
                    float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                    col.rgb = lerp(col.rgb, lum.xxx, 0.10);          // 微去飽和
                    col.rgb *= float3(0.94, 0.96, 1.0);             // 很輕的冷調
                    col.rgb *= 0.98;                                 // 陰天微壓暗
                    // 雨絲：高密度細帶、往下落（speed 負）；r*r 收成銳利雨點
                    float r = windStreak(rot2(i.uv, 1.50), tr, 150.0, -2.2, 0.0);
                    r = r * r;
                    col.rgb += r * 0.06 * float3(0.85, 0.90, 0.95);
                    col.rgb = saturate(col.rgb);
                }
                else if (_Mode > 11.5)
                {
                    // ── type 12：綿綿細雨／毛毛雨（＝type13 大雨的一半速度與一半密度）──
                    float tr = _Time.y;
                    float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                    col.rgb = lerp(col.rgb, lum.xxx, 0.10);          // 微去飽和
                    col.rgb *= float3(0.94, 0.96, 1.0);             // 很輕的冷調
                    col.rgb *= 0.98;                                 // 陰天微壓暗
                    // 同 type13 但：速度減半（-1.1）、密度減半（隨機保留約一半的雨列）
                    float2 ruv = rot2(i.uv, 1.50);
                    float r = windStreak(ruv, tr, 150.0, -1.1, 0.0);
                    r = r * r;
                    r *= step(0.5, hash11(floor(ruv.y * 150.0) * 5.1 + 3.7)); // 密度減半（對齊雨列丟掉半數）
                    col.rgb += r * 0.06 * float3(0.85, 0.90, 0.95);
                    col.rgb = saturate(col.rgb);
                }
                else if (_Mode > 10.5)
                {
                    // ── type 11：強風（只留單一斜向風絲、無白霧、幾乎不調色）──
                    float tw = _Time.y;
                    float gust = saturate(0.40 + 0.60 * (0.5 + 0.5 * sin(tw * 0.8)) * (0.5 + 0.5 * sin(tw * 0.37 + 1.5)));
                    // 兩層同一斜向角度（只差密度/速度做變化，不交叉）→ 風往同一方向猛吹。
                    float a = -0.38;  // 統一斜向角度：負值=右上吹往左下（正值則右下往左上；數字越大越斜、0=水平）
                    float w = windStreak(rot2(i.uv, a), tw, 60.0, 1.8, 0.0)
                            + windStreak(rot2(i.uv, a), tw, 82.0, 2.5, 21.3) * 0.7;
                    col.rgb += w * (0.5 + 0.5 * gust) * 0.08 * float3(0.95, 0.97, 1.0);
                    col.rgb = saturate(col.rgb);
                }
                else if (_Mode > 9.5)
                {
                    // ── type 10：風雪（陰冷暴風）──
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
