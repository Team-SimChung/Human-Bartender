using VContainer;
using VContainer.Unity;

/// <summary>
/// 인게임(Bar) 씬 전용 VContainer 스코프.
/// CameraControllerNew, PlayCamera, DialogueCharacterManager를 인터페이스로 등록한다.
/// </summary>
public class InGameLifetimeScope : LifetimeScope
{
    private CutSceneManager _cutSceneManager;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<CameraControllerNew>()
         .As<ICameraControlNew>();

        builder.RegisterComponentInHierarchy<PlayCamera>()
         .As<ISlotCamera>();

        builder.RegisterComponentInHierarchy<DialogueCharacterManager>()
            .As<ICharacterSetter>()
            .As<IDialogueFader>();

        // DialogueRunner는 여기서 등록하지 않는다. Play에서 그것을 여는 곳(VisualNovelFlow)을 걷어냈고,
        // 붙일 IDialoguePresenter 구현(DialogueSceneDirector)도 함께 없앴다. 실외 씬은 그대로 쓴다.
        //
        // 트리거 매니저와 커맨드도 걷어냈다. 그것을 부르는 데이터(triggers)가 한 건도 남지 않았다.

        builder.RegisterComponentInHierarchy<PlayPhaseController>();

        // 2부 대본 국면. [Inject]로 플레이어 데이터와 사운드를 받는다.
        builder.RegisterComponentInHierarchy<StoryFlow>();

        // 2부 화면. 자리 수에 맞춰 화면을 잡아야 해서 카메라를 [Inject]로 받는다(2부 명세 §10.1.1).
        // StoryFlow가 인스펙터로 꽂아 두는 것과 같은 객체다 — 여기서는 주입만 걸어 준다.
        builder.RegisterComponentInHierarchy<BarStoryPresenter>();

        // 1부 제조 루프. 기믹 큐가 띄우는 미니게임에도 사운드·데이터를 주입해 줘야 해서
        // 실행기가 IObjectResolver를 받을 수 있도록 스코프에 올린다.
        builder.RegisterComponentInHierarchy<GimmickRunner>();
        builder.RegisterComponentInHierarchy<CraftFlowController>();

        // 손님 주문 세션이 끝날 때 확정된 매출을 프로젝트 전역 PlayerData에 반영한다.
        builder.RegisterComponentInHierarchy<GuestManager>();

        //builder.RegisterBuildCallback(container =>
        //{
        //    _cutSceneManager = FindAnyObjectByType<CutSceneManager>();

        //    if (_cutSceneManager != null)
        //    {
        //        var zoom = container.Resolve<ICameraControl>();

        //        _cutSceneManager.SetSceneDependencies(zoom);
        //    }
        //});
    }
}
