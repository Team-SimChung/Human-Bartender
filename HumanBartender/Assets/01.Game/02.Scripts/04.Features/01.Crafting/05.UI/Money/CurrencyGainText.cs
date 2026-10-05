using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// "+60V"처럼 도착점으로 떠올라, 메인 텍스트와 겹치는 순간 값을 반영하고 사라지는 팝업 텍스트.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CurrencyGainText : MonoBehaviour
{
    [Header("Format")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private string positivePrefix = "+";
    [SerializeField] private string negativePrefix = "-";
    [SerializeField] private Color positiveColor = new Color(0.45f, 1f, 0.45f); // 초록
    [SerializeField] private Color negativeColor = new Color(1f, 0.45f, 0.45f); // 빨강

    [Header("Motion")]
    [SerializeField] private float travelDuration = 0.55f; // 도착점까지 이동 시간
    [SerializeField] private float mergeDuration = 0.15f;  // 사라지는(페이드) 시간
    [SerializeField] private float mergeThreshold = 6f;    // 이 거리 이내면 "겹침"으로 판정
    [SerializeField] private float endScale = 1.25f;       // 사라질 때 살짝 커짐
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private RectTransform rect;
    private CanvasGroup group;

    private void Awake()
    {
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
    }

    /// <summary>팝업을 시작 위치에서 목표 위치로 이동시키며 재생한다.</summary>
    /// <param name="onMerge">메인 텍스트와 겹치는 순간 호출 (값 반영 콜백)</param>
    public void Play(int amount, string suffix, Vector2 startPos, Vector2 targetPos, Action onMerge)
    {
        if (rect == null) Awake();

        bool positive = amount >= 0;
        label.text = (positive ? positivePrefix : negativePrefix) + Mathf.Abs(amount) + suffix;
        label.color = positive ? positiveColor : negativeColor;

        rect.anchoredPosition = startPos;
        rect.localScale = Vector3.one;
        group.alpha = 1f;

        StartCoroutine(Routine(startPos, targetPos, onMerge));
    }

    /// <summary>이동 -> 겹침 판정 시 값 반영 콜백 호출 -> 페이드아웃 후 자기 자신을 파괴하는 순서로 진행되는 코루틴.</summary>
    private IEnumerator Routine(Vector2 startPos, Vector2 targetPos, Action onMerge)
    {
        // 1) 도착점으로 이동
        float t = 0f;
        while (t < travelDuration)
        {
            t += Time.deltaTime;
            float k = moveCurve.Evaluate(Mathf.Clamp01(t / travelDuration));
            rect.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, k);

            if (Vector2.Distance(rect.anchoredPosition, targetPos) <= mergeThreshold)
                break; // 겹침 판정
            yield return null;
        }

        // 2) 겹치는 순간 값 반영
        rect.anchoredPosition = targetPos;
        onMerge?.Invoke();

        // 3) 페이드아웃 후 제거
        float m = 0f;
        while (m < mergeDuration)
        {
            m += Time.deltaTime;
            float k = Mathf.Clamp01(m / mergeDuration);
            group.alpha = 1f - k;
            rect.localScale = Vector3.one * Mathf.Lerp(1f, endScale, k);
            yield return null;
        }
        Destroy(gameObject);
    }
}
