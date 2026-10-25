Shader "Custom/Outside/CharacterShadow"
{
    Properties { _MainTex ("Sprite alpha", 2D) = "white" {} _Color ("Shadow color", Color) = (0.01, 0.015, 0.025, 1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _GroundRect, _Foot, _ShadowTexel;
                float4x4 _WorldToGround;
                float _Opacity;
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
            half4 Frag(Varyings input) : SV_Target
            {
                float2 ground = mul(_WorldToGround, float4(input.world, 1)).xy;
                float edge = min(min(ground.x - _GroundRect.x, ground.y - _GroundRect.y), min(_GroundRect.z - ground.x, _GroundRect.w - ground.y));
                float clipGround = saturate(edge * 100);
                // 픽셀 스프라이트 실루엣의 계단을 _ShadowTexel.z 텍셀 반경으로 부드럽게 푼다.
                float2 blur = _ShadowTexel.xy * _ShadowTexel.z;
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * 0.4;
                alpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(blur.x, 0)).a * 0.15;
                alpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(blur.x, 0)).a * 0.15;
                alpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, blur.y)).a * 0.15;
                alpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(0, blur.y)).a * 0.15;
                float distance = length(input.world.xy - _Foot.xy);
                alpha *= 1 - 0.65 * smoothstep(0, max(0.05, _Foot.z), distance);
                return half4(_Color.rgb, alpha * _Opacity * _Color.a * clipGround);
            }
            ENDHLSL
        }
    }
}
