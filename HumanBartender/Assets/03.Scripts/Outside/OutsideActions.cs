using UnityEngine;

/// <summary>CSV 동작 ID와 C가 지원하는 씬 동작의 연결.</summary>
public static class OutsideActions
{
    public const string ElevatorToggle = "elevator_toggle";

    public static bool TryGetScene(string actionRef, out string scene)
    {
        scene = actionRef switch
        {
            "enter_bar" => "Play",
            "enter_home" => "Home",
            "exit_home" => "OutSide",
            _ => null
        };
        return scene != null;
    }

    public static void RequestTransition(string actionRef)
    {
        if (!TryGetScene(actionRef, out var scene) || !Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"[Outside] 이동 대상이 없거나 빌드에 등록되지 않았습니다: {actionRef}");
            return;
        }
        if (SceneTransitionManager.Instance == null)
        {
            Debug.LogError("[Outside] SceneTransitionManager가 없습니다.");
            return;
        }
        // 현재 A API는 요청 수락/완료 결과를 제공하지 않는다.
        SceneTransitionManager.Instance.LoadScene(scene);
    }
}
