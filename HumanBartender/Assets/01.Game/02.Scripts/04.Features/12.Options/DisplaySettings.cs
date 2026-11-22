using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using VContainer.Unity;

/// <summary>
/// 16:9 화면 설정(전체화면/창 해상도/PixelPerfectCamera 채우기 방식)을 관리하는 VContainer 싱글톤.
/// 설정값은 PlayerPrefs에 저장되어 씬 간 유지된다.
/// </summary>
public class DisplaySettings : IDisplaySettings, IInitializable, ITickable, IDisposable
{
    // 조절용 수치가 아니라 창 모드와 전체화면이 공유하는 고정 화면 비율이다.
    const int AspectWidth = 16, AspectHeight = 9;
    public static readonly int[] FrameRates = { 30, 60, 120, 144, -1 };
    readonly Dictionary<UniversalAdditionalCameraData, bool> authoredPostProcessing = new();
    private static readonly Vector2Int[] _windowedSizes =
    {
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080),
    };

    public Vector2Int[] WindowedSizes => _windowedSizes;

    private PixelPerfectCamera _activePPC;
    Volume bloomVolume;
    VolumeProfile bloomProfile;
    readonly WindowState windowState = new(_windowedSizes[0]);
    bool initialized;

    public void Initialize()
    {
        int windowIndex = Mathf.Clamp(PlayerPrefs.GetInt("WinIndex", 0), 0, _windowedSizes.Length - 1);
        windowState.WindowedSize = _windowedSizes[windowIndex];
        // 기본값: 전체화면 (키가 없으면 1 = 전체화면)
        bool fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        SetFullscreen(fullscreen);
        QualitySettings.vSyncCount = PlayerPrefs.GetInt("VSync", 1) == 1 ? 1 : 0;
        Application.targetFrameRate = FrameRates[Mathf.Clamp(PlayerPrefs.GetInt("FrameRateIndex", 1), 0, FrameRates.Length - 1)];
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplySceneCameras();
        initialized = true;
    }

    public void Tick()
    {
        // 에디터의 Game 뷰 크기는 작업자가 자유롭게 바꿀 수 있게 둔다.
        if (!initialized || Application.isEditor) return;
        var screenSize = new Vector2Int(Screen.width, Screen.height);
        var display = Screen.currentResolution;
        bool fullscreen = Screen.fullScreen;
        bool correct = windowState.TryUpdate(screenSize, fullscreen,
            new Vector2Int(display.width, display.height), Time.frameCount, out var size);
        if (windowState.FullscreenChanged)
            SaveFullscreen(fullscreen);
        if (correct)
            ApplyResolution(size, fullscreen);
    }

    public void Dispose()
    {
        initialized = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        foreach (var pair in authoredPostProcessing)
            if (pair.Key != null) pair.Key.renderPostProcessing = pair.Value;
        authoredPostProcessing.Clear();
        DestroyBloomVolume();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplySceneCameras();

    void ApplySceneCameras()
    {
        var destroyed = new List<UniversalAdditionalCameraData>();
        foreach (var camera in authoredPostProcessing.Keys) if (camera == null) destroyed.Add(camera);
        foreach (var camera in destroyed) authoredPostProcessing.Remove(camera);
        foreach (var ppc in UnityEngine.Object.FindObjectsByType<PixelPerfectCamera>(FindObjectsSortMode.None))
            ApplyFill(ppc);
        foreach (var data in UnityEngine.Object.FindObjectsByType<UniversalAdditionalCameraData>(FindObjectsSortMode.None))
        {
            if (!authoredPostProcessing.ContainsKey(data)) authoredPostProcessing.Add(data, data.renderPostProcessing);
            data.renderPostProcessing = PlayerPrefs.GetInt("PostProcessing", 1) == 1 && authoredPostProcessing[data];
        }
        ApplyBloom();
    }

    public void SetEffects(GraphicsEffectOptions.Values values)
    {
        GraphicsEffectOptions.Save(values);
        ApplyBloom();
    }

    /// <summary>씬에 배치된 블룸 세기를 기준(보통)으로 배율만 덮어쓴다. 보통이면 씬 연출값을 그대로 둔다.</summary>
    void ApplyBloom()
    {
        var level = GraphicsEffectOptions.Current.Bloom;
        if (level == GraphicsEffectOptions.BloomLevel.Normal) { DestroyBloomVolume(); return; }
        float authored = 0;
        foreach (var volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (volume == bloomVolume || !volume.isActiveAndEnabled || !volume.isGlobal || volume.sharedProfile == null) continue;
            if (volume.sharedProfile.TryGet(out Bloom authoredBloom) && authoredBloom.active && authoredBloom.intensity.overrideState)
                authored = Mathf.Max(authored, authoredBloom.intensity.value);
        }
        if (bloomVolume == null)
        {
            var go = new GameObject("Bloom option volume") { hideFlags = HideFlags.HideAndDontSave };
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(go);
            bloomVolume = go.AddComponent<Volume>();
            bloomVolume.isGlobal = true;
            bloomVolume.priority = 1000;
            bloomProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            bloomProfile.hideFlags = HideFlags.HideAndDontSave;
            bloomProfile.Add<Bloom>();
            bloomVolume.sharedProfile = bloomProfile;
        }
        bloomProfile.TryGet(out Bloom bloom);
        // 블룸이 없는 씬(Home 등)에서 '강하게'를 고르면 은은한 기본 블룸을 켠다.
        bool fallback = authored <= 0 && level == GraphicsEffectOptions.BloomLevel.High;
        bloom.intensity.Override(fallback ? .5f : authored * GraphicsEffectOptions.BloomScale(level));
        bloom.threshold.overrideState = fallback;
        bloom.threshold.value = .9f;
    }

    void DestroyBloomVolume()
    {
        if (bloomVolume != null) Destroy(bloomVolume.gameObject);
        if (bloomProfile != null) Destroy(bloomProfile);
        bloomVolume = null; bloomProfile = null;
    }

    static void Destroy(UnityEngine.Object target)
    {
        if (Application.isPlaying) UnityEngine.Object.Destroy(target);
        else UnityEngine.Object.DestroyImmediate(target);
    }

    public void SetVSync(bool enabled)
    {
        QualitySettings.vSyncCount = enabled ? 1 : 0;
        PlayerPrefs.SetInt("VSync", enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetFrameRate(int index)
    {
        index = Mathf.Clamp(index, 0, FrameRates.Length - 1);
        Application.targetFrameRate = FrameRates[index];
        PlayerPrefs.SetInt("FrameRateIndex", index);
        PlayerPrefs.Save();
    }

    public void SetPostProcessing(bool enabled)
    {
        PlayerPrefs.SetInt("PostProcessing", enabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplySceneCameras();
    }

    public void SetFullscreen(bool fullscreen)
    {
        if (fullscreen)
        {
            var r = Screen.currentResolution;
            var s = GetFullscreenSize(r.width, r.height);
            // 게임과 Overlay UI를 함께 16:9로 렌더링하고, 모니터의 남는 영역은 Unity가 검게 채운다.
            ApplyResolution(s, true);
        }
        else
        {
            ApplyResolution(windowState.WindowedSize, false);
        }
        SaveFullscreen(fullscreen);
    }

    void ApplyResolution(Vector2Int size, bool fullscreen)
    {
        windowState.RecordRequest(size, fullscreen, Time.frameCount);
        Screen.SetResolution(size.x, size.y, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    static void SaveFullscreen(bool fullscreen)
    {
        PlayerPrefs.SetInt("Fullscreen", fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>모니터 안에 들어가는 가장 큰 정수 픽셀 16:9 렌더링 해상도.</summary>
    public static Vector2Int GetFullscreenSize(int displayWidth, int displayHeight)
    {
        int scale = Mathf.Max(1, Mathf.Min(displayWidth / AspectWidth, displayHeight / AspectHeight));
        return new Vector2Int(AspectWidth * scale, AspectHeight * scale);
    }

    /// <summary>드래그한 축의 크기를 기준으로 창을 16:9에 맞춘다. 반올림하여 보정할 때마다 작아지지 않게 한다.</summary>
    public static Vector2Int GetResizedWindowSize(Vector2Int size, Vector2Int previousSize)
    {
        bool widthChangedMore = Mathf.Abs(size.x - previousSize.x) * AspectHeight >= Mathf.Abs(size.y - previousSize.y) * AspectWidth;
        int scale = Mathf.Max(1, Mathf.RoundToInt(widthChangedMore ? size.x / (float)AspectWidth : size.y / (float)AspectHeight));
        return new Vector2Int(scale * AspectWidth, scale * AspectHeight);
    }

    public void SetWindowedSize(int index)
    {
        index = Mathf.Clamp(index, 0, _windowedSizes.Length - 1);
        PlayerPrefs.SetInt("WinIndex", index);
        PlayerPrefs.Save();
        windowState.WindowedSize = _windowedSizes[index];
        if (!Screen.fullScreen)
        {
            ApplyResolution(windowState.WindowedSize, false);
        }
    }

    public void SetFillMode(bool stretchToFill)
    {
        PlayerPrefs.SetInt("FillStretch", stretchToFill ? 1 : 0);
        PlayerPrefs.Save();
        ApplyFillToActive();   // 등록된 PPC에 즉시 반영
        ApplySceneCameras();
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
        if (_activePPC != null) ApplyFill(_activePPC);
    }

    static void ApplyFill(PixelPerfectCamera camera)
    {
        bool stretch = PlayerPrefs.GetInt("FillStretch", 1) == 1;   // 기본 StretchFill
        camera.cropFrame = stretch
            ? PixelPerfectCamera.CropFrame.StretchFill
            : PixelPerfectCamera.CropFrame.Windowbox;
    }

    /// <summary>OS 창 조절과 Alt+Enter를 감지한다. Unity API 호출과 분리해 전환 순서를 EditMode에서 검증한다.</summary>
    public sealed class WindowState
    {
        public Vector2Int WindowedSize { get; set; }
        public bool FullscreenChanged { get; private set; }
        Vector2Int observedSize;
        bool observedFullscreen, hasObservation, resizePending;
        int requestFrame = -1;

        public WindowState(Vector2Int windowedSize) => WindowedSize = windowedSize;

        public void RecordRequest(Vector2Int size, bool fullscreen, int frame)
        {
            if (!fullscreen) WindowedSize = size;
            requestFrame = frame;
            resizePending = false;
        }

        public bool TryUpdate(Vector2Int size, bool fullscreen, Vector2Int displaySize, int frame, out Vector2Int correctedSize)
        {
            correctedSize = size;
            FullscreenChanged = false;
            if (size.x <= 0 || size.y <= 0) return false;
            // SetResolution은 프레임 끝에 적용된다. 우리 요청을 OS의 새 조작으로 오인하지 않는다.
            if (requestFrame >= 0)
            {
                if (frame <= requestFrame) return false;
                requestFrame = -1;
                observedSize = size;
                observedFullscreen = fullscreen;
                hasObservation = true;
                return false;
            }
            if (!hasObservation)
            {
                observedSize = size;
                observedFullscreen = fullscreen;
                hasObservation = true;
                return false;
            }
            if (fullscreen != observedFullscreen)
            {
                observedFullscreen = fullscreen;
                observedSize = size;
                resizePending = false;
                FullscreenChanged = true;
                correctedSize = fullscreen ? GetFullscreenSize(displaySize.x, displaySize.y) : WindowedSize;
                return true;
            }
            if (fullscreen)
            {
                if (size == observedSize) return false;
                observedSize = size;
                correctedSize = GetFullscreenSize(displaySize.x, displaySize.y);
                return correctedSize != size;
            }
            if (size != observedSize)
            {
                observedSize = size;
                resizePending = true;
                return false;
            }
            // 드래그 중 매 프레임 창 크기를 다시 쓰지 않고, 크기가 안정된 다음 보정한다.
            if (!resizePending) return false;
            resizePending = false;
            correctedSize = GetResizedWindowSize(size, WindowedSize);
            WindowedSize = correctedSize;
            return correctedSize != size;
        }
    }
}
