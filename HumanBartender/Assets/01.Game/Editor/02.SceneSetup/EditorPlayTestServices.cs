using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using VContainer;
using VContainer.Diagnostics;
using VContainer.Unity;

/// <summary>에디터 플레이 도구가 원본 플레이어와 저장 슬롯을 변경하지 않도록 등록을 교체한다.</summary>
internal static class EditorPlayTestServices
{
    internal static void ValidateProjectScope(string feature)
    {
        var settings = PlayerSettings.GetPreloadedAssets().OfType<VContainerSettings>().FirstOrDefault();
        if (settings == null || settings.RootLifetimeScope is not ProjectLifetimeScope root || !root.autoRun)
            throw new InvalidOperationException($"[{feature}] Project Settings의 Preloaded Assets에서 VContainerSettings와 " +
                "자동 실행되는 ProjectLifetimeScope 연결을 확인하세요.");
    }

    internal static void ConfigurePlayMode(string sessionPrefix, SceneAsset main)
    {
        var options = EditorSettings.enterPlayModeOptions;
        const EnterPlayModeOptions skipReload = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        bool restore = EditorSettings.enterPlayModeOptionsEnabled && (options & skipReload) != 0;
        SessionState.SetBool(sessionPrefix + "RestorePlayModeOptions", restore);
        if (restore)
        {
            SessionState.SetInt(sessionPrefix + "PreviousPlayModeOptions", (int)options);
            EditorSettings.enterPlayModeOptions = options & ~skipReload;
        }
        EditorSceneManager.playModeStartScene = main;
    }

    internal static void RestorePlayModeOptions(string sessionPrefix)
    {
        if (!SessionState.GetBool(sessionPrefix + "RestorePlayModeOptions", false)) return;
        EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(sessionPrefix + "PreviousPlayModeOptions", 0);
        SessionState.EraseBool(sessionPrefix + "RestorePlayModeOptions");
        SessionState.EraseInt(sessionPrefix + "PreviousPlayModeOptions");
    }

    internal static void Replace(IContainerBuilder builder, PlayerDataSO player,
        Func<IObjectResolver, SaveManager> createSaves)
    {
        var replacements = new ContainerBuilder();
        var playerRegistration = replacements.RegisterInstance(player).AsSelf().As<IPlayerDataReader>().As<IPlayerDataWriter>();
        var savesRegistration = replacements.Register(createSaves, Lifetime.Singleton);
        ReplaceRegistration(builder, typeof(PlayerDataSO), playerRegistration);
        ReplaceRegistration(builder, typeof(SaveManager), savesRegistration);
        if (builder.Diagnostics == null) return;
        builder.Diagnostics.Clear();
        for (int i = 0; i < builder.Count; i++)
            builder.Diagnostics.TraceRegister(new RegisterInfo(builder[i]));
    }

    private static void ReplaceRegistration(IContainerBuilder builder, Type service, RegistrationBuilder replacement)
    {
        int index = -1;
        for (int i = 0; i < builder.Count; i++)
        {
            var registration = builder[i].Build();
            if (registration.ImplementationType != service && registration.InterfaceTypes?.Contains(service) != true) continue;
            if (index >= 0) throw new InvalidOperationException("[EditorPlayTest] 교체할 서비스가 중복 등록되어 있습니다: " + service.Name);
            index = i;
        }
        if (index < 0) throw new InvalidOperationException("[EditorPlayTest] 교체할 기존 서비스를 찾을 수 없습니다: " + service.Name);
        builder[index] = replacement;
    }
}
