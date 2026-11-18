// 焦土 sprite shader（2026-11-18，DipanVfx；地上燃燒的地面那一層，說明見 readme/DIPAN_VFX.md §4）。
//
// 兩張同尺寸的圖：
//   _MainTex  焦黑地面（RGB＝焦土顏色、alpha＝覆蓋範圍）——alpha 混合，會「蓋住」地板
//   _GlowTex  裂紋餘燼遮罩（只用 alpha）——加色發光，一明一暗
// 旋鈕（GroundFireBurst 每幀用 MaterialPropertyBlock 給）：
//   _Reveal  0→1：起火時從中心往外擴散燒開（邊緣不規則）
//   _Heat    1→0：熄火時餘燼由外往內冷卻
//   _Alpha   1→0：火滅後焦痕再慢慢淡掉
// 混合：premultiplied alpha（Blend One OneMinusSrcAlpha）：焦土 alpha 正常、餘燼 alpha 0 ⇒ 加色。
// ⚠ 用 UV 0~1：兩張圖都必須是單獨貼圖、不進圖集，Sprite Mesh Type＝Full Rect。
Shader "Custom/DipanScorch"
{
    Properties
    {
        [PerRendererData] _MainTex ("焦土圖", 2D) = "white" {}
        [PerRendererData] _GlowTex ("餘燼遮罩（alpha）", 2D) = "black" {}
        _NoiseTex ("噪聲（程式自動指定）", 2D) = "gray" {}

        _GlowColor ("餘燼顏色", Color) = (1, 0.45, 0.1, 1)
        _GlowIntensity ("餘燼亮度", Float) = 2
        _PulseSpeed ("餘燼明滅速度", Float) = 2.5

        _Reveal ("燒開 0→1", Range(0, 1)) = 1
        _Heat ("熱度 1→0", Range(0, 1)) = 1
        _Alpha ("焦痕透明度", Range(0, 1)) = 1
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
            "CanUseSpriteAtlas"="False"
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

            sampler2D _MainTex;
            sampler2D _GlowTex;
            sampler2D _NoiseTex;
            fixed4 _GlowColor;
            float _GlowIntensity, _PulseSpeed;
            float _Reveal, _Heat, _Alpha, _Seed, _Dim;

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
                float2 uv = i.uv;
                float r = length(uv - 0.5) * 2.0;                 // 0 中心、1 圖邊
                float n = tex2D(_NoiseTex, uv * 1.7 + _Seed).r;   // 不規則邊緣用

                // 燒開：中心先、往外擴，邊緣隨噪聲凹凸
                float reveal = saturate((_Reveal * 1.3 - r - (n - 0.5) * 0.35) * 6.0);

                float4 ground = tex2D(_MainTex, uv);
                float ca = ground.a * reveal * _Alpha * i.color.a;

                // 餘燼：外圈先冷卻；明滅相位跟著噪聲走，不會整片同步閃
                float heatLocal = saturate((_Heat * 1.35 - r * 0.6 - (n - 0.5) * 0.3) * 3.0);
                float pn = tex2D(_NoiseTex, uv * 2.3 + float2(_Seed * 0.5, _Time.y * 0.15)).g;
                float pulse = 0.55 + 0.45 * sin(_Time.y * _PulseSpeed + pn * 6.2831);
                float glow = tex2D(_GlowTex, uv).a * heatLocal * pulse * reveal * _GlowIntensity * i.color.a;

                float3 rgb = ground.rgb * i.color.rgb * ca * _Dim + _GlowColor.rgb * glow * _Dim;
                return float4(rgb, ca);
            }
            ENDCG
        }
    }
    Fallback Off
}
