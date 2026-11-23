using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 튜토리얼 안내 화면 한 장의 공용 동작: 내용 페이드 인 → 진행 입력 대기 → 내용 페이드 아웃.
/// 배치와 문구는 화면 프리팹이 갖고, 이 컴포넌트는 PageRoot를 켜고 끄며 내용 CanvasGroup의 투명도만 바꾼다.
/// 시간은 Time.timeScale의 영향을 받지 않고, 설정창이 열려 있는 동안에는 입력을 받지 않으며 페이드 시계도 멈춘다.
/// 언제 띄울지와 무엇을 진행 입력으로 볼지는 띄우는 쪽이 정한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialPageView : MonoBehaviour
{
    [Header("References")]
    [Tooltip("켜고 끄는 화면 전체 묶음. 배경까지 포함한다.")]
    [SerializeField] GameObject pageRoot;
    [Tooltip("페이드하는 내용 묶음의 CanvasGroup. 배경은 여기에 넣지 않아 내용이 사라져도 화면을 계속 가린다.")]
    [SerializeField] CanvasGroup contentGroup;

    [Header("Timing (seconds)")]
    [Tooltip("내용이 나타나는 시간. 다 나타나기 전에는 진행 입력을 받지 않는다. 웹 플레이 안내 1.5초.")]
    [SerializeField, Min(0f)] float fadeInSeconds = 1.5f;
    [Tooltip("나타날 때의 곡선. 웹은 smoothstep이라 InOutSine이 가장 가깝다.")]
    [SerializeField] Ease fadeInEase = Ease.InOutSine;
    [Tooltip("진행 입력 뒤 내용이 사라지는 시간. 건너뛸 수 없다. 웹 플레이 안내 3초.")]
    [SerializeField, Min(0f)] float fadeOutSeconds = 3f;
    [Tooltip("사라질 때의 곡선. 웹은 일정한 속도(Linear)다.")]
    [SerializeField] Ease fadeOutEase = Ease.Linear;

    static bool IsPaused => UIDisplayOptions.IsOptionOpen;

    void Awake()
    {
        if (pageRoot == null || contentGroup == null)
            Debug.LogError("[TutorialPage] PageRoot와 내용 CanvasGroup을 연결하세요.", this);
        Hide();
    }

    /// <summary>
    /// 화면을 켜고 내용을 페이드 인한 뒤, 진행 입력을 받으면 내용을 페이드 아웃한다.
    /// 끝난 뒤에도 PageRoot는 켜 둔다(배경이 화면을 계속 가린다). 다음 화면이 덮은 뒤 <see cref="Hide"/>로 닫는다.
    /// </summary>
    public async UniTask ShowAsync(Func<bool> advancePressed, CancellationToken token)
    {
        if (advancePressed == null) throw new ArgumentNullException(nameof(advancePressed));
        if (pageRoot == null || contentGroup == null)
            throw new InvalidOperationException("[TutorialPage] PageRoot와 내용 CanvasGroup이 연결되지 않았습니다.");

        contentGroup.alpha = 0f;
        pageRoot.SetActive(true);
        await FadeAsync(1f, fadeInSeconds, fadeInEase, token);
        while (true)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            if (!IsPaused && advancePressed()) break;
        }
        await FadeAsync(0f, fadeOutSeconds, fadeOutEase, token);
    }

    public void Hide()
    {
        if (pageRoot != null) pageRoot.SetActive(false);
    }

    async UniTask FadeAsync(float to, float seconds, Ease ease, CancellationToken token)
    {
        if (seconds <= 0f)
        {
            contentGroup.alpha = to;
            return;
        }
        var tween = contentGroup.DOFade(to, seconds).SetEase(ease).SetUpdate(true).SetLink(gameObject);
        try
        {
            while (tween.IsActive() && !tween.IsComplete())
            {
                // 설정창이 열려 있으면 시계를 멈추고, 닫히면 멈춘 자리부터 잇는다.
                if (IsPaused == tween.IsPlaying())
                {
                    if (IsPaused) tween.Pause();
                    else tween.Play();
                }
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        finally
        {
            if (tween.IsActive()) tween.Kill();
        }
    }
}
