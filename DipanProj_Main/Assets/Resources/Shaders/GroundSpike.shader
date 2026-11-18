// 地刺「從地底刺出來」的 sprite shader（2026-11-17，取代舊的地刺序列圖，見 readme/GROUND_SPIKE.md）。
//
// 作法：**世界座標的地平線以下一律不畫**（_GroundY）。程式把整根刺從地平線下往上推（GroundSpikeBurst），
// 看起來就是尖端先冒出、整根破土而出；收回時反過來往下沉，被地平線吃掉。
// 用世界座標而不是 UV：SpriteRenderer 動態合批時頂點已經在世界空間、sprite 也可能進圖集，UV 的 0~1 不可靠；
// mul(unity_ObjectToWorld, vertex) 在合批與不合批兩種情況下都會得到正確的世界座標。
//
// 旋鈕（每根刺用 MaterialPropertyBlock 各自設）：
//   _GroundY     地平線的世界 Y（＝這根刺的根部位置）
//   _GroundSoft  地平線柔邊（世界單位；0.02 左右，太大會像霧）
//   _GroundShade 貼地那一段壓暗的程度（0＝不壓）＋ _ShadeHeight 壓暗的高度：讓刺看起來是「插在土裡」而不是「貼在地上」
//   _Fade        整體透明度（淡出收尾用；1＝不透明）
//   _Dim         整體壓暗（1＝原樣）。專案是 Linear 色彩空間，素材在暗場景容易過亮（readme/PROBLEMS.md E11/E12）
// ⚠ 輸出是 premultiplied alpha（Blend One OneMinusSrcAlpha），跟 BoneCageGrow 一致。
Shader "Custom/GroundSpike"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _GroundY ("地平線世界 Y", Float) = 0
        _GroundSoft ("地平線柔邊（世界單位）", Range(0.001, 0.3)) = 0.02
        _GroundShade ("貼地壓暗 (0=關)", Range(0, 1)) = 0.45
        _ShadeHeight ("壓暗高度（世界單位）", Range(0.01, 2)) = 0.25

        _Fade ("透明度 (1=不透明)", Range(0, 1)) = 1
        _Dim ("整體壓暗 (1=原樣)", Float) = 1
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
                float  worldY   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _GroundY, _GroundSoft, _GroundShade, _ShadeHeight;
            float _Fade, _Dim;

            v2f SpriteVert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                OUT.worldY = mul(unity_ObjectToWorld, IN.vertex).y;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            fixed4 SpriteFrag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;

                // 地平線以上才畫（柔邊 _GroundSoft）
                float h = IN.worldY - _GroundY;
                c.a *= smoothstep(-_GroundSoft, _GroundSoft, h);

                // 貼地那一段壓暗：越靠近地面越暗 ⇒ 像是從土裡拔出來、根部沾著陰影
                float shade = 1.0 - _GroundShade * (1.0 - saturate(h / max(0.0001, _ShadeHeight)));
                c.rgb *= shade * _Dim;

                c.a *= _Fade;
                c.rgb *= c.a;   // premultiplied（配合 Blend One OneMinusSrcAlpha）
                return c;
            }
            ENDCG
        }
    }
}
