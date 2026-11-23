using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 튜토리얼 안내 화면의 공용 동작: 화면을 켜고 내용을 페이드 인 → 쪽마다 진행 입력을 기다려 다음 쪽으로 → 마지막 쪽 뒤 내용 페이드 아웃.
/// 배치와 그림·문구는 화면 프리팹이 갖고, 이 컴포넌트는 PageRoot와 쪽 오브젝트를 켜고 끄며 내용 CanvasGroup의 투명도·위치만 바꾼다.
/// 시간은 Time.timeScale의 영향을 받지 않고, 설정창이 열려 있는 동안에는 입력을 받지 않으며 페이드와 입력 잠금 시계도 멈춘다.
/// 언제 띄울지, 무엇을 진행으로 볼지, 쪽이 뜰 때 무엇을 할지(효과음 등)는 띄우는 쪽이 정한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialPageView : MonoBehaviour
{
    /// <summary>안내 한 쪽. 쪽 오브젝트와 입력 잠금 시간은 화면 프리팹의 Inspector에서 정한다.</summary>
    [Serializable]
    public sealed class Page
    {
        [Tooltip("이 쪽에서만 켜는 오브젝트. 다른 쪽이 뜨면 꺼진다. 비우면 내용 묶음 전체가 이 쪽이다.")]
        public GameObject Root;
        [Tooltip("이 쪽이 다 나타난 뒤 이 시간 동안은 진행 입력을 받지 않는다. 직전 키 입력으로 바로 넘어가지 않게 한다.")]
        [Min(0f)] public float InputLockSeconds;
    }

    [Header("References")]
    [Tooltip("켜고 끄는 화면 전체 묶음. 배경까지 포함한다.")]
    [SerializeField] GameObject pageRoot;
    [Tooltip("페이드하는 내용 묶음의 CanvasGroup. 배경은 여기에 넣지 않아 내용이 사라져도 화면을 계속 가린다.")]
    [SerializeField] CanvasGroup contentGroup;
    [Tooltip("차례로 보여 줄 쪽. 진행할 때마다 다음 쪽으로 넘어가고, 마지막 쪽에서 진행하면 끝난다.")]
    [SerializeField] Page[] pages = { new() };

    [Header("Timing (seconds)")]
    [Tooltip("첫 쪽의 내용이 나타나는 시간. 다 나타나기 전에는 진행 입력을 받지 않는다. 0이면 바로 보인다. 웹 플레이 안내 1.5초.")]
    [SerializeField, Min(0f)] float fadeInSeconds = 1.5f;
    [Tooltip("나타날 때의 곡선. 웹 플레이 안내는 smoothstep이라 InOutSine이 가장 가깝다.")]
    [SerializeField] Ease fadeInEase = Ease.InOutSine;
    [Tooltip("나타나는 동안 내용 묶음이 이만큼(px, 960 기준) 옆에서 제자리로 온다. 음수면 왼쪽에서 온다. 0이면 제자리에서 나타난다.")]
    [SerializeField] float fadeInOffsetX;
    [Tooltip("마지막 쪽에서 진행한 뒤 내용이 사라지는 시간. 건너뛸 수 없다. 0이면 바로 사라진다. 웹 플레이 안내 3초.")]
    [SerializeField, Min(0f)] float fadeOutSeconds = 3f;
    [Tooltip("사라질 때의 곡선. 웹 플레이 안내는 일정한 속도(Linear)다.")]
    [SerializeField] Ease fadeOutEase = Ease.Linear;

    [Header("Finish")]
    [Tooltip("켜면 마지막 쪽 뒤 화면 전체(PageRoot)를 닫는다. 끄면 배경을 남겨 다음 화면이 덮을 때까지 화면을 가리고, 띄운 쪽이 Hide로 닫는다.")]
    [SerializeField] bool hideWhenFinished = true;

    Vector2 contentPosition;

    static bool IsPaused => UIDisplayOptions.IsOptionOpen;

    string ConfigurationError =>
        pageRoot == null || contentGroup == null ? "[TutorialPage] PageRoot와 내용 CanvasGroup을 연결하세요." :
        pages == null || pages.Length == 0 ? "[TutorialPage] 쪽을 하나 이상 두세요." : null;

    void Awake()
    {
        if (ConfigurationError is { } error) Debug.LogError(error, this);
        if (contentGroup != null) contentPosition = ((RectTransform)contentGroup.transform).anchoredPosition;
        Hide();
    }

    /// <summary>
    /// 화면을 켜고 첫 쪽의 내용을 페이드 인한 뒤, 진행할 때마다 다음 쪽을 보여 준다. 마지막 쪽에서 진행하면 내용을 페이드 아웃한다.
    /// Hide When Finished가 꺼져 있으면 끝난 뒤에도 PageRoot를 켜 둔다(배경이 화면을 계속 가린다). 그때는 다음 화면이 덮은 뒤 <see cref="Hide"/>로 닫는다.
    /// </summary>
    /// <param name="advance">진행 조건. 쪽마다 입력 잠금이 끝난 뒤 매 프레임 확인한다. 설정창이 열려 있으면 확인하지 않는다.</param>
    /// <param name="pageShown">쪽이 화면에 뜰 때 쪽 번호(0부터)와 함께 부른다. 효과음처럼 화면 밖의 일은 띄운 쪽이 여기서 한다.</param>
    public async UniTask ShowAsync(Func<bool> advance, CancellationToken token, Action<int> pageShown = null)
    {
        if (advance == null) throw new ArgumentNullException(nameof(advance));
        if (ConfigurationError is { } error) throw new InvalidOperationException(error);

        contentGroup.alpha = 0f;
        ShowPage(0);
        pageRoot.SetActive(true);
        pageShown?.Invoke(0);
        await FadeAsync(1f, fadeInSeconds, fadeInEase, fadeInOffsetX, token);
        for (int i = 0; i < pages.Length; i++)
        {
            if (i > 0)
            {
                ShowPage(i);
                pageShown?.Invoke(i);
            }
            await WaitForAdvanceAsync(pages[i] != null ? pages[i].InputLockSeconds : 0f, advance, token);
        }
        await FadeAsync(0f, fadeOutSeconds, fadeOutEase, 0f, token);
        if (hideWhenFinished) Hide();
    }

    public void Hide()
    {
        if (pageRoot != null) pageRoot.SetActive(false);
    }

    void ShowPage(int index)
    {
        for (int i = 0; i < pages.Length; i++)
            if (pages[i]?.Root != null) pages[i].Root.SetActive(i == index);
    }

    static async UniTask WaitForAdvanceAsync(float lockSeconds, Func<bool> advance, CancellationToken token)
    {
        float locked = lockSeconds;
        while (true)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            // 설정창이 열려 있으면 입력을 버리고 잠금 시계도 멈춘다.
            if (IsPaused) continue;
            if (locked > 0f)
            {
                locked -= Time.unscaledDeltaTime;
                continue;
            }
            if (advance()) return;
        }
    }

    async UniTask FadeAsync(float to, float seconds, Ease ease, float offsetX, CancellationToken token)
    {
        var content = (RectTransform)contentGroup.transform;
        if (seconds <= 0f)
        {
            contentGroup.alpha = to;
            return;
        }
        var sequence = DOTween.Sequence().Join(contentGroup.DOFade(to, seconds).SetEase(ease));
        if (offsetX != 0f)
        {
            content.anchoredPosition = contentPosition + new Vector2(offsetX, 0f);
            sequence.Join(content.DOAnchorPos(contentPosition, seconds).SetEase(ease));
        }
        sequence.SetUpdate(true).SetLink(gameObject);
        try
        {
            while (sequence.IsActive() && !sequence.IsComplete())
            {
                // 설정창이 열려 있으면 시계를 멈추고, 닫히면 멈춘 자리부터 잇는다.
                if (IsPaused == sequence.IsPlaying())
                {
                    if (IsPaused) sequence.Pause();
                    else sequence.Play();
                }
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        finally
        {
            if (sequence.IsActive()) sequence.Kill();
            if (offsetX != 0f && content != null) content.anchoredPosition = contentPosition;
        }
    }
}
