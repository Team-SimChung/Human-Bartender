using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>
/// 새 게임 오프닝. 플레이 안내 → 도입 컷신 → 소파에서 기상 → 크리스 통화 → 조작 안내 순서다.
/// 실행 여부는 진행 플래그가, 안내 문구와 시간은 이 컴포넌트의 직렬화 값이, 그림은 기존 튜토리얼 오버레이가 소유한다.
/// 도입 컷신 리소스가 아직 없으면 OptionalStoryCutscene 정책대로 건너뛴다.
/// </summary>
[DisallowMultipleComponent]
public sealed class NewGameOpening : MonoBehaviour
{
    /// <summary>한 장의 안내 화면. 인스펙터에서 문구와 입력 잠금 시간을 편집한다.</summary>
    [Serializable]
    public sealed class GuidePage
    {
        public string Title;
        [TextArea(2, 5)] public string Body;
        [Tooltip("본문 아래 작은 글씨. 진행 키 안내에 쓴다.")]
        public string Footer;
        [Tooltip("이 시간 동안은 진행 입력을 받지 않는다. 직전 키 입력으로 바로 넘어가지 않게 한다.")]
        [Min(0)] public float InputLockSeconds;
        [Tooltip("뒤 화면을 가리는 정도. 1이면 완전히 가린다.")]
        [Range(0, 1)] public float ShadeAlpha = .72f;

        public GuidePage(string title, string body, string footer, float inputLock, float shade)
        {
            Title = title; Body = body; Footer = footer;
            InputLockSeconds = inputLock; ShadeAlpha = shade;
        }
    }

    // 오프닝을 끝까지 본 기록. 저장된 진행 플래그라 다시 불러와도 반복하지 않는다.
    const string SeenFlag = "flag.opening_seen";

    [Tooltip("안내 화면에 쓰는 기존 튜토리얼 오버레이 프리팹(StoryTutorialOverlay).")]
    [SerializeField] private StoryTutorialView viewPrefab;

    [Tooltip("cutscenes.csv의 도입 연출 id. 리소스가 연결되지 않았으면 건너뛴다.")]
    [SerializeField] private string introCutsceneId = "tl_intro";

    [Tooltip("도입 컷씬을 불러오는 동안 쓰는 검은 배경의 그리기 순서. 실내 UI보다 높고 컷씬 캔버스(기본 200)보다 낮게 둔다.")]
    [SerializeField] private int introBackdropSortingOrder = 199;

    [SerializeField] private GuidePage playGuide = new("플레이 안내",
        "이 게임은 인물들과의 대화를 중심으로 이야기가 진행됩니다.\n대화는 E키 또는 스페이스바로 넘길 수 있습니다.",
        "E / SPACE", 1f, 1f);

    [SerializeField] private GuidePage[] controlsPages =
    {
        new("이동 · 달리기", "A와 D를 눌러 이동합니다.\nShift를 누른 채 이동하면 달립니다.",
            "1 / 2 · E키를 눌러 진행", 2f, .72f),
        new("상호작용", "E키를 눌러 오브젝트나 다른 인물들과 상호작용할 수 있습니다.",
            "2 / 2 · E키를 눌러 진행", .3f, .72f),
    };

    [Tooltip("안내가 끝난 뒤 띄우는 새 목표.")]
    [SerializeField] private string firstObjective = "첫 출근하기!";

    [Header("첫 기상")]
    [SerializeField, Tooltip("기상 연출을 공유할 Home 씬의 소파 컴포넌트입니다.")]
    private HomeSofaAction sofaAction;

    [SerializeField, Min(0), Tooltip("꿈이 끝나고 잠든 루나가 보이기 시작한 뒤, 기상 클립을 시작하기까지의 시간(초)입니다.")]
    private float wakeUpDelay = 1.4f;

    [SerializeField, Tooltip("잠든 루나를 처음 보여줄 때 재생할 연락 효과음의 등록 키입니다.")]
    private string incomingCallSound = "SFX_incoming_call";

    [Header("크리스의 첫 통화")]
    [SerializeField, Tooltip("환복 중 암전에서 재생할 효과음의 등록 키입니다.")]
    private string dressingSound = "SFX_change_clothes";
    [SerializeField, Min(0), Tooltip("환복 효과음이 끝날 때까지 암전을 유지하는 시간(초)입니다.")]
    private float dressingDuration = 2.59f;
    [SerializeField, Tooltip("크리스 말풍선 위치와 첫 통화 CSV 장면 ID가 들어 있는 대화 대상입니다.")]
    private WorldInteractionTarget firstContact;
    [SerializeField, Tooltip("Home 씬에 배치한 홀로콜 열림 이펙트입니다.")]
    private ParticleSystem hologramOpen;
    [SerializeField, Min(0), Tooltip("열림 이펙트에서 통화 중 표시할 자세에 도달하는 시간(초)입니다.")]
    private float hologramOpenDuration = .9f;
    [SerializeField, Tooltip("Home 씬에 배치한 홀로콜 닫힘 이펙트입니다.")]
    private ParticleSystem hologramClose;
    [SerializeField, Min(0), Tooltip("닫힘 이펙트 재생 시간(초)입니다.")]
    private float hologramCloseDuration = .45f;

    [Inject] private ICutScenePlayer cutscenes;
    [Inject] private IConditionUtil conditions;
    [Inject] private IPopupService popups;
    [Inject] private ISoundEffectPlayer sound;
    [Inject] private ISceneFadeService fades;

    /// <summary>오프닝을 아직 보지 않았는지. 씬 컨텍스트가 시작 자리와 함께 판단한다.</summary>
    public bool IsPending => conditions != null && !conditions.CheckRequired(SeenFlag);

    /// <summary>플레이어 조작을 잠근 채 오프닝을 끝까지 진행하고 완료를 기록한다.</summary>
    public async UniTask PlayAsync(Player player, CancellationToken token)
    {
        if (player == null) throw new ArgumentNullException(nameof(player));
        if (conditions == null) throw new InvalidOperationException("NewGameOpening이 주입되지 않았습니다.");
        if (!IsPending) return;

        var view = StoryTutorialView.Create(viewPrefab, null);
        try
        {
            using (InteractionStateLease.Acquire(player, EInteractorState.Lock))
            {
                await ShowAsync(view, playGuide, token);
                await PlayIntroAsync(view, token);
                await PlayFirstContactAsync(player, token);
                foreach (var page in controlsPages) await ShowAsync(view, page, token);
                // 안내를 닫은 키 입력이 같은 프레임에 문·TV 상호작용으로 이어지지 않게 한 프레임 뒤에 조작을 돌려준다.
                await UniTask.NextFrame(token);
            }
            conditions.ApplyRequired(SeenFlag + " = true");
        }
        finally
        {
            if (view != null) Destroy(view.gameObject);
        }
        if (popups != null && !string.IsNullOrWhiteSpace(firstObjective))
            popups.Popup(PopupType.NewObjective, firstObjective).Forget();
    }

    private async UniTask PlayIntroAsync(StoryTutorialView view, CancellationToken token)
    {
        var sleep = sofaAction != null ? sofaAction.SleepAnimation : null;
        if (sleep == null) throw new InvalidOperationException("[NewGameOpening] 소파의 기상 연출을 연결하세요.");

        bool sleepStarted = false;
        try
        {
            await view.KeepScreenCoveredAsync(introBackdropSortingOrder, async () =>
            {
                await OptionalStoryCutscene.PlayAsync(cutscenes, introCutsceneId, token);
                sleep.BeginSleep();
                sleepStarted = true;
                sleep.PrepareWakeUp();
                // 수면 스프라이트와 카메라 구도를 적용한 프레임부터 실내를 보여준다.
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, token);
            });

            if (!string.IsNullOrWhiteSpace(incomingCallSound)) sound?.PlaySE(incomingCallSound);
            await sleep.WakeUpAsync(fades, token, wakeUpDelay, PlayDressingAsync);
        }
        finally
        {
            if (sleepStarted) sleep.Restore();
        }
    }

    private UniTask PlayDressingAsync(CancellationToken token)
    {
        if (!string.IsNullOrWhiteSpace(dressingSound)) sound?.PlaySE(dressingSound);
        return UniTask.Delay(TimeSpan.FromSeconds(dressingDuration), ignoreTimeScale: true, cancellationToken: token);
    }

    private async UniTask PlayFirstContactAsync(Player player, CancellationToken token)
    {
        var controller = WorldInteractionController.ForScene(gameObject.scene);
        if (controller == null || firstContact == null || hologramOpen == null || hologramClose == null)
            throw new InvalidOperationException("[NewGameOpening] 첫 통화 대상과 홀로콜 이펙트를 연결하세요.");

        try
        {
            if (player.TryGetComponent(out PlayerMovement2D movement))
                movement.Face(firstContact.SpeechAnchor.position.x > player.transform.position.x);
            await PlayHologramAsync(hologramOpen, hologramOpenDuration, token);
            var result = await controller.PlayScriptedAsync(firstContact, token);
            token.ThrowIfCancellationRequested();
            if (!result.Completed) throw new InvalidOperationException("[NewGameOpening] 첫 통화를 완료할 수 없습니다. " + result.Error);

            hologramOpen.gameObject.SetActive(false);
            await PlayHologramAsync(hologramClose, hologramCloseDuration, token);
        }
        finally
        {
            if (hologramOpen != null) hologramOpen.gameObject.SetActive(false);
            if (hologramClose != null) hologramClose.gameObject.SetActive(false);
        }
    }

    private static async UniTask PlayHologramAsync(ParticleSystem effect, float duration, CancellationToken token)
    {
        effect.gameObject.SetActive(true);
        effect.Simulate(0, withChildren: true, restart: true);
        // 마지막 프레임을 통화가 끝날 때까지 유지한다.
        float remaining = duration;
        while (remaining > 0)
        {
            await UniTask.NextFrame(token);
            if (UIDisplayOptions.IsOptionOpen) continue;
            float step = Mathf.Min(remaining, Time.unscaledDeltaTime);
            effect.Simulate(step, withChildren: true, restart: false, fixedTimeStep: false);
            remaining -= step;
        }
    }

    static UniTask ShowAsync(StoryTutorialView view, GuidePage page, CancellationToken token) =>
        view.ShowPageAsync(page.Title, page.Body, page.Footer, page.ShadeAlpha, page.InputLockSeconds,
            () => !UIDisplayOptions.IsOptionOpen &&
                  DialogueAdvanceInput.PressedKey(includeInteraction: true) != null, token);

    private void OnValidate()
    {
        if (viewPrefab == null) Debug.LogWarning("[NewGameOpening] View Prefab(StoryTutorialOverlay)을 연결하세요.", this);
        if (string.IsNullOrWhiteSpace(introCutsceneId)) Debug.LogWarning("[NewGameOpening] 도입 컷신 id가 비어 있습니다.", this);
        if (sofaAction == null) Debug.LogWarning("[NewGameOpening] Sofa Action(HomeSofaAction)을 연결하세요.", this);
        if (firstContact == null || hologramOpen == null || hologramClose == null)
            Debug.LogWarning("[NewGameOpening] 첫 통화 대상과 홀로콜 열림·닫힘 이펙트를 연결하세요.", this);
    }
}
