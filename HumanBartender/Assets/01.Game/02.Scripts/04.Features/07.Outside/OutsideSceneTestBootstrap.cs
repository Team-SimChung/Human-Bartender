using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.SceneManagement;
#endif

/// <summary>임시 OutSide 직접 실행 설정. 제거 시 이 컴포넌트와 스크립트만 삭제한다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-20000)]
public sealed class OutsideSceneTestBootstrap : MonoBehaviour
{
    public enum TestPhase { CommuteIn, CommuteOut }

    [SerializeField] private bool enableTest = true;
    [Min(0)] [SerializeField] private int testDay = 0;
    [SerializeField] private TestPhase testPhase = TestPhase.CommuteIn;

#if UNITY_EDITOR
    private static bool initialSceneLoading;
    private static bool applied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BeginSession()
    {
        initialSceneLoading = true;
        applied = false;
        SceneManager.sceneLoaded -= OnInitialSceneLoaded;
        SceneManager.sceneLoaded += OnInitialSceneLoaded;
    }

    private static void OnInitialSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        initialSceneLoading = false;
        SceneManager.sceneLoaded -= OnInitialSceneLoaded;
    }

    private void Awake()
    {
        // 첫 씬의 Awake에서만 허용. 이후 정상 출퇴근으로 들어온 씬은 건드리지 않는다.
        if (!enableTest || !initialSceneLoading || applied || gameObject.scene.name != "OutSide") return;

        var state = GameStateManager.Instance;
        state.CurrentDay = Mathf.Max(0, testDay);
        state.GameFlow = testPhase == TestPhase.CommuteIn ? EGameFlow.CommuteIn : EGameFlow.CommuteOut;
        state.CurrentGameState = GameState.Play;
        applied = true;
        Debug.LogWarning($"[Outside Test] 직접 실행: Day {state.CurrentDay}, {state.GameFlow}. " +
                         "OutsideSceneTestBootstrap 컴포넌트를 제거하면 테스트 설정이 해제됩니다.", this);
    }
#endif
}
