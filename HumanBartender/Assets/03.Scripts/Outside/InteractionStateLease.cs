using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>현재 상호작용 소유자만 플레이어 입력을 복원한다.</summary>
public sealed class InteractionStateLease : IDisposable
{
    static readonly Dictionary<IInteractor, InteractionStateLease> owners = new();
    readonly IInteractor player;
    readonly EInteractorState previous;
    readonly EInteractorState held;
    InteractionStateLease(IInteractor player, EInteractorState previous, EInteractorState held) { this.player=player; this.previous=previous; this.held=held; }
    public bool IsCurrent => owners.TryGetValue(player, out var current) && current == this;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => owners.Clear();
    public static InteractionStateLease Acquire(IInteractor player, EInteractorState state = EInteractorState.Interct)
    {
        if (player == null) throw new ArgumentNullException(nameof(player));
        var oldState=owners.TryGetValue(player,out var old) ? old.previous : player.State;
        var lease=new InteractionStateLease(player,oldState,state);
        owners[player]=lease;
        player.State=state;
        return lease;
    }
    public void Dispose()
    {
        if (!owners.TryGetValue(player,out var current) || current != this) return;
        owners.Remove(player);
        if ((player is not UnityEngine.Object obj || obj != null) && player.State == held) player.State=previous;
    }
}
