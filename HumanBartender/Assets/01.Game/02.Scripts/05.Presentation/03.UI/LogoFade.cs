using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>로고와 팀명을 함께 페이드하고, 선택적으로 로고를 이동한다.</summary>
public class LogoFade : MonoBehaviour
{
    [SerializeField] CanvasGroup logoGroup;
    [SerializeField] CanvasGroup fadeCanvasGroup;
    [SerializeField] RectTransform targetRect;

    [Header("Movement (Optional)")]
    [Tooltip("켜면 페이드인과 동시에 Target Rect를 이동합니다. 끄면 편집한 위치를 그대로 유지합니다.")]
    [SerializeField] bool enableMovement;
    [Tooltip("이동 시작 위치입니다. 이동량이 아닌 Target Rect의 앵커 기준 좌표입니다.")]
    [SerializeField] Vector2 targetOriginPosition;
    [Tooltip("이동 도착 위치입니다. 이동량이 아닌 Target Rect의 앵커 기준 좌표입니다.")]
    [SerializeField] Vector2 targetAnchorPosition;
    [SerializeField] Ease moveEaseGraph = Ease.Linear;
    [Tooltip("페이드인과 동시에 시작합니다. 이동과 페이드인이 모두 끝난 뒤 Logo Duration 동안 유지합니다.")]
    [Min(0f)] [SerializeField] float moveDuration = 1f;

    [Header("Fade")]
    [SerializeField] float fadeDuration = 1f;
    [SerializeField] float logoDuration = 1f;
    CancellationTokenSource playback;

    public bool IsPlaying => playback != null;
    void OnEnable()
    {
        // 프리팹의 Alpha는 편집용 미리보기 값이다. 게임에서는 항상 숨긴 채 시작한다.
        if (Application.isPlaying) Hide();
    }

    void OnDisable()
    {
        playback?.Cancel();
        if (Application.isPlaying) Hide();
    }

    void Hide()
    {
        if (logoGroup != null) { logoGroup.alpha = 0f; logoGroup.blocksRaycasts = false; }
        if (fadeCanvasGroup != null) { fadeCanvasGroup.alpha = 0f; fadeCanvasGroup.blocksRaycasts = false; }
    }

    public void ShowLogo() => ShowLogoAsync().Forget(e =>
    {
        if (e is not OperationCanceledException) Debug.LogException(e);
    });

    public async UniTask ShowLogoAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (playback != null) throw new InvalidOperationException("A logo is already playing.");
        if (!isActiveAndEnabled) throw new InvalidOperationException("The logo is disabled.");
        if (logoGroup == null || fadeCanvasGroup == null || targetRect == null)
            throw new InvalidOperationException("Logo references are missing.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        playback = source;
        bool oldActive = targetRect.gameObject.activeSelf;
        bool move = enableMovement;
        Vector2 oldPosition = targetRect.anchoredPosition;
        Sequence sequence = null;
        bool tweenAwaitStarted = false;
        try
        {
            Hide();
            if (move) targetRect.anchoredPosition = targetOriginPosition;
            targetRect.gameObject.SetActive(true);
            fadeCanvasGroup.blocksRaycasts = true;
            logoGroup.blocksRaycasts = true;
            sequence = DOTween.Sequence().SetTarget(this);
            sequence.Append(fadeCanvasGroup.DOFade(1f, Mathf.Max(0, fadeDuration)).SetEase(Ease.Linear));
            sequence.Join(logoGroup.DOFade(1f, Mathf.Max(0, fadeDuration)).SetEase(Ease.Linear));
            if (move)
                sequence.Join(targetRect.DOAnchorPos(targetAnchorPosition, Mathf.Max(0, moveDuration)).SetEase(moveEaseGraph));
            sequence.AppendInterval(Mathf.Max(0, logoDuration));
            sequence.Append(fadeCanvasGroup.DOFade(0f, Mathf.Max(0, fadeDuration)).SetEase(Ease.Linear));
            sequence.Join(logoGroup.DOFade(0f, Mathf.Max(0, fadeDuration)).SetEase(Ease.Linear));
            tweenAwaitStarted = true;
            await sequence.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, source.Token);
            source.Token.ThrowIfCancellationRequested();
        }
        finally
        {
            if (!tweenAwaitStarted) sequence?.Kill();
            if (ReferenceEquals(playback, source)) playback = null;
            // 정상 종료와 취소 모두 편집용 Alpha로 복원하지 않는다.
            Hide();
            if (targetRect != null)
            {
                if (move) targetRect.anchoredPosition = oldPosition;
                targetRect.gameObject.SetActive(oldActive);
            }
        }
    }
}
