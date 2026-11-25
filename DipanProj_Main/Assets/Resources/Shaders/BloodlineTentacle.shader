// 古神之嗣的「虛空觸手」（第三階神格特效：背後六條半透明章魚觸手亂舞）—— 由 Scripts/Gacha/BloodlineTentacle.cs 每幀建網格。
// 見 readme/BLOODLINE.md §5b〈`TentacleStyle`：虛空觸手〉。
//
// 網格＝沿每條觸手中心線鋪的帶子（同 DipanSpiritBody 的做法），頂點資料：
//   uv0.x＝皮貼圖座標（∫ds ÷ (皮厚×寬高比)；Wrap U＝Repeat ⇒ 越往尖端越細、吸盤跟著變小）
//   uv0.y＝橫向 −1~+1（網格邊；網格半寬＝可見半寬 × _GlowWidth，多出來的是輪廓光與煙絲）；**+1 一律是背**
//   uv1   ＝(沿身體長度 ÷ 角色高, 凝結進度 0→1)
//   uv2   ＝(散去進度 0→1, 亂數種子)
//   color ＝(r＝沿身體 0→1, g＝不透明度, b, a)
// 虛化手法＝BloodlineGhost／BloodlineMist 那套「煙霧密度圖」：本體斑駁半透明、凝結用門檻、散去尖端先散、外緣往尖端流的暗紫煙絲、青色輪廓光。
//
// ⚠ premultiplied alpha（Blend One OneMinusSrcAlpha）；輪廓光是加色，也要跟著溶解一起裁。
// ⚠ Linear 色彩空間（PROBLEMS E11）：顏色放 Properties（自動 sRGB→線性）。
// ⚠ 改這裡的算式要連 tools/art/bloodline_Eldritchborn/fx/sim.py 的 render_tentacle 一起改，否則預覽會騙人。
Shader "Custom/BloodlineTentacle"
{
    Properties
    {
        _MainTex ("觸手皮（程式自動帶入；左右無縫、上緣＝背、下緣＝吸盤）", 2D) = "white" {}
        _SmokeA ("厚重霧體（灰階密度圖，程式自動帶入）", 2D) = "black" {}
        _SmokeB ("細絮煙流（灰階密度圖，程式自動帶入）", 2D) = "black" {}

        _SkinBright ("皮的亮度（暗皮在暗地板上要調亮）", Float) = 2.2
        _RimColor ("輪廓光", Color) = (0.062, 0.753, 0.645, 1)      // sRGB (70,225,210)
        _RimAmount ("輪廓光量", Range(0, 2)) = 0.45
        _WispColor ("外緣煙絲", Color) = (0.021, 0.008, 0.034, 1)   // sRGB (40,22,52)
        _WispAmount ("外緣煙絲濃度", Range(0, 1)) = 0.45
        _GlowWidth ("網格半寬／可見半寬", Float) = 1.6
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

            sampler2D _MainTex, _SmokeA, _SmokeB;
            float4 _RimColor, _WispColor;
            float _SkinBright, _RimAmount, _WispAmount, _GlowWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv0    : TEXCOORD0;
                float2 uv1    : TEXCOORD1;
                float2 uv2    : TEXCOORD2;
            };
            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
                float2 uv0   : TEXCOORD0;
                float2 uv1   : TEXCOORD1;
                float2 uv2   : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color; o.uv0 = v.uv0; o.uv1 = v.uv1; o.uv2 = v.uv2;
                return o;
            }

            float ss(float a, float b, float x) { return smoothstep(a, b, x); }

            float4 frag(v2f i) : SV_Target
            {
                float v = i.uv0.y * _GlowWidth;          // ÷ 可見半寬：|v|<1 在身體內，+1＝背
                float av = abs(v);
                float s = i.color.r;                     // 沿身體 0（根）→ 1（尖）
                float opacity = i.color.g;
                float sl = i.uv1.x;                      // 沿身體長度（角色高為 1）
                float form = i.uv1.y;
                float gone = i.uv2.x;
                float seed = i.uv2.y;
                float tm = _Time.y + seed * 7.0;

                // 皮
                float4 skin = tex2D(_MainTex, float2(i.uv0.x, saturate(0.5 + 0.5 * v * 0.98)));
                float3 col = skin.rgb * _SkinBright;

                // 斑駁（半透明、濃淡不均）
                float n1 = tex2D(_SmokeA, float2(sl * 1.1 - tm * 0.35, v * 0.25 + seed)).r;
                float n2 = tex2D(_SmokeB, float2(sl * 2.3 - tm * 0.6, v * 0.5 + 0.3 + seed)).r;
                float mott = 0.30 + 0.70 * ss(0.25, 0.75, n1 * 0.6 + n2 * 0.4);
                float body = (1.0 - ss(0.82, 1.0, av)) * step(av, 1.0) * skin.a;
                float rootFade = ss(0.0, 0.12, s);                 // 根部淡入（藏在身後）
                float tipFade = 1.0 - ss(0.88, 1.0, s) * 0.6;

                // 溶解：凝結＝門檻往下；散去＝尖端先散
                float m = tex2D(_SmokeB, float2(sl * 0.9 + seed * 0.21, v * 0.3 + 0.57)).r;
                float th = 1.0 - form;
                float keep = ss(th - 0.15, th + 0.15, m * 0.6 + 0.4 * (1.0 - s * 0.3));
                float vv = gone * 1.7 - (1.0 - s) * 0.6 + (m - 0.5) * 0.4;
                keep *= 1.0 - ss(0.0, 0.3, vv);

                float alpha = body * mott * opacity * rootFade * tipFade * keep;

                // 輪廓光（加色）
                float rd = (av - 0.95) / 0.16;                 // ⚠ 不用 pow(負數, 2)：HLSL 的 pow 對負底數未定義
                float rim = exp(-rd * rd) * step(av, _GlowWidth);
                rim *= (0.35 + 0.65 * n2) * rootFade * keep * tipFade;

                // 外緣煙絲：輪廓外、往尖端流的噪聲（遮擋）
                float w = tex2D(_SmokeA, float2(sl * 1.7 - tm * 0.8, v * 0.45 + seed * 0.3)).r;
                float wisp = ss(0.55, 0.8, w) * (1.0 - ss(1.0, _GlowWidth, av)) * step(0.9, av);
                wisp *= _WispAmount * rootFade * keep * tipFade;
                alpha = alpha + wisp * (1.0 - alpha);
                col = av > 0.9 ? _WispColor.rgb : col;

                float3 rgb = col * alpha + _RimColor.rgb * rim * _RimAmount;
                return float4(max(rgb, 0), saturate(alpha));
            }
            ENDCG
        }
    }
    Fallback Off
}
