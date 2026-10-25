using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using VContainer.Unity;

/// <summary>
/// 화면 설정(전체화면/창 해상도/PixelPerfectCamera 채우기 방식)을 관리하는 VContainer 싱글톤.
/// 설정값은 PlayerPrefs에 저장되어 씬 간 유지된다.
/// </summary>
public class DisplaySettings : IDisplaySettings, IInitializable, IDisposable
{
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

    public void Initialize()
    {
        // 기본값: 전체화면 (키가 없으면 1 = 전체화면)
        bool fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        SetFullscreen(fullscreen);
        QualitySettings.vSyncCount = PlayerPrefs.GetInt("VSync", 1) == 1 ? 1 : 0;
        Application.targetFrameRate = FrameRates[Mathf.Clamp(PlayerPrefs.GetInt("FrameRateIndex", 1), 0, FrameRates.Length - 1)];
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplySceneCameras();
    }

    public void Dispose()
    {
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
}
