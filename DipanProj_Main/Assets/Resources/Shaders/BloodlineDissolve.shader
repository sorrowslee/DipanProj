// 斑駁溶解（uGUI Image 用）—— 血統揭示面板 BloodlineIntroPanel 的立繪交替。
//
// 一張圖依「程序噪點」不規則地剝落／浮現，破口邊緣一圈暗紅燒蝕光，像老壁畫剝落或紙被燒穿。
// 由 _Cutoff 單一參數驅動，**同一支著色器同時做正反兩個方向**：
//   舊立繪消失：_Cutoff 0 → 1
//   新立繪浮現：_Cutoff 1 → 0
// 所以不需要 invert 開關，兩張 Image 各自掛一份材質、各自推自己的 _Cutoff 即可。
//
// 噪點是 hash 值噪 2 個八度（粗塊 + 細粒），**不吃任何貼圖**——與專案其他程序生成視覺
// （BossIntroPanel 的暈影、LiquidOrb）同一個路數：零素材、改參數就換質感。
//
// ⚠ 為什麼 uv 可以直接拿來算噪點：立繪是 MapSpriteLoader.GetWholeSprite 用
//   Sprite.Create(tex, 整張全圖, ...) 建的，不是圖集切片，所以 uv 就是 0~1 的整張圖座標。
//   哪天立繪改走圖集，這裡要改成用 _MainTex_ST 還原到 sprite 局部座標，否則噪點會錯位。
//
// ⚠ 專案是 Linear 色彩空間，半透明疊色會比直覺強約一倍（見 readme/PROBLEMS.md E11），
//   所以 _EdgeColor 與 _EdgeBoost 的預設值刻意保守。
Shader "UI/BloodlineDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Cutoff ("Cutoff (0=完整 1=全消失)", Range(0,1)) = 0
        _NoiseScale ("噪點粗細（越大越細碎）", Float) = 7.0
        _Detail ("細粒強度（0=只有大塊剝落）", Range(0,1)) = 0.45
        _Seed ("亂數種子（同一張圖換個數字就換破法）", Float) = 0.0

        _EdgeWidth ("燒蝕邊寬度", Range(0.001,0.5)) = 0.10
        _EdgeColor ("燒蝕邊顏色", Color) = (0.55, 0.05, 0.03, 1)
        _EdgeBoost ("燒蝕邊亮度倍率", Float) = 1.6

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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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

            float _Cutoff, _NoiseScale, _Detail, _Seed;
            float _EdgeWidth, _EdgeBoost;
            // ⚠ 用 float4 不是 fixed4：fixed 在還理會它的平台上值域只有 [-2,2]，
            //    _EdgeColor.rgb * _EdgeBoost 一旦把 _EdgeBoost 調過 2 就會靜靜地夾住並產生色階。
            //    _EdgeBoost 是 Inspector 上可調的欄位，這是很容易踩到的調參陷阱。
            float4 _EdgeColor;

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

            // 2D 值噪：同一個座標每幀穩定（不是隨機閃爍，而是固定的一張「剝落圖」）。
            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);            // smoothstep 內插，避免格點硬邊
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half4 col = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;

                // ── 剝落遮罩：粗塊決定「哪一片先掉」，細粒讓邊緣毛躁不平滑 ──
                float2 p = i.texcoord * _NoiseScale + _Seed;
                float n = valueNoise(p);
                n = lerp(n, n * 0.65 + valueNoise(p * 3.7 + 11.3) * 0.35, saturate(_Detail));

                // _Cutoff=0 要「完全沒破」、=1 要「全沒了」。噪點值域是 [0,1]，
                // 端點若不往外推一點，剛好等於 0 的像素在 _Cutoff=0 時就會憑空破一個洞。
                float t = _Cutoff * 1.04 - 0.02;
                float d = n - t;                        // <0 = 已剝落
                clip(d);                                // 破口直接丟棄（不留半透明殘影）

                // ── 燒蝕邊：越靠近剝落臨界越亮。_Cutoff 幾乎為 0 時整個關掉，避免靜止時圖邊發紅 ──
                float edge = 1.0 - saturate(d / max(0.001, _EdgeWidth));
                edge *= saturate(_Cutoff * 12.0);
                edge *= edge;                           // 收窄成一圈細邊，而不是整張圖泛紅
                col.rgb = lerp(col.rgb, _EdgeColor.rgb * _EdgeBoost, edge * _EdgeColor.a);

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
