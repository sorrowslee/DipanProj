// 序章正面墜落用的「時空扭曲」UI shader。
// 把貼到的貼圖（放射速度線）做漩渦 + 漣漪的 UV 扭曲，
// 看起來像時空被攪動 —— 速度線本身保留，只是會波動旋擰。
// 純 UI（Canvas / RawImage）用：吃頂點色（Tint/alpha）、alpha 混合。
Shader "Custom/IntroWarp"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color   ("Tint", Color) = (1,1,1,1)
        _Amp     ("Ripple Amplitude", Float) = 0.035   // 漣漪幅度
        _Freq    ("Ripple Frequency", Float) = 5.0     // 漣漪密度
        _Speed   ("Animation Speed", Float) = 1.2      // 流動速度
        _Swirl   ("Swirl Amount", Float) = 0.6         // 漩渦強度
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Amp, _Freq, _Speed, _Swirl;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;

                // 以貼圖中心為極座標原點。
                float2 c = i.uv - 0.5;
                float r = length(c);
                float ang = atan2(c.y, c.x);

                // 漩渦：角度依半徑與時間擰動（越外圈擰越多 → 攪動感）。
                ang += _Swirl * sin(t * 0.8 + r * _Freq);

                // 漣漪：半徑做正弦起伏，像一圈圈漣漪往外推。
                float rr = r + _Amp * sin(r * _Freq * 3.14159 - t * 2.0);

                float2 uv = 0.5 + float2(cos(ang), sin(ang)) * rr;

                fixed4 col = tex2D(_MainTex, uv);
                col *= _Color * i.color;
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
