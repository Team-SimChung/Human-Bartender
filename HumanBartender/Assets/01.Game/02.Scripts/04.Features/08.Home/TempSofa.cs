using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

public class TempSofa : InteractiveEntity
{
    [Inject] IGameProgressionService progression;
    [Inject] SaveManager saves;
    InteractionStateLease inputLease;
    bool panelOpen;
    bool busy;
    int mode;
    int confirmSlot;
    string message;

    public override bool SupportsAction(NewInteractPointData definition)
    {
        return definition.ActionType == EActionType.System &&
               definition.ActionRef == "home_sofa_interaction";
    }

    public override void Interact(IInteractor player)
    {
        if (!isActiveAndEnabled || !isInteract || isInteracting || player == null) return;
        if (progression == null || saves == null)
        {
            Debug.LogError("[Home] 진행 또는 저장 서비스가 연결되지 않았습니다.");
            return;
        }

        panelOpen = true;
        mode = 0;
        message = null;
        inputLease = InteractionStateLease.Acquire(player, EInteractorState.Lock);
        isInteract = false;
    }

    void OnGUI()
    {
        if (!panelOpen) return;
        var area = new Rect((Screen.width - 540f) / 2f, (Screen.height - 370f) / 2f, 540f, 370f);
        GUI.Box(area, "귀가 후 Home");
        GUI.enabled = !busy;
        if (GUI.Button(new Rect(area.x + 22, area.y + 30, 115, 34), "저장")) { mode = 1; confirmSlot = 0; }
        if (GUI.Button(new Rect(area.x + 145, area.y + 30, 115, 34), "불러오기")) { mode = 2; confirmSlot = 0; }
        if (GUI.Button(new Rect(area.x + 268, area.y + 30, 115, 34), "취침"))
        {
            busy = true;
            panelOpen = false;
            isInteracting = true;
            SleepAsync().Forget(ReportException);
        }
        if (GUI.Button(new Rect(area.x + 391, area.y + 30, 125, 34), "닫기")) ClosePanel();
        GUI.enabled = true;
        if (!panelOpen) return;

        for (int slot = 1; slot <= SaveManager.SlotCount && mode != 0; slot++)
        {
            saves.TryDescribeSlot(slot, out string description, out bool canLoad);
            float y = area.y + 78 + (slot - 1) * 44;
            GUI.Label(new Rect(area.x + 22, y, 385, 34), description);
            GUI.enabled = !busy && (mode == 1 || canLoad);
            if (GUI.Button(new Rect(area.x + 410, y, 105, 32), mode == 1 ? "저장" : "불러오기"))
            {
                if (mode == 1)
                {
                    if (saves.HasSlotFile(slot) && confirmSlot != slot)
                    { confirmSlot = slot; message = "덮어쓰려면 같은 슬롯을 다시 누르세요."; }
                    else { saves.TrySaveSlot(slot, out message); confirmSlot = 0; }
                }
                else
                {
                    busy = true;
                    LoadAsync(slot).Forget(ReportException);
                }
            }
            GUI.enabled = true;
        }
        if (!string.IsNullOrEmpty(message)) GUI.Label(new Rect(area.x + 22, area.y + 312, 490, 42), message);
    }

    async UniTask LoadAsync(int slot)
    {
        try
        {
            GameProgressionResult result = await saves.LoadSlotAsync(slot, this.GetCancellationTokenOnDestroy());
            if (result.Succeeded) { ClosePanel(false); return; }
            message = result.Message ?? result.Outcome.ToString();
        }
        finally { busy = false; }
    }

    void ClosePanel(bool restoreInteraction = true)
    {
        panelOpen = false;
        confirmSlot = 0;
        inputLease?.Dispose();
        inputLease = null;
        if (restoreInteraction) isInteract = true;
    }

    void OnDestroy() => ClosePanel(false);

    async UniTask SleepAsync()
    {
        try
        {
            GameProgressionResult result = await progression.SleepAsync(RefreshConditions,
                this.GetCancellationTokenOnDestroy());
            if (result.Succeeded)
            {
                ClosePanel(false);
                // 다음 일차가 실외에서 시작하면 Home은 이미 언로드되었을 수 있다.
                if (this != null && gameObject.scene.isLoaded) OnInteracted?.Raise(this);
                return;
            }
            message = result.Message ?? result.Outcome.ToString();
            isInteract = true;
            isInteracting = false;
            panelOpen = true;
            if (result.Outcome == GameProgressionOutcome.Rejected)
                Debug.LogWarning($"[Sleep] 취침 요청 거절: {result.Message}");
            else if (result.Outcome == GameProgressionOutcome.Failed)
                Debug.LogError($"[Sleep] 취침 실패: {result.Message}");
        }
        finally
        {
            busy = false;
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
