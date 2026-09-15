using UnityEngine;

/// <summary>CSV에서 선택된 이동 동작을 공통 씬 전환 서비스에 전달한다.</summary>
public class InteractEntrance : InteractiveEntity
{
    public override bool SupportsAction(NewInteractPointData definition) =>
        definition.ActionType == EActionType.Transition && OutsideActions.TryGetScene(definition.ActionRef, out _);

    public override void Interact(IInteractor player)
    {
        if (!isInteract || !isActiveAndEnabled || !Definition.HasValue) return;
        var phase = Definition.Value.Phase;
        if (phase != EGameFlow.Both && phase != GameStateManager.Instance.GameFlow) return;
        // 결과가 없는 A API 호출 때문에 플레이어 입력을 영구 잠그지 않는다.
        OutsideActions.RequestTransition(ActionRef);
    }
}
