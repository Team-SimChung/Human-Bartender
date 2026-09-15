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

    /// <summary>
    /// 대사 진행 입력. 2부(Dialogue) 국면에서만 동작한다.
    ///
    /// 실행기가 스스로 "지금 내가 받는다"를 답하게 둔다. 대본이 돌지 않는 사이의 입력은 그냥 버린다 —
    /// 예전에는 구형 DialogueRunner로 흘려보냈는데, 그 경로는 걷어냈다.
    /// </summary>
    public void OnAdvance(InputValue value)
    {
        if (playPhaseController.CurrentPhase != EPlayPhase.Dialogue) return;

        storyFlow?.TryAdvance();
    }

    /// <summary>카메라를 현재 슬롯 기준 왼쪽 옆 칸으로 전환한다. 컷씬/미니게임 또는 1부(Tycoon) 국면일 때는 무시한다.</summary>
    public void OnLeft(InputValue value)
    {
        if (playPhaseController.CurrentPhase != EPlayPhase.Tycoon) return;
        playCamera.MoveAdjacent(-1, 0.5f);
    }

    /// <summary>카메라를 현재 슬롯 기준 오른쪽 옆 칸으로 전환한다. 컷씬/미니게임 또는 1부(Tycoon) 국면일 때는 무시한다.</summary>
    public void OnRight(InputValue value)
    {
        if (playPhaseController.CurrentPhase != EPlayPhase.Tycoon) return;
        playCamera.MoveAdjacent(1, 0.5f);
    }
}
