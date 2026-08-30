Shader "Custom/2D/StylizedWingTrail"
{
    Properties
    {
        [PerRendererData] _MainTex ("Trail Texture (Optional)", 2D) = "white" {}
        [HDR] _Color ("Base Tint", Color) = (1, 1, 1, 1)
        [HDR] _CoreColor ("Core Glow Color", Color) = (1.5, 1.8, 2.0, 1.0)
        _EmissionMultiplier ("Emission Intensity", Range(1.0, 5.0)) = 1.6
        _OverallAlpha ("Overall Alpha Multiplier", Range(0.0, 1.0)) = 1.0
        _EdgeSoftness ("Edge Softness", Range(0.2, 5.0)) = 2.2
        _CoreWidth ("Core Width", Range(0.01, 1.0)) = 0.35
        _LengthFadePow ("Length Fade Exponent", Range(0.5, 4.0)) = 1.0

        [Header(Pixel Art Quantization)]
        [Toggle(_PIXELATE_ON)] _EnablePixelation ("Enable Pixel Art Pixelation", Float) = 1
        _PixelStepsX ("Length Pixel Steps (Bands)", Range(4, 64)) = 28
        _PixelStepsY ("Width Pixel Steps (Thickness)", Range(2, 16)) = 6
        _AlphaSteps ("Alpha Quantization Steps (0 = Smooth)", Range(0, 16)) = 6
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "StylizedWingTrailURP"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _CoreColor;
                float _EmissionMultiplier;
                float _OverallAlpha;
                float _EdgeSoftness;
                float _CoreWidth;
                float _LengthFadePow;
                float _EnablePixelation;
                float _PixelStepsX;
                float _PixelStepsY;
                float _AlphaSteps;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Pixelación procedural de UVs (Pixel-Art Quantization)
                if (_EnablePixelation > 0.5)
                {
                    uv.x = floor(uv.x * _PixelStepsX) / _PixelStepsX;
                    uv.y = floor(uv.y * _PixelStepsY) / _PixelStepsY + (0.5 / _PixelStepsY);
                }

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                // Distancia normalizada desde el eje central hacia los bordes
                float distFromCenter = abs(uv.y - 0.5) * 2.0;

                // Suavizado / caída en bordes laterales
                float edgeAlpha = saturate(1.0 - pow(distFromCenter, _EdgeSoftness));

                // Núcleo brillante central (Core Glow)
                float coreFactor = saturate(1.0 - (distFromCenter / max(0.01, _CoreWidth)));
                coreFactor = coreFactor * coreFactor;

                // Color base interpolado con el núcleo HDR
                half3 finalRgb = lerp(input.color.rgb * _Color.rgb, _CoreColor.rgb * _EmissionMultiplier, coreFactor);
                finalRgb *= texColor.rgb;

                // Alpha final con atenuación general y desvanecimiento a transparente
                half finalAlpha = input.color.a * _Color.a * texColor.a * edgeAlpha * _OverallAlpha;

                // Cuantización de niveles de transparencia pixel-art si está habilitada
                if (_EnablePixelation > 0.5 && _AlphaSteps > 0.5 && finalAlpha > 0.001)
                {
                    finalAlpha = ceil(finalAlpha * _AlphaSteps) / _AlphaSteps;
                }

                return half4(finalRgb, finalAlpha);
            }
            ENDHLSL
        }
    }

    // Fallback SubShader para compatibilidad estándar
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _CoreColor;
            float _EmissionMultiplier;
            float _OverallAlpha;
            float _EdgeSoftness;
            float _CoreWidth;
            float _LengthFadePow;
            float _EnablePixelation;
            float _PixelStepsX;
            float _PixelStepsY;
            float _AlphaSteps;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                if (_EnablePixelation > 0.5)
                {
                    uv.x = floor(uv.x * _PixelStepsX) / _PixelStepsX;
                    uv.y = floor(uv.y * _PixelStepsY) / _PixelStepsY + (0.5 / _PixelStepsY);
                }

                fixed4 texColor = tex2D(_MainTex, uv);
                float distFromCenter = abs(uv.y - 0.5) * 2.0;
                float edgeAlpha = saturate(1.0 - pow(distFromCenter, _EdgeSoftness));
                float coreFactor = saturate(1.0 - (distFromCenter / max(0.01, _CoreWidth)));
                coreFactor = coreFactor * coreFactor;

                fixed3 finalRgb = lerp(IN.color.rgb * _Color.rgb, _CoreColor.rgb * _EmissionMultiplier, coreFactor);
                finalRgb *= texColor.rgb;
                fixed finalAlpha = IN.color.a * _Color.a * texColor.a * edgeAlpha * _OverallAlpha;

                if (_EnablePixelation > 0.5 && _AlphaSteps > 0.5 && finalAlpha > 0.001)
                {
                    finalAlpha = ceil(finalAlpha * _AlphaSteps) / _AlphaSteps;
                }

                return fixed4(finalRgb, finalAlpha);
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
