using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 프로젝트 전역 VContainer 루트 스코프.
/// CutSceneManager, PlayerData, SoundManager, DisplaySettings, NewDataLoadManager 등
/// 모든 씬에서 공유되는 싱글톤들을 등록한다.
/// </summary>
public class ProjectLifetimeScope : LifetimeScope
{
    [SerializeField] PlayerDataSO playerData;
    [SerializeField] PlayerSettlement playerSettlementData;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<CutSceneManager>()
       .AsSelf()
       .As<IEffectPlayer>()
       .As<ICutScenePlayer>();
        
        builder.RegisterInstance(playerData)
                      .AsSelf()
                      .As<IPlayerDataReader>()
                      .As<IPlayerDataWriter>();

        builder.RegisterInstance(playerSettlementData)
                        .AsSelf()
                        .As<ISettlementLog>();

        builder.RegisterComponentInHierarchy<SoundManager>()
        .As<ISoundManager>();

        builder.RegisterComponentInHierarchy<UIDisplayOptions>();
        builder.RegisterComponentInHierarchy<UICashPanel>();
        builder.Register<DisplaySettings>(Lifetime.Singleton)
            .AsImplementedInterfaces();

        builder.RegisterComponentInHierarchy<SceneTransitionManager>()
            .As<ISceneTransitionService>()
            .As<ISceneFadeService>();

        // 데이터 로더는 하나다. 구형 DataLoadManager는 걷어냈고, 그것이 채우던 SO 중 남은 것
        // (칵테일·컷씬·등급표·표정·태그)까지 이쪽이 채운다.
        builder.RegisterComponentInHierarchy<NewDataLoadManager>()
            .AsSelf()
            .AsImplementedInterfaces();
        //치우 수정
        builder.RegisterInstance(GameStateManager.Instance);
        builder.Register<GameProgressionService>(Lifetime.Singleton)
            .AsSelf()
            .As<IGameProgressionService>();
        builder.Register<SaveManager>(Lifetime.Singleton);
        builder.Register<IConditionUtil, ConditionUtil>(Lifetime.Singleton);
    }
}
