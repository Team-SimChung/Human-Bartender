Shader "Custom/SpriteEmissionHDR"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color (HDR)", Color) = (1,1,1,1)
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 1.5
        _AlphaMultiplier ("Alpha Multiplier", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull   Off
        Lighting Off
        ZWrite Off
        // Pure additive: 배경에 더하기만. 가리지 않음.
        // 알파는 RGB에 곱해져서 강도/페이드 조절용으로만 사용.
        Blend One One

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _EmissionColor;
                half   _EmissionIntensity;
                half   _AlphaMultiplier;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color      = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // 알파 가중치 (텍스처 알파 × 스프라이트 색 알파 × 머티리얼 슬라이더)
                half weight = tex.a * IN.color.a * _AlphaMultiplier;

                // HDR emission에 알파 가중치 곱해서 강도/페이드 조절
                //  - tex.a = 0 영역 → 0 더해짐 → 자연 페이드
                //  - tex.a = 1 영역 → 풀 강도 additive
                //  - 텍스처 RGB가 검정(0)이면 → 안 더해짐 → 빌딩 그대로 보임
                half3 finalRGB = tex.rgb * _EmissionColor.rgb * _EmissionIntensity * weight;

                // Blend One One 이라 알파는 무시됨. 1로 둠.
                return half4(finalRGB, 1);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
