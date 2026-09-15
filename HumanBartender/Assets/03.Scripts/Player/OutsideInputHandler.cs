using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Unity Input System의 
/// 
/// Input 메시지(OnXxx)를 받아 이벤트 채널로 브로드캐스트하는 입력 어댑터.
/// 상호작용 입력은 대화 진행(runnerAdvanceInputEvent)과 상호작용 시도(interactPressedEvent) 두 채널에 동시에 알린다.
/// </summary>
public class OutsideInputHandler : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Player interactor;

    [Header("Event")]
    [SerializeField] private InteractorEvent interactPressedEvent;
    [SerializeField] private VoidEvent runnerAdvanceInputEvent;
    [SerializeField] private Vector2Event onMoveEvent;
    [SerializeField] private BoolEvent setSpeedEvent;


    /// <summary>상호작용 입력. 대사 진행 입력과 상호작용 시도 이벤트를 함께 발생시킨다.</summary>
    public void OnInteract(InputValue value)
    {
        runnerAdvanceInputEvent?.Raise(new Void());
        interactPressedEvent?.Raise(interactor);
    }
    /// <summary>이동 입력 벡터를 이벤트로 전달한다.</summary>
    public void OnMove(InputValue value)
    {
        onMoveEvent?.Raise(value.Get<Vector2>());
    }
    /// <summary>달리기 입력 On/Off를 이벤트로 전달한다.</summary>
    public void OnSprint(InputValue value)
    {
        if (value.isPressed)
        {
            setSpeedEvent?.Raise(true);
        }
        else
        {
            setSpeedEvent?.Raise(false);
        }
    }
}
