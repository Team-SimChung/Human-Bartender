Shader "Custom/Outside/Puddle"
{
    Properties
    {
        _MainTex ("Optional puddle mask (alpha)", 2D) = "white" {}
        _Tint ("Reflection tint", Color) = (0.5, 0.62, 0.75, 1)
        _WaterColor ("Water tint", Color) = (0.025, 0.04, 0.065, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_CameraSortingLayerTexture);
            TEXTURE2D(_ForegroundTexture0);
            TEXTURE2D(_ForegroundTexture1);
            TEXTURE2D(_ForegroundTexture2);
            TEXTURE2D(_ForegroundTexture3);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint, _WaterColor;
                float4 _Surface, _SurfaceSize, _Shape;
                float4 _Ripples[16];
                float _SurfaceEnabled, _ReflectionEnabled;
                float4x4 _ForegroundMatrix0, _ForegroundMatrix1, _ForegroundMatrix2, _ForegroundMatrix3;
                float4 _ForegroundU0, _ForegroundU1, _ForegroundU2, _ForegroundU3;
                float4 _ForegroundV0, _ForegroundV1, _ForegroundV2, _ForegroundV3;
                float4 _ForegroundBounds0, _ForegroundBounds1, _ForegroundBounds2, _ForegroundBounds3;
                half4 _ForegroundColor0, _ForegroundColor1, _ForegroundColor2, _ForegroundColor3;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.world = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.uv = input.uv;
                return o;
            }
            float Pool(float2 p, float2 center, float2 radius)
            {
                return length((p - center) / radius) - 1;
            }
            half3 AddForeground(half3 background, float3 world, TEXTURE2D_PARAM(tex, texSampler), float4x4 worldToSprite, float3 u, float3 v, float4 bounds, half4 tint)
            {
                if (tint.a <= 0) return background;
                float3 local = float3(mul(worldToSprite, float4(world, 1)).xy, 1);
                float2 uv = float2(dot(local, u), dot(local, v));
                float inside = step(bounds.x, local.x) * step(bounds.y, local.y) * step(local.x, bounds.z) * step(local.y, bounds.w);
                half4 foreground = SAMPLE_TEXTURE2D(tex, texSampler, uv) * tint;
                return lerp(background, foreground.rgb, foreground.a * inside);
            }
            float Outline(float2 p)
            {
                // 각 형태는 서로 다른 물길/패임을 가지며 시드로 가장자리도 달라진다.
                float phase = _Shape.y * 0.73;
                p.y += sin(p.x * 5.5 + phase) * 0.1;
                float d;
                if (_Shape.x < 0.5)
                {
                    d = Pool(p, float2(-0.14, 0.06), float2(0.77, 0.67));
                    d = min(d, Pool(p, float2(0.46, -0.16), float2(0.43, 0.49)));
                }
                else if (_Shape.x < 1.5)
                {
                    d = min(Pool(p, float2(-0.44, 0.15), float2(0.48, 0.68)), Pool(p, float2(0.39, -0.18), float2(0.5, 0.64)));
                    d = min(d, Pool(p, float2(0, 0), float2(0.55, 0.25)));
                }
                else if (_Shape.x < 2.5)
                {
                    d = Pool(p, float2(0, 0.02), float2(0.91, 0.78));
                    d = max(d, -Pool(p, float2(0.2, 0.6), float2(0.62, 0.7)));
                }
                else
                {
                    float path = 0.2 * sin(p.x * 3.5 + phase);
                    d = Pool(p, float2(0, path), float2(0.95, 0.29));
                    d = min(d, Pool(p, float2(-0.45, -0.2), float2(0.31, 0.56)));
                    d = min(d, Pool(p, float2(0.6, 0.16), float2(0.3, 0.42)));
                }
                return d + _Shape.z * (0.16 * sin(p.x * 29 + phase + sin(p.y * 12)) + 0.08 * sin(p.y * 31 + phase));
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float ppu = max(1, _Surface.z);
                float2 size = max(_SurfaceSize.xy, 0.001);
                float2 uv = (floor(input.uv * size * ppu) + 0.5) / (size * ppu);
                float shapeDistance = Outline((uv - 0.5) * 2);
                float mask = 1 - smoothstep(-0.08, 0.025, shapeDistance);
                mask *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * _SurfaceEnabled;
                clip(mask - 0.005);
                float2 offset = 0;
                float glint = 0;
                float trough = 0;
                float impact = 0;
                [unroll] for (int i = 0; i < 16; i++)
                {
                    float age = _Ripples[i].z;
                    float2 delta = (uv - _Ripples[i].xy) * size;
                    delta.y *= 2.7;
                    float distance = length(delta);
                    float radius = age * 0.24 * _Ripples[i].w;
                    float life = pow(saturate(1 - age / _SurfaceSize.w), 1.4) * step(0, age);
                    float ring = (1 - smoothstep(0.45, 1.25, abs(distance - radius) * ppu)) * life;
                    float inner = (1 - smoothstep(0.4, 1.1, abs(distance - radius * 0.62) * ppu)) * life * smoothstep(0.12, 0.3, age);
                    float2 normal = delta / max(distance, 0.001);
                    offset += normal * (ring - inner * 0.4);
                    glint += ring + inner * 0.55;
                    trough += (1 - smoothstep(0.3, 1.2, abs(distance - radius - 1.4 / ppu) * ppu)) * life;
                    // 빗방울 충돌 순간에만 짧은 밝은 점을 추가한다.
                    impact += (1 - smoothstep(0.6, 1.8, distance * ppu)) * saturate(1 - age / 0.16) * step(0, age);
                }
                float compression = max(0.04, _Surface.y);
                // 수면 픽셀 중심에서 반사 좌표를 구해 마스크와 같은 픽셀 격자를 유지한다.
                float3 reflected = TransformObjectToWorld(float3(uv - 0.5, 0));
                reflected.y = _Surface.x + (_Surface.x - reflected.y) / compression;
                reflected.xy += offset * (_SurfaceSize.z / ppu);
                // 파문 왜곡이 기준선 아래 바닥을 다시 읽지 않도록 벽 밑선 위로 제한한다.
                reflected.y = max(reflected.y, _Surface.x + 0.5 / ppu);
                // 압축 반사는 수면 1픽셀이 배경 여러 줄에 대응하므로 그 구간을 평균해 줄무늬를 막는다.
                float span = max(0, 1 / compression - 1) / ppu;
                half3 reflection = 0;
                float valid = 0;
                [unroll] for (int t = 0; t < 4; t++)
                {
                    float3 tap = reflected;
                    tap.y += span * (t / 3.0 - 0.5);
                    float4 screen = ComputeScreenPos(TransformWorldToHClip(tap));
                    float2 reflectionUV = screen.xy / screen.w;
                    float edge = min(min(reflectionUV.x, reflectionUV.y), min(1 - reflectionUV.x, 1 - reflectionUV.y));
                    valid += smoothstep(0, 0.025, edge) * 0.25;
                    reflection += SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_PointClamp, saturate(reflectionUV)).rgb * 0.25;
                }
                valid *= _ReflectionEnabled;
                reflection = AddForeground(reflection, reflected, TEXTURE2D_ARGS(_ForegroundTexture0, sampler_PointClamp), _ForegroundMatrix0, _ForegroundU0.xyz, _ForegroundV0.xyz, _ForegroundBounds0, _ForegroundColor0);
                reflection = AddForeground(reflection, reflected, TEXTURE2D_ARGS(_ForegroundTexture1, sampler_PointClamp), _ForegroundMatrix1, _ForegroundU1.xyz, _ForegroundV1.xyz, _ForegroundBounds1, _ForegroundColor1);
                reflection = AddForeground(reflection, reflected, TEXTURE2D_ARGS(_ForegroundTexture2, sampler_PointClamp), _ForegroundMatrix2, _ForegroundU2.xyz, _ForegroundV2.xyz, _ForegroundBounds2, _ForegroundColor2);
                reflection = AddForeground(reflection, reflected, TEXTURE2D_ARGS(_ForegroundTexture3, sampler_PointClamp), _ForegroundMatrix3, _ForegroundU3.xyz, _ForegroundV3.xyz, _ForegroundBounds3, _ForegroundColor3);
                half3 color = lerp(_WaterColor.rgb, reflection * _Tint.rgb, valid * 0.9);
                color *= 1 - saturate(trough) * 0.3;
                float highlight = saturate(glint + impact * 1.4);
                color += highlight * _Shape.w * half3(0.64, 0.78, 0.86);
                return half4(color, mask * saturate(_Surface.w + highlight * 0.3));
            }
            ENDHLSL
        }
    }
}
