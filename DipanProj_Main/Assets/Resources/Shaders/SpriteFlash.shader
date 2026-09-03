Shader "Custom/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0,1)) = 0

        // ── 角色環境融合（CharacterEnvFusion 以 MaterialPropertyBlock 餵值；場景數據進圖時自動量）──
        // 全部預設 0／白＝**這段完全不執行**，畫面與加這功能之前逐位元相同。
        _EnvOn ("Env fusion on (0=off)", Float) = 0
        _EnvMix ("Env tint strength", Float) = 0
        // ⚠ 這兩個是**乘法係數**不是顏色：宣告成 Color 會被 Unity 在 Linear 專案下自動做一次
        //   gamma→linear 轉換而扭曲（1.08 這種大於 1 的值更是直接失真）。用 Vector + SetVector 原樣送。
        _EnvBase ("Env dark-side mul (Vector, NOT color)", Vector) = (1,1,1,1)
        _EnvLit ("Env lit-side mul (Vector, NOT color)", Vector) = (1,1,1,1)
        _EnvPivot ("Env pivot (LINEAR lum)", Float) = 0.085
        _EnvSplit ("Env split half-width (LINEAR)", Float) = 0.060
        _BlackLift ("Black level lift (LINEAR)", Float) = 0
        // 黑階抬升的**色向量**（Vector，非 Color：同 _EnvBase 的理由）。歸一化的場景暗部色。
        _LiftTint ("Black lift color (Vector, normalized)", Vector) = (1,1,1,1)
        _Sat ("Saturation delta", Float) = 0
        _LumBoost ("Lit boost (push over bloom threshold)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment SpriteFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _FlashAmount;

            // ── 角色環境融合 ──
            float _EnvOn, _EnvMix, _EnvPivot, _EnvSplit;
            float _BlackLift, _Sat, _LumBoost;
            float4 _EnvBase, _EnvLit, _LiftTint;

            v2f SpriteVert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            fixed4 SpriteFrag(v2f IN) : SV_Target
            {
                fixed4 texel = tex2D(_MainTex, IN.texcoord);
                fixed4 c = texel * IN.color;

                // ── 角色環境融合（_EnvOn = 0 時整段跳過＝原本的畫面）──
                // ⚠⚠ 這裡所有門檻與抬升量都是 **Linear 空間**的數字（專案跑 Linear，見 PROBLEMS E11/E26）：
                //    室內石材場景的 linear 亮度整張擠在 0.02~0.20、中位數才 0.083。
                //    照 sRGB 直覺填 0.5 那種「一半亮」的值＝門檻永遠達不到，症狀是「效果好像沒做」而不是報錯。
                //    pivot/split 是角色自己貼圖的亮度分界（角色空間），不隨場景變。
                if (_EnvOn > 0.5)
                {
                    // ⚠ 整段用 float 中間變數算，不要直接在 fixed3 上累加：
                    //   fixed 的精度是 1/256（0.0039），而黑階抬升是 0.008 這種量級的小數——
                    //   直接在 fixed 上做會被量化成 2 個 step、抬升量變得又跳又不準。
                    //   （桌面平台 fixed 實際上就是 float，這裡是為了語意正確與日後移植。）
                    //   c 本身維持 fixed4 不動，所以 Mode 0 的路徑跟加這功能之前完全一樣。
                    float3 e = c.rgb;
                    float lum = dot(e, float3(0.299, 0.587, 0.114));
                    // 亮側權重：與 Atmosphere 室內系同構的 smoothstep（split 小＝曲線陡＝只有真亮部才算亮側）
                    float litW  = smoothstep(_EnvPivot - _EnvSplit, _EnvPivot + _EnvSplit, lum);
                    float darkW = 1.0 - smoothstep(0.0, _EnvPivot, lum);

                    // (a) 飽和微調（負值＝去飽和，往場景的收斂調性靠）
                    e = lerp(lum.xxx, e, 1.0 + _Sat);

                    // (b) 黑階抬升 ← 本功能的主角。
                    //     全螢幕後處理對角色與場景一視同仁，**永遠不會改變「角色暗部比場景暗多少」**；
                    //     那個相對差就是「貼在背景上」的主因，只能在角色自己的 sprite 上動。
                    //
                    //     ⚠ 抬升量必須**帶場景暗部的顏色**，不能三通道等量加。
                    //     首版用中性灰加法，實測角色暗部的 R/B 從 1.81 掉到 1.51——場景暗部是暖褐的
                    //     （實機量到 R/B ≈ 1.9），角色卻被拉成灰的，**色相上反而更不融入**。
                    //     _LiftTint 是歸一化的場景暗部色（三通道平均 = 1），所以換色不會改變抬升的總亮度。
                    e += _BlackLift * darkW * _LiftTint.rgb;

                    // (c) 亮部抬升：把角色亮部推過 Atmosphere bloom 的抽取門檻（0.09 linear）。
                    //     場景亮面都在發光瀰漫、角色偏暗完全不參與 bloom → 角色是全畫面唯一的硬邊。
                    //     光暈長在角色**外面**，所以角色本體不會糊（不違反「不可 blur 角色」）。
                    e *= 1.0 + _LumBoost * litW;

                    // (d) 環境色：暗側 / 亮側各自的乘法色，沿用當前 Atmosphere mode 的 baseTint / litTint，
                    //     角色與場景吃同一組色 → 之後換氛圍角色會自動跟著變。
                    float3 tint = lerp(_EnvBase.rgb, _EnvLit.rgb, litW);
                    e = lerp(e, e * tint, _EnvMix);

                    // （原本的 (e) 邊緣融合 Test C 已於 2026-09-03 砍除：兩輪實測貢獻在雜訊級。）

                    c.rgb = e;
                }

                c.rgb = lerp(c.rgb, fixed3(1, 1, 1), _FlashAmount);
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
