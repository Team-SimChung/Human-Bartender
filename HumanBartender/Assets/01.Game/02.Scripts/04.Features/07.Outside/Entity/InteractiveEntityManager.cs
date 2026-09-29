using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>CSV 조건에 맞는 행과 대화를 선택하여 씬 엔티티에 연결한다.</summary>
public class InteractiveEntityManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] protected NewInteractPointDataSO InteractPointData;
    [SerializeField] protected NewStreetDataSO streetData;
    [SerializeField] protected NewSpotDataSO SpotData;
    [SerializeField] GameObject player;
    [SerializeField] protected ITrackedbleEvent OnTrackedText;
    [SerializeField] protected DialogueRunner runner;
    [SerializeField] protected OutsideDialoguePresenter presenter;
    [SerializeField] protected InteractableEvent OnInteracted;
    [SerializeField] protected VoidEvent OnRefreshCondition;

    [Inject] ISoundManager soundManager;
    [Inject] IConditionUtil conditionUtil;
    [Inject] IObjectResolver resolver;
    readonly Dictionary<string, InteractiveEntity> entities = new(StringComparer.Ordinal);
    readonly Dictionary<string, SpotPoint> sceneSpots = new(StringComparer.Ordinal);
    readonly HashSet<string> onceHistory = new(StringComparer.Ordinal);
    readonly UniTaskCompletionSource<bool> readiness = new();
    public bool IsReady { get; private set; }
    public string InitializationError { get; private set; }
    public bool IsSafeForSave => IsReady && (runner == null || !runner.IsRunning) &&
        entities.Values.All(entity => entity == null || !entity.IsInteracting);
    public UniTask<bool> WaitUntilReadyAsync() => readiness.Task;
    public void ReportArrivalFailure(string reason)
    {
        IsReady = false;
        InitializationError = string.IsNullOrWhiteSpace(reason) ? "도착 후 화면 전환에 실패했습니다." : reason;
    }

    async void Start()
    {
        try
        {
            var token = this.GetCancellationTokenOnDestroy();
            await NewDataLoadManager.WaitUntilLoadedAsync(token);
            foreach (var spots in FindObjectsByType<OutsideSpotManager>(FindObjectsSortMode.None))
                if (spots.gameObject.scene == gameObject.scene) await spots.EnsureRegisteredAsync(token);
            token.ThrowIfCancellationRequested();
            if (InteractPointData == null || streetData == null || SpotData == null || conditionUtil == null)
                throw new InvalidOperationException("Interaction data or conditions are missing.");

            foreach (var spot in FindObjectsByType<SpotPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (spot.gameObject.scene == gameObject.scene && !string.IsNullOrWhiteSpace(spot.SpotID) &&
                    !sceneSpots.TryAdd(spot.SpotID, spot))
                    throw new InvalidOperationException("Duplicate scene spot_id: " + spot.SpotID);

            foreach (var entity in GetComponentsInChildren<InteractiveEntity>(true))
            {
                if (string.IsNullOrWhiteSpace(entity.souceid)) continue;
                if (!entities.TryAdd(entity.souceid, entity))
                    throw new InvalidOperationException("Duplicate scene source_id: " + entity.souceid);
                resolver?.Inject(entity);
                if (entity is InteractiveNPCEntity npc)
                    npc.Init(OnInteracted, OnRefreshCondition, OnTrackedText, runner, presenter, this);
                else entity.Init(OnInteracted, OnRefreshCondition);
            }
            RefreshEntityCore();
            if (player == null || !player.TryGetComponent<Player>(out var actor) || !actor.TrySetPosition())
                throw new InvalidOperationException("Player 또는 현재 씬의 안전 시작 위치가 연결되지 않았습니다.");
            soundManager?.PlayBGM("BGM_outside");
            IsReady = true;
            readiness.TrySetResult(true);
        }
        catch (OperationCanceledException) { readiness.TrySetResult(false); }
        catch (Exception e)
        {
            IsReady = false;
            InitializationError = e.Message;
            readiness.TrySetResult(false);
            Debug.LogError($"[EntityManager] 초기화 실패: {e}");
        }
    }

    void OnDestroy() => readiness.TrySetResult(false);

    // 같은 source_id는 높은 priority, 동률이면 고정 ID 순으로 선택한다.
    public static NewInteractPointData? SelectDefinition(IEnumerable<NewInteractPointData> rows,
        string sourceId, EGameFlow phase, Func<string, bool> check)
    {
        foreach (var row in rows.Where(r => r.SourceId == sourceId)
                     .OrderByDescending(r => r.Priority).ThenBy(r => r.Id, StringComparer.Ordinal))
            if ((row.Phase == phase || row.Phase == EGameFlow.Both) && check(row.SpawnWhen)) return row;
        return null;
    }

    public void RefreshEntity()
    {
        if (!IsReady) return;
        try { RefreshEntityCore(); }
        catch (Exception error)
        {
            IsReady = false;
            InitializationError = error.Message;
            Debug.LogError($"[EntityManager] 필수 상호작용 갱신 실패: {error}");
        }
    }

    void RefreshEntityCore()
    {
        var rows = InteractPointData.interactPointData ?? Array.Empty<NewInteractPointData>();
        string requiredSource = gameObject.scene.name == "Home"
            ? GameStateManager.Instance.GameFlow == EGameFlow.CommuteOut ? "home_sofa" : "home_exit_door"
            : null;
        bool requiredReady = requiredSource == null;
        string requiredError = null;
        foreach (var pair in entities)
        {
            var target = pair.Value;
            if (target == null) continue;
            try
            {
                var selected = SelectDefinition(rows, pair.Key, GameStateManager.Instance.GameFlow, conditionUtil.CheckRequired);
                if (!selected.HasValue)
                {
                    if (pair.Key == requiredSource)
                        requiredError = $"필수 Home 상호작용 데이터가 없습니다: {requiredSource}";
                    target.BindContent(null, null, false);
                    target.gameObject.SetActive(false);
                    continue;
                }
                var row = selected.Value;
                if (!SpotData.TryGetData(row.SpotId, out var spot) || !sceneSpots.TryGetValue(row.SpotId, out var anchor) || anchor == null)
                    throw new InvalidOperationException($"Missing spot: {row.SpotId} ({row.Id})");
                spot.Position = anchor.transform.position;
                spot.Rotation = anchor.transform.rotation;
                NewSceneData? dialogue = null;
                bool allowed = row.ActionType != EActionType.None && conditionUtil.CheckRequired(row.InteractWhen);
                if (row.ActionType == EActionType.Dialogue)
                {
                    var flow = SelectDialogue(row);
                    if (flow.HasValue)
                    {
                        if (!streetData.TryGetSceneData(flow.Value.SceneId, out var scene))
                            throw new InvalidOperationException("Missing dialogue: " + flow.Value.SceneId);
                        dialogue = scene;
                    }
                    allowed &= dialogue.HasValue;
                }
                if (row.ActionType != EActionType.None && !target.SupportsAction(row))
                    throw new InvalidOperationException($"{target.GetType().Name} cannot run {row.ActionType}/{row.ActionRef} ({row.Id})");
                target.BindContent(row, dialogue, allowed);
                if (pair.Key == requiredSource)
                {
                    requiredReady = allowed;
                    if (!allowed) requiredError = $"필수 Home 상호작용을 사용할 수 없습니다: {requiredSource}";
                }
                target.ApplySpot(spot);
                if (row.Facing is "left" or "right")
                {
                    var scale = target.transform.localScale;
                    scale.x = Mathf.Abs(scale.x) * (row.Facing == "left" ? -1 : 1);
                    target.transform.localScale = scale;
                }
                target.gameObject.SetActive(true);
            }
            catch (Exception e)
            {
                target.BindContent(null, null, false);
                target.gameObject.SetActive(false);
                Debug.LogError($"[EntityManager] {pair.Key}: {e.Message}");
                if (pair.Key == requiredSource) requiredError = e.Message;
            }
        }
        if (!requiredReady || requiredError != null)
            throw new InvalidOperationException(requiredError ?? $"필수 Home 엔티티가 없습니다: {requiredSource}");
    }

    void OnGUI()
    {
        if (string.IsNullOrEmpty(InitializationError)) return;
        var area = new Rect((Screen.width - 450f) / 2f, 20f, 450f, 110f);
        GUI.Box(area, "씬 준비에 실패했습니다.");
        GUI.Label(new Rect(area.x + 15f, area.y + 28f, 420f, 40f), InitializationError);
        if (GUI.Button(new Rect(area.x + 125f, area.y + 70f, 200f, 30f), "타이틀로 돌아가기"))
            SceneTransitionManager.Instance?.LoadScene("Main");
    }

    NewInteractDialogueFlowData? SelectDialogue(NewInteractPointData row)
    {
        foreach (var flow in (row.DialogueFlows ?? Array.Empty<NewInteractDialogueFlowData>())
                     .OrderBy(f => f.FlowSeq).ThenBy(f => f.SceneId, StringComparer.Ordinal))
        {
            if (flow.Day.HasValue && flow.Day.Value != GameStateManager.Instance.CurrentDay) continue;
            if (!conditionUtil.CheckRequired(flow.When)) continue;
            if (flow.PlayType == EPlayType.Once && onceHistory.Contains(flow.SceneId)) continue;
            return flow;
        }
        return null;
    }

    public void CompleteDialogue(string sceneId, StoryExecutionResult result)
    {
        if (result.Completed && !string.IsNullOrEmpty(sceneId)) onceHistory.Add(sceneId);
    }
}
