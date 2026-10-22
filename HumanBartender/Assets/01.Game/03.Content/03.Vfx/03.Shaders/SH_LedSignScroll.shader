// 전광판(LED 사인) 글자 흐름 셰이더 (URP 2D Renderer, Sprite-Lit-Default 기반).
// 스프라이트 텍스처에서 _ScrollRect로 지정한 픽셀 영역만 가로로 감아 돌리며(wrap) 그린다.
// 이동량을 정수 픽셀로 끊어 적용하므로 글자가 미끄러지지 않고 픽셀 한 칸씩 움직인다.
// 영역 밖은 Sprite-Lit-Default와 똑같이 그려지므로 2D 라이트, 노멀맵, 마스크가 그대로 동작한다.
// 사용: 전광판이 그려진 SpriteRenderer의 머티리얼로 넣고, _ScrollRect에 텍스처 픽셀 좌표(좌상단 기준 X, Y, 너비, 높이)를 적는다.
//       테두리 모서리처럼 영역 안에 있지만 움직이면 안 되는 픽셀은 _ScrollExcludeRect로 고정한다.
Shader "Custom/VFX/SH_LedSignScroll"
{
    Properties
    {
        _MainTex ("Diffuse", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite ("ZWrite", Float) = 0

        [Header(LED Sign Scroll)]
        _ScrollRect ("흐르는 영역 (텍스처 픽셀, 좌상단 기준: X, Y, 너비, 높이)", Vector) = (0, 0, 0, 0)
        _ScrollExcludeRect ("영역 안에서 고정할 픽셀 (테두리 모서리 등: X, Y, 너비, 높이. 0이면 없음)", Vector) = (0, 0, 0, 0)
        _ScrollSpeed ("흐르는 속도 (픽셀/초, 양수 = 왼쪽, 음수 = 오른쪽)", Float) = 12
        _ScrollStep ("한 번에 이동하는 픽셀 수 (1 = 한 픽셀씩, 2 = LED 점 간격씩)", Float) = 1
        _ScrollOffset ("시작 위치 (픽셀)", Float) = 0

        // Legacy properties. Sprite-Lit-Default와 동일하게 유지해 기존 스프라이트 머티리얼 설정과 호환되게 한다.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

        // NOTE: SRP Batcher 호환을 위해 모든 패스가 같은 UnityPerMaterial 레이아웃을 쓴다. 프로퍼티를 ifdef로 감싸지 말 것.
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float4 _ScrollRect;
            float4 _ScrollExcludeRect;
            float _ScrollSpeed;
            float _ScrollStep;
            float _ScrollOffset;
        CBUFFER_END

        // 픽셀 사각형(좌상단 기준 X, Y, 너비, 높이)을 UV 사각형(uMin, vMin, uMax, vMax)으로 바꾼다. 텍스처 UV는 좌하단이 원점이라 y를 뒤집는다.
        float4 PixelRectToUV(float4 rectPx, float2 texelSize)
        {
            return float4(rectPx.x * texelSize.x,
                          1.0 - (rectPx.y + rectPx.w) * texelSize.y,
                          (rectPx.x + rectPx.z) * texelSize.x,
                          1.0 - rectPx.y * texelSize.y);
        }

        bool InsideRect(float2 uv, float4 rectUV)
        {
            return uv.x >= rectUV.x && uv.x < rectUV.z && uv.y >= rectUV.y && uv.y < rectUV.w;
        }

        // 스크롤 영역 안의 UV를 정수 픽셀만큼 옆으로 민 샘플링 UV로 바꾼다. 영역 밖과 고정 픽셀은 그대로 둔다.
        // texSize: _MainTex(스프라이트 텍스처)의 픽셀 크기
        float2 LedSignScrollUV(float2 uv, float2 texSize)
        {
            float width = _ScrollRect.z;
            float height = _ScrollRect.w;
            if (width < 1.0 || height < 1.0 || texSize.x < 1.0 || texSize.y < 1.0) return uv;

            float2 texelSize = 1.0 / texSize;
            float4 rectUV = PixelRectToUV(_ScrollRect, texelSize);
            if (!InsideRect(uv, rectUV)) return uv;

            bool useExclude = _ScrollExcludeRect.z >= 1.0 && _ScrollExcludeRect.w >= 1.0;
            float4 excludeUV = PixelRectToUV(_ScrollExcludeRect, texelSize);
            if (useExclude && InsideRect(uv, excludeUV)) return uv;

            // 이동량을 _ScrollStep 픽셀 단위로 끊어 픽셀이 한 칸씩 움직이게 한다.
            float stepPx = max(round(_ScrollStep), 1.0);
            float offsetPx = floor((_Time.y * _ScrollSpeed + _ScrollOffset) / stepPx) * stepPx;

            // 오른쪽 픽셀을 가져오면(+offset) 내용이 왼쪽으로 흐른다. 영역을 벗어난 만큼 반대쪽으로 감는다.
            float localPx = (uv.x - rectUV.x) * texSize.x;
            float scrolled = localPx + offsetPx;
            scrolled -= width * floor(scrolled / width);

            float2 scrolledUV = float2(rectUV.x + scrolled * texelSize.x, uv.y);
            // 감겨 들어올 픽셀이 고정 픽셀(테두리)이면 끌고 오지 않고 원래 자리의 픽셀을 그대로 보여 준다.
            if (useExclude && InsideRect(scrolledUV, excludeUV)) return uv;
            return scrolledUV;
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color        : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;

                return o;
            }

            // Lit2DCommon.hlsl의 CommonLitFragment와 같되, 텍스처 샘플링 UV만 스크롤된 UV를 쓴다.
            half4 LitFragment(Varyings input) : SV_Target
            {
                uint texWidth, texHeight;
                _MainTex.GetDimensions(texWidth, texHeight);
                const float2 uv = LedSignScrollUV(input.uv, float2(texWidth, texHeight));

                const half4 main = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv);
                const half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv));

                SurfaceData2D surfaceData;
                InputData2D inputData;

                InitializeSurfaceData(main.rgb, main.a, mask, normalTS, surfaceData);
                InitializeInputData(uv, input.lightingUV, inputData);

            #if defined(DEBUG_DISPLAY)
                SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, input.positionWS, input.positionCS, _MainTex);
                surfaceData.normalWS = input.normalWS;
            #endif

                return CombinedShapeLightShared(surfaceData, inputData);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4   color           : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;

                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                uint texWidth, texHeight;
                _MainTex.GetDimensions(texWidth, texHeight);
                const float2 uv = LedSignScrollUV(input.uv, float2(texWidth, texHeight));

                const half4 mainTex = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                const half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv));

                return NormalsRenderingShared(mainTex, normalTS, input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color *_Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                uint texWidth, texHeight;
                _MainTex.GetDimensions(texWidth, texHeight);
                const float2 uv = LedSignScrollUV(input.uv, float2(texWidth, texHeight));

                float4 mainTex = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

            #if defined(DEBUG_DISPLAY)
                SurfaceData2D surfaceData;
                InputData2D inputData;
                half4 debugColor = 0;

                InitializeSurfaceData(mainTex.rgb, mainTex.a, surfaceData);
                InitializeInputData(uv, inputData);
                SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, input.positionWS, input.positionCS, _MainTex);

                if (CanDebugOverrideOutputColor(surfaceData, inputData, debugColor))
                {
                    return debugColor;
                }
            #endif

                return mainTex;
            }
            ENDHLSL
        }
    }
}
