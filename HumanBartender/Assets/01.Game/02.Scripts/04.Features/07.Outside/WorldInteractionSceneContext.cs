using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>외부·홈의 CSV 연결과 플레이어 시작 위치, 씬 준비 상태를 관리함.</summary>
public sealed class WorldInteractionSceneContext : MonoBehaviour
{
    [SerializeField] private NewInteractPointDataSO interactPointData;
    [SerializeField] private NewStreetDataSO streetData;
    [SerializeField] private Player player;
    [SerializeField] private DialogueRunner runner;
    [Inject] private IConditionUtil conditions;
    [Inject] private IObjectResolver resolver;
    private readonly List<WorldInteractionContent> contents = new();
    private readonly HashSet<string> onceHistory = new(StringComparer.Ordinal);
    private readonly UniTaskCompletionSource<bool> readiness = new();
    private WorldInteractionController controller;
    private WorldAmbientDialogueController ambient;
    private readonly Dictionary<string, NewSceneData> sceneStories = new(StringComparer.Ordinal);
    private int currentDay;
    private EGameFlow currentPhase;

    public Player Player => player;
    public DialogueRunner Runner => runner;
    public bool IsReady { get; private set; }
    public string InitializationError { get; private set; }
    public bool IsSafeForSave => IsReady && runner != null && !runner.IsRunning &&
        (controller == null || controller.CanSave);
    public UniTask<bool> WaitUntilReadyAsync() => readiness.Task;

    private async void Start()
    {
        try
        {
            var token = this.GetCancellationTokenOnDestroy();
            await NewDataLoadManager.WaitUntilLoadedAsync(token);
            foreach (var spots in FindObjectsByType<OutsideSpotManager>(FindObjectsSortMode.None))
                if (spots.gameObject.scene == gameObject.scene) await spots.EnsureRegisteredAsync(token);
            if (player == null || runner == null || interactPointData == null || streetData == null || conditions == null)
                throw new InvalidOperationException("외부·Home의 초기화 참조가 없습니다.");
            controller = WorldInteractionController.ForScene(gameObject.scene);
            ambient = GetComponent<WorldAmbientDialogueController>();
            var stories = gameObject.scene.name == "Home" ? NewDataLoadManager.HomeStory : NewDataLoadManager.StreetStory;
            foreach (var story in stories.Scenes ?? Array.Empty<NewSceneData>()) sceneStories.Add(story.Id, story);
            var sources = new HashSet<string>(StringComparer.Ordinal);
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var content in root.GetComponentsInChildren<WorldInteractionContent>(true))
                {
                    if (!sources.Add(content.SourceId)) throw new InvalidOperationException("중복된 source ID: " + content.SourceId);
                    contents.Add(content);
                    content.Initialize(this);
                }
                foreach (var target in root.GetComponentsInChildren<WorldInteractionTarget>(true))
                    if (target.Action is MonoBehaviour action) resolver.Inject(action);
            }
            RefreshContents();
            if (!player.TrySetPosition()) throw new InvalidOperationException("현재 씬의 플레이어 시작 위치가 없습니다.");
            IsReady = true;
            readiness.TrySetResult(true);
        }
        catch (OperationCanceledException) { readiness.TrySetResult(false); }
        catch (Exception error)
        {
            ReportArrivalFailure(error.Message);
            readiness.TrySetResult(false);
            Debug.LogException(error, this);
        }
    }

    public void RefreshContents()
    {
        currentDay = GameStateManager.Instance.CurrentDay;
        currentPhase = GameStateManager.Instance.GameFlow;
        foreach (var content in contents)
            if (content != null) content.Refresh(IsContentRunning(content));
        if (gameObject.scene.name != "Home") return;
        string required = GameStateManager.Instance.GameFlow == EGameFlow.CommuteOut ? "home_sofa" : "home_exit_door";
        foreach (var content in contents)
            if (content.SourceId == required && content.IsAvailable) return;
        throw new InvalidOperationException("필수 Home 상호작용을 사용할 수 없습니다: " + required);
    }

    private bool IsContentRunning(WorldInteractionContent content) =>
        controller != null && controller.IsRunning && controller.ActiveTarget != null && controller.ActiveTarget.Content == content ||
        ambient != null && ambient.IsRunningFor(content);

    private void Update()
    {
        if (!IsReady || currentDay == GameStateManager.Instance.CurrentDay && currentPhase == GameStateManager.Instance.GameFlow) return;
        try { RefreshContents(); }
        catch (Exception error) { ReportArrivalFailure(error.Message); Debug.LogException(error, this); }
    }

    internal NewInteractPointData? SelectDefinition(string sourceId)
    {
        return WorldInteractionContentRules.SelectDefinition(interactPointData.interactPointData,
            sourceId, GameStateManager.Instance.GameFlow, conditions);
    }

    internal NewSceneData? SelectDialogue(NewInteractPointData definition)
    {
        var flow = WorldInteractionContentRules.SelectDialogue(definition.DialogueFlows,
            GameStateManager.Instance.CurrentDay, conditions, onceHistory);
        if (!flow.HasValue) return null;
        if (!sceneStories.TryGetValue(flow.Value.SceneId, out var scene) && !streetData.TryGetSceneData(flow.Value.SceneId, out scene))
            throw new InvalidOperationException("CSV 대화를 찾을 수 없습니다: " + flow.Value.SceneId);
        if (!string.IsNullOrEmpty(scene.CompletionFlag) && conditions.CheckRequired("flag." + scene.CompletionFlag)) return null;
        return scene;
    }

    internal bool Check(string condition) => conditions.CheckRequired(condition);

    public void CompleteDialogue(string sceneId, StoryExecutionResult result)
    {
        if (result.Completed && !string.IsNullOrEmpty(sceneId)) onceHistory.Add(sceneId);
    }

    public void ReportArrivalFailure(string reason)
    {
        IsReady = false;
        InitializationError = reason;
    }

    private void OnDestroy() => readiness.TrySetResult(false);

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(InitializationError)) return;
        GUI.Box(new Rect(20, 20, 500, 85), "씬 준비 오류");
        GUI.Label(new Rect(30, 45, 480, 45), InitializationError);
    }
}

/// <summary>기존 CSV의 우선순위·일차·한 번 재생 규칙을 공통으로 판단함.</summary>
internal static class WorldInteractionContentRules
{
    internal static NewInteractPointData? SelectDefinition(IEnumerable<NewInteractPointData> rows,
        string sourceId, EGameFlow phase, IConditionUtil conditions)
    {
        foreach (var row in (rows ?? Array.Empty<NewInteractPointData>()).OrderByDescending(ValuePriority).ThenBy(ValueId, StringComparer.Ordinal))
            if (row.SourceId == sourceId && (row.Phase == phase || row.Phase == EGameFlow.Both) && conditions.CheckRequired(row.SpawnWhen))
                return row;
        return null;
    }

    internal static NewInteractDialogueFlowData? SelectDialogue(IEnumerable<NewInteractDialogueFlowData> flows,
        int day, IConditionUtil conditions, ISet<string> completed)
    {
        foreach (var flow in (flows ?? Array.Empty<NewInteractDialogueFlowData>()).OrderBy(FlowOrder).ThenBy(FlowId, StringComparer.Ordinal))
        {
            if (flow.Day.HasValue && flow.Day.Value != day) continue;
            if (!conditions.CheckRequired(flow.When)) continue;
            if (flow.PlayType == EPlayType.Once && completed.Contains(flow.SceneId)) continue;
            return flow;
        }
        return null;
    }

    private static int ValuePriority(NewInteractPointData row) => row.Priority;
    private static string ValueId(NewInteractPointData row) => row.Id;
    private static int FlowOrder(NewInteractDialogueFlowData row) => row.FlowSeq;
    private static string FlowId(NewInteractDialogueFlowData row) => row.SceneId;
}
