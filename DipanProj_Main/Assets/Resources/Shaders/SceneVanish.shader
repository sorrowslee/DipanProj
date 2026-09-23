// 場景吞噬（鏈動作 sceneVanish）的 sprite shader——**地圖本身**（背景、地上物）換上這個材質，
// 以玩家為中心的一圈「還看得到的世界」由外往內縮，圈外的像素被換成虛空色，交界處燒一圈血紅邊。
//
// 參數全部是 **global**（Shader.SetGlobal*，由 SceneVanish.cs 每幀寫）：整張地圖所有 sprite 共用一個材質、
// 一組數字，同一圈吞噬線才會在背景與每個地上物上完全接得起來。
//
// ⚠ 圈外不是「變透明」而是「變成虛空色、保留 alpha」：背景被塗成虛空色、地上物也被塗成同一個虛空色 ⇒
//   地上物疊在背景上看不出輪廓＝消失了。若改成變透明，背景會露出相機底色、地上物反而會留下半透明殘影。
// ⚠ 專案是 Linear 色彩空間：顏色一律由 C# 用 SetGlobalColor 傳（會自動做 gamma→linear），不要在這裡寫死數字。
Shader "Custom/SceneVanish"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
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
                float2 world    : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;

            // ── global（SceneVanish.cs 寫）──
            float4 _SV_Center;      // xy ＝ 吞噬的中心（玩家腳下，世界座標）
            float  _SV_Radius;      // 還看得到的半徑（世界單位；會縮到負值＝全吞）
            float  _SV_Feather;     // 吞噬邊緣的柔邊寬
            float  _SV_Wobble;      // 吞噬線的起伏幅度（不規則才像被「吃掉」，正圓像鏡頭光圈）
            float  _SV_EdgeGlow;    // 交界燒邊亮度
            fixed4 _SV_VoidColor;   // 虛空色
            fixed4 _SV_EdgeColor;   // 燒邊色

            v2f SpriteVert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.world = mul(unity_ObjectToWorld, IN.vertex).xy;
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

                float2 d = IN.world - _SV_Center.xy;
                float dist = length(d);
                float ang = atan2(d.y, d.x);
                // 兩組不同頻率、反向流動的起伏 ⇒ 吞噬線像活的一樣蠕動
                float wob = (sin(ang * 5.0 + _Time.y * 1.7) * 0.6 + sin(ang * 11.0 - _Time.y * 2.3) * 0.4) * _SV_Wobble;
                float r = _SV_Radius + wob;
                float feather = max(0.0001, _SV_Feather);

                float m = smoothstep(r - feather, r, dist);                      // 0＝圈內保留、1＝圈外吞掉
                float rim = saturate(1.0 - abs(dist - (r - feather * 0.5)) / (feather * 0.5));

                c.rgb = lerp(c.rgb, _SV_VoidColor.rgb, m);
                c.rgb += _SV_EdgeColor.rgb * rim * rim * _SV_EdgeGlow;          // 平方＝燒邊集中在交界、不糊成一大片

                c.rgb *= c.a;   // premultiplied（配合 Blend One OneMinusSrcAlpha）
                return c;
            }
            ENDCG
        }
    }
}
