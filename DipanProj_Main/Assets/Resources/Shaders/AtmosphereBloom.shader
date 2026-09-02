// 氛圍 Bloom 的前置 pass（只給 Atmosphere 的室內系 mode 16/17 用）。
//
// 為什麼要獨立一支：Bloom 的「柔」來自**大模糊半徑**，而半徑一大、取樣數不跟著加就只會變成重影
// （見 readme/PROBLEMS.md **J5**）。在主 shader 那個單 pass 裡硬做，得塞三十幾個 tap，
// 那支已經把 19 種氛圍攤平、指令數吃緊（檔頭有註解），很容易撞編譯上限變洋紅。
//
// 所以改成降解析度的前置 pass，成本反而**比單 pass 硬做低一個數量級**：
//   先由 AtmosphereBlit 做一次**無材質 Blit 到 1/2**（硬體 bilinear 自動平均 2x2，等於免費的 box filter）。
//   Pass 0（1/2 → 1/4）：4-tap box + 亮部抽取（soft-knee threshold）。合計覆蓋原始畫面 8x8。
//     **逐級 2x 降採樣是抗鋸齒的關鍵**：一次跳 4x 就算塞 9 個 tap 覆蓋也只有 4x4，殘留的 moiré
//     會在相機移動時整片爬行（E27）。threshold 放在這一級而不是全解析度那級——它是非線性運算，
//     作用在高頻資料上會把微小亮度差放大成「有/沒有」，自己製造閃爍。
//   Pass 1（在 1/8 解析度的 RT 上跑）：9-tap tent 模糊。在 1/8 上的 9 tap ≈ 全螢幕的一大片，
//     等效半徑夠大才叫 bloom。
//   主 shader 再用一次雙線性取樣把它放大加回去——**放大本身就是額外的免費柔化**。
// 合計約等於 1.4 次全螢幕取樣；且只有 mode 16/17 會跑這兩個 pass，其餘氛圍完全不經過。
//
// 由 AtmosphereBlit 驅動（Graphics.Blit 指定 pass index），參數由 AtmosphereController 餵。
// ⚠⚠ 門檻是 **Linear 空間**的值，不是看截圖估的亮度。專案跑 Linear：截圖上 0.32 的中間灰，
//   shader 裡的 lum 只有 0.083。實測石材大廳 linear 分布 p50=0.083 / p90=0.135 / p99=0.20
//   ——**整張畫面都在 0.2 以下**，所以門檻 0.45 甚至 0.62 等於 bloom 恆為 0（前兩版都是這樣，
//   看起來像沒做）。0.09 ≈ 中位數，剛好讓「亮於平均的地方」開始溢光。
//   室內系是亮場景，bloom 要的是「光瀰漫」不是「亮點溢出」，門檻本來就該貼著中位數。
//   換場景要重定門檻時，量一張截圖的 linear percentile（方法見 readme/PROBLEMS.md E26），別用眼睛估。
Shader "Custom/AtmosphereBloom"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
        _Threshold ("亮部門檻（Linear 尺度！）", Float) = 0.09
        _Knee ("門檻柔邊", Float) = 0.06
        _Spread ("模糊擴散（來源 texel 倍數；越大越糊、殘餘斑紋越不可見）", Float) = 1.6
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_TexelSize;   // xy = 1/寬, 1/高（來源解析度）
        float _Threshold, _Knee, _Spread;

        struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

        v2f vert(appdata_img v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.texcoord;
            return o;
        }
        ENDCG

        // ── Pass 0：4-tap 平均降採樣 + 亮部抽取 ──
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            #pragma target 3.0

            fixed4 fragPrefilter(v2f i) : SV_Target
            {
                // 這一 pass 吃的是「已經被硬體 bilinear 降過一半」的畫面（AtmosphereBlit 先做一次無材質 Blit），
                // 所以這裡 4-tap／offset 半個 texel 就等於再平均 2x2 ⇒ **合計覆蓋原始畫面的 8x8**。
                // ⚠ 為什麼要分兩級、而不是一次跳 1/4：降採樣的低通要跟倍率相稱，一次跳 4x 就算塞 9 個 tap
                //   覆蓋仍只有 4x4，高頻紋理（石磚縫、環形紋樣）殘留的 moiré 在相機移動時會整片爬（**E27**）。
                //   逐級 2x 是標準解，而且靠硬體 bilinear 幫忙，總成本反而比單級塞更多 tap 低。
                // ⚠ threshold 也刻意留在這一級（不是全解析度那級）：它是非線性運算，
                //   直接作用在高頻資料上會把微小亮度差放大成「有/沒有」，自己製造閃爍。
                float2 d = _MainTex_TexelSize.xy * 0.5;
                float3 c = tex2D(_MainTex, i.uv + float2(-d.x, -d.y)).rgb;
                c += tex2D(_MainTex, i.uv + float2( d.x, -d.y)).rgb;
                c += tex2D(_MainTex, i.uv + float2(-d.x,  d.y)).rgb;
                c += tex2D(_MainTex, i.uv + float2( d.x,  d.y)).rgb;
                c *= 0.25;

                // soft-knee：門檻附近平滑進場，避免亮度剛好在門檻上下的像素一閃一閃。
                float lum = dot(c, float3(0.299, 0.587, 0.114));
                float k = smoothstep(_Threshold, _Threshold + max(0.001, _Knee), lum);
                return fixed4(c * k, 1.0);
            }
            ENDCG
        }

        // ── Pass 1：9-tap tent 模糊 ──
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            #pragma target 3.0

            fixed4 fragBlur(v2f i) : SV_Target
            {
                float2 o = _MainTex_TexelSize.xy * max(0.0, _Spread);
                float3 c = tex2D(_MainTex, i.uv).rgb * 0.25;
                c += tex2D(_MainTex, i.uv + float2( o.x, 0)).rgb * 0.125;
                c += tex2D(_MainTex, i.uv + float2(-o.x, 0)).rgb * 0.125;
                c += tex2D(_MainTex, i.uv + float2(0,  o.y)).rgb * 0.125;
                c += tex2D(_MainTex, i.uv + float2(0, -o.y)).rgb * 0.125;
                c += tex2D(_MainTex, i.uv + float2( o.x,  o.y)).rgb * 0.0625;
                c += tex2D(_MainTex, i.uv + float2(-o.x,  o.y)).rgb * 0.0625;
                c += tex2D(_MainTex, i.uv + float2( o.x, -o.y)).rgb * 0.0625;
                c += tex2D(_MainTex, i.uv + float2(-o.x, -o.y)).rgb * 0.0625;
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
