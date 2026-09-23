using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

public class TempSofa : InteractiveEntity
{
    [Inject] IGameProgressionService progression;

    public override bool SupportsAction(NewInteractPointData definition)
    {
        return definition.ActionType == EActionType.System &&
               definition.ActionRef == "home_sofa_interaction";
    }

    public override void Interact(IInteractor player)
    {
        if (!isActiveAndEnabled || !isInteract || isInteracting || player == null) return;
        if (progression == null)
        {
            Debug.LogError("[Sleep] 하루 진행 서비스가 연결되지 않았습니다.");
            return;
        }

        isInteracting = true;
        isInteract = false;
        SleepAsync().Forget(ReportException);
    }

    async UniTask SleepAsync()
    {
        try
        {
            GameProgressionResult result = await progression.SleepAsync(RefreshConditions,
                this.GetCancellationTokenOnDestroy());
            if (result.Succeeded)
            {
                // 다음 일차가 실외에서 시작하면 Home은 이미 언로드되었을 수 있다.
                if (this != null && gameObject.scene.isLoaded) OnInteracted?.Raise(this);
                return;
            }
            isInteract = true;
            if (result.Outcome == GameProgressionOutcome.Rejected)
                Debug.LogWarning($"[Sleep] 취침 요청 거절: {result.Message}");
            else if (result.Outcome == GameProgressionOutcome.Failed)
                Debug.LogError($"[Sleep] 취침 실패: {result.Message}");
        }
        finally
        {
            isInteracting = false;
        }
    }

    void RefreshConditions()
    {
        OnRefreshCondition?.Raise(new Void());
    }

    static void ReportException(System.Exception error)
    {
        Debug.LogException(error);
    }
}
