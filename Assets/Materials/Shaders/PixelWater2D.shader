Shader "Custom/2D/PixelWater2D"
{
    Properties
    {
        [Header(Base Sprite and Tint)]
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Water Palette)]
        _ShallowColor ("Shallow Color", Color) = (0.15, 0.65, 0.85, 0.8)
        _DeepColor ("Deep Color", Color) = (0.05, 0.2, 0.45, 0.95)
        _FoamColor ("Foam Color", Color) = (0.9, 0.98, 1.0, 1.0)
        _ColorBands ("Depth Color Bands", Range(2, 8)) = 4

        [Header(Pixel Art Grid and Animation)]
        _PPU ("Pixels Per Unit", Float) = 16.0
        _FPS ("Animation FPS", Range(1, 30)) = 10.0
        _WaveSpeed ("Wave Speed", Float) = 2.5
        _WaveFrequency ("Wave Frequency", Float) = 3.0
        _WaveAmplitude ("Wave Amplitude (Pixels)", Float) = 1.0
        _FoamThickness ("Foam Thickness (Pixels)", Range(0, 4)) = 1.5

        [Header(Reflection Settings)]
        [Toggle(_ENABLE_REFLECTION)] _EnableReflection ("Enable Screen Reflection", Float) = 1.0
        _ReflectionIntensity ("Reflection Intensity", Range(0, 1)) = 0.5
        _ReflectionDistortion ("Reflection Distortion (Pixels)", Range(0, 4)) = 1.0
        _ReflectionFadeDistance ("Reflection Fade Distance (Units)", Float) = 2.5
        _ReflectionTint ("Reflection Tint", Color) = (0.8, 0.9, 1.0, 1.0)

        [Header(World Surface Coordinate)]
        _SurfaceWorldY ("Surface World Y", Float) = 0.0

        [HideInInspector] _SrcBlend ("__src", Float) = 5.0
        [HideInInspector] _DstBlend ("__dst", Float) = 10.0
        [HideInInspector] _ZWrite ("__zw", Float) = 0.0
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
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            Name "PixelWater2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ENABLE_REFLECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D_X(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float4 _ReflectionTint;
                float _ColorBands;
                float _PPU;
                float _FPS;
                float _WaveSpeed;
                float _WaveFrequency;
                float _WaveAmplitude;
                float _FoamThickness;
                float _EnableReflection;
                float _ReflectionIntensity;
                float _ReflectionDistortion;
                float _ReflectionFadeDistance;
                float _SurfaceWorldY;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 1. Time stepping for authentic retro 8-12 FPS frame rate
                float steppedTime = floor(_Time.y * _FPS) / _FPS;

                // 2. Pixel Art Grid Snapping in World Space (16 PPU)
                float ppu = max(_PPU, 1.0);
                float pixelSize = 1.0 / ppu;
                float2 snappedWorldPos = floor(input.worldPos.xy * ppu) / ppu;

                // 3. Pixel Wave Calculation
                // Stepped horizontal sine wave
                float wavePhase = snappedWorldPos.x * _WaveFrequency + steppedTime * _WaveSpeed;
                float rawSine = sin(wavePhase);
                
                // Stepped wave displacement in discrete pixel units
                float waveDisplacement = floor(rawSine * _WaveAmplitude + 0.5) * pixelSize;
                float localSurfaceY = _SurfaceWorldY + waveDisplacement;

                // Vertical distance from the animated wave surface
                float distBelowSurface = localSurfaceY - input.worldPos.y;

                // Clip pixels above water surface
                if (distBelowSurface < 0.0)
                {
                    discard;
                }

                // 4. Sample Main Sprite Texture (if used as sliced sprite or tile)
                float4 spriteTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (spriteTex.a <= 0.01)
                {
                    discard;
                }

                // 5. Stepped Depth Gradient (16-bit indexed color style)
                float normalizedDepth = saturate(distBelowSurface / max(_ReflectionFadeDistance, 0.1));
                float steppedDepth = floor(normalizedDepth * _ColorBands) / _ColorBands;
                float4 waterColor = lerp(_ShallowColor, _DeepColor, steppedDepth);

                // 6. Real-Time Screen Reflection
                #if defined(_ENABLE_REFLECTION)
                if (_EnableReflection > 0.5)
                {
                    // Compute screen coordinates for reflection
                    float2 screenUV = input.screenPos.xy / input.screenPos.w;

                    // Calculate vertical distance in screen space between surface and current point
                    float4 surfaceCS = TransformWorldToHClip(float3(snappedWorldPos.x, localSurfaceY, input.worldPos.z));
                    float4 surfaceScreenPos = ComputeScreenPos(surfaceCS);
                    float surfaceScreenY = surfaceScreenPos.y / surfaceScreenPos.w;

                    // Vertical distance to surface in screen UV
                    float screenYDist = abs(surfaceScreenY - screenUV.y);

                    // Stepped horizontal wobble for the reflection
                    float reflectWobble = floor(sin(snappedWorldPos.x * _WaveFrequency * 1.5 - steppedTime * _WaveSpeed * 1.2) * _ReflectionDistortion + 0.5) * (1.0 / _ScreenParams.x);

                    // Mirrored UV: sample above the surface line
                    float2 reflectUV = float2(screenUV.x + reflectWobble, surfaceScreenY + screenYDist);

                    // Clamp to valid screen bounds
                    reflectUV = clamp(reflectUV, 0.001, 0.999);

                    // Sample screen texture from camera sorting layers
                    float4 sceneColor = SAMPLE_TEXTURE2D_X(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, reflectUV);

                    // Fade reflection with depth
                    float reflectFade = saturate(1.0 - (distBelowSurface / max(_ReflectionFadeDistance, 0.1)));
                    // Quantize reflection fade into pixel levels
                    reflectFade = floor(reflectFade * 4.0) / 4.0;

                    float4 tintedReflection = sceneColor * _ReflectionTint;
                    waterColor.rgb = lerp(waterColor.rgb, tintedReflection.rgb, reflectFade * _ReflectionIntensity * sceneColor.a);
                }
                #endif

                // 7. Surface Foam Line (Top 1-2 Pixels)
                float foamPixels = max(_FoamThickness, 0.0);
                float foamThreshold = foamPixels * pixelSize;

                if (distBelowSurface <= foamThreshold)
                {
                    // Foam pattern with rhythmic pixel teeth
                    float foamPattern = fmod(abs(floor(snappedWorldPos.x * ppu + steppedTime * (_FPS * 0.5))), 3.0);
                    if (foamPattern > 0.5 || distBelowSurface <= pixelSize)
                    {
                        waterColor = _FoamColor;
                    }
                }

                // 8. Final blending with sprite alpha and vertex color
                waterColor *= input.color * spriteTex;
                return waterColor;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/2D/Sprite-Unlit"
}
