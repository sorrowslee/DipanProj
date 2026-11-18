// 爆炸火球／黑煙 sprite shader（2026-11-18，DipanVfx；說明見 readme/DIPAN_VFX.md §4b）。
//
// 一張靜態「火球雲」圖（alpha＝密度／熱度，白色＋alpha 的 png）＋程式每幀給 _Age（0＝剛炸開、1＝燒完）：
//   ・噪聲扭曲取樣位置，越老扭越多 ⇒ 雲在翻滾
//   ・依 _Age 由噪聲把雲「溶掉」：低密度的外緣先破洞，最後整團散光
//   ・火球模式（_Smoke=0）：熱度隨年齡下降 ⇒ 白熱 → 橘 → 暗紅；外緣比中心先冷成黑煙色；同時加色比例跟著降（變成會遮住地板的煙）
//   ・黑煙模式（_Smoke=1）：同一張圖當密度，一般透明混合的煙色
// 混合：premultiplied alpha（Blend One OneMinusSrcAlpha）。
// ⚠ 用 UV 0~1：圖必須單獨一張、不進圖集、Full Rect。
Shader "Custom/DipanFireball"
{
    Properties
    {
        [PerRendererData] _MainTex ("火球雲圖（alpha＝密度）", 2D) = "white" {}
        _NoiseTex ("噪聲（程式自動指定）", 2D) = "gray" {}

        _ColorCore ("核心色", Color) = (1, 0.95, 0.78, 1)
        _ColorMid ("中段色", Color) = (1, 0.52, 0.1, 1)
        _ColorEdge ("外緣色", Color) = (0.62, 0.1, 0.03, 1)
        _SmokeColor ("煙色（a＝濃度）", Color) = (0.16, 0.13, 0.12, 0.85)
        _Intensity ("亮度", Float) = 1.6
        _Additive ("加色比例（剛炸開時）", Range(0, 1)) = 0.6

        _Age ("年齡 0→1", Range(0, 1)) = 0
        _Smoke ("黑煙模式 (0/1)", Float) = 0
        _Swirl ("翻滾扭曲", Float) = 0.08
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
            sampler2D _NoiseTex;
            fixed4 _ColorCore, _ColorMid, _ColorEdge, _SmokeColor;
            float _Intensity, _Additive, _Age, _Smoke, _Swirl, _Seed, _Dim;

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
                float age = saturate(_Age);
                float t = _Time.y;

                float n1 = tex2D(_NoiseTex, uv * 1.6 + float2(_Seed, _Seed * 0.7 - t * 0.25)).r;
                float n2 = tex2D(_NoiseTex, uv * 3.1 + float2(-_Seed * 0.3, t * 0.18)).g;
                float2 suv = uv + (float2(n1, n2) - 0.5) * _Swirl * (0.6 + age);

                float dens = tex2D(_MainTex, suv).a;
                dens *= step(0.0, suv.x) * step(suv.x, 1.0) * step(0.0, suv.y) * step(suv.y, 1.0);
                float n = n1 * 0.6 + n2 * 0.4;

                // 溶解：越老吃越多（前半段慢、後半段快）；低密度（外緣）與噪聲低處先破
                float cut = pow(age, 1.6) * 1.1 - 0.05;
                float keep = saturate((dens * 0.9 + n * 0.5 - cut * 1.4) * 5.0);

                float4 outc;
                if (_Smoke > 0.5)
                {
                    float d2 = sqrt(dens);
                    float a = smoothstep(0.02, 0.3, d2) * keep * _SmokeColor.a * i.color.a;
                    float3 rgb = _SmokeColor.rgb * (0.55 + 0.45 * n) * (0.75 + 0.5 * (1.0 - d2)) * a;   // 薄處稍亮，看得出翻滾的層次
                    outc = float4(rgb * _Dim * i.color.rgb, a);
                }
                else
                {
                    float a = smoothstep(0.02, 0.2, dens) * keep * i.color.a;
                    float heat = dens * (1.0 - age * 0.85);
                    float3 c = lerp(_ColorEdge.rgb, _ColorMid.rgb, smoothstep(0.05, 0.45, heat));
                    c = lerp(c, _ColorCore.rgb, smoothstep(0.6, 0.95, heat));
                    // 外緣比中心先冷成煙
                    float smokeT = smoothstep(0.2, 0.7, age + (1.0 - dens) * 0.45);
                    float3 col = lerp(c * _Intensity, _SmokeColor.rgb, smokeT);
                    float addK = _Additive * (1.0 - smokeT);
                    outc = float4(col * a * _Dim * i.color.rgb, a * (1.0 - addK));
                }
                return outc;
            }
            ENDCG
        }
    }
    Fallback Off
}
