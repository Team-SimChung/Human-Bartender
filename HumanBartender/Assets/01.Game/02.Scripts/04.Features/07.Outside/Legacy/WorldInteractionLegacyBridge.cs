using UnityEngine;

/// <summary>기존 엔티티와 새 상호작용 사이의 소유권·포커스 호환 처리에 쓰임.</summary>
internal static class WorldInteractionLegacyBridge
{
    internal static bool OwnsAction(MonoBehaviour entity)
    {
        if (entity == null) return false;
        var target = entity.GetComponentInParent<WorldInteractionTarget>(true);
        // 비활성 대상도 기존 입력 경로에서 다시 실행되지 않게 함.
        return target != null && target.Output == WorldInteractionOutput.Action && target.Action != null;
    }

    internal static void SetFocus(MonoBehaviour action, bool focused)
    {
        if (action is not InteractiveEntity entity) return;
        if (focused) entity.OnFocusEnter();
        else entity.OnFocusExit();
    }
}
