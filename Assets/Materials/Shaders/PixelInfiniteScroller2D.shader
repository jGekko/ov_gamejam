Shader "Custom/2D/PixelInfiniteScroller2D"
{
    Properties
    {
        [Header(Base Sprite and Texture)]
        _MainTex ("Texture (Wrap Mode should be Repeat)", 2D) = "white" {}
        [PerRendererData] _Color ("Color Tint", Color) = (1, 1, 1, 1)
        _GlobalAlpha ("Global Alpha Override", Range(0, 1)) = 1.0

        [Header(Infinite Scrolling)]
        _ScrollSpeedX ("Scroll Speed X", Float) = 0.05
        _ScrollSpeedY ("Scroll Speed Y", Float) = 0.0
        _TilingX ("Tiling X", Float) = 1.0
        _TilingY ("Tiling Y", Float) = 1.0

        [Header(Pixel Art Quantization)]
        _PPU ("Pixels Per Unit (PPU)", Float) = 16.0
        _FPS ("Stepped Animation FPS (0 = Smooth)", Range(0, 30)) = 10.0

        [Header(Water Reflection Mode)]
        [Toggle(_ENABLE_WOBBLE)] _EnableWobble ("Enable Wave Wobble (for Reflections)", Float) = 0.0
        _WobbleSpeed ("Wobble Speed", Float) = 2.0
        _WobbleFrequency ("Wobble Frequency", Float) = 5.0
        _WobbleAmplitude ("Wobble Amplitude (UV)", Range(0, 0.1)) = 0.015

        [Header(Vertical Alpha Fade)]
        [Toggle(_ENABLE_FADE)] _EnableFade ("Enable Vertical Fade", Float) = 0.0
        _FadeStart ("Fade Start (UV Y)", Range(0, 1)) = 0.0
        _FadeEnd ("Fade End (UV Y)", Range(0, 1)) = 1.0
        _FadeBands ("Fade Stepped Bands", Range(1, 8)) = 4.0

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
            "CanUseSpriteAtlas" = "False"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            Name "PixelInfiniteScroller2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ENABLE_WOBBLE
            #pragma shader_feature_local _ENABLE_FADE

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
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _GlobalAlpha;
                float _ScrollSpeedX;
                float _ScrollSpeedY;
                float _TilingX;
                float _TilingY;
                float _PPU;
                float _FPS;
                float _EnableWobble;
                float _WobbleSpeed;
                float _WobbleFrequency;
                float _WobbleAmplitude;
                float _EnableFade;
                float _FadeStart;
                float _FadeEnd;
                float _FadeBands;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 1. Time evaluation (stepped or continuous)
                float timeVal = _Time.y;
                if (_FPS > 0.5)
                {
                    timeVal = floor(_Time.y * _FPS) / _FPS;
                }

                // 2. Base UV with custom tiling
                float2 baseUV = input.uv * float2(_TilingX, _TilingY);

                // 3. Scroll offset with pixel snapping
                float2 scrollOffset = float2(_ScrollSpeedX, _ScrollSpeedY) * timeVal;
                
                if (_PPU > 0.5 && _FPS > 0.5)
                {
                    // Snap scroll offset to pixel steps
                    scrollOffset = floor(scrollOffset * _PPU) / _PPU;
                }

                float2 finalUV = baseUV + scrollOffset;

                // 4. Optional Wave Wobble (for water reflections)
                #if defined(_ENABLE_WOBBLE)
                if (_EnableWobble > 0.5)
                {
                    float wobblePhase = input.worldPos.x * _WobbleFrequency + timeVal * _WobbleSpeed;
                    float rawWobble = sin(wobblePhase) * _WobbleAmplitude;
                    
                    if (_PPU > 0.5)
                    {
                        rawWobble = floor(rawWobble * _PPU + 0.5) / _PPU;
                    }
                    finalUV.x += rawWobble;
                }
                #endif

                // 5. Infinite Wrapping (frac ensures repeat even on trimmed UVs)
                finalUV = frac(finalUV);

                // 6. Texture Sampling
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV);

                // 7. Optional Vertical Fade (useful for fading water reflections into depth)
                #if defined(_ENABLE_FADE)
                if (_EnableFade > 0.5)
                {
                    float fadeFactor = saturate((input.uv.y - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));
                    if (_FadeBands > 1.0)
                    {
                        fadeFactor = floor(fadeFactor * _FadeBands) / _FadeBands;
                    }
                    texColor.a *= fadeFactor;
                }
                #endif

                // 8. Tint, vertex color and global alpha multiplier
                float4 result = texColor * input.color;
                result.a *= _GlobalAlpha;
                return result;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/2D/Sprite-Unlit"
}
