// 萬魂黑霧（第三階神格特效「常駐黑霧＋霧中凝結骷髏」）—— 由 Scripts/Gacha/BloodlineMist.cs 掛在玩家身後。
// 見 readme/BLOODLINE.md §5b〈`MistStyle`：身後黑霧〉。
//
// ⭐ **骨架是 `Custom/BloodMist`（血霧衛士）**，那支又是 `UI/BossAura`（Boss 開戰黑霧）的世界版：
//   兩張灰階煙霧密度圖當原料 → domain warp 翻騰 → 厚重層 screen 疊 → 細絮 → 徑向遮罩被霧體啃邊 → 吹散。
//   差別：
//     ① 霧團是**往上竄的焰形**（越高越窄、頂端散掉、底部收掉），整體往上流——「身上不斷散發」，不是一團包住的霧。
//     ② 畫在**角色之下**（元件給 sortingOrder −1），中央被身體擋住、只從輪廓冒出來，角色本人看得清楚。
//     ③ 主體**幾乎純黑**，只有最外緣一窄條紫色冥光（_RimAmount）＋稀薄處的紫色細絲——
//        純黑在暗地板上讀不出形狀（2026-11-09 numpy 預覽第二輪實測），整團紫又變成紫火（第一、三輪）。
//     ④ 多一張骷髏貼圖：元件餵 _SkullPos／_SkullT，用煙霧密度圖做溶解門檻 ⇒ 「霧凝結成骷髏 → 停留 → 上方先散回霧裡」。
//
// ⚠⚠ 不要改回純程序 noise（BossAura／BloodMist 檔頭同一條）：fbm 生不出煙的絲與捲，形狀靠那兩張美術畫的密度圖。
// ⚠ premultiplied alpha（Blend One OneMinusSrcAlpha），散射光要跟著吹散一起裁（BloodMist 同一個陷阱）。
// ⚠ Linear 色彩空間（PROBLEMS E11）：顏色放 Properties（自動 sRGB→線性）。
// 2026-11-09 先用 numpy 照同一套算式＋真的兩張密度圖＋該隱 idle 算靜態預覽，暗底看四輪才進 shader
//   （TempImage/bloodline_Netherborn/fx/preview.py 是對照版——改這裡的算式要兩邊一起改，否則預覽會騙人）。
Shader "Custom/BloodlineMist"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}   // 不取樣，只為了讓 SpriteRenderer 正常送資料

        _SmokeA ("厚重霧體（灰階密度圖，程式自動帶入）", 2D) = "black" {}
        _SmokeB ("細絮煙流（灰階密度圖，程式自動帶入）", 2D) = "black" {}
        _SkullTex ("骷髏（灰階亮度＋alpha，程式自動帶入）", 2D) = "black" {}

        _Fade ("顯現程度（0=吹散光 1=完整；程式每幀餵）", Range(0, 1)) = 1
        _WorldOffset ("紋理的世界偏移（程式每幀餵）", Vector) = (0, 0, 0, 0)
        _Seed ("亂數種子", Float) = 0
        _SkullPos ("骷髏：中心 uv.xy、大小 z（佔 quad 寬）、w＝quad 寬/高（程式每幀餵）", Vector) = (0.37, 0.56, 0.24, 0.708)
        _SkullT ("骷髏進度：0→1 凝結、1→2 散掉（程式每幀餵；<0 = 沒有骷髏）", Float) = -1

        _DeepColor ("霧本體（近黑紫）", Color) = (0.0196, 0.0118, 0.0353, 1)   // sRGB (5,3,9)
        _GlowColor ("冥光（邊緣與細絲）", Color) = (0.376, 0.188, 0.612, 1)    // sRGB (96,48,156)
        _SkullColor ("骷髏亮部", Color) = (0.549, 0.478, 0.706, 1)            // sRGB (140,122,180)
        _BodyTint ("本體帶一點冥光的量（0＝純黑）", Range(0, 1)) = 0.15
        _WispBoost ("紫色細絲量", Range(0, 2)) = 0.35
        _RimAmount ("輪廓冥光量（黑霧在暗地板上靠它讀出形狀）", Range(0, 2)) = 0.55
        _Scatter ("散射加光", Range(0, 1)) = 0.05
        _SkullAlpha ("骷髏濃度", Range(0, 1)) = 0.85

        _DensityLo ("密度下限（往上＝霧變稀）", Range(0, 1)) = 0.10
        _DensityHi ("密度上限（往下＝霧變濃）", Range(0, 1)) = 0.85
        _FogMinAlpha ("霧最薄處的不透明度", Range(0, 1)) = 0.55
        _MaxOpacity ("霧最濃處的不透明度", Range(0, 1)) = 0.95

        _TexScale ("紋理尺度（越大霧越碎）", Float) = 1.2
        _FlowSpeed ("翻騰速度", Float) = 0.8
        _RiseSpeed ("往上流的速度", Float) = 0.10
        _WarpAmount ("翻騰扭曲強度", Range(0, 0.3)) = 0.12

        _CenterY ("霧團重心高度（uv）", Range(0, 1)) = 0.45
        _InnerRadius ("霧團實心半徑（uv）", Range(0, 0.5)) = 0.10
        _OuterRadius ("霧團外緣半徑（uv）", Range(0, 0.5)) = 0.33
        _EdgeBite ("外緣被霧體啃掉的量（0＝圓盤）", Range(0, 0.6)) = 0.30
        _TopNarrow ("頂端收窄（1＝不收）", Range(0.2, 1)) = 0.55
        _TopFade0 ("頂端開始散掉的高度（uv）", Range(0, 1)) = 0.55
        _TopFade1 ("頂端散光的高度（uv）", Range(0, 1)) = 0.98

        // ── 吹散（_Fade；與 BloodMist／BossAura 同義同值）──
        _Rise ("吹散：往上飄的量", Float) = 0.45
        _Turb ("吹散：橫向湍流", Float) = 0.09
        _EdgeSoft ("吹散：破口柔和度", Range(0.01, 0.6)) = 0.26
        _UpBias ("吹散：上方先散的偏量", Range(0, 1)) = 0.35
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

            sampler2D _SmokeA, _SmokeB, _SkullTex;
            float4 _WorldOffset, _SkullPos;
            float4 _DeepColor, _GlowColor, _SkullColor;   // float4 不是 fixed4（乘倍率時 fixed 會被夾住）
            float _Fade, _Seed, _SkullT;
            float _BodyTint, _WispBoost, _RimAmount, _Scatter, _SkullAlpha;
            float _DensityLo, _DensityHi, _FogMinAlpha, _MaxOpacity;
            float _TexScale, _FlowSpeed, _RiseSpeed, _WarpAmount;
            float _CenterY, _InnerRadius, _OuterRadius, _EdgeBite, _TopNarrow, _TopFade0, _TopFade1;
            float _Rise, _Turb, _EdgeSoft, _UpBias;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float2 rot2(float2 p, float a) { float s = sin(a), c = cos(a); return float2(c * p.x - s * p.y, s * p.x + c * p.y); }

            float4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float p = 1.0 - saturate(_Fade);
                float t = _Time.y * _FlowSpeed;

                // ── 霧團形狀：往上竄的焰形（越高越窄）──
                float2 q = float2(uv.x - 0.5, uv.y - _CenterY);
                float wfac = lerp(1.0, _TopNarrow, saturate(uv.y));
                float d = length(float2(q.x / wfac * 1.2, q.y * 0.8));

                // ── 取樣座標：整體往上流＋世界偏移＋種子 ──
                float2 suv = uv * _TexScale + _WorldOffset.xy + _Seed;
                suv.y -= t * _RiseSpeed;
                suv.y -= (p * p) * _Rise * 0.9;
                suv.x += sin(uv.y * 7.0 + t * 1.3) * _Turb * p;

                // ── domain warp ──
                float wx = tex2D(_SmokeB, suv * 0.25 + float2( t * 0.013, -t * 0.021)).r;
                float wy = tex2D(_SmokeB, suv * 0.25 + float2(0.37, 0.71) + float2(-t * 0.017, t * 0.011)).r;
                float2 wuv = suv + (float2(wx, wy) - 0.5) * (_WarpAmount * 2.0);

                // ── 厚重層（screen 疊）＋細絮層（max）──
                float a1 = tex2D(_SmokeA, rot2(wuv, 0.00) * 0.42 + float2( t * 0.010, -t * 0.022)).r;
                float a2 = tex2D(_SmokeA, rot2(wuv, 2.10) * 0.75 + float2(-t * 0.015, -t * 0.031)).r;
                float thick = 1.0 - (1.0 - a1) * (1.0 - a2);
                float b1 = tex2D(_SmokeB, rot2(wuv, 1.10) * 0.60 + float2( t * 0.021, -t * 0.040)).r;
                float b2 = tex2D(_SmokeB, rot2(wuv, 4.00) * 1.05 + float2(-t * 0.029, -t * 0.055)).r;
                float wisp = max(b1, b2);

                // ── 徑向遮罩（外緣被啃）＋頂端散掉＋底部收掉 ──
                float radial = 1.0 - smoothstep(_InnerRadius, _OuterRadius, d - (thick - 0.5) * _EdgeBite);
                radial *= 1.0 - smoothstep(_TopFade0, _TopFade1, uv.y) * (1.0 - thick * 0.5);
                radial *= smoothstep(0.02, 0.16, uv.y);
                float body = smoothstep(_DensityLo, _DensityHi, thick) * radial;

                // ── 上色：本體近黑；紫只給細絲（偏稀薄處）與最外緣一窄條 ──
                float3 col = lerp(_DeepColor.rgb, _GlowColor.rgb, saturate(body * _BodyTint));
                col += _GlowColor.rgb * wisp * _WispBoost * body * (1.0 - body * 0.6);
                float rim = smoothstep(0.02, 0.18, radial) * (1.0 - smoothstep(0.22, 0.45, radial)) * smoothstep(0.05, 0.5, thick);
                col += _GlowColor.rgb * rim * _RimAmount;

                float a = lerp(_FogMinAlpha, _MaxOpacity, body) * radial;
                float3 scatter = _GlowColor.rgb * (body * _Scatter + rim * 0.05);

                // ── 骷髏：霧凝結成形 → 停留 → 上方先散 ──
                if (_SkullT >= 0.0)
                {
                    float2 k = (uv - _SkullPos.xy) / max(_SkullPos.z, 1e-3) * float2(1.0, 1.0 / max(_SkullPos.w, 1e-3)) + 0.5;   // w = quad 寬/高，讓骷髏在世界裡是正方形
                    float inside = step(0.0, k.x) * step(k.x, 1.0) * step(0.0, k.y) * step(k.y, 1.0);
                    float4 s = tex2D(_SkullTex, saturate(k));
                    float sa = s.a * inside;
                    float lum = s.r;
                    float m = tex2D(_SmokeB, k * 0.9 + float2(0.13, 0.57)).r;
                    float form = saturate(_SkullT);
                    float gone = saturate(_SkullT - 1.0);
                    float th = 1.0 - form;                                   // 凝結：門檻由 1 降到 0
                    float keepS = smoothstep(th - 0.18, th + 0.18, m * 0.55 + sa * 0.45);
                    keepS *= 1.0 - smoothstep(0.0, 1.0, gone * 1.4 - (1.0 - k.y) * 0.4);   // 散掉：上方先散
                    float sk = sa * keepS * _SkullAlpha;
                    float3 skc = lerp(_DeepColor.rgb, _SkullColor.rgb, lum);
                    col = lerp(col, skc, sk);
                    a = max(a, sk * 0.92);
                    scatter += _GlowColor.rgb * sk * lum * 0.10;
                }

                // ── 吹散裁切（_Fade：出現時由下往上長出來；消失時上方先散）──
                float mm = tex2D(_SmokeA, uv * 1.9 + 0.31 + _Seed).r;
                float lo = -_EdgeSoft - 0.5 * _UpBias;
                float hi = 1.0 + _EdgeSoft + 0.5 * _UpBias;
                float thr = lerp(lo, hi, p) + (uv.y - 0.5) * _UpBias;
                float keep = smoothstep(thr - _EdgeSoft, thr + _EdgeSoft, mm);
                a *= keep;
                scatter *= keep;   // ⚠ 散射光也要一起裁（premultiplied 陷阱）

                float outA = saturate(a) * i.color.a;
                float4 o;
                o.rgb = saturate(col * i.color.rgb * outA + scatter * i.color.a);
                o.a = outA;
                return o;
            }
            ENDCG
        }
    }
    Fallback Off
}
