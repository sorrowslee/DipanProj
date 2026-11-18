// 圓形光暈用的簡單加色（Additive）Unlit Shader：把貼圖的 rgb 乘頂點色後 additive 疊上（不看 alpha）。
// 目前用在血統光環（BloodlineHalo）與掃射源點光球（SweepStrike）。
// 原本是舊雷射砲口/命中光暈用的；2026-11-18 雷射改用 DipanVfx 的 BeamVisual（Custom/DipanBeamOrb）後，舊光暈素材已刪除。
Shader "Custom/AdditiveGlow"
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
                fixed3 col = tex.rgb * i.color.rgb * _TintColor.rgb * i.color.a * _Intensity;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
