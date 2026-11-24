using System;
using UnityEngine;
using UnityEngine.InputSystem;

public interface ICraftInputReceiver
{
    void SetRoutedInput(bool routed);
}

public interface ICraftPointerInput : ICraftInputReceiver
{
    void OnPointerInput(bool pressed);
}

/// <summary>클릭과 Space를 같은 누름/뗌으로 받는 경로형 셰이커.</summary>
public interface ICraftShakeInput : ICraftPointerInput { }

public interface ICraftCapInput : ICraftInputReceiver
{
    void OnCapInput();
}

public interface ICraftStirInput : ICraftInputReceiver
{
    void OnStirInput(EStirDirection direction);
}

/// <summary>쉐이킹 V2. 맵 ShakingV2의 네 액션을 누른 순간만 받는다.</summary>
public interface ICraftShakeV2Input : ICraftInputReceiver
{
    void OnShakeV2Input(EShakeV2Input input);
}

/// <summary>Play 씬의 PlayerInput 하나에서 현재 기믹으로만 입력을 전달한다.</summary>
public sealed class CraftGimmickInputRouter : MonoBehaviour
{
    [SerializeField] private PlayerInput playerInput;

    internal const string GimmickMap = "CraftGimmick";
    /// <summary>쉐이킹 V2 전용 맵. 그 기믹이 바인드된 동안만 켠다(D·A·E가 젓기 키와 겹친다).</summary>
    internal const string ShakeV2Map = "ShakingV2";

    private ICraftPointerInput pointerReceiver;
    private ICraftCapInput capReceiver;
    private ICraftStirInput stirReceiver;
    private ICraftShakeV2Input shakeV2Receiver;
    private bool pointerHeld, shakeHeld, forwardedHeld;

    /// <summary>제조 중 라우터가 켜 두려는 기믹 맵. PlayerInput이 없는 테스트에서도 확인할 수 있게 따로 둔다.</summary>
    internal string GimmickActionMap { get; private set; } = GimmickMap;

    public void Begin()
    {
        if (playerInput == null || playerInput.actions == null ||
            playerInput.actions.FindActionMap(GimmickMap, false) == null)
            throw new InvalidOperationException("[CraftInput] PlayerInput과 CraftGimmick 액션 맵을 연결해 주세요.");

        UseGimmickMap(GimmickMap);
    }

    public void Bind(GameObject instance)
    {
        Unbind();
        if (instance == null) throw new ArgumentNullException(nameof(instance));

        // 독립 미니게임 씬용 PlayerInput은 Play에서는 사용하지 않는다.
        foreach (PlayerInput localInput in instance.GetComponentsInChildren<PlayerInput>(true))
            localInput.enabled = false;

        pointerReceiver = instance.GetComponentInChildren<ICraftPointerInput>(true);
        capReceiver = instance.GetComponentInChildren<ICraftCapInput>(true);
        stirReceiver = instance.GetComponentInChildren<ICraftStirInput>(true);
        shakeV2Receiver = instance.GetComponentInChildren<ICraftShakeV2Input>(true);
        if (shakeV2Receiver != null && playerInput != null && playerInput.actions != null &&
            playerInput.actions.FindActionMap(ShakeV2Map, false) == null)
            throw new InvalidOperationException("[CraftInput] PlayerInput 액션에 ShakingV2 맵이 없습니다.");

        pointerReceiver?.SetRoutedInput(true);
        capReceiver?.SetRoutedInput(true);
        stirReceiver?.SetRoutedInput(true);
        shakeV2Receiver?.SetRoutedInput(true);
        // D·A·E가 젓기 키와 겹치므로 쉐이킹 V2가 바인드된 동안만 맵을 바꾼다. Unbind에서 되돌린다.
        if (shakeV2Receiver != null) UseGimmickMap(ShakeV2Map);
    }

    public void Unbind()
    {
        pointerHeld = shakeHeld = forwardedHeld = false;
        pointerReceiver?.OnPointerInput(false);
        pointerReceiver?.SetRoutedInput(false);
        capReceiver?.SetRoutedInput(false);
        stirReceiver?.SetRoutedInput(false);
        shakeV2Receiver?.SetRoutedInput(false);
        pointerReceiver = null;
        capReceiver = null;
        stirReceiver = null;
        if (shakeV2Receiver == null) return;
        shakeV2Receiver = null;
        UseGimmickMap(GimmickMap);
    }

    public void End()
    {
        Unbind();
        if (playerInput != null && playerInput.actions != null)
            playerInput.SwitchCurrentActionMap("Play");
    }

    void UseGimmickMap(string map)
    {
        GimmickActionMap = map;
        if (playerInput != null && playerInput.actions != null) playerInput.SwitchCurrentActionMap(map);
    }

    public void OnPointerPress(InputValue value)
    {
        RoutePointer(value.isPressed, false);
    }

    public void OnShakePress(InputValue value) => RoutePointer(value.isPressed, true);

    internal void RoutePointer(bool pressed, bool fromKeyboard)
    {
        if (fromKeyboard && pointerReceiver is not ICraftShakeInput) return;
        if (pointerReceiver is not ICraftShakeInput)
        {
            pointerReceiver?.OnPointerInput(!UIDisplayOptions.IsOptionOpen && !GamePause.IsPaused && pressed);
            return;
        }
        if (UIDisplayOptions.IsOptionOpen || GamePause.IsPaused) { ReleasePointer(); return; }
        if (fromKeyboard) shakeHeld = pressed;
        else pointerHeld = pressed;
        bool held = pointerHeld || shakeHeld;
        if (held == forwardedHeld) return;
        forwardedHeld = held;
        pointerReceiver.OnPointerInput(held);
    }

    void Update()
    {
        if (UIDisplayOptions.IsOptionOpen || GamePause.IsPaused) ReleasePointer();
    }

    void ReleasePointer()
    {
        pointerHeld = shakeHeld = false;
        if (!forwardedHeld) return;
        forwardedHeld = false;
        pointerReceiver?.OnPointerInput(false);
    }

    public void OnCapPress(InputValue value)
    {
        if (!UIDisplayOptions.IsOptionOpen && value.isPressed) capReceiver?.OnCapInput();
    }

    public void OnStirUp(InputValue value)
    {
        if (!UIDisplayOptions.IsOptionOpen && value.isPressed) stirReceiver?.OnStirInput(EStirDirection.Up);
    }

    public void OnStirRight(InputValue value)
    {
        if (!UIDisplayOptions.IsOptionOpen && value.isPressed) stirReceiver?.OnStirInput(EStirDirection.Right);
    }

    public void OnStirDown(InputValue value)
    {
        if (!UIDisplayOptions.IsOptionOpen && value.isPressed) stirReceiver?.OnStirInput(EStirDirection.Down);
    }

    public void OnStirLeft(InputValue value)
    {
        if (!UIDisplayOptions.IsOptionOpen && value.isPressed) stirReceiver?.OnStirInput(EStirDirection.Left);
    }

    public void OnShakeSeal(InputValue value) => RouteShakeV2(EShakeV2Input.Seal, value.isPressed);
    public void OnShakeDown(InputValue value) => RouteShakeV2(EShakeV2Input.Down, value.isPressed);
    public void OnShakeBack(InputValue value) => RouteShakeV2(EShakeV2Input.Back, value.isPressed);
    public void OnShakeUp(InputValue value) => RouteShakeV2(EShakeV2Input.Up, value.isPressed);

    /// <summary>누른 순간만 넘긴다. 설정창이 열렸거나 게임이 멈춘 동안의 입력은 버린다.</summary>
    internal void RouteShakeV2(EShakeV2Input input, bool pressed)
    {
        if (!pressed || UIDisplayOptions.IsOptionOpen || GamePause.IsPaused) return;
        shakeV2Receiver?.OnShakeV2Input(input);
    }
}
