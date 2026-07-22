// 馬賽克清晰（像素格由粗到細慢慢收斂）一次性全螢幕後處理 —— Built-in 算繪管線。
// 由 MosaicController 掛在主相機上做一次 Blit。時間軸（_Progress 0→1）：
//   0 = 最粗的馬賽克格（畫面被切成大方塊、看不清），隨 _Progress 上升格子數變多→越來越細，
//   1 = 收回原始清晰畫面。格子在「螢幕上」保持正方（用 _Aspect 修正）。
Shader "Hidden/Mosaic"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Progress ("Progress", Range(0,1)) = 0     // 收斂進度（0 最粗、1 清晰）
        _Aspect ("Aspect", Float) = 1.7778         // 螢幕寬高比（讓格子是正方）
        _MinCells ("MinCells", Float) = 14.0        // 最粗時「垂直方向」的格子數（越小越粗）
        _MaxCells ("MaxCells", Float) = 300.0       // 最細時的格子數（夠大≒原生解析度）
        _Bright ("Bright", Range(0,1)) = 1.0        // 亮度斜坡（0=全黑、1=正常）；由控制器隨進度推入
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Progress, _Aspect, _MinCells, _MaxCells, _Bright;

            fixed4 frag(v2f_img i) : SV_Target
            {
                // 用「垂直格子數」cellsY 為基準，橫向乘上 aspect → 螢幕上是正方格。
                // 由粗到細：cellsY 從 _MinCells 指數上升到 _MaxCells（前段變化明顯、後段快速收斂）。
                float t = saturate(_Progress);
                float cellsY = lerp(_MinCells, _MaxCells, t * t);   // t^2：一開始格子明顯，接近尾端快速變細
                float cellsX = max(1.0, cellsY * _Aspect);
                cellsY = max(1.0, cellsY);

                float2 grid = float2(cellsX, cellsY);
                float2 cell = floor(i.uv * grid) + 0.5;
                float2 quv = cell / grid;                            // 取每個格子中心的顏色 → 馬賽克
                fixed4 mosaic = tex2D(_MainTex, quv);

                // 收尾：最後一小段直接融回原圖，確保 100% 清晰（避免殘留半格）。
                fixed4 orig = tex2D(_MainTex, i.uv);
                float clear = smoothstep(0.88, 1.0, t);
                fixed4 outc = lerp(mosaic, orig, clear);
                outc.rgb *= _Bright;   // 亮度斜坡：一開始暗（銜接黑幕）→ 收斂時回到正常亮度
                return outc;
            }
            ENDCG
        }
    }
    Fallback Off
}
