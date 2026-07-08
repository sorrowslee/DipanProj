// 穿隧道洞口的「光發散進來」光暈（放射光束＋霧感）。純程序、加法混合，鋪在黑底之上、洞口拱門之下。
// 取代原本烘死在貼圖裡的那圈粗邊柔暈：由洞口中心往外算，亮度隨距離柔和遞減（不再有硬邊），
// 依角度加放射狀光束、疊一層低頻霧感雜訊，並用 _Anim（外部餵 unscaledTime）讓光微微流動。
// 純 UI（Canvas/Image）用；顏色由 _Color（乘頂點色）。中心/半徑/散開/光束/霧感全參數化。
Shader "Custom/TunnelMouthGlow"
{
    Properties
    {
        _MainTex     ("Texture", 2D) = "white" {}
        _Color       ("Glow Color", Color) = (1,0.93,0.78,1)
        _Center      ("Center (uv)", Vector) = (0.5,0.5,0,0)
        _Radius      ("Radius", Float) = 0.18
        _Spread      ("Spread", Float) = 0.32
        _RayStrength ("Ray Strength", Float) = 0.6
        _RayFreq     ("Ray Freq", Float) = 8
        _RaySharp    ("Ray Sharpness", Float) = 2.2
        _Haze        ("Haze", Float) = 0.35
        _Anim        ("Anim Time", Float) = 0
        _Aspect      ("Aspect w/h", Float) = 1.777
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend One One   // 加法混合（黑底發光）

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };

            fixed4 _Color;
            float4 _Center;
            float _Radius, _Spread, _RayStrength, _RayFreq, _RaySharp, _Haze, _Anim, _Aspect;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float a = hash(i), b = hash(i + float2(1,0)), c = hash(i + float2(0,1)), d = hash(i + float2(1,1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float s = 0.0, a = 0.5;
                for (int k = 0; k < 4; k++) { s += a * vnoise(p); p *= 2.0; a *= 0.5; }
                return s;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv - _Center.xy;
                p.x *= _Aspect;               // 長寬校正 → 光暈是圓的不是橢圓
                float r = length(p);
                float ang = atan2(p.y, p.x);
                float2 adir = float2(cos(ang), sin(ang));   // 用單位向量取角度雜訊＝環繞無接縫

                // 徑向柔和遞減：洞口邊緣(_Radius)往外，在 _Spread 內散開消失
                float fall = 1.0 - smoothstep(_Radius, _Radius + _Spread, r);
                fall = pow(saturate(fall), 1.6);

                // 放射光束：只隨角度變化（沿半徑不變）＝一條條放射；隨 _Anim 微微流動
                float rn = fbm(adir * _RayFreq + float2(_Anim * 0.15, 0.0));
                float rays = pow(saturate(rn), _RaySharp);

                // 霧感：低頻雜訊，緩慢呼吸
                float haze = fbm(p * 3.0 + float2(0.0, _Anim * 0.05));

                float intensity = fall * (_Haze * (0.6 + 0.4 * haze) + _RayStrength * rays);
                intensity = saturate(intensity);

                float3 col = _Color.rgb * i.color.rgb * intensity;
                return fixed4(col, intensity);
            }
            ENDCG
        }
    }
    Fallback Off
}
