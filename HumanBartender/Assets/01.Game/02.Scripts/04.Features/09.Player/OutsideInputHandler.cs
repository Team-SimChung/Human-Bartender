using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input System의 이동·달리기 입력을 이벤트 채널로 전달함.
/// 외부·홈의 상호작용 입력은 공통 Controller에만 전달함.
/// </summary>
public class OutsideInputHandler : MonoBehaviour
{
    [Header("Event")]
    [SerializeField] private Vector2Event onMoveEvent;
    [SerializeField] private BoolEvent setSpeedEvent;
    private WorldInteractionController worldInteractions;


    /// <summary>공통 세션에서 시작·대화 진행·선택 확정을 구분함.</summary>
    public void OnInteract(InputValue value)
    {
        if (worldInteractions == null) worldInteractions = WorldInteractionController.ForScene(gameObject.scene);
        if (!value.isPressed || worldInteractions == null || !worldInteractions.isActiveAndEnabled) return;
        worldInteractions.TryHandleInput();
    }
    /// <summary>이동 입력 벡터를 이벤트로 전달함.</summary>
    public void OnMove(InputValue value)
    {
        onMoveEvent?.Raise(value.Get<Vector2>());
    }
    /// <summary>달리기 입력 On/Off를 이벤트로 전달함.</summary>
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
