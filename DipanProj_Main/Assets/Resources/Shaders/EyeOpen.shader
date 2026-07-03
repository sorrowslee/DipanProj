// 進場睜眼一次性後處理（Built-in 算繪管線）——由 EyeOpenController 播放一次。
// 承接墜落昏迷 → 睜開眼睛：上下眼皮/杏眼狀開合遮罩 + 視線由模糊轉清晰 + 亮度由暗回正 + 暗角。
// 由控制器每幀餵入時間軸參數：
//   _Open   0=完全閉眼(全黑) → 1=完全睜開(整片清晰)
//   _Bright 0=全黑 → 1=正常亮度
//   _Blur   模糊半徑(UV 單位，剛醒最大 → 對焦後 0)
//   _Aspect 畫面寬高比(讓暗角/杏眼形狀不被拉伸)
//   _Feather 眼皮邊緣柔化寬度(常數即可)
// 只影響主相機算繪的畫面；Screen Space Overlay 的 UI 在其後合成，不受影響。
Shader "Custom/EyeOpen"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Open ("Open (0 closed..1 open)", Float) = 0
        _Bright ("Brightness", Float) = 1
        _Blur ("Blur radius (uv)", Float) = 0
        _Aspect ("Aspect (w/h)", Float) = 1.777
        _Feather ("Lid feather", Float) = 0.06
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Open, _Bright, _Blur, _Aspect, _Feather;

            // 八方向 + 中心的圓盤模糊（半徑 r，UV 單位）。r=0 時等同直接取樣。
            fixed3 blurSample(float2 uv, float r)
            {
                if (r <= 0.00001) return tex2D(_MainTex, uv).rgb;
                const float k = 0.70710678; // 1/sqrt(2)，對角方向
                fixed3 c = tex2D(_MainTex, uv).rgb * 0.28;
                c += tex2D(_MainTex, uv + float2( r, 0)).rgb * 0.09;
                c += tex2D(_MainTex, uv + float2(-r, 0)).rgb * 0.09;
                c += tex2D(_MainTex, uv + float2( 0, r)).rgb * 0.09;
                c += tex2D(_MainTex, uv + float2( 0,-r)).rgb * 0.09;
                c += tex2D(_MainTex, uv + float2( r*k,  r*k)).rgb * 0.09;
                c += tex2D(_MainTex, uv + float2(-r*k,  r*k)).rgb * 0.09;
                c += tex2D(_MainTex, uv + float2( r*k, -r*k)).rgb * 0.09;
                c += tex2D(_MainTex, uv + float2(-r*k, -r*k)).rgb * 0.09;
                return c;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                // ── 視線：模糊 → 清晰、暗 → 正常 ──
                fixed3 scene = blurSample(uv, _Blur) * _Bright;

                // 暗角：越沒睜開越重（模擬剛醒周邊發黑），全開時幾乎無。
                float rad = length((uv - 0.5) * float2(_Aspect, 1.0));
                float vig = 1.0 - saturate((rad - 0.34) / 0.55) * (1.0 - _Open) * 0.65;
                scene *= vig;

                // ── 眼皮遮罩：上下對稱，杏眼形狀（中間開得多、兩側窄），全開時回到整片清晰 ──
                float dy = abs(uv.y - 0.5);              // 0=中線 .. 0.5=上下緣
                float cx = (uv.x - 0.5) * 2.0;           // -1..1
                float almond = sqrt(saturate(1.0 - cx * cx * 0.85));  // 中央1、兩側~0.39
                float shape = lerp(almond, 1.0, _Open);  // 越開越接近整片(全開清乾淨)
                float halfOpen = _Open * 0.55 * shape;   // 0.55>0.5：_Open=1 時完全露出
                float lidMask = smoothstep(0.0, _Feather, dy - halfOpen); // 0=露出、1=眼皮(黑)

                fixed3 outc = lerp(scene, fixed3(0, 0, 0), lidMask);
                return fixed4(outc, 1.0);
            }
            ENDCG
        }
    }
}
