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
    public bool IsReady { get; private set; }

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
            soundManager?.PlayBGM("BGM_outside");
            IsReady = true;
            RefreshEntity();
            if (player != null && player.TryGetComponent<Player>(out var actor)) actor.setpos();
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { IsReady = false; Debug.LogError($"[EntityManager] 초기화 실패: {e}"); }
    }

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
        var rows = InteractPointData.interactPointData ?? Array.Empty<NewInteractPointData>();
        foreach (var pair in entities)
        {
            var target = pair.Value;
            if (target == null) continue;
            try
            {
                var selected = SelectDefinition(rows, pair.Key, GameStateManager.Instance.GameFlow, conditionUtil.CheckRequired);
                if (!selected.HasValue)
                {
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
            }
        }
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
