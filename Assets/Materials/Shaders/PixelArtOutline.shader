Shader "Custom/UI/PixelArtOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Outline Settings)]
        [Toggle(_ENABLE_OUTLINE)] _OutlineEnabled ("Enable Outline", Float) = 0
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width (Pixels)", Range(0, 5)) = 1
        _AlphaThreshold ("Alpha Cutoff", Range(0.01, 0.99)) = 0.1

        // UI Mask / Stencil Support
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma shader_feature_local _ENABLE_OUTLINE
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _OutlineEnabled;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _AlphaThreshold;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 mainColor = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                mainColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(mainColor.a - 0.001);
                #endif

                // Si la outline está deshabilitada, retornar color directo
                if (_OutlineEnabled < 0.5 || _OutlineWidth <= 0.0)
                {
                    return mainColor;
                }

                // Pixel-art outline: Muestreo ortogonal de 4 vecinos (y 4 diagonales para esquinas limpias)
                if (mainColor.a <= _AlphaThreshold)
                {
                    float2 offset = _MainTex_TexelSize.xy * _OutlineWidth;

                    float maxNeighborAlpha = 0.0;
                    // 4 vecinos ortogonales
                    maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, IN.texcoord + float2(offset.x, 0.0)).a);
                    maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, IN.texcoord - float2(offset.x, 0.0)).a);
                    maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, IN.texcoord + float2(0.0, offset.y)).a);
                    maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, IN.texcoord - float2(0.0, offset.y)).a);

                    // 4 vecinos diagonales para contorno completo
                    maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, IN.texcoord + float2(offset.x, offset.y)).a);
                    maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, IN.texcoord + float2(-offset.x, offset.y)).a);
                    maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, IN.texcoord + float2(offset.x, -offset.y)).a);
                    maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, IN.texcoord + float2(-offset.x, -offset.y)).a);

                    if (maxNeighborAlpha > _AlphaThreshold)
                    {
                        fixed4 outline = _OutlineColor;
                        outline.a *= IN.color.a;

                        #ifdef UNITY_UI_CLIP_RECT
                        outline.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                        #endif

                        return outline;
                    }
                }

                return mainColor;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}
