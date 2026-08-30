Shader "Custom/2D/TilemapHazardOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Main Tint", Color) = (1, 1, 1, 1)

        [Header(Outline Mode)]
        [Enum(InnerContour_AntiBleed, 0, OuterExpanded, 1)] _OutlineMode ("Outline Mode (Inner = 100% No Ghost Pixels)", Float) = 0

        [Header(Outline Appearance)]
        [HDR] _OutlineColor ("Outline Color", Color) = (1, 0.2, 0.15, 1)
        [Range(0.5, 3.0)] _OutlineThickness ("Outline Thickness (Pixels)", Float) = 1.0
        [Range(0.0, 1.0)] _OutlineOpacity ("Outline Opacity", Float) = 1.0
        [Range(0.05, 0.95)] _AlphaThreshold ("Sprite Alpha Cutoff", Float) = 0.2
        [Range(0.1, 0.95)] _SolidAlphaThreshold ("Outer Mode Neighbor Cutoff", Float) = 0.5
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
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "TilemapOutlineURP"
            Tags { "LightMode" = "Universal2D" }

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

            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _OutlineColor;
                float _OutlineMode;
                float _OutlineThickness;
                float _OutlineOpacity;
                float _AlphaThreshold;
                float _SolidAlphaThreshold;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 mainColor = _MainTex.Sample(sampler_MainTex, input.uv) * input.color;

                // 1. Si el píxel es transparente / aire:
                if (mainColor.a < _AlphaThreshold)
                {
                    // En modo InnerContour (0), el aire SIEMPRE es transparente: 0% posibilidad de sangrado o píxeles fantasma
                    if (_OutlineMode < 0.5)
                    {
                        return float4(0, 0, 0, 0);
                    }

                    // Modo OuterExpanded (1)
                    float2 texel = _MainTex_TexelSize.xy * _OutlineThickness;
                    float maxAlpha = 0.0;

                    maxAlpha = max(maxAlpha, _MainTex.Sample(sampler_MainTex, input.uv + float2(texel.x, 0.0)).a);
                    maxAlpha = max(maxAlpha, _MainTex.Sample(sampler_MainTex, input.uv - float2(texel.x, 0.0)).a);
                    maxAlpha = max(maxAlpha, _MainTex.Sample(sampler_MainTex, input.uv + float2(0.0, texel.y)).a);
                    maxAlpha = max(maxAlpha, _MainTex.Sample(sampler_MainTex, input.uv - float2(0.0, texel.y)).a);

                    maxAlpha = max(maxAlpha, _MainTex.Sample(sampler_MainTex, input.uv + float2(texel.x, texel.y)).a);
                    maxAlpha = max(maxAlpha, _MainTex.Sample(sampler_MainTex, input.uv + float2(-texel.x, texel.y)).a);
                    maxAlpha = max(maxAlpha, _MainTex.Sample(sampler_MainTex, input.uv + float2(texel.x, -texel.y)).a);
                    maxAlpha = max(maxAlpha, _MainTex.Sample(sampler_MainTex, input.uv + float2(-texel.x, -texel.y)).a);

                    if (maxAlpha >= _SolidAlphaThreshold)
                    {
                        float4 outCol = _OutlineColor;
                        outCol.a *= _OutlineOpacity * input.color.a;
                        return outCol;
                    }

                    return float4(0, 0, 0, 0);
                }

                // 2. Si el píxel es sólido y estamos en modo InnerContour:
                if (_OutlineMode < 0.5)
                {
                    float2 texel = _MainTex_TexelSize.xy * _OutlineThickness;
                    float minAlpha = 1.0;

                    minAlpha = min(minAlpha, _MainTex.Sample(sampler_MainTex, input.uv + float2(texel.x, 0.0)).a);
                    minAlpha = min(minAlpha, _MainTex.Sample(sampler_MainTex, input.uv - float2(texel.x, 0.0)).a);
                    minAlpha = min(minAlpha, _MainTex.Sample(sampler_MainTex, input.uv + float2(0.0, texel.y)).a);
                    minAlpha = min(minAlpha, _MainTex.Sample(sampler_MainTex, input.uv - float2(0.0, texel.y)).a);

                    // Si algún vecino cardinal es transparente, este píxel está en el perímetro exterior del sprite
                    if (minAlpha < _AlphaThreshold)
                    {
                        float4 outlineCol = _OutlineColor;
                        outlineCol.rgb = lerp(mainColor.rgb, outlineCol.rgb, _OutlineOpacity);
                        outlineCol.a = mainColor.a;
                        return outlineCol;
                    }
                }

                return mainColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "TilemapOutlineFallback"

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

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineMode;
            float _OutlineThickness;
            float _OutlineOpacity;
            float _AlphaThreshold;
            float _SolidAlphaThreshold;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 mainColor = tex2D(_MainTex, i.texcoord) * i.color;

                if (mainColor.a < _AlphaThreshold)
                {
                    if (_OutlineMode < 0.5)
                    {
                        return fixed4(0, 0, 0, 0);
                    }

                    float2 texel = _MainTex_TexelSize.xy * _OutlineThickness;
                    float maxAlpha = 0.0;

                    maxAlpha = max(maxAlpha, tex2D(_MainTex, i.texcoord + float2(texel.x, 0.0)).a);
                    maxAlpha = max(maxAlpha, tex2D(_MainTex, i.texcoord - float2(texel.x, 0.0)).a);
                    maxAlpha = max(maxAlpha, tex2D(_MainTex, i.texcoord + float2(0.0, texel.y)).a);
                    maxAlpha = max(maxAlpha, tex2D(_MainTex, i.texcoord - float2(0.0, texel.y)).a);

                    maxAlpha = max(maxAlpha, tex2D(_MainTex, i.texcoord + float2(texel.x, texel.y)).a);
                    maxAlpha = max(maxAlpha, tex2D(_MainTex, i.texcoord + float2(-texel.x, texel.y)).a);
                    maxAlpha = max(maxAlpha, tex2D(_MainTex, i.texcoord + float2(texel.x, -texel.y)).a);
                    maxAlpha = max(maxAlpha, tex2D(_MainTex, i.texcoord + float2(-texel.x, -texel.y)).a);

                    if (maxAlpha >= _SolidAlphaThreshold)
                    {
                        fixed4 outCol = _OutlineColor;
                        outCol.a *= _OutlineOpacity * i.color.a;
                        return outCol;
                    }

                    return fixed4(0, 0, 0, 0);
                }

                if (_OutlineMode < 0.5)
                {
                    float2 texel = _MainTex_TexelSize.xy * _OutlineThickness;
                    float minAlpha = 1.0;

                    minAlpha = min(minAlpha, tex2D(_MainTex, i.texcoord + float2(texel.x, 0.0)).a);
                    minAlpha = min(minAlpha, tex2D(_MainTex, i.texcoord - float2(texel.x, 0.0)).a);
                    minAlpha = min(minAlpha, tex2D(_MainTex, i.texcoord + float2(0.0, texel.y)).a);
                    minAlpha = min(minAlpha, tex2D(_MainTex, i.texcoord - float2(0.0, texel.y)).a);

                    if (minAlpha < _AlphaThreshold)
                    {
                        fixed4 outlineCol = _OutlineColor;
                        outlineCol.rgb = lerp(mainColor.rgb, outlineCol.rgb, _OutlineOpacity);
                        outlineCol.a = mainColor.a;
                        return outlineCol;
                    }
                }

                return mainColor;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
