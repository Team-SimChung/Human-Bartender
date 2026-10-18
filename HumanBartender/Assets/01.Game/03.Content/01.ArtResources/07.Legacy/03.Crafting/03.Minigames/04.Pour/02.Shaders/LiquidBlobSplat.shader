Shader "Pour/LiquidBlobSplat"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Particle { float4 positionVelocity; float4 impact; };
            StructuredBuffer<Particle> _Particles;
            float4 _BlobShape;
            float4x4 _WorldToScreen;
            struct Varyings {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 velocity : TEXCOORD1;
                float3 impact : TEXCOORD2;
                float2 world : TEXCOORD3;
            };
            Varyings vert(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
            {
                static const float2 corners[6] = {
                    float2(-1,-1),float2(1,-1),float2(1,1),
                    float2(-1,-1),float2(1,1),float2(-1,1)
                };
                Particle p = _Particles[instanceId];
                float2 velocity = p.positionVelocity.zw;
                float speed = length(velocity);
                float2 direction = speed > 0.01 ? velocity / speed : float2(1,0);
                float stretch = clamp(1 + speed * _BlobShape.y, 1, max(1,_BlobShape.z));
                float normalizedStretch = (stretch - 1) / max(0.001, _BlobShape.z - 1);
                float thin = lerp(1, 1 - _BlobShape.w, normalizedStretch);
                float2 corner = corners[vertexId];
                float2 world = p.positionVelocity.xy + _BlobShape.x *
                    (direction * corner.x * stretch + float2(-direction.y,direction.x) * corner.y * thin);
                float2 uv = mul(_WorldToScreen, float4(world,0,1)).xy;
                Varyings o;
                o.position = float4(uv * 2 - 1, 0, 1);
                // 아래에서 위로 증가하는 필드 UV에 맞춰 D3D 렌더 타깃 방향을 보정한다.
                #if UNITY_UV_STARTS_AT_TOP
                o.position.y = -o.position.y;
                #endif
                o.uv = corner;
                o.velocity = velocity;
                o.impact = p.impact.xyz;
                o.world = world;
                return o;
            }
            float4 frag(Varyings i) : SV_Target
            {
                float t = saturate(1 - length(i.uv));
                float weight = t * t * t;
                float hit = saturate(1 - length(i.world - i.impact.xy) / max(0.001,_BlobShape.x));
                return float4(weight, i.velocity * weight, hit * hit * i.impact.z);
            }
            ENDHLSL
        }
    }
}
