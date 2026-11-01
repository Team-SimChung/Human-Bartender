Shader "Custom/SpriteRevealUpward"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Progress ("Progress", Range(0,1)) = 0.8
        _Softness ("Edge Softness", Range(0.001, 0.5)) = 0.1
        // x = bounds 최소 로컬 Y, y = bounds 높이. 스크립트에서 매 프레임 주입.
        _SpriteBounds ("Sprite Bounds (minY, height)", Vector) = (-0.5, 1, 0, 0)
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float localY:TEXCOORD1; };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Progress;
                float _Softness;
                float4 _SpriteBounds;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                OUT.localY = IN.positionOS.y;   // Pivot 기준 로컬 Y
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color * _Color;

                // 로컬 Y를 bounds(minY, height)로 0~1 정규화 -> 프레임/아틀라스 무관하게 일관
                float minY   = _SpriteBounds.x;
                float height = max(_SpriteBounds.y, 1e-4);
                float y = saturate((IN.localY - minY) / height);

                // y가 _Progress보다 아래면 밝게, 위면 검게
                float edge = smoothstep(_Progress - _Softness, _Progress + _Softness, y);
                tex.rgb *= (1.0 - edge);   // 아래부터 위로 밝아짐

                return tex;   // 알파 유지 -> 가려진 부분은 불투명 검정
            }
            ENDHLSL
        }
    }
}
