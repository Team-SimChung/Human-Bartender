using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>
/// Play 씬 진입 시 1부(TycoonFlow)와 2부(StoryFlow)를 순서대로 실행하는 중간관리자.
///
/// 2부는 원래 구형 비주얼노벨 경로가 맡았다. 그 경로는 걷어냈고, 지금은 script/bar/dayN.json을
/// 읽는 StoryFlow가 맡는다.
/// 각 Flow는 이 컨트롤러가 호출할 때만 시작되며, 완료(RunAsync 반환) 시 다음 국면으로 넘어간다.
/// </summary>
public class PlayPhaseController : MonoBehaviour
{
    [SerializeField] TycoonFlow tycoonFlow;

    [Tooltip("2부 대본 국면. script/bar/dayN.json을 실행한다.")]
    [SerializeField] StoryFlow storyFlow;

    // ── 국면별 화면 ────────────────────────────────────────────────
    //
    // 어느 국면에 무엇이 보이는지는 국면을 넘기는 이곳이 정한다. 각 Flow가 상대편 화면을 끄게 하면
    // 1부는 2부 화면을, 2부는 1부 화면을 알아야 해서 둘이 서로를 붙들게 된다.

    [Header("국면별 화면")]
    [Tooltip("1부에만 보이는 것. 손님 자리(Tycoon Resource), 코스터 트레이 캔버스, 제조 슬라이드 패널 캔버스.")]
    [SerializeField] GameObject[] tycoonOnlyObjects;

    [Tooltip("2부에만 보이는 것. 좌석 인물(Story Resource).")]
    [SerializeField] GameObject[] storyOnlyObjects;

    [Header("Test")]
    [Tooltip("켜면 1부를 건너뛰고 곧장 2부를 연다. 2부 대본만 확인할 때 쓴다. " +
             "일차는 바꾸지 않는다 — 아래 Test Day가 따로 정한다.")]
    [SerializeField] bool skipTycoonForTest;

    [Tooltip("테스트용 진행 일차. 음수면 실제 일차를 그대로 둔다. " +
             "0일차도 실제로 쓰는 값이라(손님이 없어 1부를 건너뛰고 바로 2부로 간다) 0을 '끄기'로 쓸 수 없어 " +
             "음수를 비활성 값으로 둔다. 일차가 바뀌면 등장 손님과 해금 칵테일이 통째로 달라진다.")]
    [SerializeField] int testDay = -1;

    public EPlayPhase CurrentPhase { get; private set; } = EPlayPhase.Tycoon;

    /// <summary>
    /// 테스트 일차를 Start보다 먼저 반영한다.
    ///
    /// 일차를 읽는 쪽(손님 대기열·해금 칵테일·그날 대본)은 모두 Start 이후에 묻기 때문에 여기서 정하면
    /// 늦지 않는다. 정하는 곳을 하나로 두는 것이 요점이다 — 여럿이면 어느 쪽이 이겼는지 알 수 없다.
    /// </summary>
    private void Awake()
    {
        if (testDay < 0) return;

        GameStateManager.Instance.CurrentDay = testDay;
        Debug.LogWarning($"[PlayPhase] 테스트 설정으로 진행 일차를 {testDay}일차로 바꿨습니다. " +
                         "실제 일차로 돌리려면 PlayPhaseController의 Test Day를 음수로 두세요.");
    }

    private void Start()
    {
        RunDayAsync().Forget();
    }

    public void OpenBar()
    {
        RunDayAsync().Forget();
    }

    private async UniTask RunDayAsync()
    {
        if (skipTycoonForTest)
        {
            // 조용히 건너뛰지 않는다. 손님이 하나도 안 오는 것과 이 스위치가 켜진 것은 화면에서 똑같이
            // 보여서, 남기지 않으면 "왜 1부가 안 뜨지"를 코드에서 찾게 된다.
            Debug.LogWarning("[PlayPhase] 테스트 설정으로 1부를 건너뜁니다. " +
                             "PlayPhaseController의 Skip Tycoon For Test를 끄면 원래대로 돌아옵니다.");
        }
        else
        {
            CurrentPhase = EPlayPhase.Tycoon;
            ShowPhase(tycoon: true);
            await tycoonFlow.RunAsync();
        }

        CurrentPhase = EPlayPhase.Dialogue;
        ShowPhase(tycoon: false);
        await storyFlow.RunAsync();
    }

    /// <summary>
    /// 지금 국면의 화면만 남긴다.
    ///
    /// 1부 화면을 2부에서 끄는 것이 핵심이다 — 코스터 트레이와 제조 패널은 Screen Space 캔버스라
    /// 그냥 두면 대사 위에 그대로 얹히고, 클릭도 먼저 먹는다.
    /// </summary>
    void ShowPhase(bool tycoon)
    {
        SetActive(tycoonOnlyObjects, tycoon);
        SetActive(storyOnlyObjects, !tycoon);
    }

    static void SetActive(GameObject[] objects, bool on)
    {
        if (objects == null) return;

        foreach (var target in objects)
        {
            if (target != null) target.SetActive(on);
        }
    }
}
