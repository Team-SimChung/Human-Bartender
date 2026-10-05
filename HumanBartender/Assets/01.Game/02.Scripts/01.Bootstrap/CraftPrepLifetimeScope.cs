using VContainer;
using VContainer.Unity;

/// <summary>
/// 제조 준비 테스트 씬(CraftPrep.unity) 전용 스코프.
///
/// GimmickRunner가 [Inject]로 IObjectResolver를 받아야 기믹 안쪽(셰이킹의 사운드 등)에도 주입이
/// 이어진다. 그래서 이 둘만 스코프에 올린다 — InGameLifetimeScope를 그대로 쓰면 테스트 씬에 없는
/// 컴포넌트까지 등록하게 된다.
///
/// ISoundManager 같은 공용 의존은 부모인 ProjectLifetimeScope에서 내려온다.
/// </summary>
public class CraftPrepLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<GimmickRunner>();
        builder.RegisterComponentInHierarchy<CraftFlowController>();
    }
}
