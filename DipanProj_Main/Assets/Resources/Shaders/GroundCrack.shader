// 地面龜裂（程序化，零素材）——跳躍踐踏落地時在地上炸開的裂痕。
//
// ⭐ 2026-09-18 改寫：**從「放射狀爆裂」換成「Voronoi 均勻泥塊」**。
//    作者附了乾裂泥地的參考圖：「我想要的是平均的那種龜裂，不是你做的這種中間有個洞的」。
//    第一版是「中心一個碎坑 ＋ N 條主裂往外竄 ＋ 分支」——那是玻璃/衝擊波的裂法，
//    乾裂泥地完全不同：它是**整片均勻的不規則多邊形**（泥塊收縮各自拉開），沒有中心、沒有方向性。
//    這兩種在演算法上沒有交集，所以整支重寫，不是調參數。
//
// 畫法：**jittered grid Voronoi**。把 uv 放大成格子，每格塞一個隨機特徵點，
// 對每個像素找最近(F1)與次近(F2)的特徵點——**F2−F1 接近 0 的地方就是兩塊泥板的交界**，那就是裂縫。
// 再疊一層格子更密、更細更淡的二級裂（真實泥地大塊裡面還會有小裂）。
// 三塊交會的頂點處 F2−F1 天然會寬一點，剛好就是泥裂那種 Y 型節點，不必另外做。
//
// `_Progress` 控制「裂到多遠」：只顯示半徑 ≤ 進度 的那一圈，所以落地瞬間是一個點、然後裂紋一圈圈往外蔓延。
// （這一層是唯一有「中心」的東西，而且只在 0.85 秒的演出期間看得到，裂完就是一整片均勻的龜裂。）
//
// ⚠ 用 alpha 混合（不是加色）：裂痕是**吃光的暗痕**，要能實心遮住地板。
//    加色（Blend One One）永遠做不出「不透明」，只會讓地板變亮——同 readme/GROUND_EFFECT.md
//    背景符號層踩過的那條結論。
// ⚠ Linear 色彩空間下疊色比直覺重（readme/PROBLEMS.md E11），所以這裡的 alpha 一律取保守值，
//    要更明顯優先加寬 _CrackWidth，不要加 alpha。
// ⚠ 每像素跑兩輪 3×3 Voronoi（18 次 hash）。目前一隻怪一生只跳一次、場上頂多幾道，沒問題；
//    哪天做成「可重複跳」或大量同時出現，先看這裡。
Shader "Custom/GroundCrack"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}   // 不取樣，只為了讓 SpriteRenderer 正常送資料
        _CrackColor ("裂縫顏色（暗痕）", Color) = (0.05, 0.04, 0.03, 1)
        _RimColor   ("裂縫邊緣（翻起的土）", Color) = (0.38, 0.30, 0.22, 1)
        _RingColor  ("衝擊環顏色", Color) = (0.55, 0.45, 0.32, 1)
        _Progress   ("裂開進度 0~1", Range(0,1)) = 1
        _RingProgress ("衝擊環擴散 0~1（>1 代表已散完）", Range(0,1.4)) = 0
        _Fade       ("整體淡出 0~1", Range(0,1)) = 1
        _Seed       ("亂數種子", Float) = 0
        _CellScale  ("泥塊密度（整個圓的直徑上有幾塊）", Range(3,24)) = 9
        _CrackWidth ("裂縫粗細", Range(0.005,0.3)) = 0.045
        _RimAmount  ("裂縫邊緣翻土的明顯程度", Range(0,1)) = 0.3
        _Jitter     ("泥塊形狀不規則度（0=規則蜂巢 1=很亂）", Range(0,1)) = 0.85
        _FineScale  ("二級細裂的密度倍率", Range(1,4)) = 2.4
        _FineAmount ("二級細裂的明顯程度", Range(0,1)) = 0.45
        _PlateShade ("泥塊面的明暗差（0=只畫裂縫不碰塊面）", Range(0,0.5)) = 0.10
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 3.0：fragment 裡有兩輪 3×3 的 Voronoi 迴圈，SM2.5 的指令數限制編不過。
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            fixed4 _CrackColor, _RimColor, _RingColor;
            float _Progress, _RingProgress, _Fade, _Seed;
            float _CellScale, _CrackWidth, _Jitter, _FineScale, _FineAmount, _PlateShade, _RimAmount;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float2 hash22(float2 p)
            {
                p += _Seed * 0.137;
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float hash12(float2 p)
            {
                p += _Seed * 0.211;
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Voronoi：回傳 (F2−F1) ＝「離最近的一條交界有多遠」（0 ＝正在交界上），
            // 以及最近特徵點所屬格子的編號（給泥塊面上色用）。
            float voronoiEdge(float2 p, out float2 cellId)
            {
                float2 n = floor(p);
                float2 f = frac(p);

                float f1 = 8.0, f2 = 8.0;
                cellId = n;

                [unroll] for (int j = -1; j <= 1; j++)
                {
                    [unroll] for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2(i, j);
                        float2 o = hash22(n + g);
                        // Jitter 0 ＝特徵點都在格子中心（規則蜂巢）、1 ＝滿格亂放（很不規則）
                        float2 pt = g + 0.5 + (o - 0.5) * _Jitter;
                        float d = length(pt - f);
                        if (d < f1) { f2 = f1; f1 = d; cellId = n + g; }
                        else if (d < f2) { f2 = d; }
                    }
                }
                return f2 - f1;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;     // -1~1
                float r = length(p);
                if (r > 1.0) discard;              // 圓形範圍外不畫（quad 的四角）

                // ── 主裂：均勻的不規則多邊形交界 ──
                float2 cellId;
                float e1 = voronoiEdge(i.uv * _CellScale, cellId);
                float crack = 1.0 - smoothstep(_CrackWidth * 0.35, _CrackWidth, e1);

                // ── 二級細裂：格子更密、更細更淡（大塊泥板裡面的小裂）──
                float2 cellId2;
                float e2 = voronoiEdge(i.uv * _CellScale * _FineScale + 17.3, cellId2);
                float fine = 1.0 - smoothstep(_CrackWidth * 0.25, _CrackWidth * 0.6, e2);
                crack = max(crack, fine * _FineAmount);

                // ── 泥塊面：每塊給一點點明暗差，讓它看起來是「一塊一塊翹起來的板」而不是畫在地上的線 ──
                // 預設很低（0.10）：這東西是疊在原本的地板上的，太重會變成「地上多了一坨土」。
                float plate = (hash12(cellId) - 0.5) * 2.0;    // -1~1

                // ── 邊界以「泥塊」為單位抖動，而不是一個完美的圓 ──
                // ⚠ 這一行是為了解掉「圓周上長出一圈尖刺」：裂縫線本來就會穿過圓周，
                //   拿一個正圓去淡出它們，每條被切斷的裂縫都會留下一個漸層的三角形殘影 ⇒ 看起來像海膽。
                //   改成讓**整塊泥板一起進、一起出**（每塊隨機往內或往外一點），邊界就變成自然的鋸齒狀，
                //   而且擴散時是「一塊一塊裂開」而不是一圈平滑的環。
                float rEdge = r + (hash12(cellId) - 0.5) * 0.22;

                // ── 擴散：只顯示半徑 ≤ 進度 的那一圈（落地瞬間是一個點，然後一塊塊往外裂開）──
                float edge = _Progress * 1.08;
                float reveal = 1.0 - smoothstep(edge - 0.12, edge, rEdge);
                crack *= reveal;

                // ── 衝擊環：落地瞬間往外掃一圈的塵浪，掃完就沒（純表演，與裂痕獨立）──
                float ring = 0.0;
                if (_RingProgress > 0.0001 && _RingProgress < 1.0)
                {
                    float rr = _RingProgress;
                    ring = 1.0 - smoothstep(0.0, 0.16, abs(r - rr));
                    ring *= (1.0 - rr) * 0.55;
                }

                // ── 合成（alpha 混合；先鋪塊面與亮土，再蓋暗裂痕）──
                float plateA = saturate(abs(plate) * _PlateShade) * reveal;
                fixed3 rgb = lerp(_RimColor.rgb, _CrackColor.rgb, saturate(plate * 0.5 + 0.5));
                float a = plateA;

                // 裂縫邊緣翻起的土：裂痕外圍一圈較亮的窄帶
                // 0.9（第一版）在這種密集的 Voronoi 上會讓每條縫都鑲一圈亮邊 ⇒ 整片看起來在發光。
                float rim = saturate(crack * 2.2) - crack;
                rim = saturate(rim) * _RimAmount;
                rgb = lerp(rgb, _RimColor.rgb, saturate(rim / max(a + rim, 0.0001)));
                a = saturate(a + rim * _RimColor.a);

                rgb = lerp(rgb, _RingColor.rgb, saturate(ring / max(a + ring, 0.0001)));
                a = saturate(a + ring * _RingColor.a);

                rgb = lerp(rgb, _CrackColor.rgb, saturate(crack));
                a = saturate(a + crack * _CrackColor.a);

                // 外緣整體收邊（用抖動過的 rEdge，收邊也跟著泥塊走）
                a *= 1.0 - smoothstep(0.72, 1.0, rEdge);

                a *= _Fade * i.color.a;
                if (a <= 0.003) discard;
                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
