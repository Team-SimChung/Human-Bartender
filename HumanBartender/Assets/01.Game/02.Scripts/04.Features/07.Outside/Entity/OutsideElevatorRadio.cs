using UnityEngine;

/// <summary>엘리베이터 방송의 CSV 대본과 완료 이력만 제공함. 재생은 공통 세션에 맡김.</summary>
public sealed class OutsideElevatorRadio : MonoBehaviour
{
    [SerializeField] private WorldInteractionContent content;
    public bool IsAvailable => content != null && content.IsAvailable;
    public Step[] Steps => content != null && content.Dialogue.HasValue ? content.Dialogue.Value.Steps : null;
    public string DialogueSceneId => content != null && content.Dialogue.HasValue ? content.Dialogue.Value.Id : null;

    public void CompletePlayback(string sceneId, StoryExecutionResult result)
    {
        if (content != null) content.CompleteDialogue(sceneId, result);
    }
}
