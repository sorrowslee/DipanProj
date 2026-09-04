// 黑霧（uGUI Image 用）—— Boss 開戰前奏 BossIntroPanel 的霧層。
//
// ⚠ **是黑霧不是霧**（2026-09-04 定案）：一開始做的是霧，但「強敵現身」文字圖本身就是紅的，
//   紅字疊在霧上完全沒有對比、字讀不出來。改成黑為主、灰白為輔之後，紅色成了畫面上唯一的強調色、
//   只屬於文字。要改回霧之前先想清楚文字怎麼辦。
//
// 兩張灰階煙霧密度圖當**原料**，shader 負責**行為**：翻騰、流動、聚攏到畫面中央、上色、被風吹散。
//   _SmokeA = Resources/UI/BossIntroPanel/BossIntroPanel_Smoke1（厚重霧體，大團體積感）
//   _SmokeB = Resources/UI/BossIntroPanel/BossIntroPanel_Smoke2（細絮煙流，絲狀高頻細節）
//
// ⚠ **為什麼一定要貼圖、不能純程序 noise**（2026-09-04 第一版就是純 fbm，被作者退回）：
//   fbm 生得出「雲斑」——一團一團的濃淡起伏，但生不出煙的「絲」與「捲」。煙霧的捲曲邊、拉伸絮、
//   濃淡層次是**形狀**不是噪聲，那要美術畫。這支的作法是業界標準：**貼圖提供形狀、shader 提供行為**，
//   也對應 readme/ART_DIRECTION.md 紀律四「買來的特效包是原料，改色統一之後才是素材」。
//   不要為了省一張圖把這裡改回純程序，那條路試過了。
//
// ⚠ **兩張圖的匯入設定有三項是必要條件**（改錯會直接壞掉，見 PROGRESS.md 那一條）：
//   Wrap Mode = **Repeat**（要平鋪捲動；Clamp 會把邊緣像素拖成長條）
//   Generate Mip Maps = **開**（多層縮放取樣，關著縮小時細絮會閃爍）
//   sRGB (Color Texture) = **關**（這兩張是密度資料不是顏色；Linear 專案下當 sRGB 取樣會把中間調壓掉一大截）
//
// ⚠ 時間走外部餵的 _T，不用 _Time：面板 PausesGame=true，shader 內建 _Time 來自 Time.timeSinceLevelLoad
//   ＝受 timeScale 影響，timeScale=0 時會整個凍住（2026-09-04 踩過）。
//
// ⚠ 這一層是**半透明**壓在遊戲畫面上，不是蓋掉它——作者要「留一點場景輪廓透出來」。
//   所以 _MaxOpacity 刻意 < 1，而且霧稀薄處還有一個 alpha 底線 _SceneDarken（＝一層黑紗），
//   讓場景被壓暗、染上主色但仍讀得出形狀（ART_DIRECTION 紀律一「染成主色」＋紀律二「背景最暗、文字最亮」）。
//
// 三個動畫參數各管一段演出，都由 BossIntroPanel 每幀推進來：
//   _Amount    整體強度（霧湧入的淡入）
//   _Gather    0=均勻佈滿全螢幕　1=聚攏成畫面中央一團
//   _Progress  被風吹散（0=完整、1=散光）。與 UI/SmokeDissolve 用同一套湍流公式與同一個 _T，
//              所以霧與文字是同一陣風吹走的——要調風就兩邊一起調。
Shader "UI/BossAura"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _SmokeA ("厚重霧體（灰階密度圖）", 2D) = "black" {}
        _SmokeB ("細絮煙流（灰階密度圖）", 2D) = "black" {}

        _T ("Time (unscaled, 外部餵)", Float) = 0
        _Amount ("整體強度", Range(0,1)) = 1
        _Gather ("聚攏 (0=佈滿 1=聚到中央)", Range(0,1)) = 0
        _Progress ("吹散進度 (0=完整 1=散光)", Range(0,1)) = 0
        _Aspect ("螢幕寬高比", Float) = 1.7778

        // ⚠ **黑霧、不是霧**（2026-09-04 定案）：文字圖本身是紅的，霧再是紅的就沒有對比、字讀不出來。
        //    改成黑為主、灰白為輔之後，紅色成為畫面上唯一的強調色，只屬於文字——
        //    這也才符合 ART_DIRECTION 紀律一（強調色是稀缺資源）與紀律二（背景最暗、文字最亮）。
        //    要找回一點血色的話，把 _GlowColor 往暖灰偏一點點就好（例如 R 比 GB 高 0.02），**不要調成紅的**。
        // ⚠ 兩個顏色要**靠近**（都在無彩的黑→灰之間）：霧的濃淡靠不透明度與散射光表現，不是靠顏色分。
        _DeepColor ("霧稀薄處的顏色（近黑，帶一點冷調讓黑不死）", Color) = (0.012, 0.012, 0.016, 1)
        _GlowColor ("霧濃處的顏色（中灰；**上限受文字亮度約束**，太亮會跟紅字搶）", Color) = (0.155, 0.152, 0.165, 1)
        _CoreBoost ("霧脊額外加亮", Range(0,2)) = 0.30

        // ⚠ _DensityLo/_Hi 是**對照 Smoke1 的實際直方圖**定的，不是憑感覺：
        //    Smoke1 兩層 screen 混合後中位約 0.53、25% 分位約 0.32、75% 分位約 0.73。
        //    第一版把 Lo 設 0.30（≈ 單層取樣的中位 0.314），等於把一半以上的霧體砍成 0，
        //    只剩 Smoke2 的亮絲推得過門檻 ⇒ 畫面上看到的是「紅絲」不是「霧」（作者實機回報）。
        //    改參數前先看 readme/PROGRESS.md 那一條裡記的分位數，別再憑直覺調這兩個。
        _DensityLo ("密度下限（往上調＝霧變稀、空隙變多）", Range(0,1)) = 0.10
        _DensityHi ("密度上限（往下調＝霧變濃）", Range(0,1)) = 0.95
        _WispBoost ("細絮高光量（**只加在顏色上、不影響遮蔽**，做出煙裡的白絲＝「白色為輔」的那個白）", Range(0,2)) = 0.55
        _FlowSpeed ("流動速度", Float) = 0.8
        _WarpAmount ("翻騰扭曲強度", Range(0,0.3)) = 0.09

        _Scatter ("散射加光（霧自己發的光；濃霧的體積感靠它）", Range(0,1.5)) = 0.38
        // ⚠ **濃重籠罩版**（2026-09-04 作者定調：「不要再用這麼薄會看到背景的霧」）：
        //    三個 alpha 全部拉到接近 1，背景基本上被蓋住。**霧的層次因此完全交給顏色的明暗**
        //    （_DeepColor↔_GlowColor 的落差＋散射光＋白絲），不再靠「透出背景的程度」。
        //    這是刻意的取捨：想要「透得出背景的薄霧」就把 _FogMinAlpha 拉回 0.5 上下，
        //    但那在近乎全黑的 boss 房裡看起來就是黑斑，試過三輪都不行（見 readme/PROGRESS.md）。
        _MaxOpacity ("霧最濃處的不透明度", Range(0,1)) = 1.0
        _FogMinAlpha ("霧最薄處的不透明度（濃重籠罩＝拉到接近 1；要看得到背景才調低）", Range(0,1)) = 0.92
        _SceneDarken ("無霧處的黑紗底線（聚攏後外圍靠它壓住場景）", Range(0,1)) = 0.90
        _EdgeDarken ("畫面邊緣再壓暗多少（壓的是顏色，不是不透明度）", Range(0,1)) = 0.55

        _GatherPull ("聚攏時的向心壓縮倍率", Float) = 2.2
        _GatherDensify ("聚攏時中央變濃多少", Float) = 1.5
        _SwirlSpeed ("聚攏時的渦流角速度（滾滾感的來源；中心快、外圍慢）", Float) = 0.12
        _GatherWarpBoost ("聚攏時翻騰加劇倍率", Float) = 2.0

        // ── 吹散（與 UI/SmokeDissolve 對應欄位同義、同值才會是同一陣風）──
        _Rise ("吹散：往上飄的量", Float) = 0.45
        _Turb ("吹散：橫向湍流", Float) = 0.09
        _EdgeSoft ("吹散：破口柔和度", Range(0.01,0.6)) = 0.26
        _UpBias ("吹散：上方先散的偏量", Range(0,1)) = 0.35

        // ── 以下為 uGUI 遮罩/Mask 的標準樣板欄位，UI 系統會自動填，不要手動改 ──
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        // ⚠ **premultiplied alpha**，不是一般 UI 的 SrcAlpha OneMinusSrcAlpha。
        //    霧要同時做兩件事：擋住背景（遮蔽）＋自己發光（散射）。一般 over 混合只能做前者，
        //    霧就會變成「一層有洞的紅膜」；premultiplied 下 rgb 可以超過 alpha 該有的量，
        //    多出來的部分就是散射光，霧濃處會比背景更亮——**那才是眼睛認出「霧」的關鍵**。
        //    代價：frag 輸出的 rgb 必須自己先乘上 alpha（見 frag 結尾），頂點色 alpha 也要乘進 rgb。
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            sampler2D _SmokeA, _SmokeB;

            float _T, _Amount, _Gather, _Progress, _Aspect;
            // ⚠ 用 float4 不是 fixed4：fixed 值域只有 [-2,2]，顏色乘上加亮倍率會靜靜地被夾住產生色階
            //    （同 BloodlineDissolve 檔頭那條）。
            float4 _DeepColor, _GlowColor;
            float _CoreBoost, _DensityLo, _DensityHi, _WispBoost, _FlowSpeed, _WarpAmount;
            float _Scatter, _MaxOpacity, _FogMinAlpha, _SceneDarken, _EdgeDarken;
            float _GatherPull, _GatherDensify, _SwirlSpeed, _GatherWarpBoost;
            float _Rise, _Turb, _EdgeSoft, _UpBias;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            // 繞原點旋轉。**每一層取樣都轉不同角度**，是為了藏平鋪接縫：
            // 兩張圖不是無縫的（左右邊差約 8~11/255），單層捲動會看到規律的接痕，
            // 三層不同角度＋不同縮放疊起來，接痕彼此錯開就散掉了。
            float2 rot2(float2 p, float a) { float s = sin(a), c = cos(a); return float2(c * p.x - s * p.y, s * p.x + c * p.y); }

            fixed4 frag(v2f i) : SV_Target
            {
                float p = saturate(_Progress);
                float t = _T * _FlowSpeed;
                float2 uv = i.texcoord;

                // 螢幕寬高比修正過的中心座標（給徑向遮罩用，不然寬螢幕會拉成橢圓）
                float2 c = (uv - 0.5) * float2(_Aspect, 1.0);
                float d = length(c);

                // ── 聚攏：向心壓縮 ＋ 渦流 ⇒ 「滾滾紅塵」的密度感 ──
                // 兩件事一起做才滾得起來：
                //   ① 取樣座標往外拉 ⇒ 紋理內容往中心縮（霧被吸向畫面中央）
                //   ② 繞中心旋轉，且**角速度隨半徑遞減**（中心轉得快、外圍幾乎不動）＝渦流。
                //      只做 ① 的話霧只是「變小」，不會「翻滾」——這是第一版沒有滾滾感的原因。
                // 旋轉在等比座標下做（先乘 _Aspect），不然寬螢幕會轉成歪斜的橢圓。
                float2 q = uv - 0.5;
                q.x *= _Aspect;
                float swirlAng = _Gather * (t * _SwirlSpeed) / (0.25 + length(q) * 2.0);
                q = rot2(q, swirlAng);
                q.x /= _Aspect;
                q *= lerp(1.0, _GatherPull, _Gather);
                float2 guv = q + 0.5;

                // ── 吹散位移：往上飄 ＋ 橫向湍流（與 UI/SmokeDissolve 同一套公式）──
                // p 平方：前段幾乎不動、後段才被吹走，看起來才像「先撐著、然後散掉」。
                float2 suv = guv;
                suv.y -= (p * p) * _Rise * 0.9;
                suv.x += sin(guv.y * 7.0 + t * 1.3) * _Turb * p;

                // ── domain warp：拿細絮圖當扭曲場去擾動取樣座標 ──
                // 這一步是「翻騰」的來源。少了它，霧只會整片平移，看起來像一張圖在滑動而不是在翻滾。
                float wx = tex2D(_SmokeB, suv * 0.25 + float2( t * 0.013, -t * 0.021)).r;
                float wy = tex2D(_SmokeB, suv * 0.25 + float2(0.37, 0.71) + float2(-t * 0.017, t * 0.011)).r;
                float2 wuv = suv + (float2(wx, wy) - 0.5) * (_WarpAmount * lerp(1.0, _GatherWarpBoost, _Gather));

                // ── 厚重層：兩次取樣，不同角度/縮放/流向（藏接縫＋做出前後兩層的視差）──
                // ⚠ 縮放刻意很小（＝把 1254px 的圖放得很大）：全螢幕上要看到的是**幾個大霧團**，
                //    不是滿版的碎斑。前一版用 1.00/1.63，畫面上橫向排了十幾個小團，讀起來像斑駁的牆面紋理。
                //    放大後圖會變糊，但那正好——霧本來就是柔的（mipmap 已開，不會閃）。
                float a1 = tex2D(_SmokeA, rot2(wuv, 0.00) * 0.42 + float2( t * 0.010, -t * 0.022)).r;
                float a2 = tex2D(_SmokeA, rot2(wuv, 2.10) * 0.75 + float2(-t * 0.015, -t * 0.031)).r;
                // ⚠ 用 screen 混合（1-(1-a)(1-b)）不是加權平均：兩層霧疊在一起物理上是**更不透明**，
                //    平均只會把它拉回中位、霧永遠濃不起來。這是「整片濃厚」的關鍵一步。
                float thick = 1.0 - (1.0 - a1) * (1.0 - a2);

                // ── 細絮層：兩次取樣，用 max 不用相加——相加會把兩層的絲糊成一片灰，max 保留最亮的那幾絲 ──
                float b1 = tex2D(_SmokeB, rot2(wuv, 1.10) * 0.60 + float2( t * 0.021, -t * 0.040)).r;
                float b2 = tex2D(_SmokeB, rot2(wuv, 4.00) * 1.05 + float2(-t * 0.029, -t * 0.055)).r;
                float wisp = max(b1, b2);

                // ── 霧體：**只由厚重層決定**，柔和連續。這一條決定「遮蔽」，也就是霧感的來源 ──
                // ⚠ 細絮不參與遮蔽（它在下面的上色段才登場）：把高頻塞進 alpha 會讓霧的輪廓變得破碎銳利，
                //    看起來像墨漬或苔癬，不像蓬鬆的煙。真實煙霧的高頻出現在**亮度**上，不是在輪廓上。
                float body = smoothstep(_DensityLo, _DensityHi, thick);
                body = saturate(body * lerp(1.0, _GatherDensify, _Gather));   // 聚攏時中央更濃

                // ── 徑向遮罩：佈滿(整片，完全不衰減) → 聚攏(收成畫面中央一大團) ──
                // ⚠ 佈滿時的 r0 要大於畫面對角距離（16:9 約 1.02），否則邊角會被吃掉一圈、
                //    看起來就不是「整個畫面都是霧」。聚攏後也刻意留得夠大——「滾滾紅塵」是一大團在翻，
                //    不是縮成一顆球。
                float r0 = lerp(1.30, 0.22, _Gather);
                float r1 = lerp(1.80, 0.92, _Gather);
                float radial = 1.0 - smoothstep(r0, r1, d);
                body *= radial;

                // ── 上色：兩個顏色刻意靠近（都是紅），濃淡**不是**靠顏色分的 ──
                // 細絮在這裡登場：只加在顏色上，做出「煙裡被照亮的絲」；乘 body 讓它只在有霧處發亮，
                // 不會在空處自己長出孤立的絲。
                float3 col = lerp(_DeepColor.rgb, _GlowColor.rgb, saturate(body * 1.10));
                col += _GlowColor.rgb * wisp * _WispBoost * body;
                col += _GlowColor.rgb * pow(saturate(body), 4.0) * _CoreBoost;

                // 邊緣壓暗：壓的是**顏色**不是不透明度。壓 alpha 會讓四周變成一圈實心暗紅、失去霧感。
                col *= saturate(1.0 - _EdgeDarken * smoothstep(0.40, 1.10, d));

                // ── 遮蔽量：霧擋住背景多少（連續變化，薄處看得見背景）──
                float fogA = lerp(_FogMinAlpha, _MaxOpacity, body) * radial;
                // 無霧處（聚攏後的外圍）仍留一層黑紗壓住場景，不然霧一散開會突然露出明亮的房間。
                float a = max(fogA, _SceneDarken);

                // ── 散射加光：**這是霧感的主要來源** ──
                // ⚠ 前兩版的癥結：只做遮蔽，霧就只是「一層有洞的紅膜」。而且本專案的 boss 房本身近乎全黑，
                //    「霧薄處透出暗場景」和「霧薄處畫黑色」在眼睛看來完全一樣（都是黑斑）——
                //    所以把濃淡從顏色搬到 alpha 之後，畫面看起來幾乎沒變（作者實機回報）。
                //    真實的霧會**散射光線**：濃的地方比背景更亮。那個「亮起來」才是眼睛的辨識線索。
                //    premultiplied 混合讓 rgb 可以超過 alpha 該有的量，多出來的就是這道散射光。
                float3 scatter = _GlowColor.rgb * body * _Scatter;

                // ── 吹散裁切：noise 閾值，上方先散（_UpBias）──
                // 端點要往外推 _EdgeSoft，否則 p=0 時就已經破洞、p=1 時還留殘片。
                float m = tex2D(_SmokeA, uv * 1.9 + 0.31).r;
                float lo = -_EdgeSoft - 0.5 * _UpBias;
                float hi = 1.0 + _EdgeSoft + 0.5 * _UpBias;
                float th = lerp(lo, hi, p) + (uv.y - 0.5) * _UpBias;
                float blowKeep = smoothstep(th - _EdgeSoft, th + _EdgeSoft, m);
                a *= blowKeep;
                // ⚠ 散射光也要一起裁：它是加上去的光、不受 alpha 約束，漏掉的話霧散開之後
                //    畫面會留下一整片發光殘影（premultiplied 混合的典型陷阱）。
                scatter *= blowKeep;

                // ── 輸出：premultiplied alpha（見 SubShader 的 Blend 註解）──
                // rgb 要自己乘上 alpha；散射光**不乘** alpha（它是加上去的光，不是被遮蔽的顏色）。
                // 頂點色 alpha（面板 CanvasGroup 的淡入淡出）必須同時乘進 rgb 與 a，否則淡出時會發光殘留。
                float outA = saturate(a * _Amount) * i.color.a;
                fixed4 outc;
                outc.rgb = saturate(col * outA + scatter * _Amount * i.color.a);
                outc.a = outA;

                #ifdef UNITY_UI_CLIP_RECT
                // premultiplied：裁切要同時乘 rgb，只乘 a 的話被裁掉的區域會留下散射光的殘影。
                float clipK = UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                outc.rgb *= clipK;
                outc.a *= clipK;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(outc.a - 0.001);
                #endif

                return outc;
            }
            ENDCG
        }
    }
    Fallback Off
}
