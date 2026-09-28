using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 병따기 입력. 스페이스와 클릭 둘 다 같은 '한 번 누름'으로 취급한다.
///
/// MinigameInputHandler(SendMessages 방식)를 쓰지 않는 이유는 그쪽이 클릭만 받고 첫 클릭을
/// 게임 시작으로 소비하기 때문이다. 병따기는 첫 입력부터 판정 대상이라 그 규칙이 맞지 않는다.
/// 그래서 Pour처럼 Update에서 직접 폴링한다.
/// </summary>
public class CapInputHandler : MonoBehaviour, ICraftCapInput
{
    [SerializeField] CapManager cap;
    private bool routedInput;

    public void SetRoutedInput(bool routed)
    {
        routedInput = routed;
    }

    public void OnCapInput()
    {
        if (cap != null) cap.OnPress();
    }

    void Update()
    {
        if (cap == null || routedInput) return;

        // 같은 프레임에 둘 다 눌려도 한 번만 친다 — || 단축 평가로 자연스럽게 처리된다.
        bool pressed = WasSpacePressedThisFrame() || WasPointerPressedThisFrame();

        if (pressed) OnCapInput();
    }

    static bool WasSpacePressedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
    }

    static bool WasPointerPressedThisFrame()
    {
        // Pointer는 마우스/터치를 함께 덮는다. 누르고 있는 동안이 아니라 '누른 순간'만 본다.
        Pointer pointer = Pointer.current;
        return pointer != null && pointer.press.wasPressedThisFrame;
    }
}
