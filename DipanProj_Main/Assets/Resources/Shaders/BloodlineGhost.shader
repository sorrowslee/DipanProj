// 饕餮幽靈（第三階神格特效「身邊兩隻若隱若現的水墨饕餮幽靈」）—— 由 Scripts/Gacha/BloodlineGhost.cs 掛在玩家身邊，一隻幽靈一個 quad。
// 見 readme/BLOODLINE.md §5b-2〈`GhostStyle`：身邊幽靈〉。
//
// ⭐ 溶解手法抄 `Custom/BloodlineMist` 的骷髏（同一套「煙霧密度圖當門檻」），差別：
//   ① 主體是一張水墨幽靈貼圖（灰階亮度＋alpha）：墨＝_InkColor、留白（眼、牙、雲紋）＝_PaleColor。
//   ② **尾巴會擺**：離頭越遠（_HeadUv）domain warp 越大，紋理往上流 ⇒ 墨煙尾巴像煙一樣飄；頭幾乎不動，臉看得清楚。
//   ③ 墨色在暗地板上讀不出形狀（BloodlineMist 的教訓①）⇒ 外圍加一圈暗紅微光（_GlowColor，8 點取樣的模糊 alpha 減掉本體）。
//   ④ **若隱若現**：整隻的不透明度緩慢起伏（_Flicker）＋局部煙紋讓墨色濃淡不均。
//   ⑤ 散去時**尾巴先散**、頭最後散（BloodlineMist 骷髏是上方先散）。
//
// ⚠ premultiplied alpha（Blend One OneMinusSrcAlpha），微光也要跟著溶解一起裁（BloodMist 同一個陷阱）。
// ⚠ Linear 色彩空間（PROBLEMS E11）：顏色放 Properties（自動 sRGB→線性）。
// ⚠ 改這裡的算式要連 TempImage/bloodline_Gluttonborn/fx/preview.py 一起改，否則預覽會騙人。
Shader "Custom/BloodlineGhost"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}   // 不取樣，只為了讓 SpriteRenderer 正常送資料

        _SmokeA ("厚重霧體（灰階密度圖，程式自動帶入）", 2D) = "black" {}
        _SmokeB ("細絮煙流（灰階密度圖，程式自動帶入）", 2D) = "black" {}
        _GhostTex ("幽靈（灰階亮度＋alpha，程式自動帶入）", 2D) = "black" {}

        _GhostT ("進度：0→1 凝結、1 停留、1→2 散去（程式每幀餵；<0 不顯示）", Float) = 1
        _Seed ("亂數種子（程式每次出現換一個）", Float) = 0
        _Flip ("左右鏡射（程式餵）", Float) = 0

        _InkColor ("墨色", Color) = (0.0024, 0.0018, 0.0024, 1)      // sRGB (8,6,8)
        _PaleColor ("留白（眼、牙、雲紋）", Color) = (0.75, 0.69, 0.55, 1)   // sRGB (225,216,196)
        _GlowColor ("外圍微光（讓墨在暗地板上讀得出輪廓）", Color) = (0.162, 0.008, 0.008, 1)  // sRGB (112,20,20)
        _GlowAmount ("外圍微光量", Range(0, 3)) = 1.0
        _GlowRadius ("外圍微光半徑（uv）", Range(0, 0.08)) = 0.025
        _Opacity ("整體不透明度", Range(0, 1)) = 0.88
        _Flicker ("若隱若現：整體不透明度起伏量", Range(0, 1)) = 0.35
        _FlickerSpeed ("若隱若現：起伏速度", Float) = 0.35
        _Mottle ("墨色濃淡不均", Range(0, 1)) = 0.25

        _HeadUv ("頭的中心（貼圖 uv；離這裡越遠越像尾巴）", Vector) = (0.5, 0.36, 0, 0)
        _TailStart ("從多遠開始算尾巴（uv）", Range(0, 0.6)) = 0.26
        _TailWarp ("尾巴擺動量", Range(0, 0.2)) = 0.07
        _HeadWarp ("頭的擺動量（小一點臉才看得清楚）", Range(0, 0.05)) = 0.006
        _WarpSpeed ("擺動往上流的速度", Float) = 0.12
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha   // premultiplied（見檔頭）

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            sampler2D _SmokeA, _SmokeB, _GhostTex;
            float4 _HeadUv;
            float4 _InkColor, _PaleColor, _GlowColor;   // float4 不是 fixed4（乘倍率時 fixed 會被夾住）
            float _GhostT, _Seed, _Flip;
            float _GlowAmount, _GlowRadius, _Opacity, _Flicker, _FlickerSpeed, _Mottle;
            float _TailStart, _TailWarp, _HeadWarp, _WarpSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float ghostA(float2 uv)
            {
                float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                return tex2D(_GhostTex, saturate(uv)).a * inside;
            }

            float4 frag (v2f i) : SV_Target
            {
                if (_GhostT < 0.0) return 0;
                float2 uv = i.uv;
                if (_Flip > 0.5) uv.x = 1.0 - uv.x;
                float t = _Time.y;

                // ── 尾巴遮罩：離頭越遠越像尾巴 ──
                float dh = length(uv - _HeadUv.xy);
                float tail = smoothstep(_TailStart, _TailStart + 0.28, dh);

                // ── 擺動：細絮煙流當位移，整體往上流 ──
                float2 wuv = uv * 0.8 + float2(_Seed * 0.37, _Seed * 0.11);
                float wx = tex2D(_SmokeB, wuv + float2( t * 0.013, -t * _WarpSpeed)).r;
                float wy = tex2D(_SmokeB, wuv + float2(0.37, 0.71) + float2(-t * 0.017, -t * _WarpSpeed * 0.8)).r;
                float2 w = float2(wx, wy) - 0.5;
                float2 guv = uv + w * (_TailWarp * tail + _HeadWarp) * 2.0;

                float4 s = tex2D(_GhostTex, saturate(guv));
                float inside = step(0.0, guv.x) * step(guv.x, 1.0) * step(0.0, guv.y) * step(guv.y, 1.0);
                float a = s.a * inside;
                float lum = s.r;

                // ── 上色：墨＋留白；墨色濃淡不均 ──
                float mot = tex2D(_SmokeA, uv * 1.3 + float2(_Seed, -t * 0.05)).r;
                float3 col = lerp(_InkColor.rgb, _PaleColor.rgb, lum);
                a *= lerp(1.0 - _Mottle, 1.0, mot) * lerp(1.0, 0.85, tail);

                // ── 外圍微光：模糊 alpha（8 點）減掉本體 ──
                float r = _GlowRadius;
                float blur = 0.0;
                blur += ghostA(guv + float2( r, 0)); blur += ghostA(guv + float2(-r, 0));
                blur += ghostA(guv + float2(0,  r)); blur += ghostA(guv + float2(0, -r));
                blur += ghostA(guv + float2( r,  r) * 0.707); blur += ghostA(guv + float2(-r,  r) * 0.707);
                blur += ghostA(guv + float2( r, -r) * 0.707); blur += ghostA(guv + float2(-r, -r) * 0.707);
                blur *= 0.125;
                float glow = saturate(blur - s.a * inside) * _GlowAmount;

                // ── 凝結 → 停留 → 尾巴先散 ──
                float m = tex2D(_SmokeB, uv * 0.9 + float2(0.13 + _Seed * 0.21, 0.57)).r;
                float form = saturate(_GhostT);
                float gone = saturate(_GhostT - 1.0);
                float th = 1.0 - form;                                         // 凝結：門檻由 1 降到 0
                float keep = smoothstep(th - 0.18, th + 0.18, m * 0.55 + max(a, blur) * 0.45);
                float v = gone * 1.6 - (1.0 - tail) * 0.55 + (m - 0.5) * 0.35;  // 散去：尾巴先散、頭最後
                keep *= 1.0 - smoothstep(0.0, 0.35, v);

                // ── 若隱若現：整隻不透明度緩慢起伏 ──
                float fl = tex2D(_SmokeA, float2(_Seed * 0.13 + t * _FlickerSpeed * 0.05, 0.5 + _Seed * 0.07)).r;
                float vis = lerp(1.0 - _Flicker, 1.0, smoothstep(0.2, 0.8, fl)) * _Opacity;

                float outA = saturate(a * keep * vis) * i.color.a;
                float3 glowRgb = _GlowColor.rgb * glow * keep * vis * i.color.a;   // ⚠ 微光跟著溶解一起裁
                float4 o;
                o.rgb = saturate(col * i.color.rgb * outA + glowRgb);
                o.a = saturate(outA + glow * keep * vis * 0.35 * i.color.a);
                return o;
            }
            ENDCG
        }
    }
    Fallback Off
}
