using UnityEngine.InputSystem;

/// <summary>상호작용 세션에서 쓰는 키보드·패드 입력을 읽음. 실제 실행은 Controller가 담당함.</summary>
internal static class WorldInteractionInput
{
    internal static bool EscapePressed => Keyboard.current?.escapeKey.wasPressedThisFrame == true;
    internal static bool CancelPressed => Gamepad.current?.buttonEast.wasPressedThisFrame == true;
    internal static bool AdvancePressed => GameKeyBindings.WasPressed(GameControl.Interact) ||
        Keyboard.current?.enterKey.wasPressedThisFrame == true || Gamepad.current?.buttonSouth.wasPressedThisFrame == true;

    internal static int ReadChoiceDirection()
    {
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        if (keyboard?.upArrowKey.wasPressedThisFrame == true || keyboard?.wKey.wasPressedThisFrame == true ||
            gamepad?.dpad.up.wasPressedThisFrame == true) return -1;
        if (keyboard?.downArrowKey.wasPressedThisFrame == true || keyboard?.sKey.wasPressedThisFrame == true ||
            gamepad?.dpad.down.wasPressedThisFrame == true) return 1;
        return 0;
    }
}
