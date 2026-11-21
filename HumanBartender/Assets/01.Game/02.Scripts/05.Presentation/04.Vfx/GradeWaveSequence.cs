using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// 부모 파티클의 시간으로 GradeWave 셰이더와 교체 가능한 등급 글자를 함께 재생한다.
/// 부모는 3.4초, 반복 없음, 방출 없음으로 두고 자식 파티클을 함께 Play한다.
/// 글자의 위치와 크기는 유지하고 0.44~0.56초에 불투명도만 빠르게 올린다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(ParticleSystem))]
[DisallowMultipleComponent]
public sealed class GradeWaveSequence : MonoBehaviour
{
    public const float SequenceDuration = 3.4f;

    private static readonly int SequenceTimeId = Shader.PropertyToID("_SequenceTime");

    [Header("연출 대상")]
    [Tooltip("교체할 등급 글자의 부모. UI(TMP, Image), SpriteRenderer, TextMeshPro(3D)를 지원한다.")]
    [SerializeField] private Transform textSlot;

    [Tooltip("Custom/VFX/SH_GradeWave를 사용하는 자식 파티클 렌더러. 공유 머티리얼 대신 렌더러별 시간만 바꾼다.")]
    [SerializeField] private ParticleSystemRenderer[] effectRenderers = System.Array.Empty<ParticleSystemRenderer>();

    [Header("글자 등장")]
    [Tooltip("부모 파티클 재생 후 글자가 나타나기 시작하는 시간(초).")]
    [Min(0f)] [SerializeField] private float revealDelay = 0.44f;

    [Tooltip("글자의 불투명도가 원래 값까지 올라오는 시간(초). 위치와 크기는 바꾸지 않는다.")]
    [Min(0f)] [SerializeField] private float revealDuration = 0.12f;

    [Tooltip("씬 뷰 Particle Effect의 Play, Pause, 시간 이동에 맞춰 연출을 미리 본다.")]
    [SerializeField] private bool previewInEditMode = true;

    private ParticleSystem _timer;
    private Transform _cachedTextSlot;
    private ParticleSystemRenderer[] _cachedEffectRenderers;
    private bool _targetsDirty = true;
    private bool _applied;
    private float _lastSequenceTime = -1f;
    private bool _manualPreview;
    private float _manualPreviewTime;

    private readonly List<CanvasRenderer> _canvasTargets = new List<CanvasRenderer>();
    private readonly List<SpriteRenderer> _spriteTargets = new List<SpriteRenderer>();
    private readonly List<TextMeshPro> _textTargets = new List<TextMeshPro>();
    private float[] _canvasAlpha = System.Array.Empty<float>();
    private Color[] _spriteColors = System.Array.Empty<Color>();
    private Color[] _textColors = System.Array.Empty<Color>();
    private RendererState[] _rendererStates = System.Array.Empty<RendererState>();

#if UNITY_EDITOR
    private bool _saving;
#endif

    private sealed class RendererState
    {
        public readonly ParticleSystemRenderer Renderer;
        public readonly MaterialPropertyBlock Original = new MaterialPropertyBlock();
        public readonly MaterialPropertyBlock Preview = new MaterialPropertyBlock();
        public bool OriginalWasEmpty;

        public RendererState(ParticleSystemRenderer renderer)
        {
            Renderer = renderer;
        }
    }

    /// <summary>글자를 교체할 자리. 자식 교체 후 RefreshTextTargets를 호출한다.</summary>
    public Transform TextSlot => textSlot;

    /// <summary>슬롯과 이펙트 대상을 연결한다. 기존 대상의 프리뷰 값은 먼저 복원한다.</summary>
    public void Configure(Transform slot, ParticleSystemRenderer[] renderers)
    {
        if (!CanUseInstance()) return;
        Restore();
        textSlot = slot;
        effectRenderers = renderers ?? System.Array.Empty<ParticleSystemRenderer>();
        _targetsDirty = true;
        CacheTargets();
    }

    /// <summary>슬롯 안의 UI나 글자를 교체했을 때 캐시를 갱신한다.</summary>
    public void RefreshTextTargets()
    {
        if (!CanUseInstance()) return;
        Restore();
        _targetsDirty = true;
        CacheTargets();
        Evaluate();
    }

    /// <summary>부모와 모든 자식 파티클을 처음부터 재생한다.</summary>
    public void Play()
    {
        if (!CanUseInstance()) return;
        _manualPreview = false;
        var timer = Timer;
        if (timer == null) return;
        timer.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Restore();
        timer.Play(true);
        Evaluate();
    }

    /// <summary>모든 파티클을 멈추고 글자 및 렌더러 속성을 원래대로 복원한다.</summary>
    public void Stop()
    {
        if (!CanUseInstance()) return;
        _manualPreview = false;
        var timer = Timer;
        if (timer != null) timer.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        RestoreAndRepaint();
    }

    /// <summary>
    /// 결정적인 캡처용 시간 지정. 파티클 자체는 호출자가 Simulate한다.
    /// active=false 또는 Play/Stop 호출로 시간 지정을 해제하고 원래 상태로 돌아간다.
    /// </summary>
    public void EvaluatePreview(float time, bool active)
    {
        if (!CanUseInstance()) return;
        _manualPreview = active;
        _manualPreviewTime = Mathf.Clamp(time, 0f, SequenceDuration);
        if (active) Evaluate();
        else Restore();
#if UNITY_EDITOR
        if (!Application.isPlaying) SceneView.RepaintAll();
#endif
    }

    private ParticleSystem Timer => _timer != null ? _timer : (_timer = GetComponent<ParticleSystem>());

    private bool CanUseInstance()
    {
#if UNITY_EDITOR
        // 프리팹 임포트 중에는 에셋의 색상이나 파티클 상태를 절대 변경하지 않는다.
        if (EditorUtility.IsPersistent(this) || EditorUtility.IsPersistent(gameObject)) return false;
#endif
        return true;
    }

    private void Reset()
    {
        if (!CanUseInstance()) return;
        var slot = transform.Find("GradeText_Slot");
        if (slot != null) textSlot = slot;
        _targetsDirty = true;
    }

    private void OnEnable()
    {
        if (!CanUseInstance()) return;
        _timer = GetComponent<ParticleSystem>();
        _targetsDirty = true;
        CacheTargets();
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
        EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
        EditorApplication.hierarchyChanged += OnEditorHierarchyChanged;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        PrefabStage.prefabSaving -= OnPrefabSaving;
        PrefabStage.prefabSaving += OnPrefabSaving;
#endif
        Evaluate();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
        EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        PrefabStage.prefabSaving -= OnPrefabSaving;
        EditorApplication.delayCall -= ResumeAfterSave;
        _saving = false;
#endif
        _manualPreview = false;
        RestoreAndRepaint();
    }

    private void OnValidate()
    {
        revealDelay = Mathf.Max(0f, revealDelay);
        revealDuration = Mathf.Max(0f, revealDuration);
        // OnValidate는 임포트 스레드에서도 호출될 수 있어 실제 조회는 다음 프레임에 한다.
        _targetsDirty = true;
    }

    private void OnTransformChildrenChanged()
    {
        _targetsDirty = true;
    }

    private void LateUpdate()
    {
        if (Application.isPlaying) Evaluate();
    }

    private void CacheTargets()
    {
        if (!_targetsDirty && _cachedTextSlot == textSlot && _cachedEffectRenderers == effectRenderers) return;
        Restore();
        _targetsDirty = false;
        _cachedTextSlot = textSlot;
        _cachedEffectRenderers = effectRenderers;
        _canvasTargets.Clear();
        _spriteTargets.Clear();
        _textTargets.Clear();
        if (textSlot != null)
        {
            textSlot.GetComponentsInChildren(true, _canvasTargets);
            textSlot.GetComponentsInChildren(true, _spriteTargets);
            textSlot.GetComponentsInChildren(true, _textTargets);
        }
        _canvasAlpha = new float[_canvasTargets.Count];
        _spriteColors = new Color[_spriteTargets.Count];
        _textColors = new Color[_textTargets.Count];

        int count = effectRenderers != null ? effectRenderers.Length : 0;
        _rendererStates = new RendererState[count];
        for (int i = 0; i < count; i++)
            _rendererStates[i] = new RendererState(effectRenderers[i]);
    }

    private void CaptureBaseline()
    {
        for (int i = 0; i < _canvasTargets.Count; i++)
            if (_canvasTargets[i] != null) _canvasAlpha[i] = _canvasTargets[i].GetAlpha();
        for (int i = 0; i < _spriteTargets.Count; i++)
            if (_spriteTargets[i] != null) _spriteColors[i] = _spriteTargets[i].color;
        for (int i = 0; i < _textTargets.Count; i++)
            if (_textTargets[i] != null) _textColors[i] = _textTargets[i].color;
        for (int i = 0; i < _rendererStates.Length; i++)
        {
            var state = _rendererStates[i];
            if (state.Renderer == null) continue;
            state.Renderer.GetPropertyBlock(state.Original);
            state.OriginalWasEmpty = state.Original.isEmpty;
            state.Renderer.GetPropertyBlock(state.Preview);
        }
        _applied = true;
    }

    private bool Evaluate()
    {
        if (!CanUseInstance()) return false;
#if UNITY_EDITOR
        if (_saving) return false;
#endif
        CacheTargets();
        var timer = Timer;
        bool active = _manualPreview || (timer != null && (timer.isPlaying || timer.isPaused));
        if (!Application.isPlaying && !previewInEditMode) active = false;
        if (!isActiveAndEnabled || !active) return Restore();

        float time = _manualPreview ? _manualPreviewTime : Mathf.Clamp(timer.time, 0f, SequenceDuration);
        bool changed = !_applied || time != _lastSequenceTime;
        if (!_applied) CaptureBaseline();
        _lastSequenceTime = time;
        float progress = revealDuration > 0f
            ? Mathf.Clamp01((time - revealDelay) / revealDuration)
            : (time >= revealDelay ? 1f : 0f);
        float alpha = progress * progress * (3f - 2f * progress);

        for (int i = 0; i < _canvasTargets.Count; i++)
            if (_canvasTargets[i] != null) _canvasTargets[i].SetAlpha(_canvasAlpha[i] * alpha);
        for (int i = 0; i < _spriteTargets.Count; i++)
        {
            if (_spriteTargets[i] == null) continue;
            Color color = _spriteColors[i];
            color.a *= alpha;
            _spriteTargets[i].color = color;
        }
        for (int i = 0; i < _textTargets.Count; i++)
        {
            if (_textTargets[i] == null) continue;
            Color color = _textColors[i];
            color.a *= alpha;
            _textTargets[i].color = color;
        }
        for (int i = 0; i < _rendererStates.Length; i++)
        {
            var state = _rendererStates[i];
            if (state.Renderer == null) continue;
            state.Preview.SetFloat(SequenceTimeId, time);
            state.Renderer.SetPropertyBlock(state.Preview);
        }
        return changed;
    }

    private bool Restore()
    {
        if (!_applied) return false;
        for (int i = 0; i < _canvasTargets.Count; i++)
            if (_canvasTargets[i] != null) _canvasTargets[i].SetAlpha(_canvasAlpha[i]);
        for (int i = 0; i < _spriteTargets.Count; i++)
            if (_spriteTargets[i] != null) _spriteTargets[i].color = _spriteColors[i];
        for (int i = 0; i < _textTargets.Count; i++)
            if (_textTargets[i] != null) _textTargets[i].color = _textColors[i];
        for (int i = 0; i < _rendererStates.Length; i++)
        {
            var state = _rendererStates[i];
            if (state.Renderer != null)
                state.Renderer.SetPropertyBlock(state.OriginalWasEmpty ? null : state.Original);
        }
        _applied = false;
        _lastSequenceTime = -1f;
        return true;
    }

    private void RestoreAndRepaint()
    {
        bool changed = Restore();
#if UNITY_EDITOR
        if (changed && !Application.isPlaying) SceneView.RepaintAll();
#endif
    }

#if UNITY_EDITOR
    private void OnEditorHierarchyChanged()
    {
        _targetsDirty = true;
    }

    private void EditorTick()
    {
        if (this == null)
        {
            EditorApplication.update -= EditorTick;
            return;
        }
        if (Application.isPlaying || _saving || !isActiveAndEnabled || !CanUseInstance()) return;
        bool changed = Evaluate();
        bool advancing = previewInEditMode && !_manualPreview && _timer != null && _timer.isPlaying;
        // 정지한 상태에서는 갱신을 예약하지 않는다. Pause 중에는 시간 이동 때만 다시 그린다.
        if (!changed && !advancing) return;
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }

    private void SuspendForSave()
    {
        _saving = true;
        RestoreAndRepaint();
        EditorApplication.delayCall -= ResumeAfterSave;
        EditorApplication.delayCall += ResumeAfterSave;
    }

    private void ResumeAfterSave()
    {
        EditorApplication.delayCall -= ResumeAfterSave;
        _saving = false;
        if (this != null && isActiveAndEnabled) EditorTick();
    }

    private void OnSceneSaving(Scene scene, string path)
    {
        if (this != null && gameObject.scene == scene) SuspendForSave();
    }

    private void OnPrefabSaving(GameObject prefabContentsRoot)
    {
        if (this == null || prefabContentsRoot == null) return;
        if (transform == prefabContentsRoot.transform || transform.IsChildOf(prefabContentsRoot.transform))
            SuspendForSave();
    }
#endif
}
