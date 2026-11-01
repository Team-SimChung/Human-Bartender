// 유리 위 물방울을 가상 픽셀 격자의 높이 텍스처와 물자국 텍스처로 그린다.
// 위치는 연속 좌표, 래스터화만 격자 단위(포인트 필터 저해상도 타깃)라 픽셀 아트 실루엣이 된다.
Shader "Hidden/WindowRain/Drops"
{
    SubShader
    {
        ZWrite Off ZTest Always Cull Off
        HLSLINCLUDE
        #include "UnityCG.cginc"
        struct Drop { float4 shape; float4 motion; float4 merge; float4 surface; };
        StructuredBuffer<Drop> _Drops;
        float4 _RainGrid;
        float _Fade;
        float _RainTime;
        static const float2 Corners[6] = {
            float2(-1,-1),float2(1,-1),float2(1,1),
            float2(-1,-1),float2(1,1),float2(-1,1)
        };
        float4 ToClip(float2 pixel)
        {
            float2 uv = pixel * _RainGrid.zw;
            float4 position = float4(uv * 2 - 1, 0, 1);
            #if UNITY_UV_STARTS_AT_TOP
            position.y = -position.y;
            #endif
            return position;
        }
        ENDHLSL

        // 0: 높이(구면 캡). 진행 방향 앞은 둥근 머리, 뒤는 늘어나며 가늘어지는 꼬리.
        Pass
        {
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 local : TEXCOORD0;
                nointerpolation float4 shape : TEXCOORD1;
                nointerpolation float4 merge : TEXCOORD2;
                nointerpolation float2 surface : TEXCOORD3;
            };
            Varyings vert(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
            {
                Drop d = _Drops[instanceId];
                float2 corner = Corners[vertexId];
                float radius = max(d.shape.z, 0.3);
                float stretch = d.shape.w;
                float2 forward = normalize(d.motion.xy + float2(0, -1e-4));
                float2 side = float2(-forward.y, forward.x);
                float2 absorbed = float2(dot(d.merge.xy, side), dot(d.merge.xy, forward));
                // 머리의 비대칭과 합체 중인 두 번째 덩어리까지 포함한다.
                float margin = radius * 0.3 + d.merge.w + 0.75;
                float2 lo = float2(-radius, -radius * stretch) - margin;
                float2 hi = float2(radius, radius) + margin;
                if (d.merge.z > 0.01)
                {
                    lo = min(lo, absorbed - d.merge.z - margin);
                    hi = max(hi, absorbed + d.merge.z + margin);
                }
                float2 local = lerp(lo, hi, corner * 0.5 + 0.5);
                Varyings o;
                o.position = ToClip(d.shape.xy + forward * local.y + side * local.x);
                o.local = local;
                o.shape = float4(radius, stretch, 0, 0);
                o.merge = float4(absorbed, d.merge.zw);
                o.surface = d.surface.xy;
                return o;
            }
            float4 frag(Varyings i) : SV_Target
            {
                float along = i.local.y / i.shape.x;
                float tail = saturate(-along / max(1, i.shape.y));
                float phase = i.surface.x;
                // 저주파 변화만 사용해 픽셀 단위 노이즈가 튀지 않게 한다.
                float pulse = sin(_RainTime * 3.1 + phase);
                float bend = (sin(tail * 4.6 + phase + _RainTime * 2.3) - sin(phase + _RainTime * 2.3)) * tail * i.surface.y;
                float width = lerp(1, 0.58, tail) * (1 + i.surface.y * (0.4 * pulse + 0.6 * sin(along * 3.2 + phase + _RainTime * 1.8)));
                if (along < 0) along /= i.shape.y;
                float across = (i.local.x / i.shape.x - bend) / max(0.25, width);
                float distance = (length(float2(across, along)) - 1) * i.shape.x;
                if (i.merge.z > 0.01)
                {
                    float other = length(i.local - i.merge.xy) - i.merge.z;
                    float neck = max(i.merge.w, 0.001);
                    float blend = saturate(0.5 + 0.5 * (other - distance) / neck);
                    distance = lerp(other, distance, blend) - neck * blend * (1 - blend);
                }
                if (distance >= 0) discard;
                float radius = max(i.shape.x, i.merge.z);
                float cap = saturate(1 + distance / radius);
                return float4(sqrt(1 - cap * cap) * radius, 1, 0, 0);
            }
            ENDHLSL
        }

        // 1: 물자국이 마르며 옅어진다(곱셈).
        Pass
        {
            Blend DstColor Zero
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(uint vertexId : SV_VertexID) : SV_POSITION
            {
                float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
                return float4(uv * 2 - 1, 0, 1);
            }
            float4 frag() : SV_Target { return _Fade; }
            ENDHLSL
        }

        // 2: 흐르는 방울이 지나간 선분에 얇은 물자국을 남긴다.
        Pass
        {
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            struct Varyings { float4 position : SV_POSITION; float2 local : TEXCOORD0; };
            Varyings vert(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
            {
                Drop d = _Drops[instanceId];
                float2 corner = Corners[vertexId];
                float2 from = d.motion.zw, to = d.shape.xy;
                float2 delta = to - from;
                float len = length(delta);
                Varyings o;
                if (len < 1e-3)
                {
                    o.position = float4(2, 2, 0, 1); // 멈춘 방울: 그리지 않는다.
                    o.local = 0;
                    return o;
                }
                float2 forward = delta / len;
                float2 side = float2(-forward.y, forward.x);
                float width = max(0.65, d.shape.z * 0.32) * (0.92 + 0.08 * sin(d.surface.x + _RainTime * 2.3));
                float2 pixel = lerp(from, to, corner.y * 0.5 + 0.5) + side * corner.x * width;
                o.position = ToClip(pixel);
                o.local = corner;
                return o;
            }
            float4 frag(Varyings i) : SV_Target { return float4(sqrt(saturate(1 - i.local.x * i.local.x)), 0, 0, 0); }
            ENDHLSL
        }
    }
}
