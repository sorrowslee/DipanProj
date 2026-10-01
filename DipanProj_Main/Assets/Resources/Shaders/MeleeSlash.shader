// 近戰揮擊刀光（程序化，零素材）——血月鬼爪的「三道爪痕沿扇形掃過」。
//
// 為什麼不用序列圖：舊版是 64px 的像素爪痕放大 2.2 倍，畫面上只有 1.4 單位、判定卻是半徑 2.1 的扇形，
// 看到的比打到的小一半、形狀也不對（素材是往前直刺，判定是 110° 橫掃）。程序化畫在「判定那個扇形」上，
// 範圍與形狀永遠跟判定一致，多大都銳利（須彌珠／集氣放大也不糊）。
//
// 風格照 readme/art_direction/VFX_GUIDELINE.md §1：**畫出來的，不是渲染出來的**——
// 硬色階（白熱核心／血紅／暗紅）＋黑色撕裂邊，沒有高斯光暈。亮部只在爪尖小面積（§2.3）。
//
// 幾何：quad 以揮擊圓心為中心、邊長＝2×判定半徑，C# 端把 transform 轉到瞄準方向（local +x＝瞄準）。
//   uv → p＝(uv−0.5)×2 → r＝|p|（1＝判定半徑）、θ＝atan2(p)（相對瞄準方向，逆時針為正）。
// 三道爪痕：半徑 0.50／0.69／0.87、寬 0.070／0.092／0.078、起點延遲 0／0.06／0.12（外側最長也最晚到，有撕開的層次）。
//   每道從扇形一側掃到另一側（_Dir＝+1 由右往左、−1 反之＝左右爪交替），沿痕兩端尖、中段粗、往內彎；
//   尾端隨時間變老（色階往下掉）＝拖尾感。
//
// ⚠ 這支的公式與作者看過的預覽（Python 渲染）逐行一致，改參數請同時記在 readme/RECIPE_DESCRIBE.md〈Melee〉。
// ⚠ premultiplied alpha（Blend One OneMinusSrcAlpha）：多層 over 合成才正確。
// ⚠ 專案是 Linear 色彩空間（PROBLEMS E11）：半透明的扇面底 _SectorAlpha 取保守值，實機覺得太重先降它。
Shader "Custom/MeleeSlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}   // 不取樣，只為了讓 SpriteRenderer 正常送資料
        _CoreColor  ("爪尖白熱", Color) = (1.0, 0.92, 0.88, 1)
        _MainColor  ("主色（血紅）", Color) = (0.84, 0.09, 0.16, 1)
        _DarkColor  ("暗紅外緣", Color) = (0.40, 0.02, 0.07, 1)
        _RimColor   ("黑色撕裂邊", Color) = (0.07, 0.01, 0.03, 1)
        _T          ("經過秒數", Float) = 0
        _Dir        ("掃動方向 +1/-1", Float) = 1
        _HalfAngle  ("扇形半角（弧度）", Float) = 0.96
        _Sweep      ("掃過秒數", Float) = 0.10
        _Hold       ("停留秒數", Float) = 0.03
        _FadeTime   ("淡出秒數", Float) = 0.12
        _SectorAlpha("扇面底 alpha", Range(0,0.5)) = 0.07
        _Bend       ("爪痕往內彎", Range(0,0.2)) = 0.06
        _Taper      ("兩端收尖程度", Range(0.1,2)) = 0.55
        _Tear       ("撕裂鋸齒", Range(0,1)) = 0.35
        _RimW       ("黑邊寬（相對爪痕寬）", Range(0,1)) = 0.40
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            fixed4 _CoreColor, _MainColor, _DarkColor, _RimColor;
            float _T, _Dir, _HalfAngle, _Sweep, _Hold, _FadeTime, _SectorAlpha, _Bend, _Taper, _Tear, _RimW;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // 三道爪痕 (半徑, 寬, 起點延遲)。放 global static const：函式內的區域常數陣列有些平台編不過。
            static const float3 CLAWS[3] = { float3(0.50, 0.070, 0.00), float3(0.69, 0.092, 0.06), float3(0.87, 0.078, 0.12) };

            float hash1(float x) { return frac(sin(x * 127.1 + 311.7) * 43758.5453); }
            float ease(float t) { float k = 1.0 - t; return 1.0 - k * k * k; }

            // premultiplied over：把顏色 c、覆蓋率 aa 疊到 (pm, a) 上
            void over(inout float3 pm, inout float a, float3 c, float aa)
            {
                pm = pm * (1.0 - aa) + c * aa;
                a  = a  * (1.0 - aa) + aa;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float r = length(p);
                float th = atan2(p.y, p.x);          // 相對瞄準方向（C# 已把 quad 轉到瞄準方向）
                float H = _HalfAngle;
                float dir = _Dir >= 0 ? 1.0 : -1.0;

                float fade = (_T <= _Sweep + _Hold) ? 1.0 : saturate(1.0 - (_T - _Sweep - _Hold) / max(_FadeTime, 1e-4));
                float th0 = -dir * H;
                float swept = dir * (th - th0);

                float3 pm = 0; float a = 0;

                // ── 扇面底（很淡，給範圍感；只在已掃過的角度）──
                float s0 = ease(saturate(_T / max(_Sweep, 1e-4)));
                float insec = (swept >= 0 && swept <= 2.0 * H * s0 && r > 0.2 && r < 1.0) ? 1.0 : 0.0;
                float sa = _SectorAlpha * fade * saturate((r - 0.2) / 0.3) * saturate((1.0 - r) / 0.08) * insec;
                over(pm, a, _DarkColor.rgb, sa);

                // ── 三道爪痕 ──
                [unroll]
                for (int k = 0; k < 3; k++)
                {
                    float rc = CLAWS[k].x, w = CLAWS[k].y, delay = CLAWS[k].z;
                    float s = ease(saturate((_T - delay * _Sweep) / max(_Sweep * (1.0 - delay), 1e-4)));
                    float span = 2.0 * H * s;
                    if (span < 1e-4) continue;

                    float u = dir * (th - th0) / span;          // 0＝尾、1＝頭（爪尖）
                    float valid = (u >= 0.0 && u <= 1.0) ? 1.0 : 0.0;
                    float uc = saturate(u);
                    float center = rc - _Bend * sin(UNITY_PI * uc);          // 往內彎
                    float prof = pow(max(sin(UNITY_PI * uc), 0.0), _Taper);  // 兩端尖
                    float n = hash1(floor((th + 10.0) * 38.0) + k * 17.0);   // 撕裂鋸齒（沿角度的階梯雜訊＝硬邊）
                    float width = w * prof * (1.0 - _Tear * 0.5 + _Tear * n) * (0.45 + 0.55 * fade);
                    float d = abs(r - center);

                    float I = 1.0 - d / max(width, 1e-5);
                    float tip = saturate((uc - 0.55) / 0.45);
                    I = I - 0.45 * (1.0 - uc) * (_T > _Sweep * 0.5 ? 1.0 : 0.0) - 0.35 * (1.0 - fade);
                    float coreI = I + 0.55 * tip - 0.45;

                    float rimz  = (valid > 0 && d >= width && d < width * (1.0 + _RimW) && prof > 0.08) ? 1.0 : 0.0;
                    float core  = (valid > 0 && coreI > 0.35) ? 1.0 : 0.0;
                    float mainz = (valid > 0 && I > 0.28 && core < 0.5) ? 1.0 : 0.0;
                    float darkz = (valid > 0 && I > 0.0 && core < 0.5 && mainz < 0.5) ? 1.0 : 0.0;

                    over(pm, a, _RimColor.rgb,  rimz  * 0.85 * fade);
                    over(pm, a, _DarkColor.rgb, darkz * 0.95 * fade);
                    over(pm, a, _MainColor.rgb, mainz * 1.00 * fade);
                    over(pm, a, _CoreColor.rgb, core  * 1.00 * fade);
                }

                // SpriteRenderer 的 color.a 當整體淡出倍率（預設 1）
                return fixed4(pm * i.color.a, a * i.color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
