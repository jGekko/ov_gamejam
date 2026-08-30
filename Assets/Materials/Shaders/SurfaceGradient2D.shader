Shader "Custom/2D/SurfaceGradient2D"
{
    Properties
    {
        [Header(Base Sprite Texture)]
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Vertex Tint Multiplier", Color) = (1, 1, 1, 1)

        [Header(Surface and Base Palette)]
        _SurfaceColor ("Surface / Top Color", Color) = (0.35, 0.75, 0.25, 1.0)
        _BaseColor ("Base / Interior Color", Color) = (0.22, 0.15, 0.12, 1.0)
        _HighlightColor ("Edge Highlight Color", Color) = (0.65, 0.92, 0.35, 1.0)
        _HighlightThickness ("Highlight Thickness (0 = Off)", Range(0, 0.5)) = 0.08

        [Header(Gradient Dimensions and Falloff)]
        _GradientHeight ("Gradient Height / Span (Units)", Float) = 2.0
        _SurfaceOffset ("Surface Offset / Shift Y", Float) = 0.0
        _GradientFalloff ("Gradient Falloff (Power)", Range(0.1, 5.0)) = 1.0
        [Toggle(_REPEAT_GRADIENT)] _RepeatGradient ("Repeat Gradient per Height Unit", Float) = 0.0

        [Header(Direction and Surface Detection)]
        [KeywordEnum(Ground_TopDown, Ceiling_BottomUp, Auto_ScreenHalf, Custom_Angle, World_Y_Interval)] _DirectionMode ("Direction Mode", Float) = 0
        _ScreenSplitY ("Auto Screen Split Line (0..1)", Range(0.0, 1.0)) = 0.5
        _ScreenTransitionSoftness ("Auto Screen Transition Softness", Range(0.01, 0.5)) = 0.1
        _CustomAngle ("Custom Angle (Degrees)", Range(0, 360)) = 90.0
        _WorldMinY ("World Min Y (Interval Mode)", Float) = -5.0
        _WorldMaxY ("World Max Y (Interval Mode)", Float) = 5.0

        [Header(Coordinate Space)]
        [Toggle(_USE_LOCAL_SPACE)] _UseLocalSpace ("Use Local Space (Per Sprite)", Float) = 0.0

        [Header(Pixel Art Quantization and Banding)]
        [Toggle(_PIXELATE_GRADIENT)] _PixelateGradient ("Enable Gradient Pixelation", Float) = 1.0
        _GradientSteps ("Color Band Steps", Range(2, 16)) = 4.0
        _PPU ("Pixels Per Unit (PPU)", Float) = 16.0
        [Toggle(_SNAP_TO_PPU)] _SnapToPPU ("Snap Position to PPU Grid", Float) = 1.0

        [Header(Texture Blending Mode)]
        [KeywordEnum(Solid_Mask, Multiply_Detail, Overlay_Blend, Tint_Ambient)] _TextureBlendMode ("Texture Blend Mode", Float) = 0
        _TextureDetailStrength ("Detail / Shading Strength", Range(0, 1)) = 1.0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.01

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

        // Pass 1: URP 2D Renderer Pass
        Pass
        {
            Name "SurfaceGradient2D_Universal2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _DIRECTIONMODE_GROUND_TOPDOWN _DIRECTIONMODE_CEILING_BOTTOMUP _DIRECTIONMODE_AUTO_SCREENHALF _DIRECTIONMODE_CUSTOM_ANGLE _DIRECTIONMODE_WORLD_Y_INTERVAL
            #pragma shader_feature_local _TEXTUREBLENDMODE_SOLID_MASK _TEXTUREBLENDMODE_MULTIPLY_DETAIL _TEXTUREBLENDMODE_OVERLAY_BLEND _TEXTUREBLENDMODE_TINT_AMBIENT
            #pragma shader_feature_local _PIXELATE_GRADIENT
            #pragma shader_feature_local _SNAP_TO_PPU
            #pragma shader_feature_local _USE_LOCAL_SPACE
            #pragma shader_feature_local _REPEAT_GRADIENT

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
                float3 localPos : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _SurfaceColor;
                float4 _BaseColor;
                float4 _HighlightColor;
                float _HighlightThickness;
                float _GradientHeight;
                float _SurfaceOffset;
                float _GradientFalloff;
                float _RepeatGradient;
                float _DirectionMode;
                float _ScreenSplitY;
                float _ScreenTransitionSoftness;
                float _CustomAngle;
                float _WorldMinY;
                float _WorldMaxY;
                float _UseLocalSpace;
                float _PixelateGradient;
                float _GradientSteps;
                float _PPU;
                float _SnapToPPU;
                float _TextureBlendMode;
                float _TextureDetailStrength;
                float _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.localPos = input.positionOS.xyz;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 1. Determine base coordinate (World space by default for seamless connection across tiles)
                float2 coord = input.worldPos.xy;
                #if defined(_USE_LOCAL_SPACE)
                if (_UseLocalSpace > 0.5)
                {
                    coord = input.localPos.xy;
                }
                #endif

                // 2. Pixel Grid Snapping
                #if defined(_SNAP_TO_PPU)
                if (_SnapToPPU > 0.5 && _PPU > 0.5)
                {
                    coord = floor(coord * _PPU) / _PPU;
                }
                #endif

                // 3. Compute Normalized Gradient Factor 't' (1.0 = Surface Color, 0.0 = Base Color)
                float t = 0.0;
                float gradHeight = max(_GradientHeight, 0.001);

                #if defined(_DIRECTIONMODE_CEILING_BOTTOMUP)
                    // Ceiling / Hanging structures: surface is at the bottom, fades upwards into base color
                    float yCoord = coord.y - _SurfaceOffset;
                    #if defined(_REPEAT_GRADIENT)
                    if (_RepeatGradient > 0.5)
                        t = 1.0 - frac(yCoord / gradHeight);
                    else
                        t = saturate(-yCoord / gradHeight);
                    #else
                        t = saturate(-yCoord / gradHeight);
                    #endif

                #elif defined(_DIRECTIONMODE_AUTO_SCREENHALF)
                    // Auto detection based on screen position
                    float screenY = input.screenPos.y / max(input.screenPos.w, 0.0001);
                    float yCoord = coord.y - _SurfaceOffset;
                    
                    // Lower screen: Ground (Top-to-Bottom gradient: higher Y is surface)
                    float tGround;
                    #if defined(_REPEAT_GRADIENT)
                    if (_RepeatGradient > 0.5)
                        tGround = frac(yCoord / gradHeight);
                    else
                        tGround = saturate(yCoord / gradHeight);
                    #else
                        tGround = saturate(yCoord / gradHeight);
                    #endif

                    // Upper screen: Ceiling (Bottom-to-Top gradient: lower Y is surface)
                    float tCeiling;
                    #if defined(_REPEAT_GRADIENT)
                    if (_RepeatGradient > 0.5)
                        tCeiling = 1.0 - frac(yCoord / gradHeight);
                    else
                        tCeiling = saturate(-yCoord / gradHeight);
                    #else
                        tCeiling = saturate(-yCoord / gradHeight);
                    #endif

                    float isUpper = smoothstep(_ScreenSplitY - _ScreenTransitionSoftness, _ScreenSplitY + _ScreenTransitionSoftness, screenY);
                    t = lerp(tGround, tCeiling, isUpper);

                #elif defined(_DIRECTIONMODE_CUSTOM_ANGLE)
                    // Custom Angle for slopes, ramps, walls, or pillars
                    float rad = radians(_CustomAngle);
                    float2 dir = float2(cos(rad), sin(rad));
                    float proj = dot(coord, dir) - _SurfaceOffset;
                    #if defined(_REPEAT_GRADIENT)
                    if (_RepeatGradient > 0.5)
                        t = frac(proj / gradHeight);
                    else
                        t = saturate(proj / gradHeight);
                    #else
                        t = saturate(proj / gradHeight);
                    #endif

                #elif defined(_DIRECTIONMODE_WORLD_Y_INTERVAL)
                    // Explicit interval between World Min Y and Max Y
                    float span = max(_WorldMaxY - _WorldMinY, 0.001);
                    t = saturate((coord.y - _WorldMinY) / span);

                #else
                    // Default: _DIRECTIONMODE_GROUND_TOPDOWN
                    // Ground / Floor / Platforms: surface is at top, fades downwards into base color
                    float yCoord = coord.y - _SurfaceOffset;
                    #if defined(_REPEAT_GRADIENT)
                    if (_RepeatGradient > 0.5)
                        t = frac(yCoord / gradHeight);
                    else
                        t = saturate(yCoord / gradHeight);
                    #else
                        t = saturate(yCoord / gradHeight);
                    #endif
                #endif

                // 4. Apply Gradient Falloff Power
                t = saturate(t);
                t = pow(t, max(_GradientFalloff, 0.001));

                // 5. Pixelation / Posterization (Discrete Color Bands)
                #if defined(_PIXELATE_GRADIENT)
                if (_PixelateGradient > 0.5 && _GradientSteps > 1.5)
                {
                    float steps = floor(_GradientSteps);
                    t = floor(t * steps) / max(steps - 1.0, 1.0);
                    t = saturate(t);
                }
                #endif

                // 6. Base to Surface Color Interpolation
                float4 gradientColor = lerp(_BaseColor, _SurfaceColor, t);

                // Optional Edge Highlight
                if (_HighlightThickness > 0.001 && t >= (1.0 - _HighlightThickness))
                {
                    float hFactor = (t - (1.0 - _HighlightThickness)) / _HighlightThickness;
                    gradientColor = lerp(gradientColor, _HighlightColor, saturate(hFactor));
                }

                // 7. Sample Sprite Texture
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // Alpha Cutoff / Clip
                float alpha = texColor.a * gradientColor.a * input.color.a;
                clip(alpha - _Cutoff);

                // 8. Blend Gradient with Sprite Texture
                float3 finalRGB = gradientColor.rgb;

                #if defined(_TEXTUREBLENDMODE_MULTIPLY_DETAIL)
                    // Detail Mode: sprite's luminance / details modulate the gradient (rocks, flowers, mushrooms, grass)
                    float luminance = dot(texColor.rgb, float3(0.299, 0.587, 0.114));
                    float3 detailed = gradientColor.rgb * (luminance * 2.0);
                    finalRGB = lerp(gradientColor.rgb, detailed, _TextureDetailStrength);

                #elif defined(_TEXTUREBLENDMODE_OVERLAY_BLEND)
                    // Overlay Mode: blends sprite artwork with gradient
                    float3 overlayResult = (gradientColor.rgb < 0.5) ?
                        (2.0 * gradientColor.rgb * texColor.rgb) :
                        (1.0 - 2.0 * (1.0 - gradientColor.rgb) * (1.0 - texColor.rgb));
                    finalRGB = lerp(gradientColor.rgb, overlayResult, _TextureDetailStrength);

                #elif defined(_TEXTUREBLENDMODE_TINT_AMBIENT)
                    // Tint Mode: original sprite colored by surface gradient
                    finalRGB = texColor.rgb * gradientColor.rgb;

                #else
                    // Default: _TEXTUREBLENDMODE_SOLID_MASK (Flat square tiles / set piece shapes)
                    finalRGB = gradientColor.rgb;
                #endif

                // 9. Multiply with vertex color (for SpriteRenderer / Tilemap per-tile tinting)
                finalRGB *= input.color.rgb;

                return float4(finalRGB, alpha);
            }
            ENDHLSL
        }

        // Pass 2: UniversalForward / Unlit Fallback Pass
        Pass
        {
            Name "SurfaceGradient2D_UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _DIRECTIONMODE_GROUND_TOPDOWN _DIRECTIONMODE_CEILING_BOTTOMUP _DIRECTIONMODE_AUTO_SCREENHALF _DIRECTIONMODE_CUSTOM_ANGLE _DIRECTIONMODE_WORLD_Y_INTERVAL
            #pragma shader_feature_local _TEXTUREBLENDMODE_SOLID_MASK _TEXTUREBLENDMODE_MULTIPLY_DETAIL _TEXTUREBLENDMODE_OVERLAY_BLEND _TEXTUREBLENDMODE_TINT_AMBIENT
            #pragma shader_feature_local _PIXELATE_GRADIENT
            #pragma shader_feature_local _SNAP_TO_PPU
            #pragma shader_feature_local _USE_LOCAL_SPACE
            #pragma shader_feature_local _REPEAT_GRADIENT

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
                float3 localPos : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _SurfaceColor;
                float4 _BaseColor;
                float4 _HighlightColor;
                float _HighlightThickness;
                float _GradientHeight;
                float _SurfaceOffset;
                float _GradientFalloff;
                float _RepeatGradient;
                float _DirectionMode;
                float _ScreenSplitY;
                float _ScreenTransitionSoftness;
                float _CustomAngle;
                float _WorldMinY;
                float _WorldMaxY;
                float _UseLocalSpace;
                float _PixelateGradient;
                float _GradientSteps;
                float _PPU;
                float _SnapToPPU;
                float _TextureBlendMode;
                float _TextureDetailStrength;
                float _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.localPos = input.positionOS.xyz;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 coord = input.worldPos.xy;
                #if defined(_USE_LOCAL_SPACE)
                if (_UseLocalSpace > 0.5)
                {
                    coord = input.localPos.xy;
                }
                #endif

                #if defined(_SNAP_TO_PPU)
                if (_SnapToPPU > 0.5 && _PPU > 0.5)
                {
                    coord = floor(coord * _PPU) / _PPU;
                }
                #endif

                float t = 0.0;
                float gradHeight = max(_GradientHeight, 0.001);

                #if defined(_DIRECTIONMODE_CEILING_BOTTOMUP)
                    float yCoord = coord.y - _SurfaceOffset;
                    #if defined(_REPEAT_GRADIENT)
                    if (_RepeatGradient > 0.5)
                        t = 1.0 - frac(yCoord / gradHeight);
                    else
                        t = saturate(-yCoord / gradHeight);
                    #else
                        t = saturate(-yCoord / gradHeight);
                    #endif

                #elif defined(_DIRECTIONMODE_AUTO_SCREENHALF)
                    float screenY = input.screenPos.y / max(input.screenPos.w, 0.0001);
                    float yCoord = coord.y - _SurfaceOffset;
                    
                    float tGround;
                    #if defined(_REPEAT_GRADIENT)
                    if (_RepeatGradient > 0.5)
                        tGround = frac(yCoord / gradHeight);
                    else
                        tGround = saturate(yCoord / gradHeight);
                    #else
                        tGround = saturate(yCoord / gradHeight);
                    #endif

                    float tCeiling;
                    #if defined(_REPEAT_GRADIENT)
                    if (_RepeatGradient > 0.5)
                        tCeiling = 1.0 - frac(yCoord / gradHeight);
                    else
                        tCeiling = saturate(-yCoord / gradHeight);
                    #else
                        tCeiling = saturate(-yCoord / gradHeight);
                    #endif

                    float isUpper = smoothstep(_ScreenSplitY - _ScreenTransitionSoftness, _ScreenSplitY + _ScreenTransitionSoftness, screenY);
                    t = lerp(tGround, tCeiling, isUpper);

                #elif defined(_DIRECTIONMODE_CUSTOM_ANGLE)
                    float rad = radians(_CustomAngle);
                    float2 dir = float2(cos(rad), sin(rad));
                    float proj = dot(coord, dir) - _SurfaceOffset;
                    #if defined(_REPEAT_GRADIENT)
                    if (_RepeatGradient > 0.5)
                        t = frac(proj / gradHeight);
                    else
                        t = saturate(proj / gradHeight);
                    #else
                        t = saturate(proj / gradHeight);
                    #endif

                #elif defined(_DIRECTIONMODE_WORLD_Y_INTERVAL)
                    float span = max(_WorldMaxY - _WorldMinY, 0.001);
                    t = saturate((coord.y - _WorldMinY) / span);

                #else
                    float yCoord = coord.y - _SurfaceOffset;
                    #if defined(_REPEAT_GRADIENT)
                    if (_RepeatGradient > 0.5)
                        t = frac(yCoord / gradHeight);
                    else
                        t = saturate(yCoord / gradHeight);
                    #else
                        t = saturate(yCoord / gradHeight);
                    #endif
                #endif

                t = saturate(t);
                t = pow(t, max(_GradientFalloff, 0.001));

                #if defined(_PIXELATE_GRADIENT)
                if (_PixelateGradient > 0.5 && _GradientSteps > 1.5)
                {
                    float steps = floor(_GradientSteps);
                    t = floor(t * steps) / max(steps - 1.0, 1.0);
                    t = saturate(t);
                }
                #endif

                float4 gradientColor = lerp(_BaseColor, _SurfaceColor, t);

                if (_HighlightThickness > 0.001 && t >= (1.0 - _HighlightThickness))
                {
                    float hFactor = (t - (1.0 - _HighlightThickness)) / _HighlightThickness;
                    gradientColor = lerp(gradientColor, _HighlightColor, saturate(hFactor));
                }

                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float alpha = texColor.a * gradientColor.a * input.color.a;
                clip(alpha - _Cutoff);

                float3 finalRGB = gradientColor.rgb;

                #if defined(_TEXTUREBLENDMODE_MULTIPLY_DETAIL)
                    float luminance = dot(texColor.rgb, float3(0.299, 0.587, 0.114));
                    float3 detailed = gradientColor.rgb * (luminance * 2.0);
                    finalRGB = lerp(gradientColor.rgb, detailed, _TextureDetailStrength);

                #elif defined(_TEXTUREBLENDMODE_OVERLAY_BLEND)
                    float3 overlayResult = (gradientColor.rgb < 0.5) ?
                        (2.0 * gradientColor.rgb * texColor.rgb) :
                        (1.0 - 2.0 * (1.0 - gradientColor.rgb) * (1.0 - texColor.rgb));
                    finalRGB = lerp(gradientColor.rgb, overlayResult, _TextureDetailStrength);

                #elif defined(_TEXTUREBLENDMODE_TINT_AMBIENT)
                    finalRGB = texColor.rgb * gradientColor.rgb;

                #else
                    finalRGB = gradientColor.rgb;
                #endif

                finalRGB *= input.color.rgb;

                return float4(finalRGB, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/2D/Sprite-Unlit"
}
