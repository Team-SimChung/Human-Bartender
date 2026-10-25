Shader "Custom/Character/SpriteLitShading"
{
    Properties
    {
        _MainTex ("Diffuse", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _ShadeColor ("Body shade color", Color) = (0.32, 0.3, 0.45, 1)
        // x 접지 음영 강도, y 음영 높이(키 비율), z 역광면 그림자 강도, w 역광면 폭(텍셀)
        _BodyShade ("Body shade", Vector) = (0.35, 0.45, 0.4, 2)
        // x 림 강도, y 림 폭(텍셀), z 2D 라이트 적용(0/1)
        _Rim ("Rim", Vector) = (1.2, 1, 1, 0)
        _RimColor ("Rim color (premultiplied by light influence)", Color) = (0, 0, 0, 1)
        // x 글로우 강도, y 글로우 폭(텍셀)
        _RimGlow ("Rim glow", Vector) = (0.6, 3, 0, 0)
        // xy 광원 월드 좌표, z 역광면 그림자 가중치
        _LightPos ("Light position", Vector) = (0, 1000, 0, 0)
        // x 발 월드 y, y 캐릭터 키
        _BodyBounds ("Body bounds", Vector) = (0, 1, 0, 0)

        [HideInInspector] _SpriteTexel ("Sprite texel", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
    CBUFFER_START(UnityPerMaterial)
        half4 _Color, _ShadeColor, _RimColor;
        float4 _BodyShade, _Rim, _RimGlow, _LightPos, _BodyBounds;
        float4 _SpriteTexel; // CharacterSpriteLighting이 현재 스프라이트 텍스처 기준으로 채운다.
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode"="Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half2 lightingUV : TEXCOORD1;
                float3 world : TEXCOORD2;
                float2 lightDir : TEXCOORD3;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(o.world);
                o.uv = input.uv;
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                o.color = input.color * _Color * unity_SpriteColor;
                // 광원 방향을 텍스처 공간으로 옮긴다. 좌우 반전은 정점만 뒤집으므로 방향도 같이 뒤집는다.
                float2 toLight = _LightPos.xy - o.world.xy;
                float2 local = TransformWorldToObjectDir(float3(toLight, 0)).xy * unity_SpriteProps.xy;
                o.lightDir = local / max(length(local), 0.0001);
                return o;
            }

            half Alpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            // dir 쪽 width 텍셀 거리의 상하좌우 이웃이 비어 있는 정도를 방향 가중치로 합친다.
            // 대각선 광원에서도 픽셀 외곽이 끊기지 않고 이어진다.
            half EdgeAt(float2 uv, float2 dir, float width)
            {
                float2 t = _SpriteTexel.xy * width;
                return saturate((1 - Alpha(uv + float2(t.x, 0))) * saturate(dir.x) + (1 - Alpha(uv - float2(t.x, 0))) * saturate(-dir.x)
                     + (1 - Alpha(uv + float2(0, t.y))) * saturate(dir.y) + (1 - Alpha(uv - float2(0, t.y))) * saturate(-dir.y));
            }

            half EdgeFacing(float2 uv, float2 dir, float width)
            {
                return max(EdgeAt(uv, dir, 1), EdgeAt(uv, dir, max(1, width)));
            }

            // 외곽에서 안쪽으로 갈수록 약해지는 띠. 블룸 임계값을 넘는 면적을 넓혀 번짐을 키운다.
            half GlowBand(float2 uv, float2 dir, float width)
            {
                half glow = 0;
                [unroll] for (int k = 1; k <= 4; k++)
                {
                    float distance = 1 + (max(1, width) - 1) * k / 4.0;
                    glow = max(glow, EdgeAt(uv, dir, distance) * (1 - (k - 1) / 4.0));
                }
                return glow;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 main = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (main.a <= 0) discard;

                // 발에 가까울수록 바닥 차폐로 어두워진다.
                float height = saturate((input.world.y - _BodyBounds.x) / max(0.01, _BodyBounds.y * _BodyShade.y));
                float occlusion = _BodyShade.x * (1 - height) * (1 - height);
                // 광원 반대편 외곽선 안쪽: 그 방향 이웃이 비어 있으면 그늘면이다.
                half away = EdgeFacing(input.uv, -input.lightDir, _BodyShade.w);
                float shade = saturate(occlusion + away * _BodyShade.z * _LightPos.z);
                main.rgb = lerp(main.rgb, main.rgb * _ShadeColor.rgb, shade);

                SurfaceData2D surfaceData;
                InputData2D inputData;
                half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv);
                InitializeSurfaceData(main.rgb, main.a, mask, surfaceData);
                InitializeInputData(input.uv, input.lightingUV, inputData);
                half4 color = CombinedShapeLightShared(surfaceData, inputData);
                color.rgb = lerp(main.rgb, color.rgb, _Rim.z);

                // 광원 쪽 외곽선: 그 방향 이웃이 비어 있는 픽셀만 밝힌다.
                half rim = EdgeFacing(input.uv, input.lightDir, _Rim.y);
                half glow = GlowBand(input.uv, input.lightDir, _RimGlow.y);
                // 외곽선은 글로우만큼 더 밝혀 블룸에 걸리게 하고, 안쪽 띠는 은은하게 번진다.
                color.rgb += _RimColor.rgb * (_Rim.x * rim * (1 + _RimGlow.x) + glow * _RimGlow.x * 0.6);
                color.a = main.a;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode"="NormalsRendering" }

            HLSLPROGRAM
            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };
            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4 color : COLOR;
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
                return CommonNormalsFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

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

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 UnlitFragment(Varyings input) : SV_Target
            {
                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }
}
