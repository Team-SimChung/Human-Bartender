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
        // x 방향 집중도(지수), y 거리 감쇠 지수, z 광원에서 몸까지 가장 가까운 거리
        _RimShape ("Rim shape", Vector) = (2, 1.5, 1, 0)
        // xy 광원 월드 좌표, z 역광면 그림자 가중치, w 광원 방향 가중치(0이면 _AmbientDir)
        _LightPos ("Light position", Vector) = (0, 0, 0, 0)
        _AmbientDir ("Ambient light direction", Vector) = (0.33, 0.94, 0, 0)
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
        float4 _BodyShade, _Rim, _RimGlow, _RimShape, _LightPos, _AmbientDir, _BodyBounds;
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
                return o;
            }

            // 화면 미분으로 월드 방향을 텍스처(텍셀) 방향으로 옮긴다.
            // flipX, 음수 스케일, 아틀라스 회전이 모두 uv 미분에 담겨 있어 반전 정보에 의존하지 않는다.
            float2 WorldToTexelDir(float2 dir, float2 uv, float2 world)
            {
                float2 uvX = ddx(uv), uvY = ddy(uv), worldX = ddx(world), worldY = ddy(world);
                float det = worldX.x * worldY.y - worldY.x * worldX.y;
                float2 screen = float2(dir.x * worldY.y - dir.y * worldY.x, dir.y * worldX.x - dir.x * worldX.y) / (abs(det) > 1e-12 ? det : 1e-12);
                float2 texel = (uvX * screen.x + uvY * screen.y) / max(_SpriteTexel.xy, 1e-6);
                return texel / max(length(texel), 1e-6);
            }

            half Alpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            // dir 쪽 width 텍셀 거리의 상하좌우 이웃이 비어 있는 정도를 방향 가중치로 합친다.
            // 대각선 광원에서도 픽셀 외곽이 끊기지 않고 이어지며, focus가 클수록 광원을 정면으로 향한 변만 남는다.
            half EdgeAt(float2 uv, float2 dir, float width, float focus)
            {
                float2 t = _SpriteTexel.xy * width;
                float4 w = pow(saturate(float4(dir.x, -dir.x, dir.y, -dir.y)), focus);
                return saturate((1 - Alpha(uv + float2(t.x, 0))) * w.x + (1 - Alpha(uv - float2(t.x, 0))) * w.y
                     + (1 - Alpha(uv + float2(0, t.y))) * w.z + (1 - Alpha(uv - float2(0, t.y))) * w.w);
            }

            half EdgeFacing(float2 uv, float2 dir, float width, float focus)
            {
                return max(EdgeAt(uv, dir, 1, focus), EdgeAt(uv, dir, max(1, width), focus));
            }

            // 외곽에서 안쪽으로 갈수록 약해지는 띠. 블룸 임계값을 넘는 면적을 넓혀 번짐을 키운다.
            half GlowBand(float2 uv, float2 dir, float width, float focus)
            {
                half glow = 0;
                [unroll] for (int k = 1; k <= 4; k++)
                {
                    float reach = 1 + (max(1, width) - 1) * k / 4.0;
                    glow = max(glow, EdgeAt(uv, dir, reach, focus) * (1 - (k - 1) / 4.0));
                }
                return glow;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 미분은 discard 전에 구한다.
                float2 toLight = _LightPos.xy - input.world.xy;
                float lightDistance = max(length(toLight), 0.0001);
                float2 worldDir = normalize(lerp(_AmbientDir.xy, toLight / lightDistance, saturate(_LightPos.w)) + 1e-5);
                float2 lightDir = WorldToTexelDir(worldDir, input.uv, input.world.xy);
                half4 main = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (main.a <= 0) discard;

                // 발에 가까울수록 바닥 차폐로 어두워진다.
                float height = saturate((input.world.y - _BodyBounds.x) / max(0.01, _BodyBounds.y * _BodyShade.y));
                float occlusion = _BodyShade.x * (1 - height) * (1 - height);
                // 광원 반대편 외곽선 안쪽: 그 방향 이웃이 비어 있으면 그늘면이다.
                half away = EdgeFacing(input.uv, -lightDir, _BodyShade.w, 1);
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
                float focus = max(0.5, _RimShape.x);
                half rim = EdgeFacing(input.uv, lightDir, _Rim.y, focus);
                half glow = GlowBand(input.uv, lightDir, _RimGlow.y, focus);
                // 광원에서 먼 부분일수록 약하게: 가로등 아래에서는 머리·어깨가 밝고 발끝은 은은하다.
                float attenuation = pow(saturate(max(0.05, _RimShape.z) / lightDistance), _RimShape.y);
                attenuation = lerp(1, attenuation, saturate(_LightPos.w));
                rim *= attenuation; glow *= attenuation;
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
