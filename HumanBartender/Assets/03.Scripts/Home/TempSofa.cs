using UnityEngine;
public class TempSofa : InteractiveEntity
{
    public override bool SupportsAction(NewInteractPointData definition) =>
        definition.ActionType == EActionType.System && definition.ActionRef == "home_sofa_interaction";

    public override async void Interact(IInteractor player)
    {
        if (!isActiveAndEnabled || !isInteract || isInteracting || player == null) return;

        isInteracting = true;
        isInteract = false;
        try
        {
            OnInteracted?.Raise(this);
            Debug.Log("[Sleep] 페이드 시작");
            await SceneTransitionManager.Instance.FadeOutAsync(0.5f);
            GameStateManager.Instance.CurrentDay++;
            GameStateManager.Instance.GameFlow = EGameFlow.CommuteIn;
            OnRefreshCondition?.Raise(new Void());
            await SceneTransitionManager.Instance.FadeInAsync(0.5f);
        }
        finally
        {
            isInteracting = false;
        }
    }
}
