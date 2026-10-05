using UnityEngine;

/// <summary>InteractionAction(SO) 하나를 실행하는 것으로 상호작용을 위임하는 범용 엔티티.</summary>
public class InteractiveActionEntity : InteractiveEntity
{
    [SerializeField] private InteractionAction action;

    public override void Interact(IInteractor player)
    {
        action.Execute(this, player);
    }
}
