using UnityEngine;
using UnityEngine.Rendering.Universal;
using VContainer.Unity;

/// <summary>
/// 화면 설정(전체화면/창 해상도/PixelPerfectCamera 채우기 방식)을 관리하는 VContainer 싱글톤.
/// 설정값은 PlayerPrefs에 저장되어 씬 간 유지된다.
/// </summary>
public class DisplaySettings : IDisplaySettings, IInitializable
{
    private static readonly Vector2Int[] _windowedSizes =
    {
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080),
    };

    public Vector2Int[] WindowedSizes => _windowedSizes;

    private PixelPerfectCamera _activePPC;

    public void Initialize()
    {
        // 기본값: 전체화면 (키가 없으면 1 = 전체화면)
        bool fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        SetFullscreen(fullscreen);
    }

    public void SetFullscreen(bool fullscreen)
    {
        if (fullscreen)
        {
            var r = Screen.currentResolution;
            Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
        }
        else
        {
            int i = Mathf.Clamp(PlayerPrefs.GetInt("WinIndex", 0), 0, _windowedSizes.Length - 1);
            var s = _windowedSizes[i];
            Screen.SetResolution(s.x, s.y, FullScreenMode.Windowed);
        }
        PlayerPrefs.SetInt("Fullscreen", fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetWindowedSize(int index)
    {
        index = Mathf.Clamp(index, 0, _windowedSizes.Length - 1);
        PlayerPrefs.SetInt("WinIndex", index);
        PlayerPrefs.Save();
        if (!Screen.fullScreen)
        {
            var s = _windowedSizes[index];
            Screen.SetResolution(s.x, s.y, FullScreenMode.Windowed);
        }
    }

    public void SetFillMode(bool stretchToFill)
    {
        PlayerPrefs.SetInt("FillStretch", stretchToFill ? 1 : 0);
        PlayerPrefs.Save();
        ApplyFillToActive();   // 등록된 PPC에 즉시 반영
    }

    public void RegisterActiveCamera(PixelPerfectCamera ppc)
    {
        if (ppc == null) return;
        _activePPC = ppc;
        ApplyFillToActive();   // 씬 진입 시 새 PPC에 현재 설정 반영
    }

    public void UnregisterActiveCamera(PixelPerfectCamera ppc)
    {
        // 다른 PPC가 이미 등록됐다면 덮어쓰지 않음
        if (_activePPC == ppc) _activePPC = null;
    }

    private void ApplyFillToActive()
    {
        if (_activePPC == null) return;
        bool stretch = PlayerPrefs.GetInt("FillStretch", 1) == 1;   // 기본 StretchFill
        _activePPC.cropFrame = stretch
            ? PixelPerfectCamera.CropFrame.StretchFill
            : PixelPerfectCamera.CropFrame.Windowbox;
    }
}