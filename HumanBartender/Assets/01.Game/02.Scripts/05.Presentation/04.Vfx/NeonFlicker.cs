using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// 네온 간판의 2D 라이트를 불규칙하게 껐다 켜는 컴포넌트.
/// 그룹(간판)마다 따로 움직이며, 평소에는 켜져 있다가 가끔 빠르게 몇 번 깜빡이고, 그 뒤 가끔은 잠깐 꺼져 있다가 다시 켜진다.
/// - lights: 간판 자체의 빛. 꺼지면 함께 꺼진다.
/// - spillLights: 간판 주변 벽과 바닥에 번지는 빛. 꺼질 때 spillAmount만큼 약해진다.
///   여러 간판이 같은 번짐 라이트를 나눠 쓰면 각 간판의 영향이 곱해진다.
/// - dimRenderer / dimChannel: 텍스처에 그려진 간판 불빛을 _NeonDimMask 채널만큼 함께 어둡게 한다 (SH_LedSignScroll).
/// 에디터에서도 플레이 없이 미리 볼 수 있으며(Preview In Edit Mode), 씬을 저장할 때는 원래 밝기로 되돌린 뒤 저장한다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class NeonFlicker : MonoBehaviour
{
    [Serializable]
    public sealed class Group
    {
        [Tooltip("인스펙터에서 구분하기 위한 이름")]
        public string name;
        [Tooltip("간판 자체의 빛. 꺼지면 함께 꺼진다.")]
        public Light2D[] lights = Array.Empty<Light2D>();
        [Tooltip("간판 주변에 번지는 빛. 꺼질 때 아래 비율만큼 약해진다.")]
        public Light2D[] spillLights = Array.Empty<Light2D>();
        [Tooltip("간판이 꺼질 때 주변 번짐 빛이 줄어드는 비율 (1 = 완전히 꺼짐)")]
        [Range(0f, 1f)] public float spillAmount = 1f;
        [Tooltip("간판이 그려진 렌더러. 꺼질 때 이 렌더러 머티리얼의 _NeonDimMask 채널 픽셀을 함께 어둡게 한다 (SH_LedSignScroll).")]
        public Renderer dimRenderer;
        [Tooltip("_NeonDimMask에서 이 간판이 쓰는 채널 (0 = R, 1 = G, 2 = B, 3 = A, -1 = 텍스처는 어둡게 하지 않음)")]
        [Range(-1, 3)] public int dimChannel = -1;
        [Tooltip("이 간판만 더 자주(>1) 또는 더 드물게(<1) 깜빡이게 하는 배율. 0이면 깜빡이지 않는다.")]
        [Range(0f, 5f)] public float frequencyScale = 1f;

        [NonSerialized] internal Phase phase;
        [NonSerialized] internal bool isOn = true;
        [NonSerialized] internal float timer;
        [NonSerialized] internal int flickersLeft;
    }

    internal enum Phase { Stable, Burst, LongOff }

    private sealed class LightState
    {
        public float baseIntensity;
        public float applied;
    }

    [SerializeField] private Group[] groups = Array.Empty<Group>();

    [Tooltip("플레이하지 않아도 에디터 씬 뷰에서 깜빡임을 보여 준다. 라이트 밝기를 고칠 때는 꺼 두는 것이 편하다.")]
    [SerializeField] private bool previewInEditMode = true;

    [Header("빈도")]
    [Tooltip("간판 하나가 1분 동안 평균 몇 번 깜빡일지. 클수록 자주 깜빡인다. (그룹별 배율이 곱해진다)")]
    [Min(0f)] [SerializeField] private float burstsPerMinute = 10f;
    [Tooltip("깜빡임 간격의 랜덤 정도. 0 = 매번 같은 간격, 1 = 평균의 0배~2배 사이에서 들쭉날쭉")]
    [Range(0f, 1f)] [SerializeField] private float intervalRandomness = 0.7f;

    [Header("깜빡임")]
    [Tooltip("한 번 깜빡일 때 꺼졌다 켜지는 횟수 범위")]
    [SerializeField] private Vector2Int flickersPerBurst = new Vector2Int(2, 6);
    [Tooltip("깜빡이는 동안 한 번 꺼져 있는 시간 범위 (초)")]
    [SerializeField] private Vector2 flickerOffSeconds = new Vector2(0.03f, 0.14f);
    [Tooltip("깜빡이는 동안 한 번 켜져 있는 시간 범위 (초)")]
    [SerializeField] private Vector2 flickerOnSeconds = new Vector2(0.03f, 0.2f);

    [Header("길게 꺼짐")]
    [Tooltip("깜빡임이 끝난 뒤 한동안 꺼져 있을 확률")]
    [Range(0f, 1f)] [SerializeField] private float longOffChance = 0.25f;
    [Tooltip("길게 꺼져 있는 시간 범위 (초)")]
    [SerializeField] private Vector2 longOffSeconds = new Vector2(0.6f, 3f);

    [Header("밝기")]
    [Tooltip("꺼졌을 때 남는 밝기 (0 = 완전히 꺼짐)")]
    [Range(0f, 1f)] [SerializeField] private float offBrightness = 0f;

    private static readonly int NeonDimId = Shader.PropertyToID("_NeonDim");
    private MaterialPropertyBlock _block;
    private readonly Dictionary<Light2D, LightState> _lights = new Dictionary<Light2D, LightState>();
    private readonly Dictionary<Renderer, Vector4> _dimValues = new Dictionary<Renderer, Vector4>();
    private bool _initialized;

    private void OnEnable()
    {
        Initialize();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            _lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorTick;
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
#endif
        RestoreAll();
        _initialized = false;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        Step(Time.deltaTime);
    }

    private void Initialize()
    {
        _lights.Clear();
        _dimValues.Clear();
        foreach (var group in groups)
        {
            if (group == null) continue;
            group.lights ??= Array.Empty<Light2D>();
            group.spillLights ??= Array.Empty<Light2D>();
            Register(group.lights);
            Register(group.spillLights);

            // 그룹마다 첫 깜빡임 시점을 흩어 놓아 동시에 깜빡이지 않게 한다.
            group.phase = Phase.Stable;
            group.isOn = true;
            group.flickersLeft = 0;
            float firstWait = StableSeconds(group);
            group.timer = float.IsPositiveInfinity(firstWait) ? firstWait : UnityEngine.Random.Range(0f, 1f) * firstWait;
        }
        _initialized = true;
        ApplyAll();
    }

    private void Register(Light2D[] list)
    {
        foreach (var light in list)
        {
            if (light == null || _lights.ContainsKey(light)) continue;
            _lights[light] = new LightState { baseIntensity = light.intensity, applied = light.intensity };
        }
    }

    // 모든 그룹을 켠 상태(원래 밝기)로 되돌린다.
    private void RestoreAll()
    {
        if (!_initialized) return;
        foreach (var group in groups)
        {
            if (group == null) continue;
            group.isOn = true;
            group.phase = Phase.Stable;
            group.flickersLeft = 0;
        }
        ApplyAll();
    }

    private bool Step(float dt)
    {
        if (!_initialized) return false;
        bool changed = false;
        foreach (var group in groups)
        {
            if (group == null) continue;
            group.timer -= dt;
            if (group.timer > 0f) continue;
            Advance(group);
            changed = true;
        }
        if (changed) ApplyAll();
        return changed;
    }

    private void Advance(Group group)
    {
        switch (group.phase)
        {
            case Phase.Stable:
                // 안정적으로 켜져 있던 시간이 끝나면 깜빡이기 시작한다.
                group.phase = Phase.Burst;
                group.flickersLeft = UnityEngine.Random.Range(Mathf.Max(1, flickersPerBurst.x), Mathf.Max(flickersPerBurst.x, flickersPerBurst.y) + 1);
                group.isOn = false;
                group.timer = RandomIn(flickerOffSeconds);
                break;

            case Phase.Burst:
                if (!group.isOn)
                {
                    group.isOn = true;
                    group.flickersLeft--;
                    group.timer = RandomIn(flickerOnSeconds);
                }
                else if (group.flickersLeft > 0)
                {
                    group.isOn = false;
                    group.timer = RandomIn(flickerOffSeconds);
                }
                else if (UnityEngine.Random.value < longOffChance)
                {
                    // 깜빡임이 끝난 뒤 가끔 한동안 꺼져 있는다.
                    group.phase = Phase.LongOff;
                    group.isOn = false;
                    group.timer = RandomIn(longOffSeconds);
                }
                else
                {
                    group.phase = Phase.Stable;
                    group.isOn = true;
                    group.timer = StableSeconds(group);
                }
                break;

            case Phase.LongOff:
                group.phase = Phase.Stable;
                group.isOn = true;
                group.timer = StableSeconds(group);
                break;
        }
    }

    // 라이트마다 자신을 쓰는 모든 그룹의 영향을 곱해 최종 밝기를 정하고, 텍스처 어둡게 하기 값도 갱신한다.
    private void ApplyAll()
    {
        foreach (var pair in _lights)
        {
            var light = pair.Key;
            if (light == null) continue;
            float multiplier = 1f;
            foreach (var group in groups)
            {
                if (group == null || group.isOn) continue;
                if (Array.IndexOf(group.lights, light) >= 0) multiplier *= offBrightness;
                else if (Array.IndexOf(group.spillLights, light) >= 0) multiplier *= 1f - group.spillAmount * (1f - offBrightness);
            }
            float value = pair.Value.baseIntensity * multiplier;
            light.intensity = value;
            pair.Value.applied = value;
        }

        foreach (var group in groups)
            if (group != null) ApplyDim(group, group.isOn ? 0f : 1f - offBrightness);
    }

    // 그룹이 꺼져 있으면 렌더러의 _NeonDim 해당 채널을 올려 텍스처 속 간판 픽셀도 어둡게 한다.
    private void ApplyDim(Group group, float dim)
    {
        var renderer = group.dimRenderer;
        if (renderer == null || group.dimChannel < 0 || group.dimChannel > 3) return;
        bool known = _dimValues.TryGetValue(renderer, out var value);
        if (known && Mathf.Approximately(value[group.dimChannel], dim)) return;
        value[group.dimChannel] = dim;
        _dimValues[renderer] = value;
        _block ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(_block);
        _block.SetVector(NeonDimId, value);
        renderer.SetPropertyBlock(_block);
    }

    // 다음 깜빡임까지 켜져 있는 시간. 평균 = 60 / (분당 횟수 x 그룹 배율), 랜덤 정도만큼 평균 주변으로 흩어진다.
    private float StableSeconds(Group group)
    {
        float rate = burstsPerMinute * Mathf.Max(0f, group.frequencyScale);
        if (rate <= 0.0001f) return float.PositiveInfinity;
        float mean = 60f / rate;
        float spread = Mathf.Clamp01(intervalRandomness);
        return Mathf.Max(0.05f, mean * UnityEngine.Random.Range(1f - spread, 1f + spread));
    }

    private static float RandomIn(Vector2 range) => UnityEngine.Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));

#if UNITY_EDITOR
    private double _lastEditorTime;
    private bool _previewRunning;

    // 플레이하지 않을 때 에디터 시간으로 깜빡임을 돌린다.
    private void EditorTick()
    {
        if (this == null || Application.isPlaying) return;

        if (!previewInEditMode || !isActiveAndEnabled)
        {
            if (_previewRunning) { RestoreAll(); _previewRunning = false; RepaintViews(); }
            _lastEditorTime = EditorApplication.timeSinceStartup;
            return;
        }
        if (!_initialized) Initialize();

        // 인스펙터에서 라이트 밝기를 직접 고치면 그 값을 새 기준 밝기로 삼는다.
        foreach (var pair in _lights)
        {
            if (pair.Key != null && !Mathf.Approximately(pair.Key.intensity, pair.Value.applied))
            {
                pair.Value.baseIntensity = pair.Key.intensity;
                pair.Value.applied = pair.Key.intensity;
            }
        }

        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Min((float)(now - _lastEditorTime), 0.1f);
        _lastEditorTime = now;
        _previewRunning = true;
        if (Step(dt)) RepaintViews();
    }

    // 꺼진 순간에 저장해도 꺼진 밝기가 씬에 저장되지 않게, 저장 직전에 원래 밝기로 되돌린다.
    private void OnSceneSaving(Scene scene, string path)
    {
        if (this == null || scene != gameObject.scene) return;
        RestoreAll();
    }

    private static void RepaintViews()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    private void OnValidate()
    {
        // 그룹 구성이나 설정이 바뀌면 원래 밝기로 되돌린 뒤 다시 초기화한다.
        if (Application.isPlaying) return;
        EditorApplication.delayCall += () =>
        {
            if (this == null || !isActiveAndEnabled) return;
            RestoreAll();
            Initialize();
        };
    }
#endif
}
