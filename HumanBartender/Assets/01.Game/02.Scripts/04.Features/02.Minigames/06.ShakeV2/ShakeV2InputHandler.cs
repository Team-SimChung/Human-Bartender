using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 쉐이킹 V2 입력. Play 씬에서는 CraftGimmickInputRouter가 ShakingV2 맵의 입력을 넘겨 주고(OnShakeV2Input),
/// 독립 Dev 씬에서는 프리팹 루트의 PlayerInput(SendMessages, 맵 ShakingV2)이 같은 액션을 보낸다.
/// 누른 순간만 넘긴다. 설정창이 열렸거나 게임이 멈춘 동안의 입력은 버린다.
/// </summary>
public sealed class ShakeV2InputHandler : MonoBehaviour, ICraftShakeV2Input
{
    [SerializeField] ShakeV2Manager manager;

    bool routedInput;

    public void SetRoutedInput(bool routed) => routedInput = routed;

    public void OnShakeV2Input(EShakeV2Input input)
    {
        if (UIDisplayOptions.IsOptionOpen || GamePause.IsPaused) return;
        if (manager != null) manager.Press(input);
    }

    // ── Dev 씬: 프리팹 PlayerInput의 SendMessages ───────────────────────

    public void OnShakeSeal(InputValue value) => Local(EShakeV2Input.Seal, value.isPressed);
    public void OnShakeDown(InputValue value) => Local(EShakeV2Input.Down, value.isPressed);
    public void OnShakeBack(InputValue value) => Local(EShakeV2Input.Back, value.isPressed);
    public void OnShakeUp(InputValue value) => Local(EShakeV2Input.Up, value.isPressed);

    void Local(EShakeV2Input input, bool pressed)
    {
        // Play에서는 라우터가 이 PlayerInput을 끄지만, 혹시 켜져 있어도 두 번 받지 않는다.
        if (routedInput || !pressed) return;
        OnShakeV2Input(input);
    }
}
