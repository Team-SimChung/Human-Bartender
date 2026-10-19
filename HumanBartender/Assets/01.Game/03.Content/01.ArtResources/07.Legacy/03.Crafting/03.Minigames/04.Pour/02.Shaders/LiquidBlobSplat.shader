// 블롭 하나를 자기 크기만 한 사각형으로 그려 필드값을 가산 합성한다(메타볼 1패스).
//
// 이전 방식은 화면의 모든 픽셀마다 모든 블롭을 검사해서 비용이 (픽셀 수 x 파티클 수)였다.
// 여기서는 각 블롭이 자기 주변 픽셀만 칠하므로 (파티클 수 x 블롭 픽셀)이 되어, 파티클을 몇 배로
// 늘려도 비용이 거의 늘지 않는다.
//
// 사각형 정점이 이미 "진행 방향으로 늘어난 타원"의 축을 따라 배치되므로, 셰이더는 사각형 안에서의
// 위치(uv, -1~1)의 길이만 보면 된다 — uv 공간의 원이 곧 월드의 타원이다.
Shader "Pour/LiquidBlobSplat"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend One One          // 가산 합성 — 겹칠수록 필드값이 쌓인다
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float d = length(i.uv);

                float t = saturate(1 - d);
                float field = t * t * t;

                return fixed4(field, 0, 0, 0);
            }
            ENDCG
        }
    }
}
