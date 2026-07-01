// 穿隧道洞口用的「失焦朦朧」UI 模糊 shader。
// 對貼到的貼圖（程式畫的拱門洞口）做多點取樣高斯模糊，營造朦朦朧朧、舉步蹣跚的視覺。
// 純 UI（Canvas / Image）用：吃頂點色（Tint/alpha）、alpha 混合。
// 模糊量由 _BlurSize（UV 位移）控制；0＝幾乎不糊。
Shader "Custom/TunnelBlur"
{
    Properties
    {
        _MainTex  ("Texture", 2D) = "white" {}
        _Color    ("Tint", Color) = (1,1,1,1)
        _BlurSize ("Blur Size (UV)", Float) = 0.008
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
            float _BlurSize;

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
                float b = _BlurSize;

                // 13 點兩圈高斯：中心 + 內圈(軸向/對角) + 外圈(軸向)，權重總和 20 → 柔化成「失焦朦朧」。
                fixed4 sum = tex2D(_MainTex, i.uv) * 4.0;

                sum += tex2D(_MainTex, i.uv + float2( b, 0)) * 2.0;
                sum += tex2D(_MainTex, i.uv + float2(-b, 0)) * 2.0;
                sum += tex2D(_MainTex, i.uv + float2( 0, b)) * 2.0;
                sum += tex2D(_MainTex, i.uv + float2( 0,-b)) * 2.0;

                sum += tex2D(_MainTex, i.uv + float2( b, b)) * 1.0;
                sum += tex2D(_MainTex, i.uv + float2( b,-b)) * 1.0;
                sum += tex2D(_MainTex, i.uv + float2(-b, b)) * 1.0;
                sum += tex2D(_MainTex, i.uv + float2(-b,-b)) * 1.0;

                sum += tex2D(_MainTex, i.uv + float2( 2.0*b, 0)) * 1.0;
                sum += tex2D(_MainTex, i.uv + float2(-2.0*b, 0)) * 1.0;
                sum += tex2D(_MainTex, i.uv + float2( 0, 2.0*b)) * 1.0;
                sum += tex2D(_MainTex, i.uv + float2( 0,-2.0*b)) * 1.0;

                fixed4 col = sum / 20.0;
                col *= _Color * i.color;
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
