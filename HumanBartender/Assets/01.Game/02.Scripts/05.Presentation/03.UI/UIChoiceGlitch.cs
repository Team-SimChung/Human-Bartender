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
    Selectable selectable;
    bool hovered, selected, animating;
    double startedAt;

    void OnEnable()
    {
        selectable = GetComponent<Selectable>();
        if (label != null) label.OnPreRenderText += Distort;
        if (holdButton != null) holdButton.HoldingChanged += OnHoldingChanged;
        selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        RefreshFocus();
    }

    void OnDisable()
    {
        if (holdButton != null) holdButton.HoldingChanged -= OnHoldingChanged;
        if (label != null) label.OnPreRenderText -= Distort;
        hovered = selected = animating = false;
        if (focusDecoration != null) focusDecoration.SetActive(false);
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
    public void OnSelect(BaseEventData data) { selected = true; RefreshFocus(true); }
    public void OnDeselect(BaseEventData data) { selected = false; RefreshFocus(); }

    void RefreshFocus(bool entered = false)
    {
        bool focus = (hovered || selected) && selectable != null && selectable.IsInteractable();
        if (focusDecoration != null) focusDecoration.SetActive(focus);
        if (label != null) label.color = focus ? focusedColor : restingColor;
        // 홀드 선택지도 포커스 색은 적용하되, 글자 글리치는 누르고 있는 동안만 재생한다.
        if (holdButton != null) return;
        if (focus && entered) { animating = true; startedAt = Time.unscaledTimeAsDouble; }
        else if (!focus) { animating = false; Restore(); }
    }

    void LateUpdate()
    {
        if (!animating || label == null) return;
        if (selectable == null || !selectable.IsInteractable() ||
            (holdButton == null && Time.unscaledTimeAsDouble - startedAt >= .3))
        {
            animating = false;
            Restore();
            return;
        }
        // TMP regenerates pristine vertices before OnPreRenderText; offsets never accumulate.
        label.ForceMeshUpdate();
    }

    void Distort(TMP_TextInfo info)
    {
        if (!animating) return;
        float phase = (float)((Time.unscaledTimeAsDouble - startedAt) % .3);
        int frame = Mathf.FloorToInt(phase * 60);
        if (frame % 6 == 0 || frame > 15) return;
        for (int i = 0; i < info.characterCount; i++)
        {
            var c = info.characterInfo[i];
            if (!c.isVisible) continue;
            var mesh = info.meshInfo[c.materialReferenceIndex];
            float offset = ((i * 7 + frame * 3) % 5 - 2) * .75f;
            Color32 tint = (i + frame) % 3 == 0 ? new Color32(101, 247, 255, 255) : new Color32(230, 166, 214, 255);
            for (int v = 0; v < 4; v++)
            {
                int index = c.vertexIndex + v;
                mesh.vertices[index].x += offset;
                if ((i + frame) % 3 != 1) { tint.a = mesh.colors32[index].a; mesh.colors32[index] = tint; }
            }
        }
    }

    void Restore() { if (label != null) label.ForceMeshUpdate(true); }
    void OnApplicationFocus(bool focused) { if (!focused) { animating = false; hovered = false; Restore(); } }
}
