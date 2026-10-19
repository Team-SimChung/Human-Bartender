using System;
using UnityEngine;

/// <summary>interact_points의 source ID를 새 상호작용과 방송에 연결함. 입력과 UI는 처리하지 않음.</summary>
[DisallowMultipleComponent]
public sealed class WorldInteractionContent : MonoBehaviour
{
    [SerializeField] private string sourceId;
    [SerializeField] private bool useSpotPosition;
    [Tooltip("켜면 CSV의 등장 조건에 따라 오브젝트 전체를 켜고 끔. 끄면 씬의 표시 상태를 유지하고 상호작용 가능 여부만 갱신함. 문처럼 항상 보여야 하는 대상은 끔.")]
    [SerializeField] private bool controlObjectVisibility = true;
    private WorldInteractionSceneContext context;
    private Transform spot;
    public string SourceId => sourceId;
    public NewInteractPointData? Definition { get; private set; }
    public NewSceneData? Dialogue { get; private set; }
    public bool IsAvailable { get; private set; }
    public bool IsProximity => Definition.HasValue && Definition.Value.ActivationMode == EActivationMode.Proximity;

    internal void Initialize(WorldInteractionSceneContext value) => context = value;

    internal void Refresh(bool keepActiveDuringSession = false)
    {
        if (context == null || string.IsNullOrWhiteSpace(sourceId))
            throw new InvalidOperationException("CSV Source ID 또는 씬 컨텍스트가 없습니다: " + name);
        Definition = context.SelectDefinition(sourceId);
        Dialogue = null;
        IsAvailable = false;
        if (!Definition.HasValue)
        {
            if (controlObjectVisibility && !keepActiveDuringSession) gameObject.SetActive(false);
            return;
        }
        var definition = Definition.Value;
        if (definition.ActionType == EActionType.Dialogue) Dialogue = context.SelectDialogue(definition);
        IsAvailable = definition.ActionType != EActionType.None && context.Check(definition.InteractWhen) &&
            (definition.ActionType != EActionType.Dialogue || Dialogue.HasValue);
        if (useSpotPosition)
        {
            spot = FindSpot(definition.SpotId);
            if (spot == null) throw new InvalidOperationException("씬 위치를 찾을 수 없습니다: " + definition.SpotId);
            transform.position = spot.position;
        }
        if (definition.Facing is "left" or "right")
        {
            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (definition.Facing == "left" ? -1 : 1);
            transform.localScale = scale;
        }
        if (controlObjectVisibility) gameObject.SetActive(true);
    }

    private Transform FindSpot(string id)
    {
        foreach (var point in FindObjectsByType<SpotPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (point.gameObject.scene == gameObject.scene && point.SpotID == id) return point.transform;
        return null;
    }

    internal bool TryResolve(float radius, out NewSceneData story)
    {
        story = default;
        if (!IsAvailable || !Definition.HasValue) return false;
        story = Dialogue ?? new NewSceneData { Id = Definition.Value.Id };
        story.RuntimeScene = gameObject.scene.name;
        story.Route = "both";
        story.Radius = radius;
        story.StartMode = IsProximity ? ENewSceneTrigger.Auto : ENewSceneTrigger.Interact;
        return true;
    }

    public void CompleteDialogue(string sceneId, StoryExecutionResult result)
    {
        context.CompleteDialogue(sceneId, result);
        Refresh();
    }
}
