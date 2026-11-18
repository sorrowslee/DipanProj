// 地面衝擊波圈（2026-11-18，DipanVfx；爆炸用，說明見 readme/DIPAN_VFX.md §4b）。
// 純程序：在一張全白的正方形上畫一圈（UV 半徑 _Ring 附近、寬 _Width），噪聲讓圈斷斷續續；加色發光。
// 程式把整張縮放到「圈的半徑＝傷害半徑」，並隨時間放大、淡出（_Alpha）。
Shader "Custom/DipanShockRing"
{
    Properties
    {
        [PerRendererData] _MainTex ("（不用，SpriteRenderer 需要）", 2D) = "white" {}
        _NoiseTex ("噪聲（程式自動指定）", 2D) = "gray" {}
        _Color ("顏色", Color) = (1, 0.6, 0.24, 1)
        _Intensity ("亮度", Float) = 1.5
        _Ring ("圈的位置（UV 半徑）", Range(0.1, 1)) = 0.92
        _Width ("圈寬（UV）", Range(0.005, 0.5)) = 0.06
        _Alpha ("透明度", Range(0, 1)) = 1
        _Seed ("亂數種子", Float) = 0
        _Dim ("整體壓暗 (1=原樣)", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _NoiseTex;
            fixed4 _Color;
            float _Intensity, _Ring, _Width, _Alpha, _Seed, _Dim;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv    : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                float r = length(p) * 2.0;
                float ang = atan2(p.y, p.x) / 6.2831853;
                float n = tex2D(_NoiseTex, float2(ang * 3.0 + _Seed, r * 0.5 + _Seed * 0.3)).r;
                float w = _Width * (0.6 + 0.8 * n);
                float ring = saturate(1.0 - abs(r - _Ring) / max(w, 0.001));
                ring *= ring;
                // 圈內側拖一點淡淡的餘暉
                float inner = saturate(1.0 - (_Ring - r) / 0.25) * step(r, _Ring) * 0.25;
                float a = (ring + inner) * (0.55 + 0.45 * n) * _Alpha * i.color.a;
                float3 rgb = _Color.rgb * i.color.rgb * a * _Intensity * _Dim;
                return float4(rgb, 0);   // 純加色
            }
            ENDCG
        }
    }
    Fallback Off
}
