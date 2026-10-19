using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>공통 상호작용에서 CSV의 출입 동작을 하루 진행 서비스로 전달함.</summary>
[RequireComponent(typeof(WorldInteractionContent))]
public sealed class WorldEntranceAction : MonoBehaviour, IWorldInteractionAction
{
    [SerializeField] private WorldInteractionContent content;
    [Inject] private IGameProgressionService progression;
    public bool CanExecute => isActiveAndEnabled && content != null && content.IsAvailable && progression != null;

    public async UniTask ExecuteAsync(WorldInteractionActionContext context, CancellationToken token)
    {
        if (!CanExecute || !content.Definition.HasValue ||
            !OutsideActions.TryGetDestination(content.Definition.Value.ActionRef, out var destination))
            throw new InvalidOperationException("현재 출입할 수 없거나 이동 목적지가 없습니다.");
        var result = await progression.EnterAsync(destination, token);
        if (result.Outcome == GameProgressionOutcome.Canceled) throw new OperationCanceledException(token);
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "장소 이동에 실패했습니다.");
    }
}
