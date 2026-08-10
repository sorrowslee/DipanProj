// 地圖編輯器的「照明預覽」圖層（不是遊戲用的，遊戲端請看主專案 Resources/Shaders/Atmosphere.shader）。
//
// 做法：一張蓋滿相機視野的四邊形，用 **相乘混合（Blend DstColor Zero）** 疊在所有地圖 sprite 之上。
// 相乘的結果 = 場景顏色 × 本 shader 輸出，所以只要輸出「亮度係數」就等於把畫面壓暗／照亮：
//     輸出 = lerp(1 - 環境壓暗, 1, 光照量) × 光色偏移
// 這條式子與遊戲端 Atmosphere.shader 的 type 1（正常＋環境壓暗）分支**完全相同**，
// 所以編輯器看到的明暗與遊戲一致（差別只在遊戲的 2 幽暗/3 噩夢還會另外去飽和與加冷色調，這裡不模擬）。
//
// 為什麼用世界空間而不是 viewport：這是一張世界空間的四邊形，片段直接拿得到世界座標，
// 半徑就是「格」，不必像遊戲端那樣做 orthographicSize 換算，縮放/平移自動正確。
//
// 為什麼是「畫一張四邊形」而不是相機後處理（OnRenderImage）：
// 編輯器的參考線（光圈、選取框、格線）都畫在 OnPostRender，那是在相機算繪之後、後處理之前——
// 用後處理會把參考線一起壓暗看不清。改成四邊形參與正常算繪，OnPostRender 的線就會蓋在它上面，維持清楚。
Shader "Custom/EditorLightPreview"
{
    Properties
    {
        _EnvDark ("Env Darken (0..1)", Float) = 0.6
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend DstColor Zero        // 相乘：結果 = 已畫好的畫面 × 本 shader 輸出
        Cull Off
        ZWrite Off
        ZTest Always
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            // 編輯器端可以放寬（這支 shader 很短，不像遊戲端要跟 15 種氛圍擠指令數）。
            // 注意：遊戲端同框上限是 12 盞，所以這裡看得到 13 盞不代表遊戲看得到——
            // LightPreview.cs 會在「畫面內盞數 > 遊戲上限」時於面板出示警告。
            #define MAX_LIGHTS 32
            float4 _LightPos[MAX_LIGHTS];    // xy = 世界座標, z = 外圈半徑, w = 內圈半徑（世界單位）
            float4 _LightTint[MAX_LIGHTS];   // rgb = 光色, a = 亮度
            float  _LightCount;
            float  _EnvDark;                 // 環境壓暗量 0~1（＝1 − 環境亮度/100）

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float2 world : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 多盞用 screen 疊合（v + vi − v·vi），與遊戲端一致：兩圈交界自然變亮、不出現硬邊。
                float v = 0.0;
                float3 tintAcc = 0.0;
                int cnt = (int)_LightCount;
                [loop]
                for (int li = 0; li < MAX_LIGHTS; li++)
                {
                    if (li >= cnt) break;
                    float d = distance(i.world, _LightPos[li].xy);
                    float vi = 1.0 - smoothstep(_LightPos[li].w, _LightPos[li].z, d);
                    vi = saturate(vi * _LightTint[li].a);
                    tintAcc += _LightTint[li].rgb * vi;
                    v = v + vi - v * vi;
                }
                v = saturate(v);

                // 光色做亮度歸一 → 只改色相不改明暗（與遊戲端同一條式子）
                float3 avgTint = tintAcc / max(v, 0.001);
                float tintLum = max(dot(avgTint, float3(0.299, 0.587, 0.114)), 0.001);
                float3 lightShift = lerp(float3(1.0, 1.0, 1.0), avgTint / tintLum, 0.65 * v);

                float3 outCol = lerp(1.0 - _EnvDark, 1.0, v) * lightShift;
                return fixed4(outCol, 1.0);
            }
            ENDCG
        }
    }
}
