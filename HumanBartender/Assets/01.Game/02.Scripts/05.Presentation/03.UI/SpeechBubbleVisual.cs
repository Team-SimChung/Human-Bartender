using UnityEngine;
using UnityEngine.UI;

public enum SpeechBubbleVisualMode
{
    SingleImage = 0,
    Parts = 1
}

/// <summary>외형 전환과 갱신 순서만 조정한다. 본체 배치는 PartsView, 꼬리 배치는 Tail이 소유한다.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(DynamicSpeechBubble))]
[DefaultExecutionOrder(10002)]
public sealed class SpeechBubbleVisual : MonoBehaviour
{
    [SerializeField, InspectorName("Body Parts")] SpeechBubblePartsView parts;
    DynamicSpeechBubble source;

    public SpeechBubblePartsView Parts => parts;
    public bool IsConfigured => parts != null && parts.IsConfigured &&
        HasTail(Source.centerArrow) && HasTail(Source.leftArrow) && HasTail(Source.rightArrow);
    DynamicSpeechBubble Source => source != null ? source : source = GetComponent<DynamicSpeechBubble>();

    static bool HasTail(GameObject target) => target != null &&
        target.TryGetComponent<SpeechBubbleTail>(out var tail) && tail.enabled && tail.IsConfigured;

    void OnEnable()
    {
        Canvas.willRenderCanvases += Refresh;
        Refresh();
    }

    void LateUpdate() => Refresh();

    public void Refresh()
    {
        if (!isActiveAndEnabled || Source.bubble == null) return;
        Apply(Source.visualMode == SpeechBubbleVisualMode.Parts && IsConfigured);
    }

    void OnDisable()
    {
        Canvas.willRenderCanvases -= Refresh;
        if (Source != null && Source.bubble != null) Apply(false);
    }

    void Apply(bool useParts)
    {
        if (source.bubble.TryGetComponent<Image>(out var bodyImage) && bodyImage.enabled == useParts)
            bodyImage.enabled = !useParts;
        if (parts != null)
        {
            if (parts.gameObject.activeSelf != useParts) parts.gameObject.SetActive(useParts);
            if (useParts) parts.Refresh();
        }
        RefreshTail(0, source.centerArrow, useParts);
        RefreshTail(1, source.leftArrow, useParts);
        RefreshTail(2, source.rightArrow, useParts);
    }

    void RefreshTail(int index, GameObject target, bool useParts)
    {
        SpeechBubbleTail.Connection connection = default;
        if (target != null && target.TryGetComponent<SpeechBubbleTail>(out var tail))
            connection = tail.Refresh(source.bubble, useParts, useParts ? parts.EffectiveCornerSize : Vector2.zero);
        if (useParts) parts.SetTailConnection(index, connection);
    }
}
