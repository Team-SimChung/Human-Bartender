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

/// <summary>Play 씬의 PlayerInput 하나에서 현재 기믹으로만 입력을 전달한다.</summary>
public sealed class CraftGimmickInputRouter : MonoBehaviour
{
    [SerializeField] private PlayerInput playerInput;

    private ICraftPointerInput pointerReceiver;
    private ICraftCapInput capReceiver;
    private ICraftStirInput stirReceiver;
    private bool pointerHeld, shakeHeld, forwardedHeld;

    public void Begin()
    {
        if (playerInput == null || playerInput.actions == null ||
            playerInput.actions.FindActionMap("CraftGimmick", false) == null)
            throw new InvalidOperationException("[CraftInput] PlayerInput과 CraftGimmick 액션 맵을 연결해 주세요.");

        playerInput.SwitchCurrentActionMap("CraftGimmick");
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
        pointerReceiver?.SetRoutedInput(true);
        capReceiver?.SetRoutedInput(true);
        stirReceiver?.SetRoutedInput(true);
    }

    public void Unbind()
    {
        pointerHeld = shakeHeld = forwardedHeld = false;
        pointerReceiver?.OnPointerInput(false);
        pointerReceiver?.SetRoutedInput(false);
        capReceiver?.SetRoutedInput(false);
        stirReceiver?.SetRoutedInput(false);
        pointerReceiver = null;
        capReceiver = null;
        stirReceiver = null;
    }

    public void End()
    {
        Unbind();
        if (playerInput != null && playerInput.actions != null)
            playerInput.SwitchCurrentActionMap("Play");
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
}
