using UnityEngine;

/// <summary>다음 대사 입력을 기다릴 때만 프리팹의 표시를 천천히 위아래로 띄운다.</summary>
[DisallowMultipleComponent]
public sealed class DialogueInputPrompt : MonoBehaviour
{
    [SerializeField] private RectTransform indicator;
    [SerializeField, Min(0f)] private float floatHeight = 2f;
    [SerializeField, Min(0.1f)] private float floatPeriod = 1.25f;

    private bool waiting;
    private Vector2 restingPosition;
    private double startedAt;

    private void OnEnable() => SetWaiting(false);
    private void OnDisable() => SetWaiting(false);

    public void SetWaiting(bool value)
    {
        if (indicator == null) return;
        if (value && !waiting)
        {
            restingPosition = indicator.anchoredPosition;
            startedAt = Time.unscaledTimeAsDouble;
        }
        else if (!value && waiting)
        {
            indicator.anchoredPosition = restingPosition;
        }

        waiting = value;
        indicator.gameObject.SetActive(value);
    }

    private void LateUpdate()
    {
        if (!waiting || indicator == null) return;
        float phase = (float)((Time.unscaledTimeAsDouble - startedAt) / Mathf.Max(0.1f, floatPeriod));
        float offset = (1f - Mathf.Cos(phase * Mathf.PI * 2f)) * 0.5f * floatHeight;
        indicator.anchoredPosition = restingPosition + Vector2.up * offset;
    }
}
