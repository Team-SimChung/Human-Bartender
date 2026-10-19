using VContainer;
using VContainer.Unity;

/// <summary>
/// Outside 씬 전용 VContainer 스코프.
/// CameraControllerNew, InteractiveEntityManager, OustideTimelineManager를 등록한다.
/// </summary>
public class OutsideGameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<CameraControllerNew>()
        .As<ICameraControlNew>();

        builder.RegisterComponentInHierarchy<InteractiveEntityManager>();
        builder.RegisterComponentInHierarchy<UIDialogueTextView>();
        builder.RegisterComponentInHierarchy<OustideTimelineManager>()
            .As<IOutsideTimeliner>();
    }
}
