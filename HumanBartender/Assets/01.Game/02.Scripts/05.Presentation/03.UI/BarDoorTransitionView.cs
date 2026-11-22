using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// 바를 드나들 때 페이드 위에 띄우는 언노운 문 화면. 로딩 중 문구와 로딩 막대를 보여 주고, 씬이 준비되면
/// 최소 표시 시간을 채운 뒤 입점은 문을 열고 퇴점은 문을 닫는다. 그림 배치는 프리팹이, 문구와 시간은 이 컴포넌트가 소유한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class BarDoorTransitionView : MonoBehaviour, ISceneTransitionOverlay
{
    [Header("References")]
    [Tooltip("옆으로 밀려 열리는 문짝. 문틀 안쪽 마스크의 자식이어야 열린 부분이 문틀 밖으로 보이지 않는다.")]
    [SerializeField] private RectTransform leaf;
    [SerializeField] private TMP_Text caption;
    [Tooltip("로딩 막대 안에서 좌우로 오가는 조각.")]
    [SerializeField] private RectTransform loadSlider;
    [Tooltip("로딩 막대 전체. 문이 열리면 숨긴다.")]
    [SerializeField] private GameObject loadTrack;
    [Tooltip("열린 문에서 바닥으로 새는 빛. 문이 닫히면 함께 사라진다.")]
    [SerializeField] private CanvasGroup floorLight;
    [Tooltip("완성된 문 그림 위를 덮는 검은 막. 그림 레이어 자체를 투명하게 만들지 않는다.")]
    [SerializeField] private CanvasGroup fadeCurtain;

    [Header("Text")]
    [SerializeField] private string entranceLoading = "영업을 준비하고 있습니다";
    [SerializeField] private string entranceDone = "어서 오세요.";
    [SerializeField] private string exitLoading = "오늘의 영업을 마칩니다";
    [SerializeField] private string exitDone = "내일 다시 만나요.";

    [Header("Timing (seconds)")]
    [SerializeField, Min(0)] private float fadeInSeconds = .55f;
    [Tooltip("씬이 빨리 준비되어도 문 화면을 이만큼은 보여 준다(나타난 순간부터).")]
    [SerializeField, Min(0)] private float minimumDisplaySeconds = 3f;
    [Tooltip("문이 열리거나 닫히는 시간.")]
    [SerializeField, Min(0)] private float doorOpenSeconds = 1.45f;
    [Tooltip("문 동작이 끝난 뒤 인사 문구를 읽을 시간.")]
    [SerializeField, Min(0)] private float holdAfterOpenSeconds = .25f;
    [SerializeField, Min(0)] private float fadeOutSeconds = .8f;
    [Tooltip("로딩 조각이 한쪽 끝에서 반대쪽 끝까지 가는 시간.")]
    [SerializeField, Min(.1f)] private float sliderSweepSeconds = 1.8f;
    [Tooltip("문짝이 열릴 때 자기 폭의 몇 배만큼 옆으로 밀려나는지.")]
    [SerializeField, Min(0)] private float openTravel = 1.03f;

    private CanvasGroup group;
    private Vector2 leafClosed;
    private float shownAt;
    private SceneTransitionStyle style;
    private bool loading;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (leaf == null || caption == null || fadeCurtain == null) throw new InvalidOperationException("[BarDoorTransitionView] 문짝, 문구, 페이드 막을 연결하세요.");
        leafClosed = leaf.anchoredPosition;
        HideImmediate();
    }

    public async UniTask ShowAsync(SceneTransitionStyle transitionStyle, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        style = transitionStyle;
        SetDoorOpenness(style == SceneTransitionStyle.BarExit ? 1 : 0);
        caption.text = style == SceneTransitionStyle.BarExit ? exitLoading : entranceLoading;
        if (loadTrack != null) loadTrack.SetActive(true);
        loading = true;
        group.blocksRaycasts = true;
        group.alpha = 1;
        fadeCurtain.alpha = 1;
        shownAt = Time.unscaledTime;
        await TweenAlphaAsync(1, 0, fadeInSeconds, cancellationToken);
    }

    public async UniTask FinishAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        float remaining = shownAt + minimumDisplaySeconds - Time.unscaledTime;
        if (remaining > 0) await UniTask.Delay(TimeSpan.FromSeconds(remaining), DelayType.UnscaledDeltaTime, cancellationToken: cancellationToken);
        loading = false;
        if (loadTrack != null) loadTrack.SetActive(false);
        caption.text = style == SceneTransitionStyle.BarExit ? exitDone : entranceDone;
        bool exiting = style == SceneTransitionStyle.BarExit;
        float from = exiting ? 1 : 0;
        float to = exiting ? 0 : 1;
        float elapsed = 0;
        while (elapsed < doorOpenSeconds)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            elapsed += Time.unscaledDeltaTime;
            SetDoorOpenness(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, elapsed / doorOpenSeconds)));
        }
        SetDoorOpenness(to);
        if (holdAfterOpenSeconds > 0)
            await UniTask.Delay(TimeSpan.FromSeconds(holdAfterOpenSeconds), DelayType.UnscaledDeltaTime, cancellationToken: cancellationToken);
    }

    public async UniTask HideAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await TweenAlphaAsync(fadeCurtain.alpha, 1, fadeOutSeconds, cancellationToken);
        HideImmediate();
    }

    public void HideImmediate()
    {
        loading = false;
        if (group == null) group = GetComponent<CanvasGroup>();
        // 오브젝트는 켜 둔 채 투명하게만 만든다. 꺼 두면 Awake가 첫 표시 때 실행되어 방금 띄운 화면을 다시 숨긴다.
        group.alpha = 0;
        group.blocksRaycasts = false;
        if (fadeCurtain != null) fadeCurtain.alpha = 1;
        if (leaf != null) SetDoorOpenness(0);
    }

    private void SetDoorOpenness(float openness)
    {
        leaf.anchoredPosition = leafClosed + Vector2.left * leaf.rect.width * openTravel * openness;
        if (floorLight != null) floorLight.alpha = openness;
    }

    private void Update()
    {
        if (!loading || loadSlider == null || loadSlider.parent is not RectTransform track) return;
        // 막대 안을 좌우로 오간다. 화면 전환 중 게임 시간이 멈춰도 움직이도록 실제 시간을 쓴다.
        float travel = Mathf.Max(0, track.rect.width - loadSlider.rect.width);
        float t = Mathf.PingPong(Time.unscaledTime / sliderSweepSeconds, 1);
        loadSlider.anchoredPosition = new Vector2(Mathf.SmoothStep(0, travel, t), loadSlider.anchoredPosition.y);
    }

    private async UniTask TweenAlphaAsync(float from, float to, float seconds, CancellationToken cancellationToken)
    {
        float elapsed = 0;
        fadeCurtain.alpha = from;
        while (elapsed < seconds)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            elapsed += Time.unscaledDeltaTime;
            fadeCurtain.alpha = Mathf.Lerp(from, to, elapsed / seconds);
        }
        fadeCurtain.alpha = to;
    }
}
