using UnityEngine.InputSystem;

/// <summary>상호작용 세션에서 쓰는 키보드·패드 입력을 읽음. 실제 실행은 Controller가 담당함.</summary>
internal static class WorldInteractionInput
{
    internal static bool EscapePressed => Keyboard.current?.escapeKey.wasPressedThisFrame == true;
    internal static bool CancelPressed => Gamepad.current?.buttonEast.wasPressedThisFrame == true;
    internal static bool AdvancePressed => DialogueAdvanceInput.PressedKey(includeInteraction: true) != null ||
        Gamepad.current?.buttonSouth.wasPressedThisFrame == true;

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

    /// <summary>마우스 휠: 위로 굴리면 이전(−1), 아래로 굴리면 다음(+1). 굴린 양은 보지 않는다.</summary>
    internal static int ReadChoiceWheel()
    {
        float scroll = Mouse.current?.scroll.ReadValue().y ?? 0;
        if (scroll == 0 || ModifierHeld) return 0;
        return scroll > 0 ? -1 : 1;
    }

    // 웹(world-choices.js wheel)처럼 Ctrl·Alt·Cmd/Win과 함께 굴린 휠(확대·시스템 단축키)은 선택지를 옮기지 않는다.
    private static bool ModifierHeld
    {
        get
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.ctrlKey.isPressed || keyboard.altKey.isPressed ||
                keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed);
        }
    }
}
