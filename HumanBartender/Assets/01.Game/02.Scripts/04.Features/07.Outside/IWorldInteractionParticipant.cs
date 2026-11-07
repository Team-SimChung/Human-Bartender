using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>대화에 참여하는 월드 대상의 행동 수명. 입력·대본·게임 결과는 각 기존 담당자가 소유한다.</summary>
public interface IWorldInteractionParticipant
{
    bool CanInteract { get; }
    void BeginInteraction();
    UniTask PlayWorldActionAsync(OutsideStoryWorldAction action, CancellationToken token);
    UniTask CompleteInteractionAsync(CancellationToken token);
    void EndInteraction();
}
