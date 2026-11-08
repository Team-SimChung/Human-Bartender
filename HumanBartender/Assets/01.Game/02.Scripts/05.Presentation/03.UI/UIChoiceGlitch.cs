using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>선택 규칙과 분리된 글자 연출. 홀드 해제·포커스 상실·비활성화 때 원본 메시를 복원한다.</summary>
[DisallowMultipleComponent]
public sealed class UIChoiceGlitch : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] TMP_Text label;
    [SerializeField] ChoiceHoldButton holdButton;
    [SerializeField] GameObject focusDecoration;
    [SerializeField] Color restingColor = new Color32(77, 145, 165, 255);
    [SerializeField] Color focusedColor = new Color32(237, 252, 255, 255);
    [SerializeField] TMP_FontAsset restingFont;
    [SerializeField] TMP_FontAsset focusedFont;

    [Header("Glitch")]
    [Tooltip("글리치 연출 값 에셋(04.Data/01.Definitions/UI/ChoiceGlitchSettings). 비우면 기본값으로 재생한다.")]
    [SerializeField] UIChoiceGlitchSettings settings;

    float Duration => Mathf.Max(settings != null ? settings.Duration : UIChoiceGlitchSettings.DefaultDuration, .01f);
    float FrameRate => settings != null ? settings.FrameRate : UIChoiceGlitchSettings.DefaultFrameRate;
    int ActiveFrames => settings != null ? settings.ActiveFrames : UIChoiceGlitchSettings.DefaultActiveFrames;
    int RestEvery => settings != null ? settings.RestEvery : UIChoiceGlitchSettings.DefaultRestEvery;
    float Jitter => settings != null ? settings.Jitter : UIChoiceGlitchSettings.DefaultJitter;
    Color32 TintA => settings != null ? settings.TintA : UIChoiceGlitchSettings.DefaultTintA;
    Color32 TintB => settings != null ? settings.TintB : UIChoiceGlitchSettings.DefaultTintB;

    Selectable selectable;
    bool hovered, selected, animating;
    // 마우스로 누르면 Selectable이 이 버튼을 EventSystem 선택으로 잡는다. 마우스 강조는 호버가 맡으므로
    // 그 선택은 강조로 치지 않는다(웹 :focus-visible과 같음). 꺼졌다 다시 켜져도 남은 선택을 구분하려고 유지한다.
    bool selectedByPointer;
    bool lastFocus;
    double startedAt;

    void OnEnable()
    {
        selectable = GetComponent<Selectable>();
        if (label != null) label.OnPreRenderText += Distort;
        if (holdButton != null) holdButton.HoldingChanged += OnHoldingChanged;
        // 이전 선택지에서 마우스로 누른 칸이 같은 자리에 다시 켜지면, 남아 있는 선택 때문에 강조가 고정되지 않게 한다.
        selected = !selectedByPointer && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        RefreshFocus();
    }

    void OnDisable()
    {
        if (holdButton != null) holdButton.HoldingChanged -= OnHoldingChanged;
        if (label != null) label.OnPreRenderText -= Distort;
        hovered = selected = animating = false;
        if (focusDecoration != null) focusDecoration.SetActive(false);
        if (label != null && restingFont != null) label.font = restingFont;
        Restore();
    }

    void OnHoldingChanged(bool holding)
    {
        animating = holding;
        startedAt = Time.unscaledTimeAsDouble;
        if (!holding) Restore();
    }

    public void OnPointerEnter(PointerEventData data) { hovered = true; RefreshFocus(true); }
    public void OnPointerExit(PointerEventData data) { hovered = false; RefreshFocus(); }
    public void OnSelect(BaseEventData data)
    {
        selectedByPointer = data is PointerEventData;
        selected = !selectedByPointer;
        RefreshFocus(true);
    }

    public void OnDeselect(BaseEventData data) { selected = selectedByPointer = false; RefreshFocus(); }

    void RefreshFocus(bool entered = false)
    {
        bool focus = (hovered || selected) && selectable != null && selectable.IsInteractable();
        lastFocus = focus;
        if (focusDecoration != null) focusDecoration.SetActive(focus);
        if (label != null)
        {
            label.color = focus ? focusedColor : restingColor;
            TMP_FontAsset font = focus ? focusedFont : restingFont;
            if (font != null && label.font != font) label.font = font;
        }
        // 홀드 선택지도 포커스 색은 적용하되, 글자 글리치는 누르고 있는 동안만 재생한다.
        if (holdButton != null) return;
        if (focus && entered) { animating = true; startedAt = Time.unscaledTimeAsDouble; }
        else if (!focus) { animating = false; Restore(); }
    }

    void LateUpdate()
    {
        bool focus = (hovered || selected) && selectable != null && selectable.IsInteractable();
        if (focus != lastFocus) RefreshFocus();
        if (!animating || label == null) return;
        if (selectable == null || !selectable.IsInteractable() ||
            (holdButton == null && Time.unscaledTimeAsDouble - startedAt >= Duration))
        {
            animating = false;
            Restore();
            return;
        }
        // 글자 흔들기와 셰이더가 같은 박자로 켜지고 쉬도록, 타이밍은 여기 한 곳에서만 계산한다.
        bool active = TryGetGlitchFrame(out _, out int pattern);
        if (settings != null && settings.TextGlitchMaterial != null)
            materialGlitch.Apply(label, settings.TextGlitchMaterial, active, pattern);
        // TMP regenerates pristine vertices before OnPreRenderText; offsets never accumulate.
        label.ForceMeshUpdate();
    }

    /// <summary>지금 글리치가 보이는 프레임인지. frame은 주기 안 번호(글자 흔들기용), pattern은 시작부터 센 번호(셰이더 노이즈용).</summary>
    bool TryGetGlitchFrame(out int frame, out int pattern)
    {
        double elapsed = Time.unscaledTimeAsDouble - startedAt;
        frame = Mathf.FloorToInt((float)(elapsed % Duration) * FrameRate);
        pattern = Mathf.FloorToInt((float)elapsed * FrameRate);
        int restEvery = RestEvery;
        return animating && !((restEvery > 0 && frame % restEvery == 0) || frame > ActiveFrames);
    }

    readonly TMPGlitchMaterial materialGlitch = new TMPGlitchMaterial();

    void OnDestroy() { if (label != null) materialGlitch.Release(label); materialGlitch.Dispose(); }

    void Distort(TMP_TextInfo info)
    {
        if (!TryGetGlitchFrame(out int frame, out _)) return;
        float jitter = Jitter;
        Color32 tintA = TintA, tintB = TintB;
        for (int i = 0; i < info.characterCount; i++)
        {
            var c = info.characterInfo[i];
            if (!c.isVisible) continue;
            var mesh = info.meshInfo[c.materialReferenceIndex];
            float offset = ((i * 7 + frame * 3) % 5 - 2) * jitter;
            Color32 tint = (i + frame) % 3 == 0 ? tintA : tintB;
            for (int v = 0; v < 4; v++)
            {
                int index = c.vertexIndex + v;
                mesh.vertices[index].x += offset;
                if ((i + frame) % 3 != 1) { tint.a = mesh.colors32[index].a; mesh.colors32[index] = tint; }
            }
        }
    }

    void Restore()
    {
        if (label == null) return;
        materialGlitch.Release(label);
        label.ForceMeshUpdate(true);
    }

    void OnApplicationFocus(bool focused) { if (!focused) { animating = false; hovered = false; Restore(); } }
}

/// <summary>
/// 글리치 동안만 글자 머티리얼을 Custom/TMPGlitch 사본으로 바꾼다. 같은 폰트 글자끼리 머티리얼을 공유하므로
/// 사본을 써야 누른 칸만 지지직거린다. 템플릿의 _Glitch* 속성은 이름을 몰라도 전부 복사한다.
/// </summary>
sealed class TMPGlitchMaterial
{
    const string Prefix = "_Glitch";
    static readonly int StrengthId = Shader.PropertyToID("_GlitchStrength");
    static readonly int FrameId = Shader.PropertyToID("_GlitchFrame");
    Material copy, source;

    public void Apply(TMP_Text label, Material template, bool active, int pattern)
    {
        Material current = label.fontSharedMaterial;
        if (current == null) return;
        // 포커스 때 폰트가 바뀌면 TMP가 폰트 기본 머티리얼로 되돌린다. 그때는 새 원본으로 사본을 다시 만든다.
        if (current != copy)
        {
            if (copy == null || source != current) Create(current, template);
            label.fontSharedMaterial = copy;
        }
        copy.SetFloat(StrengthId, active ? 1f : 0f);
        copy.SetFloat(FrameId, pattern);
    }

    public void Release(TMP_Text label)
    {
        if (copy != null && label.fontSharedMaterial == copy) label.fontSharedMaterial = source;
    }

    public void Dispose()
    {
        if (copy == null) return;
        if (Application.isPlaying) Object.Destroy(copy); else Object.DestroyImmediate(copy);
        copy = source = null;
    }

    void Create(Material current, Material template)
    {
        Dispose();
        copy = new Material(current) { name = current.name + " (Glitch)", hideFlags = HideFlags.HideAndDontSave };
        copy.shader = template.shader;
        Shader shader = template.shader;
        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            string name = shader.GetPropertyName(i);
            if (!name.StartsWith(Prefix, System.StringComparison.Ordinal)) continue;
            switch (shader.GetPropertyType(i))
            {
                case UnityEngine.Rendering.ShaderPropertyType.Float:
                case UnityEngine.Rendering.ShaderPropertyType.Range: copy.SetFloat(name, template.GetFloat(name)); break;
                case UnityEngine.Rendering.ShaderPropertyType.Color: copy.SetColor(name, template.GetColor(name)); break;
                case UnityEngine.Rendering.ShaderPropertyType.Vector: copy.SetVector(name, template.GetVector(name)); break;
                case UnityEngine.Rendering.ShaderPropertyType.Int: copy.SetInteger(name, template.GetInteger(name)); break;
            }
        }
        source = current;
    }
}
