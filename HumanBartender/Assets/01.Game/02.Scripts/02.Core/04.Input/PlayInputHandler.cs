using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Play(Bar) 씬 전용 입력 어댑터. 스페이스바로 대사를 진행시키고, Q/E로 카메라 슬롯(왼쪽/오른쪽)을 전환한다.
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

    /// <summary>
    /// 대사 진행 입력. 2부(Dialogue) 국면에서만 동작한다.
    ///
    /// 실행기가 스스로 "지금 내가 받는다"를 답하게 둔다. 대본이 돌지 않는 사이의 입력은 그냥 버린다 —
    /// 예전에는 구형 DialogueRunner로 흘려보냈는데, 그 경로는 걷어냈다.
    /// </summary>
    public void OnAdvance(InputValue value)
    {
        if (playPhaseController == null || !playPhaseController.CanReceiveInput(EPlayPhase.Dialogue)) return;
        if (servicePanelController != null && servicePanelController.IsMenuOpen) return;
        if (recipeScreen != null && recipeScreen.IsOpen) return;

        if (storyFlow != null) storyFlow.TryAdvance();
    }

    /// <summary>Q: 영업 중 또는 바 둘러보기 실습 중 왼쪽으로 한 자리 이동한다.</summary>
    public void OnLeft(InputValue value) => MoveSeat(value, -1);

    /// <summary>E: 영업 중 또는 바 둘러보기 실습 중 오른쪽으로 한 자리 이동한다.</summary>
    public void OnRight(InputValue value) => MoveSeat(value, 1);

    void MoveSeat(InputValue value, int step)
    {
        // SendMessages also forwards key release; move only on the press.
        if (!value.isPressed || playPhaseController == null) return;
        if (playPhaseController.CanReceiveInput(EPlayPhase.Dialogue))
        {
            if (servicePanelController != null && servicePanelController.IsMenuOpen) return;
            if (recipeScreen != null && recipeScreen.IsOpen) return;
            storyFlow?.TryMoveTutorialSeat(step);
            return;
        }
        if (!playPhaseController.CanReceiveInput(EPlayPhase.Tycoon)) return;
        if (playCamera == null) return;
        playCamera.MoveAdjacent(step, 0.5f);
    }

    public void OnServicePanel(InputValue value)
    {
        if (!value.isPressed || playPhaseController == null ||
            !playPhaseController.CanReceiveInput(EPlayPhase.Tycoon) && !playPhaseController.CanReceiveInput(EPlayPhase.Dialogue)) return;
        if (servicePanelController == null) return;
        servicePanelController.Toggle();
    }

    public void OnPrepPrevious(InputValue value)
    {
        if (craftPrepStageHost != null && craftPrepStageHost.IsOpen) craftPrepStageHost.Previous();
    }

    public void OnPrepNext(InputValue value)
    {
        if (craftPrepStageHost != null && craftPrepStageHost.IsOpen) craftPrepStageHost.Next();
    }
}
