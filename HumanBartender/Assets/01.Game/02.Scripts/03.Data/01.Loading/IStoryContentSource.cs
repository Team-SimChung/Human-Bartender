/// <summary>현재 로더가 공개한 대본만 조회한다. 별도 데이터 사본이나 플레이어 상태를 보유하지 않는다.</summary>
public interface IStoryContentSource
{
    Step[] GetSceneSteps(string id);
    NewChoiceOptionData[] GetSharedChoices(string id);
    NewDialogueStepData[] GetCutsceneSteps(string id);
}
