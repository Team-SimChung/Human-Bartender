using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Play 씬의 대화 진행, 좌석 이동, 주문 되묻기 입력을 각 기능으로 전달한다.
/// </summary>
public class PlayInputHandler : MonoBehaviour
{
    [Header("References")]
    [Tooltip("2부 대본 실행기. 대본이 도는 동안 진행 입력을 받는다.")]
    [SerializeField] private StoryFlow storyFlow;
    [SerializeField] private PlayCamera playCamera;
    [SerializeField] private PlayPhaseController playPhaseController;
    [SerializeField] private ServicePanelController servicePanelController;
    [SerializeField] private RecipeBrowserScreen recipeScreen;
    [SerializeField] private CraftPrepStageHost craftPrepStageHost;
    [SerializeField] private RepeatOrderView repeatOrder;

    const float SkipInterval = 0.12f;
    bool skipHeld;
    float skipElapsed;

    bool CanUseDialogueInput => playPhaseController != null && playPhaseController.CanReceiveInput(EPlayPhase.Dialogue)
        && !UIDisplayOptions.IsOptionOpen && (servicePanelController == null || !servicePanelController.BlocksBarInput)
        && (recipeScreen == null || !recipeScreen.IsOpen);

    public void OnAdvance(InputValue value)
    {
        if (value.isPressed && CanUseDialogueInput) storyFlow?.TryAdvance();
    }

    public void OnSkipDialogue(InputValue value)
    {
        skipHeld = isActiveAndEnabled && value.isPressed && CanUseDialogueInput;
        skipElapsed = 0f;
    }

    void Update()
    {
        if (!skipHeld) return;
        if (!CanUseDialogueInput)
        {
            ResetSkip();
            return;
        }
        if (storyFlow == null || !storyFlow.CanAdvanceDialogue || (playCamera != null && playCamera.IsMoving)
            || CoasterDragItem.Active != null || DrinkDragItem.Active != null || Time.timeScale == 0f)
        {
            skipElapsed = 0f;
            return;
        }

        skipElapsed += Time.unscaledDeltaTime;
        if (skipElapsed < SkipInterval) return;
        skipElapsed = 0f;
        storyFlow.TryAdvance();
    }

    void ResetSkip()
    {
        skipHeld = false;
        skipElapsed = 0f;
    }

    void OnDisable() => ResetSkip();
    void OnApplicationFocus(bool focused)
    {
        if (!focused) ResetSkip();
    }

    /// <summary>영업 중 또는 바 둘러보기 실습 중 왼쪽으로 한 자리 이동한다.</summary>
    public void OnLeft(InputValue value) => MoveSeat(value, -1);

    /// <summary>영업 중 또는 바 둘러보기 실습 중 오른쪽으로 한 자리 이동한다.</summary>
    public void OnRight(InputValue value) => MoveSeat(value, 1);

    public void OnRepeatOrder(InputValue value)
    {
        if (value.isPressed) repeatOrder.Submit();
    }

    void MoveSeat(InputValue value, int step)
    {
        if (UIDisplayOptions.IsOptionOpen) return;
        // SendMessages also forwards key release; move only on the press.
        if (!value.isPressed || playPhaseController == null) return;
        if (playPhaseController.CanReceiveInput(EPlayPhase.Dialogue))
        {
            if (servicePanelController != null && servicePanelController.BlocksBarInput) return;
            if (recipeScreen != null && recipeScreen.IsOpen) return;
            storyFlow?.TryMoveTutorialSeat(step);
            return;
        }
        if (!playPhaseController.CanReceiveInput(EPlayPhase.Tycoon)) return;
        if (playCamera == null || (servicePanelController != null && !servicePanelController.CanUseGuestActions)) return;
        playCamera.MoveAdjacent(step, 0.5f);
    }

    public void OnServicePanel(InputValue value)
    {
        if (UIDisplayOptions.IsOptionOpen) return;
        if (!value.isPressed || playPhaseController == null ||
            !playPhaseController.CanReceiveInput(EPlayPhase.Tycoon) && !playPhaseController.CanReceiveInput(EPlayPhase.Dialogue)) return;
        if (servicePanelController == null) return;
        servicePanelController.Toggle();
    }

    public void OnPrepPrevious(InputValue value)
    {
        if (UIDisplayOptions.IsOptionOpen) return;
        if (value.isPressed && craftPrepStageHost != null && craftPrepStageHost.IsOpen) craftPrepStageHost.Previous();
    }

    public void OnPrepNext(InputValue value)
    {
        if (UIDisplayOptions.IsOptionOpen) return;
        if (value.isPressed && craftPrepStageHost != null && craftPrepStageHost.IsOpen) craftPrepStageHost.Next();
    }
}
