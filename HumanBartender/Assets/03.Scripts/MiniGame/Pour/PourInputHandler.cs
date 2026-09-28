using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 포인터를 누르고 있는 동안 병이 기울어지도록 BottleTiltController에 입력값을 전달한다.
/// 드래그 거리가 아니라 단순 홀드 상태(누름=1, 뗌=0)만 넘기고, 실제 기울기 보간 속도는
/// BottleTiltController의 tiltSpeed/returnSpeed가 담당한다. 새 Input System의 Pointer.current를
/// Update에서 폴링한다(연속 입력이라 MinigameInputHandler의 SendMessages 콜백 방식 대신 사용).
/// </summary>
public class PourInputHandler : MonoBehaviour, ICraftPointerInput
{
    [SerializeField] BottleTiltController bottle;
    private bool routedInput;

    public void SetRoutedInput(bool routed)
    {
        routedInput = routed;
        if (!routed && bottle != null) bottle.SetTiltInput01(0f);
    }

    public void OnPointerInput(bool pressed)
    {
        if (bottle != null) bottle.SetTiltInput01(pressed ? 1f : 0f);
    }

    void Update()
    {
        if (routedInput) return;
        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        OnPointerInput(pointer.press.isPressed);
    }
}
