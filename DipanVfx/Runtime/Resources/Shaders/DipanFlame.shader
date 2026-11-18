// 火焰 sprite shader（2026-11-18，DipanVfx；說明見 readme/DIPAN_VFX.md §3）。
//
// 一張「靜態」火焰圖就能燒起來：
//   ① 火焰圖只用 alpha 當「熱度」（白色＋alpha 的 png；GPT 畫黑底火焰、程式把亮度轉成 alpha）。
//   ② 兩層往上捲動的噪聲扭曲取樣位置（越往上扭越多）⇒ 火舌一直流動，沒有序列圖的「一幀一幀跳」。
//   ③ 同一組噪聲從熱度裡「侵蝕」掉一塊塊 ⇒ 火舌尖端斷裂、忽長忽短。
//   ④ 熱度對應三段色帶（外緣→中段→核心）⇒ 同一組圖換顏色就是綠火、藍火、血火，不必重畫。
//   ⑤ _Lean 讓火舌往一邊倒（程式依移動速度給：往右跑火往左拖）；_Ignite 從根部長出來；_Fade 從尖端往下熄滅。
//
// 混合：premultiplied alpha（Blend One OneMinusSrcAlpha）。_Additive＝1 時 alpha 輸出 0 ⇒ 純加色發光；
// 0 時是一般不透明火。中間值＝核心發光、外焰又能蓋住底下（專案是 Linear 色彩空間，純加色在亮地板上會被洗白，
// 見 readme/PROBLEMS.md E11～E13）。
//
// ⚠ 用 UV 0~1：火焰圖必須是**單獨一張貼圖、不進圖集**，Sprite Mesh Type＝Full Rect（.meta 的 spriteMeshType: 0）。
//   圖左右要留透明空間給 _Lean 倒過去，不然會被四邊切掉。
Shader "Custom/DipanFlame"
{
    Properties
    {
        [PerRendererData] _MainTex ("火焰圖（alpha＝熱度）", 2D) = "white" {}
        _NoiseTex ("噪聲（程式自動指定）", 2D) = "gray" {}

        _ColorCore ("核心色", Color) = (1, 0.95, 0.75, 1)
        _ColorMid ("中段色", Color) = (1, 0.55, 0.12, 1)
        _ColorEdge ("外緣色", Color) = (0.7, 0.12, 0.04, 1)
        _Intensity ("亮度", Float) = 1.35
        _Additive ("加色比例 (0=不透明火 1=純發光)", Range(0, 1)) = 0.55

        _ScrollSpeed ("往上捲動速度", Float) = 1.4
        _Distort ("扭曲強度", Float) = 0.1
        _Erode ("侵蝕強度（火舌斷裂）", Float) = 0.45
        _NoiseScale ("噪聲尺度", Float) = 1.6

        _BaseY ("根部 UV.y（程式依 pivot 自動給）", Range(0, 1)) = 0.1
        _Lean ("傾斜（UV；正＝往右倒）", Float) = 0
        _Ignite ("起燃 0→1", Range(0, 1)) = 1
        _Fade ("熄滅 0→1", Range(0, 1)) = 0
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
            fixed4 _ColorCore, _ColorMid, _ColorEdge;
            float _Intensity, _Additive;
            float _ScrollSpeed, _Distort, _Erode, _NoiseScale;
            float _BaseY, _Lean, _Ignite, _Fade, _Seed, _Dim;

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
                float t = _Time.y * _ScrollSpeed;
                float2 uv = i.uv;

                // 起燃：以根部為中心等比放大（_Ignite 小 ⇒ 取樣範圍被拉開 ⇒ 火變小、貼在根部）
                float2 root = float2(0.5, _BaseY);
                float g = max(_Ignite, 0.04);
                float2 uvI = root + (uv - root) / g;

                // 0＝根部、1＝圖頂
                float h = saturate((uvI.y - _BaseY) / max(1.0 - _BaseY, 0.01));

                // 傾斜：越高倒越多（二次曲線，根部不動）
                uvI.x -= _Lean * h * h;

                // 兩層往上捲動的噪聲（噪聲座標跟著畫面 UV，不跟扭曲後的座標，避免自我回授）
                float2 nuv = uv * _NoiseScale;
                float n1 = tex2D(_NoiseTex, nuv * float2(1.0, 0.7) + float2(_Seed, -t)).r;
                float n2 = tex2D(_NoiseTex, nuv * 1.9 + float2(-_Seed * 0.37, -t * 1.6)).g;

                // 扭曲：根部幾乎不動、尖端晃最大
                float2 d = (float2(n1, n2) - 0.5) * _Distort * (0.2 + h);
                float2 suv = uvI + d;

                float heat = tex2D(_MainTex, suv).a;
                float inside = step(0.0, suv.x) * step(suv.x, 1.0) * step(0.0, suv.y) * step(suv.y, 1.0);
                heat *= inside;

                // 侵蝕：越高吃越多 ⇒ 尖端斷成一團團往上飄
                float n = n1 * 0.6 + n2 * 0.4;
                heat -= (1.0 - n) * _Erode * (0.15 + h);
                // 熄滅：從尖端往下
                heat -= _Fade * (0.35 + h) * 1.2;
                heat = saturate(heat * (1.0 + _Erode));

                float a = smoothstep(0.02, 0.22, heat);
                float3 c = lerp(_ColorEdge.rgb, _ColorMid.rgb, smoothstep(0.05, 0.5, heat));
                c = lerp(c, _ColorCore.rgb, smoothstep(0.65, 0.98, heat));

                float vis = a * i.color.a;
                float3 rgb = c * i.color.rgb * vis * _Intensity * _Dim;
                float alpha = vis * (1.0 - _Additive);
                return float4(rgb, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
