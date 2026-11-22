using VContainer;
using VContainer.Unity;

/// <summary>
/// Outside 씬 전용 VContainer 스코프.
/// 카메라, CSV 씬 컨텍스트, 새 상호작용 Controller를 등록함.
/// </summary>
public class OutsideGameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<CameraControllerNew>()
        .As<ICameraControlNew>();

        builder.RegisterComponentInHierarchy<WorldInteractionSceneContext>();
        builder.RegisterComponentInHierarchy<DialogueRunner>();
        builder.Register<ShopStoryEvents>(Lifetime.Scoped).As<IOutsideStoryEventHandler>();
        builder.Register<NeighborhoodStoryEvents>(Lifetime.Scoped).As<IOutsideStoryEventHandler>();
        builder.Register<StreetStoryEvents>(Lifetime.Scoped).As<IOutsideStoryEventHandler>();
        builder.Register<OutsideStoryEvents>(Lifetime.Scoped).As<IOutsideStoryEvents>();
        // 외부·홈에 배치된 새 상호작용만 등록함. 바는 전용 스코프를 사용함.
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var interaction in root.GetComponentsInChildren<WorldInteractionController>(true))
                builder.RegisterComponent(interaction);
        builder.RegisterComponentInHierarchy<WorldInteractionView>();
        builder.RegisterComponentInHierarchy<WorldAmbientDialogueController>();
        builder.RegisterComponentInHierarchy<VerticalCameraImpulse>().As<ICameraImpulse>();

    }
}
