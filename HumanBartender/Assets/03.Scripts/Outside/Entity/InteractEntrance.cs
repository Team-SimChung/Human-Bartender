using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>CSV에서 선택된 이동 동작을 공통 씬 전환 서비스에 전달한다.</summary>
public class InteractEntrance : InteractiveEntity
{
    [Inject] IGameProgressionService progression;

    public override bool SupportsAction(NewInteractPointData definition)
    {
        return definition.ActionType == EActionType.Transition &&
               OutsideActions.TryGetDestination(definition.ActionRef, out _);
    }

    public override void Interact(IInteractor player)
    {
        if (!isInteract || !isActiveAndEnabled || !Definition.HasValue) return;
        var phase = Definition.Value.Phase;
        if (phase != EGameFlow.Both && phase != GameStateManager.Instance.GameFlow) return;
        if (!OutsideActions.TryGetDestination(ActionRef, out GameProgressionDestination destination)) return;
        if (progression == null)
        {
            Debug.LogError("[Outside] 하루 진행 서비스가 연결되지 않았습니다.");
            return;
        }
        ObserveTransitionAsync(destination).Forget();
    }

    async UniTask ObserveTransitionAsync(GameProgressionDestination destination)
    {
        GameProgressionResult result = await progression.EnterAsync(destination, this.GetCancellationTokenOnDestroy());
        if (result.Outcome == GameProgressionOutcome.Rejected)
            Debug.LogWarning($"[Outside] 이동 요청 거절: {result.Message}");
        else if (result.Outcome == GameProgressionOutcome.Failed)
            Debug.LogError($"[Outside] 이동 실패: {result.Message}");
    }
}
