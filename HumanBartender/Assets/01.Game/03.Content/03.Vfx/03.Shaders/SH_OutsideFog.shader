Shader "Custom/Outside/BackgroundFog"
{
    Properties
    {
        _FogColor ("Fog color", Color) = (0.42, 0.58, 0.78, 1)
        // x 농도, y 지면 기준 월드 y, z 포그 높이, w 높이 감쇠 지수
        _Density ("Density", Vector) = (0.45, -1.2, 4, 1.4)
        // x 노이즈 크기(월드), y 노이즈 강도, z 흐름 속도, w 픽셀/유닛
        _Noise ("Noise", Vector) = (2.5, 0.55, 0.12, 100)
        // x 2D 라이트 영향, y 밝기 단계(0이면 부드럽게), z 가장자리 페이드 폭(uv)
        _Style ("Style", Vector) = (0.5, 0, 0.04, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                float4 _Density, _Noise, _Style;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; half2 lightingUV : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.world = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.uv = input.uv;
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                return o;
            }
            float Hash(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }
            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // 픽셀아트와 같은 격자로 끊어서 포그 경계도 도트로 보이게 한다.
                float ppu = max(1, _Noise.w);
                float2 world = (floor(input.world.xy * ppu) + 0.5) / ppu;
                // 지면에서 가장 짙고 위로 갈수록 옅어지는 안개층.
                float height = saturate((world.y - _Density.y) / max(0.01, _Density.z));
                float density = pow(1 - height, max(0.1, _Density.w));
                // 옆으로 천천히 흐르는 두 겹의 안개 덩어리.
                float2 p = world / max(0.05, _Noise.x);
                float drift = _Time.y * _Noise.z;
                float clouds = ValueNoise(p + float2(drift, 0)) * 0.6 + ValueNoise(p * 2.3 + float2(-drift * 1.7, drift * 0.3)) * 0.4;
                density *= lerp(1, saturate(clouds * 1.6 - 0.2), _Noise.y);
                // 사각형 경계가 보이지 않도록 좌우·위를 부드럽게 지운다.
                float fade = max(0.0001, _Style.z);
                density *= smoothstep(0, fade, input.uv.x) * smoothstep(0, fade, 1 - input.uv.x) * smoothstep(0, fade, 1 - input.uv.y);
                float alpha = saturate(density * _Density.x);
                if (_Style.y > 0.5) alpha = floor(alpha * _Style.y + 0.5) / _Style.y;
                clip(alpha - 0.002);
                // 같은 레이어를 비추는 2D 라이트가 있으면 안개가 그 색으로 물든다.
                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(_FogColor.rgb, 1, surfaceData);
                InitializeInputData(input.uv, input.lightingUV, inputData);
                half3 lit = CombinedShapeLightShared(surfaceData, inputData).rgb;
                return half4(lerp(_FogColor.rgb, lit, saturate(_Style.x)), alpha * _FogColor.a);
            }
            ENDHLSL
        }
    }
}
