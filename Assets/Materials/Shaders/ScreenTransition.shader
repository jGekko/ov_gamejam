Shader "Custom/ScreenTransition"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Transition Color", Color) = (0, 0, 0, 1)
        _Progress ("Progress", Range(0, 1)) = 0
        [Enum(DiamondWave,0,CircleIris,1)] _Mode ("Transition Mode", Float) = 0
        _PixelSize ("Diamond / Pixel Size (Grid)", Float) = 24.0
        _Center ("Focus Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _AspectRatio ("Aspect Ratio (Width/Height)", Float) = 1.777778
    }

    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent+1000" 
            "IgnoreProjector" = "True" 
            "RenderType" = "Transparent" 
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

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
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            fixed4 _Color;
            float _Progress;
            float _Mode;
            float _PixelSize;
            float4 _Center;
            float _AspectRatio;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                if (_Progress <= 0.0001)
                {
                    discard;
                    return fixed4(0, 0, 0, 0);
                }
                if (_Progress >= 0.9999)
                {
                    return _Color;
                }

                float2 uv = IN.texcoord;
                float2 focus = _Center.xy;

                float aspect = _AspectRatio > 0.01 ? _AspectRatio : (_ScreenParams.y > 0.0 ? _ScreenParams.x / _ScreenParams.y : 1.777778);
                float alpha = 0.0;
                int mode = (int)(_Mode + 0.5);

                // -------------------------------------------------------------
                // MODO 0: DIAMOND GRID WAVE (Onda de rombos desde el origen)
                // -------------------------------------------------------------
                if (mode == 0)
                {
                    float diamondSize = max(6.0, _PixelSize);
                    float cellsX = max(4.0, _ScreenParams.x / diamondSize);
                    float cellsY = max(4.0, _ScreenParams.y / diamondSize);

                    float2 gridUV = uv * float2(cellsX, cellsY);
                    float2 cellIndex = floor(gridUV);
                    float2 cellFract = frac(gridUV);

                    // Distancia Manhattan dentro de cada celda romboidal (0 en el centro, 1 en los vértices)
                    float diamondDist = abs(cellFract.x - 0.5) + abs(cellFract.y - 0.5);

                    // Distancia radial escalada desde el punto de foco/muerte
                    float2 cellCenterNorm = (cellIndex + 0.5) / float2(cellsX, cellsY);
                    float2 aspectDist = float2((cellCenterNorm.x - focus.x) * aspect, cellCenterNorm.y - focus.y);
                    float distFromFocus = length(aspectDist);

                    // Onda expansiva de crecimiento de rombos
                    float waveDelay = distFromFocus * 0.55;
                    float cellProgress = clamp((_Progress * 1.85) - waveDelay, 0.0, 1.0);

                    // Los rombos crecen hasta cubrir completamente la celda
                    float threshold = cellProgress * 1.05;
                    alpha = diamondDist <= threshold ? 1.0 : 0.0;
                }
                // -------------------------------------------------------------
                // MODO 1: CIRCLE SPOTLIGHT IRIS (Foco circular pixelado hacia/desde el jugador)
                // -------------------------------------------------------------
                else
                {
                    float pixelBlock = max(2.0, _PixelSize * 0.5);
                    float cellsX = max(16.0, _ScreenParams.x / pixelBlock);
                    float cellsY = max(16.0, _ScreenParams.y / pixelBlock);
                    float2 pixelatedUV = floor(uv * float2(cellsX, cellsY)) / float2(cellsX, cellsY);

                    float2 aspectUV = float2((pixelatedUV.x - focus.x) * aspect, pixelatedUV.y - focus.y);
                    float dist = length(aspectUV);

                    // Radio del círculo decrece de maxRadius (abierto) a 0 (cerrado)
                    float maxRadius = 2.6;
                    float currentRadius = maxRadius * (1.0 - _Progress);

                    alpha = dist >= currentRadius ? 1.0 : 0.0;
                }

                if (alpha <= 0.01)
                {
                    discard;
                }

                return fixed4(_Color.rgb, alpha * _Color.a);
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}
