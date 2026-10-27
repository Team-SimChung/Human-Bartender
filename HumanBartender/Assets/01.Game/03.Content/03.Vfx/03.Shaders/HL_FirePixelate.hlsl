// fire.shadergraph용 도트 격자 함수 (Custom Function 노드에서 File 방식으로 사용).
// 씬 도트 격자(Pixel Perfect Camera Asset PPU 100 → 한 도트 0.01유닛)에 이펙트를 맞춘다.
//
// FirePixelUV    : 각 화면 픽셀의 월드 위치를 도트 칸 중심으로 옮기고, 그 칸 중심에 해당하는 UV를 화면 미분(ddx/ddy)으로 역산한다.
//                  셰이더가 쓰는 모든 UV를 이 값으로 바꾸면 무늬, 마스크, 흐름이 전부 월드 도트 칸 단위로 끊긴다.
// FireSteppedTime: 시간을 초당 Fps 단계로 끊어 흐름이 도트 애니메이션처럼 칸칸이 넘어가게 한다. (Fps <= 0이면 끔)
// FireSnapVertex : 오브젝트 원점을 도트 격자에 맞춰 서브픽셀 이동으로 무늬가 미끄러지지 않게 한다. (정점 단계)

#ifndef HL_FIRE_PIXELATE_INCLUDED
#define HL_FIRE_PIXELATE_INCLUDED

// 화면 1픽셀 이동량 W(월드)와 J(UV)로, 월드 차이 dWs만큼 이동했을 때의 UV 차이를 구한다.
float2 FirePixel_UVDelta(float2 uv, float2 wx, float2 wy, float2 dWs, float det)
{
    // W * s = dWs 를 풀어 화면 이동량 s를 구하고, J * s 로 UV 이동량을 만든다.
    float2 s = float2(wy.y * dWs.x - wy.x * dWs.y, -wx.y * dWs.x + wx.x * dWs.y) / det;
    return ddx(uv) * s.x + ddy(uv) * s.y;
}

void FirePixelUV_float(float4 UV0, float4 UV1, float3 PositionWS, float PixelSize, float Enable,
                       out float4 OutUV0, out float4 OutUV1)
{
    OutUV0 = UV0;
    OutUV1 = UV1;
#if !defined(SHADERGRAPH_PREVIEW)
    if (Enable < 0.5 || PixelSize <= 0.0) return;

    float2 ws = PositionWS.xy;
    float2 cell = (floor(ws / PixelSize) + 0.5) * PixelSize;
    float2 dWs = cell - ws;

    float2 wx = ddx(ws);
    float2 wy = ddy(ws);
    float det = wx.x * wy.y - wy.x * wx.y;
    if (abs(det) < 1e-12) return;

    OutUV0.xy = UV0.xy + FirePixel_UVDelta(UV0.xy, wx, wy, dWs, det);
    OutUV1.xy = UV1.xy + FirePixel_UVDelta(UV1.xy, wx, wy, dWs, det);
#endif
}

void FirePixelUV_half(half4 UV0, half4 UV1, half3 PositionWS, half PixelSize, half Enable,
                      out half4 OutUV0, out half4 OutUV1)
{
    float4 a, b;
    FirePixelUV_float(UV0, UV1, PositionWS, PixelSize, Enable, a, b);
    OutUV0 = a;
    OutUV1 = b;
}

void FireSteppedTime_float(float Fps, out float Time)
{
    float t = _TimeParameters.x;
    Time = Fps > 0.0 ? floor(t * Fps) / Fps : t;
}

void FireSteppedTime_half(half Fps, out half Time)
{
    float t;
    FireSteppedTime_float(Fps, t);
    Time = t;
}

void FireSnapVertex_float(float3 PositionOS, float PixelSize, float Enable, out float3 OutPositionOS)
{
    OutPositionOS = PositionOS;
#if !defined(SHADERGRAPH_PREVIEW)
    if (Enable < 0.5 || PixelSize <= 0.0) return;

    float3 originWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
    float3 snapped = originWS;
    snapped.xy = round(originWS.xy / PixelSize) * PixelSize;
    float3 positionWS = TransformObjectToWorld(PositionOS) + (snapped - originWS);
    OutPositionOS = TransformWorldToObject(positionWS);
#endif
}

void FireSnapVertex_half(half3 PositionOS, half PixelSize, half Enable, out half3 OutPositionOS)
{
    float3 p;
    FireSnapVertex_float(PositionOS, PixelSize, Enable, p);
    OutPositionOS = p;
}

#endif
