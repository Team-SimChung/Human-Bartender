using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>
/// Play 씬 2부: 단골과의 대화 국면(2부 운영 명세 §5).
///
/// 1부가 끝나 좌석이 모두 빈 뒤 PlayPhaseController가 부른다. 하는 일은 그날의 바 대본을 찾아
/// 실행기에 넘기고 끝날 때까지 기다리는 것까지다.
///
/// 구형 비주얼노벨 경로(VisualNovelFlow)를 대신한다. 그쪽은 2026-09-11에 걷어냈다.
/// </summary>
public class StoryFlow : MonoBehaviour, IPlayPhaseFlow
{
    [Header("Scene")]
    [SerializeField] StoryScriptRunner runner;

    [Tooltip("이 화면에 대본을 그리는 구현체(IStoryPresenter). 바에서는 BarStoryPresenter를 꽂는다. " +
             "공용 대화 시스템이 IDialoguePresenter를 씬마다 갈아 끼우는 것과 같은 자리다.")]
    [SerializeField] MonoBehaviour presenter;

    [Tooltip("주문·제조·서빙을 이어 주는 창구(IStoryCraftGate). StoryCraftGate를 꽂는다. " +
             "비우면 order·craft·serve 스텝에서 오류가 나고 그 자리에서 멈춘다.")]
    [SerializeField] MonoBehaviour craftGate;

    [Inject] ISoundManager soundManager;

    /// <summary>
    /// 컷씬 재생기(CutSceneManager). 루트 스코프에 있어 인스펙터로 꽂을 수 없으므로 주입받는다.
    /// 2부 대본의 timeline 스텝이 이것으로 연출을 재생한다.
    /// </summary>
    [Inject] ICutScenePlayer cutScenePlayer;

    /// <summary>
    /// 조건 평가기. 서빙 결과가 나오면 여기에 담겨 후속 조건이 읽는다.
    ///
    /// 루트 스코프에 하나만 있는 것을 주입받는다. 실외 스폰 조건과 같은 것을 쓴다 —
    /// 같은 식이 곳에 따라 다르게 판정되지 않게 하려는 것이다.
    /// </summary>
    [Inject] IConditionUtil conditions;

    public async UniTask RunAsync()
    {
        int day = GameStateManager.Instance.CurrentDay;

        // 로딩이 끝나기 전에 대본을 물으면 아직 없는 날로 읽힌다.
        //
        // 로더를 인스펙터로 꽂지 않는다. 그쪽은 Play 씬이 아니라 VContainer 루트 스코프에 얹혀
        // 실행 중에 만들어져서, 씬 오브젝트가 참조할 수 있는 대상이 아니다.
        await NewDataLoadManager.WaitUntilLoadedAsync(this.GetCancellationTokenOnDestroy());

        if (!NewDataLoadManager.TryGetBarScript(day, out NewDayScriptBase script))
        {
            // 파일이 없는 날은 오류가 아니다. 그날 바 2부를 만들지 않았다는 뜻이다(§5.2).
            Debug.Log($"[Story] Day {day}의 바 대본이 없어 2부를 건너뜁니다.");
            return;
        }

        if (presenter is not IStoryPresenter storyPresenter)
        {
            Debug.LogError("[Story] presenter가 IStoryPresenter가 아닙니다. BarStoryPresenter를 꽂으세요.");
            return;
        }

        if (craftGate != null && craftGate is not IStoryCraftGate)
        {
            Debug.LogError("[Story] craftGate가 IStoryCraftGate가 아닙니다. StoryCraftGate를 꽂으세요.");
            return;
        }

        // 하루가 새로 시작하므로 지난 적용 기록을 비운다. 평가기는 루트 스코프에 하나뿐이라
        // 비우지 않으면 같은 날을 다시 열었을 때 effects가 통째로 건너뛰어진다.
        // The runner owns its execution result and effect tokens.

        runner.Bind(storyPresenter, conditions, craftGate as IStoryCraftGate, cutScenePlayer);

        GameStateManager.Instance.GameFlow = EGameFlow.Bar;
        soundManager?.PlayBGM("BGM_bar_01", 1f, true);

        Debug.Log($"[Story] Day {day} 2부 시작");

        var result = await runner.RunAsync(script, this.GetCancellationTokenOnDestroy());
        if (!result.Completed)
        {
            if (result.Status == StoryExecutionStatus.Failed) Debug.LogError($"[Story] {result.Error}");
            return;
        }

        Debug.Log($"[Story] Day {day} 2부 종료");

        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;

        await SceneTransitionManager.Instance.FadeOutAsync(2f);

        SceneTransitionManager.Instance.LoadScene("Outside");
    }

    /// <summary>
    /// 대사 진행 입력. 대본이 돌고 있을 때만 받는다.
    ///
    /// 공용 DialogueRunner와 이 실행기가 동시에 도는 일은 없지만, 입력을 보내는 쪽이 그것을 알 필요는
    /// 없게 여기서 걸러 준다.
    /// </summary>
    public bool TryAdvance()
    {
        if (runner == null || !runner.IsRunning) return false;

        runner.OnAdvanceInput();
        return true;
    }
}
