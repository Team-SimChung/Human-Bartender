using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>
/// 새 게임 오프닝. 플레이 안내 → 도입 컷신 → 소파에서 기상 → 크리스 통화 → 조작 안내 순서다.
/// 실행 여부는 진행 플래그가, 각 안내 화면의 그림·문구·배치·시간은 화면 프리팹(Canvas_PlayGuide, Canvas_ChoiceTutorial,
/// Canvas_ControlsTutorial)이 소유한다. 이 컴포넌트는 언제 띄우고 닫을지와 효과음만 정한다.
/// 도입 컷신 리소스가 아직 없으면 OptionalStoryCutscene 정책대로 건너뛴다.
/// </summary>
[DisallowMultipleComponent]
public sealed class NewGameOpening : MonoBehaviour
{
    // 오프닝을 끝까지 본 기록. 저장된 진행 플래그라 다시 불러와도 반복하지 않는다.
    internal const string SeenFlag = "opening_seen";

    [Tooltip("cutscenes.csv의 도입 연출 id. 리소스가 연결되지 않았으면 건너뛴다.")]
    [SerializeField] private string introCutsceneId = "tl_intro";

    [Tooltip("새 게임에서 가장 먼저 띄우는 플레이 안내 화면 프리팹(Canvas_PlayGuide). 문구와 배치, 페이드 시간은 프리팹이 갖는다. " +
             "내용이 사라진 뒤에도 남는 검은 배경이 도입 컷씬을 불러와 재생하고 수면 자세를 준비하는 동안 화면을 가린다.")]
    [SerializeField] private TutorialPageView playGuidePrefab;

    [Tooltip("통화가 끝난 뒤 띄우는 이동·달리기 / 상호작용 안내 화면 프리팹(Canvas_ControlsTutorial). 쪽 그림과 쪽마다의 입력 잠금 시간은 프리팹이 갖는다.")]
    [SerializeField] private TutorialPageView controlsTutorialPrefab;

    [Tooltip("조작 안내의 각 쪽과 선택지 안내가 뜰 때 재생할 효과음의 등록 키. 비우면 소리 없이 띄운다. 플레이 안내는 소리가 없다.")]
    [SerializeField] private string guidePopupSound = "SFX_controls_popup";

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
    [SerializeField, Tooltip("첫 통화의 선택지가 열려 있는 동안 화면 왼쪽에 띄우는 선택지 조작 안내 프리팹(Canvas_ChoiceTutorial)입니다. " +
        "입력을 막지 않고, 선택을 확정해 선택지가 닫히면 사라집니다. 비우면 띄우지 않습니다.")]
    private TutorialPageView choiceTutorialPrefab;
    [SerializeField, Tooltip("Home 씬에 배치한 홀로콜 열림 이펙트입니다.")]
    private ParticleSystem hologramOpen;
    [SerializeField, Min(0), Tooltip("열림 이펙트를 이 시간(초)까지 진행한 뒤 그 모습을 통화가 끝날 때까지 멈춰 둡니다. " +
        "홀로그램 패널(Contact·Card·Fan)이 다 펴져 반복 구간에 들어가는 시간(FX_Holo_Open 기준 1.1초)보다 길어야 납작하게 멈추지 않습니다.")]
    private float hologramOpenDuration = 1.2f;
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
    public bool IsPending => conditions != null && !conditions.CheckRequired("flag." + SeenFlag);
    internal bool IntroFinished { get; private set; }

    /// <summary>플레이어 조작을 잠근 채 오프닝을 끝까지 진행하고 완료를 기록한다.</summary>
    public async UniTask PlayAsync(Player player, CancellationToken token)
    {
        if (player == null) throw new ArgumentNullException(nameof(player));
        if (conditions == null) throw new InvalidOperationException("NewGameOpening이 주입되지 않았습니다.");
        if (!IsPending) return;
        if (playGuidePrefab == null) throw new InvalidOperationException("[NewGameOpening] 플레이 안내 프리팹(Canvas_PlayGuide)을 연결하세요.");
        if (controlsTutorialPrefab == null) throw new InvalidOperationException("[NewGameOpening] 조작 안내 프리팹(Canvas_ControlsTutorial)을 연결하세요.");

        IntroFinished = false;
        TutorialPageView playGuide = null;
        try
        {
            using (InteractionStateLease.Acquire(player, EInteractorState.Lock))
            {
                playGuide = Instantiate(playGuidePrefab);
                await playGuide.ShowAsync(AdvancePressed, token);
                // 플레이 안내는 내용이 사라진 뒤에도 검은 배경을 남긴다. 그 배경이 도입 컷씬 동안 화면을 가리고,
                // 실내를 보여 줄 프레임에 닫힌다. 그 사이에 실내가 한 프레임도 보이지 않는다.
                await PlayIntroAsync(() =>
                {
                    if (playGuide == null) return;
                    playGuide.Hide();
                    Destroy(playGuide.gameObject);
                    playGuide = null;
                }, token);
                await PlayFirstContactAsync(player, token);
                await ShowControlsAsync(token);
                // 안내를 닫은 키 입력이 같은 프레임에 문·TV 상호작용으로 이어지지 않게 한 프레임 뒤에 조작을 돌려준다.
                await UniTask.NextFrame(token);
            }
            conditions.ApplyRequired("flag." + SeenFlag + " = true");
        }
        finally
        {
            IntroFinished = true;
            if (playGuide != null) Destroy(playGuide.gameObject);
        }
        if (popups != null && !string.IsNullOrWhiteSpace(firstObjective))
            popups.Popup(PopupType.NewObjective, firstObjective).Forget();
    }

    /// <param name="reveal">실내를 처음 보여 줄 프레임(수면 자세와 카메라 구도를 적용한 프레임의 다음)에 부른다. 화면을 가리던 것을 닫는다.</param>
    private async UniTask PlayIntroAsync(Action reveal, CancellationToken token)
    {
        var sleep = sofaAction != null ? sofaAction.SleepAnimation : null;
        if (sleep == null) throw new InvalidOperationException("[NewGameOpening] 소파의 기상 연출을 연결하세요.");

        bool sleepStarted = false;
        try
        {
            await OptionalStoryCutscene.PlayAsync(cutscenes, introCutsceneId, token);
            sleep.BeginSleep();
            sleepStarted = true;
            sleep.PrepareWakeUp();
            // 수면 스프라이트와 카메라 구도를 적용한 프레임까지 가린 뒤, 그다음 프레임부터 실내를 보여준다.
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, token);
            reveal();

            IntroFinished = true;
            PlaySound(incomingCallSound);
            await sleep.WakeUpAsync(fades, token, wakeUpDelay, PlayDressingAsync);
        }
        finally
        {
            if (sleepStarted) sleep.Restore();
        }
    }

    private UniTask PlayDressingAsync(CancellationToken token)
    {
        PlaySound(dressingSound);
        return UniTask.Delay(TimeSpan.FromSeconds(dressingDuration), ignoreTimeScale: true, cancellationToken: token);
    }

    private async UniTask PlayFirstContactAsync(Player player, CancellationToken token)
    {
        var controller = WorldInteractionController.ForScene(gameObject.scene);
        if (controller == null || firstContact == null || hologramOpen == null || hologramClose == null)
            throw new InvalidOperationException("[NewGameOpening] 첫 통화 대상과 홀로콜 이펙트를 연결하세요.");

        TutorialPageView choiceGuide = null;
        try
        {
            if (choiceTutorialPrefab != null) choiceGuide = Instantiate(choiceTutorialPrefab);
            if (player.TryGetComponent(out PlayerMovement2D movement))
                movement.Face(firstContact.SpeechAnchor.position.x > player.transform.position.x);
            await PlayHologramAsync(hologramOpen, hologramOpenDuration, token);

            StoryExecutionResult result;
            using (var guiding = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                var guide = FollowChoicesAsync(choiceGuide, controller.View, guiding.Token);
                try { result = await controller.PlayScriptedAsync(firstContact, token); }
                finally
                {
                    guiding.Cancel();
                    await guide.SuppressCancellationThrow();
                }
            }
            token.ThrowIfCancellationRequested();
            if (!result.Completed) throw new InvalidOperationException("[NewGameOpening] 첫 통화를 완료할 수 없습니다. " + result.Error);

            hologramOpen.gameObject.SetActive(false);
            await PlayHologramAsync(hologramClose, hologramCloseDuration, token);
        }
        finally
        {
            if (choiceGuide != null) Destroy(choiceGuide.gameObject);
            if (hologramOpen != null) hologramOpen.gameObject.SetActive(false);
            if (hologramClose != null) hologramClose.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 첫 통화 동안 선택지가 열려 있을 때만 선택지 안내를 보여 주고, 선택을 확정해 닫히면 숨긴다(웹 world-choices.js의 tutorial 안내).
    /// 입력은 막지 않는다. 효과음은 처음 보일 때 한 번만 낸다. 통화가 끝나면 취소로 멈춘다.
    /// </summary>
    private async UniTask FollowChoicesAsync(TutorialPageView guide, WorldInteractionView view, CancellationToken token)
    {
        if (guide == null || view == null) return;
        bool played = false;
        while (true)
        {
            await UniTask.WaitUntil(() => view != null && view.IsChoosing, PlayerLoopTiming.PreLateUpdate, token);
            await guide.ShowAsync(() => view == null || !view.IsChoosing, token, _ =>
            {
                if (played) return;
                played = true;
                PlaySound(guidePopupSound);
            });
        }
    }

    private async UniTask ShowControlsAsync(CancellationToken token)
    {
        var controls = Instantiate(controlsTutorialPrefab);
        try
        {
            // 쪽(이동·달리기, 상호작용)이 뜰 때마다 효과음을 한 번 낸다. 마지막 쪽에서 진행하면 화면이 닫힌다.
            await controls.ShowAsync(AdvancePressed, token, _ => PlaySound(guidePopupSound));
        }
        finally
        {
            if (controls != null) Destroy(controls.gameObject);
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

    // 설정창이 열려 있는 동안의 입력은 안내 화면(TutorialPageView)이 버린다.
    private static bool AdvancePressed() => DialogueAdvanceInput.PressedKey(includeInteraction: true) != null;

    private void PlaySound(string key)
    {
        if (!string.IsNullOrWhiteSpace(key)) sound?.PlaySE(key);
    }

    private void OnValidate()
    {
        if (playGuidePrefab == null) Debug.LogWarning("[NewGameOpening] Play Guide Prefab(Canvas_PlayGuide)을 연결하세요.", this);
        if (controlsTutorialPrefab == null) Debug.LogWarning("[NewGameOpening] Controls Tutorial Prefab(Canvas_ControlsTutorial)을 연결하세요.", this);
        if (choiceTutorialPrefab == null) Debug.LogWarning("[NewGameOpening] Choice Tutorial Prefab(Canvas_ChoiceTutorial)이 비어 있어 첫 통화의 선택지 안내를 띄우지 않습니다.", this);
        if (string.IsNullOrWhiteSpace(introCutsceneId)) Debug.LogWarning("[NewGameOpening] 도입 컷신 id가 비어 있습니다.", this);
        if (sofaAction == null) Debug.LogWarning("[NewGameOpening] Sofa Action(HomeSofaAction)을 연결하세요.", this);
        if (firstContact == null || hologramOpen == null || hologramClose == null)
            Debug.LogWarning("[NewGameOpening] 첫 통화 대상과 홀로콜 열림·닫힘 이펙트를 연결하세요.", this);
    }
}
