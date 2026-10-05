using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>CSV action_ref의 컷신을 실행하며 성공한 경우에만 소비한다.</summary>
public class InteractiveTriggerEntity : InteractiveEntity
{
    [Inject] IOutsideTimeliner timeliner;
    CancellationTokenSource playback;
    string completedId;
    void OnDisable() => playback?.Cancel();
    public override bool SupportsAction(NewInteractPointData definition) =>
        definition.ActionType == EActionType.Scene && !string.IsNullOrWhiteSpace(definition.ActionRef);
    public override void Interact(IInteractor player)
    {
        if (!isInteract || !isActiveAndEnabled || playback != null || completedId == Definition?.Id) return;
        PlayAsync().Forget(e => { if (e is not OperationCanceledException) Debug.LogException(e); });
    }
    async UniTask PlayAsync()
    {
        if (timeliner == null) throw new InvalidOperationException("Outside Timeline service is missing.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        playback = source;
        isInteracting = true;
        var id = Definition?.Id;
        try
        {
            OnInteracted?.Raise(this);
            await timeliner.PlayTimelineCutSceneAsync(ActionRef, source.Token);
            source.Token.ThrowIfCancellationRequested();
            completedId = id;
            isInteract = false;
        }
        finally { playback = null; isInteracting = false; }
    }
}
