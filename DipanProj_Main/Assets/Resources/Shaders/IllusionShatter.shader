// 破幻術（幻境崩碎回歸現實）一次性全螢幕後處理 —— Built-in 算繪管線。
// 由 IllusionShatterController 掛在主相機上做一次 Blit。視覺時間軸（_Progress 0→1）：
//   1) 定格＋暖色幻術暈染，沿 voronoi 邊緣迸出亮白裂紋（_Crack）。
//   2) 每片碎塊依 cell 亂數方向/相位錯開往外崩落、邊崩邊色散(chromatic aberration)＋翻轉，
//      碎塊消失處露出底下的白光「現實」。
//   3) 最後一道白光收尾＝全白（承接跨關載入頁）。
// 說明見 readme/MAP_ENTER_EFFECT.md 破幻術一節。
Shader "Hidden/IllusionShatter"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Progress ("Progress", Range(0,1)) = 0        // 總崩碎進度
        _Crack ("Crack", Range(0,1)) = 0              // 裂紋亮線強度
        _Aspect ("Aspect", Float) = 1.7778            // 螢幕寬高比（讓碎塊在螢幕上是方的）
        _Density ("Density", Float) = 11.0            // voronoi 密度（碎塊數量感）
        _MaxDisp ("MaxDisplace", Float) = 0.55        // 碎塊最大飛離距離（uv）
        _MaxSpin ("MaxSpin", Float) = 1.2             // 碎塊最大翻轉角（弧度）
        _CA ("Chromatic", Float) = 0.02               // 色散強度
        _VoidBright ("VoidBright", Float) = 1.0       // 露出的白光亮度
        _TintStrength ("TintStrength", Range(0,1)) = 0.35  // 幻術暖色濃度（隨崩碎淡出）
        _TintColor ("TintColor", Color) = (1.0, 0.86, 0.72, 1)  // 幻術暖色調
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
            float _Progress, _Crack, _Aspect, _Density, _MaxDisp, _MaxSpin, _CA, _VoidBright, _TintStrength;
            fixed4 _TintColor;

            // 2D 亂數（同一個 cell 每幀穩定）。
            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // IQ 兩趟 voronoi：回傳最近 cell 座標(id)、cell 中心(在 aspect-uv 空間)、到邊界距離。
            void voronoi(float2 g, out float2 id, out float2 centerAspect, out float edge)
            {
                float2 n = floor(g);
                float2 f = frac(g);

                // 第一趟：找最近特徵點。
                float2 mr = 0;      // 最近點相對位移
                float2 mg = 0;      // 最近點所在 cell 偏移
                float md = 8.0;
                for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    float2 o = float2(i, j);
                    float2 r = o + hash2(n + o) - f;
                    float d = dot(r, r);
                    if (d < md) { md = d; mr = r; mg = o; }
                }
                id = n + mg;
                centerAspect = (n + mg + hash2(n + mg)) / _Density;   // 轉回 aspect-uv 空間

                // 第二趟：到最近 cell 邊界的距離（相鄰特徵點連線中垂線的最小距）。
                md = 8.0;
                for (int j2 = -2; j2 <= 2; j2++)
                for (int i2 = -2; i2 <= 2; i2++)
                {
                    float2 o = mg + float2(i2, j2);
                    float2 r = o + hash2(n + o) - f;
                    float2 diff = r - mr;
                    if (dot(diff, diff) > 0.00001)
                        md = min(md, dot(0.5 * (mr + r), normalize(diff)));
                }
                edge = md;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 aspectUV = float2(uv.x * _Aspect, uv.y);   // 讓 cell 在螢幕上是方的
                float2 g = aspectUV * _Density;

                float2 id, centerAspect; float edge;
                voronoi(g, id, centerAspect, edge);

                // 每片碎塊的亂數：方向角＋崩落相位（錯開起始時間，碎塊才不會同時飛走）。
                float2 rnd = hash2(id + 3.17);
                float ang = rnd.x * 6.28318;
                float phase = rnd.y;
                const float SPREAD = 0.55;   // 相位錯開幅度（越大越參差）
                float cp = saturate(_Progress * (1.0 + SPREAD) - phase * SPREAD);  // 這片的本地進度 0..1

                // cell 中心轉回一般 uv（除掉 aspect）。
                float2 centerUV = float2(centerAspect.x / _Aspect, centerAspect.y);

                // 崩落：碎塊沿 dir 加速飛離＋繞中心翻轉；取樣座標反向位移＝畫面上碎塊在移動。
                float2 dir = float2(cos(ang), sin(ang));
                float2 disp = dir * (cp * cp) * _MaxDisp;
                float spin = (rnd.x - 0.5) * 2.0 * cp * _MaxSpin;
                float sn = sin(spin), cs = cos(spin);
                float2 rel = uv - centerUV;
                rel = float2(rel.x * cs - rel.y * sn, rel.x * sn + rel.y * cs);
                float2 sampUV = centerUV + rel - disp;

                // 色散：RGB 各偏一點（隨崩落加大）。
                float2 caOff = dir * cp * _CA;
                float3 shard;
                shard.r = tex2D(_MainTex, saturate(sampUV + caOff)).r;
                shard.g = tex2D(_MainTex, saturate(sampUV)).g;
                shard.b = tex2D(_MainTex, saturate(sampUV - caOff)).b;

                // 幻術暖色暈染：崩碎前最濃、隨 _Progress 淡出。
                float tintAmt = (1.0 - _Progress) * _TintStrength;
                shard = lerp(shard, shard * _TintColor.rgb, tintAmt);

                // 碎塊在自身進度末段淡出；空出處露白光「現實」。
                float shardAlpha = 1.0 - smoothstep(0.72, 1.0, cp);
                float3 voidCol = float3(1, 1, 1) * _VoidBright;
                float3 col = lerp(voidCol, shard, shardAlpha);

                // 裂紋亮線：沿邊界、崩碎前段最亮（引導「先裂再碎」）。
                float vein = 1.0 - smoothstep(0.0, 0.045, edge);
                float crackGlow = vein * _Crack * saturate(cp * 4.0) * shardAlpha;
                col += crackGlow * float3(1.0, 0.97, 0.9);

                // 收尾：接近全滿時強制壓成全白，跟載入頁無縫銜接。
                col = lerp(col, voidCol, smoothstep(0.86, 1.0, _Progress));

                return fixed4(saturate(col), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
