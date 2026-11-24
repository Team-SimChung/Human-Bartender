using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using HumanBartender.CutsceneStudio;

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

/// <summary>동작의 강제 중지 가능 여부와 ESC·패드 취소를 무시할 구간을 제공함.</summary>
public interface IWorldInteractionCancellationPolicy
{
    public bool CanCancel { get; }
    // 기본적으로 중지 불가 구간은 입력도 무시한다. 일반 엘리베이터 이동은 설정창을 허용한다.
    public bool BlocksEscape => !CanCancel;
}

public readonly struct WorldInteractionActionContext
{
    public readonly IInteractor Player;
    public readonly Func<Step[], Transform, CancellationToken, UniTask<StoryExecutionResult>> PlayAutomaticDialogue;
    public readonly Func<CancellationToken, UniTask> PlayNight;
    public readonly Action RefreshConditions;
    /// <summary>실행 중에 만든 선택지(저장 슬롯 목록)로 사물 메뉴를 연다. 고정 메뉴는 ChooseOutsideChoices로 CSV에서 읽는다.</summary>
    public readonly Func<NewStreetOptionData[], string, CancellationToken, UniTask<NewStreetOptionData>> ChooseMenu;
    public readonly Action PrepareMorning;
    public readonly Func<CancellationToken, UniTask> FinishNight;
    public readonly Func<StudioSequence, CancellationToken, UniTask> PlayStudio;
    /// <summary>CSV 메뉴 장면(scene_id)의 선택지로 사물 메뉴를 연다. 인자: 장면 ID, 루나 위 메시지(없으면 null), 추가 선택 가능 조건, 취소.</summary>
    public readonly Func<string, string, Func<string, bool>, CancellationToken, UniTask<NewStreetOptionData>> ChooseOutsideChoices;
    /// <summary>
    /// CSV 대사를 일반 대화처럼 재생한다. 줄마다 E·Enter·대화 진행 키나 말풍선 클릭을 기다린다.
    /// 인자: 대사, 루나가 아닌 화자의 말풍선 기준점(없으면 null), 취소. 2초 자동 진행은 PlayAutomaticDialogue.
    /// </summary>
    public readonly Func<Step[], Transform, CancellationToken, UniTask<StoryExecutionResult>> PlayDialogue;

    public WorldInteractionActionContext(IInteractor player,
        Func<Step[], Transform, CancellationToken, UniTask<StoryExecutionResult>> playAutomaticDialogue,
        Func<CancellationToken, UniTask> playNight = null, Action refreshConditions = null,
        Func<NewStreetOptionData[], string, CancellationToken, UniTask<NewStreetOptionData>> chooseMenu = null,
        Action prepareMorning = null, Func<CancellationToken, UniTask> finishNight = null,
        Func<StudioSequence, CancellationToken, UniTask> playStudio = null,
        Func<string, string, Func<string, bool>, CancellationToken, UniTask<NewStreetOptionData>> chooseOutsideChoices = null,
        Func<Step[], Transform, CancellationToken, UniTask<StoryExecutionResult>> playDialogue = null)
    {
        Player = player;
        PlayAutomaticDialogue = playAutomaticDialogue;
        PlayNight = playNight;
        RefreshConditions = refreshConditions;
        ChooseMenu = chooseMenu;
        PrepareMorning = prepareMorning;
        FinishNight = finishNight;
        PlayStudio = playStudio;
        ChooseOutsideChoices = chooseOutsideChoices;
        PlayDialogue = playDialogue;
    }
}
