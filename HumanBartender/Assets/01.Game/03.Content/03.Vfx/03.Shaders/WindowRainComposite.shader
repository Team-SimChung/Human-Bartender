// 최종 장면 색을 물방울로 굴절시킨다. 실제 광원 정보는 쓰지 않는다: 밝은 간판·조명은
// 화면에 그려진 밝은 픽셀 그대로 방울 안에서 휘어 보이고, 하이라이트는 고정된 방향의 근사다.
Shader "Hidden/WindowRain/Composite"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off
        Pass
        {
            Name "WindowRainComposite"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_RainWater);
            TEXTURE2D(_RainTrail);
            float4 _RainGrid;        // 가상 픽셀 격자 크기와 역수
            float4 _RainOptics;      // 굴절, 분산(픽셀), 하이라이트, 가장자리 그늘
            float4 _RainStyle;       // 전체 강도, 셰이딩 픽셀 크기 배율
            float4 _RainTrailParams; // 물자국 진하기

            float Height(float2 uv) { return SAMPLE_TEXTURE2D_LOD(_RainWater, sampler_LinearClamp, uv, 0).r; }
            float3 Scene(float2 uv) { return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, saturate(uv), 0).rgb; }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                [branch] if (_RainStyle.x <= 0) return scene;
                // 물리 격자는 유지하고 광학 셰이딩만 조금 더 굵은 격자로 샘플링한다.
                float2 texel = _RainGrid.zw;
                float2 shadingGrid = max(round(_RainGrid.xy / max(1, _RainStyle.y)), 1);
                float2 cell = (floor(uv * shadingGrid) + 0.5) / shadingGrid;
                float h = Height(cell);
                float trail = SAMPLE_TEXTURE2D_LOD(_RainTrail, sampler_PointClamp, cell, 0).r;
                [branch] if (h <= 0.02 && trail < 0.02) return scene;

                float3 color = scene.rgb;
                if (h > 0.02)
                {
                    // 이웃 높이 차로 굴곡을 구한다. 테두리는 바깥이 0이라 기울기가 크고 굴절도 강하다.
                    float2 slope = float2(Height(cell + float2(texel.x, 0)) - Height(cell - float2(texel.x, 0)),
                                          Height(cell + float2(0, texel.y)) - Height(cell - float2(0, texel.y))) * 0.5;
                    float3 normal = normalize(float3(-slope * 1.2, 1));
                    float edge = 1 - normal.z;
                    float size = saturate(h / 3);
                    // 볼록 렌즈처럼 반대편 장면을 뒤집어 비춘다. 두꺼운 방울일수록 많이 휜다.
                    float2 offset = -normal.xy * _RainOptics.x * float2(_RainGrid.y / _RainGrid.x, 1) * (0.35 + 0.65 * size);
                    float2 base = cell + offset;
                    // 색 분산은 강하게 꺾이는 가장자리에만, 한두 픽셀 안에서.
                    float spread = _RainOptics.y * smoothstep(0.2, 0.7, edge);
                    float2 direction = normalize(normal.xy + float2(1e-5, 0)) * texel * spread;
                    float3 refracted = float3(Scene(base + direction).r, Scene(base).g, Scene(base - direction).b);
                    // 중앙에 흰 막을 씌우지 않는다. 투명한 볼록 유리의 밝기는 가장자리에 모은다.
                    float2 light = normalize(float2(-0.55, 0.85));
                    float facing = dot(normalize(normal.xy + float2(1e-5, 0)), light);
                    float rim = smoothstep(0.08, 0.55, edge);
                    refracted *= 1 - _RainOptics.w * rim * saturate(-facing) * 0.65;
                    float spec = smoothstep(0.94, 0.995, dot(normal, normalize(float3(-0.35, 0.55, 0.75)))) * smoothstep(0.8, 1.8, h);
                    float reflection = rim * (0.12 + 0.5 * saturate(facing)) + spec * 0.55;
                    refracted += reflection * _RainOptics.z * (1 - saturate(refracted));
                    color = refracted;
                }
                else
                {
                    // 얇은 물막의 두께 변화로 굴절과 약한 가장자리 반사를 남긴다.
                    float t = saturate(trail) * _RainTrailParams.x;
                    float2 filmSlope = float2(
                        SAMPLE_TEXTURE2D_LOD(_RainTrail, sampler_PointClamp, cell + float2(texel.x, 0), 0).r - SAMPLE_TEXTURE2D_LOD(_RainTrail, sampler_PointClamp, cell - float2(texel.x, 0), 0).r,
                        SAMPLE_TEXTURE2D_LOD(_RainTrail, sampler_PointClamp, cell + float2(0, texel.y), 0).r - SAMPLE_TEXTURE2D_LOD(_RainTrail, sampler_PointClamp, cell - float2(0, texel.y), 0).r);
                    float3 film = Scene(cell - filmSlope * texel * 2.2) * 0.96;
                    film += saturate(dot(filmSlope, float2(-0.55, 0.85))) * 0.1;
                    color = lerp(color, film, t * 0.85);
                }
                return half4(lerp(scene.rgb, color, saturate(_RainStyle.x)), scene.a);
            }
            ENDHLSL
        }
    }
}
