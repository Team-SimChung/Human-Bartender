using UnityEngine;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// 불 주변을 비추는 2D 라이트의 밝기와 반경을 펄린 노이즈로 일렁이게 하는 컴포넌트.
/// 느린 일렁임과 빠른 떨림을 섞고, 결과를 초당 Steps Per Second 단계로 끊어 도트 애니메이션과 박자를 맞춘다.
/// 에디터에서도 플레이 없이 미리 볼 수 있으며, 씬을 저장할 때는 원래 값으로 되돌린 뒤 저장한다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Light2D))]
public sealed class FireLightFlicker : MonoBehaviour
{
    [Tooltip("플레이하지 않아도 에디터 씬 뷰에서 일렁임을 보여 준다.")]
    [SerializeField] private bool previewInEditMode = true;

    [Header("밝기")]
    [Tooltip("밝기가 기준값에서 위아래로 흔들리는 비율 (0.3 = ±30%)")]
    [Range(0f, 1f)] [SerializeField] private float intensityAmount = 0.3f;
    [Tooltip("느린 일렁임 속도")]
    [Min(0f)] [SerializeField] private float slowSpeed = 1.3f;
    [Tooltip("빠른 떨림 속도")]
    [Min(0f)] [SerializeField] private float fastSpeed = 7f;
    [Tooltip("빠른 떨림이 섞이는 비율")]
    [Range(0f, 1f)] [SerializeField] private float fastMix = 0.35f;

    [Header("반경")]
    [Tooltip("포인트 라이트 바깥 반경이 흔들리는 비율 (0 = 고정)")]
    [Range(0f, 0.5f)] [SerializeField] private float radiusAmount = 0.08f;

    [Header("도트 박자")]
    [Tooltip("값을 바꾸는 횟수 (초당). 불 셰이더의 Anim_Fps와 맞추면 박자가 같다. 0이면 매 프레임")]
    [Min(0f)] [SerializeField] private float stepsPerSecond = 16f;

    private Light2D _light;
    private float _baseIntensity;
    private float _baseRadius;
    private float _appliedIntensity;
    private float _appliedRadius;
    private float _seed;
    private bool _captured;

    private void OnEnable()
    {
        _light = GetComponent<Light2D>();
        Capture();
        _seed = Random.Range(0f, 100f);
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
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
        Restore();
    }

    private void Update()
    {
        if (Application.isPlaying) Apply(Time.time);
    }

    private void Capture()
    {
        if (_light == null) return;
        _baseIntensity = _appliedIntensity = _light.intensity;
        _baseRadius = _appliedRadius = _light.pointLightOuterRadius;
        _captured = true;
    }

    // 이 컴포넌트가 마지막으로 넣은 값과 지금 값이 다르면 누군가(인스펙터, 다른 스크립트) 바꾼 것이므로 그 값을 새 기준으로 삼는다.
    private void SyncBase()
    {
        if (!_captured || _light == null) return;
        if (!Mathf.Approximately(_light.intensity, _appliedIntensity)) _baseIntensity = _appliedIntensity = _light.intensity;
        if (!Mathf.Approximately(_light.pointLightOuterRadius, _appliedRadius)) _baseRadius = _appliedRadius = _light.pointLightOuterRadius;
    }

    private void Restore()
    {
        if (!_captured || _light == null) return;
        SyncBase();
        _light.intensity = _baseIntensity;
        _light.pointLightOuterRadius = _baseRadius;
        _appliedIntensity = _baseIntensity;
        _appliedRadius = _baseRadius;
    }

    private void Apply(float time)
    {
        if (!_captured || _light == null) return;
        if (stepsPerSecond > 0f) time = Mathf.Floor(time * stepsPerSecond) / stepsPerSecond;

        float slow = Mathf.PerlinNoise(_seed, time * slowSpeed) * 2f - 1f;
        float fast = Mathf.PerlinNoise(_seed + 37.1f, time * fastSpeed) * 2f - 1f;
        float n = Mathf.Lerp(slow, fast, fastMix);

        _appliedIntensity = Mathf.Max(0f, _baseIntensity * (1f + n * intensityAmount));
        _appliedRadius = Mathf.Max(0.001f, _baseRadius * (1f + n * radiusAmount));
        _light.intensity = _appliedIntensity;
        _light.pointLightOuterRadius = _appliedRadius;
    }

#if UNITY_EDITOR
    private bool _previewRunning;

    private void EditorTick()
    {
        if (this == null || Application.isPlaying || _light == null) return;
        if (!previewInEditMode || !isActiveAndEnabled)
        {
            if (_previewRunning) { Restore(); _previewRunning = false; SceneView.RepaintAll(); }
            return;
        }
        // 인스펙터에서 직접 고친 값은 새 기준값으로 삼는다.
        SyncBase();

        float before = _appliedIntensity;
        Apply((float)EditorApplication.timeSinceStartup);
        _previewRunning = true;
        if (!Mathf.Approximately(before, _appliedIntensity))
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }

    // 일렁이는 중간 값이 씬에 저장되지 않게, 저장 직전에 원래 값으로 되돌린다.
    private void OnSceneSaving(Scene scene, string path)
    {
        if (this == null || scene != gameObject.scene) return;
        Restore();
    }
#endif
}
