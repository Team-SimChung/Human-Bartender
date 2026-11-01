Shader "Custom/URP2DLitRimLight"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)

        [Header(Rim Color Gradient)]
        _RimColor ("Rim Color (Base)", Color) = (0, 1, 1, 1)
        _RimColorTop ("Rim Color (Top)", Color) = (1, 1, 0, 1)
        _GradientHeight ("Gradient Height (0-1)", Range(0, 1)) = 0.8

        [Header(Rim Width)]
        _RimWidth ("Rim Width (Pixels)", Float) = 2

        [Header(Rim Behavior)]
        [Toggle(_RIM_ALWAYS_BRIGHT)] _RimAlwaysBright ("Rim Always Bright (ignore darkness)", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
            "PreviewType"    = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        // ===== Pass 1: 2D 라이팅 메인 패스 =====
        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex   CombinedShapeLightVertex
            #pragma fragment CombinedShapeLightFragment

            #pragma multi_compile_fragment USE_SHAPE_LIGHT_TYPE_0
            #pragma multi_compile_fragment USE_SHAPE_LIGHT_TYPE_1
            #pragma multi_compile_fragment USE_SHAPE_LIGHT_TYPE_2
            #pragma multi_compile_fragment USE_SHAPE_LIGHT_TYPE_3
            #pragma multi_compile_local_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_local_fragment _ _RIM_ALWAYS_BRIGHT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Unity 6 URP: SurfaceData2D / InputData2D 구조체 정의 헤더
            // 이게 CombinedShapeLightShared.hlsl 보다 먼저 include되어야 함
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/SurfaceData2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/InputData2D.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _RimColor;
                float4 _RimColorTop;
                float  _GradientHeight;
                float  _RimWidth;
                float4 _Color;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                half4  color        : COLOR;
                float2 uv           : TEXCOORD0;
                half2  lightingUV   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings CombinedShapeLightVertex(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv         = TRANSFORM_TEX(v.uv, _MainTex);
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                o.color      = v.color;
                return o;
            }

            half4 CombinedShapeLightFragment(Varyings i) : SV_Target
            {
               half4 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
    half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv);

    // ===== 4방향 알파 샘플링 =====
    float2 offX = float2(_RimWidth * _MainTex_TexelSize.x, 0);
    float2 offY = float2(0, _RimWidth * _MainTex_TexelSize.y);

    half a    = main.a;
    half aR   = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + offX).a;
    half aL   = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - offX).a;
    half aU   = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + offY).a;
    half aD   = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - offY).a;

    half neighborMin = min(min(aR, aL), min(aU, aD));
    half neighborMax = max(max(aR, aL), max(aU, aD));

    // ===== 안쪽 림 + 바깥쪽 림 =====
    half innerRim = saturate(a - neighborMin);          // 빌딩 안쪽 가장자리
    half outerRim = saturate(neighborMax - a);          // 빌딩 바깥쪽 가장자리
    half rimMask  = max(outerRim, outerRim);

    // ===== 림 색상 그라데이션 =====
    half3 rimGradient = lerp(_RimColor.rgb, _RimColorTop.rgb,
                             saturate(i.uv.y / _GradientHeight));
    half3 rimRGB = rimGradient * rimMask;

    // ===== 2D 라이팅 적용 =====
    SurfaceData2D surfaceData;
    InitializeSurfaceData(main.rgb, main.a, mask, surfaceData);

    InputData2D inputData;
    InitializeInputData(i.uv, i.lightingUV, inputData);

    half4 lit = CombinedShapeLightShared(surfaceData, inputData);

    // ===== 림 합성 =====
    half3 finalRGB;
    #if defined(_RIM_ALWAYS_BRIGHT)
        finalRGB = lit.rgb + rimRGB;
    #else
        finalRGB = lit.rgb + rimRGB * (lit.rgb / max(main.rgb, 0.0001));
    #endif

    // ===== 알파 출력 =====
    // 바깥쪽 림은 알파 0인 픽셀에 그려지므로, 알파를 림 강도만큼 끌어올림
    half outAlpha = max(main.a, outerRim) * i.color.a;

    return half4(finalRGB, outAlpha);
            }
            ENDHLSL
        }

        // ===== Pass 2: 노멀맵 패스 =====
        Pass
        {
            Tags { "LightMode" = "NormalsRendering" }

            HLSLPROGRAM
            #pragma vertex   NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _RimColor;
                float4 _RimColorTop;
                float  _GradientHeight;
                float  _RimWidth;
                float4 _Color;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/NormalsRenderingShared.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 tangent    : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                half4  color       : COLOR;
                float2 uv          : TEXCOORD0;
                half3  normalWS    : TEXCOORD1;
                half3  tangentWS   : TEXCOORD2;
                half3  bitangentWS : TEXCOORD3;
            };

            Varyings NormalsRenderingVertex(Attributes v)
            {
                Varyings o = (Varyings)0;
                o.positionCS  = TransformObjectToHClip(v.positionOS);
                o.uv          = v.uv;
                o.color       = v.color;
                o.normalWS    = -GetViewForwardDir();
                o.tangentWS   = TransformObjectToWorldDir(v.tangent.xyz);
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * v.tangent.w;
                return o;
            }

            half4 NormalsRenderingFragment(Varyings i) : SV_Target
            {
                half4 mainTex  = i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
                return NormalsRenderingShared(mainTex, normalTS, i.tangentWS, i.bitangentWS, i.normalWS);
            }
            ENDHLSL
        }

        // ===== Pass 3: Forward / Unlit 폴백 =====
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   UnlitVertex
            #pragma fragment UnlitFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _RimColor;
                float4 _RimColorTop;
                float  _GradientHeight;
                float  _RimWidth;
                float4 _Color;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            Varyings UnlitVertex(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv         = TRANSFORM_TEX(v.uv, _MainTex);
                o.color      = v.color;
                return o;
            }

            half4 UnlitFragment(Varyings i) : SV_Target
            {
                half4 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                float2 rightOffset = float2(_RimWidth * _MainTex_TexelSize.x, 0);
                half4  colRight    = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + rightOffset);
                half   rimMask     = saturate(main.a - colRight.a);

                half3 rimGradient = lerp(_RimColor.rgb, _RimColorTop.rgb,
                                         saturate(i.uv.y / _GradientHeight));

                half3 finalRGB = main.rgb * i.color.rgb + rimGradient * rimMask;
                return half4(finalRGB, main.a * i.color.a);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
