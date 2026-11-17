using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>저장된 푸어러 두 개를 교대로 표시한다. 분리는 한 따르기에서 한 번만 일어난다.</summary>
public sealed class PourerBreakaway : MonoBehaviour
{
    [Header("Bottle / Resources")]
    [SerializeField] BottleTiltController bottle;
    [SerializeField] SpriteRenderer ingredientBottle;
    [Tooltip("BottleVisual 아래에 저장한 푸어러. 윗끝을 액체 출구에 맞춘다.")]
    [SerializeField] SpriteRenderer attachedPourer;
    [Tooltip("회전하지 않는 무대 아래에 저장한 분리 연출용 푸어러.")]
    [SerializeField] SpriteRenderer detachedPourer;
    [Tooltip("웹빌드와 같이 푸어러를 사용하지 않는 재료 ID. 빈 ID도 푸어러를 사용하지 않는다.")]
    [SerializeField] string[] ingredientsWithoutPourer =
    {
        "soda_water", "cola", "cranberry_juice", "milk", "beer", "orange_juice", "red_wine", "champagne"
    };

    [Header("Breakaway / Flow")]
    [Tooltip("병을 잡은 상태에서 이 각도 이상 기울이면 푸어러가 분리된다. 병의 최대 기울기보다 낮게 설정한다.")]
    [SerializeField, Range(0f, 180f)] float detachAngle = 160f;
    [Tooltip("분리 후 실제 입자 방출량 배율. 웹빌드의 푸어러 유량 42 → 분리 후 95에 해당한다.")]
    [SerializeField, Min(1f)] float detachedFlowMultiplier = 95f / 42f;

    [Header("Attached Stream")]
    [Tooltip("푸어러가 붙어 있을 때 유입 물줄기 입자의 가로 두께. 잔 안에서 낙하 중인 입자에도 적용한다. 0.2이면 기존 두께의 20%이며 출수량과 물리는 실제 설정을 사용한다.")]
    [SerializeField, Range(0.1f, 1f)] float attachedStreamWidthScale = 0.2f;

    [Header("Detached Motion")]
    [Tooltip("무대 로컬 좌표 기준 발사 속도. X는 오른쪽, Y는 위쪽이다.")]
    [SerializeField] Vector2 launchVelocity = new Vector2(2.1f, 3.5f);
    [Tooltip("무대 로컬 좌표 기준 아래쪽 낙하 가속도.")]
    [SerializeField, Min(0f)] float gravity = 9f;
    [Tooltip("초당 회전 각도. 음수는 시계 방향이다.")]
    [SerializeField] float spinDegreesPerSecond = -920f;
    [Tooltip("분리된 푸어러를 표시하는 시간(초).")]
    [SerializeField, Min(0.01f)] float flightSeconds = 2.4f;
    [Tooltip("비행 마지막에 투명해지는 시간(초).")]
    [SerializeField, Min(0f)] float flightFadeSeconds = 0.3f;

    [Header("Pop Feedback")]
    [SerializeField] CanvasGroup feedbackRoot;
    [SerializeField] TMP_Text feedbackText;
    [SerializeField] string popTextKo = "펑!";
    [SerializeField] string popTextEn = "POP!";
    [SerializeField, Min(0.01f)] float feedbackSeconds = 0.9f;
    [SerializeField, Min(0f)] float feedbackFadeSeconds = 0.5f;
    [SerializeField, Min(0f)] float feedbackRise = 28f;

    [Header("Breakaway Sound (Optional)")]
    [Tooltip("분리 순간 한 번 재생할 효과음. 나중에 클립을 넣으면 적용되며 비워 두면 소리 없이 동작한다.")]
    [SerializeField] AudioClip detachClip;
    [Tooltip("프리팹에 저장된 전용 출력. 공용 효과음 믹서를 사용한다.")]
    [SerializeField] AudioSource detachSource;
    [Tooltip("효과음 자체의 음량. 사용자 효과음 음량은 공용 믹서에서 별도로 적용된다.")]
    [SerializeField, Range(0f, 1f)] float detachGain = 1f;

    bool hasPourer;
    bool bodyOffsetApplied;
    Vector3 bodyRestPosition;
    Vector3 flightStartPosition;
    Quaternion flightStartRotation;
    Color flightColor;
    float flightElapsed;
    bool flying;
    Vector2 feedbackRestPosition;
    bool feedbackPositionCaptured;
    Sequence feedbackTween;

    public bool IsDetached { get; private set; }
    public bool IsAttached => hasPourer && !IsDetached;
    public float DetachAngle => detachAngle;
    public float FlowMultiplier => IsDetached ? Mathf.Max(1f, detachedFlowMultiplier) : 1f;
    public float StreamWidthScale => IsAttached ? attachedStreamWidthScale : 1f;

    /// <summary>다음 재료를 시작할 때 호출한다. 입자당 용량이나 잔의 수량은 변경하지 않는다.</summary>
    public void BeginPour(string ingredientId, Vector3 mouthLocalPosition)
    {
        ResetState();
        hasPourer = UsesPourer(ingredientId) && bottle != null && attachedPourer != null &&
            attachedPourer.sprite != null && detachedPourer != null;
        if (!hasPourer) return;

        // 병마다 스프라이트의 PPU·피벗이 달라도 푸어러의 윗끝과 기존 액체 출구가 일치한다.
        Transform cap = attachedPourer.transform;
        Bounds bounds = attachedPourer.sprite.bounds;
        Vector3 tip = new Vector3(bounds.center.x, bounds.max.y, 0f);
        cap.position = bottle.BottleVisual.TransformPoint(mouthLocalPosition) - cap.TransformVector(tip);
        attachedPourer.gameObject.SetActive(true);

        // 웹빌드처럼 푸어러 길이만큼 병 그림을 내린다. 분리되면 원래 입구 위치로 돌아온다.
        if (ingredientBottle != null && ingredientBottle.gameObject.activeSelf)
        {
            bodyRestPosition = ingredientBottle.transform.localPosition;
            Vector3 neck = cap.TransformVector(Vector3.up * bounds.size.y);
            Transform parent = ingredientBottle.transform.parent;
            ingredientBottle.transform.localPosition -= parent != null ? parent.InverseTransformVector(neck) : neck;
            bodyOffsetApplied = true;
        }
    }

    public bool UsesPourer(string ingredientId)
    {
        if (string.IsNullOrEmpty(ingredientId)) return false;
        if (ingredientsWithoutPourer != null)
            foreach (string excluded in ingredientsWithoutPourer)
                if (string.Equals(excluded, ingredientId, System.StringComparison.Ordinal)) return false;
        return true;
    }

    /// <summary>게임이 조작 가능할 때만 매니저가 호출한다. 놓은 병의 복귀 회전으로는 발동하지 않는다.</summary>
    public bool TryDetach()
    {
        if (!IsAttached || bottle == null || !bottle.IsDragging || bottle.CurrentAngle < detachAngle) return false;

        IsDetached = true;
        Transform source = attachedPourer.transform;
        Transform target = detachedPourer.transform;
        target.SetPositionAndRotation(source.position, source.rotation);
        Vector3 parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
        Vector3 sourceScale = source.lossyScale;
        target.localScale = new Vector3(sourceScale.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            sourceScale.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), sourceScale.z);
        detachedPourer.sprite = attachedPourer.sprite;
        detachedPourer.color = attachedPourer.color;
        flightColor = detachedPourer.color;
        // 원본 스프라이트 피벗이 왼쪽 아래여도 중심을 기준으로 회전하며 날아간다.
        flightStartPosition = target.localPosition + target.localRotation *
            Vector3.Scale(detachedPourer.sprite.bounds.center, target.localScale);
        flightStartRotation = target.localRotation;
        flightElapsed = 0f;
        flying = true;
        detachedPourer.gameObject.SetActive(true);
        attachedPourer.gameObject.SetActive(false);
        RestoreBody();
        ShowFeedback();
        PlayDetachSound();
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f) { StopDetachSound(); return; }
        if (!flying) return;
        flightElapsed += deltaTime;
        float t = Mathf.Min(flightElapsed, flightSeconds);
        Transform target = detachedPourer.transform;
        target.localRotation = flightStartRotation * Quaternion.Euler(0f, 0f, spinDegreesPerSecond * t);
        Vector3 center = flightStartPosition + new Vector3(launchVelocity.x * t, launchVelocity.y * t - 0.5f * gravity * t * t, 0f);
        target.localPosition = center - target.localRotation * Vector3.Scale(detachedPourer.sprite.bounds.center, target.localScale);
        Color color = flightColor;
        if (flightFadeSeconds > 0f) color.a *= Mathf.Clamp01((flightSeconds - t) / flightFadeSeconds);
        detachedPourer.color = color;
        if (flightElapsed < flightSeconds) return;
        flying = false;
        detachedPourer.gameObject.SetActive(false);
    }

    void ShowFeedback()
    {
        if (feedbackRoot == null || feedbackText == null) return;
        bool english = GameStateManager.Instance != null && GameStateManager.Instance.Language == ELanguage.En;
        feedbackText.text = english ? popTextEn : popTextKo;
        var rect = (RectTransform)feedbackRoot.transform;
        feedbackRestPosition = rect.anchoredPosition;
        feedbackPositionCaptured = true;
        feedbackRoot.alpha = 1f;
        feedbackRoot.gameObject.SetActive(true);
        float duration = Mathf.Max(0.01f, feedbackSeconds);
        float fade = Mathf.Clamp(feedbackFadeSeconds, 0f, duration);
        feedbackTween = DOTween.Sequence()
            .Join(rect.DOAnchorPosY(feedbackRestPosition.y + feedbackRise, duration).SetEase(Ease.Linear))
            .Join(feedbackRoot.DOFade(0f, fade).SetDelay(duration - fade))
            .OnComplete(HideFeedback);
    }

    void HideFeedback()
    {
        if (feedbackRoot == null) return;
        if (feedbackPositionCaptured) ((RectTransform)feedbackRoot.transform).anchoredPosition = feedbackRestPosition;
        feedbackRoot.alpha = 0f;
        feedbackRoot.gameObject.SetActive(false);
    }

    void RestoreBody()
    {
        if (bodyOffsetApplied && ingredientBottle != null) ingredientBottle.transform.localPosition = bodyRestPosition;
        bodyOffsetApplied = false;
    }

    void PlayDetachSound()
    {
        if (detachClip == null || detachSource == null) return;
        AudioOutputSettings.Shared.Route(detachSource, AudioBus.SoundEffects);
        detachSource.PlayOneShot(detachClip, detachGain);
    }

    void StopDetachSound()
    {
        if (detachSource != null) detachSource.Stop();
    }

    public void ResetState()
    {
        RestoreBody();
        hasPourer = false;
        IsDetached = false;
        flying = false;
        StopDetachSound();
        feedbackTween?.Kill();
        feedbackTween = null;
        HideFeedback();
        if (attachedPourer != null) attachedPourer.gameObject.SetActive(false);
        if (detachedPourer != null) detachedPourer.gameObject.SetActive(false);
    }

    void OnDisable() => ResetState();
    void OnApplicationFocus(bool focus) { if (!focus) StopDetachSound(); }
    void OnApplicationPause(bool pause) { if (pause) StopDetachSound(); }
}
