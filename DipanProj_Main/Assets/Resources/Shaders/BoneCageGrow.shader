// 骨牢「從地裡長出來」的 sprite shader。
//
// 作法：由下往上**揭露**（_Grow 0→1），揭露門檻依 X 抖動 ⇒ 每根骨刺錯開破土、不同時到頂。
// 單純的水平揭露線太整齊，看起來像「被地平線切開」而不是「長出來」；抖動 ＋ 呼叫端的縱向
// 超調回彈（scale.y 0.75→1.06→1.0）才是「生長感」的來源。
//
// ⚠ _Grow=1 時必須**整張都露出來**：所以上界要補 +_GrowJitter，否則抖動最大的那幾列會被切頭
//   （症狀是骨刺永遠少一截，而且不會報錯）。
// ⚠ _Dim/_Desat 預設 1/0 ＝ 不動原圖。專案是 Linear 色彩空間，特效素材直接用在暗場景會過亮
//   （見 readme/PROBLEMS.md E11/E12），這兩個旋鈕是留給調色用的，不是裝飾。
Shader "Custom/BoneCageGrow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Grow ("生長進度 (0=埋在地下 1=全長出)", Range(0,1)) = 1
        _GrowJitter ("每根錯開幅度", Range(0,0.5)) = 0.18
        _GrowSoft ("揭露柔邊", Range(0.001,0.2)) = 0.03
        _EdgeGlow ("破土邊緣亮度 (0=關)", Float) = 0
        _EdgeColor ("破土邊緣顏色", Color) = (1, 0.35, 0.28, 1)

        _Dim ("整體壓暗 (1=原樣)", Float) = 1
        _Desat ("去飽和 (0=原樣 1=全灰)", Range(0,1)) = 0
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
            float _Grow, _GrowJitter, _GrowSoft, _EdgeGlow;
            fixed4 _EdgeColor;
            float _Dim, _Desat;

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
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;

                // 每一直行一個固定的隨機相位 ⇒ 三根骨刺錯開破土（同一 x 的整條維持一致，不會閃爍）。
                float n = frac(sin(IN.texcoord.x * 91.37 + 3.1) * 43758.5453);

                // 揭露線：_Grow=0 → 整張在線下方（全隱藏）；=1 → 整張在線上方（全顯示，含抖動最大的那幾列）。
                float edge = lerp(-_GrowSoft, 1.0 + _GrowSoft + _GrowJitter, _Grow) - _GrowJitter * n;
                float reveal = 1.0 - smoothstep(edge - _GrowSoft, edge + _GrowSoft, IN.texcoord.y);
                c.a *= reveal;

                // 破土的那一條亮線（_EdgeGlow=0 ＝ 這段不做事）。
                // ⚠ 乘 c.a：透明區加亮會在圖外圍長出一圈方形光暈（同 SpriteFlash 的 _ChargeAmount）。
                float band = saturate(1.0 - abs(IN.texcoord.y - edge) / max(0.0001, _GrowSoft * 2.0));
                c.rgb += _EdgeColor.rgb * band * _EdgeGlow * c.a;

                // 調色（預設不動原圖）
                float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
                c.rgb = lerp(c.rgb, lum.xxx, _Desat) * _Dim;

                c.rgb *= c.a;   // premultiplied（配合 Blend One OneMinusSrcAlpha）
                return c;
            }
            ENDCG
        }
    }
}
