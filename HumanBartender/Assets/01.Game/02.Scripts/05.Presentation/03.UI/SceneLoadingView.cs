using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일반 씬 로딩(새 게임·이어하기·다음 날 등) 동안 페이드 위에 띄우는 로딩 화면.
/// 웹 resource-loading(campaign.js·loading-screen.css)과 같이 검은 화면 가운데에서 비둘기가 쉬고,
/// "Loading" 뒤 점이 0~4개로 바뀐다. 바 출입은 별도의 문 화면(BarDoorTransitionView)이 맡는다.
/// 구조는 프리팹에 있고, 이 컴포넌트는 LoadingRoot를 켜고 끄며 프레임·점만 바꾼다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SceneLoadingView : MonoBehaviour, ISceneTransitionOverlay
{
    [Header("References")]
    [Tooltip("켜고 끄는 화면 전체 묶음. 평소에는 꺼 둔다.")]
    [SerializeField] GameObject loadingRoot;
    [Tooltip("LoadingRoot의 CanvasGroup. 사라질 때 투명도를 낮춘다.")]
    [SerializeField] CanvasGroup rootGroup;
    [Tooltip("비둘기 대기 프레임을 바꿔 그리는 그림.")]
    [SerializeField] Image pigeonImage;
    [SerializeField] TMP_Text loadingLabel;
    [Tooltip("'Loading' 뒤의 점. 점 개수가 바뀌어도 'Loading' 글자가 밀리지 않게 따로 둔다.")]
    [SerializeField] TMP_Text dotsLabel;

    [Header("Pigeon")]
    [Tooltip("Pigeon_idle 시트의 12프레임(Pigeon_idle_0~11). 웹과 같은 그림이다.")]
    [SerializeField] Sprite[] pigeonFrames;
    [Tooltip("초당 프레임 수. 웹 loading-screen.css 2초 12단계 = 6.")]
    [SerializeField, Min(1f)] float pigeonFramesPerSecond = 6f;

    [Header("Text")]
    [SerializeField] string loadingText = "Loading";
    [Tooltip("점 개수가 바뀌는 간격(초). 웹 0.1초.")]
    [SerializeField, Min(.01f)] float dotStepSeconds = .1f;
    [Tooltip("점의 최대 개수. 웹은 0~4개를 돈다.")]
    [SerializeField, Min(0)] int maxDots = 4;

    [Header("Timing (seconds)")]
    [Tooltip("로딩이 빨리 끝나도 이만큼은 보여 준다. 웹은 따로 두지 않는다(0).")]
    [SerializeField, Min(0f)] float minimumDisplaySeconds;
    [Tooltip("새 씬이 밝아질 때 로딩 화면이 사라지는 시간.")]
    [SerializeField, Min(0f)] float hideFadeSeconds = .25f;

    float shownAt;
    bool showing;
    Tween hideTween;

    /// <summary>시간 t(초)에 보여 줄 비둘기 프레임 번호.</summary>
    public static int FrameAt(float time, float framesPerSecond, int frameCount) =>
        frameCount <= 0 ? 0 : (int)Mathf.Repeat(Mathf.Floor(Mathf.Max(0f, time) * Mathf.Max(1f, framesPerSecond)), frameCount);

    /// <summary>시간 t(초)에 보여 줄 점 개수(0~maxDots).</summary>
    public static int DotsAt(float time, float stepSeconds, int maxDots) =>
        maxDots <= 0 ? 0 : (int)Mathf.Repeat(Mathf.Floor(Mathf.Max(0f, time) / Mathf.Max(.01f, stepSeconds)), maxDots + 1);

    void Awake()
    {
        if (loadingRoot == null || rootGroup == null || pigeonImage == null || loadingLabel == null || dotsLabel == null)
            Debug.LogError("[SceneLoading] LoadingRoot·CanvasGroup·비둘기·글자를 연결하세요.", this);
        HideImmediate();
    }

    public UniTask ShowAsync(SceneTransitionStyle style, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        hideTween?.Kill();
        shownAt = Time.unscaledTime;
        showing = true;
        if (loadingLabel != null) loadingLabel.text = loadingText;
        if (rootGroup != null) rootGroup.alpha = 1f;
        Draw(0f);
        // 페이드로 이미 화면이 검게 덮인 뒤에 불리므로 바로 켠다.
        if (loadingRoot != null) loadingRoot.SetActive(true);
        return UniTask.CompletedTask;
    }

    public async UniTask FinishAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        float remaining = shownAt + minimumDisplaySeconds - Time.unscaledTime;
        if (remaining > 0f)
            await UniTask.Delay(TimeSpan.FromSeconds(remaining), DelayType.UnscaledDeltaTime, cancellationToken: cancellationToken);
    }

    public async UniTask HideAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (rootGroup != null && hideFadeSeconds > 0f && loadingRoot != null && loadingRoot.activeSelf)
        {
            hideTween = rootGroup.DOFade(0f, hideFadeSeconds).SetUpdate(true);
            await hideTween.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken);
        }
        HideImmediate();
    }

    public void HideImmediate()
    {
        hideTween?.Kill();
        hideTween = null;
        showing = false;
        if (loadingRoot != null) loadingRoot.SetActive(false);
        if (rootGroup != null) rootGroup.alpha = 1f;
    }

    void Update()
    {
        // 전환 중 게임 시간이 멈춰도 움직이도록 실제 시간을 쓴다(웹 CSS 애니메이션과 같다).
        if (showing) Draw(Time.unscaledTime - shownAt);
    }

    void Draw(float elapsed)
    {
        if (pigeonImage != null && pigeonFrames != null && pigeonFrames.Length > 0)
            pigeonImage.sprite = pigeonFrames[FrameAt(elapsed, pigeonFramesPerSecond, pigeonFrames.Length)];
        if (dotsLabel != null) dotsLabel.text = new string('.', DotsAt(elapsed, dotStepSeconds, maxDots));
    }
}
