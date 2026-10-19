using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>접근 감지와 입력은 공통 시스템에 맡기고 오브젝트의 동작을 실행함.</summary>
public interface IWorldInteractionAction
{
    public bool CanExecute { get; }
    public UniTask ExecuteAsync(WorldInteractionActionContext context, CancellationToken token);
}

/// <summary>메뉴를 열어 둔 상태에서도 수동 저장을 허용하는 동작에 쓰임.</summary>
public interface IWorldInteractionSavePolicy
{
    public bool CanSave { get; }
}

public readonly struct WorldInteractionActionContext
{
    public readonly IInteractor Player;
    public readonly Func<Step[], Transform, CancellationToken, UniTask<StoryExecutionResult>> PlayAutomaticDialogue;
    public readonly Func<CancellationToken, UniTask> PlayNight;
    public readonly Action RefreshConditions;
    public readonly Func<NewStreetOptionData[], string, CancellationToken, UniTask<NewStreetOptionData>> ChooseMenu;

    public WorldInteractionActionContext(IInteractor player,
        Func<Step[], Transform, CancellationToken, UniTask<StoryExecutionResult>> playAutomaticDialogue,
        Func<CancellationToken, UniTask> playNight = null, Action refreshConditions = null,
        Func<NewStreetOptionData[], string, CancellationToken, UniTask<NewStreetOptionData>> chooseMenu = null)
    {
        Player = player;
        PlayAutomaticDialogue = playAutomaticDialogue;
        PlayNight = playNight;
        RefreshConditions = refreshConditions;
        ChooseMenu = chooseMenu;
    }
}
