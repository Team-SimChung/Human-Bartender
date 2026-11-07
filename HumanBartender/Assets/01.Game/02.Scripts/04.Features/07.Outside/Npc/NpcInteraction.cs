using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

public enum NpcSequenceTrigger
{
    [InspectorName("CSV · 연출 ID로 호출")] Named,
    [InspectorName("CSV · 등록된 명령으로 호출")] WorldAction,
    [InspectorName("대화 정상 종료 후 실행")] DialogueCompleted,
    [InspectorName("대화 시작 전 실행")] BeforeDialogue
}
public enum NpcActionKind
{
    [InspectorName("상대 이동")] MoveBy,
    [InspectorName("대상에게 접근")] Approach,
    [InspectorName("대상 바라보기")] Face,
    [InspectorName("애니메이션 재생")] Animation,
    [InspectorName("대기")] Wait,
    [InspectorName("화면 암전")] ScreenFadeOut,
    [InspectorName("화면 복귀")] ScreenFadeIn,
    [InspectorName("이동 후 퇴장")] Depart,
    [InspectorName("목표 완료 알림")] ObjectivePopup,
    [InspectorName("NPC 표시")] Show,
    [InspectorName("NPC 숨김")] Hide,
    [InspectorName("기준 위치로 이동")] MoveTo,
    [InspectorName("기준 위치에 배치")] SetPosition,
    [InspectorName("페이드 후 숨김")] FadeOut,
    [InspectorName("효과음")] Sound,
    [InspectorName("이동하며 페이드 퇴장")] DepartWhileMoving,
    [InspectorName("목적지로 이동하며 퇴장")] MoveToAndFade,
    [InspectorName("포물선 이동")] ArcMove,
    [InspectorName("플레이어 위치 맞추기")] MovePlayer,
    [InspectorName("플레이어 위치 설정")] SetPlayerPosition,
    [InspectorName("플레이어 바라보기 방향")] FacePlayer,
    [InspectorName("카메라 구도 맞추기")] CameraFocus,
    [InspectorName("카메라 이동")] CameraPan,
    [InspectorName("카메라 추적 복귀")] CameraRestore,
    [InspectorName("카메라 구도 즉시 설정")] CameraSet
}

public enum NpcPositionOrigin
{
    [InspectorName("현재 위치")] Current,
    [InspectorName("처음 배치 위치")] Initial,
    [InspectorName("연결한 대상 위치")] Target,
    [InspectorName("월드 원점")] World
}

public enum NpcOffsetMode
{
    [InspectorName("작성한 X/Y 그대로")] Fixed,
    [InspectorName("대상의 현재 같은 쪽")] SameSide,
    [InspectorName("대상 반대 방향으로")] AwayFromTarget
}

public enum NpcPresencePhase { Any, CommuteIn, CommuteOut }

[Serializable]
public sealed class NpcProximityAnimation
{
    public Animator animator;
    public string stateName = "Shake";
    public string returnState = "Idle";
    [Min(0)] public float duration = .9f;
    [Min(.01f)] public float radius = .85f;
    public string when;
    [Tooltip("처음 재생할 때 기록할 기존 진행 플래그. 비우면 범위를 벗어난 뒤 다시 재생할 수 있습니다.")]
    public string completionFlag;
}

[Serializable]
public sealed class NpcActorBinding
{
    public NpcActor actor;
    public bool visibleOutsideSession = true;
    [Tooltip("이 배우의 추가 표시 조건. 대화 중에는 동작 목록에서 표시·숨김을 제어합니다.")]
    public string presenceCondition;
}

[Serializable]
public sealed class NpcActionStep
{
    public NpcActionKind kind;
    [Tooltip("이 동작을 실행할 NPC. 비워 두면 대표 Actor를 사용합니다.")]
    public NpcActor performer;
    [Tooltip("기준 위치로 이동·배치할 때의 기준입니다. Offset을 더한 위치를 사용합니다.")]
    public NpcPositionOrigin positionOrigin;
    [Tooltip("기준 위치로 이동할 때의 속도 변화입니다.")]
    public NpcMotionEase movementEase;
    [Tooltip("대상의 높이 대신 실행 NPC의 현재 Y/Z를 유지하고 X만 이동합니다.")]
    public bool horizontalOnly;
    [Tooltip("접근·바라보기 대상. 비워 두면 현재 씬의 플레이어를 사용합니다.")]
    public Transform target;
    [Min(0)] public float distance = .65f;
    public Vector2 offset;
    public NpcOffsetMode offsetMode;
    [Min(0), Tooltip("0이면 이동 시간 사용. 양수이면 거리/속도로 시간을 계산합니다.")]
    public float unitsPerSecond;
    [Min(0)] public float arcHeight = .55f;
    public bool keepFacing;
    public bool clampPlayerLeft;
    public bool cameraWithMovement;
    public bool cameraMidpoint;
    public Vector2 cameraOffset;
    [Min(.01f)] public float cameraWidth = 4.8f;
    [Min(0)] public float fadeInTogether;
    public string stateName = "Punch";
    public string returnState = "Idle";
    [Min(0)] public float duration = .35f;
    [Tooltip("이동하며 페이드 퇴장에서 페이드가 시작되기 전 이동 시간입니다.")]
    [Min(0)] public float walkDuration = 1f;
    public NpcMotionEase fadeEase;
    public string text;
}

[Serializable]
public sealed class NpcSequence
{
    [Tooltip("이 Interaction 안에서 고유한 연출 ID입니다. 'CSV · 연출 ID로 호출'에서는 CSV의 type=npc-sequence, arg=이 ID로 연결합니다. 등록된 명령 방식에서는 ID 대신 선택한 명령으로 연결합니다.")]
    public string id;
    public NpcSequenceTrigger trigger;
    public OutsideStoryWorldAction worldAction;
    [Tooltip("기존 조건식입니다. 대화 종료 연출은 조건이 맞을 때만 실행합니다.")]
    public string requiredCondition;
    public NpcActionStep[] actions = Array.Empty<NpcActionStep>();
}

/// <summary>Inspector의 동작 목록을 실행한다. 대사·결제·영속 플래그 기록은 기존 CSV 실행기가 소유한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WorldInteractionTarget))]
public sealed class NpcInteraction : MonoBehaviour, IWorldInteractionParticipant, IWorldInteractionSequenceParticipant, IWorldInteractionPreparationParticipant
{
    [Header("NPC 연결")]
    [SerializeField] private NpcActor actor;
    [SerializeField] private bool actorVisibleOutsideSession = true;
    [SerializeField] private string actorPresenceCondition;
    [SerializeField] private NpcActorBinding[] additionalActors = Array.Empty<NpcActorBinding>();
    [Tooltip("숨겨진 NPC가 대화 도중 등장하는 사건에서는 끕니다.")]
    [SerializeField] private bool requireVisibleActorToInteract = true;
    [Tooltip("종료 시 모든 배우를 시작 자세로 복원한 뒤 현재 등장 조건을 적용합니다. 반복 대화·여러 배우 사건에서 사용합니다.")]
    [SerializeField] private bool restorePoseAfterInteraction;
    [Header("등장 조건")]
    [Tooltip("CSV 사건의 일차·출퇴근·조건·완료 플래그를 표시 조건으로 사용합니다. 여러 날 상주하는 NPC는 끄고 아래 조건을 설정합니다.")]
    [SerializeField] private bool useStoryPresence = true;
    [SerializeField, Min(0)] private int minimumDay;
    [Tooltip("-1이면 마지막 일차 제한 없음")]
    [SerializeField] private int maximumDay = -1;
    [SerializeField] private NpcPresencePhase presencePhase;
    [Tooltip("최소 일차에는 퇴근부터 표시하고, 그 다음 날부터는 출퇴근 모두 표시합니다.")]
    [SerializeField] private bool minimumDayStartsOnReturn;
    [Tooltip("추가 표시 조건. 비우면 추가 제한이 없습니다.")]
    [SerializeField] private string presenceCondition;
    [Tooltip("이 조건이 참이면 중단해도 시작 자세로 되돌리지 않습니다. 비우면 CSV의 완료 플래그를 사용합니다.")]
    [SerializeField] private string committedCondition;
    [Header("연출 목록")]
    [SerializeField] private NpcSequence[] sequences = Array.Empty<NpcSequence>();
    [Header("접근 시 주변 애니메이션")]
    [SerializeField] private NpcProximityAnimation proximityAnimation;
    private CancellationTokenSource proximityAction;
    private bool wasInsideProximity;
    private bool resetProximityAnimation;

    private IConditionUtil conditions;
    private ISceneFadeService fades;
    private IPopupService popups;
    [Inject] private ISoundEffectPlayer sound;
    private Transform player;
    private NpcSceneStaging staging;
    private Vector3 playerSessionStart;
    private bool completedSession;
    private WorldInteractionContent content;
    [NonSerialized] private NewSceneData story;
    private bool initialized;
    private bool playing;
    private CancellationTokenSource interaction;
    private readonly Dictionary<NpcActor, Vector3> initialPositions = new();
    private readonly Dictionary<NpcActor, NpcActor.PoseSnapshot> startingPoses = new();

    private IEnumerable<NpcActor> Actors
    {
        get
        {
            if (actor != null) yield return actor;
            foreach (var binding in additionalActors ?? Array.Empty<NpcActorBinding>())
                if (binding?.actor != null) yield return binding.actor;
        }
    }

    public bool CanInteract => initialized && isActiveAndEnabled && actor != null && actor.isActiveAndEnabled &&
        (!requireVisibleActorToInteract || actor.IsVisible) && Actors.All(value => value.isActiveAndEnabled && !value.IsBusy) &&
        !playing && interaction == null && IsPresent;

    private bool IsPresent
    {
        get
        {
            if (!initialized) return false;
            var state = GameStateManager.Instance;
            return state.CurrentDay >= minimumDay && (maximumDay < 0 || state.CurrentDay <= maximumDay) &&
                (!minimumDayStartsOnReturn || state.CurrentDay > minimumDay || state.GameFlow == EGameFlow.CommuteOut) &&
                (presencePhase == NpcPresencePhase.Any ||
                    presencePhase == NpcPresencePhase.CommuteIn && state.GameFlow == EGameFlow.CommuteIn ||
                    presencePhase == NpcPresencePhase.CommuteOut && state.GameFlow == EGameFlow.CommuteOut) &&
                conditions.CheckRequired(presenceCondition) &&
                (content == null || content.Definition.HasValue) &&
                (!useStoryPresence || WorldInteractionSceneRules.IsEligible(story, gameObject.scene.name,
                    state.CurrentDay, state.GameFlow, conditions) &&
                    OutsideStorySceneRules.IsAvailable(story.Id, state.CurrentDay, state.GameFlow, conditions));
        }
    }

    [Inject]
    public void Initialize(IConditionUtil conditionUtil, WorldInteractionSceneContext context,
        ISceneFadeService sceneFades, IPopupService popupService)
    {
        if (context == null || context.Player == null)
            throw new InvalidOperationException("NPC 연출의 현재 씬 플레이어가 없습니다.");
        var target = GetComponent<WorldInteractionTarget>();
        var definition = default(NewSceneData);
        if (target.Content == null)
        {
            var source = gameObject.scene.name == "Home" ? NewDataLoadManager.HomeStory : NewDataLoadManager.StreetStory;
            definition = (source.Scenes ?? Array.Empty<NewSceneData>()).Single(value => value.Id == target.StorySceneId);
            if (OutsideStorySceneRules.TryBind(definition, gameObject.scene.name, out var bound)) definition = bound;
        }
        Initialize(conditionUtil, context.Player.transform, definition, sceneFades, popupService);
    }

    internal void Initialize(IConditionUtil conditionUtil, Transform playerTransform, NewSceneData definition,
        ISceneFadeService sceneFades, IPopupService popupService)
    {
        if (actor == null || actor.gameObject.scene != gameObject.scene ||
            playerTransform != null && playerTransform.gameObject.scene != gameObject.scene)
            throw new InvalidOperationException("NPC의 Actor와 현재 씬 플레이어 연결을 확인하세요.");
        conditions = conditionUtil ?? throw new ArgumentNullException(nameof(conditionUtil));
        player = playerTransform;
        content = GetComponent<WorldInteractionTarget>().Content;
        if (content != null && useStoryPresence)
            throw new InvalidOperationException("Content NPC는 CSV 사건 표시 조건 대신 Content의 등장 조건을 사용하세요: " + name);
        story = definition;
        fades = sceneFades;
        popups = popupService;
        staging = GetComponent<NpcSceneStaging>();
        if (staging != null) staging.Initialize(player);
        ValidateActors();
        ValidateProximityAnimation();
        foreach (var value in Actors)
            if (!initialPositions.ContainsKey(value)) initialPositions.Add(value, value.transform.position);
        ValidateSequences();
        initialized = true;
        foreach (var value in Actors) value.SetConversation(player, false);
        RefreshPresence();
    }

    private void Awake() { foreach (var value in Actors) value.SetVisible(false); }
    private void Update()
    {
        if (!initialized || interaction != null) return;
        ResetProximityAnimation();
        RefreshPresence();
        UpdateProximityAnimation();
    }

    private void UpdateProximityAnimation()
    {
        var cue = proximityAnimation;
        if (cue?.animator == null || player == null || !IsPresent || !conditions.CheckRequired(cue.when)) return;
        bool inside = Mathf.Abs(player.position.x - transform.position.x) < cue.radius &&
            Mathf.Abs(player.position.y - transform.position.y) < 1.6f;
        if (!inside) { wasInsideProximity = false; return; }
        if (wasInsideProximity || proximityAction != null || UIDisplayOptions.IsOptionOpen) return;
        var controller = WorldInteractionController.ForScene(gameObject.scene);
        if (controller != null && controller.IsRunning) return;
        if (!string.IsNullOrWhiteSpace(cue.completionFlag) && conditions.CheckRequired("flag." + cue.completionFlag)) return;
        wasInsideProximity = true;
        PlayProximityAnimationAsync(cue).Forget(Debug.LogException);
    }

    private async UniTask PlayProximityAnimationAsync(NpcProximityAnimation cue)
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        proximityAction = source;
        try
        {
            if (!string.IsNullOrWhiteSpace(cue.completionFlag)) conditions.ApplyRequired("flag." + cue.completionFlag + " = true");
            cue.animator.Play(cue.stateName, 0, 0);
            cue.animator.Update(0);
            await UniTask.Delay(TimeSpan.FromSeconds(cue.duration), cancellationToken: source.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            resetProximityAnimation = true;
            ResetProximityAnimation();
            if (ReferenceEquals(proximityAction, source)) proximityAction = null;
        }
    }

    private void ResetProximityAnimation()
    {
        var cue = proximityAnimation;
        if (!resetProximityAnimation || cue?.animator == null || !cue.animator.isActiveAndEnabled) return;
        cue.animator.Play(cue.returnState, 0, 0);
        cue.animator.Update(0);
        resetProximityAnimation = false;
    }

    private void ValidateProximityAnimation()
    {
        var cue = proximityAnimation;
        if (cue?.animator == null) return;
        if (cue.animator.gameObject.scene != gameObject.scene ||
            !cue.animator.transform.IsChildOf(transform) || cue.animator.runtimeAnimatorController == null ||
            !float.IsFinite(cue.duration) || cue.duration <= 0 || !float.IsFinite(cue.radius) || cue.radius <= 0 ||
            string.IsNullOrWhiteSpace(cue.stateName) || string.IsNullOrWhiteSpace(cue.returnState))
            throw new InvalidOperationException("접근 시 애니메이션의 연결·상태·시간·범위를 확인하세요: " + name);
        if (!cue.animator.HasState(0, Animator.StringToHash(cue.stateName)) ||
            !cue.animator.HasState(0, Animator.StringToHash(cue.returnState)))
            throw new InvalidOperationException("접근 시 애니메이션의 재생·복귀 상태가 없습니다: " + name);
    }
    private void RefreshPresence()
    {
        if (actor == null || !initialized) return;
        bool present = isActiveAndEnabled && IsPresent;
        SetPresence(actor, present && actorVisibleOutsideSession && conditions.CheckRequired(actorPresenceCondition));
        foreach (var binding in additionalActors)
            SetPresence(binding.actor, present && binding.visibleOutsideSession && conditions.CheckRequired(binding.presenceCondition));
    }

    private static void SetPresence(NpcActor value, bool visible)
    {
        if (value != null && value.IsVisible != visible) value.SetVisible(visible);
    }

    public void BeginInteraction()
    {
        if (!CanInteract) throw new InvalidOperationException("현재 NPC 대화를 시작할 수 없습니다.");
        ValidateSequences();
        startingPoses.Clear();
        foreach (var value in Actors) startingPoses.Add(value, value.CapturePose());
        foreach (var value in Actors) value.SetConversation(player, true);
        playerSessionStart = player != null ? player.position : default;
        completedSession = false;
        staging?.BeginSession();
        interaction = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
    }

    public async UniTask PrepareInteractionAsync(CancellationToken token)
    {
        RequireSession();
        foreach (var sequence in sequences)
            if (sequence.trigger == NpcSequenceTrigger.BeforeDialogue && conditions.CheckRequired(sequence.requiredCondition))
                await PlaySequence(sequence, token);
    }

    public UniTask PlayWorldActionAsync(OutsideStoryWorldAction action, CancellationToken token)
    {
        var sequence = sequences.SingleOrDefault(value => value.trigger == NpcSequenceTrigger.WorldAction && value.worldAction == action);
        if (sequence == null) throw new InvalidOperationException(name + "에 CSV 연출 명령을 연결하세요: " + action);
        return PlaySequence(sequence, token);
    }

    public UniTask PlaySequenceAsync(string sequenceId, CancellationToken token)
    {
        var sequence = sequences.SingleOrDefault(value => value.id == sequenceId);
        if (string.IsNullOrWhiteSpace(sequenceId) || sequence == null)
            throw new InvalidOperationException(name + "에 연출 ID를 연결하세요: " + sequenceId);
        return PlaySequence(sequence, token);
    }

    public async UniTask CompleteInteractionAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        RequireSession();
        var sessionToken = interaction.Token;
        sessionToken.ThrowIfCancellationRequested();
        foreach (var sequence in sequences)
        {
            token.ThrowIfCancellationRequested();
            if (sequence.trigger == NpcSequenceTrigger.DialogueCompleted && conditions.CheckRequired(sequence.requiredCondition))
                await PlaySequence(sequence, token);
        }
        token.ThrowIfCancellationRequested();
        sessionToken.ThrowIfCancellationRequested();
        completedSession = true;
    }

    private void RequireSession()
    {
        if (interaction == null || playing || !isActiveAndEnabled)
            throw new InvalidOperationException("NPC 대화가 없거나 이미 연출 중입니다.");
    }

    private async UniTask PlaySequence(NpcSequence sequence, CancellationToken token)
    {
        RequireSession();
        if (!conditions.CheckRequired(sequence.requiredCondition))
            throw new InvalidOperationException("NPC 연출 조건이 충족되지 않았습니다: " + sequence.id);
        ValidateActions(sequence);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, interaction.Token);
        var executor = new NpcSequenceExecutor(transform, actor, player, playerSessionStart,
            initialPositions, staging, fades, popups, sound);
        playing = true;
        try
        {
            await executor.ExecuteAsync(sequence.actions, source);
        }
        finally
        {
            // 병렬 이동·카메라 중 한쪽이 실패해도 나머지가 연출 밖에서 계속 실행되지 않게 한다.
            source.Cancel();
            try
            {
                // 취소·설정 누락으로 연출이 끝나도 검은 화면이 남지 않게 한다.
                await executor.RecoverScreenAsync();
            }
            finally { playing = false; }
        }
    }

    private void ValidateActors()
    {
        if (additionalActors == null || additionalActors.Any(value => value == null || value.actor == null))
            throw new InvalidOperationException("추가 NPC Actor 연결을 확인하세요: " + name);
        var values = Actors.ToArray();
        if (values.Distinct().Count() != values.Length || values.Any(value => value.gameObject.scene != gameObject.scene ||
            value.transform != transform && !value.transform.IsChildOf(transform)))
            throw new InvalidOperationException("NPC Actor는 중복 없이 같은 상호작용 대상 아래에 연결하세요: " + name);
    }

    private void ValidateSequences()
    {
        NpcSequenceValidator.ValidateSequences(sequences, Actors, name);
    }

    private void ValidateActions(NpcSequence sequence)
    {
        NpcSequenceValidator.ValidateActions(sequence, Actors);
    }

    public void EndInteraction()
    {
        var source = interaction;
        interaction = null;
        source?.Cancel();
        source?.Dispose();
        staging?.EndSession(!completedSession);
        foreach (var value in Actors) value.SetConversation(player, false);
        if (source != null && actor != null)
        {
            string committed = !string.IsNullOrWhiteSpace(committedCondition) ? committedCondition :
                !string.IsNullOrEmpty(story.CompletionFlag) ? "flag." + story.CompletionFlag : null;
            bool restore = restorePoseAfterInteraction || string.IsNullOrEmpty(committed) || !conditions.CheckRequired(committed);
            foreach (var pair in startingPoses)
            {
                if (pair.Key == null) continue;
                if (restore) pair.Key.RestorePose(pair.Value);
                else pair.Key.CancelAction();
            }
            startingPoses.Clear();
        }
        RefreshPresence();
    }

    private void OnDisable()
    {
        proximityAction?.Cancel();
        EndInteraction();
        foreach (var value in Actors) value.SetVisible(false);
    }
}
