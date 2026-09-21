// 對話立繪的「邊緣羽化」UI 著色器（Built-in 算繪管線）。
//
// 為什麼需要它：部分立繪素材的人物一路畫到畫布邊界才被切斷（例如蟲皇左緣有 69% 的畫布邊是不透明的），
// 去背再乾淨也救不回來——邊緣就是一條硬切的直線，疊在場景上非常突兀。
// 這支 shader 讓「靠近圖檔邊界」的像素 alpha 漸層衰減，硬邊化成柔邊、融進暗場景。
//
// ⚠ 這是**短期補救**，根本解仍是產圖時要求人物四周留白（見 readme/AI_IMAGE_GEN_GUIDE.md）。
// ⚠ 對沒有切邊的立繪完全無害：那些圖的邊緣 alpha 本來就是 0，乘上衰減仍是 0。
// ⚠ 羽化算在 **UV 空間**（圖檔自己的邊界），所以右側立繪 localScale.x = -1 的鏡像不影響結果。
//
// 內容是 Unity 內建 UI-Default 的標準模板，只在 frag 末段多了羽化那幾行。
Shader "UI/TalkAvatarFeather"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _FeatherX ("左右羽化寬度（UV 比例）", Range(0, 0.5)) = 0.07
        _FeatherY ("上下羽化寬度（UV 比例）", Range(0, 0.5)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

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
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _FeatherX;
            float _FeatherY;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // ── 邊緣羽化：距圖檔邊界越近，alpha 越低（smoothstep = 兩端平滑、不會出現漸層帶） ──
                if (_FeatherX > 0.0001)
                {
                    float dx = min(IN.texcoord.x, 1.0 - IN.texcoord.x);
                    color.a *= smoothstep(0.0, _FeatherX, dx);
                }
                if (_FeatherY > 0.0001)
                {
                    float dy = min(IN.texcoord.y, 1.0 - IN.texcoord.y);
                    color.a *= smoothstep(0.0, _FeatherY, dy);
                }

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
