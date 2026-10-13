// 血霧（世界空間 sprite 用）—— 菁英血衛士化霧後追人的那團霧（見 Scripts/AI/BloodMistVisual.cs、readme/BOSS_MODULE.md §13.4）。
//
// ⭐ **這是 `UI/BossAura`（Boss 開戰前奏的黑霧）的世界空間紅色版**（2026-10-13 作者指定：「參考打 boss 前的黑霧特效，換成紅色」）。
//   算式逐段照抄 BossAura：兩張灰階煙霧密度圖當原料 → domain warp 翻騰 → 厚重層 screen 疊 → 細絮高光 → 散射光 →
//   吹散（_Fade 反過來用＝湧現／吹散）。差別只有四處：
//     ① 全螢幕佈滿 → **包住怪物的一團**（徑向遮罩，邊緣被霧體密度啃成煙狀，不是圓盤）
//     ② 黑灰 → **暗紅→血紅**
//     ③ 時間用 _Time（世界端特效，遊戲暫停時本來就該凍住；BossAura 是暫停中的 UI 才要外部餵 _T）
//     ④ 多了 _Drag（往後拖長）與 _WorldOffset（煙的紋理跟著世界走、不跟著怪走 ⇒ 移動時煙往後流）
//
// ⚠⚠ **不要改回純程序 noise**：第一版（2026-10-13 早上）就是 fbm＋Voronoi 黑斑、外型仿 mist 序列圖，被作者退回
//   （mist 序列圖本身作者就嫌醜，只用在「變身」那一段）。BossAura 檔頭早寫過同一條：
//   「fbm 生得出雲斑，生不出煙的絲與捲——那要美術畫。貼圖提供形狀、shader 提供行為。」
//
// 原料（與 BossAura **共用同兩張圖**，由 BloodMistVisual 載入後 SetTexture）：
//   _SmokeA = Resources/UI/BossIntroPanel/BossIntroPanel_Smoke1（厚重霧體）
//   _SmokeB = Resources/UI/BossIntroPanel/BossIntroPanel_Smoke2（細絮煙流）
//   ⚠ 匯入設定三項是必要條件（Wrap=Repeat、Mip Maps 開、sRGB 關），改了 BossAura 也會壞，見 BossAura.shader 檔頭。
//
// ⚠ premultiplied alpha（Blend One OneMinusSrcAlpha），理由同 BossAura：霧要同時「擋住背景」＋「自己散射一點光」，
//   一般 over 混合做不出後者，霧會變成一層有洞的紅膜。代價：rgb 要自己乘 alpha、散射光要跟著吹散一起裁。
// ⚠ Linear 色彩空間（PROBLEMS E11）：顏色放 Properties（自動 sRGB→線性）。紅色主色刻意壓暗（150,14,18），
//   散射光只給 0.14——暗場景裡加上去的光很顯眼，給多了霧會變霓虹（numpy 預覽第一輪就是這樣）。
// 2026-10-13 先用 numpy 照同一套算式＋真的兩張密度圖算靜態預覽，亮／暗底各看三輪才進 shader。
Shader "Custom/BloodMist"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}   // 不取樣，只為了讓 SpriteRenderer 正常送資料

        _SmokeA ("厚重霧體（灰階密度圖，程式自動帶入）", 2D) = "black" {}
        _SmokeB ("細絮煙流（灰階密度圖，程式自動帶入）", 2D) = "black" {}

        _Fade ("顯現程度（0=吹散光 1=完整；程式每幀餵）", Range(0, 1)) = 1
        _Drag ("拖曳（uv，往後的方向與長度；程式每幀餵）", Vector) = (0, 0, 0, 0)
        _WorldOffset ("紋理的世界偏移（程式每幀餵）", Vector) = (0, 0, 0, 0)
        _Seed ("亂數種子（每隻怪不同）", Float) = 0

        _DeepColor ("霧稀薄處（暗紅近黑）", Color) = (0.133, 0.008, 0.020, 1)   // sRGB (34,2,5)
        _GlowColor ("霧濃處（血紅）",       Color) = (0.588, 0.055, 0.071, 1)   // sRGB (150,14,18)
        _CoreBoost ("霧脊額外加亮", Range(0, 2)) = 0.25
        _WispBoost ("細絮高光量（只加顏色、不影響遮蔽）", Range(0, 2)) = 0.45
        _Scatter ("散射加光（給多了會霓虹）", Range(0, 1.5)) = 0.14

        _DensityLo ("密度下限（往上＝霧變稀、空隙變多）", Range(0, 1)) = 0.15
        _DensityHi ("密度上限（往下＝霧變濃）", Range(0, 1)) = 0.95
        _FogMinAlpha ("霧最薄處的不透明度", Range(0, 1)) = 0.45
        _MaxOpacity ("霧最濃處的不透明度", Range(0, 1)) = 0.95

        _TexScale ("紋理尺度（越大霧團越碎）", Float) = 1.0
        _FlowSpeed ("流動速度", Float) = 0.8
        _WarpAmount ("翻騰扭曲強度", Range(0, 0.3)) = 0.12

        _InnerRadius ("霧團實心半徑（uv，quad 邊緣＝0.5）", Range(0, 0.5)) = 0.10
        _OuterRadius ("霧團外緣半徑（uv）", Range(0, 0.5)) = 0.36
        _EdgeBite ("外緣被霧體啃掉的量（0＝圓盤）", Range(0, 0.6)) = 0.28
        _MaxStretch ("往後拖長的上限", Range(0, 0.6)) = 0.30

        // ── 吹散（與 BossAura／SmokeDissolve 同義同值）──
        _Rise ("吹散：往上飄的量", Float) = 0.45
        _Turb ("吹散：橫向湍流", Float) = 0.09
        _EdgeSoft ("吹散：破口柔和度", Range(0.01, 0.6)) = 0.26
        _UpBias ("吹散：上方先散的偏量", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha   // premultiplied（見檔頭）

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            sampler2D _SmokeA, _SmokeB;
            float4 _Drag, _WorldOffset;
            // float4 不是 fixed4：顏色乘加亮倍率時 fixed 會被夾住產生色階（同 BossAura）
            float4 _DeepColor, _GlowColor;
            float _Fade, _Seed, _CoreBoost, _WispBoost, _Scatter, _DensityLo, _DensityHi, _FogMinAlpha, _MaxOpacity;
            float _TexScale, _FlowSpeed, _WarpAmount, _InnerRadius, _OuterRadius, _EdgeBite, _MaxStretch;
            float _Rise, _Turb, _EdgeSoft, _UpBias;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // 每一層取樣轉不同角度，藏兩張圖的平鋪接縫（同 BossAura）
            float2 rot2(float2 p, float a) { float s = sin(a), c = cos(a); return float2(c * p.x - s * p.y, s * p.x + c * p.y); }

            float4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float p = 1.0 - saturate(_Fade);    // 吹散進度（＝BossAura 的 _Progress）
                float t = _Time.y * _FlowSpeed;

                // ── 霧團形狀：以 quad 中心為圓心；移動時往後拖長（只拉「後半邊」）──
                float2 q = uv - 0.5;
                float dragLen = length(_Drag.xy);
                if (dragLen > 1e-4)
                {
                    float2 dn = _Drag.xy / dragLen;
                    float behind = max(dot(q, dn), 0.0);
                    q -= dn * behind * min(dragLen * 1.6, _MaxStretch);
                }
                float d = length(float2(q.x * 1.15, q.y * 0.9));   // 略高於寬（人形的霧）

                // ── 取樣座標：quad uv × 尺度 ＋ 世界偏移（煙跟著世界走 ⇒ 移動時往後流）＋ 種子 ──
                float2 suv = uv * _TexScale + _WorldOffset.xy + _Seed;
                // 吹散位移：往上飄＋橫向湍流（p 平方：先撐著、後段才被吹走）
                suv.y -= (p * p) * _Rise * 0.9;
                suv.x += sin(uv.y * 7.0 + t * 1.3) * _Turb * p;

                // ── domain warp（翻騰的來源）──
                float wx = tex2D(_SmokeB, suv * 0.25 + float2( t * 0.013, -t * 0.021)).r;
                float wy = tex2D(_SmokeB, suv * 0.25 + float2(0.37, 0.71) + float2(-t * 0.017, t * 0.011)).r;
                float2 wuv = suv + (float2(wx, wy) - 0.5) * (_WarpAmount * 2.0);

                // ── 厚重層：兩層 screen 疊（物理上兩層霧＝更不透明）──
                float a1 = tex2D(_SmokeA, rot2(wuv, 0.00) * 0.42 + float2( t * 0.010, -t * 0.022)).r;
                float a2 = tex2D(_SmokeA, rot2(wuv, 2.10) * 0.75 + float2(-t * 0.015, -t * 0.031)).r;
                float thick = 1.0 - (1.0 - a1) * (1.0 - a2);

                // ── 細絮層：max 保留最亮的絲 ──
                float b1 = tex2D(_SmokeB, rot2(wuv, 1.10) * 0.60 + float2( t * 0.021, -t * 0.040)).r;
                float b2 = tex2D(_SmokeB, rot2(wuv, 4.00) * 1.05 + float2(-t * 0.029, -t * 0.055)).r;
                float wisp = max(b1, b2);

                // ── 徑向遮罩：外緣被霧體密度「啃」過 ⇒ 輪廓是煙不是圓盤 ──
                float radial = 1.0 - smoothstep(_InnerRadius, _OuterRadius, d - (thick - 0.5) * _EdgeBite);
                float body = smoothstep(_DensityLo, _DensityHi, thick) * radial;

                // ── 上色（濃淡靠暗紅↔血紅，細絮只加在顏色上）──
                float3 col = lerp(_DeepColor.rgb, _GlowColor.rgb, saturate(body * 1.10));
                col += _GlowColor.rgb * wisp * _WispBoost * body;
                col += _GlowColor.rgb * pow(saturate(body), 4.0) * _CoreBoost;

                float a = lerp(_FogMinAlpha, _MaxOpacity, body) * radial;
                float3 scatter = _GlowColor.rgb * body * _Scatter;

                // ── 吹散裁切（湧現時反過來＝從中心、下方先長出來）──
                float m = tex2D(_SmokeA, uv * 1.9 + 0.31 + _Seed).r;
                float lo = -_EdgeSoft - 0.5 * _UpBias;
                float hi = 1.0 + _EdgeSoft + 0.5 * _UpBias;
                float th = lerp(lo, hi, p) + (uv.y - 0.5) * _UpBias;
                float keep = smoothstep(th - _EdgeSoft, th + _EdgeSoft, m);
                a *= keep;
                scatter *= keep;   // ⚠ 散射光也要一起裁，否則霧散了還留一片發光殘影（premultiplied 陷阱，同 BossAura）

                // ── 輸出：premultiplied；頂點色 alpha 同時乘進 rgb 與 a ──
                float outA = saturate(a) * i.color.a;
                float4 o;
                o.rgb = saturate(col * i.color.rgb * outA + scatter * i.color.a);
                o.a = outA;
                return o;
            }
            ENDCG
        }
    }
    Fallback Off
}
