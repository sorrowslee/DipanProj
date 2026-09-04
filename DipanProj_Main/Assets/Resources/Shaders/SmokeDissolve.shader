// 煙霧凝聚／消散（uGUI Image 用）—— Boss 開戰前奏的「強敵現身」文字圖。
//
// 一張圖像煙一樣**往上飄散**：湍流位移把圖往上推並橫向擾動，多層拖尾取樣拖出煙尾，
// 再用另一組噪點做 alpha 閾值，讓它是「一塊塊散開」而不是整體變淡——後者只會像 fade，不像煙。
//
// 由 _Progress 單一參數驅動，**同一支著色器同時做正反兩個方向**（同 UI/BloodlineDissolve 的設計）：
//   凝聚成形：_Progress 1 → 0（散開的煙收攏成字）
//   煙霧散去：_Progress 0 → 1
// 所以不需要 invert 開關，一張 Image 掛一份材質、推自己的 _Progress 即可。
//
// ⚠ **_Pad 是必要的，不是可有可無的美化**：uv 位移只能在圖自己的範圍內取樣，圖框外沒有像素，
//   煙一飄出去就被切平。所以 Image 的顯示尺寸要放大 _Pad 倍，shader 內把 uv 內縮 _Pad 倍映射回
//   原圖區域，多出來的外圈就是「可以飄出去的空白」。
//   內縮後超出 [0,1] 的取樣一律當透明（自己乘 inside 遮罩）——**不能靠 texture 的 wrapMode**，
//   sprite 預設是 Clamp，會把邊緣像素拖成長條。
//
// ⚠ 時間走外部餵的 _T，不用 _Time：面板 PausesGame=true，shader 內建 _Time 受 timeScale 影響，
//   timeScale=0 時會凍住（2026-09-04 踩過，見 readme/PROGRESS.md）。
//
// ⚠ uv 直接拿來算噪點的前提：這張圖是 Resources 的單張 sprite（uv 就是 0~1 整張圖）。
//   哪天改走圖集，噪點與 _Pad 都要改用 _MainTex_ST 還原到 sprite 局部座標，否則整個錯位。
Shader "UI/SmokeDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _T ("Time (unscaled, 外部餵)", Float) = 0
        _Progress ("進度 (0=完整 1=散光；反向跑＝凝聚)", Range(0,1)) = 0
        _Pad ("外圈留白倍率（Image 要一起放大同樣倍率）", Float) = 1.6

        _Rise ("往上飄的量", Float) = 0.45
        _Turb ("橫向湍流", Float) = 0.09
        _NoiseScale ("噪點粗細（越大越細碎）", Float) = 3.0
        _EdgeSoft ("破口柔和度（越大越像煙、越小越像撕裂）", Range(0.01,0.6)) = 0.26
        _UpBias ("上方先散的偏量", Range(0,1)) = 0.35
        _Trail ("拖尾強度（0=無殘影、1=滿）", Range(0,1)) = 0.8
        _Seed ("亂數種子（換個數字就換散法）", Float) = 0.0

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

            float _T, _Progress, _Pad;
            float _Rise, _Turb, _NoiseScale, _EdgeSoft, _UpBias, _Trail, _Seed;

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
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = valueNoise(p) * 0.55;
                v += valueNoise(p * 2.13 + 7.7) * 0.30;
                v += valueNoise(p * 4.37 + 19.1) * 0.15;
                return v;
            }

            // 取樣並自己做邊界遮罩：sprite 的 wrapMode 是 Clamp，越界會把邊緣像素拖成長條，
            // 位移一大整張圖外圍就會出現拉絲，所以越界一律當透明。
            half4 sampleMasked(float2 u)
            {
                float inside = step(0.0, u.x) * step(u.x, 1.0) * step(0.0, u.y) * step(u.y, 1.0);
                return (tex2D(_MainTex, u) + _TextureSampleAdd) * inside;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float p = saturate(_Progress);

                // ── uv 內縮：把 Image 放大出來的外圈換成「煙可以飄出去的空白」──
                float2 uv = (i.texcoord - 0.5) * _Pad + 0.5;

                // ── 湍流位移：往上飄（uv.y 減＝畫面上往上移）＋ 橫向擾動 ──
                // p 平方：前段幾乎不動，後段才被吹走，看起來才像「先撐著、然後散掉」。
                float2 nuv = uv * _NoiseScale + _Seed;
                nuv.y -= _T * 0.30;
                float n = fbm(nuv);

                float2 off;
                off.y = -(p * p) * _Rise * (0.40 + n * 1.20);
                off.x = ((n - 0.5) * 2.0 * _Turb
                         + sin(uv.y * 11.0 + _T * 1.6) * _Turb * 0.40) * p;

                // ── 三層拖尾取樣：主體飄到最遠，另兩層留在半路＝從原位拖到新位置的一串煙尾 ──
                // 權重總和恆為 1（_Trail=0 時只剩主體、沒有殘影；=1 時殘影最重），
                // 所以 _Trail 不會連帶改變整體亮度。p=0 時 off=0，三層取樣同一點＝原圖，靜止時零副作用。
                float w1 = 0.32 * _Trail;
                float w2 = 0.23 * _Trail;
                float w0 = 1.0 - w1 - w2;
                half4 col = sampleMasked(uv + off) * w0
                          + sampleMasked(uv + off * 0.66) * w1
                          + sampleMasked(uv + off * 0.33) * w2;
                col *= i.color;

                // ── alpha 閾值裁切：讓字一塊塊散開，而不是整體變淡 ──
                // 端點要往外推 _EdgeSoft＋_UpBias，否則 p=0 時就已經缺角、p=1 時還留殘片。
                float m = fbm(uv * _NoiseScale * 0.8 + 17.3 + _Seed);
                float lo = -_EdgeSoft - 0.5 * _UpBias;
                float hi = 1.0 + _EdgeSoft + 0.5 * _UpBias;
                float th = lerp(lo, hi, p) + (uv.y - 0.5) * _UpBias;   // uv.y 大（上方）→ th 大 → 先散
                col.a *= smoothstep(th - _EdgeSoft, th + _EdgeSoft, m);

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
    Fallback Off
}
