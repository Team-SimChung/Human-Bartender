using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// 등급 연출 이펙트(FX_GradeReveal_*)의 부모에 붙어, 등급 글자 슬롯을 부모 파티클의 재생 시간에 맞춰 등장시키는 컴포넌트.
/// - 부모 파티클을 Play 하면 (씬 뷰 Particle Effect 패널의 Play·Restart·시간 이동 포함) 재생 시간을 읽어 글자 슬롯의 불투명도와 크기를 곡선대로 바꾼다.
///   빛·고리·반짝이는 자식 파티클이 맡고 이 컴포넌트는 글자만 맡으므로, 부모 하나만 Play 하면 연출 전체가 나온다.
/// - 글자 슬롯(textSlot) 아래에 무엇을 넣어도 같이 연출된다: Canvas 안의 UI(TextMeshProUGUI, Image 등), SpriteRenderer, TextMeshPro(3D 글자).
///   글자를 바꿀 때는 슬롯의 자식만 바꾸면 되고, 이 컴포넌트와 파티클은 건드리지 않아도 된다. 글자의 눈에 보이는 중심을 슬롯 원점에 맞춘다.
/// - 재생 중이 아닐 때(정지, 끝남)는 글자를 원래 모습으로 둔다. 씬이나 프리팹을 저장할 때도 원래 모습으로 되돌린 뒤 저장한다.
/// - 에디터 미리보기에서는 저장되는 값(슬롯 Transform 크기, SpriteRenderer·3D 글자 색)을 바꾸지 않는다.
///   UI 투명도는 CanvasRenderer 알파로, TMP 글자 크기는 메시 정점으로만 바꾸므로 재생 중에 복제·오버라이드 적용·저장을 해도 값이 남지 않는다.
///   (SpriteRenderer로 바꾼 글자는 에디터 미리보기에서는 움직이지 않고, 플레이 모드에서만 연출된다.)
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class GradeRevealSequence : MonoBehaviour
{
    [Tooltip("등급 글자를 넣는 자리. 이 오브젝트의 자식으로 글자 UI(이미지, TMP 등)를 넣으면 같이 연출된다.")]
    [SerializeField] private Transform textSlot;

    [Tooltip("플레이하지 않아도 씬 뷰 파티클 미리보기(Play)에 맞춰 글자도 같이 움직인다.")]
    [SerializeField] private bool previewInEditMode = true;

    [Header("글자 등장")]
    [Tooltip("부모 파티클을 Play 한 뒤 글자가 나타나기 시작하는 시간 (초). 가운데 빛이 터지는 순간(FX_GradeReveal_Star의 Start Delay)에 맞춘다.")]
    [Min(0f)] [SerializeField] private float revealDelay = 0.24f;

    [Tooltip("나타나기 시작한 뒤 경과 시간(초)에 따른 글자 불투명도 (0~1). 나타나기 전에는 0(안 보임)이다.")]
    [SerializeField] private AnimationCurve alphaOverTime = Linear(0f, 0f, 0.04f, 1f, 0.5f, 1f);

    [Tooltip("나타나기 시작한 뒤 경과 시간(초)에 따른 글자 가로 크기 배율")]
    [SerializeField] private AnimationCurve scaleXOverTime = Linear(0f, 1.15f, 0.1f, 0.97f, 0.18f, 1f);

    [Tooltip("나타나기 시작한 뒤 경과 시간(초)에 따른 글자 세로 크기 배율. 가로 빛줄기에서 위아래로 열리듯 나타나게 한다.")]
    [SerializeField] private AnimationCurve scaleYOverTime = Linear(0f, 0.1f, 0.06f, 1.25f, 0.12f, 0.92f, 0.18f, 1f);

    private ParticleSystem _timer;
    private bool _applied;
    private bool _appliedInEditMode;
    private Transform _appliedSlot;
    private Vector3 _restScale = Vector3.one;
    private readonly Dictionary<CanvasRenderer, float> _canvasAlpha = new Dictionary<CanvasRenderer, float>();
    private readonly Dictionary<SpriteRenderer, Color> _spriteColor = new Dictionary<SpriteRenderer, Color>();
    private readonly Dictionary<TextMeshPro, Color> _textColor = new Dictionary<TextMeshPro, Color>();
    private readonly List<CanvasRenderer> _canvasBuffer = new List<CanvasRenderer>();
    private readonly List<SpriteRenderer> _spriteBuffer = new List<SpriteRenderer>();
    private readonly List<TextMeshPro> _textBuffer = new List<TextMeshPro>();
    private readonly List<TMP_Text> _meshTextBuffer = new List<TMP_Text>();
    private readonly HashSet<TMP_Text> _meshTextTouched = new HashSet<TMP_Text>();

    /// <summary>등급 글자를 넣는 자리.</summary>
    public Transform TextSlot => textSlot;

    /// <summary>부모 파티클을 Play 한 뒤 글자가 나타나기 시작하는 시간 (초).</summary>
    public float RevealDelay => revealDelay;

    private ParticleSystem Timer => _timer != null ? _timer : (_timer = GetComponent<ParticleSystem>());

    /// <summary>연출을 처음부터 다시 재생한다 (자식 파티클 포함).</summary>
    public void Play()
    {
        var timer = Timer;
        if (timer == null) return;
        timer.Clear(true);
        timer.Play(true);
        Evaluate();
    }

    /// <summary>연출을 멈추고 파티클을 지운다. 글자는 원래 모습으로 돌아온다.</summary>
    public void Stop()
    {
        var timer = Timer;
        if (timer != null) timer.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Restore();
    }

    private void Reset()
    {
        // 컴포넌트를 처음 붙일 때 이름이 GradeText_Slot인 자식을 찾아 연결한다.
        var slot = transform.Find("GradeText_Slot");
        if (slot != null) textSlot = slot;
    }

    private void OnEnable()
    {
        _timer = GetComponent<ParticleSystem>();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.update += EditorTick;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;
        }
#endif
        Evaluate();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        PrefabStage.prefabSaving -= OnPrefabSaving;
#endif
        Restore();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;
        Evaluate();
    }

    private void Evaluate()
    {
        var timer = Timer;
        bool running = textSlot != null && timer != null && (timer.isPlaying || timer.isPaused);
        if (!Application.isPlaying && !previewInEditMode) running = false;
        if (!running)
        {
            Restore();
            return;
        }

        float t = timer.time - revealDelay;
        float alpha = t < 0f ? 0f : Mathf.Clamp01(Sample(alphaOverTime, t));
        float scaleX = Sample(scaleXOverTime, Mathf.Max(t, 0f));
        float scaleY = Sample(scaleYOverTime, Mathf.Max(t, 0f));
        Apply(alpha, scaleX, scaleY);
    }

    private static float Sample(AnimationCurve curve, float time)
    {
        return curve != null && curve.length > 0 ? curve.Evaluate(time) : 1f;
    }

    private void Apply(float alpha, float scaleX, float scaleY)
    {
        bool editPreview = !Application.isPlaying;
        if (_applied && (_appliedSlot != textSlot || _appliedInEditMode != editPreview)) Restore();
        if (!_applied)
        {
            _applied = true;
            _appliedSlot = textSlot;
            _appliedInEditMode = editPreview;
            _restScale = textSlot.localScale;
        }

        // UI(TextMeshProUGUI, Image 등): CanvasRenderer 알파는 저장되지 않는 값이라 씬·프리팹에 남지 않는다.
        textSlot.GetComponentsInChildren(true, _canvasBuffer);
        foreach (var canvasRenderer in _canvasBuffer)
        {
            if (!_canvasAlpha.TryGetValue(canvasRenderer, out float rest))
            {
                rest = canvasRenderer.GetAlpha();
                _canvasAlpha[canvasRenderer] = rest;
            }
            canvasRenderer.SetAlpha(rest * alpha);
        }

        if (editPreview)
        {
            // 에디터 미리보기에서는 저장되는 값(Transform 크기, 색)을 건드리지 않는다.
            // 재생 중에 오브젝트를 복제하거나 오버라이드를 적용하면 그 순간 값이 그대로 저장되기 때문이다.
            // TMP 글자는 메시 정점만 늘였다 줄이고, 3D 글자의 투명도는 정점 색으로 준다 (모두 저장되지 않는 값).
            ScaleTextMeshes(alpha, scaleX, scaleY);
            return;
        }

        // 플레이 중에는 끝나면 값이 되돌아가므로 슬롯 크기와 색을 직접 바꾼다.
        textSlot.localScale = new Vector3(_restScale.x * scaleX, _restScale.y * scaleY, _restScale.z);

        textSlot.GetComponentsInChildren(true, _spriteBuffer);
        foreach (var sprite in _spriteBuffer)
        {
            if (!_spriteColor.TryGetValue(sprite, out var rest))
            {
                rest = sprite.color;
                _spriteColor[sprite] = rest;
            }
            sprite.color = new Color(rest.r, rest.g, rest.b, rest.a * alpha);
        }

        textSlot.GetComponentsInChildren(true, _textBuffer);
        foreach (var text in _textBuffer)
        {
            if (!_textColor.TryGetValue(text, out var rest))
            {
                rest = text.color;
                _textColor[text] = rest;
            }
            text.color = new Color(rest.r, rest.g, rest.b, rest.a * alpha);
        }
    }

    // 글자 슬롯 아래 TMP 글자의 메시 정점을 슬롯 원점 기준으로 늘였다 줄인다. 메시는 저장되지 않으므로 씬·프리팹에 남지 않는다.
    private void ScaleTextMeshes(float alpha, float scaleX, float scaleY)
    {
        textSlot.GetComponentsInChildren(true, _meshTextBuffer);
        Vector3 pivotWorld = textSlot.position;
        foreach (var text in _meshTextBuffer)
        {
            if (text == null || !text.isActiveAndEnabled) continue;
            text.ForceMeshUpdate();
            _meshTextTouched.Add(text);
            var info = text.textInfo;
            if (info == null || info.meshInfo == null) continue;

            Vector3 pivot = text.transform.InverseTransformPoint(pivotWorld);
            bool tintVertices = !(text is TextMeshProUGUI);
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                var vertices = info.meshInfo[m].vertices;
                if (vertices != null)
                {
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        Vector3 p = vertices[v];
                        p.x = pivot.x + (p.x - pivot.x) * scaleX;
                        p.y = pivot.y + (p.y - pivot.y) * scaleY;
                        vertices[v] = p;
                    }
                }
                var colors = info.meshInfo[m].colors32;
                if (tintVertices && colors != null)
                {
                    for (int v = 0; v < colors.Length; v++)
                        colors[v].a = (byte)(colors[v].a * alpha);
                }
            }
            text.UpdateVertexData(tintVertices ? TMP_VertexDataUpdateFlags.All : TMP_VertexDataUpdateFlags.Vertices);
        }
    }

    private void Restore()
    {
        if (!_applied) return;
        _applied = false;

        if (_appliedSlot != null && !_appliedInEditMode) _appliedSlot.localScale = _restScale;
        foreach (var pair in _canvasAlpha)
            if (pair.Key != null) pair.Key.SetAlpha(pair.Value);
        foreach (var pair in _spriteColor)
            if (pair.Key != null) pair.Key.color = pair.Value;
        foreach (var pair in _textColor)
            if (pair.Key != null) pair.Key.color = pair.Value;
        foreach (var text in _meshTextTouched)
            if (text != null) text.ForceMeshUpdate();

        _canvasAlpha.Clear();
        _spriteColor.Clear();
        _textColor.Clear();
        _meshTextTouched.Clear();
        _appliedSlot = null;

#if UNITY_EDITOR
        if (!Application.isPlaying) SceneView.RepaintAll();
#endif
    }

    // 시간·값 쌍으로 직선으로 잇는 곡선을 만든다 (기본값용).
    private static AnimationCurve Linear(params float[] timeValuePairs)
    {
        int count = timeValuePairs.Length / 2;
        var keys = new Keyframe[count];
        for (int i = 0; i < count; i++)
            keys[i] = new Keyframe(timeValuePairs[i * 2], timeValuePairs[i * 2 + 1]);
        for (int i = 0; i < count; i++)
        {
            float inSlope = i > 0 ? Slope(keys[i - 1], keys[i]) : 0f;
            float outSlope = i < count - 1 ? Slope(keys[i], keys[i + 1]) : 0f;
            keys[i].inTangent = inSlope;
            keys[i].outTangent = outSlope;
        }
        return new AnimationCurve(keys);
    }

    private static float Slope(Keyframe a, Keyframe b)
    {
        float dt = b.time - a.time;
        return dt > 0f ? (b.value - a.value) / dt : 0f;
    }

#if UNITY_EDITOR
    private void EditorTick()
    {
        if (this == null)
        {
            EditorApplication.update -= EditorTick;
            return;
        }
        if (Application.isPlaying) return;
        Evaluate();
    }

    private void OnSceneSaving(Scene scene, string path)
    {
        if (this != null && gameObject.scene == scene) Restore();
    }

    private void OnPrefabSaving(GameObject prefabContentsRoot)
    {
        if (this != null && prefabContentsRoot != null && transform.IsChildOf(prefabContentsRoot.transform)) Restore();
    }
#endif
}
