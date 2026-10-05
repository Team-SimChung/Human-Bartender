using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>로고 연출 하나의 Tween과 화면 차단 수명을 관리한다.</summary>
public class LogoFade : MonoBehaviour
{
    [SerializeField] CanvasGroup logoGroup;
    [SerializeField] CanvasGroup fadeCanvasGroup;
    [SerializeField] RectTransform targetRect;
    [SerializeField] Vector2 targetAnchorPosition;
    [SerializeField] Vector2 targetOriginPosition;
    [SerializeField] Ease moveEaseGraph = Ease.Linear;
    [SerializeField] float moveDuration = 1f;
    [SerializeField] float fadeDuration = 1f;
    [SerializeField] float logoDuration = 1f;
    CancellationTokenSource playback;

    public bool IsPlaying => playback != null;
    void OnDisable() => playback?.Cancel();

    public void ShowLogo() => ShowLogoAsync().Forget(e =>
    {
        if (e is not OperationCanceledException) Debug.LogException(e);
    });

    public async UniTask ShowLogoAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (playback != null) throw new InvalidOperationException("A logo is already playing.");
        if (logoGroup == null || fadeCanvasGroup == null || targetRect == null)
            throw new InvalidOperationException("Logo references are missing.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        playback = source;
        var oldPosition = targetRect.anchoredPosition;
        bool oldActive = targetRect.gameObject.activeSelf;
        float oldLogo = logoGroup.alpha, oldFade = fadeCanvasGroup.alpha;
        bool oldLogoBlock = logoGroup.blocksRaycasts, oldFadeBlock = fadeCanvasGroup.blocksRaycasts;
        Sequence sequence = null;
        bool tweenAwaitStarted = false;
        try
        {
            targetRect.anchoredPosition = targetOriginPosition;
            targetRect.gameObject.SetActive(true);
            fadeCanvasGroup.blocksRaycasts = true;
            logoGroup.blocksRaycasts = true;
            sequence = DOTween.Sequence();
            sequence.Append(fadeCanvasGroup.DOFade(1f, Mathf.Max(0, fadeDuration)));
            sequence.Append(targetRect.DOAnchorPos(targetAnchorPosition, Mathf.Max(0, moveDuration)).SetEase(moveEaseGraph));
            sequence.Join(logoGroup.DOFade(1f, Mathf.Max(0, fadeDuration)));
            sequence.AppendInterval(Mathf.Max(0, logoDuration));
            sequence.Append(fadeCanvasGroup.DOFade(0f, Mathf.Max(0, fadeDuration)));
            sequence.Join(logoGroup.DOFade(0f, Mathf.Max(0, fadeDuration)));
            tweenAwaitStarted = true;
            await sequence.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, source.Token);
            source.Token.ThrowIfCancellationRequested();
        }
        finally
        {
            if (!tweenAwaitStarted) sequence?.Kill();
            if (ReferenceEquals(playback, source)) playback = null;
            if (fadeCanvasGroup != null) { fadeCanvasGroup.alpha = oldFade; fadeCanvasGroup.blocksRaycasts = oldFadeBlock; }
            if (logoGroup != null) { logoGroup.alpha = oldLogo; logoGroup.blocksRaycasts = oldLogoBlock; }
            if (targetRect != null) { targetRect.anchoredPosition = oldPosition; targetRect.gameObject.SetActive(oldActive); }
        }
    }
}
