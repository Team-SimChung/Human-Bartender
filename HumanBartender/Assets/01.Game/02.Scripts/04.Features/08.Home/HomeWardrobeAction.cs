using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>
/// 집 옷장의 버튼(웹 home-interactions.js). 누를 때마다 열림↔닫힘 전환 클립을 재생하고 마지막 칸을 유지한다.
/// 한 번도 누르지 않았으면 선택지와 함께 안내 문구를, 처음 누르면 전환 뒤 후속 대사를 보여 준다.
/// 두 상태는 저장되는 진행 플래그에 두므로 씬 재진입·다음 날·불러오기 뒤에도 유지된다.
/// </summary>
[DisallowMultipleComponent]
public sealed class HomeWardrobeAction : MonoBehaviour, IWorldInteractionAction
{
    private static readonly int OpenState = Animator.StringToHash("Base Layer.Open");
    private static readonly int CloseState = Animator.StringToHash("Base Layer.Close");

    [Header("옷장 그림")]
    [SerializeField, Tooltip("이 오브젝트의 Animator입니다. Wardrobe 컨트롤러의 Open·Close 상태를 코드가 이름으로 재생하며 전이·파라미터는 쓰지 않습니다.")]
    private Animator animator;
    [SerializeField, Tooltip("닫힘→열림 전환 클립(wardrobe_change1, 9칸)입니다. 컨트롤러 Open 상태의 클립과 같아야 하며, 길이·프레임 속도를 여기서 읽습니다.")]
    private AnimationClip openClip;
    [SerializeField, Tooltip("열림→닫힘 전환 클립(wardrobe_change2, 9칸)입니다. 컨트롤러 Close 상태의 클립과 같아야 합니다.")]
    private AnimationClip closeClip;
    [SerializeField, Min(.01f), Tooltip("전환 재생 배율입니다. 1이면 클립에 저장한 속도(12fps, 0.75초)로 재생합니다.")]
    private float playbackSpeed = 1;

    [Header("진행 기록 (저장 파일에 포함)")]
    [SerializeField, Tooltip("한 번이라도 눌렀는지 기록하는 플래그 이름(flag. 뒤)입니다. 꺼져 있으면 선택지와 함께 안내 문구가 나오고, 처음 누를 때 후속 대사가 나옵니다.")]
    private string pressedFlag = "wardrobe_pressed";
    [SerializeField, Tooltip("지금 열려 있는지 기록하는 플래그 이름(flag. 뒤)입니다. 씬에 들어오면 이 값에 맞는 마지막 칸을 보여 줍니다.")]
    private string openFlag = "wardrobe_open";

    [Header("CSV 대사")]
    [SerializeField, Tooltip("한 번도 누르지 않았을 때 선택지와 함께 루나 위에 띄울 안내 문구(say 행 하나)가 있는 장면 ID입니다.")]
    private string promptSceneId = "home-wardrobe";
    [SerializeField, Tooltip("처음 누른 뒤 전환이 끝나면 재생할 대사 장면 ID입니다. 키나 말풍선 클릭으로 넘깁니다.")]
    private string pressedSceneId = "home-wardrobe-pressed";

    [Inject] private IConditionUtil conditions;
    [Inject] private IStoryContentSource content;
    private CancellationTokenSource execution;
    // 지금 보여 주는 마지막 칸(true 열림, false 닫힘). null이면 다음 Update에서 저장 값에 맞춘다.
    private bool? shownOpen;

    public bool CanExecute => isActiveAndEnabled && execution == null && conditions != null && content != null &&
        animator != null && openClip != null && closeClip != null;
    internal bool IsPressed => conditions.CheckRequired("flag." + pressedFlag);
    internal bool IsOpen => conditions.CheckRequired("flag." + openFlag);

#if UNITY_EDITOR
    /// <summary>테스트가 한 프레임에 흐른 시간(초)을 대신 돌려준다. 비어 있으면 실제 다음 프레임을 기다린다.</summary>
    internal Func<CancellationToken, UniTask<float>> FrameStepForTest { get; set; }
#endif

    // Animator는 다시 켜질 때 기본 상태로 돌아가므로 저장 값에 맞는 칸을 다시 보여 준다.
    private void OnEnable() => shownOpen = null;

    private void OnDisable()
    {
        execution?.Cancel();
        shownOpen = null;
    }

    // 씬 준비 중 주입이 끝난 뒤와 테스트 도구로 플래그를 바꾼 뒤에도 그림을 저장 값에 맞춘다.
    private void Update() => SynchronizePose();

    /// <summary>재생 중이 아니면 저장된 열림 기록에 맞는 마지막 칸을 보여 준다. 주입 전이거나 이미 같은 칸이면 그대로 둔다.</summary>
    internal void SynchronizePose()
    {
        if (execution != null || conditions == null || animator == null || !animator.isActiveAndEnabled) return;
        bool open = IsOpen;
        if (shownOpen == open) return;
        // 한 번도 누르지 않은 옷장도 닫힘 클립의 마지막 칸(닫힌 그림)으로 둔다. 클립은 재생하지 않는다.
        ShowPose(open ? OpenState : CloseState, 1);
        shownOpen = open;
    }

    public async UniTask ExecuteAsync(WorldInteractionActionContext context, CancellationToken token)
    {
        if (!CanExecute || context.ChooseMenu == null || context.PlayDialogue == null)
            throw new InvalidOperationException("[HomeWardrobe] 옷장 Animator·전환 클립 또는 공통 상호작용(선택창·대사) 연결을 확인하세요.");
        Validate();
        bool first = !IsPressed;
        // CSV 문구를 먼저 읽어, 없으면 기록과 그림을 바꾸기 전에 멈춘다.
        string prompt = first ? ReadPrompt() : null;
        var followUp = first ? ReadFollowUp() : null;
        SynchronizePose();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        execution = lifetime;
        token = lifetime.Token;
        try
        {
            var selected = await context.ChooseMenu(StreetStoryEvents.WardrobeOptions(), prompt, token);
            token.ThrowIfCancellationRequested();
            if (selected.IsCancel == true) return;
            if (selected.Id != StreetStoryEvents.WardrobePressId)
                throw new InvalidOperationException("[HomeWardrobe] 알 수 없는 선택지: " + selected.Id);

            // 웹처럼 누르는 순간 기록을 뒤집고 전환을 재생한다. 중간에 끊기면 finally가 새 기록의 마지막 칸으로 맞춘다.
            bool open = !IsOpen;
            conditions.ApplyRequired($"flag.{pressedFlag} = true; flag.{openFlag} = {(open ? "true" : "false")}");
            await PlayTransitionAsync(open, token);
            if (!first) return;

            var result = await context.PlayDialogue(followUp, null, token);
            if (result.Status == StoryExecutionStatus.Cancelled) throw new OperationCanceledException(token);
            if (!result.Completed)
                throw new InvalidOperationException("[HomeWardrobe] 처음 누른 뒤의 대사를 재생하지 못했습니다: " + result.Error);
        }
        finally
        {
            execution = null;
            shownOpen = null;
            SynchronizePose();
        }
    }

    private async UniTask PlayTransitionAsync(bool open, CancellationToken token)
    {
        var clip = open ? openClip : closeClip;
        int state = open ? OpenState : CloseState;
        shownOpen = null;
        float frameDuration = 1f / clip.frameRate;
        float elapsed = 0;
        while (elapsed < clip.length)
        {
            token.ThrowIfCancellationRequested();
            ShowPose(state, elapsed / clip.length);
            elapsed += await NextFrameAsync(frameDuration, token) * playbackSpeed;
        }
        token.ThrowIfCancellationRequested();
        // 반복하지 않는 클립의 마지막 칸을 유지한다.
        ShowPose(state, 1);
        shownOpen = open;
    }

    private async UniTask<float> NextFrameAsync(float frameDuration, CancellationToken token)
    {
#if UNITY_EDITOR
        if (FrameStepForTest != null) return await FrameStepForTest(token);
#endif
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, token);
        // 설정창이 열린 동안은 멈추고, 한 프레임이 길어져도 칸을 건너뛰지 않는다(HomeSleepAnimation과 같음).
        return UIDisplayOptions.IsOptionOpen ? 0 : Mathf.Min(Time.unscaledDeltaTime, frameDuration);
    }

    private void ShowPose(int state, float normalizedTime)
    {
        // 상태 시간은 코드가 정한다. Animator가 스스로 다음 칸으로 넘기지 않게 속도를 0으로 둔다.
        animator.speed = 0;
        animator.Play(state, 0, normalizedTime);
        animator.Update(0);
    }

    private string ReadPrompt()
    {
        var lines = (content.GetSceneSteps(promptSceneId) ?? Array.Empty<Step>()).Where(step => step.Type == "say").ToArray();
        if (lines.Length != 1 || string.IsNullOrWhiteSpace(lines[0].Text?.Ko))
            throw new InvalidOperationException("[HomeWardrobe] 안내 문구 장면에 say 행을 하나만 두세요: " + promptSceneId);
        var text = lines[0].Text;
        return GameStateManager.Instance.Language == ELanguage.En && !string.IsNullOrEmpty(text.En) ? text.En : text.Ko;
    }

    private Step[] ReadFollowUp()
    {
        var steps = content.GetSceneSteps(pressedSceneId);
        if (steps == null || steps.Length == 0)
            throw new InvalidOperationException("[HomeWardrobe] 처음 누른 뒤의 대사 장면이 비어 있습니다: " + pressedSceneId);
        return steps;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(pressedFlag) || string.IsNullOrWhiteSpace(openFlag) || pressedFlag == openFlag ||
            string.IsNullOrWhiteSpace(promptSceneId) || string.IsNullOrWhiteSpace(pressedSceneId))
            throw new InvalidOperationException("[HomeWardrobe] 서로 다른 진행 플래그 두 개와 CSV 장면 ID를 입력하세요.");
        if (animator.runtimeAnimatorController == null || !animator.HasState(0, OpenState) || !animator.HasState(0, CloseState))
            throw new InvalidOperationException("[HomeWardrobe] 옷장 Animator에 Open·Close 상태가 있는 Wardrobe 컨트롤러를 연결하세요.");
        if (openClip.length <= 0 || openClip.frameRate <= 0 || closeClip.length <= 0 || closeClip.frameRate <= 0 ||
            !float.IsFinite(playbackSpeed) || playbackSpeed <= 0)
            throw new InvalidOperationException("[HomeWardrobe] 길이와 프레임 속도가 있는 전환 클립과 0보다 큰 재생 배율을 설정하세요.");
    }
}
