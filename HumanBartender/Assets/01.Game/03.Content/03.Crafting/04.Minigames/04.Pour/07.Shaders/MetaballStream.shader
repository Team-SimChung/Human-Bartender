// 블롭(점+반지름) 배열을 받아 필드값을 합산하고 threshold로 잘라 방울들이 서로 뭉쳐 보이게 그린다.
// 블롭 사이 간격이 반지름보다 좁으면 자연스럽게 하나로 이어진 액체 줄기처럼 보인다.
Shader "Pour/MetaballStream"
{
    Properties
    {
        _Color ("Liquid Color", Color) = (1,1,1,1)
        _Threshold ("Threshold", Range(0.01, 3)) = 1.0
        _Smoothness ("Edge Smoothness", Range(0.001, 1)) = 0.15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            // SphLiquidRenderer.MaxBlobs와 반드시 같아야 한다.
            // 루프는 실제 블롭 수(_BlobCount)만큼만 도므로, 이 값을 키워도 안 쓰면 비용은 늘지 않는다.
            // SphLiquidRenderer.MaxBlobs와 반드시 같아야 한다.
            //
            // 1023이 이 방식의 천장이다 — Unity의 SetVectorArray가 그 이상을 받지 않는다.
            // 게다가 아래 루프가 픽셀마다 전 블롭을 훑으므로 비용이 (화면 픽셀 수 x 이 값)이라,
            // 여기까지 올리면 프레임을 크게 내준다. 액체를 더 늘려야 한다면 이 값이 아니라
            // 렌더 방식을 바꿔야 한다(블롭마다 자기 크기 사각형만 그리는 2패스 방식).
            #define MAX_BLOBS 1023

            fixed4 _Color;
            float _Threshold;
            float _Smoothness;

            int _BlobCount;
            // xy = 블롭 월드 위치, z = 반지름
            float4 _BlobPositions[MAX_BLOBS];
            // xy = 진행 방향(단위벡터), z = 그 방향으로 늘이는 배수
            float4 _BlobStretch[MAX_BLOBS];

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 worldPos : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float field = 0;

                for (int b = 0; b < _BlobCount; b++)
                {
                    float2 c = _BlobPositions[b].xy;
                    float r = _BlobPositions[b].z;
                    if (r <= 0) continue;

                    // 진행 방향과 그 수직 방향에 서로 다른 배율을 건 타원 거리.
                    //
                    // 길이(stretch): 빠르게 떨어지는 파티클은 중력에 가속돼 서로 간격이 벌어지는데,
                    // 그대로 원으로 그리면 필드가 겹치지 않아 알갱이로 흩어져 보인다. 진행 방향으로
                    // 늘여주면 앞뒤 파티클이 이어붙어 하나의 줄기로 보인다.
                    //
                    // 두께(thin): 수직 방향만 조인다. 축이 달라서 아무리 가늘게 해도 줄기가 끊어지지
                    // 않는다 — 이어짐은 위의 stretch가 담당한다. 파티클 수를 늘리지 않고(=spacing을
                    // 줄이지 않고) 줄기만 가늘게 만드는 유일한 방법이다.
                    float2 delta = i.worldPos - c;
                    float2 dir = _BlobStretch[b].xy;
                    float stretch = max(_BlobStretch[b].z, 1.0);
                    float thin = max(_BlobStretch[b].w, 0.05);

                    float along = dot(delta, dir) / stretch;
                    float perp = dot(delta, float2(-dir.y, dir.x)) / thin;
                    float d = sqrt(along * along + perp * perp);

                    float t = saturate(1 - d / r);
                    field += t * t * t;
                }

                float alpha = smoothstep(_Threshold - _Smoothness, _Threshold + _Smoothness, field);
                clip(alpha - 0.001);

                return fixed4(_Color.rgb, alpha * _Color.a);
            }
            ENDCG
        }
    }
}
