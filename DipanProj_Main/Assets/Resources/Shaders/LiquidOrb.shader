// 液體血球（HP/MP orb）——燃燈劫底部操控列用。Built-in 算繪管線、掛在 uGUI RawImage 上。
// 由 LiquidOrb.cs 每幀餵入參數：
//   _Fill  0..1  液面高度（= 當前值 / 上限）
//   _Slosh       搖晃量（帶正負；受擊/耗魔時被「阻尼彈簧」灌值，靜止時 0）
//   _T           時間（用 unscaledTime，暫停時液面仍微動）
//   _Color/_Color2 液體亮色 / 深色（HP 紅、MP 藍）
// 亮度旋鈕（都由 LiquidOrb.cs 設定，方便在暗場景調到不刺眼）：
//   _Bright      整體亮度倍率（越小越暗）
//   _Gloss       高光點強度（那顆白亮點；越小越不跳）
//   _RimStrength 玻璃邊緣亮環強度
//   _SurfStrength 液面亮邊強度
// 液面 = 依 _Fill 的水平線 + 兩道正弦漣漪（永遠微動）+ 搖晃傾斜/上下晃；
// 再疊球面明暗、液面亮邊、內部流動噪訊、玻璃邊緣亮環。見 readme/COMBAT.md、readme/UI_SYSTEM.md。
Shader "Custom/LiquidOrb"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Color  ("Liquid",  Color) = (0.44, 0.06, 0.05, 1)
        _Color2 ("Deep",    Color) = (0.13, 0.01, 0.02, 1)
        _Fill   ("Fill (0..1)", Range(0,1)) = 1
        _Slosh  ("Slosh", Float) = 0
        _T      ("Time",  Float) = 0
        _Bright       ("Brightness", Float) = 0.8
        _Gloss        ("Gloss",      Float) = 0.22
        _RimStrength  ("Rim",        Float) = 0.30
        _SurfStrength ("Surface",    Float) = 0.22
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
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };
            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                fixed4 color : COLOR;
            };

            fixed4 _Color;
            fixed4 _Color2;
            float  _Fill;
            float  _Slosh;
            float  _T;
            float  _Bright;
            float  _Gloss;
            float  _RimStrength;
            float  _SurfStrength;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = v.uv;
                o.color = v.color;
                return o;
            }

            float hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;   // [-1,1]，y 向上
                float d = length(p);
                float aa = fwidth(d) * 1.4 + 1e-4;
                float body = 1.0 - smoothstep(1.0 - aa, 1.0 + aa, d);   // 1 = 在圓內

                // 球面法線（讓平面圓看起來像球）
                float dd = min(d, 1.0);
                float nz = sqrt(max(0.0, 1.0 - dd * dd));
                float3 n = float3(p, nz);
                float3 L = normalize(float3(-0.45, 0.65, 0.8));
                float diff = saturate(dot(n, L));
                float spec = pow(diff, 34.0);          // 稍緊一點，白點更小

                // 液面線
                float yLevel = _Fill * 2.0 - 1.0;
                float amp = 0.018 + 0.045 * abs(_Slosh);
                float ripple = amp * sin(p.x * 7.0 + _T * 2.4)
                             + amp * 0.55 * sin(p.x * 14.0 - _T * 3.6);
                float tilt = _Slosh * p.x * 0.42;
                float bob  = _Slosh * 0.05 * sin(_T * 7.0);
                float waterLine = yLevel + ripple + tilt + bob;

                float s = waterLine - p.y;             // >0 = 在液面下
                float below = 1.0 - smoothstep(-0.008, 0.008, p.y - waterLine);

                // 液體
                float depth = saturate(s * 0.55);
                float3 liquid = lerp(_Color.rgb, _Color2.rgb, depth);   // 越深越暗
                float ca = vnoise(p * 3.5 + float2(_T * 0.5, -_T * 0.35));
                liquid += _Color.rgb * 0.10 * ca;                       // 內部流動感
                liquid *= _Bright * (0.60 + 0.52 * diff);              // 球面明暗 + 整體亮度
                float surf = 1.0 - smoothstep(0.0, 0.07, max(s, 0.0));
                liquid += surf * _SurfStrength;                        // 液面亮邊
                liquid += spec * _Gloss;                              // 高光點

                // 空的玻璃內壁
                float3 glass = _Color2.rgb * (0.22 * _Bright) * (0.5 + 0.55 * diff)
                             + spec * (_Gloss * 0.6);

                float3 col = lerp(glass, liquid, below);
                col *= 1.0 - smoothstep(0.82, 1.0, d) * 0.5;           // 內緣暗環（更沉）

                // 外緣玻璃亮邊
                float rim = smoothstep(0.90, 1.0, d) * (1.0 - smoothstep(1.0, 1.0 + aa, d));
                col += float3(0.80, 0.84, 0.95) * rim * _RimStrength;

                float alpha = body * i.color.a;        // 吃 CanvasGroup 淡入淡出
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
