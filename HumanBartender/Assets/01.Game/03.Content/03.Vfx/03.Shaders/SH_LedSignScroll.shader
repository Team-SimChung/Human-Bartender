// 전광판(LED 사인) 글자 흐름 셰이더 (URP 2D Renderer, Sprite-Lit-Default 기반).
// 스프라이트 텍스처에서 전광판 안쪽(_ScrollMaskTex로 칠한 픽셀)만 가로로 감아 돌리며(wrap) 그린다.
// 이동량을 정수 픽셀로 끊어 적용하므로 글자가 미끄러지지 않고 픽셀 한 칸씩 움직인다.
// 마스크 밖(테두리, 건물)은 Sprite-Lit-Default와 똑같이 그려지므로 2D 라이트, 노멀맵, 마스크가 그대로 동작한다.
// 테두리의 깎인 모서리처럼 마스크가 짧은 줄은, 감겨 들어올 픽셀이 마스크 밖이면 빈 전광판 픽셀(_ScrollBlankPixel)을 보여 준다.
// 글자가 테두리 뒤로 들어갔다 나오는 것처럼 보인다.
// 전광판의 밝은 글자는 스프라이트 라이트 쿠키(볼류메트릭 포함)가 비추는 빛이다. 라이트는 화면에 고정돼 있어 같이 흐르지 않으므로,
// 라이트 쿠키에서는 전광판 글자 부분을 지우고(TX_LedSign_LightCookie_01) 그 빛을 이 셰이더가 원본 쿠키에서 스크롤된 위치로 읽어 직접 더한다.
//   곱해지는 빛: 텍스처 색 x 쿠키 x 빛 색          (라이트의 Multiply 블렌드와 같음)
//   더해지는 빛: 쿠키 x 쿠키 알파 x 빛 색 x 볼류메트릭 강도 (라이트의 Volumetric과 같음)
// 사용: 전광판이 그려진 SpriteRenderer의 머티리얼로 넣는다. _ScrollRect는 마스크 텍스처가 덮는 스프라이트 픽셀 영역
//       (좌상단 기준 X, Y, 너비, 높이)이고, 마스크 텍스처 크기는 그 너비, 높이와 같아야 한다.
// 팬 회전: _SpinCenter를 중심으로 _SpinRadius 안의 픽셀을 각도 단위(_SpinSteps)로 끊어 돌린다.
//          각 화면 픽셀이 원본 텍스처 픽셀 하나를 그대로 가져오므로 픽셀 격자가 유지된다.
Shader "Custom/VFX/SH_LedSignScroll"
{
    Properties
    {
        _MainTex ("Diffuse", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite ("ZWrite", Float) = 0

        [Header(LED Sign Scroll)]
        _ScrollRect ("마스크가 덮는 영역 (텍스처 픽셀, 좌상단 기준: X, Y, 너비, 높이)", Vector) = (0, 0, 0, 0)
        [NoScaleOffset] _ScrollMaskTex ("전광판 안쪽 마스크 (흰색 = 흐르는 픽셀, 크기 = 영역 너비 x 높이)", 2D) = "white" {}
        _ScrollBlankPixel ("빈 전광판 픽셀 위치 (테두리 뒤에서 나올 때 채울 색, 좌상단 기준 X, Y)", Vector) = (0, 0, 0, 0)
        _ScrollSpeed ("흐르는 속도 (픽셀/초, 양수 = 왼쪽, 음수 = 오른쪽)", Float) = 12
        _ScrollStep ("한 번에 이동하는 픽셀 수 (1 = 한 픽셀씩, 2 = LED 점 간격씩)", Float) = 1
        _ScrollOffset ("시작 위치 (픽셀)", Float) = 0
        [Toggle] _ScrollLighting ("영역 안의 2D 라이트도 함께 흐르기", Float) = 0
        _PixelsPerUnit ("스프라이트 Pixels Per Unit (텍스처 임포트 설정과 같게)", Float) = 100

        [Header(LED Glow)]
        _SignGlowTex ("글자 빛 텍스처 (라이트 쿠키 원본)", 2D) = "black" {}
        _SignGlowOffset ("빛 텍스처 픽셀 오프셋 (스프라이트 픽셀 + 오프셋 = 빛 텍스처 픽셀, 좌상단 기준 X, Y)", Vector) = (0, 0, 0, 0)
        [HDR] _SignGlowColor ("빛 색 (라이트 Color x Intensity)", Color) = (0, 0, 0, 1)
        _SignGlowVolume ("볼류메트릭 강도 (라이트 Volume Intensity)", Float) = 0
        _SignGlowStrength ("빛 세기 배율", Range(0, 4)) = 1

        [Header(Fan Spin)]
        _SpinCenter ("팬 중심 (텍스처 픽셀, 좌상단 기준 X, Y. 픽셀 한가운데는 .5)", Vector) = (0, 0, 0, 0)
        _SpinCenter2 ("두 번째 팬 중심 (X, Y. 0, 0이면 끔. 나머지 값은 첫 번째 팬과 같이 씀)", Vector) = (0, 0, 0, 0)
        _SpinRadius ("돌아가는 반지름 (픽셀, 0이면 끔)", Float) = 0
        _SpinInnerRadius ("고정할 중앙 반지름 (허브, 픽셀)", Float) = 0
        _SpinSpeed ("초당 회전 수 (양수 = 시계 방향, 음수 = 반시계)", Float) = 1
        _SpinSteps ("한 바퀴를 끊는 단계 수 (클수록 부드럽게)", Float) = 12

        [Header(Neon Flicker Dim)]
        [NoScaleOffset] _NeonDimMask ("깜빡일 때 어두워질 간판 픽셀 (R, G, B, A = NeonFlicker 그룹 채널 0~3)", 2D) = "black" {}
        _NeonDimFloor ("간판이 꺼졌을 때 남는 텍스처 밝기", Range(0, 1)) = 0.35
        [HideInInspector] _NeonDim ("채널별 꺼짐 정도 (NeonFlicker가 실행 중에 설정)", Vector) = (0, 0, 0, 0)

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
            float4 _ScrollBlankPixel;
            float _ScrollSpeed;
            float _ScrollStep;
            float _ScrollOffset;
            float _ScrollLighting;
            float _PixelsPerUnit;
            float4 _SignGlowOffset;
            half4 _SignGlowColor;
            float _SignGlowVolume;
            float _SignGlowStrength;
            float4 _SpinCenter;
            float4 _SpinCenter2;
            float _SpinRadius;
            float _SpinInnerRadius;
            float _SpinSpeed;
            float _SpinSteps;
            float4 _NeonDim;
            float _NeonDimFloor;
        CBUFFER_END

        TEXTURE2D(_ScrollMaskTex);
        SAMPLER(sampler_ScrollMaskTex);

        // 스프라이트 텍스처 UV <-> 픽셀 좌표(좌상단 기준 정수). 텍스처 UV는 좌하단이 원점이라 y를 뒤집는다.
        float2 LedSignUVToPixel(float2 uv, float2 texSize)
        {
            return float2(floor(uv.x * texSize.x), texSize.y - 1.0 - floor(uv.y * texSize.y));
        }

        float2 LedSignPixelToUV(float2 px, float2 texSize)
        {
            return float2((px.x + 0.5) / texSize.x, 1.0 - (px.y + 0.5) / texSize.y);
        }

        // 스프라이트 픽셀이 전광판 안쪽(마스크 흰색)이면 true
        bool LedSignInMask(float2 px)
        {
            float2 local = px - _ScrollRect.xy;
            if (local.x < 0.0 || local.y < 0.0 || local.x >= _ScrollRect.z || local.y >= _ScrollRect.w) return false;
            float2 maskUV = float2((local.x + 0.5) / _ScrollRect.z, 1.0 - (local.y + 0.5) / _ScrollRect.w);
            return SAMPLE_TEXTURE2D_LOD(_ScrollMaskTex, sampler_ScrollMaskTex, maskUV, 0).r > 0.5;
        }

        // center를 중심으로 한 팬 원 안의 픽셀이면 회전된 원본 픽셀 위치를 돌려주고 true. 아니면 false.
        bool FanSpinAround(float2 px, float2 center, out float2 sourcePx)
        {
            sourcePx = px;
            float2 d = px + 0.5 - center;
            float radiusSq = _SpinRadius * _SpinRadius;
            float innerSq = _SpinInnerRadius * _SpinInnerRadius;
            // 바깥 테두리와 중앙 허브는 돌리지 않는다.
            if (dot(d, d) > radiusSq || dot(d, d) <= innerSq) return false;

            // 각도를 _SpinSteps 단계로 끊는다. 화면 픽셀이 가져올 원본 위치는 반대 방향으로 돌린 자리다.
            float steps = max(round(_SpinSteps), 1.0);
            float turns = floor(_Time.y * _SpinSpeed * steps) / steps;
            float angle = -turns * 6.28318530718;
            float c = cos(angle);
            float s = sin(angle);
            float2 rotated = floor(center + float2(c * d.x - s * d.y, s * d.x + c * d.y));

            // 반올림 때문에 원 밖이나 허브 픽셀을 끌어오면 테두리, 허브가 섞이므로 그 픽셀은 그대로 둔다.
            float2 sd = rotated + 0.5 - center;
            if (dot(sd, sd) > radiusSq || dot(sd, sd) <= innerSq) return false;

            sourcePx = rotated;
            return true;
        }

        // 팬 원(첫 번째, 두 번째) 안의 픽셀이면 회전된 원본 픽셀의 UV를 돌려주고 spun = true. 아니면 uv 그대로.
        // 두 팬은 반지름, 허브, 속도, 단계 수를 같이 쓴다. 두 번째 중심이 (0, 0)이면 두 번째 팬은 끈다.
        float2 FanSpinUV(float2 uv, float2 texSize, out bool spun)
        {
            spun = false;
            if (_SpinRadius <= 0.0 || texSize.x < 1.0 || texSize.y < 1.0) return uv;

            float2 px = LedSignUVToPixel(uv, texSize);
            float2 sourcePx;
            if (FanSpinAround(px, _SpinCenter.xy, sourcePx) ||
                (any(_SpinCenter2.xy != 0.0) && FanSpinAround(px, _SpinCenter2.xy, sourcePx)))
            {
                spun = true;
                return LedSignPixelToUV(sourcePx, texSize);
            }
            return uv;
        }

        struct LedSignSample
        {
            float2 uv;        // 실제로 텍스처를 읽을 UV
            float2 sourcePx;  // 흘러 들어온 픽셀의 원래 위치 (글자 빛을 읽을 위치)
            float hasGlow;    // 1이면 sourcePx 위치의 글자 빛을 더한다
        };

        // 전광판 안쪽 픽셀은 정수 픽셀만큼 옆에서 가져오고, 바깥은 그대로 둔다.
        // texSize: _MainTex(스프라이트 텍스처)의 픽셀 크기
        LedSignSample LedSignScroll(float2 uv, float2 texSize)
        {
            LedSignSample s;
            s.uv = uv;
            s.sourcePx = 0;
            s.hasGlow = 0;

            bool spun;
            float2 spinUV = FanSpinUV(uv, texSize, spun);
            if (spun)
            {
                s.uv = spinUV;
                return s;
            }

            if (_ScrollRect.z < 1.0 || _ScrollRect.w < 1.0 || texSize.x < 1.0 || texSize.y < 1.0) return s;

            float2 px = LedSignUVToPixel(uv, texSize);
            if (!LedSignInMask(px)) return s;

            // 이동량을 _ScrollStep 픽셀 단위로 끊어 픽셀이 한 칸씩 움직이게 한다.
            float stepPx = max(round(_ScrollStep), 1.0);
            float offsetPx = floor((_Time.y * _ScrollSpeed + _ScrollOffset) / stepPx) * stepPx;

            // 오른쪽 픽셀을 가져오면(+offset) 내용이 왼쪽으로 흐른다. 영역 너비만큼 반대쪽으로 감는다.
            float width = _ScrollRect.z;
            float local = px.x - _ScrollRect.x + offsetPx;
            local -= width * floor(local / width);
            float2 sourcePx = float2(_ScrollRect.x + local, px.y);

            if (LedSignInMask(sourcePx))
            {
                s.uv = LedSignPixelToUV(sourcePx, texSize);
                s.sourcePx = sourcePx;
                s.hasGlow = 1;
            }
            else
            {
                // 감겨 들어올 자리가 테두리(마스크 밖)면 글자는 테두리 뒤에 있는 것으로 보고 빈 전광판을 보여 준다.
                s.uv = LedSignPixelToUV(_ScrollBlankPixel.xy, texSize);
            }
            return s;
        }

        float2 LedSignScrollUV(float2 uv, float2 texSize)
        {
            return LedSignScroll(uv, texSize).uv;
        }

        // 텍스처에서 오른쪽으로 1픽셀 갔을 때 라이팅 UV(화면 UV)가 변하는 양. 정점 셰이더에서 계산한다.
        float2 LedSignLightUVPerTexel()
        {
            float3 deltaOS = float3(unity_SpriteProps.x / max(_PixelsPerUnit, 0.0001), 0.0, 0.0);
            float3 deltaWS = mul((float3x3)GetObjectToWorldMatrix(), deltaOS);
            float4 deltaCS = mul(GetWorldToHClipMatrix(), float4(deltaWS, 0.0));
            return 0.5 * float2(deltaCS.x, deltaCS.y * _ProjectionParams.x);
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
                float2 lightUVPerTexel : TEXCOORD4;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            TEXTURE2D(_SignGlowTex);
            SAMPLER(sampler_SignGlowTex);
            TEXTURE2D(_NeonDimMask);
            SAMPLER(sampler_NeonDimMask);

            // 흘러 들어온 픽셀의 원래 위치에 해당하는 글자 빛(쿠키) 색. 전광판 밖이거나 빈 픽셀이면 0.
            half4 SampleLedSignGlow(LedSignSample s)
            {
                if (s.hasGlow < 0.5) return 0;
                uint glowWidth, glowHeight;
                _SignGlowTex.GetDimensions(glowWidth, glowHeight);
                if (glowWidth < 1 || glowHeight < 1) return 0;

                // 스프라이트 픽셀(좌상단 기준) -> 빛 텍스처 픽셀(좌상단 기준) -> 빛 텍스처 UV
                float2 glowPx = s.sourcePx + _SignGlowOffset.xy;
                float2 glowUV = float2((glowPx.x + 0.5) / glowWidth, 1.0 - (glowPx.y + 0.5) / glowHeight);
                return SAMPLE_TEXTURE2D_LOD(_SignGlowTex, sampler_SignGlowTex, glowUV, 0);
            }

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.lightUVPerTexel = LedSignLightUVPerTexel();

                return o;
            }

            // Lit2DCommon.hlsl의 CommonLitFragment와 같되, 텍스처 샘플링 UV를 스크롤된 UV로 바꾸고
            // 라이트도 그 픽셀이 원래 있던 화면 위치에서 읽는다.
            half4 LitFragment(Varyings input) : SV_Target
            {
                uint texWidth, texHeight;
                _MainTex.GetDimensions(texWidth, texHeight);
                const LedSignSample scroll = LedSignScroll(input.uv, float2(texWidth, texHeight));
                const float2 uv = scroll.uv;
                const float shiftTexels = (uv.x - input.uv.x) * texWidth * step(0.5, _ScrollLighting);
                const half2 lightingUV = half2(input.lightingUV + input.lightUVPerTexel * shiftTexels);

                half4 main = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                // NeonFlicker가 끈 간판은 텍스처에 그려진 불빛도 어둡게 한다.
                const half neonDim = saturate(dot(SAMPLE_TEXTURE2D_LOD(_NeonDimMask, sampler_NeonDimMask, uv, 0), _NeonDim));
                main.rgb *= lerp(1.0, _NeonDimFloor, neonDim);
                const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv);
                const half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv));

                SurfaceData2D surfaceData;
                InputData2D inputData;

                InitializeSurfaceData(main.rgb, main.a, mask, normalTS, surfaceData);
                InitializeInputData(uv, lightingUV, inputData);

            #if defined(DEBUG_DISPLAY)
                SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, input.positionWS, input.positionCS, _MainTex);
                surfaceData.normalWS = input.normalWS;
            #endif

                half4 color = CombinedShapeLightShared(surfaceData, inputData);

                // 라이트 쿠키에서 지운 전광판 글자 빛을 스크롤된 위치에서 다시 더한다.
                const half4 cookie = SampleLedSignGlow(scroll);
                const half3 glowColor = _SignGlowColor.rgb * _SignGlowStrength;
                color.rgb += main.rgb * cookie.rgb * glowColor;
                color.rgb += cookie.rgb * cookie.a * glowColor * _SignGlowVolume * main.a;
                return color;
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
