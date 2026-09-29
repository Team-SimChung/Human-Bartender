using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>화면 설정(전체화면/창모드 크기/채우기 방식, PixelPerfectCamera 등록)을 관리하는 인터페이스.</summary>
public interface IDisplaySettings
{
    Vector2Int[] WindowedSizes { get; }
    void SetFullscreen(bool fullscreen);
    void SetWindowedSize(int index);
    void SetFillMode(bool stretchToFill);

    void RegisterActiveCamera(PixelPerfectCamera ppc);
    void UnregisterActiveCamera(PixelPerfectCamera ppc);
}
