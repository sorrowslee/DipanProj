// 佛光護罩用的加色（Additive）Unlit Shader，帶「燈火忽強忽弱」明滅。
// 與 Custom/AdditiveGlow 的差別：本 shader 額外乘上貼圖 alpha（tex.a），
// 因此即使貼圖透明區的 RGB 不是黑色，additive 疊上去也不會出現方塊，只留柔邊光暈。
// 亮度由 SpriteRenderer 的 vertex color（color.a）與 _Intensity 共同驅動——
// GroundEffectInstance 每幀調 color.a 做不規則明滅。
Shader "Custom/AuraGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend One One
        Cull Off
        ZWrite Off
        Lighting Off

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
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TintColor;
            float _Intensity;

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
                fixed4 tex = tex2D(_MainTex, i.uv);
                // 乘 tex.a：保留貼圖透明邊（避免 additive 疊出方塊）；
                // 亮度 = color.a（每幀明滅）× _Intensity。
                fixed3 col = tex.rgb * tex.a * i.color.rgb * i.color.a * _Intensity * _TintColor.rgb;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
